// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.Sql
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/SqlDataSourceEnumerator/*' />
    public sealed partial class SqlDataSourceEnumerator : DbDataSourceEnumerator
    {
        private partial DataTable GetDataSourcesInternal()
        {
#if NETFRAMEWORK
            return SqlDataSourceEnumeratorNativeHelper.GetDataSources();
#else
            return SqlClient.LocalAppContextSwitches.UseManagedNetworking ? SqlDataSourceEnumeratorManagedHelper.GetDataSources() : SqlDataSourceEnumeratorNativeHelper.GetDataSources();
#endif
        }
    }
}
