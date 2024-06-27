// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler meant to be invoked after the Prelogin exchange is complete.
    /// This handler is needed for Tds 7.4 and below, because it negotiates the TLS encryption,
    /// and validates the server certificate after the pre-login exchange.
    /// </summary>
    internal class Tds74TlsHandler : IHandler<PreloginHandlerContext>
    {
        /// <summary>
        /// The Tls authenticator to authenticate as client.
        /// </summary>
        private readonly TlsAuthenticator _authenticator;

        public Tds74TlsHandler(TlsAuthenticator authenticator)
        {
            _authenticator = authenticator;
        }

        public IHandler<PreloginHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(PreloginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            if (context.DoesClientNeedEncryption())
            {
                if (!context.ServerSupportsEncryption)
                {
                    SqlErrorCollection collection = context.ConnectionContext.ErrorCollection;
                    string serverName = context.ConnectionContext.ServerInfo.ResolvedServerName;
                    collection.Add(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, serverName, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                    throw SqlException.CreateException(collection, null);
                }

                SslClientAuthenticationOptions options = Tds74TlsHandler.BuildClientAuthenticationOptions(context);
                await _authenticator.AuthenticateClientInternal(context, options, isAsync, ct).ConfigureAwait(false);

                // Enable encryption for Login. 
                context.ConnectionContext.TdsStream.ReplaceUnderlyingStream(context.ConnectionContext.SslStream);
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create the SSL Authentication options applicable to TDS 7.4
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static SslClientAuthenticationOptions BuildClientAuthenticationOptions(PreloginHandlerContext context)
        {
            return new()
            {
                TargetHost = context.ConnectionContext.DataSource.ServerName,
                ClientCertificates = null,
                EnabledSslProtocols = SslProtocols.None,
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
