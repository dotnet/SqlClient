// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProviderBase.xml' path='docs/members[@name="SqlAuthenticationProviderBase"]/SqlAuthenticationProviderBase/*'/>
public abstract class SqlAuthenticationProviderBase
{
    /// <include file='../doc/SqlAuthenticationProviderBase.xml' path='docs/members[@name="SqlAuthenticationProviderBase"]/BeforeLoad/*'/>
    public virtual void BeforeLoad(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProviderBase.xml' path='docs/members[@name="SqlAuthenticationProviderBase"]/BeforeUnload/*'/>
    public virtual void BeforeUnload(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProviderBase.xml' path='docs/members[@name="SqlAuthenticationProviderBase"]/IsSupported/*'/>
    public abstract bool IsSupported(SqlAuthenticationMethod authenticationMethod);

    /// <include file='../doc/SqlAuthenticationProviderBase.xml' path='docs/members[@name="SqlAuthenticationProviderBase"]/AcquireTokenAsync/*'/>
    public abstract Task<SqlAuthenticationTokenBase> AcquireTokenAsync(SqlAuthenticationParametersBase parameters);
}
