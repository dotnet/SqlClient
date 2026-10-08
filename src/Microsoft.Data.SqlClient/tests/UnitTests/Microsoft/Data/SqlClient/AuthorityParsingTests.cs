// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    /// <summary>
    /// Tests for splitting the STSURL supplied by the server in the FEDAUTHINFO TDS token into an
    /// authority host and a tenant.
    /// </summary>
    /// <remarks>
    /// The cases below only cover authority shapes that Entra ID actually documents:
    /// https://learn.microsoft.com/entra/identity-platform/authentication-national-cloud
    /// </remarks>
    [Collection("SqlAuthenticationProvider")]
    public class AuthorityParsingTests
    {
        private const string Tenant = "72f988bf-86f1-41af-91ab-2d7cd011db47";

        public static TheoryData<string, string, string, string> AuthorityData => new()
        {
            // Azure SQL / Fabric style authority.
            {
                $"https://login.microsoftonline.com/{Tenant}",
                "https://login.microsoftonline.com/",
                Tenant,
                $"https://login.microsoftonline.com/{Tenant}"
            },
            // Trailing slash.
            {
                $"https://login.microsoftonline.com/{Tenant}/",
                "https://login.microsoftonline.com/",
                Tenant,
                $"https://login.microsoftonline.com/{Tenant}"
            },
            // v1.0 authorize endpoint, as returned by the Dataverse / Dynamics 365 TDS endpoint.
            {
                $"https://login.microsoftonline.com/{Tenant}/oauth2/authorize",
                "https://login.microsoftonline.com/",
                Tenant,
                $"https://login.microsoftonline.com/{Tenant}"
            },
            // v2.0 token endpoint.
            {
                $"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/token",
                "https://login.microsoftonline.com/",
                Tenant,
                $"https://login.microsoftonline.com/{Tenant}"
            },
            // US Government cloud.
            {
                $"https://login.microsoftonline.us/{Tenant}/oauth2/authorize",
                "https://login.microsoftonline.us/",
                Tenant,
                $"https://login.microsoftonline.us/{Tenant}"
            },
            // Microsoft Azure operated by 21Vianet.
            {
                $"https://login.partner.microsoftonline.cn/{Tenant}",
                "https://login.partner.microsoftonline.cn/",
                Tenant,
                $"https://login.partner.microsoftonline.cn/{Tenant}"
            },
            // Domain-name tenant.
            {
                "https://login.microsoftonline.com/contoso.onmicrosoft.com",
                "https://login.microsoftonline.com/",
                "contoso.onmicrosoft.com",
                "https://login.microsoftonline.com/contoso.onmicrosoft.com"
            },
            // Placeholder tenant.
            {
                "https://login.microsoftonline.com/common/oauth2/authorize",
                "https://login.microsoftonline.com/",
                "common",
                "https://login.microsoftonline.com/common"
            },
            {
                "https://login.microsoftonline.com/organizations",
                "https://login.microsoftonline.com/",
                "organizations",
                "https://login.microsoftonline.com/organizations"
            },
            {
                "https://login.microsoftonline.com/consumers",
                "https://login.microsoftonline.com/",
                "consumers",
                "https://login.microsoftonline.com/consumers"
            },
        };

        /// <summary>
        /// Supported authority URLs yield the authority host, first-segment tenant, and normalized
        /// MSAL authority without trailing endpoint paths.
        /// </summary>
        [Theory]
        [MemberData(nameof(AuthorityData))]
        public void TryParseAuthority_SplitsHostAndTenant(
            string authorityUrl,
            string expectedHost,
            string expectedTenant,
            string expectedMsalAuthority)
        {
            Assert.True(ActiveDirectoryAuthenticationProvider.TryParseAuthority(
                authorityUrl,
                out string host,
                out string tenant,
                out string msalAuthority));

            Assert.Equal(expectedHost, host);
            Assert.Equal(expectedTenant, tenant);
            Assert.Equal(expectedMsalAuthority, msalAuthority);
        }

        /// <summary>
        /// Invalid authority URLs are rejected and all parsed outputs are empty.
        /// </summary>
        [Theory]
        // A tenant is required; an authority without one cannot yield a usable credential.
        [InlineData("https://login.microsoftonline.com")]
        [InlineData("https://login.microsoftonline.com/")]
        // An empty leading path segment leaves no tenant to authenticate against.
        [InlineData("https://login.microsoftonline.com//oauth2/authorize")]
        // Entra ID authorities are always HTTPS; other schemes are rejected.
        [InlineData("http://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47")]
        [InlineData("http://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/authorize")]
        // The server may omit the STSURL entirely.
        [InlineData("")]
        public void TryParseAuthority_RejectsInvalidAuthority(string authorityUrl)
        {
            Assert.False(ActiveDirectoryAuthenticationProvider.TryParseAuthority(
                authorityUrl,
                out string host,
                out string tenant,
                out string msalAuthority));

            Assert.Equal(string.Empty, host);
            Assert.Equal(string.Empty, tenant);
            Assert.Equal(string.Empty, msalAuthority);
        }

        /// <summary>
        /// A malformed authority must surface as an argument error identifying the bad authority,
        /// before any credential is created or any network call is attempted.
        /// </summary>
        [Theory]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword)]
        public async Task AcquireTokenAsync_MalformedAuthority_ThrowsArgumentException(
            SqlAuthenticationMethod method)
        {
            const string BadAuthority = "https://login.microsoftonline.com/";

            SqlAuthenticationParameters parameters = new SqlAuthenticationParameters.Builder(
                method,
                "https://database.windows.net/",
                BadAuthority,
                "test-server",
                "test-database")
                .WithUserId("test-user")
                .WithPassword("test-password");

            ActiveDirectoryAuthenticationProvider provider = new();

            ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(
                () => provider.AcquireTokenAsync(parameters));

            Assert.Equal(nameof(SqlAuthenticationParameters.Authority), ex.ParamName);
            Assert.Contains(BadAuthority, ex.Message, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(Strings.SQL_InvalidAuthority));
            Assert.Contains(StringsHelper.GetString(Strings.SQL_InvalidAuthority, BadAuthority), ex.Message, StringComparison.Ordinal);
            Assert.Null(ex.InnerException);
        }
    }
}
