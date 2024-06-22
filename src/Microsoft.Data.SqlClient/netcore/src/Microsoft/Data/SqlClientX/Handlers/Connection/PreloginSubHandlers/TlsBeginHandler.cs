// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler for beginning the TLS process.
    /// </summary>
    internal class TlsBeginHandler : BaseTlsHandler
    {
        /// <summary>
        /// Takes care of beginning TLS handshake in Tls First aka TDS8.0.
        /// </summary>
        /// <param name="context">The prelogin context object.</param>
        /// <param name="isAsync">Whether this operations should be done asynchronosly or not.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns></returns>
        public override async ValueTask Handle(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            InitializeSslStream(context);

            if (context.IsTlsFirst)
            {
                //Always validate the certificate in Tls First mode.
                context.ValidateCertificate = true;

                await AuthenticateClientInternal(context, isAsync, ct).ConfigureAwait(false);

                // Since encryption has already been negotiated, we need to set encryption not supported in
                // prelogin so that we don't try to negotiate encryption again during Pre login response read.
                context.InternalEncryptionOption = EncryptionOptions.NOT_SUP;
            }

            if (context.HasError)
            {
                return;
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }

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
                    if (!ValidateCert)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(TlsBeginHandler), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: connectionId);
                        return true;
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(TlsBeginHandler), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: connectionId);

                    return SNICommon.ValidateSslServerCertificate(connectionId,
                        preloginContext.ConnectionContext.DataSource.ServerName,
                        preloginContext.HostNameInCertificate,
                        certificate, preloginContext.ServerCertificateFilename,
                        sslPolicyErrors);
                }
            }
        }
    }
}
