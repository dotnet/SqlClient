// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// Default auth provider for AD Integrated.
    /// </summary>
    internal class ActiveDirectoryNativeAuthenticationProvider : SqlAuthenticationProvider
    {
        private readonly string _type = typeof(ActiveDirectoryNativeAuthenticationProvider).Name;
        private readonly SqlClientLogger _logger = new SqlClientLogger();

        /// <summary>
        /// Get token.
        /// </summary>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) => Task.Run(() =>
        {
            long expiresOnFileTime = 0;
            byte[] token;
            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
            {
                token = ADALNativeWrapper.ADALGetAccessTokenForWindowsIntegrated(parameters.Authority, parameters.Resource, parameters.ConnectionId, ActiveDirectoryAuthentication.AdoClientId, ref expiresOnFileTime);
                return new SqlAuthenticationToken(token, DateTimeOffset.FromFileTime(expiresOnFileTime));
            }
            else
            {
                token = ADALNativeWrapper.ADALGetAccessToken(parameters.UserId, parameters.Password, parameters.Authority, parameters.Resource, parameters.ConnectionId, ActiveDirectoryAuthentication.AdoClientId, ref expiresOnFileTime);
                return new SqlAuthenticationToken(token, DateTimeOffset.FromFileTime(expiresOnFileTime));
            }
        });

        /// <summary>
        /// Checks support for authentication type in lower case.
        /// </summary>
        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                || authentication == SqlAuthenticationMethod.ActiveDirectoryPassword;
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
