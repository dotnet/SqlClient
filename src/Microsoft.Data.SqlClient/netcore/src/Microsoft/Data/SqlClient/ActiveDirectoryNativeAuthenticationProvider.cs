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
    internal class ActiveDirectoryNativeAuthenticationProvider : SqlAuthenticationProvider
    {
        private static readonly string s_defaultScopeSuffix = "/.default";
        private readonly string _type = typeof(ActiveDirectoryNativeAuthenticationProvider).Name;

        /// <summary>
        /// Get token.
        /// </summary>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) => Task.Run(() =>
        {
            IPublicClientApplication app = PublicClientApplicationBuilder.Create(ActiveDirectoryAuthentication.AdoClientId)
                .WithAuthority(parameters.Authority)
                .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                .Build();
            AuthenticationResult result;
            string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;
            string[] scopes = new string[] { scope };

            // Note: CorrelationId, which existed in ADAL, can not be set in MSAL (yet?).
            // parameter.ConnectionId was passed as the CorrelationId in ADAL to aid support in troubleshooting.
            // If/When MSAL adds CorrelationId support, it should be passed from parameters here, too.

            SecureString password = new SecureString();
            foreach (char c in parameters.Password)
                password.AppendChar(c);
            password.MakeReadOnly();
            result = app.AcquireTokenByUsernamePassword(scopes, parameters.UserId, password).ExecuteAsync().Result;

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        });

        /// <summary>
        /// Checks support for authentication type in lower case.
        /// Interactive authenticatin added.
        /// </summary>
        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryPassword;
        }
    }
}
