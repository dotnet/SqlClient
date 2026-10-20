// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationInitializer.xml'
///     path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
public abstract class SqlAuthenticationInitializer
{
    /// <include file='../doc/SqlAuthenticationInitializer.xml'
    ///     path='docs/members[@name="SqlAuthenticationInitializer"]/ctor/*'/>
    protected SqlAuthenticationInitializer() { }

    /// <include file='../doc/SqlAuthenticationInitializer.xml'
    ///     path='docs/members[@name="SqlAuthenticationInitializer"]/Initialize/*'/>
    public abstract void Initialize();
}
