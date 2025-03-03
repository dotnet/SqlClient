using System;
using System.Buffers;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal partial class TdsParser
    {
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
                var writer = SqlObjectPools.BufferWriter.Rent();

                try
                {
                    // make call for SSPI data
                    _authenticationProvider!.SSPIData(receivedBuff.AsSpan(0, receivedLength), writer, _serverSpn);

                    // DO NOT SEND LENGTH - TDS DOC INCORRECT!  JUST SEND SSPI DATA!
                    _physicalStateObj.WriteByteSpan(writer.WrittenSpan);

                }
                finally
                {
                    SqlObjectPools.BufferWriter.Return(writer);
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
                        sspiWriter = SqlObjectPools.BufferWriter.Rent();

                        // Call helper function for SSPI data and actual length.
                        // Since we don't have SSPI data from the server, send null for the
                        // byte[] buffer and 0 for the int length.
                        Debug.Assert(SniContext.Snix_Login == _physicalStateObj.SniContext, $"Unexpected SniContext. Expecting Snix_Login, actual value is '{_physicalStateObj.SniContext}'");
                        _physicalStateObj.SniContext = SniContext.Snix_LoginSspi;
                        _authenticationProvider.SSPIData(ReadOnlySpan<byte>.Empty, sspiWriter, _serverSpn);

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
                    SqlObjectPools.BufferWriter.Return(sspiWriter);
                }
            }

            _physicalStateObj.WritePacket(TdsEnums.HARDFLUSH);
            _physicalStateObj.ResetSecurePasswordsInformation();     // Password information is needed only from Login process; done with writing login packet and should clear information
            _physicalStateObj.HasPendingData = true;
            _physicalStateObj._messageStatus = 0;
        }// tdsLogin
    }
}
