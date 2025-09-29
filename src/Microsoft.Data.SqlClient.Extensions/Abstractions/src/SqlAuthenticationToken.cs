// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/SqlAuthenticationToken/*'/>
public class SqlAuthenticationToken
{
    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ExpiresOn/*'/>
    public DateTimeOffset ExpiresOn { get; }

    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/AccessToken/*'/>
    public string AccessToken { get; }

    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ctor/*'/>
    public SqlAuthenticationToken(string accessToken, DateTimeOffset expiresOn)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            // TODO: Throw an appropriate SqlException.
            //throw SQL.ParameterCannotBeEmpty("AccessToken");
            throw new ArgumentException("AccessToken cannot be null or empty", nameof(accessToken));
        }

        AccessToken = accessToken;
        ExpiresOn = expiresOn;
    }
}
