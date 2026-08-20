// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SNI connection handle
    /// </summary>
    internal abstract class SniHandle
    {
        private SqlClientCertificateContext _clientCertificateContext;

        protected static readonly SslProtocols s_supportedProtocols = SslProtocols.None;

        protected static readonly List<SslApplicationProtocol> s_tdsProtocols = new List<SslApplicationProtocol>(1) { new(TdsEnums.TDS8_Protocol) };

        /// <summary>
        /// Builds the TLS client options used for both the synchronous and asynchronous handshakes.
        /// </summary>
        /// <param name="serverNameIndication">The server name to present as SNI.</param>
        /// <param name="certificateContext">The client certificate chain, or <see langword="null" /> when no client certificate is configured.</param>
        /// <param name="useTds8Alpn">Whether to advertise the TDS 8.0 ALPN protocol.</param>
        /// <returns>The configured client authentication options.</returns>
        private static SslClientAuthenticationOptions CreateClientAuthenticationOptions(
            string serverNameIndication,
            SslStreamCertificateContext certificateContext,
            bool useTds8Alpn)
        {
            SslClientAuthenticationOptions sslClientOptions = new()
            {
                TargetHost = serverNameIndication,
                EnabledSslProtocols = s_supportedProtocols,
                ClientCertificateContext = certificateContext,
            };
            if (useTds8Alpn)
            {
                sslClientOptions.ApplicationProtocols = s_tdsProtocols;
            }

            return sslClientOptions;
        }

        /// <summary>
        /// Performs the TLS handshake synchronously.
        /// </summary>
        /// <remarks>
        /// This intentionally uses the synchronous <c>AuthenticateAsClient</c> overload. Blocking on
        /// the asynchronous overload would require a second thread to run the continuation, which
        /// deadlocks under thread-pool starvation and on single-threaded synchronization contexts.
        /// </remarks>
        /// <param name="sslStream">The stream to authenticate.</param>
        /// <param name="serverNameIndication">The server name to present as SNI.</param>
        /// <param name="certificateContext">The client certificate chain, or <see langword="null" /> when no client certificate is configured.</param>
        /// <param name="useTds8Alpn">Whether to advertise the TDS 8.0 ALPN protocol.</param>
        protected static void AuthenticateAsClient(
            SslStream sslStream,
            string serverNameIndication,
            SslStreamCertificateContext certificateContext,
            bool useTds8Alpn)
        {
            SslClientAuthenticationOptions sslClientOptions =
                CreateClientAuthenticationOptions(serverNameIndication, certificateContext, useTds8Alpn);

            sslStream.AuthenticateAsClient(sslClientOptions);
        }

        protected SslStreamCertificateContext GetClientCertificateContext(
            string certificatePath,
            string keyPath,
            string keyPassword)
        {
            if (string.IsNullOrEmpty(certificatePath))
            {
                return null;
            }

            _clientCertificateContext ??= SqlClientCertificateLoader.Load(
                certificatePath,
                keyPath,
                keyPassword);
            return _clientCertificateContext.SslContext;
        }

        protected void DisposeClientCertificate()
        {
            _clientCertificateContext?.Dispose();
            _clientCertificateContext = null;
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
        public abstract uint EnableSsl(uint options, string clientCertificate, string clientKey, string clientKeyPassword);

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
