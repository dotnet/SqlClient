// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.IO;

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
        
        private const int GUID_SIZE = 16;
        
        private bool _validateCert = true;

        private static int _objectTypeCount; // EventSource counter

        internal readonly int _objectID = Interlocked.Increment(ref _objectTypeCount);

        internal int ObjectID => _objectID;

        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(ConnectionHandlerContext connectionContext, bool isAsync, CancellationToken ct)
        {
            PreLoginHandlerContext context = new PreLoginHandlerContext(connectionContext);

            await TlsBegin(context, isAsync, ct).ConfigureAwait(false);

            await CreatePreLoginAndSend(context, isAsync, ct).ConfigureAwait(false);

            await ReadPreLoginresponse(context, isAsync, ct).ConfigureAwait(false);

            await TlsEnd(context, isAsync, ct).ConfigureAwait(false);

            ReorderStream(context);

            if (NextHandler is not null)
            {
                await NextHandler.Handle(connectionContext, isAsync, ct).ConfigureAwait(false);
            }
        }

        private Task TlsEnd(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            return Task.FromException(new NotImplementedException());
        }

        private async Task<PreLoginStatus> ReadPreLoginresponse(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            context.ConnectionContext.MarsCapable  = context.ConnectionContext.ConnectionString.MARS; // Assign default value
            context.ConnectionContext.FedAuthRequired = false;
            bool is2005OrLater = false;


            TdsStream tdsStream = context.ConnectionContext.TdsStream;

            // Look into the first byte to see if we are connecting to a 6.5 or earlier server.
            int option = await tdsStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            if (option == 0xaa)
            {
                // If the first byte is 0xAA, we are connecting to a 6.5 or earlier server, which
                // is not supported.
                throw SQL.InvalidSQLServerVersionUnknown();
            }

            byte[] payload = new byte[tdsStream.PacketDataLeft];

            int payloadOffset = 0;
            int payloadLength = 0;
            int offset = 0;
            bool serverSupportsEncryption = false;
            
            // Allocate an array of the size PreLoginOptions.NUMOPTS to hold options offsets

            // Allocate an array to hold thh options length




            while (option != (byte)PreLoginOptions.LASTOPT)
            {
                switch (option)
                {
                    case (int)PreLoginOptions.VERSION:
                        
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        payloadLength = payload[offset++] << 8 | payload[offset++];

                        byte majorVersion = payload[payloadOffset];
                        byte minorVersion = payload[payloadOffset + 1];
                        int level = (payload[payloadOffset + 2] << 8) |
                                             payload[payloadOffset + 3];

                        is2005OrLater = majorVersion >= 9;
                        if (!is2005OrLater)
                        {
                            context.ConnectionContext.MarsCapable = false;            // If pre-2005, MARS not supported.
                        }

                        break;

                    case (int)PreLoginOptions.ENCRYPT:
                        if (context.IsTlsFirst)
                        {
                            // Can skip/ignore this option if we are doing TDS 8.
                            offset += 4;
                            break;
                        }

                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        payloadLength = payload[offset++] << 8 | payload[offset++];

                        EncryptionOptions serverOption = (EncryptionOptions)payload[payloadOffset];

                        // Any response other than NOT_SUP means the server supports encryption.
                        serverSupportsEncryption = serverOption != EncryptionOptions.NOT_SUP;

                        switch (context.InternalEncryptionOption)
                        {
                            case (EncryptionOptions.OFF):
                                if (serverOption == EncryptionOptions.OFF)
                                {
                                    // Only encrypt login.
                                    context.InternalEncryptionOption = EncryptionOptions.LOGIN;
                                }
                                else if (serverOption == EncryptionOptions.REQ)
                                {
                                    // Encrypt all.
                                    context.InternalEncryptionOption = EncryptionOptions.ON;
                                }
                                // NOT_SUP: No encryption.
                                break;

                            case (EncryptionOptions.NOT_SUP):
                                if (serverOption == EncryptionOptions.REQ)
                                {
                                    // Server requires encryption, but client does not support it.
                                    //_physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByClient(), "", 0));
                                    //_physicalStateObj.Dispose();
                                    //ThrowExceptionAndWarning(_physicalStateObj);
                                    // TODO : Error handling needs to happen here. Till then 
                                    // adding an exception and returning 
                                    context.ConnectionContext.Error = new Exception("Encryption not supported by server");
                                    return PreLoginStatus.Failed;
                                }

                                break;
                            default:
                                // Any other client option needs encryption
                                if (serverOption == EncryptionOptions.NOT_SUP)
                                { 
                                    //    _physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                                    //    _physicalStateObj.Dispose();
                                    //    ThrowExceptionAndWarning(_physicalStateObj);
                                    context.ConnectionContext.Error  = new Exception("Encryption not supported by server");
                                    return PreLoginStatus.Failed;
                                }
                                break;
                        }

                        break;

                    case (int)PreLoginOptions.INSTANCE:
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        payloadLength = payload[offset++] << 8 | payload[offset++];

                        byte ERROR_INST = 0x1;
                        byte instanceResult = payload[payloadOffset];

                        if (instanceResult == ERROR_INST)
                        {
                            // Check if server says ERROR_INST. That either means the cached info
                            // we used to connect is not valid or we connected to a named instance
                            // listening on default params.
                            return PreLoginStatus.InstanceFailure;
                        }

                        break;

                    case (int)PreLoginOptions.THREADID:
                        // DO NOTHING FOR THREADID
                        offset += 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        payloadLength = payload[offset++] << 8 | payload[offset++];

                        context.ConnectionContext.MarsCapable = (payload[payloadOffset] == 0 ? false : true);

                        Debug.Assert(payload[payloadOffset] == 0 || payload[payloadOffset] == 1, "Value for Mars PreLoginHandshake option not equal to 1 or 0!");
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        // DO NOTHING FOR TRACEID
                        offset += 4;
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        payloadLength = payload[offset++] << 8 | payload[offset++];

                        // Only 0x00 and 0x01 are accepted values from the server.
                        if (payload[payloadOffset] != 0x00 && payload[payloadOffset] != 0x01)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}|ERR> {1}, " +
                                "Server sent an unexpected value for FedAuthRequired PreLogin Option. Value was {2}.", "ReadPreLoginresponse", ObjectID, (int)payload[payloadOffset]);
                            throw SQL.ParsingErrorValue(ParsingErrorState.FedAuthRequiredPreLoginResponseInvalidValue, (int)payload[payloadOffset]);
                        }

                        // We must NOT use the response for the FEDAUTHREQUIRED PreLogin option, if the connection string option
                        // was not using the new Authentication keyword or in other words, if Authentication=NotSpecified
                        // Or AccessToken is not null, mean token based authentication is used.
                        if ((context.ConnectionContext.ConnectionString != null
                            && context.ConnectionContext.ConnectionString.Authentication != SqlAuthenticationMethod.NotSpecified)
                            || context.ConnectionContext.AccessTokenInBytes != null || context.ConnectionContext.AccessTokenCallback != null)
                        {
                            context.ConnectionContext.FedAuthRequired = payload[payloadOffset] == 0x01 ? true : false;
                        }
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in ConsumePreLoginHandshake, option:" + option);

                        // DO NOTHING FOR THESE UNKNOWN OPTIONS
                        offset += 4;

                        break;
                }

                if (offset < payload.Length)
                {
                    option = payload[offset++];
                }
                else
                {
                    break;
                }
            }

            if (context.InternalEncryptionOption == EncryptionOptions.ON ||
                context.InternalEncryptionOption == EncryptionOptions.LOGIN)
            {
                if (!serverSupportsEncryption)
                {
                    //_physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                    //_physicalStateObj.Dispose();
                    //ThrowExceptionAndWarning(_physicalStateObj);
                    context.ConnectionContext.Error = new Exception("Encryption not supported by server");
                    return PreLoginStatus.Failed;
                }

                context.ValidateCertificate = ShouldValidateSertificate(context);

                await EnableSsl(context, isAsync, ct).ConfigureAwait(false);
            }

            return PreLoginStatus.Successful;

            static bool ShouldValidateSertificate(PreLoginHandlerContext context)
            {
                // Validate Certificate if Trust Server Certificate=false and Encryption forced (EncryptionOptions.ON) from Server.
                return (context.InternalEncryptionOption == EncryptionOptions.ON && !context.TrustServerCert) ||
                                    (context.ConnectionContext.AccessTokenInBytes != null && !context.TrustServerCert);
            }
        }

        private async Task CreatePreLoginAndSend(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(context.ConnectionContext.TdsStream != null, "A Tds Stream is expected");

            TdsStream tdsStream = context.ConnectionContext.TdsStream;
            tdsStream.PacketHeaderType = TdsStreamPacketType.PreLogin;

            // Initialize option offset into payload buffer
            // 5 bytes for each option (1 byte length, 2 byte offset, 2 byte payload length)
            int offset = (int)PreLoginOptions.NUMOPT * 5 + 1;

            byte[] payload = new byte[(int)PreLoginOptions.NUMOPT * 5 + TdsEnums.MAX_PRELOGIN_PAYLOAD_LENGTH];
            int payloadLength = 0;

            byte[] instanceName = new byte[1];

            for (int option = (int)PreLoginOptions.VERSION; option < (int)PreLoginOptions.NUMOPT; option++)
            {
                int optionDataSize = 0;

                // Fill in the option
                await tdsStream.WriteByteAsync((byte)option, isAsync, ct);

                // Fill in the offset of the option data
                await tdsStream.WriteByteAsync((byte)((offset & 0xff00) >> 8), isAsync, ct); // send upper order byte
                await tdsStream.WriteByteAsync((byte)(offset & 0x00ff), isAsync, ct); // send lower order byte

                switch (option)
                {
                    case (int)PreLoginOptions.VERSION:
                        Version systemDataVersion = ADP.GetAssemblyVersion();

                        // Major and minor
                        payload[payloadLength++] = (byte)(systemDataVersion.Major & 0xff);
                        payload[payloadLength++] = (byte)(systemDataVersion.Minor & 0xff);

                        // Build (Big Endian)
                        payload[payloadLength++] = (byte)((systemDataVersion.Build & 0xff00) >> 8);
                        payload[payloadLength++] = (byte)(systemDataVersion.Build & 0xff);

                        // Sub-build (Little Endian)
                        payload[payloadLength++] = (byte)(systemDataVersion.Revision & 0xff);
                        payload[payloadLength++] = (byte)((systemDataVersion.Revision & 0xff00) >> 8);
                        offset += 6;
                        optionDataSize = 6;
                        break;

                    case (int)PreLoginOptions.ENCRYPT:
                        if (context.InternalEncryptionOption == EncryptionOptions.NOT_SUP)
                        {
                            //If OS doesn't support encryption and encryption is not required, inform server "not supported" by client.
                            payload[payloadLength] = (byte)EncryptionOptions.NOT_SUP;
                        }
                        else
                        {
                            // Else, inform server of user request.
                            if (context.ConnectionEncryptionOption == SqlConnectionEncryptOption.Mandatory)
                            {
                                payload[payloadLength] = (byte)EncryptionOptions.ON;
                                context.InternalEncryptionOption = EncryptionOptions.ON;
                            }
                            else
                            {
                                payload[payloadLength] = (byte)EncryptionOptions.OFF;
                                context.InternalEncryptionOption = EncryptionOptions.OFF;
                            }
                        }

                        payloadLength += 1;
                        offset += 1;
                        optionDataSize = 1;
                        break;

                    case (int)PreLoginOptions.INSTANCE:
                        int i = 0;

                        while (instanceName[i] != 0)
                        {
                            payload[payloadLength] = instanceName[i];
                            payloadLength++;
                            i++;
                        }

                        payload[payloadLength] = 0; // null terminate
                        payloadLength++;
                        i++;

                        offset += i;
                        optionDataSize = i;
                        break;

                    case (int)PreLoginOptions.THREADID:
                        int threadID = TdsParserStaticMethods.GetCurrentThreadIdForTdsLoginOnly();

                        payload[payloadLength++] = (byte)((0xff000000 & threadID) >> 24);
                        payload[payloadLength++] = (byte)((0x00ff0000 & threadID) >> 16);
                        payload[payloadLength++] = (byte)((0x0000ff00 & threadID) >> 8);
                        payload[payloadLength++] = (byte)(0x000000ff & threadID);
                        offset += 4;
                        optionDataSize = 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payload[payloadLength++] = (byte)(context.ConnectionContext.ConnectionString.MARS ? 1 : 0);
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        context.ConnectionContext.ConnectionId.TryWriteBytes(payload.AsSpan(payloadLength, GUID_SIZE));
                        payloadLength += GUID_SIZE;
                        offset += GUID_SIZE;
                        optionDataSize = GUID_SIZE;

                        ActivityCorrelator.ActivityId actId = ActivityCorrelator.Next();
                        actId.Id.TryWriteBytes(payload.AsSpan(payloadLength, GUID_SIZE));
                        payloadLength += GUID_SIZE;
                        payload[payloadLength++] = (byte)(0x000000ff & actId.Sequence);
                        payload[payloadLength++] = (byte)((0x0000ff00 & actId.Sequence) >> 8);
                        payload[payloadLength++] = (byte)((0x00ff0000 & actId.Sequence) >> 16);
                        payload[payloadLength++] = (byte)((0xff000000 & actId.Sequence) >> 24);
                        int actIdSize = GUID_SIZE + sizeof(uint);
                        offset += actIdSize;
                        optionDataSize += actIdSize;
                        SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.SendPreLoginHandshake|INFO> ClientConnectionID {0}, ActivityID {1}", 
                            context.ConnectionContext.ConnectionId, actId);
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payload[payloadLength++] = 0x01;
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in SendPreLoginHandshake");
                        break;
                }

                await tdsStream.WriteByteAsync((byte)((optionDataSize & 0xff00) >> 8), isAsync, ct).ConfigureAwait(false);
                await tdsStream.WriteByteAsync((byte)(optionDataSize & 0x00ff), isAsync, ct).ConfigureAwait(false);
            }

            // Write out last option - to let server know the second part of packet completed
            await tdsStream.WriteByteAsync((byte)PreLoginOptions.LASTOPT, isAsync, ct).ConfigureAwait(false);

            if (isAsync)
            { 
                // Write out payload
                await tdsStream.WriteAsync(payload.AsMemory(0, payloadLength), ct).ConfigureAwait(false);
            }
            else
            {
                tdsStream.Write(payload.AsSpan(0, payloadLength));
            }
            // Flush packet
            
            if (isAsync)
            {
                await tdsStream.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                tdsStream.Flush();
            }
        }

        private void ReorderStream(PreLoginHandlerContext context)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Takes care of beginning TLS handshake.
        /// </summary>
        /// <param name="preloginContext"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async ValueTask TlsBegin(PreLoginHandlerContext preloginContext, bool isAsync, CancellationToken ct)
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
                    if (!_validateCert)
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

        private async ValueTask EnableSsl(PreLoginHandlerContext request, bool isAsync, CancellationToken ct)
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
                            nameof(EnableSsl), 
                            SqlClientLogger.LogLevel.Warning, 
                            warningMessage);
                    }
                    else
                    {
                        // This logs console warning of insecure protocol in use.
                        request.ConnectionContext.Logger.LogWarning(nameof(PreloginHandler), nameof(EnableSsl), warningMessage);
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
                    _validateCert = context.ValidateCertificate;
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
                            await sslStream.AuthenticateAsClientAsync(options).ConfigureAwait(false);
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
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Authentication exception occurred: {1}", args0: _connectionId, args1: aue?.Message);
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
            public bool ValidateCertificate { get; internal set; }
            public SNIError SniError { get; internal set; }

            public PreLoginHandlerContext(ConnectionHandlerContext connectionContext)
            {
                ConnectionContext = connectionContext;
                SqlConnectionString connectionOptions = connectionContext.ConnectionString;
                ConnectionEncryptionOption = connectionOptions.Encrypt;
                IsTlsFirst = (ConnectionEncryptionOption == SqlConnectionEncryptOption.Strict);
                TrustServerCert = connectionOptions.TrustServerCertificate;
                IntegratedSecurity = connectionOptions.IntegratedSecurity;
                AuthType = connectionOptions.Authentication;
                HostNameInCertificate = connectionOptions.HostNameInCertificate;
                ServerCertificateFilename = connectionOptions.ServerCertificate;
            }
        }

        private enum PreLoginStatus
        {
            Successful,
            InstanceFailure,
            Failed
        }
    }
}
