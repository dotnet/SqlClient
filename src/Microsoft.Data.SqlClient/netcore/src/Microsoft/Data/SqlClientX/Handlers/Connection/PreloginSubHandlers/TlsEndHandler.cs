// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler meant to be invoked after the Prelogin exchange is complete.
    /// This handler is most significant for Tds 7.4 and below, because it negotiates the TLS encryption,
    /// and validates the server certificate, which are mandatory for Tds 8.0, and are already done at this point.
    /// </summary>
    internal class TlsEndHandler : BaseTlsHandler
    {
        /// <inheritdoc />
        public override async ValueTask Handle(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            if (!context.IsTlsFirst && DoesClientNeedEncryption(context))
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

                context.ValidateCertificate = ShouldValidateSertificate(context);

                await AuthenticateClientInternal(context, isAsync, ct).ConfigureAwait(false);
            }

            if (context.HasError)
            {
                return;
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }

            // Validate Certificate if Trust Server Certificate=false and Encryption forced (EncryptionOptions.ON) from Server.
            static bool ShouldValidateSertificate(PreLoginHandlerContext context) =>
                (context.InternalEncryptionOption == EncryptionOptions.ON && !context.TrustServerCert) ||
                (context.ConnectionContext.AccessTokenInBytes != null && !context.TrustServerCert);

            // Do client settings require encryption?
            static bool DoesClientNeedEncryption(PreLoginHandlerContext context) =>
                context.InternalEncryptionOption == EncryptionOptions.ON || context.InternalEncryptionOption == EncryptionOptions.LOGIN;
        }
    }
}
