// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlAuthenticationProviderManager
    {
        static SqlAuthenticationProviderManager()
        {
            var activeDirectoryAuthProvider = new ActiveDirectoryAuthenticationProvider();
            Instance = new SqlAuthenticationProviderManager();
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, activeDirectoryAuthProvider);
        }
    }
}
