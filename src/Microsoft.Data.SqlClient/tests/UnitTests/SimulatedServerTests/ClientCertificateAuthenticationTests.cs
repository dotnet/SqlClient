// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies the TDS and TLS behavior used for SQL Server on Linux loopback client-certificate authentication.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class ClientCertificateAuthenticationTests : IDisposable
{
    private const string CertificatePassword = "<pwd>";

    private static readonly byte[] s_preLoginEncryptOnResponse =
    {
        0x12, 0x01, 0x00, 0x1A, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x0B, 0x00, 0x06,
        0x01, 0x00, 0x11, 0x00, 0x01,
        0xFF,
        0x11, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01,
    };

    private static readonly byte[] s_preLoginEncryptOffResponse =
    {
        0x12, 0x01, 0x00, 0x1A, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x0B, 0x00, 0x06,
        0x01, 0x00, 0x11, 0x00, 0x01,
        0xFF,
        0x11, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00,
    };

    private static readonly byte[] s_preLoginEncryptNotSupportedResponse =
    {
        0x12, 0x01, 0x00, 0x1A, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x0B, 0x00, 0x06,
        0x01, 0x00, 0x11, 0x00, 0x01,
        0xFF,
        0x11, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02,
    };

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"SqlClientClientCertificateTests-{Guid.NewGuid():N}");

    /// <summary>
    /// Removes certificate files created by the test.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies that a certificate-authentication connection advertises the PRELOGIN flag,
    /// presents the configured certificate, and omits SQL credentials from LOGIN7.
    /// </summary>
    /// <param name="encryptionMode">Zero for Optional, one for Mandatory, or two for Strict encryption.</param>
    /// <param name="async">Whether to open the connection asynchronously.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Open_WithClientCertificate_SendsCertificateAuthenticationHandshake(int encryptionMode, bool async)
    {
        bool strict = encryptionMode == 2;
        Directory.CreateDirectory(_temporaryDirectory);
        string clientCertificatePath = Path.Combine(_temporaryDirectory, "client.pfx");
        string serverCertificatePath = Path.Combine(_temporaryDirectory, "server.cer");

        using X509Certificate2 serverCertificate = CreateCertificate(
            "CN=localhost",
            "1.3.6.1.5.5.7.3.1");
        using X509Certificate2 clientCertificate = CreateCertificate(
            "CN=SqlClient Loopback",
            "1.3.6.1.5.5.7.3.2");
        File.WriteAllBytes(
            clientCertificatePath,
            clientCertificate.Export(X509ContentType.Pkcs12, CertificatePassword));
        File.WriteAllBytes(
            serverCertificatePath,
            serverCertificate.Export(X509ContentType.Cert));

        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task<HandshakeResult> serverTask = Task.Run(
            () => RunServer(listener, serverCertificate, encryptionMode));

        using LocalAppContextSwitchesHelper switches = new();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            switches.UseManagedNetworking = true;
        }

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{port}",
            Encrypt = encryptionMode switch
            {
                0 => SqlConnectionEncryptOption.Optional,
                1 => SqlConnectionEncryptOption.Mandatory,
                _ => SqlConnectionEncryptOption.Strict,
            },
            TrustServerCertificate = true,
            ServerCertificate = strict ? serverCertificatePath : string.Empty,
            ClientCertificate = clientCertificatePath,
            ClientKeyPassword = CertificatePassword,
            ConnectTimeout = 5,
            ConnectRetryCount = 0,
            Pooling = false,
        };

        try
        {
            using SqlConnection connection = new(builder.ConnectionString);
            Exception? openException = async
                ? await Record.ExceptionAsync(() => connection.OpenAsync())
                : Record.Exception(() => connection.Open());

            HandshakeResult result;
            try
            {
                result = await serverTask;
            }
            catch (Exception serverException)
            {
                throw new AggregateException(
                    "The client and simulated server did not complete the certificate-authentication handshake.",
                    openException,
                    serverException);
            }

            Assert.IsType<SqlException>(openException);
            Assert.True(result.ClientCertificateFlagSet);
            Assert.Equal(clientCertificate.Thumbprint, result.ClientCertificateThumbprint);
            Assert.Equal(0, result.UserNameLength);
            Assert.Equal(0, result.PasswordLength);
        }
        finally
        {
            listener.Stop();
            try
            {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                // The assertions above surface the server failure with its original stack trace.
            }
        }
    }

    /// <summary>
    /// Verifies that certificate-file failures flow through the TLS authentication error path on
    /// both the synchronous and asynchronous open paths.
    /// </summary>
    /// <param name="async">Whether to open the connection asynchronously.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_WithMissingClientCertificate_ReportsAuthenticationFailure(bool async)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string missingCertificatePath = Path.Combine(_temporaryDirectory, "missing.pfx");

        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<byte[]> serverTask = Task.Run(() => CompletePreLoginOnly(listener, s_preLoginEncryptOnResponse));

        using LocalAppContextSwitchesHelper switches = new();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            switches.UseManagedNetworking = true;
        }

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{port}",
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            ClientCertificate = missingCertificatePath,
            ConnectTimeout = 5,
            ConnectRetryCount = 0,
            Pooling = false,
        };

        try
        {
            using SqlConnection connection = new(builder.ConnectionString);
            SqlException exception = async
                ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                : Assert.Throws<SqlException>(() => connection.Open());
            AuthenticationException authenticationException =
                Assert.IsType<AuthenticationException>(exception.InnerException);
            Assert.Contains(
                "client certificate or private key could not be loaded",
                authenticationException.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }

    /// <summary>
    /// Verifies that a server which declines encryption fails the connection instead of silently
    /// sending an empty, anonymous LOGIN7 record that no certificate could ever authenticate.
    /// </summary>
    /// <param name="async">Whether to open the connection asynchronously.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_WhenServerDeclinesEncryption_FailsInsteadOfAnonymousLogin(bool async)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string clientCertificatePath = Path.Combine(_temporaryDirectory, "client.pfx");

        using X509Certificate2 clientCertificate = CreateCertificate(
            "CN=SqlClient Loopback",
            "1.3.6.1.5.5.7.3.2");
        File.WriteAllBytes(
            clientCertificatePath,
            clientCertificate.Export(X509ContentType.Pkcs12, CertificatePassword));

        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<byte[]> serverTask = Task.Run(
            () => CompletePreLoginOnly(listener, s_preLoginEncryptNotSupportedResponse));

        using LocalAppContextSwitchesHelper switches = new();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            switches.UseManagedNetworking = true;
        }

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = true,
            ClientCertificate = clientCertificatePath,
            ClientKeyPassword = CertificatePassword,
            ConnectTimeout = 5,
            ConnectRetryCount = 0,
            Pooling = false,
        };

        try
        {
            using SqlConnection connection = new(builder.ConnectionString);
            SqlException exception = async
                ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                : Assert.Throws<SqlException>(() => connection.Open());
            Assert.Contains(
                "requires an encrypted connection",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);

            // The client must close without sending an unauthenticated LOGIN7 record.
            Assert.Empty(await serverTask);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }

    /// <summary>
    /// Completes PRELOGIN and TLS as a server, then inspects the decrypted LOGIN7 packet.
    /// </summary>
    /// <param name="listener">The listener accepting the test connection.</param>
    /// <param name="serverCertificate">The certificate presented by the simulated server.</param>
    /// <param name="encryptionMode">Zero for Optional, one for Mandatory, or two for Strict encryption.</param>
    /// <returns>The observed certificate-authentication protocol values.</returns>
    private static HandshakeResult RunServer(
        TcpListener listener,
        X509Certificate2 serverCertificate,
        int encryptionMode)
    {
        using TcpClient client = listener.AcceptTcpClient();
        client.ReceiveTimeout = 10_000;
        client.SendTimeout = 10_000;
        using NetworkStream networkStream = client.GetStream();

        string? clientCertificateThumbprint = null;
        if (encryptionMode == 2)
        {
            using SslStream sslStream = CreateServerSslStream(
                networkStream,
                certificate => clientCertificateThumbprint = certificate);
            AuthenticateServer(sslStream, serverCertificate, useTds8Alpn: true);

            byte[] preLoginPacket = ReadTdsPacket(sslStream);
            bool clientCertificateFlagSet = HasClientCertificateFlag(preLoginPacket);
            sslStream.Write(s_preLoginEncryptOnResponse, 0, s_preLoginEncryptOnResponse.Length);
            sslStream.Flush();

            return ReadLoginResult(
                sslStream,
                clientCertificateFlagSet,
                clientCertificateThumbprint);
        }

        byte[] classicPreLoginPacket = ReadTdsPacket(networkStream);
        bool classicClientCertificateFlagSet = HasClientCertificateFlag(classicPreLoginPacket);
        byte[] preLoginResponse = encryptionMode == 0
            ? s_preLoginEncryptOffResponse
            : s_preLoginEncryptOnResponse;
        networkStream.Write(preLoginResponse, 0, preLoginResponse.Length);
        networkStream.Flush();

        TDSStream tdsStream = new(networkStream, leaveInnerStreamOpen: true)
        {
            PacketSize = 4096,
        };
        AutoTDSStream tlsOverTds = new(tdsStream, closeInnerStream: false)
        {
            OutgoingMessageType = TDSMessageType.PreLogin,
        };
        PlaceholderStream multiplexer = new(tlsOverTds, leaveInnerStreamOpen: true);

        using SslStream classicSslStream = CreateServerSslStream(
            multiplexer,
            certificate => clientCertificateThumbprint = certificate);
        AuthenticateServer(classicSslStream, serverCertificate, useTds8Alpn: false);

        multiplexer.InnerStream = networkStream;
        return ReadLoginResult(
            classicSslStream,
            classicClientCertificateFlagSet,
            clientCertificateThumbprint);
    }

    /// <summary>
    /// Completes PRELOGIN, then records anything the client sends before closing.
    /// </summary>
    /// <param name="listener">The listener accepting the test connection.</param>
    /// <param name="preLoginResponse">The PRELOGIN response written to the client.</param>
    /// <returns>The bytes the client sent after the PRELOGIN response.</returns>
    private static byte[] CompletePreLoginOnly(TcpListener listener, byte[] preLoginResponse)
    {
        using TcpClient client = listener.AcceptTcpClient();
        client.ReceiveTimeout = 10_000;
        client.SendTimeout = 10_000;
        using NetworkStream networkStream = client.GetStream();
        ReadTdsPacket(networkStream);
        networkStream.Write(preLoginResponse, 0, preLoginResponse.Length);
        networkStream.Flush();

        using MemoryStream trailingBytes = new();
        try
        {
            int value;
            while ((value = networkStream.ReadByte()) >= 0)
            {
                trailingBytes.WriteByte((byte)value);
            }
        }
        catch (IOException)
        {
            // The expected failure closes the client socket.
        }

        return trailingBytes.ToArray();
    }

    /// <summary>
    /// Creates a server-side TLS stream that accepts and records the client certificate.
    /// </summary>
    /// <param name="transport">The TLS transport stream.</param>
    /// <param name="captureThumbprint">Receives the presented client certificate thumbprint.</param>
    /// <returns>The configured TLS stream.</returns>
    private static SslStream CreateServerSslStream(
        Stream transport,
        Action<string> captureThumbprint)
    {
        return new SslStream(
            transport,
            leaveInnerStreamOpen: true,
            (_, certificate, _, _) =>
            {
                if (certificate != null)
                {
                    using X509Certificate2 certificate2 = new(certificate);
                    captureThumbprint(certificate2.Thumbprint);
                }

                return true;
            });
    }

    /// <summary>
    /// Authenticates the simulated server with optional TDS 8.0 ALPN negotiation.
    /// </summary>
    /// <param name="sslStream">The server TLS stream.</param>
    /// <param name="serverCertificate">The certificate presented by the server.</param>
    /// <param name="useTds8Alpn">Whether to advertise the TDS 8.0 ALPN protocol.</param>
    private static void AuthenticateServer(
        SslStream sslStream,
        X509Certificate2 serverCertificate,
        bool useTds8Alpn)
    {
        SslServerAuthenticationOptions options = new()
        {
            ServerCertificate = serverCertificate,
            ClientCertificateRequired = true,
            EnabledSslProtocols = SslProtocols.Tls12,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };
        if (useTds8Alpn)
        {
            options.ApplicationProtocols = new List<SslApplicationProtocol>
            {
                new(Encoding.ASCII.GetBytes("tds/8.0")),
            };
        }

        sslStream.AuthenticateAsServer(options);
    }

    /// <summary>
    /// Reads the decrypted LOGIN7 packet and returns the observed authentication fields.
    /// </summary>
    /// <param name="sslStream">The authenticated TLS stream.</param>
    /// <param name="clientCertificateFlagSet">Whether PRELOGIN requested certificate authentication.</param>
    /// <param name="clientCertificateThumbprint">The client certificate thumbprint observed by TLS.</param>
    /// <returns>The observed certificate-authentication protocol values.</returns>
    private static HandshakeResult ReadLoginResult(
        SslStream sslStream,
        bool clientCertificateFlagSet,
        string? clientCertificateThumbprint)
    {
        byte[] loginPacket = ReadTdsPacket(sslStream);
        Assert.Equal((byte)TDSMessageType.TDS7Login, loginPacket[0]);

        ReadOnlySpan<byte> loginPayload = loginPacket.AsSpan(8);
        ushort userNameLength = BinaryPrimitives.ReadUInt16LittleEndian(loginPayload.Slice(42, 2));
        ushort passwordLength = BinaryPrimitives.ReadUInt16LittleEndian(loginPayload.Slice(46, 2));

        return new HandshakeResult(
            clientCertificateFlagSet,
            clientCertificateThumbprint,
            userNameLength,
            passwordLength);
    }

    /// <summary>
    /// Reports whether the PRELOGIN encryption option contains the client-certificate bit.
    /// </summary>
    /// <param name="preLoginPacket">The complete PRELOGIN packet.</param>
    /// <returns><see langword="true" /> when the client-certificate bit is set.</returns>
    private static bool HasClientCertificateFlag(byte[] preLoginPacket) =>
        GetPreLoginEncryptionValue(preLoginPacket) is byte encryptionValue &&
        (encryptionValue & 0x80) != 0;

    /// <summary>
    /// Reads one complete TDS packet from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the TDS packet.</param>
    /// <returns>The complete packet, including its eight-byte header.</returns>
    private static byte[] ReadTdsPacket(Stream stream)
    {
        byte[] header = new byte[8];
        ReadExactly(stream, header);
        int packetLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        Assert.True(packetLength >= header.Length);

        byte[] packet = new byte[packetLength];
        Buffer.BlockCopy(header, 0, packet, 0, header.Length);
        ReadExactly(stream, packet.AsSpan(header.Length));
        return packet;
    }

    /// <summary>
    /// Reads until the destination buffer is full or throws when the peer closes early.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="buffer">The destination buffer.</param>
    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = stream.Read(buffer.Slice(totalRead));
            if (bytesRead == 0)
            {
                throw new EndOfStreamException();
            }

            totalRead += bytesRead;
        }
    }

    /// <summary>
    /// Extracts the PRELOGIN encryption option from a complete TDS packet.
    /// </summary>
    /// <param name="packet">The complete PRELOGIN packet.</param>
    /// <returns>The encryption byte, or <see langword="null" /> when the option is absent.</returns>
    private static byte? GetPreLoginEncryptionValue(byte[] packet)
    {
        const int headerLength = 8;
        int optionPosition = headerLength;
        while (packet[optionPosition] != 0xFF)
        {
            byte option = packet[optionPosition];
            int dataOffset = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(optionPosition + 1, 2));
            int dataLength = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(optionPosition + 3, 2));

            if (option == 0x01 && dataLength == 1)
            {
                return packet[headerLength + dataOffset];
            }

            optionPosition += 5;
        }

        return null;
    }

    /// <summary>
    /// Creates a short-lived self-signed certificate for the requested TLS purpose.
    /// </summary>
    /// <param name="subjectName">The certificate subject.</param>
    /// <param name="enhancedKeyUsageOid">The server- or client-authentication OID.</param>
    /// <returns>A certificate with an exportable private key.</returns>
    private static X509Certificate2 CreateCertificate(string subjectName, string enhancedKeyUsageOid)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new(
            subjectName,
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        OidCollection usages = new() { new Oid(enhancedKeyUsageOid) };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));
        using X509Certificate2 ephemeralCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5));
        byte[] certificateBytes = ephemeralCertificate.Export(X509ContentType.Pkcs12);
        try
        {
#pragma warning disable SYSLIB0057
            return new X509Certificate2(
                certificateBytes,
                (string?)null,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
        }
        finally
        {
            CryptographicOperations.ZeroMemory(certificateBytes);
        }
    }

    /// <summary>
    /// Captures the certificate-authentication values observed by the simulated server.
    /// </summary>
    private sealed record HandshakeResult(
        bool ClientCertificateFlagSet,
        string? ClientCertificateThumbprint,
        ushort UserNameLength,
        ushort PasswordLength);
}

#endif
