// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler for TDS8 TLS handling. The consumers of this handler, should use this,
    /// only when TDS8 is being used.
    /// </summary>
    internal class Tds8TlsHandler : IHandler<PreloginHandlerContext>
    {
        private static readonly List<SslApplicationProtocol> s_tdsProtocols = new(1) { new(TdsEnums.TDS8_Protocol) };
        
        private readonly TlsAuthenticator _authenticator;

        public Tds8TlsHandler(TlsAuthenticator authenticator)
        {
            this._authenticator = authenticator;
        }

        public IHandler<PreloginHandlerContext> NextHandler { get; set; }

        /// <summary>
        /// Takes care of setting up TLS in Tls First aka TDS8.0.
        /// </summary>
        /// <param name="context">The prelogin context object.</param>
        /// <param name="isAsync">Whether this operations should be done asynchronously or not.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns></returns>
        public async ValueTask Handle(PreloginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            SslClientAuthenticationOptions options = Tds8TlsHandler.BuildClientAuthenticationOptions(context);

            await _authenticator.AuthenticateClientInternal(context, options, isAsync, ct).ConfigureAwait(false);

            // Since encryption has already been negotiated, we need to set encryption not supported in
            // prelogin so that we don't try to negotiate encryption again during Pre login response read.
            context.InternalEncryptionOption = EncryptionOptions.NOT_SUP;

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        protected static SslClientAuthenticationOptions BuildClientAuthenticationOptions(PreloginHandlerContext context)
        {
            return new()
            {
                TargetHost = context.ConnectionContext.DataSource.ServerName,
                ApplicationProtocols = s_tdsProtocols,
            };
        }
    }
}
