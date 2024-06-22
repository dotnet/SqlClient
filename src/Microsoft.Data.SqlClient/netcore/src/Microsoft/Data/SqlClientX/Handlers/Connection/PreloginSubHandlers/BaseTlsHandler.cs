using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    internal abstract class BaseTlsHandler : IHandler<PreLoginHandlerContext>
    {
        private static readonly SslProtocols s_supportedProtocols = SslProtocols.None;

        private static readonly List<SslApplicationProtocol> s_tdsProtocols = new List<SslApplicationProtocol>(1) { new(TdsEnums.TDS8_Protocol) };

        protected bool ValidateCert { get; set; } = true;

        public IHandler<PreLoginHandlerContext> NextHandler { get; set; }

        public abstract ValueTask Handle(PreLoginHandlerContext request, bool isAsync, CancellationToken ct);

        protected async ValueTask AuthenticateClientInternal(PreLoginHandlerContext request, bool isAsync, CancellationToken ct)
        {
            await AuthenticateClient(request, isAsync, ct).ConfigureAwait(false);

            if (request.SniError != null)
            {
                SqlError error = request.SniError.ToSqlError(SniContext.Snix_PreLogin,
                    new ServerInfo(request.ConnectionContext.ConnectionString));
                // TODO; enhance
                throw request.Exception;
            }

            LogWarningIfNeeded(request);

            static void LogWarningIfNeeded(PreLoginHandlerContext request)
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

            static bool ShouldNotLogWarning(PreLoginHandlerContext request)
            {
                return !request.ConnectionEncryptionOption && LocalAppContextSwitches.SuppressInsecureTLSWarning;
            }
        }

        private async ValueTask AuthenticateClient(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("PreloginHandler.AuthenticateClient | Info | Session Id {0}", context.ConnectionContext.ConnectionId);
                using (TrySNIEventScope.Create(nameof(PreloginHandler)))
                {
                    Guid _connectionId = context.ConnectionContext.ConnectionId;
                    ValidateCert = context.ValidateCertificate;
                    string serverName = context.ConnectionContext.DataSource.ServerName;
                    SslOverTdsStream sslOverTdsStream = context.ConnectionContext.SslOverTdsStream;
                    SslStream sslStream = context.ConnectionContext.SslStream;
                    try
                    {
                        SslClientAuthenticationOptions options =
                            context.IsTlsFirst ?
                                new()
                                {
                                    TargetHost = serverName,
                                    ClientCertificates = null,
                                    EnabledSslProtocols = s_supportedProtocols,
                                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                                } :
                                new()
                                {
                                    TargetHost = serverName,
                                    ApplicationProtocols = s_tdsProtocols,
                                    ClientCertificates = null
                                };


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
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, SSL enabled successfully.", args0: _connectionId);
                }
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("PreloginHandler.AuthenticateClient | Err | Session Id {0}, SNI Handshake failed with exception: {1}",
                    context.ConnectionContext.ConnectionId,
                    e.Message);
                context.Exception = e;
            }
        }
    }
}
