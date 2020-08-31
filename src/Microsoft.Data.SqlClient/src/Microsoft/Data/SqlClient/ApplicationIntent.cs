// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ApplicationIntent/*'/>
#if NETFRAMEWORK
    [System.Serializable]
#endif
    public enum ApplicationIntent
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ReadWrite/*'/>
        ReadWrite = 0,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ReadOnly/*'/>
        ReadOnly = 1,
    }
}
