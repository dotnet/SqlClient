// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    ///
    /// </summary>
    public sealed class AzureActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        private static readonly string s_defaultScopeSuffix = "/.default";
        private readonly string _type = typeof(AzureActiveDirectoryAuthenticationProvider).Name;
        private readonly SqlClientLogger _logger = new();
        // private readonly string _applicationClientId = ActiveDirectoryAuthentication.AdoClientId;
        private readonly TokenCredential _credential;

        /// <summary>
        ///
        /// </summary>
        public AzureActiveDirectoryAuthenticationProvider(TokenCredential credential)
        {
            _credential = credential;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public override bool IsSupported(SqlAuthenticationMethod authentication) => authentication switch
        {
            SqlAuthenticationMethod.ActiveDirectoryTokenCredential => true,
            SqlAuthenticationMethod.ActiveDirectoryIntegrated => true,
            SqlAuthenticationMethod.ActiveDirectoryPassword => true,
            SqlAuthenticationMethod.ActiveDirectoryInteractive => true,
            SqlAuthenticationMethod.ActiveDirectoryServicePrincipal => true,
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow => true,
            SqlAuthenticationMethod.ActiveDirectoryManagedIdentity => true,
            SqlAuthenticationMethod.ActiveDirectoryMSI => true,
            SqlAuthenticationMethod.ActiveDirectoryDefault => true,
            _ => false
        };

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        public override void BeforeLoad(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeLoad", $"being loaded into SqlAuthProviders for {authentication}.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        public override void BeforeUnload(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeUnload", $"being unloaded from SqlAuthProviders for {authentication}.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            CancellationTokenSource cts = new();

            // Use Connection timeout value to cancel token acquire request after certain period of time.
            cts.CancelAfter(parameters.ConnectionTimeout * 1000); // Convert to milliseconds

            string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;
            string[] scopes = { scope };
            TokenRequestContext tokenRequestContext = new(scopes);
            string clientId = string.IsNullOrWhiteSpace(parameters.UserId) ? null : parameters.UserId;
            AccessToken accessToken = await _credential.GetTokenAsync(tokenRequestContext, cts.Token);
            return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
        }
    }
}
