// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationTokenBase.xml' path='docs/members[@name="SqlAuthenticationTokenBase"]/SqlAuthenticationTokenBase/*'/>
public class SqlAuthenticationTokenBase
{
    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationTokenBase"]/ctor/*'/>
    public SqlAuthenticationTokenBase(string accessToken, DateTimeOffset expiresOn)
    {
        AccessToken = accessToken;
        ExpiresOn = expiresOn;
    }

    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationTokenBase"]/AccessToken/*'/>
    public string AccessToken { get; }

    /// <include file='../doc/SqlAuthenticationTokenBase.xml' path='docs/members[@name="SqlAuthenticationTokenBase"]/ExpiresOn/*'/>
    public DateTimeOffset ExpiresOn { get; }
}
