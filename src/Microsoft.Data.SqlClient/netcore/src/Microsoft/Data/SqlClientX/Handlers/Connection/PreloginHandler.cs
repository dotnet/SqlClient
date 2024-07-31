// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Handler to send and receive the prelogin request.
    /// This handler will send the prelogin based on the features requested in the connection string.
    /// It will consume the prelogin handshake and pass the control to the next handler.
    /// </summary>
    internal class PreloginHandler : ContextHandler<ConnectionHandlerContext>
    {
        /// <summary>
        /// The Helper object to perform TLS authentication.
        /// </summary>
        private readonly TlsAuthenticator _tlsAuthenticator;
        
        /// <summary>
        /// A Factory to create sub handlers for the prelogin.
        /// </summary>
        private readonly PreloginSubHandlerBuilder _subHandlerChainBuilder;

        /// <summary>
        /// Parameter-less constructor which creates an authenticator for TLS.
        /// </summary>
        public PreloginHandler() : this(new TlsAuthenticator(), new PreloginSubHandlerBuilder())
        {
        }

        /// <summary>
        /// Constructs Prelogin handler with an authenticator
        /// </summary>
        /// <param name="tlsAuthenticator">The Tls authenticator to use.</param>
        /// <param name="subHandlerChainBuilder">Chain builder.</param>
        public PreloginHandler(TlsAuthenticator tlsAuthenticator, PreloginSubHandlerBuilder subHandlerChainBuilder)
        {
            _tlsAuthenticator = tlsAuthenticator;
            _subHandlerChainBuilder = subHandlerChainBuilder;
        }

        /// <inheritdoc />
        public override async ValueTask Handle(ConnectionHandlerContext connectionContext, bool isAsync, CancellationToken ct)
        {
            PreloginHandlerContext context = new PreloginHandlerContext(connectionContext);

            InitializeSslStream(context);

            IHandler<PreloginHandlerContext> firstHandler = _subHandlerChainBuilder.CreateChain(context, _tlsAuthenticator);
            
            await firstHandler.Handle(context, isAsync, ct).ConfigureAwait(false);

            if (NextHandler is not null)
            {
                await NextHandler.Handle(connectionContext, isAsync, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Initializes the SSL required for the TLS handshake.
        /// In case of Tds7.4, the SslOverTdsStream is created as well.
        /// </summary>
        /// <param name="preloginContext"></param>
        void InitializeSslStream(PreloginHandlerContext preloginContext)
        {
            // Create the streams
            // If tls first then create a sslStream with the underlying stream as the transport stream.
            // if this is not tlsfirst then ssl over tds stream with transport stream as the underlying stream.
            Stream transportStream = preloginContext.ConnectionContext.ConnectionStream;

            // baseStream is the underlying stream for the SslStream.
            Stream baseStream = transportStream;
            if (!preloginContext.IsTlsFirst)
            {
                SslOverTdsStream sslOVerTdsStream;
                baseStream = sslOVerTdsStream 
                    = preloginContext.ConnectionContext.SslOverTdsStream
                    = new SslOverTdsStream(transportStream, preloginContext.ConnectionContext.ConnectionId);
            }

            SslStream sslStream =
                preloginContext.ConnectionContext.SslStream 
                = new SslStream(baseStream, true, ValidateServerCertificate);

            Stream preloginStream = preloginContext.IsTlsFirst ? (Stream)sslStream : (Stream)preloginContext.ConnectionContext.ConnectionStream;

            preloginContext.ConnectionContext.TdsStream = new IO.TdsStream(new IO.TdsWriteStream(preloginStream), new IO.TdsReadStream(preloginStream));

            bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                Guid connectionId = preloginContext.ConnectionContext.ConnectionId;
                if (!preloginContext.ShouldValidateCertificate())
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(PreloginHandler), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: connectionId);
                    return true;
                }

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(PreloginHandler), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: connectionId);

                return SNICommon.ValidateSslServerCertificate(connectionId,
                    preloginContext.ConnectionContext.DataSource.ServerName,
                    preloginContext.HostNameInCertificate,
                    certificate, preloginContext.ServerCertificateFilename,
                    sslPolicyErrors);
            }
        }

        /// <summary>
        /// A builder to build the prelogin handler chain.
        /// </summary>
        internal class PreloginSubHandlerBuilder
        {
            /// <summary>
            /// Creates a chain for prelogin based on the context and supplied authenticator.
            /// </summary>
            /// <param name="context"></param>
            /// <param name="authenticator"></param>
            /// <returns></returns>
            public virtual IHandler<PreloginHandlerContext> CreateChain(PreloginHandlerContext context, TlsAuthenticator authenticator)
            {
                PreloginPacketHandler preloginPacketHandler = new();
                IHandler<PreloginHandlerContext> firstHandler = preloginPacketHandler;
                if (context.IsTlsFirst)
                {
                    IHandler<PreloginHandlerContext> tlsHandler = firstHandler = new Tds8TlsHandler(authenticator);
                    tlsHandler.NextHandler = preloginPacketHandler;
                }
                else
                {
                    Tds74TlsHandler tlsEndHandler = new(authenticator);
                    preloginPacketHandler.NextHandler = tlsEndHandler;
                }
                return firstHandler;
            }
        }
    }
}
