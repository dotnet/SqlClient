// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Parser;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SNI connection handle
    /// </summary>
    internal abstract class SniHandle
    {
        protected static readonly SslProtocols s_supportedProtocols = SslProtocols.None;

        protected static readonly List<SslApplicationProtocol> s_tdsProtocols = new List<SslApplicationProtocol>(1) { new(TdsEnums.TDS8_Protocol) };

        private static async Task AuthenticateAsClientAsync(SslStream sslStream, string serverNameIndication, X509CertificateCollection certificate, RemoteCertificateValidationCallback validationCallback, CancellationToken token)
        {
            SslClientAuthenticationOptions sslClientOptions = new()
            {
                TargetHost = serverNameIndication,
                ApplicationProtocols = s_tdsProtocols,
                ClientCertificates = certificate,
                RemoteCertificateValidationCallback = validationCallback
            };
            await sslStream.AuthenticateAsClientAsync(sslClientOptions, token);
        }

        /// <summary>
        /// Performs the client TLS handshake, raising <paramref name="validationCallback"/> to
        /// validate the server certificate for this handshake only.
        /// </summary>
        /// <param name="sslStream">The stream to authenticate. It must not have a validation callback of its own.</param>
        /// <param name="targetServer">The server name the certificate is validated against.</param>
        /// <param name="tlsFirst">
        /// <see langword="true"/> for Strict (TDS 8), which negotiates the TDS 8 application protocol.
        /// </param>
        /// <param name="validationCallback">The server certificate validation callback.</param>
        protected static void AuthenticateAsClient(SslStream sslStream, string targetServer, bool tlsFirst, RemoteCertificateValidationCallback validationCallback)
        {
            if (tlsFirst)
            {
                AuthenticateAsClientAsync(sslStream, targetServer, null, validationCallback, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                // Equivalent to AuthenticateAsClient(targetServer, null, s_supportedProtocols, false),
                // which cannot take a per-handshake validation callback.
                sslStream.AuthenticateAsClient(new SslClientAuthenticationOptions
                {
                    TargetHost = targetServer,
                    EnabledSslProtocols = s_supportedProtocols,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    RemoteCertificateValidationCallback = validationCallback
                });
            }
        }

        /// <summary>
        /// Dispose class
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        public abstract void SetAsyncCallbacks(SniAsyncCallback receiveCallback, SniAsyncCallback sendCallback);

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public abstract void SetBufferSize(int bufferSize);

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public abstract uint Send(SniPacket packet);

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public abstract uint SendAsync(SniPacket packet);

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds</param>
        /// <returns>SNI error code</returns>
        public abstract uint Receive(out SniPacket packet, int timeoutInMilliseconds);

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public abstract uint ReceiveAsync(ref SniPacket packet);

        /// <summary>
        /// Enable SSL
        /// </summary>
        /// <param name="options">SSL options</param>
        /// <param name="encryptionInfo">
        /// Receives the negotiated protocol, cipher suite and certificate details of this handshake,
        /// including when it fails; <see langword="null"/> to record nothing.
        /// </param>
        /// <param name="cancellationToken">Cancelled when the connection's Connect Timeout elapses; aborts a handshake still in progress.</param>
        /// <returns>SNI error code</returns>
        public abstract uint EnableSsl(uint options, EncryptionInfoAccumulator encryptionInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Disable SSL
        /// </summary>
        public abstract void DisableSsl();

        /// <summary>
        /// Check connection status
        /// </summary>
        /// <returns>SNI error code</returns>
        public abstract uint CheckConnection();

        /// <summary>
        /// Last handle status
        /// </summary>
        public abstract uint Status { get; }

        /// <summary>
        /// Connection ID
        /// </summary>
        public abstract Guid ConnectionId { get; }

        public virtual int ReserveHeaderSize => 0;

        public abstract SniPacket RentPacket(int headerSize, int dataSize);

        public abstract void ReturnPacket(SniPacket packet);

        /// <summary>
        /// Gets a value that indicates the security protocol used to authenticate this connection.
        /// </summary>
        public virtual SslProtocols ProtocolVersion { get; } = 0;

        #if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public abstract void KillConnection();
        #endif
    }
}

#endif
