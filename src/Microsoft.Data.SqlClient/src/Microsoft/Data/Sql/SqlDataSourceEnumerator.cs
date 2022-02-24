// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.Sql
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/SqlDataSourceEnumerator/*' />
    public sealed class SqlDataSourceEnumerator : DbDataSourceEnumerator
    {
        private static readonly SqlDataSourceEnumerator s_singletonInstance = new();
        internal const string ServerName = "ServerName";
        internal const string InstanceName = "InstanceName";
        internal const string IsClustered = "IsClustered";
        internal const string Version = "Version";

        private SqlDataSourceEnumerator() : base()
        {
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/Instance/*' />  
        public static SqlDataSourceEnumerator Instance => SqlDataSourceEnumerator.s_singletonInstance;

        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/GetDataSources/*' />      
        override public DataTable GetDataSources()
        {
#if NETFRAMEWORK
            return SqlDataSourceEnumeratorNativeHelper.GetDataSources();
#else
            return SqlClient.TdsParserStateObjectFactory.UseManagedSNI ? SqlDataSourceEnumeratorManagedHelper.GetDataSources() : SqlDataSourceEnumeratorNativeHelper.GetDataSources();
#endif
        }
    }
}
