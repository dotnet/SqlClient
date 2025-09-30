// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/SqlAuthenticationToken/*'/>
public abstract class SqlAuthenticationTokenBase
{
    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ExpiresOn/*'/>
    public DateTimeOffset ExpiresOn { get; }

    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/AccessToken/*'/>
    public string AccessToken { get; }

    protected SqlAuthenticationTokenBase(
        string accessToken,
        DateTimeOffset expiresOn)
    {
        AccessToken = accessToken;
        ExpiresOn = expiresOn;
    }
}
