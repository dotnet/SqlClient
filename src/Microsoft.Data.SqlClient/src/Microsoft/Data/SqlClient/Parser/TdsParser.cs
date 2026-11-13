using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient.Parser.Login;
using Microsoft.Data.SqlClient.Utilities;

#nullable enable

namespace Microsoft.Data.SqlClient.Parser
{
    
    internal partial class TdsParser
    {
        private static readonly Encoding s_utf8EncodingWithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        internal void ProcessSSPI(int receivedLength)
        {
            Debug.Assert(_authenticationProvider is not null);

            SniContext outerContext = _physicalStateObj.SniContext;
            _physicalStateObj.SniContext = SniContext.Snix_ProcessSspi;

            // allocate received buffer based on length from SSPI message
            byte[] receivedBuff = ArrayPool<byte>.Shared.Rent(receivedLength);

            try
            {
                // read SSPI data received from server
                Debug.Assert(_physicalStateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
                TdsOperationStatus result = _physicalStateObj.TryReadByteArray(receivedBuff, receivedLength);
                if (result != TdsOperationStatus.Done)
                {
                    throw SQL.SynchronousCallMayNotPend();
                }

                // allocate send buffer and initialize length
                var writer = ObjectPools.BufferWriter.Rent();

                try
                {
                    // make call for SSPI data
                    _authenticationProvider!.WriteSSPIContext(receivedBuff.AsSpan(0, receivedLength), writer);

                    // DO NOT SEND LENGTH - TDS DOC INCORRECT!  JUST SEND SSPI DATA!
                    _physicalStateObj.WriteByteSpan(writer.WrittenSpan);

                }
                finally
                {
                    ObjectPools.BufferWriter.Return(writer);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(receivedBuff, clearArray: true);
            }

            // set message type so server knows its a SSPI response
            _physicalStateObj._outputMessageType = TdsEnums.MT_SSPI;

            // send to server
            _physicalStateObj.WritePacket(TdsEnums.HARDFLUSH);
            _physicalStateObj.SniContext = outerContext;
        }

#nullable disable

        /// <summary>
        /// The size of a PRELOGIN option header that follows the option token: a 2-byte data
        /// offset and a 2-byte data length.
        /// </summary>
        private const int PreLoginOptionHeaderSize = 4;

        /// <summary>
        /// Returns the exact data length the server must declare for a PRELOGIN option whose
        /// value the client reads, or <see langword="null"/> for options whose value the client
        /// does not read (including unknown options), which may declare any length.
        /// </summary>
        private static int? GetExpectedPreLoginOptionLength(int option) => option switch
        {
            // UL_VERSION (4 bytes) followed by US_SUBBUILD (2 bytes).
            (int)PreLoginOptions.VERSION => 6,
            (int)PreLoginOptions.ENCRYPT => 1,
            (int)PreLoginOptions.INSTANCE => 1,
            (int)PreLoginOptions.MARS => 1,
            (int)PreLoginOptions.FEDAUTHREQUIRED => 1,
            _ => null,
        };

        /// <summary>
        /// Reads and validates the PRELOGIN option header that starts at
        /// <paramref name="headerOffset"/>: a big-endian 2-byte data offset followed by a
        /// big-endian 2-byte data length. The header must lie within
        /// <paramref name="preLoginResponse"/>, the declared length must equal
        /// <paramref name="expectedBytes"/> when one is given, and the option data the header
        /// describes must lie within <paramref name="preLoginResponse"/>.
        /// </summary>
        /// <param name="preLoginResponse">The PRELOGIN response.</param>
        /// <param name="headerOffset">The offset of the option header.</param>
        /// <param name="expectedBytes">
        /// The exact data length the option must declare, or <see langword="null"/> to accept
        /// any declared length for options whose data the client does not read.
        /// </param>
        /// <returns>
        /// The offset and length of the option data within
        /// <paramref name="preLoginResponse"/>.
        /// </returns>
        private (int DataOffset, int DataLength) ReadPreLoginOptionHeader(
            byte[] preLoginResponse,
            int headerOffset,
            int? expectedBytes)
        {
            if (headerOffset + PreLoginOptionHeaderSize > preLoginResponse.Length)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "<sc.TdsParser.ConsumePreLoginHandshake|ERR> {0}, " +
                    "PreLogin option header at offset {1} exceeds response length {2}.",
                    ObjectID, headerOffset, preLoginResponse.Length);
                throw SQL.ParsingErrorOffset(ParsingErrorState.CorruptedTdsStream, headerOffset);
            }

            int dataOffset = preLoginResponse[headerOffset] << 8
                | preLoginResponse[headerOffset + 1];
            int dataLength = preLoginResponse[headerOffset + 2] << 8
                | preLoginResponse[headerOffset + 3];

            if (expectedBytes.HasValue && dataLength != expectedBytes.Value)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "<sc.TdsParser.ConsumePreLoginHandshake|ERR> {0}, " +
                    "PreLogin option length {1} does not match expected length {2}.",
                    ObjectID, dataLength, expectedBytes.Value);
                throw SQL.ParsingErrorLength(ParsingErrorState.CorruptedTdsStream, dataLength);
            }

            if (dataOffset + dataLength > preLoginResponse.Length)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "<sc.TdsParser.ConsumePreLoginHandshake|ERR> {0}, " +
                    "PreLogin option data at offset {1} with length {2} exceeds response " +
                    "length {3}.",
                    ObjectID, dataOffset, dataLength, preLoginResponse.Length);
                throw SQL.ParsingErrorOffset(ParsingErrorState.CorruptedTdsStream, dataOffset);
            }

            return (dataOffset, dataLength);
        }

        private PreLoginHandshakeStatus ConsumePreLoginHandshake(
            SqlConnectionEncryptOption encrypt,
            bool trustServerCert,
            bool integratedSecurity,
            out bool marsCapable,
            out bool fedAuthRequired,
            bool tlsFirst,
            string serverCertificateFilename)
        {
            // Assign default values
            marsCapable = _fMARS;
            fedAuthRequired = false;
            Debug.Assert(_physicalStateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
            TdsOperationStatus result = _physicalStateObj.TryReadNetworkPacket();
            if (result != TdsOperationStatus.Done)
            {
                throw SQL.SynchronousCallMayNotPend();
            }

            if (_physicalStateObj._inBytesRead == 0)
            {
                // If the server did not respond then something has gone wrong and we need to close the connection
                _physicalStateObj.AddError(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.PreloginError(), "", 0));
                _physicalStateObj.Dispose();
                ThrowExceptionAndWarning(_physicalStateObj);
            }

            if (_physicalStateObj.TryProcessHeader() != TdsOperationStatus.Done)
            {
                throw SQL.SynchronousCallMayNotPend();
            }

            if (_physicalStateObj._inBytesPacket > TdsEnums.MAX_PACKET_SIZE || _physicalStateObj._inBytesPacket <= 0)
            {
#if NETFRAMEWORK
                throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
#else
                throw SQL.ParsingError();
#endif
            }
            byte[] payload = new byte[_physicalStateObj._inBytesPacket];

            Debug.Assert(_physicalStateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
            result = _physicalStateObj.TryReadByteArray(payload, payload.Length);
            if (result != TdsOperationStatus.Done)
            {
                throw SQL.SynchronousCallMayNotPend();
            }

            if (payload[0] == 0xaa)
            {
                // If the first byte is 0xAA, we are connecting to a 6.5 or earlier server, which
                // is not supported.  SQL BU DT 296425
                throw SQL.InvalidSQLServerVersionUnknown();
            }

            int offset = 0;
            int option = payload[offset++];
            bool serverSupportsEncryption = false;
            bool terminatorFound = false;

            // The number of response bytes described by the option tokens, option headers and
            // option data read so far. A well-formed response is fully described by its options
            // and the terminator, so any mismatch with the response length means bytes that
            // belong to no option, or bytes claimed by more than one option.
            int describedBytes = 0;

            while (true)
            {
                if (option == (byte)PreLoginOptions.LASTOPT)
                {
                    terminatorFound = true;
                    describedBytes++;
                    break;
                }

                (int dataOffset, int dataLength) = ReadPreLoginOptionHeader(
                    payload, offset, GetExpectedPreLoginOptionLength(option));
                offset += PreLoginOptionHeaderSize;
                describedBytes += 1 + PreLoginOptionHeaderSize + dataLength;

                switch (option)
                {
                    case (int)PreLoginOptions.VERSION:
                        byte majorVersion = payload[dataOffset];
                        byte minorVersion = payload[dataOffset + 1];
                        int level = (payload[dataOffset + 2] << 8) |
                                             payload[dataOffset + 3];
                        break;

                    case (int)PreLoginOptions.ENCRYPT:
                        if (tlsFirst)
                        {
                            // Can skip/ignore this option's value if we are doing TDS 8.
                            break;
                        }

                        EncryptionOptions serverOption = (EncryptionOptions)payload[dataOffset];

                        /* internal enum EncryptionOptions {
                            OFF,
                            ON,
                            NOT_SUP,
                            REQ,
                            LOGIN,
                            OPTIONS_MASK = 0x3f
                        } */

                        // Any response other than NOT_SUP means the server supports encryption.
#if NETFRAMEWORK
                        serverSupportsEncryption = (serverOption & EncryptionOptions.OPTIONS_MASK) != EncryptionOptions.NOT_SUP;

                        switch (_encryptionOption & EncryptionOptions.OPTIONS_MASK)
#else
                        serverSupportsEncryption = serverOption != EncryptionOptions.NOT_SUP;

                        switch (_encryptionOption)
#endif
                        {
                            case (EncryptionOptions.OFF):
#if NETFRAMEWORK
                                if ((serverOption & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.OFF)
                                {
                                    // Only encrypt login.
                                    _encryptionOption = EncryptionOptions.LOGIN | (_encryptionOption & ~EncryptionOptions.OPTIONS_MASK);
                                }
                                else if ((serverOption & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.REQ)
                                {
                                    // Encrypt all.
                                    _encryptionOption = EncryptionOptions.ON | (_encryptionOption & ~EncryptionOptions.OPTIONS_MASK);
                                }
#else
                                if (serverOption == EncryptionOptions.OFF)
                                {
                                    // Only encrypt login.
                                    _encryptionOption = EncryptionOptions.LOGIN;
                                }
                                else if (serverOption == EncryptionOptions.REQ)
                                {
                                    // Encrypt all.
                                    _encryptionOption = EncryptionOptions.ON;
                                }
#endif
                                // NOT_SUP: No encryption.
                                break;

                            case (EncryptionOptions.NOT_SUP):
#if NETFRAMEWORK
                                if ((serverOption & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.REQ)
#else
                                if (serverOption == EncryptionOptions.REQ)
#endif
                                {
                                    // Server requires encryption, but client does not support it.
                                    _physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByClient(), "", 0));
                                    _physicalStateObj.Dispose();
                                    ThrowExceptionAndWarning(_physicalStateObj);
                                }

                                break;
                            default:
                                // Any other client option needs encryption
#if NETFRAMEWORK
                                if ((serverOption & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.NOT_SUP)
#else
                                if (serverOption == EncryptionOptions.NOT_SUP)
#endif
                                {
                                    _physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                                    _physicalStateObj.Dispose();
                                    ThrowExceptionAndWarning(_physicalStateObj);
                                }
                                break;
                        }

                        break;

                    case (int)PreLoginOptions.INSTANCE:
                        byte ERROR_INST = 0x1;
                        byte instanceResult = payload[dataOffset];

                        if (instanceResult == ERROR_INST)
                        {
                            // Check if server says ERROR_INST. That either means the cached info
                            // we used to connect is not valid or we connected to a named instance
                            // listening on default params.
                            return PreLoginHandshakeStatus.InstanceFailure;
                        }

                        break;

                    case (int)PreLoginOptions.MARS:
                        marsCapable = (payload[dataOffset] == 0 ? false : true);

                        Debug.Assert(payload[dataOffset] == 0 || payload[dataOffset] == 1, "Value for Mars PreLoginHandshake option not equal to 1 or 0!");
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        // Only 0x00 and 0x01 are accepted values from the server.
                        if (payload[dataOffset] != 0x00 && payload[dataOffset] != 0x01)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.ConsumePreLoginHandshake|ERR> {0}, " +
                                "Server sent an unexpected value for FedAuthRequired PreLogin Option. Value was {1}.", ObjectID, (int)payload[dataOffset]);
                            throw SQL.ParsingErrorValue(ParsingErrorState.FedAuthRequiredPreLoginResponseInvalidValue, (int)payload[dataOffset]);
                        }

                        // We must NOT use the response for the FEDAUTHREQUIRED PreLogin option, if the connection string option
                        // was not using the new Authentication keyword or in other words, if Authentication=NotSpecified
                        // Or an access token was supplied, which means token-based authentication is used.
                        if ((_connHandler.ConnectionOptions != null
                            && _connHandler.ConnectionOptions.Authentication != SqlAuthenticationMethod.NotSpecified)
                            || _connHandler.IsAccessTokenProvided)
                        {
                            fedAuthRequired = payload[dataOffset] == 0x01 ? true : false;
                        }
                        break;

                    case (int)PreLoginOptions.THREADID:
                    case (int)PreLoginOptions.TRACEID:
                    default:
                        // The client does not use the value of this option, and skips options it
                        // does not recognize. The header has already been validated above.
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

            if (!terminatorFound || describedBytes != payload.Length)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "<sc.TdsParser.ConsumePreLoginHandshake|ERR> {0}, " +
                    "PreLogin options describe {1} bytes but the response length is {2}. " +
                    "Terminator found: {3}.",
                    ObjectID, describedBytes, payload.Length, terminatorFound);
                throw SQL.ParsingErrorLength(ParsingErrorState.CorruptedTdsStream, payload.Length);
            }

#if NETFRAMEWORK
            if ((_encryptionOption & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.ON ||
                (_encryptionOption & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.LOGIN)
#else
            if (_encryptionOption == EncryptionOptions.ON ||
                _encryptionOption == EncryptionOptions.LOGIN)
#endif
            {
                if (!serverSupportsEncryption)
                {
                    _physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                    _physicalStateObj.Dispose();
                    ThrowExceptionAndWarning(_physicalStateObj);
                }

                // Validate Certificate if Trust Server Certificate=false and Encryption forced (EncryptionOptions.ON) from Server.
                bool shouldValidateServerCert = (_encryptionOption == EncryptionOptions.ON && !trustServerCert) ||
                    (_connHandler.IsAccessTokenProvided && !trustServerCert);

                uint info = (shouldValidateServerCert ? TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE : 0)
                    | TdsEnums.SNI_SSL_USE_SCHANNEL_CACHE;

                EnableSsl(info, encrypt, integratedSecurity, serverCertificateFilename);
            }

            return PreLoginHandshakeStatus.Successful;
        }

        internal void TdsLogin(
            SqlLogin rec,
            TdsEnums.FeatureExtension requestedFeatures,
            SessionData recoverySessionData,
            FederatedAuthenticationFeatureExtensionData fedAuthFeatureExtensionData,
            SqlConnectionEncryptOption encrypt)
        {
            _physicalStateObj.SetTimeoutSeconds(rec.timeout);

            Debug.Assert(recoverySessionData == null || (requestedFeatures & TdsEnums.FeatureExtension.SessionRecovery) != 0, "Recovery session data without session recovery feature request");
            Debug.Assert(TdsEnums.MAXLEN_HOSTNAME >= rec.hostName.Length, "_workstationId.Length exceeds the max length for this value");

            Debug.Assert(!(rec.useSSPI && _connHandler._fedAuthRequired), "Cannot use SSPI when server has responded 0x01 for FedAuthRequired PreLogin Option.");
            Debug.Assert(!rec.useSSPI || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) == 0, "Cannot use both SSPI and FedAuth");
            Debug.Assert(fedAuthFeatureExtensionData == null || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0, "fedAuthFeatureExtensionData provided without fed auth feature request");
            Debug.Assert(fedAuthFeatureExtensionData != null || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) == 0, "Fed Auth feature requested without specifying fedAuthFeatureExtensionData.");

            Debug.Assert(rec.userName == null || (rec.userName != null && TdsEnums.MAXLEN_CLIENTID >= rec.userName.Length), "_userID.Length exceeds the max length for this value");
            Debug.Assert(rec.credential == null || (rec.credential != null && TdsEnums.MAXLEN_CLIENTID >= rec.credential.UserId.Length), "_credential.UserId.Length exceeds the max length for this value");

            Debug.Assert(rec.password == null || (rec.password != null && TdsEnums.MAXLEN_CLIENTSECRET >= rec.password.Length), "_password.Length exceeds the max length for this value");
            Debug.Assert(rec.credential == null || (rec.credential != null && TdsEnums.MAXLEN_CLIENTSECRET >= rec.credential.Password.Length), "_credential.Password.Length exceeds the max length for this value");

            Debug.Assert(rec.credential != null || rec.userName != null || rec.password != null, "cannot mix the new secure password system and the connection string based password");
            Debug.Assert(rec.newSecurePassword != null || rec.newPassword != null, "cannot have both new secure change password and string based change password");
            Debug.Assert(TdsEnums.MAXLEN_APPNAME >= rec.applicationName.Length, "_applicationName.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_SERVERNAME >= rec.serverName.Length, "_dataSource.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_LANGUAGE >= rec.language.Length, "_currentLanguage .Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_DATABASE >= rec.database.Length, "_initialCatalog.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_ATTACHDBFILE >= rec.attachDBFilename.Length, "_attachDBFileName.Length exceeds the max length for this value");

            Debug.Assert(_connHandler != null, "SqlConnectionInternalTds handler can not be null at this point.");
            _connHandler!.TimeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.LoginBegin);
            _connHandler.TimeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth);

            // get the password up front to use in sspi logic below
            byte[] encryptedPassword = null;
            byte[] encryptedChangePassword = null;
            int encryptedPasswordLengthInBytes;
            int encryptedChangePasswordLengthInBytes;
            bool useFeatureExt = (requestedFeatures != TdsEnums.FeatureExtension.None);

            string userName;

            if (rec.credential != null)
            {
                userName = rec.credential.UserId;
                encryptedPasswordLengthInBytes = rec.credential.Password.Length * 2;
            }
            else
            {
                userName = rec.userName;
                encryptedPassword = TdsParserStaticMethods.ObfuscatePassword(rec.password);
                encryptedPasswordLengthInBytes = encryptedPassword.Length;  // password in clear text is already encrypted and its length is in byte
            }

            if (rec.newSecurePassword != null)
            {
                encryptedChangePasswordLengthInBytes = rec.newSecurePassword.Length * 2;
            }
            else
            {
                encryptedChangePassword = TdsParserStaticMethods.ObfuscatePassword(rec.newPassword);
                encryptedChangePasswordLengthInBytes = encryptedChangePassword.Length;
            }

            // set the message type
            _physicalStateObj._outputMessageType = TdsEnums.MT_LOGIN7;

            // length in bytes
            int length = TdsEnums.SQL2005_LOG_REC_FIXED_LEN;

            string clientInterfaceName = TdsEnums.SQL_PROVIDER_NAME;
            Debug.Assert(TdsEnums.MAXLEN_CLIENTINTERFACE >= clientInterfaceName.Length, "cchCltIntName can specify at most 128 unicode characters. See Tds spec");

            // add up variable-len portions (multiply by 2 for byte len of char strings)
            //
            checked
            {
                length += (rec.hostName.Length + rec.applicationName.Length +
                            rec.serverName.Length + clientInterfaceName.Length +
                            rec.language.Length + rec.database.Length +
                            rec.attachDBFilename.Length) * 2;
                if (useFeatureExt)
                {
                    length += 4;
                }
            }

            // allocate memory for SSPI variables
            ArrayBufferWriter<byte> sspiWriter = null;

            try
            {
                // only add lengths of password and username if not using SSPI or requesting federated authentication info
                if (!rec.useSSPI && !(_connHandler._federatedAuthenticationInfoRequested || _connHandler._federatedAuthenticationRequested))
                {
                    checked
                    {
                        length += (userName.Length * 2) + encryptedPasswordLengthInBytes
                        + encryptedChangePasswordLengthInBytes;
                    }
                }
                else
                {
                    if (rec.useSSPI)
                    {
                        sspiWriter = ObjectPools.BufferWriter.Rent();

                        // Call helper function for SSPI data and actual length.
                        // Since we don't have SSPI data from the server, send null for the
                        // byte[] buffer and 0 for the int length.
                        Debug.Assert(SniContext.Snix_Login == _physicalStateObj.SniContext, $"Unexpected SniContext. Expecting Snix_Login, actual value is '{_physicalStateObj.SniContext}'");
                        _physicalStateObj.SniContext = SniContext.Snix_LoginSspi;
                        _authenticationProvider.WriteSSPIContext(ReadOnlySpan<byte>.Empty, sspiWriter);

                        _physicalStateObj.SniContext = SniContext.Snix_Login;

                        checked
                        {
                            length += (int)sspiWriter.WrittenCount;
                        }
                    }
                }

                int feOffset = length;
                // calculate and reserve the required bytes for the featureEx
                length = ApplyFeatureExData(requestedFeatures, recoverySessionData, fedAuthFeatureExtensionData, useFeatureExt, length);

                WriteLoginData(rec,
                               requestedFeatures,
                               recoverySessionData,
                               fedAuthFeatureExtensionData,
                               encrypt,
                               encryptedPassword,
                               encryptedChangePassword,
                               encryptedPasswordLengthInBytes,
                               encryptedChangePasswordLengthInBytes,
                               useFeatureExt,
                               userName,
                               length,
                               feOffset,
                               clientInterfaceName,
                               sspiWriter is { } ? sspiWriter.WrittenSpan : ReadOnlySpan<byte>.Empty);
            }
            finally
            {
                if (sspiWriter is not null)
                {
                    ObjectPools.BufferWriter.Return(sspiWriter);
                }
            }

            _physicalStateObj.WritePacket(TdsEnums.HARDFLUSH);
            _physicalStateObj.ResetSecurePasswordsInformation();     // Password information is needed only from Login process; done with writing login packet and should clear information
            _physicalStateObj.HasPendingData = true;
            _physicalStateObj._messageStatus = 0;
        }// tdsLogin
    }
}
