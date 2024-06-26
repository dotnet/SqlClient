// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    internal abstract class BaseTlsHandler : IHandler<PreloginHandlerContext>
    {

        public IHandler<PreloginHandlerContext> NextHandler { get; set; }

        public abstract ValueTask Handle(PreloginHandlerContext request, bool isAsync, CancellationToken ct);

        protected async ValueTask AuthenticateClientInternal(PreloginHandlerContext request, bool isAsync, CancellationToken ct)
        {
            try
            { 
                await AuthenticateClient(request, isAsync, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // TODO: Convert to a Sql Exception.
                // this would require that we convert the error to a Sql error with the traditional details about 
                // SNI. A lot of errors strings require SNI providers to be passed in.
                // So we will stick to the format.
                throw;
            }
            

            LogWarningIfNeeded(request);

            static void LogWarningIfNeeded(PreloginHandlerContext request)
            {
                string warningMessage = request.ConnectionContext.SslStream.SslProtocol.GetProtocolWarning();
                if (!string.IsNullOrEmpty(warningMessage))
                {
                    if (ShouldNotLogWarning(request))
                    {
                        // Skip console warning
                        SqlClientEventSource.Log.TryTraceEvent("<sc|{0}|{1}|{2}>{3}",
                            nameof(PreloginHandler),
                            nameof(AuthenticateClientInternal),
                            SqlClientLogger.LogLevel.Warning,
                            warningMessage);
                    }
                    else
                    {
                        // This logs console warning of insecure protocol in use.
                        request.ConnectionContext.Logger.LogWarning(nameof(PreloginHandler), nameof(AuthenticateClientInternal), warningMessage);
                    }
                }
            }

            static bool ShouldNotLogWarning(PreloginHandlerContext request)
            {
                return !request.ConnectionEncryptionOption && LocalAppContextSwitches.SuppressInsecureTLSWarning;
            }
        }

        private async ValueTask AuthenticateClient(PreloginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("PreloginHandler.AuthenticateClient | Info | Session Id {0}", context.ConnectionContext.ConnectionId);
                using (TrySNIEventScope.Create(nameof(PreloginHandler)))
                {
                    Guid _connectionId = context.ConnectionContext.ConnectionId;
                    SslOverTdsStream sslOverTdsStream = context.ConnectionContext.SslOverTdsStream;
                    SslStream sslStream = context.ConnectionContext.SslStream;
                    try
                    {
                        SslClientAuthenticationOptions options = BuildClientAuthenticationOptions(context);

                        if (isAsync)
                        {
                            await sslStream.AuthenticateAsClientAsync(options, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            sslStream.AuthenticateAsClient(options);
                        }

                        // If we are using SslOverTdsStream, we need to finish the handshake so that the Ssl stream,
                        // is no longer encapsulated in TDS.
                        sslOverTdsStream?.FinishHandshake();
                    }

                    catch (AuthenticationException aue)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(BaseTlsHandler), EventType.ERR, "Connection Id {0}, Authentication exception occurred: {1}", args0: _connectionId, args1: aue?.Message);
                        context.SniError = new SNIError(SNIProviders.SSL_PROV, SNICommon.InternalExceptionError, aue, SNIError.CertificateValidationErrorCode);
                        return;
                    }
                    catch (InvalidOperationException ioe)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Invalid Operation Exception occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                        context.SniError = new SNIError(SNIProviders.SSL_PROV, SNICommon.InternalExceptionError, ioe);
                        return;
                    }

                    context.ConnectionContext.TdsStream = new TdsStream(new TdsWriteStream(sslStream), new TdsReadStream(sslStream));
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(BaseTlsHandler), EventType.INFO, "Connection Id {0}, SSL enabled successfully.", args0: _connectionId);
                }
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("PreloginHandler.AuthenticateClient | Err | Session Id {0}, SNI Handshake failed with exception: {1}",
                    context.ConnectionContext.ConnectionId,
                    e.Message);
                throw;
            }
        }

        /// <summary>
        /// Builds the Ssl Client authentication options during prelogin.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract SslClientAuthenticationOptions BuildClientAuthenticationOptions(PreloginHandlerContext context);
    }
}
