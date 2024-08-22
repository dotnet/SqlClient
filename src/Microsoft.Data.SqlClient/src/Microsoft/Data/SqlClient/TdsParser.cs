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
            _physicalStateObj.SetTimeoutSeconds(rec.Timeout);

            Debug.Assert(recoverySessionData == null || (requestedFeatures & TdsEnums.FeatureExtension.SessionRecovery) != 0, "Recovery session data without session recovery feature request");
            Debug.Assert(TdsEnums.MAXLEN_HOSTNAME >= rec.HostName.Length, "_workstationId.Length exceeds the max length for this value");

            Debug.Assert(!(rec.UseSspi && _connHandler._fedAuthRequired), "Cannot use SSPI when server has responded 0x01 for FedAuthRequired PreLogin Option.");
            Debug.Assert(!rec.UseSspi || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) == 0, "Cannot use both SSPI and FedAuth");
            Debug.Assert(fedAuthFeatureExtensionData == null || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0, "fedAuthFeatureExtensionData provided without fed auth feature request");
            Debug.Assert(fedAuthFeatureExtensionData != null || (requestedFeatures & TdsEnums.FeatureExtension.FedAuth) == 0, "Fed Auth feature requested without specifying fedAuthFeatureExtensionData.");

            Debug.Assert(rec.UserName == null || (rec.UserName != null && TdsEnums.MAXLEN_CLIENTID >= rec.UserName.Length), "_userID.Length exceeds the max length for this value");
            Debug.Assert(rec.Credential == null || (rec.Credential != null && TdsEnums.MAXLEN_CLIENTID >= rec.Credential.UserId.Length), "_credential.UserId.Length exceeds the max length for this value");

            Debug.Assert(rec.Password == null || (rec.Password != null && TdsEnums.MAXLEN_CLIENTSECRET >= rec.Password.Length), "_password.Length exceeds the max length for this value");
            Debug.Assert(rec.Credential == null || (rec.Credential != null && TdsEnums.MAXLEN_CLIENTSECRET >= rec.Credential.Password.Length), "_credential.Password.Length exceeds the max length for this value");

            Debug.Assert(rec.Credential != null || rec.UserName != null || rec.Password != null, "cannot mix the new secure password system and the connection string based password");
            Debug.Assert(rec.NewSecurePassword != null || rec.NewPassword != null, "cannot have both new secure change password and string based change password");
            Debug.Assert(TdsEnums.MAXLEN_APPNAME >= rec.ApplicationName.Length, "_applicationName.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_SERVERNAME >= rec.ServerName.Length, "_dataSource.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_LANGUAGE >= rec.Language.Length, "_currentLanguage .Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_DATABASE >= rec.Database.Length, "_initialCatalog.Length exceeds the max length for this value");
            Debug.Assert(TdsEnums.MAXLEN_ATTACHDBFILE >= rec.AttachDbFilename.Length, "_attachDBFileName.Length exceeds the max length for this value");

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

            if (rec.Credential != null)
            {
                userName = rec.Credential.UserId;
                encryptedPasswordLengthInBytes = rec.Credential.Password.Length * 2;
            }
            else
            {
                userName = rec.UserName;
                encryptedPassword = TdsParserStaticMethods.ObfuscatePassword(rec.Password);
                encryptedPasswordLengthInBytes = encryptedPassword.Length;  // password in clear text is already encrypted and its length is in byte
            }

            if (rec.NewSecurePassword != null)
            {
                encryptedChangePasswordLengthInBytes = rec.NewSecurePassword.Length * 2;
            }
            else
            {
                encryptedChangePassword = TdsParserStaticMethods.ObfuscatePassword(rec.NewPassword);
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
                length += (rec.HostName.Length + rec.ApplicationName.Length +
                            rec.ServerName.Length + clientInterfaceName.Length +
                            rec.Language.Length + rec.Database.Length +
                            rec.AttachDbFilename.Length) * 2;
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
            if (!rec.UseSspi && !(_connHandler._federatedAuthenticationInfoRequested || _connHandler._federatedAuthenticationRequested))
            {
                checked
                {
                    length += (userName.Length * 2) + encryptedPasswordLengthInBytes
                    + encryptedChangePasswordLengthInBytes;
                }
            }
            else
            {
                if (rec.UseSspi)
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
