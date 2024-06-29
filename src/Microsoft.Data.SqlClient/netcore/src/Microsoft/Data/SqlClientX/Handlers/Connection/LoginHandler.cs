// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.Handlers.Connection.Login;
using Microsoft.Data.SqlClientX.Handlers;

namespace Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.Handlers.Connection
{
    internal class LoginHandler : IHandler<ConnectionHandlerContext>
    {
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        public ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            ValidateIncomingContext(context);

            LoginHandlerContext loginHandlerContext = new LoginHandlerContext(context);
            PrepareLoginDetails(loginHandlerContext);

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

            return ValueTask.CompletedTask;
        }

        private void PrepareLoginDetails(LoginHandlerContext context)
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
            TdsFeatures features = context.Features;
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
            TdsLogin(context);

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

        private void TdsLogin(LoginHandlerContext context)
        {
            // TODO: Set the timeout
            _ = context.Login.timeout;
            throw new NotImplementedException();
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
        public TdsFeatures Features { get; internal set; } = new();
        public SqlLogin Login { get; internal set; }
    }
}
