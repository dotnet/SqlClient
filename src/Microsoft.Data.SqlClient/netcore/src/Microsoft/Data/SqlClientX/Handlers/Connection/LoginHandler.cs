// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Handlers.Connection.Login;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// A handler for Sql Server login. 
    /// </summary>
    internal class LoginHandler : IHandler<ConnectionHandlerContext>
    {
        // NIC address caching
        private static byte[] s_nicAddress;             // cache the NIC address from the registry
        
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        public async ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            ValidateIncomingContext(context);

            LoginHandlerContext loginHandlerContext = new LoginHandlerContext(context);
            await PrepareLoginDetails(loginHandlerContext, isAsync, ct).ConfigureAwait(false);

            void ValidateIncomingContext(ConnectionHandlerContext context)
            {
                if (context.ConnectionString is null)
                {
                    throw new ArgumentNullException(nameof(context.ConnectionString));
                }

                if (context.DataSource is null)
                {
                    throw new ArgumentNullException(nameof(context.DataSource));
                }

                if (context.ConnectionStream is null)
                {
                    throw new ArgumentNullException(nameof(context.ConnectionStream));
                }

                if (context.Error is not null)
                {
                    return;
                }
            }
        }

        private async ValueTask PrepareLoginDetails(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            SqlLogin login = new SqlLogin();

            PasswordChangeRequest passwordChangeRequest = context.ConnectionContext.PasswordChangeRequest;

            // gather all the settings the user set in the connection string or
            // properties and do the login
            string currentDatabase = context.ServerInfo.ResolvedDatabaseName;

            string currentLanguage = context.ConnectionOptions.CurrentLanguage;

            TimeoutTimer timeout = context.ConnectionContext.TimeoutTimer;

            // If a timeout tick value is specified, compute the timeout based
            // upon the amount of time left in seconds.

            // TODO: Rethink timeout handling.

            int timeoutInSeconds = 0;

            if (!timeout.IsInfinite)
            {
                long t = timeout.MillisecondsRemaining / 1000;

                // This change was done because the timeout 0 being sent to SNI led to infinite timeout.
                // TODO: is this really needed for Managed code? 
                if (t == 0 && LocalAppContextSwitches.UseMinimumLoginTimeout)
                {
                    // Take 1 as the minimum value, since 0 is treated as an infinite timeout
                    // to allow 1 second more for login to complete, since it should take only a few milliseconds.
                    t = 1;
                }

                if (int.MaxValue > t)
                {
                    timeoutInSeconds = (int)t;
                }
            }

            login.authentication = context.ConnectionOptions.Authentication;
            login.timeout = timeoutInSeconds;
            login.userInstance = context.ConnectionOptions.UserInstance;
            login.hostName = context.ConnectionOptions.ObtainWorkstationId();
            login.userName = context.ConnectionOptions.UserID;
            login.password = context.ConnectionOptions.Password;
            login.applicationName = context.ConnectionOptions.ApplicationName;

            login.language = currentLanguage;
            if (!login.userInstance)
            {
                // Do not send attachdbfilename or database to SSE primary instance
                login.database = currentDatabase;
                login.attachDBFilename = context.ConnectionOptions.AttachDBFilename;
            }

            // VSTS#795621 - Ensure ServerName is Sent During TdsLogin To Enable Sql Azure Connectivity.
            // Using server.UserServerName (versus ConnectionOptions.DataSource) since TdsLogin requires
            // serverName to always be non-null.
            login.serverName = context.ServerInfo.UserServerName;

            login.useReplication = context.ConnectionOptions.Replication;
            login.useSSPI = context.ConnectionOptions.IntegratedSecurity  // Treat AD Integrated like Windows integrated when against a non-FedAuth endpoint
                                     || (context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated 
                                     && !context.ConnectionContext.FedAuthRequired);
            login.packetSize = context.ConnectionOptions.PacketSize;
            login.newPassword = passwordChangeRequest?.NewPassword;
            login.readOnlyIntent = context.ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly;
            login.credential = passwordChangeRequest?.Credential;
            if (passwordChangeRequest?.NewSecurePassword != null)
            {
                login.newSecurePassword = passwordChangeRequest?.NewSecurePassword;
            }

            TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            FeatureExtensions features = context.Features;
            if (context.ConnectionOptions.ConnectRetryCount > 0)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.SessionRecovery;
                features.SessionRecoveryRequested = true;
            }

            
            if (ShouldRequestFedAuth(context))
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                features.FederatedAuthenticationInfoRequested = true;
                features.FedAuthFeatureExtensionData =
                    new FederatedAuthenticationFeatureExtensionData
                    {
                        libraryType = TdsEnums.FedAuthLibrary.MSAL,
                        authentication = context.ConnectionOptions.Authentication,
                        fedAuthRequiredPreLoginResponse = context.ConnectionContext.FedAuthRequired
                    };
            }

            if (context.ConnectionContext.AccessTokenInBytes != null)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                features.FedAuthFeatureExtensionData = new FederatedAuthenticationFeatureExtensionData
                {
                    libraryType = TdsEnums.FedAuthLibrary.SecurityToken,
                    fedAuthRequiredPreLoginResponse = context.ConnectionContext.FedAuthRequired,
                    accessToken = context.ConnectionContext.AccessTokenInBytes
                };
                // No need any further info from the server for token based authentication. So set _federatedAuthenticationRequested to true
                features.FederatedAuthenticationRequested = true;
            }

            // The GLOBALTRANSACTIONS, DATACLASSIFICATION, TCE, and UTF8 support features are implicitly requested
            requestedFeatures |= TdsEnums.FeatureExtension.GlobalTransactions | TdsEnums.FeatureExtension.DataClassification | TdsEnums.FeatureExtension.Tce | TdsEnums.FeatureExtension.UTF8Support;

            // The SQLDNSCaching feature is implicitly set
            requestedFeatures |= TdsEnums.FeatureExtension.SQLDNSCaching;

            features.RequestedFeatures = requestedFeatures;
            context.Login = login;

            await TdsLogin(context, isAsync, ct).ConfigureAwait(false);

            // If the workflow being used is Active Directory Authentication and server's prelogin response
            // for FEDAUTHREQUIRED option indicates Federated Authentication is required, we have to insert FedAuth Feature Extension
            // in Login7, indicating the intent to use Active Directory Authentication for SQL Server.
            static bool ShouldRequestFedAuth(LoginHandlerContext context)
            {
                return context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
                                // Since AD Integrated may be acting like Windows integrated, additionally check _fedAuthRequired
                                || (context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && context.ConnectionContext.FedAuthRequired)
                                || context.ConnectionContext.AccessTokenCallback != null;
            }
        }

        private async ValueTask TdsLogin(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            // TODO: Set the timeout
            _ = context.Login.timeout;

            // TODO: Add debug asserts.

            // TODO: Add timeout internal details.

            // TODO: Password Change

            context.ConnectionContext.TdsStream.PacketHeaderType = TdsStreamPacketType.Login7;

            // Fixed length of the login record
            int length = TdsEnums.SQL2005_LOG_REC_FIXED_LEN;


            string clientInterfaceName = TdsEnums.SQL_PROVIDER_NAME;
            Debug.Assert(TdsEnums.MAXLEN_CLIENTINTERFACE >= clientInterfaceName.Length, "cchCltIntName can specify at most 128 unicode characters. See Tds spec");

            SqlLogin rec = context.Login;

            // Calculate the fixed length
            checked
            {
                

                length += (rec.hostName.Length + rec.applicationName.Length +
                            rec.serverName.Length + clientInterfaceName.Length +
                            rec.language.Length + rec.database.Length +
                            rec.attachDBFilename.Length) * 2;
                if (context.Features.RequestedFeatures != TdsEnums.FeatureExtension.None)
                {
                    length += 4;
                }
            }

            byte[] rentedSSPIBuff = null;
            byte[] outSSPIBuff = null; // track the rented buffer as a separate variable in case it is updated via the ref parameter
            uint outSSPILength = 0;

            // only add lengths of password and username if not using SSPI or requesting federated authentication info
            if (!rec.useSSPI && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
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

            await WriteLoginData(rec,
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
                           outSSPILength,
                           isAsync,
                           ct).ConfigureAwait(false);

            if (rentedSSPIBuff != null)
            {
                ArrayPool<byte>.Shared.Return(rentedSSPIBuff, clearArray: true);
            }


            TdsStream stream = context.ConnectionContext.TdsStream;
            ct.ThrowIfCancellationRequested();
            
            if (isAsync)
            {
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                stream.Flush();
            }

            _physicalStateObj.ResetSecurePasswordsInformation();     // Password information is needed only from Login process; done with writing login packet and should clear information
            _physicalStateObj.HasPendingData = true;
            _physicalStateObj._messageStatus = 0;
        }

        private ValueTask<int> ApplyFeatureExData(LoginHandlerContext context,
                                        TdsEnums.FeatureExtension requestedFeatures,
                                        SessionData recoverySessionData,
                                        FederatedAuthenticationFeatureExtensionData fedAuthFeatureExtensionData,
                                        bool useFeatureExt,
                                        int length,
                                        bool isAsync,
                                        CancellationToken ct,
                                        bool write = false)
        {
            if (useFeatureExt)
            {
                if ((requestedFeatures & TdsEnums.FeatureExtension.SessionRecovery) != 0)
                {
                    length += WriteSessionRecoveryFeatureRequest(recoverySessionData, write);
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.TdsLogin|SEC> Sending federated authentication feature request & wirte = {0}", write);
                    Debug.Assert(fedAuthFeatureExtensionData != null, "fedAuthFeatureExtensionData should not null.");
                    length += WriteFedAuthFeatureRequest(fedAuthFeatureExtensionData, write: write);
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.Tce) != 0)
                {
                    length += WriteTceFeatureRequest(write);
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.GlobalTransactions) != 0)
                {
                    length += WriteGlobalTransactionsFeatureRequest(write);
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.DataClassification) != 0)
                {
                    length += WriteDataClassificationFeatureRequest(write);
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.UTF8Support) != 0)
                {
                    length += WriteUTF8SupportFeatureRequest(write);
                }

                if ((requestedFeatures & TdsEnums.FeatureExtension.SQLDNSCaching) != 0)
                {
                    length += WriteSQLDNSCachingFeatureRequest(write);
                }

                length++; // for terminator
                if (write)
                {
                    _physicalStateObj.WriteByte(0xFF); // terminator
                }
            }

            return length;
        }


        internal int WriteSessionRecoveryFeatureRequest(LoginHandlerContext context, SessionData reconnectData, bool write /* if false just calculates the length */)
        {
            int len = 1;
            if (write)
            {
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_SRECOVERY);
            }
            if (reconnectData == null)
            {
                if (write)
                {
                    WriteInt(0, _physicalStateObj);
                }
                len += 4;
            }
            else
            {
                Debug.Assert(reconnectData._unrecoverableStatesCount == 0, "Unrecoverable state count should be 0");
                int initialLength = 0; // sizeof(DWORD) - length itself
                initialLength += 1 + 2 * TdsParserStaticMethods.NullAwareStringLength(reconnectData._initialDatabase);
                initialLength += 1 + 2 * TdsParserStaticMethods.NullAwareStringLength(reconnectData._initialLanguage);
                initialLength += (reconnectData._initialCollation == null) ? 1 : 6;
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._initialState[i] != null)
                    {
                        initialLength += 1 /* StateId*/ + StateValueLength(reconnectData._initialState[i].Length);
                    }
                }
                int currentLength = 0; // sizeof(DWORD) - length itself
                currentLength += 1 + 2 * (reconnectData._initialDatabase == reconnectData._database ? 0 : TdsParserStaticMethods.NullAwareStringLength(reconnectData._database));
                currentLength += 1 + 2 * (reconnectData._initialLanguage == reconnectData._language ? 0 : TdsParserStaticMethods.NullAwareStringLength(reconnectData._language));
                currentLength += (reconnectData._collation != null && !SqlCollation.Equals(reconnectData._collation, reconnectData._initialCollation)) ? 6 : 1;
                bool[] writeState = new bool[SessionData._maxNumberOfSessionStates];
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._delta[i] != null)
                    {
                        Debug.Assert(reconnectData._delta[i]._recoverable, "State should be recoverable");
                        writeState[i] = true;
                        if (reconnectData._initialState[i] != null && reconnectData._initialState[i].Length == reconnectData._delta[i]._dataLength)
                        {
                            writeState[i] = false;
                            for (int j = 0; j < reconnectData._delta[i]._dataLength; j++)
                            {
                                if (reconnectData._initialState[i][j] != reconnectData._delta[i]._data[j])
                                {
                                    writeState[i] = true;
                                    break;
                                }
                            }
                        }
                        if (writeState[i])
                        {
                            currentLength += 1 /* StateId*/ + StateValueLength(reconnectData._delta[i]._dataLength);
                        }
                    }
                }
                if (write)
                {
                    WriteInt(8 + initialLength + currentLength, _physicalStateObj); // length of data w/o total length (initial + current + 2 * sizeof(DWORD))
                    WriteInt(initialLength, _physicalStateObj);
                    WriteIdentifier(reconnectData._initialDatabase, _physicalStateObj);
                    WriteCollation(reconnectData._initialCollation, _physicalStateObj);
                    WriteIdentifier(reconnectData._initialLanguage, _physicalStateObj);
                    for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                    {
                        if (reconnectData._initialState[i] != null)
                        {
                            _physicalStateObj.WriteByte((byte)i);
                            if (reconnectData._initialState[i].Length < 0xFF)
                            {
                                _physicalStateObj.WriteByte((byte)reconnectData._initialState[i].Length);
                            }
                            else
                            {
                                _physicalStateObj.WriteByte(0xFF);
                                WriteInt(reconnectData._initialState[i].Length, _physicalStateObj);
                            }
                            _physicalStateObj.WriteByteArray(reconnectData._initialState[i], reconnectData._initialState[i].Length, 0);
                        }
                    }
                    WriteInt(currentLength, _physicalStateObj);
                    WriteIdentifier(reconnectData._database != reconnectData._initialDatabase ? reconnectData._database : null, _physicalStateObj);
                    WriteCollation(SqlCollation.Equals(reconnectData._initialCollation, reconnectData._collation) ? null : reconnectData._collation, _physicalStateObj);
                    WriteIdentifier(reconnectData._language != reconnectData._initialLanguage ? reconnectData._language : null, _physicalStateObj);
                    for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                    {
                        if (writeState[i])
                        {
                            _physicalStateObj.WriteByte((byte)i);
                            if (reconnectData._delta[i]._dataLength < 0xFF)
                            {
                                _physicalStateObj.WriteByte((byte)reconnectData._delta[i]._dataLength);
                            }
                            else
                            {
                                _physicalStateObj.WriteByte(0xFF);
                                WriteInt(reconnectData._delta[i]._dataLength, _physicalStateObj);
                            }
                            _physicalStateObj.WriteByteArray(reconnectData._delta[i]._data, reconnectData._delta[i]._dataLength, 0);
                        }
                    }
                }
                len += initialLength + currentLength + 12 /* length fields (initial, current, total) */;
            }
            return len;
        }

        internal int WriteFedAuthFeatureRequest(LoginHandlerContext context, FederatedAuthenticationFeatureExtensionData fedAuthFeatureData,
                                                bool write /* if false just calculates the length */)
        {
            Debug.Assert(fedAuthFeatureData.libraryType == TdsEnums.FedAuthLibrary.MSAL || fedAuthFeatureData.libraryType == TdsEnums.FedAuthLibrary.SecurityToken,
                "only fed auth library type MSAL and Security Token are supported in writing feature request");

            int dataLen = 0;
            int totalLen = 0;

            // set dataLen and totalLen
            switch (fedAuthFeatureData.libraryType)
            {
                case TdsEnums.FedAuthLibrary.MSAL:
                    dataLen = 2;  // length of feature data = 1 byte for library and echo + 1 byte for workflow
                    break;
                case TdsEnums.FedAuthLibrary.SecurityToken:
                    Debug.Assert(fedAuthFeatureData.accessToken != null, "AccessToken should not be null.");
                    dataLen = 1 + sizeof(int) + fedAuthFeatureData.accessToken.Length; // length of feature data = 1 byte for library and echo, security token length and sizeof(int) for token length itself
                    break;
                default:
                    Debug.Fail("Unrecognized library type for fedauth feature extension request");
                    break;
            }

            totalLen = dataLen + 5; // length of feature id (1 byte), data length field (4 bytes), and feature data (dataLen)

            // write feature id
            if (write)
            {
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_FEDAUTH);

                // set options
                byte options = 0x00;

                // set upper 7 bits of options to indicate fed auth library type
                switch (fedAuthFeatureData.libraryType)
                {
                    case TdsEnums.FedAuthLibrary.MSAL:
                        Debug.Assert(_connHandler._federatedAuthenticationInfoRequested == true, "_federatedAuthenticationInfoRequested field should be true");
                        options |= TdsEnums.FEDAUTHLIB_MSAL << 1;
                        break;
                    case TdsEnums.FedAuthLibrary.SecurityToken:
                        Debug.Assert(_connHandler._federatedAuthenticationRequested == true, "_federatedAuthenticationRequested field should be true");
                        options |= TdsEnums.FEDAUTHLIB_SECURITYTOKEN << 1;
                        break;
                    default:
                        Debug.Fail("Unrecognized FedAuthLibrary type for feature extension request");
                        break;
                }

                options |= (byte)(fedAuthFeatureData.fedAuthRequiredPreLoginResponse == true ? 0x01 : 0x00);

                // write dataLen and options
                WriteInt(dataLen, _physicalStateObj);
                _physicalStateObj.WriteByte(options);

                // write accessToken for FedAuthLibrary.SecurityToken
                switch (fedAuthFeatureData.libraryType)
                {
                    case TdsEnums.FedAuthLibrary.MSAL:
                        byte workflow = 0x00;
                        switch (fedAuthFeatureData.authentication)
                        {
                            case SqlAuthenticationMethod.ActiveDirectoryPassword:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYPASSWORD;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYINTEGRATED;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYINTERACTIVE;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYSERVICEPRINCIPAL;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYDEVICECODEFLOW;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                            case SqlAuthenticationMethod.ActiveDirectoryMSI:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYMANAGEDIDENTITY;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryDefault:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYDEFAULT;
                                break;
                            case SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity:
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYWORKLOADIDENTITY;
                                break;
                            default:
                                if (_connHandler._accessTokenCallback != null)
                                {
                                    workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYTOKENCREDENTIAL;
                                }
                                else
                                {
                                    Debug.Assert(false, "Unrecognized Authentication type for fedauth MSAL request");
                                }
                                break;
                        }

                        _physicalStateObj.WriteByte(workflow);
                        break;
                    case TdsEnums.FedAuthLibrary.SecurityToken:
                        WriteInt(fedAuthFeatureData.accessToken.Length, _physicalStateObj);
                        _physicalStateObj.WriteByteArray(fedAuthFeatureData.accessToken, fedAuthFeatureData.accessToken.Length, 0);
                        break;
                    default:
                        Debug.Fail("Unrecognized FedAuthLibrary type for feature extension request");
                        break;
                }
            }
            return totalLen;
        }

        internal int WriteTceFeatureRequest(LoginHandlerContext context, bool write /* if false just calculates the length */)
        {
            int len = 6; // (1byte = featureID, 4bytes = featureData length, 1 bytes = Version

            if (write)
            {
                // Write Feature ID, length of the version# field and TCE Version#
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_TCE);
                WriteInt(1, _physicalStateObj);
                _physicalStateObj.WriteByte(TdsEnums.MAX_SUPPORTED_TCE_VERSION);
            }

            return len; // size of data written
        }

        internal int WriteDataClassificationFeatureRequest(LoginHandlerContext context, bool write /* if false just calculates the length */)
        {
            int len = 6; // 1byte = featureID, 4bytes = featureData length, 1 bytes = Version

            if (write)
            {
                // Write Feature ID, length of the version# field and Sensitivity Classification Version#
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_DATACLASSIFICATION);
                WriteInt(1, _physicalStateObj);
                _physicalStateObj.WriteByte(TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED);
            }

            return len; // size of data written
        }

        internal int WriteGlobalTransactionsFeatureRequest(LoginHandlerContext context, bool write /* if false just calculates the length */)
        {
            int len = 5; // 1byte = featureID, 4bytes = featureData length

            if (write)
            {
                // Write Feature ID
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS);
                WriteInt(0, _physicalStateObj); // we don't send any data
            }

            return len;
        }
        internal int WriteUTF8SupportFeatureRequest(LoginHandlerContext context, bool write /* if false just calculates the length */)
        {
            int len = 5; // 1byte = featureID, 4bytes = featureData length, sizeof(DWORD)

            if (write)
            {
                // Write Feature ID
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_UTF8SUPPORT);
                WriteInt(0, _physicalStateObj); // we don't send any data
            }

            return len;
        }

        internal int WriteSQLDNSCachingFeatureRequest(LoginHandlerContext context, bool write /* if false just calculates the length */)
        {
            int len = 5; // 1byte = featureID, 4bytes = featureData length

            if (write)
            {
                // Write Feature ID
                _physicalStateObj.WriteByte(TdsEnums.FEATUREEXT_SQLDNSCACHING);
                WriteInt(0, _physicalStateObj); // we don't send any data
            }

            return len;
        }


        private async ValueTask WriteLoginData(SqlLogin rec,
                                     TdsEnums.FeatureExtension requestedFeatures,
                                     SessionData recoverySessionData,
                                     FederatedAuthenticationFeatureExtensionData fedAuthFeatureExtensionData,
                                     SqlConnectionEncryptOption encrypt,
                                     byte[] encryptedPassword,
                                     byte[] encryptedChangePassword,
                                     int encryptedPasswordLengthInBytes,
                                     int encryptedChangePasswordLengthInBytes,
                                     bool useFeatureExt,
                                     string userName,
                                     int length,
                                     int featureExOffset,
                                     string clientInterfaceName,
                                     byte[] outSSPIBuff,
                                     uint outSSPILength,
                                     LoginHandlerContext context,
                                     bool isAsync,
                                     CancellationToken ct)
        {
            try
            {
                TdsStream stream = context.ConnectionContext.TdsStream;
                TdsWriter writer = stream.TdsWriter;
                await writer.WriteIntAsync(length, isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(length, isAsync, ct).ConfigureAwait(false);
                if (recoverySessionData == null)
                {
                    int protocolVersion = encrypt == SqlConnectionEncryptOption.Strict
                        ? (TdsEnums.TDS8_MAJOR << 24) | (TdsEnums.TDS8_INCREMENT << 16) | TdsEnums.TDS8_MINOR 
                        : (TdsEnums.SQL2012_MAJOR << 24) | (TdsEnums.SQL2012_INCREMENT << 16) | TdsEnums.SQL2012_MINOR;
                    await writer.WriteIntAsync(protocolVersion, isAsync, ct).ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteUnsignedIntAsync(recoverySessionData._tdsVersion, isAsync, ct).ConfigureAwait(false);
                }

                await writer.WriteIntAsync(rec.packetSize, isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(TdsEnums.CLIENT_PROG_VER, isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(TdsParserStaticMethods.GetCurrentProcessIdForTdsLoginOnly(), isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false); // connectionID is unused

                // Log7Flags (DWORD)
                 
                int log7Flags = CreateLogin7Flags(context);
                await writer.WriteIntAsync(log7Flags, isAsync, ct).ConfigureAwait(false);
                
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.TdsLogin|ADV> {0}, TDS Login7 flags = {1}:", ObjectID, log7Flags);

                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);  // ClientTimeZone is not used
                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);  // LCID is unused by server

                // Start writing offset and length of variable length portions
                int offset = TdsEnums.SQL2005_LOG_REC_FIXED_LEN;

                // write offset/length pairs

                // note that you must always set ibHostName since it indicates the beginning of the variable length section of the login record
                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // host name offset
                await writer.WriteShortAsync(rec.hostName.Length, isAsync, ct).ConfigureAwait(false);
                offset += rec.hostName.Length * 2;

                // Only send user/password over if not fSSPI...  If both user/password and SSPI are in login
                // rec, only SSPI is used.  Confirmed same behavior as in luxor.
                if (!rec.useSSPI && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
                {
                    await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);  // userName offset
                    await writer.WriteShortAsync(userName.Length, isAsync, ct).ConfigureAwait(false);
                    offset += userName.Length * 2;

                    // the encrypted password is a byte array - so length computations different than strings
                    await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // password offset
                    await writer.WriteShortAsync(encryptedPasswordLengthInBytes / 2, isAsync, ct).ConfigureAwait(false);
                    offset += encryptedPasswordLengthInBytes;
                }
                else
                {
                    // case where user/password data is not used, send over zeros
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);  // userName offset
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);  // password offset
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // app name offset
                await writer.WriteShortAsync(rec.applicationName.Length, isAsync, ct).ConfigureAwait(false);
                offset += rec.applicationName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // server name offset
                await writer.WriteShortAsync(rec.serverName.Length, isAsync, ct).ConfigureAwait(false);
                offset += rec.serverName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);
                if (useFeatureExt)
                {
                    await writer.WriteShortAsync(4, isAsync, ct).ConfigureAwait(false); // length of ibFeatgureExtLong (which is a DWORD)
                    offset += 4;
                }
                else
                {
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false); // unused (was remote password ?)
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // client interface name offset
                await writer.WriteShortAsync(clientInterfaceName.Length, isAsync, ct).ConfigureAwait(false);
                offset += clientInterfaceName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // language name offset
                await writer.WriteShortAsync(rec.language.Length, isAsync, ct).ConfigureAwait(false);
                offset += rec.language.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // database name offset
                await writer.WriteShortAsync(rec.database.Length, isAsync, ct).ConfigureAwait(false);
                offset += rec.database.Length * 2;

                if (null == s_nicAddress)
                    s_nicAddress = TdsParserStaticMethods.GetNetworkPhysicalAddressForTdsLoginOnly();

                if (isAsync)
                {
                    await stream.WriteAsync(s_nicAddress.AsMemory(), ct).ConfigureAwait(false);
                }
                else
                {
                    stream.Write(s_nicAddress.AsSpan());
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // ibSSPI offset
                if (rec.useSSPI)
                {
                    await writer.WriteShortAsync((int)outSSPILength, isAsync, ct).ConfigureAwait(false);
                    offset += (int)outSSPILength;
                }
                else
                {
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // DB filename offset
                await writer.WriteShortAsync(rec.attachDBFilename.Length, isAsync, ct).ConfigureAwait(false);
                offset += rec.attachDBFilename.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // reset password offset
                await writer.WriteShortAsync(encryptedChangePasswordLengthInBytes / 2, isAsync, ct).ConfigureAwait(false);

                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);        // reserved for chSSPI

                // write variable length portion
                await stream.WriteStringAsync(rec.hostName, isAsync, ct).ConfigureAwait(false);

                // if we are using SSPI, do not send over username/password, since we will use SSPI instead
                // same behavior as Luxor
                if (!rec.useSSPI && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
                {
                    await stream.WriteStringAsync(userName, isAsync, ct).ConfigureAwait(false);

                    if (rec.credential != null)
                    {
                        // TODO: Implement secure string save.
                        throw new NotImplementedException();
                        // _physicalStateObj.WriteSecureString(rec.credential.Password);
                    }
                    else
                    {
                        if (isAsync)
                        { 
                            await stream.WriteAsync(encryptedPassword.AsMemory(0, encryptedChangePasswordLengthInBytes), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            stream.Write(encryptedPassword.AsSpan(0, encryptedPasswordLengthInBytes));
                        }
                    }
                }

                await stream.WriteStringAsync(rec.applicationName, isAsync, ct).ConfigureAwait(false);

                await stream.WriteStringAsync(rec.serverName, isAsync, ct).ConfigureAwait(false);

                // write ibFeatureExtLong
                if (useFeatureExt)
                {
                    if ((requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.TdsLogin|SEC> Sending federated authentication feature request");
                    }

                    await writer.WriteIntAsync(featureExOffset, isAsync, ct).ConfigureAwait(false);
                }

                await stream.WriteStringAsync(clientInterfaceName, isAsync, ct).ConfigureAwait(false);
                await stream.WriteStringAsync(rec.language, isAsync, ct).ConfigureAwait(false);
                await stream.WriteStringAsync(rec.database, isAsync, ct).ConfigureAwait(false);

                // send over SSPI data if we are using SSPI
                if (rec.useSSPI)
                { 
                    if (isAsync)
                    {
                        await stream.WriteAsync(outSSPIBuff.AsMemory(0, (int)outSSPILength), ct).ConfigureAwait(false);
                    }
                    else
                    {
                        stream.Write(outSSPIBuff.AsSpan(0, (int)outSSPILength));
                    }
                }
                await stream.WriteStringAsync(rec.attachDBFilename, isAsync, ct).ConfigureAwait(false);
                if (!rec.useSSPI && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
                {
                    if (rec.newSecurePassword != null)
                    {
                        _physicalStateObj.WriteSecureString(rec.newSecurePassword);
                    }
                    else
                    {
                        _physicalStateObj.WriteByteArray(encryptedChangePassword, encryptedChangePasswordLengthInBytes, 0);
                    }
                }

                ApplyFeatureExData(requestedFeatures, recoverySessionData, fedAuthFeatureExtensionData, useFeatureExt, length, true);
            }
            catch (Exception e)
            {
                if (ADP.IsCatchableExceptionType(e))
                {
                    // be sure to wipe out our buffer if we started sending stuff
                    _physicalStateObj.ResetPacketCounters();
                    _physicalStateObj.ResetBuffer();
                }

                throw;
            }
        }

        private static int CreateLogin7Flags(LoginHandlerContext context)
        {
            SqlLogin rec = context.Login;
            bool useFeatureExt = context.UseFeatureExt;

            int log7Flags = 0;
            /*
             Current snapshot from TDS spec with the offsets added:
                0) fByteOrder:1,                // byte order of numeric data types on client
                1) fCharSet:1,                  // character set on client
                2) fFloat:2,                    // Type of floating point on client
                4) fDumpLoad:1,                 // Dump/Load and BCP enable
                5) fUseDb:1,                    // USE notification
                6) fDatabase:1,                 // Initial database fatal flag
                7) fSetLang:1,                  // SET LANGUAGE notification
                8) fLanguage:1,                 // Initial language fatal flag
                9) fODBC:1,                     // Set if client is ODBC driver
               10) fTranBoundary:1,             // Transaction boundary notification
               11) fDelegatedSec:1,             // Security with delegation is available
               12) fUserType:3,                 // Type of user
               15) fIntegratedSecurity:1,       // Set if client is using integrated security
               16) fSQLType:4,                  // Type of SQL sent from client
               20) fOLEDB:1,                    // Set if client is OLEDB driver
               21) fSpare1:3,                   // first bit used for read-only intent, rest unused
               24) fResetPassword:1,            // set if client wants to reset password
               25) fNoNBCAndSparse:1,           // set if client does not support NBC and Sparse column
               26) fUserInstance:1,             // This connection wants to connect to a SQL "user instance"
               27) fUnknownCollationHandling:1, // This connection can handle unknown collation correctly.
               28) fExtension:1                 // Extensions are used
               32 - total
            */

            // first byte
            log7Flags |= TdsEnums.USE_DB_ON << 5;
            log7Flags |= TdsEnums.INIT_DB_FATAL << 6;
            log7Flags |= TdsEnums.SET_LANG_ON << 7;

            // second byte
            log7Flags |= TdsEnums.INIT_LANG_FATAL << 8;
            log7Flags |= TdsEnums.ODBC_ON << 9;
            if (rec.useReplication)
            {
                log7Flags |= TdsEnums.REPL_ON << 12;
            }
            if (rec.useSSPI)
            {
                log7Flags |= TdsEnums.SSPI_ON << 15;
            }

            // third byte
            if (rec.readOnlyIntent)
            {
                log7Flags |= TdsEnums.READONLY_INTENT_ON << 21; // read-only intent flag is a first bit of fSpare1
            }

            // 4th one
            if (!string.IsNullOrEmpty(rec.newPassword) || (rec.newSecurePassword != null && rec.newSecurePassword.Length != 0))
            {
                log7Flags |= 1 << 24;
            }
            if (rec.userInstance)
            {
                log7Flags |= 1 << 26;
            }
            if (useFeatureExt)
            {
                log7Flags |= 1 << 28;
            }

            return log7Flags;
        }
    }

    internal class LoginHandlerContext : HandlerRequest
    {

        public LoginHandlerContext(ConnectionHandlerContext context)
        {
            this.ConnectionContext = context;
            this.ServerInfo = context.ServerInfo;
            this.ConnectionOptions = context.ConnectionString;
        }

        public ConnectionHandlerContext ConnectionContext { get; }
        public ServerInfo ServerInfo { get; }
        public SqlConnectionString ConnectionOptions { get; }

        /// <summary>
        /// Features in the login request.
        /// </summary>
        public FeatureExtensions Features { get; internal set; } = new();

        /// <summary>
        /// The login record.
        /// </summary>
        public SqlLogin Login { get; internal set; }

        /// <summary>
        /// If feature extensions being used.
        /// </summary>
        public bool UseFeatureExt => Features.RequestedFeatures != TdsEnums.FeatureExtension.None;
    }
}
