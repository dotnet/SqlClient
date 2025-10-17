// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/SqlAuthenticationMethod/*'/>
public enum SqlAuthenticationMethod : int
{
    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/NotSpecified/*'/>
    NotSpecified = 0,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/SqlPassword/*'/>
    SqlPassword,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryPassword/*'/>
    [Obsolete("ActiveDirectoryPassword is deprecated, use a more secure authentication method. See https://aka.ms/SqlClientEntraIDAuthentication for more details.")]
    ActiveDirectoryPassword,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryIntegrated/*'/>
    ActiveDirectoryIntegrated,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryInteractive/*'/>
    ActiveDirectoryInteractive,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryServicePrincipal/*'/>
    ActiveDirectoryServicePrincipal,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryDeviceCodeFlow/*'/>
    ActiveDirectoryDeviceCodeFlow,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryManagedIdentity/*'/>
    ActiveDirectoryManagedIdentity,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryMSI/*'/>
    ActiveDirectoryMSI,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryDefault/*'/>
    ActiveDirectoryDefault,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryWorkloadIdentity/*'/>
    ActiveDirectoryWorkloadIdentity
}
