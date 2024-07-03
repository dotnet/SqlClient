// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
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
        private readonly GlobalTransactionsFeature _globalTransactionsFeature;
        private readonly DataClassificationFeature _dataClassificationFeature;
        private readonly Utf8Feature _utf8SupportFeature;
        private readonly SqlDnsCachingFeature _sqlDnsCachingFeature;
        private readonly SessionRecoveryFeature _sessionRecoveryFeature;
        private readonly FedAuthFeature _fedAuthFeature;
        private readonly TceFeature _tceFeature;

        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <summary>
        /// Parameterless constructor for the login handler.
        /// </summary>
        public LoginHandler()
        {
            // Setup the feature extensions.
            _sessionRecoveryFeature = new SessionRecoveryFeature();
            _fedAuthFeature = new FedAuthFeature();
            _tceFeature = new TceFeature();
            _globalTransactionsFeature = new GlobalTransactionsFeature();
            _dataClassificationFeature = new DataClassificationFeature();
            _utf8SupportFeature = new Utf8Feature();
            _sqlDnsCachingFeature = new SqlDnsCachingFeature();
        }

        public async ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            LoginHandlerContext loginHandlerContext = new LoginHandlerContext(context);
            await SendLogin(loginHandlerContext, isAsync, ct).ConfigureAwait(false);

            bool enlistInDistributedTransaction = !context.ConnectionString.Pooling; 
            //await CompleteLogin(loginHandlerContext, enlistInDistributedTransaction, isAsync, ct).ConfigureAwait(false);
        }

        //private async ValueTask CompleteLogin(LoginHandlerContext context, bool enlistInDistributedTransaction, bool isAsync, CancellationToken ct)
        //{
        //    bool enlist = context.ConnectionContext.ConnectionString.Enlist;

        //    await ProcessLoginFromStream(context, isAsync, ct).ConfigureAwait(false);

        //    _parser.Run(RunBehavior.UntilDone, null, null, null, _parser._physicalStateObj);

        //    if (RoutingInfo == null)
        //    {
        //        // ROR should not affect state of connection recovery
        //        if (context.Features.FederatedAuthenticationRequested && !context.Features.FederatedAuthenticationAcknowledged)
        //        {
        //            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.CompleteLogin|ERR> {0}, Server did not acknowledge the federated authentication request", ObjectID);
        //            throw SQL.ParsingError(ParsingErrorState.FedAuthNotAcknowledged);
        //        }
        //        if (context.Features.FederatedAuthenticationInfoRequested && !context.Features.FederatedAuthenticationInfoReceived)
        //        {
        //            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.CompleteLogin|ERR> {0}, Server never sent the requested federated authentication info", ObjectID);
        //            throw SQL.ParsingError(ParsingErrorState.FedAuthInfoNotReceived);
        //        }
        //        if (!_sessionRecoveryAcknowledged)
        //        {
        //            _currentSessionData = null;
        //            if (_recoverySessionData != null)
        //            {
        //                throw SQL.CR_NoCRAckAtReconnection(this);
        //            }
        //        }
        //        if (_currentSessionData != null && _recoverySessionData == null)
        //        {
        //            _currentSessionData._initialDatabase = CurrentDatabase;
        //            _currentSessionData._initialCollation = _currentSessionData._collation;
        //            _currentSessionData._initialLanguage = _currentLanguage;
        //        }
        //        bool isEncrypted = _parser.EncryptionOptions == EncryptionOptions.ON;
        //        if (_recoverySessionData != null)
        //        {
        //            if (_recoverySessionData._encrypted != isEncrypted)
        //            {
        //                throw SQL.CR_EncryptionChanged(this);
        //            }
        //        }
        //        if (_currentSessionData != null)
        //        {
        //            _currentSessionData._encrypted = isEncrypted;
        //        }
        //        _recoverySessionData = null;
        //    }

        //    Debug.Assert(SniContext.Snix_Login == Parser._physicalStateObj.SniContext, $"SniContext should be Snix_Login; actual Value: {Parser._physicalStateObj.SniContext}");
        //    _parser._physicalStateObj.SniContext = SniContext.Snix_EnableMars;
        //    _parser.EnableMars();

        //    _fConnectionOpen = true; // mark connection as open
        //    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.CompleteLogin|ADV> Post-Login Phase: Server connection obtained.");

        //    // for non-pooled connections, enlist in a distributed transaction
        //    // if present - and user specified to enlist
        //    if (enlistInDistributedTransaction && enlist && RoutingInfo == null)
        //    {
        //        _parser._physicalStateObj.SniContext = SniContext.Snix_AutoEnlist;
        //        Transaction tx = ADP.GetCurrentTransaction();
        //        Enlist(tx);
        //    }

        //    _parser._physicalStateObj.SniContext = SniContext.Snix_Login;
        //}

        //private async ValueTask ProcessLoginFromStream(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        //{
        //    TdsStream tdsStream = context.ConnectionContext.TdsStream;

        //    // Create a chain of handlers for login.
        //}

        private async ValueTask SendLogin(LoginHandlerContext context, bool isAsync, CancellationToken ct)
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
                                     && !context.ConnectionContext.FedAuthNegotiatedInPrelogin);
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
                        fedAuthRequiredPreLoginResponse = context.ConnectionContext.FedAuthNegotiatedInPrelogin
                    };
            }

            if (context.ConnectionContext.AccessTokenInBytes != null)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                features.FedAuthFeatureExtensionData = new FederatedAuthenticationFeatureExtensionData
                {
                    libraryType = TdsEnums.FedAuthLibrary.SecurityToken,
                    fedAuthRequiredPreLoginResponse = context.ConnectionContext.FedAuthNegotiatedInPrelogin,
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
                bool IsEntraIdAuthInConnectionString = context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
                                                || context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
                                                // Since AD Integrated may be acting like Windows integrated, additionally check _fedAuthRequired
                                                || (context.ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated);

                return IsEntraIdAuthInConnectionString && context.ConnectionContext.FedAuthNegotiatedInPrelogin
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
                if (context.UseFeatureExt)
                {
                    length += 4;
                }
            }

            string userName = rec.credential != null ? rec.credential.UserId : rec.userName;
            byte[] encryptedPassword = rec.credential != null ? null : TdsParserStaticMethods.ObfuscatePassword(rec.password);
            int encryptedPasswordLengthInBytes = encryptedPassword != null ? encryptedPassword.Length : 0;
            
            PasswordChangeRequest passwordChangeRequest = context.ConnectionContext.PasswordChangeRequest;
            byte[] encryptedChangePassword = passwordChangeRequest?.NewSecurePassword != null ? null: TdsParserStaticMethods.ObfuscatePassword(passwordChangeRequest.NewPassword);
            int encryptedChangePasswordLengthInBytes = passwordChangeRequest?.NewSecurePassword != null ? passwordChangeRequest.NewSecurePassword.Length : encryptedChangePassword.Length;

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
                    throw new NotImplementedException("SSPI is not implemented");
                    // now allocate proper length of buffer, and set length
                    //outSSPILength = _authenticationProvider.MaxSSPILength;
                    //rentedSSPIBuff = ArrayPool<byte>.Shared.Rent((int)outSSPILength);
                    //outSSPIBuff = rentedSSPIBuff;

                    //// Call helper function for SSPI data and actual length.
                    //// Since we don't have SSPI data from the server, send null for the
                    //// byte[] buffer and 0 for the int length.
                    //Debug.Assert(SniContext.Snix_Login == _physicalStateObj.SniContext, $"Unexpected SniContext. Expecting Snix_Login, actual value is '{_physicalStateObj.SniContext}'");
                    //_physicalStateObj.SniContext = SniContext.Snix_LoginSspi;
                    //_authenticationProvider.SSPIData(ReadOnlyMemory<byte>.Empty, ref outSSPIBuff, ref outSSPILength, _sniSpnBuffer);

                    //if (outSSPILength > int.MaxValue)
                    //{
                    //    throw SQL.InvalidSSPIPacketSize();  // SqlBu 332503
                    //}
                    //_physicalStateObj.SniContext = SniContext.Snix_Login;

                    //checked
                    //{
                    //    length += (int)outSSPILength;
                    //}
                }
            }


            int feOffset = length;
            // calculate and reserve the required bytes for the featureEx
            length = CalculateFeatureExtensionLength(context);

            length += feOffset;


            // TODO : Plumb Session recovery.
            SessionData recoverySessionData = null;

            await WriteLoginData(context,
                           recoverySessionData,
                           encryptedPassword,
                           encryptedChangePassword,
                           encryptedPasswordLengthInBytes,
                           encryptedChangePasswordLengthInBytes,
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

            //TODO: _physicalStateObj.ResetSecurePasswordsInformation();     // Password information is needed only from Login process; done with writing login packet and should clear information
            //TODO:  _physicalStateObj.HasPendingData = true;
            //TODO: _physicalStateObj._messageStatus = 0;
        }


        /// <summary>
        /// Calculates the length in bytes to send all the feature extensions.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private int CalculateFeatureExtensionLength(LoginHandlerContext context)
        {
            int feLength = 0;
            TdsEnums.FeatureExtension requestedFeatures = context.Features.RequestedFeatures;
            if (context.UseFeatureExt)
            {
                if (_sessionRecoveryFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _sessionRecoveryFeature.GetLengthInBytes(context);
                }
                if (_fedAuthFeature.ShouldUseFeature(requestedFeatures))
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.TdsLogin|SEC> Calculating Length for federated authentication feature request");
                    feLength += _fedAuthFeature.GetLengthInBytes(context);
                }
                if (_tceFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _tceFeature.GetLengthInBytes(context);
                }
                if (_globalTransactionsFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _globalTransactionsFeature.GetLengthInBytes(context);
                }
                if (_dataClassificationFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _dataClassificationFeature.GetLengthInBytes(context);
                }
                if (_utf8SupportFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _utf8SupportFeature.GetLengthInBytes(context);
                }
                if (_sqlDnsCachingFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _sqlDnsCachingFeature.GetLengthInBytes(context);
                }

                // terminator
                feLength++;
            }
            return feLength;
        }


        private async ValueTask WriteLoginData(LoginHandlerContext context, 
                                     SessionData recoverySessionData,
                                     byte[] encryptedPassword,
                                     byte[] encryptedChangePassword,
                                     int encryptedPasswordLengthInBytes,
                                     int encryptedChangePasswordLengthInBytes,
                                     string userName,
                                     int length,
                                     int featureExOffset,
                                     string clientInterfaceName,
                                     byte[] outSSPIBuff,
                                     uint outSSPILength,
                                     bool isAsync,
                                     CancellationToken ct)
        {
            try
            {
                SqlLogin rec = context.Login;
                TdsEnums.FeatureExtension requestedFeatures = context.Features.RequestedFeatures;
                TdsStream stream = context.ConnectionContext.TdsStream;
                SqlConnectionEncryptOption encrypt = context.ConnectionOptions.Encrypt;
                FederatedAuthenticationFeatureExtensionData fedAuthFeatureExtensionData = context.Features.FedAuthFeatureExtensionData;
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
                
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.LoginHandler.WriteLoginData|ADV> TDS Login7 flags = {0}:", log7Flags);

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
                if (context.UseFeatureExt)
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
                if (context.UseFeatureExt)
                {
                    if ((requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.LoginHandler.WriteLoginData|SEC> Sending federated authentication feature request");
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
                        // TODO : implement saving secure string.
                        throw new NotImplementedException();
                        // _physicalStateObj.WriteSecureString(rec.newSecurePassword);
                    }
                    else
                    {
                        if (isAsync)
                        {
                            await stream.WriteAsync(encryptedChangePassword.AsMemory(0, encryptedChangePasswordLengthInBytes), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            stream.Write(encryptedChangePassword.AsSpan(0, encryptedChangePasswordLengthInBytes));
                        }
                    }
                }

                await SendFeatureExtensionData(context, 
                    requestedFeatures, 
                    recoverySessionData, 
                    fedAuthFeatureExtensionData, 
                    isAsync, 
                    ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (ADP.IsCatchableExceptionType(e))
                {
                    // Reset the buffer if there was an exception.
                    context.ConnectionContext.TdsStream.Reset();
                }

                throw;
            }
        }

        private async ValueTask SendFeatureExtensionData(LoginHandlerContext context,
                                        TdsEnums.FeatureExtension requestedFeatures,
                                        SessionData recoverySessionData,
                                        FederatedAuthenticationFeatureExtensionData fedAuthFeatureExtensionData,
                                        bool isAsync,
                                        CancellationToken ct)
        {
            if (context.UseFeatureExt)
            {
                if (_sessionRecoveryFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _sessionRecoveryFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_fedAuthFeature.ShouldUseFeature(requestedFeatures))
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.TdsLogin|SEC> Sending federated authentication feature request");
                    Debug.Assert(fedAuthFeatureExtensionData != null, "fedAuthFeatureExtensionData should not null.");
                    await _fedAuthFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_tceFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _tceFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_globalTransactionsFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _globalTransactionsFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_dataClassificationFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _dataClassificationFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_utf8SupportFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _utf8SupportFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }

                if (_sqlDnsCachingFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _sqlDnsCachingFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }

                // terminator
                await context.ConnectionContext.TdsStream.WriteByteAsync(0xFF, isAsync, ct).ConfigureAwait(false);

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
