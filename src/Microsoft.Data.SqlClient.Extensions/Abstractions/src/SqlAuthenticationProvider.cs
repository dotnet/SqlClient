// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
public abstract partial class SqlAuthenticationProvider
{
    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeLoad/*'/>
    public virtual void BeforeLoad(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeUnload/*'/>
    public virtual void BeforeUnload(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/IsSupported/*'/>
    public abstract bool IsSupported(SqlAuthenticationMethod authenticationMethod);

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/AcquireTokenAsync/*'/>
    public abstract Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters);


    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/GetProvider/*'/>
    //
    // We would like to deprecate this method in favour of
    // SqlAuthenticationProviderManager.GetProvider().
    //
    public static SqlAuthenticationProvider? GetProvider(
        SqlAuthenticationMethod authenticationMethod)
    {
        return Internal.GetProvider(authenticationMethod);
    }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SetProvider/*'/>
    //
    // We would like to deprecate this method in favour of
    // SqlAuthenticationProviderManager.SetProvider().
    //
    public static bool SetProvider(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider)
    {
        return Internal.SetProvider(authenticationMethod, provider);
    }
}
