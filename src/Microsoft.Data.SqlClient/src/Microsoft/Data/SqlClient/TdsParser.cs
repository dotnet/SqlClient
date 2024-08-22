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

            // read SSPI data received from server
            Debug.Assert(_physicalStateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
            TdsOperationStatus result = _physicalStateObj.TryReadByteArray(receivedBuff, receivedLength);
            if (result != TdsOperationStatus.Done)
            {
                throw SQL.SynchronousCallMayNotPend();
            }

            // allocate send buffer and initialize length
            byte[] rentedSendBuff = ArrayPool<byte>.Shared.Rent((int)_authenticationProvider!.MaxSSPILength);
            byte[] sendBuff = rentedSendBuff; // need to track these separately in case someone updates the ref parameter
            uint sendLength = _authenticationProvider.MaxSSPILength;

            // make call for SSPI data
            _authenticationProvider.SSPIData(receivedBuff.AsMemory(0, receivedLength), ref sendBuff, ref sendLength, _sniSpnBuffer);

            // DO NOT SEND LENGTH - TDS DOC INCORRECT!  JUST SEND SSPI DATA!
            _physicalStateObj.WriteByteArray(sendBuff, (int)sendLength, 0);

            ArrayPool<byte>.Shared.Return(rentedSendBuff, clearArray: true);
            ArrayPool<byte>.Shared.Return(receivedBuff, clearArray: true);

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
#if NETFRAMEWORK
            SqlClientOriginalNetworkAddressInfo originalNetworkAddressInfo,
#endif
            SqlConnectionEncryptOption encrypt)
        {
            _physicalStateObj.SetTimeoutSeconds(rec._timeout);

            Debug.Assert(recoverySessionData == null || (requestedFeatures & TdsEnums.FeatureExtension.SessionRecovery) != 0, "Recovery session data without session recovery feature request");
            Debug.Assert(TdsEnums.MAXLEN_HOSTNAME >= rec._hostName.Length, "_workstationId.Length exceeds the max length for this value");

            Debug.Assert(!(rec._useSSPI && _connHandler._fedAuthRequired), "Cannot use SSPI when server has responded 0x01 for FedAuthRequired PreLogin Option.");
            Debug.Assert(!rec._useSSPI || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) == 0, "Cannot use both SSPI and FedAuth");
            Debug.Assert(fedAuthFeatureExtensionData == null || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0, "fedAuthFeatureExtensionData provided without fed auth feature request");
            Debug.Assert(fedAuthFeatureExtensionData != null || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) == 0, "Fed Auth feature requested without specifying fedAuthFeatureExtensionData.");

            Debug.Assert(rec._userName == null || (rec._userName != null && TdsEnums.MAXLEN_CLIENTID >= rec._userName.Length), "_userID.Length exceeds the max length for this value");
            Debug.Assert(rec._credential == null || (rec._credential != null && TdsEnums.MAXLEN_CLIENTID >= rec._credential.UserId.Length), "_credential.UserId.Length exceeds the max length for this value");

            Debug.Assert(rec._password == null || (rec._password != null && TdsEnums.MAXLEN_CLIENTSECRET >= rec._password.Length), "_password.Length exceeds the max length for this value");
            Debug.Assert(rec._credential == null || (rec._credential != null && TdsEnums.MAXLEN_CLIENTSECRET >= rec._credential.Password.Length), "_credential.Password.Length exceeds the max length for this value");

            Debug.Assert(rec._credential != null || rec._userName != null || rec._password != null, "cannot mix the new secure password system and the connection string based password");
            Debug.Assert(rec._newSecurePassword != null || rec._newPassword != null, "cannot have both new secure change password and string based change password");
            Debug.Assert(TdsEnums.MAXLEN_APPNAME >= rec._applicationName.Length, "_applicationName.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_SERVERNAME >= rec._serverName.Length, "_dataSource.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_LANGUAGE >= rec._language.Length, "_currentLanguage .Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_DATABASE >= rec._database.Length, "_initialCatalog.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_ATTACHDBFILE >= rec._attachDBFilename.Length, "_attachDBFileName.Length exceeds the max length for this value");

            Debug.Assert(_connHandler != null, "SqlConnectionInternalTds handler can not be null at this point.");
            _connHandler!.TimeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.LoginBegin);
            _connHandler.TimeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth);

#if NETFRAMEWORK
            // Add CTAIP Provider
            //
            if (originalNetworkAddressInfo != null)
            {
                SNINativeMethodWrapper.CTAIPProviderInfo cauthInfo = new SNINativeMethodWrapper.CTAIPProviderInfo();
                cauthInfo.originalNetworkAddress = originalNetworkAddressInfo.Address.GetAddressBytes();
                cauthInfo.fromDataSecurityProxy = originalNetworkAddressInfo.IsFromDataSecurityProxy;

                UInt32 error = SNINativeMethodWrapper.SNIAddProvider(_physicalStateObj.Handle, SNINativeMethodWrapper.ProviderEnum.CTAIP_PROV, cauthInfo);
                if (error != TdsEnums.SNI_SUCCESS)
                {
                    _physicalStateObj.AddError(ProcessSNIError(_physicalStateObj));
                    ThrowExceptionAndWarning(_physicalStateObj);
                }

                try
                { } // EmptyTry/Finally to avoid FXCop violation
                finally
                {
                    _physicalStateObj.ClearAllWritePackets();
                }
            }
#endif

            // get the password up front to use in sspi logic below
            byte[] encryptedPassword = null;
            byte[] encryptedChangePassword = null;
            int encryptedPasswordLengthInBytes;
            int encryptedChangePasswordLengthInBytes;
            bool useFeatureExt = (requestedFeatures != TdsEnums.FeatureExtension.None);

            string userName;

            if (rec._credential != null)
            {
                userName = rec._credential.UserId;
                encryptedPasswordLengthInBytes = rec._credential.Password.Length * 2;
            }
            else
            {
                userName = rec._userName;
                encryptedPassword = TdsParserStaticMethods.ObfuscatePassword(rec._password);
                encryptedPasswordLengthInBytes = encryptedPassword.Length;  // password in clear text is already encrypted and its length is in byte
            }

            if (rec._newSecurePassword != null)
            {
                encryptedChangePasswordLengthInBytes = rec._newSecurePassword.Length * 2;
            }
            else
            {
                encryptedChangePassword = TdsParserStaticMethods.ObfuscatePassword(rec._newPassword);
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
                length += (rec._hostName.Length + rec._applicationName.Length +
                            rec._serverName.Length + clientInterfaceName.Length +
                            rec._language.Length + rec._database.Length +
                            rec._attachDBFilename.Length) * 2;
                if (useFeatureExt)
                {
                    length += 4;
                }
            }

            // allocate memory for SSPI variables
            byte[] rentedSSPIBuff = null;
            byte[] outSSPIBuff = null; // track the rented buffer as a separate variable in case it is updated via the ref parameter
            uint outSSPILength = 0;

            // only add lengths of password and username if not using SSPI or requesting federated authentication info
            if (!rec._useSSPI && !(_connHandler._federatedAuthenticationInfoRequested || _connHandler._federatedAuthenticationRequested))
            {
                checked
                {
                    length += (userName.Length * 2) + encryptedPasswordLengthInBytes
                    + encryptedChangePasswordLengthInBytes;
                }
            }
            else
            {
                if (rec._useSSPI)
                {
                    // now allocate proper length of buffer, and set length
                    outSSPILength = _authenticationProvider.MaxSSPILength;
                    rentedSSPIBuff = ArrayPool<byte>.Shared.Rent((int)outSSPILength);
                    outSSPIBuff = rentedSSPIBuff;

                    // Call helper function for SSPI data and actual length.
                    // Since we don't have SSPI data from the server, send null for the
                    // byte[] buffer and 0 for the int length.
                    Debug.Assert(SniContext.Snix_Login == _physicalStateObj.SniContext, $"Unexpected SniContext. Expecting Snix_Login, actual value is '{_physicalStateObj.SniContext}'");
                    _physicalStateObj.SniContext = SniContext.Snix_LoginSspi;
                    _authenticationProvider.SSPIData(ReadOnlyMemory<byte>.Empty, ref outSSPIBuff, ref outSSPILength, _sniSpnBuffer);

                    if (outSSPILength > int.MaxValue)
                    {
                        throw SQL.InvalidSSPIPacketSize();  // SqlBu 332503
                    }
                    _physicalStateObj.SniContext = SniContext.Snix_Login;

                    checked
                    {
                        length += (int)outSSPILength;
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
                           outSSPIBuff,
                           outSSPILength);

            if (rentedSSPIBuff != null)
            {
                ArrayPool<byte>.Shared.Return(rentedSSPIBuff, clearArray: true);
            }

            _physicalStateObj.WritePacket(TdsEnums.HARDFLUSH);
            _physicalStateObj.ResetSecurePasswordsInformation();     // Password information is needed only from Login process; done with writing login packet and should clear information
            _physicalStateObj.HasPendingData = true;
            _physicalStateObj._messageStatus = 0;

#if NETFRAMEWORK
            // Remvove CTAIP Provider after login record is sent.
            //
            if (originalNetworkAddressInfo != null)
            {
                UInt32 error = SNINativeMethodWrapper.SNIRemoveProvider(_physicalStateObj.Handle, SNINativeMethodWrapper.ProviderEnum.CTAIP_PROV);
                if (error != TdsEnums.SNI_SUCCESS)
                {
                    _physicalStateObj.AddError(ProcessSNIError(_physicalStateObj));
                    ThrowExceptionAndWarning(_physicalStateObj);
                }

                try
                { } // EmptyTry/Finally to avoid FXCop violation
                finally
                {
                    _physicalStateObj.ClearAllWritePackets();
                }
            }
#endif
        }// tdsLogin
    }
}
