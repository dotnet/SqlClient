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
    /// <summary>
    /// TlsAuthenticator takes care of authenticating a client using TLS.
    /// It is meant to be used during pre-login.
    /// </summary>
    internal class TlsAuthenticator
    {
        public virtual async ValueTask AuthenticateClientInternal(PreloginHandlerContext request,
            SslClientAuthenticationOptions options,
            bool isAsync,
            CancellationToken ct)
        {
            await AuthenticateClient(request, options, isAsync, ct).ConfigureAwait(false);   

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
                            nameof(TlsAuthenticator),
                            nameof(AuthenticateClientInternal),
                            SqlClientLogger.LogLevel.Warning,
                            warningMessage);
                    }
                    else
                    {
                        // This logs console warning of insecure protocol in use.
                        ConnectionHandlerContext.Logger.LogWarning(nameof(TlsAuthenticator), nameof(AuthenticateClientInternal), warningMessage);
                    }
                }
            }

            static bool ShouldNotLogWarning(PreloginHandlerContext request)
            {
                return !request.ConnectionEncryptionOption && LocalAppContextSwitches.SuppressInsecureTLSWarning;
            }
        }

        private async ValueTask AuthenticateClient(PreloginHandlerContext context, SslClientAuthenticationOptions options, bool isAsync, CancellationToken ct)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("PreloginHandler.AuthenticateClient | Info | Session Id {0}", context.ConnectionContext.ConnectionId);
                using (TrySNIEventScope.Create(nameof(PreloginHandler)))
                {
                    Guid connectionId = context.ConnectionContext.ConnectionId;
                    SslOverTdsStream sslOverTdsStream = context.ConnectionContext.SslOverTdsStream;
                    SslStream sslStream = context.ConnectionContext.SslStream;
                    try
                    {
                        ct.ThrowIfCancellationRequested();
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
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(TlsAuthenticator), EventType.ERR, "Connection Id {0}, Authentication exception occurred: {1}", args0: connectionId, args1: aue?.Message);
                        throw;
                    }
                    catch (InvalidOperationException ioe)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(TlsAuthenticator), EventType.ERR, "Connection Id {0}, Invalid Operation Exception occurred: {1}", args0: connectionId, args1: ioe?.Message);
                        throw;
                    }

                    context.ConnectionContext.TdsStream = new TdsStream(new TdsWriteStream(sslStream), new TdsReadStream(sslStream));
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(TlsAuthenticator), EventType.INFO, "Connection Id {0}, SSL enabled successfully.", args0: connectionId);
                }
            }
            catch (Exception exception)
            {
                SqlClientEventSource.Log.TryTraceEvent("PreloginHandler.AuthenticateClient | Err | Session Id {0}, SNI Handshake failed with exception: {1}",
                    context.ConnectionContext.ConnectionId,
                    exception.Message);
                SqlError sqlError = SNIProviders.SSL_PROV.CreateSqlError(SNICommon.HandshakeFailureError, exception, PreloginHandlerContext.SniContext, context.ServerInfo.ResolvedServerName);
                context.ConnectionContext.ErrorCollection.Add(sqlError);
                throw SqlException.CreateException(context.ConnectionContext.ErrorCollection, null);
            }
        }
    }
}
