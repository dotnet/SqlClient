// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/SqlAuthenticationMethod/*'/>
public enum SqlAuthenticationMethod
{
    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/NotSpecified/*'/>
    NotSpecified = 0,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/SqlPassword/*'/>
    SqlPassword = 1,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryPassword/*'/>
    ActiveDirectoryPassword = 2,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryIntegrated/*'/>
    ActiveDirectoryIntegrated = 3,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryInteractive/*'/>
    ActiveDirectoryInteractive = 4,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryServicePrincipal/*'/>
    ActiveDirectoryServicePrincipal = 5,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryDeviceCodeFlow/*'/>
    ActiveDirectoryDeviceCodeFlow = 6,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryManagedIdentity/*'/>
    ActiveDirectoryManagedIdentity = 7,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryMSI/*'/>
    ActiveDirectoryMSI = 8,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryDefault/*'/>
    ActiveDirectoryDefault = 9,

    /// <include file='../doc/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryWorkloadIdentity/*'/>
    ActiveDirectoryWorkloadIdentity = 10
}
