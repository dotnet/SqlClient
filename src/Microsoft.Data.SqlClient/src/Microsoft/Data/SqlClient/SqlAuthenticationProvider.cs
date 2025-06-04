// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
    public abstract class SqlAuthenticationProvider : SqlAuthenticationProviderBase
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/GetProvider/*'/>
        public static SqlAuthenticationProvider GetProvider(SqlAuthenticationMethod authenticationMethod)
        {
            return SqlAuthenticationProviderManager.Instance.GetProvider(authenticationMethod);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SetProvider/*'/>
        public static bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider)
        {
            return SqlAuthenticationProviderManager.Instance.SetProvider(authenticationMethod, provider);
        }
    }
}
