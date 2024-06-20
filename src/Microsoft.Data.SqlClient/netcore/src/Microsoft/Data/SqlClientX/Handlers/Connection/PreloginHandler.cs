// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Extensions.Options;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Handler to send and receive the prelogin request.
    /// This handler will send the prelogin based on the features requested in the connection string.
    /// It will consume the prelogin handshake and pass the control to the next handler.
    /// </summary>
    internal class PreloginHandler : IHandler<ConnectionHandlerContext>
    {
        private static readonly SslProtocols s_supportedProtocols = SslProtocols.None;

        private static readonly List<SslApplicationProtocol> s_tdsProtocols = new List<SslApplicationProtocol>(1) { new(TdsEnums.TDS8_Protocol) };

        private bool _validateCert = true;

        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(ConnectionHandlerContext connectionContext, bool isAsync, CancellationToken ct)
        {
            PreLoginHandlerContext context = new PreLoginHandlerContext(connectionContext);

            await TlsBegin(context, isAsync, ct).ConfigureAwait(false);

            ReorderStream(context);

            await CreatePreLoginAndSend(context, isAsync, ct).ConfigureAwait(false);

            await ReadPreLoginresponse(context, isAsync, ct).ConfigureAwait(false);

            await TlsEnd(context, isAsync, ct).ConfigureAwait(false);

            ReorderStream(context);

            if (NextHandler is not null)
            {
                await NextHandler.Handle(connectionRequest, isAsync, ct).ConfigureAwait(false);
            }
        }

        private async Task TlsEnd(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private async Task ReadPreLoginresponse(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private async Task CreatePreLoginAndSend(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private void ReorderStream(PreLoginHandlerContext context)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Takes care of beginning TLS handshake.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async ValueTask TlsBegin(PreLoginHandlerContext request, bool isAsync, CancellationToken ct)
        {
            // Create the streams
            // If tls first then create a sslStream with the underlying stream as the transport stream.
            // if this is not tlsfirst then ssl over tds stream with transport stream as the underlying stream.

            Stream transportStream = request.ConnectionContext.ConnectionStream;
            Stream baseStream = transportStream;
            if (!request.IsTlsFirst)
            {
                SslOverTdsStream sslOVerTdsStream = new SslOverTdsStream(transportStream, request.ConnectionContext.ConnectionId);

                // This will be used later to finish the handshake.
                request.ConnectionContext.SslOverTdsStream = sslOVerTdsStream;
            }

            SslStream sslStream = new SslStream(baseStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate));
            request.ConnectionContext.SslStream = sslStream;

            if (request.ConnectionEncryptionOption == SqlConnectionEncryptOption.Strict)
            {
                //Always validate the certificate when in strict encryption mode
                uint info = TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE | TdsEnums.SNI_SSL_USE_SCHANNEL_CACHE | TdsEnums.SNI_SSL_SEND_ALPN_EXTENSION;

                EnableSsl(request, info);

                // Since encryption has already been negotiated, we need to set encryption not supported in
                // prelogin so that we don't try to negotiate encryption again during ConsumePreLoginHandshake.
                request.InternalEncryptionOption = EncryptionOptions.NOT_SUP;
            }

        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (!_validateCert)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: _connectionId);
                return true;
            }

            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: _connectionId);
            return SNICommon.ValidateSslServerCertificate(_connectionId, _targetServer, _hostNameInCertificate, serverCertificate, _serverCertificateFilename, policyErrors);
        }

        private async ValueTask EnableSsl(PreLoginHandlerContext request, uint info, bool isAsync, CancellationToken ct)
        {
            uint error = await EnableSsl2(request, info, isAsync, ct).ConfigureAwait(false);

            if (error != TdsEnums.SNI_SUCCESS)
            {
                _physicalStateObj.AddError(ProcessSNIError(_physicalStateObj));
                ThrowExceptionAndWarning(_physicalStateObj);
            }

            int protocolVersion = 0;
            WaitForSSLHandShakeToComplete(ref error, ref protocolVersion);

            SslProtocols protocol = (SslProtocols)protocolVersion;
            string warningMessage = protocol.GetProtocolWarning();
            if (!string.IsNullOrEmpty(warningMessage))
            {
                if (!request.ConnectionEncryptionOption && LocalAppContextSwitches.SuppressInsecureTLSWarning)
                {
                    // Skip console warning
                    SqlClientEventSource.Log.TryTraceEvent("<sc|{0}|{1}|{2}>{3}", nameof(TdsParser), nameof(EnableSsl), SqlClientLogger.LogLevel.Warning, warningMessage);
                }
                else
                {
                    // This logs console warning of insecure protocol in use.
                    _logger.LogWarning(nameof(TdsParser), nameof(EnableSsl), warningMessage);
                }
            }

            // create a new packet encryption changes the internal packet size
            _physicalStateObj.ClearAllWritePackets();
            ;
        }

        private async ValueTask<uint> EnableSsl2(PreLoginHandlerContext context, uint info, bool isAsync, CancellationToken ct)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableSsl | Info | Session Id {0}", context.ConnectionContext.ConnectionId);
                return await EnableSsl3(context, info, isAsync, ct);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableSsl | Err | Session Id {0}, SNI Handshake failed with exception: {1}", context.ConnectionContext.ConnectionId, e.Message);
                return SNICommon.ReportSNIError(SNIProviders.SSL_PROV, SNICommon.HandshakeFailureError, e);
            }
        }

        private async ValueTask<uint> EnableSsl3(PreLoginHandlerContext context, uint options, bool isAsync, CancellationToken ct)
        {
            using (TrySNIEventScope.Create(nameof(PreloginHandler)))
            {
                _validateCert = (options & TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE) != 0;
                string serverName = context.ConnectionContext.DataSource.ServerName;
                SslOverTdsStream sslOverTdsStream = context.ConnectionContext.SslOverTdsStream;
                try
                {
                    if (context.IsTlsFirst)
                    {
                        await AuthenticateAsClientAsync(context.ConnectionContext.SslStream, serverName, null, isAsync, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        context.ConnectionContext.SslStream.AuthenticateAsClient(serverName, null, s_supportedProtocols, false);
                    }

                    // If we are using SslOverTdsStream, we need to finish the handshake so that the Ssl stream,
                    // is no longer encapsulated in TDS.
                    sslOverTdsStream?.FinishHandshake();
                }

                catch (AuthenticationException aue)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Authentication exception occurred: {1}", args0: _connectionId, args1: aue?.Message);
                    return ReportTcpSNIError(aue, SNIError.CertificateValidationErrorCode);
                }
                catch (InvalidOperationException ioe)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Invalid Operation Exception occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                    return ReportTcpSNIError(ioe);
                }

                _stream = _sslStream;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, SSL enabled successfully.", args0: _connectionId);
                return TdsEnums.SNI_SUCCESS;
            }
        }

        private async ValueTask AuthenticateAsClientAsync(SslStream sslStream, string serverName, X509CertificateCollection certificate, bool isAsync, CancellationToken ct)
        {
            SslClientAuthenticationOptions sslClientOptions = new()
            {
                TargetHost = serverName,
                ApplicationProtocols = s_tdsProtocols,
                ClientCertificates = certificate
            };
            if (isAsync)
            {
                await sslStream.AuthenticateAsClientAsync(sslClientOptions, ct);
            }
            else
            {
                sslStream.AuthenticateAsClient(sslClientOptions);
            }
        }


        /// <summary>
        /// Handler context for Prelogin.
        /// </summary>
        private class PreLoginHandlerContext : HandlerRequest
        {
            public SqlConnectionEncryptOption ConnectionEncryptionOption { get; private set; }
            public bool IsTlsFirst { get; private set; }
            public bool TrustServerCert { get; private set; }
            public bool IntegratedSecurity { get; private set; }
            public SqlAuthenticationMethod AuthType { get; private set; }
            public string HostNameInCertificate { get; private set; }
            public string ServerCertificateFilename { get; private set; }

            public EncryptionOptions InternalEncryptionOption { get; set; } = EncryptionOptions.OFF;

            public ConnectionHandlerContext ConnectionContext { get; private set; }

            public PreLoginHandlerContext(ConnectionHandlerContext connectionContext)
            {
                this.ConnectionContext = connectionContext;

                var connectionOptions = connectionContext.ConnectionString;
                ConnectionEncryptionOption = connectionOptions.Encrypt;
                IsTlsFirst = (ConnectionEncryptionOption == SqlConnectionEncryptOption.Strict);
                TrustServerCert = connectionOptions.TrustServerCertificate;
                IntegratedSecurity = connectionOptions.IntegratedSecurity;
                AuthType = connectionOptions.Authentication;
                HostNameInCertificate = connectionOptions.HostNameInCertificate;
                ServerCertificateFilename = connectionOptions.ServerCertificate;
            }
        }
    }
}
