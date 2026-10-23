// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Tests that the client validates the option offsets in a PRELOGIN response and
/// reports a parsing error, rather than reading outside the response buffer, when
/// an option header or the option data it points to lies beyond the end of the response.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class PreLoginOptionBoundsTests : IDisposable
{
    private const byte Version = 0x00;
    private const byte Encrypt = 0x01;
    private const byte Instance = 0x02;
    private const byte ThreadId = 0x03;
    private const byte Mars = 0x04;
    private const byte FedAuthRequired = 0x06;
    private const byte Terminator = 0xFF;

    private readonly PreLoginOverrideTdsServer _server;
    private readonly string _connectionString;

    public PreLoginOptionBoundsTests()
    {
        _server = new PreLoginOverrideTdsServer();
        _server.Start();
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    public void Dispose() => _server.Dispose();

    /// <summary>
    /// Control: a well-formed PRELOGIN response containing every option the client reads
    /// is accepted and the connection opens.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WellFormedResponse_Connects(bool useAsync)
    {
        _server.BuildResponse = BuildValidResponse;

        using SqlConnection connection = new(_connectionString);
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// An option whose data offset points far beyond the end of the response is rejected
    /// with a parsing error.
    /// </summary>
    [Theory]
    [InlineData(Version, false)]
    [InlineData(Version, true)]
    [InlineData(Encrypt, false)]
    [InlineData(Encrypt, true)]
    [InlineData(Instance, false)]
    [InlineData(Instance, true)]
    [InlineData(Mars, false)]
    [InlineData(Mars, true)]
    [InlineData(FedAuthRequired, false)]
    [InlineData(FedAuthRequired, true)]
    public async Task OptionOffsetBeyondResponse_ThrowsParsingError(byte option, bool useAsync)
    {
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption);
            SetOptionOffset(response, option, 0xFFFF);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex, expectedOffset: 0xFFFF);
    }

    /// <summary>
    /// An option whose data starts inside the response but does not fit before the end of
    /// the response is rejected. This checks the boundary: VERSION reads 4 bytes, so an
    /// offset 3 bytes from the end is too short; single-byte options at an offset equal to
    /// the response length are just past the end.
    /// </summary>
    [Theory]
    [InlineData(Version, 3, false)]
    [InlineData(Version, 3, true)]
    [InlineData(Encrypt, 0, false)]
    [InlineData(Encrypt, 0, true)]
    [InlineData(Instance, 0, false)]
    [InlineData(Instance, 0, true)]
    [InlineData(Mars, 0, false)]
    [InlineData(Mars, 0, true)]
    [InlineData(FedAuthRequired, 0, false)]
    [InlineData(FedAuthRequired, 0, true)]
    public async Task OptionDataTruncatedAtEndOfResponse_ThrowsParsingError(byte option, int bytesAvailable, bool useAsync)
    {
        int expectedOffset = 0;
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption);
            expectedOffset = response.Length - bytesAvailable;
            SetOptionOffset(response, option, (ushort)expectedOffset);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex, expectedOffset);
    }

    /// <summary>
    /// A response that ends in the middle of an option header is rejected.
    /// </summary>
    [Theory]
    [InlineData(Version, false)]
    [InlineData(Version, true)]
    [InlineData(Encrypt, false)]
    [InlineData(Encrypt, true)]
    [InlineData(Instance, false)]
    [InlineData(Instance, true)]
    [InlineData(Mars, false)]
    [InlineData(Mars, true)]
    [InlineData(FedAuthRequired, false)]
    [InlineData(FedAuthRequired, true)]
    public async Task OptionHeaderTruncated_ThrowsParsingError(byte option, bool useAsync)
    {
        // Option token followed by only 2 of the 4 header bytes.
        _server.BuildResponse = _ => new byte[] { option, 0x00, 0x05 };

        Exception ex = await OpenExpectingFailure(useAsync);

        // The header begins immediately after the option token, at offset 1.
        AssertCorruptedStream(ex, expectedOffset: 1);
    }

    private async Task<Exception> OpenExpectingFailure(bool useAsync)
    {
        using SqlConnection connection = new(_connectionString);
        if (useAsync)
        {
            return await Assert.ThrowsAnyAsync<InvalidOperationException>(connection.OpenAsync);
        }

        return Assert.ThrowsAny<InvalidOperationException>(connection.Open);
    }

    private static void AssertCorruptedStream(Exception ex, int expectedOffset)
    {
        Assert.IsNotType<IndexOutOfRangeException>(ex);
        Assert.Contains("Error state: 18", ex.Message); // ParsingErrorState.CorruptedTdsStream
        Assert.Contains($"Offset: {expectedOffset}", ex.Message);
    }

    /// <summary>
    /// Builds a well-formed PRELOGIN response containing VERSION, ENCRYPT, INSTANCE,
    /// THREADID, MARS and FEDAUTHREQUIRED options.
    /// </summary>
    private static byte[] BuildValidResponse(TDSPreLoginTokenEncryptionType encryption)
    {
        // The test server omits ENCRYPT when it has no encryption preference; NOT_SUP is the
        // equivalent wire value and keeps the client in agreement with the server session.
        byte encryptValue = encryption == TDSPreLoginTokenEncryptionType.None
            ? (byte)TDSPreLoginTokenEncryptionType.NotSupported
            : (byte)encryption;

        (byte Option, byte[] Data)[] options =
        {
            (Version, new byte[] { 0x10, 0x00, 0x00, 0x00, 0x00, 0x00 }),
            (Encrypt, new byte[] { encryptValue }),
            (Instance, new byte[] { 0x00 }),
            (ThreadId, new byte[] { 0x00, 0x00, 0x00, 0x00 }),
            (Mars, new byte[] { 0x00 }),
            (FedAuthRequired, new byte[] { 0x00 }),
        };

        int headerLength = options.Length * 5 + 1;
        byte[] response = new byte[headerLength + options.Sum(o => o.Data.Length)];

        int headerPosition = 0;
        int dataPosition = headerLength;
        foreach ((byte option, byte[] data) in options)
        {
            response[headerPosition++] = option;
            response[headerPosition++] = (byte)(dataPosition >> 8);
            response[headerPosition++] = (byte)dataPosition;
            response[headerPosition++] = (byte)(data.Length >> 8);
            response[headerPosition++] = (byte)data.Length;

            Buffer.BlockCopy(data, 0, response, dataPosition, data.Length);
            dataPosition += data.Length;
        }

        response[headerPosition] = Terminator;
        return response;
    }

    private static void SetOptionOffset(byte[] response, byte option, ushort offset)
    {
        for (int position = 0; response[position] != Terminator; position += 5)
        {
            if (response[position] == option)
            {
                response[position + 1] = (byte)(offset >> 8);
                response[position + 2] = (byte)offset;
                return;
            }
        }

        throw new ArgumentException($"Option {option} not found in PRELOGIN response.", nameof(option));
    }

    /// <summary>
    /// A TDS server that replaces its PRELOGIN response with caller-supplied raw bytes.
    /// </summary>
    private sealed class PreLoginOverrideTdsServer : TdsServer
    {
        public Func<TDSPreLoginTokenEncryptionType, byte[]>? BuildResponse { get; set; }

        public override TDSMessageCollection OnPreLoginRequest(ITDSServerSession session, TDSMessage request)
        {
            // Let the base server negotiate so its session state matches a normal handshake.
            TDSMessageCollection response = base.OnPreLoginRequest(session, request);

            if (BuildResponse is null)
            {
                return response;
            }

            TDSPreLoginToken token = response[0].OfType<TDSPreLoginToken>().First();
            return new TDSMessageCollection(
                new TDSMessage(TDSMessageType.Response, new RawToken(BuildResponse(token.Encryption))));
        }
    }

    private sealed class RawToken : TDSPacketToken
    {
        private readonly byte[] _bytes;

        public RawToken(byte[] bytes) => _bytes = bytes;

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination) => destination.Write(_bytes, 0, _bytes.Length);
    }
}
