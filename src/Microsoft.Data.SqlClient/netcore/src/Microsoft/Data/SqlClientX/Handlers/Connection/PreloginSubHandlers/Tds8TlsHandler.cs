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
    internal class Tds8TlsHandler : BaseTlsHandler
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
            // Check if Tls needs to be negotiated first. This is a safety check to make sure that the handler is not 
            // called when it is not supposed to be.
            if (context.IsTlsFirst)
            {
                await AuthenticateClientInternal(context, isAsync, ct).ConfigureAwait(false);

                // Since encryption has already been negotiated, we need to set encryption not supported in
                // prelogin so that we don't try to negotiate encryption again during Pre login response read.
                context.InternalEncryptionOption = EncryptionOptions.NOT_SUP;
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
        }
    }
}
