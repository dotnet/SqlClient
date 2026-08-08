// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Tests for splitting the STSURL supplied by the server into an authority host and a tenant.
/// </summary>
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
        // ADAL v1 style authority returned by the Dataverse / Dynamics 365 TDS endpoint.
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
        // Sovereign cloud authority.
        {
            $"https://login.microsoftonline.us/{Tenant}/oauth2/authorize",
            "https://login.microsoftonline.us/",
            Tenant,
            $"https://login.microsoftonline.us/{Tenant}"
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
        // Non-default port is preserved in the authority host.
        {
            $"https://sts.contoso.com:8443/{Tenant}/oauth2/authorize",
            "https://sts.contoso.com:8443/",
            Tenant,
            $"https://sts.contoso.com:8443/{Tenant}"
        },
    };

    [Theory]
    [MemberData(nameof(AuthorityData))]
    public void ParseAuthority_SplitsHostAndTenant(
        string authorityUrl,
        string expectedHost,
        string expectedTenant,
        string expectedMsalAuthority)
    {
        ActiveDirectoryAuthenticationProvider.ParseAuthority(
            authorityUrl,
            out string host,
            out string tenant,
            out string msalAuthority);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedTenant, tenant);
        Assert.Equal(expectedMsalAuthority, msalAuthority);
    }

    [Theory]
    // No tenant segment at all.
    [InlineData("https://login.microsoftonline.com/", "https://login.microsoftonline.com/", "")]
    // Not an absolute HTTP(S) URL - legacy split behavior is retained.
    [InlineData("login.microsoftonline.com/tenant", "login.microsoftonline.com/", "tenant")]
    public void ParseAuthority_FallsBackToLegacySplit(
        string authorityUrl,
        string expectedHost,
        string expectedTenant)
    {
        ActiveDirectoryAuthenticationProvider.ParseAuthority(
            authorityUrl,
            out string host,
            out string tenant,
            out string msalAuthority);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedTenant, tenant);
        Assert.Equal(authorityUrl, msalAuthority);
    }
}
