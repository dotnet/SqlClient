// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ActiveDirectoryAuthenticationProvider/*'/>
    public sealed partial class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetParentActivityOrWindowFunc/*'/>
        public void SetParentActivityOrWindowFunc(System.Func<object> parentActivityOrWindowFunc) { }
    }
}
