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
        /// Takes care of beginning TLS handshake.
        /// </summary>
        /// <param name="preloginContext"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async ValueTask Handle(PreLoginHandlerContext preloginContext, bool isAsync, CancellationToken ct)
        {
            InitializeSslStream(preloginContext);

            if (preloginContext.ConnectionEncryptionOption == SqlConnectionEncryptOption.Strict)
            {
                //Always validate the certificate when in strict encryption mode
                preloginContext.ValidateCertificate = true;

                await EnableSsl(preloginContext, isAsync, ct).ConfigureAwait(false);

                // Since encryption has already been negotiated, we need to set encryption not supported in
                // prelogin so that we don't try to negotiate encryption again during ConsumePreLoginHandshake.
                preloginContext.InternalEncryptionOption = EncryptionOptions.NOT_SUP;
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
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: connectionId);
                        return true;
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: connectionId);

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
