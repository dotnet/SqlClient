// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers;

namespace Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.Handlers.Connection
{
    internal class LoginHandler : IHandler<ConnectionHandlerContext>
    {
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        public async ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            ValidateIncomingContext(context);
            

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

        private void PrepareLoginDetails()
        {
            SqlLogin login = new SqlLogin();

            // gather all the settings the user set in the connection string or
            // properties and do the login
            CurrentDatabase = server.ResolvedDatabaseName;
            _currentPacketSize = ConnectionOptions.PacketSize;
            _currentLanguage = ConnectionOptions.CurrentLanguage;

            int timeoutInSeconds = 0;

            // If a timeout tick value is specified, compute the timeout based
            // upon the amount of time left in seconds.
            if (!timeout.IsInfinite)
            {
                long t = timeout.MillisecondsRemaining / 1000;
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

            login.authentication = ConnectionOptions.Authentication;
            login.timeout = timeoutInSeconds;
            login.userInstance = ConnectionOptions.UserInstance;
            login.hostName = ConnectionOptions.ObtainWorkstationId();
            login.userName = ConnectionOptions.UserID;
            login.password = ConnectionOptions.Password;
            login.applicationName = ConnectionOptions.ApplicationName;

            login.language = _currentLanguage;
            if (!login.userInstance)
            {
                // Do not send attachdbfilename or database to SSE primary instance
                login.database = CurrentDatabase;
                login.attachDBFilename = ConnectionOptions.AttachDBFilename;
            }

            // VSTS#795621 - Ensure ServerName is Sent During TdsLogin To Enable Sql Azure Connectivity.
            // Using server.UserServerName (versus ConnectionOptions.DataSource) since TdsLogin requires
            // serverName to always be non-null.
            login.serverName = server.UserServerName;

            login.useReplication = ConnectionOptions.Replication;
            login.useSSPI = ConnectionOptions.IntegratedSecurity  // Treat AD Integrated like Windows integrated when against a non-FedAuth endpoint
                                     || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && !_fedAuthRequired);
            login.packetSize = _currentPacketSize;
            login.newPassword = newPassword;
            login.readOnlyIntent = ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly;
            login.credential = _credential;
            if (newSecurePassword != null)
            {
                login.newSecurePassword = newSecurePassword;
            }

            TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            if (ConnectionOptions.ConnectRetryCount > 0)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.SessionRecovery;
                _sessionRecoveryRequested = true;
            }

            // If the workflow being used is Active Directory Authentication and server's prelogin response
            // for FEDAUTHREQUIRED option indicates Federated Authentication is required, we have to insert FedAuth Feature Extension
            // in Login7, indicating the intent to use Active Directory Authentication for SQL Server.
            if (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
                // Since AD Integrated may be acting like Windows integrated, additionally check _fedAuthRequired
                || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && _fedAuthRequired)
                || _accessTokenCallback != null)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                _federatedAuthenticationInfoRequested = true;
                _fedAuthFeatureExtensionData =
                    new FederatedAuthenticationFeatureExtensionData
                    {
                        libraryType = TdsEnums.FedAuthLibrary.MSAL,
                        authentication = ConnectionOptions.Authentication,
                        fedAuthRequiredPreLoginResponse = _fedAuthRequired
                    };
            }

            if (_accessTokenInBytes != null)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                _fedAuthFeatureExtensionData = new FederatedAuthenticationFeatureExtensionData
                {
                    libraryType = TdsEnums.FedAuthLibrary.SecurityToken,
                    fedAuthRequiredPreLoginResponse = _fedAuthRequired,
                    accessToken = _accessTokenInBytes
                };
                // No need any further info from the server for token based authentication. So set _federatedAuthenticationRequested to true
                _federatedAuthenticationRequested = true;
            }

            // The GLOBALTRANSACTIONS, DATACLASSIFICATION, TCE, and UTF8 support features are implicitly requested
            requestedFeatures |= TdsEnums.FeatureExtension.GlobalTransactions | TdsEnums.FeatureExtension.DataClassification | TdsEnums.FeatureExtension.Tce | TdsEnums.FeatureExtension.UTF8Support;

            // The SQLDNSCaching feature is implicitly set
            requestedFeatures |= TdsEnums.FeatureExtension.SQLDNSCaching;

            _parser.TdsLogin(login, requestedFeatures, _recoverySessionData, _fedAuthFeatureExtensionData, encrypt);
        }
    }

    internal class LoginHandlerContext : HandlerRequest
    {

        public LoginHandlerContext(ConnectionHandlerContext context)
        {

        }
    }
}
