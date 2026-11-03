// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Contains the server-supplied authority and resource from the FEDAUTHINFO token.
/// </summary>
/// <remarks>
/// SQL Server supplies STSURL and SPN according to its authentication configuration. The driver
/// passes them to the authentication provider or access-token callback as Authority and Resource,
/// respectively, without imposing a fixed authority or resource allow-list. This supports
/// customer-configured authentication systems. Protocol checks and the selected authentication
/// provider's requirements still apply.
/// </remarks>
internal sealed class SqlFedAuthInfo
{
    internal SqlFedAuthInfo(string spn, string stsurl)
    {
        Spn = spn;
        StsUrl = stsurl;
    }

    /// <summary>
    /// Gets the target resource identifier used for token acquisition, not a Kerberos server SPN.
    /// </summary>
    internal string Spn { get; }

    /// <summary>
    /// Gets the security token service URL used as the authentication authority.
    /// </summary>
    internal string StsUrl { get; }

    public override string ToString()
    {
        return $"SPN: {Spn}, STSURL: {StsUrl}";
    }
}
