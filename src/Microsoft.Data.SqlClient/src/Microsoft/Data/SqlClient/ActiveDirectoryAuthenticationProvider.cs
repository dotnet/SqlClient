// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// Default auth provider for AD Integrated.
    /// </summary>
    internal class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        private static readonly string s_defaultScopeSuffix = "/.default";
        private readonly string _type = typeof(ActiveDirectoryAuthenticationProvider).Name;
        private readonly SqlClientLogger _logger = new SqlClientLogger();

        /// <summary>
        /// Get token.
        /// </summary>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) => Task.Run(async () =>
        {
            AuthenticationResult result;
            string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;
            string[] scopes = new string[] { scope };

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)
            {
                IConfidentialClientApplication ccApp = ConfidentialClientApplicationBuilder.Create(parameters.UserId)
                    .WithAuthority(parameters.Authority)
                    .WithClientSecret(parameters.Password)
                    .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                    .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                    .Build();

                result = ccApp.AcquireTokenForClient(scopes).ExecuteAsync().Result;
                return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
            }

            IPublicClientApplication app = PublicClientApplicationBuilder.Create(ActiveDirectoryAuthentication.AdoClientId)
                .WithAuthority(parameters.Authority)
                .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                .Build();

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
            {
                result = app.AcquireTokenByIntegratedWindowsAuth(scopes)
                    .WithCorrelationId(parameters.ConnectionId)
                    .ExecuteAsync().Result;
            }
            else if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryPassword)
            {
                SecureString password = new SecureString();
                foreach (char c in parameters.Password)
                    password.AppendChar(c);
                password.MakeReadOnly();
                result = app.AcquireTokenByUsernamePassword(scopes, parameters.UserId, password)
                    .WithCorrelationId(parameters.ConnectionId)
                    .ExecuteAsync().Result;
            }
            else
            {
                result = await app.AcquireTokenInteractive(scopes)
                    .WithCorrelationId(parameters.ConnectionId)
                    .WithLoginHint(parameters.UserId)
                    .ExecuteAsync();
            }

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        });

        /// <summary>
        /// Checks support for authentication type in lower case.
        /// Interactive authentication added.
        /// </summary>
        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                || authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
                || authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
        }

        public override void BeforeLoad(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeLoad", $"being loaded into SqlAuthProviders for {authentication}.");
        }

        public override void BeforeUnload(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeUnload", $"being unloaded from SqlAuthProviders for {authentication}.");
        }
    }
}
