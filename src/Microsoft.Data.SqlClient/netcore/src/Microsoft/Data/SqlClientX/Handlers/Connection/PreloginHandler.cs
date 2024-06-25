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
    internal class PreloginHandler : IHandler<ConnectionHandlerContext>
    {
        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(ConnectionHandlerContext connectionContext, bool isAsync, CancellationToken ct)
        {
            PreLoginHandlerContext context = new PreLoginHandlerContext(connectionContext);

            InitializeSslStream(context);

            var preloginPacketHandler = new PreloginPacketHandler();

            IHandler<PreLoginHandlerContext> firstHandler;

            if (context.IsTlsFirst)
            {
                IHandler<PreLoginHandlerContext> tlsHandler = firstHandler = new Tds8TlsHandler();
                tlsHandler.NextHandler = preloginPacketHandler;
            }
            else
            {
                var tlsEndHandler = new Tds74TlsHandler();
                preloginPacketHandler.NextHandler = tlsEndHandler;
                firstHandler = preloginPacketHandler;
            }

            await firstHandler.Handle(context, isAsync, ct).ConfigureAwait(false);

            if (NextHandler is not null)
            {
                await NextHandler.Handle(connectionContext, isAsync, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Initializes the various streams required for the TLS handshake.
        /// </summary>
        /// <param name="preloginContext"></param>
        void InitializeSslStream(PreLoginHandlerContext preloginContext)
        {
            // Create the streams
            // If tls first then create a sslStream with the underlying stream as the transport stream.
            // if this is not tlsfirst then ssl over tds stream with transport stream as the underlying stream.
            Stream transportStream = preloginContext.ConnectionContext.ConnectionStream;
            Stream baseStream = transportStream;
            if (!preloginContext.IsTlsFirst)
            {
                SslOverTdsStream sslOVerTdsStream = new SslOverTdsStream(transportStream, preloginContext.ConnectionContext.ConnectionId);

                // This will be used later to finish the handshake.
                preloginContext.ConnectionContext.SslOverTdsStream = sslOVerTdsStream;
            }
            SslStream sslStream = new SslStream(baseStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate));
            preloginContext.ConnectionContext.SslStream = sslStream;

            bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                Guid connectionId = preloginContext.ConnectionContext.ConnectionId;
                if (!preloginContext.ShouldValidateCertificate())
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(Tds8TlsHandler), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: connectionId);
                    return true;
                }

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(Tds8TlsHandler), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: connectionId);

                return SNICommon.ValidateSslServerCertificate(connectionId,
                    preloginContext.ConnectionContext.DataSource.ServerName,
                    preloginContext.HostNameInCertificate,
                    certificate, preloginContext.ServerCertificateFilename,
                    sslPolicyErrors);
            }
        }
    }
}
