// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlAuthenticationProviderManager
    {
        static SqlAuthenticationProviderManager()
        {
            var activeDirectoryAuthNativeProvider = new ActiveDirectoryNativeAuthenticationProvider();
            Instance = new SqlAuthenticationProviderManager();
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, activeDirectoryAuthNativeProvider);
        }
    }
}
