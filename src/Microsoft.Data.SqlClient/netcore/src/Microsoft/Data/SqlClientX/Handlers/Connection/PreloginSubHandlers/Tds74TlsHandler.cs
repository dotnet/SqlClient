// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler meant to be invoked after the Prelogin exchange is complete.
    /// This handler is needed for Tds 7.4 and below, because it negotiates the TLS encryption,
    /// and validates the server certificate after the pre-login exchange.
    /// </summary>
    internal class Tds74TlsHandler : BaseTlsHandler
    {
        private static readonly SslProtocols s_supportedProtocols = SslProtocols.None;

        /// <inheritdoc />
        public override async ValueTask Handle(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            if (!context.IsTlsFirst && context.DoesClientNeedEncryption())
            {
                if (!context.ServerSupportsEncryption)
                {
                    //_physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                    //_physicalStateObj.Dispose();
                    //ThrowExceptionAndWarning(_physicalStateObj);
                    // TODO: Error handling needs to be aligned for the new design. This is a placeholder for now.
                    context.ConnectionContext.Error = new Exception("Encryption not supported by server");
                    return;
                }
                
                await AuthenticateClientInternal(context, isAsync, ct).ConfigureAwait(false);

                // Enable encryption for Login. 
                context.ConnectionContext.TdsStream.ReplaceUnderlyingStream(context.ConnectionContext.SslStream);
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
        }

        protected override SslClientAuthenticationOptions BuildClientAuthenticationOptions(PreLoginHandlerContext context)
        {
            string serverName = context.ConnectionContext.DataSource.ServerName;
            return new()
            {
                TargetHost = serverName,
                ClientCertificates = null,
                EnabledSslProtocols = s_supportedProtocols,
                //TODO: Revisit the CRL revocation check. 
                // CRL revocation check can be done online, where the CRL where the CA authority may need to be reached
                // which could cause delays, and may not be necessary for all scenarios. Deeper investigation is needed about 
                // the implication of Online/Offline check.
                // e.g. How does the offline check work, and where are the CRL certificates expected?
                // How does the offline check deal with CRL list which can be included in the chain of certificates.
                // How does this compare with Windows cert validation handled in Native SNI?
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            };
        }
    }
}
