// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.Sql
{
    /// <summary>
    /// Provides a mechanism for enumerating all available instances of SQL Server within the local network
    /// </summary>
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

        /// <summary>
        /// Gets an instance of the SqlDataSourceEnumerator, which can be used to retrieve information about available SQL Server instances.
        /// </summary>
        public static SqlDataSourceEnumerator Instance => SqlDataSourceEnumerator.s_singletonInstance;

        /// <summary>
        /// Provides a mechanism for enumerating all available instances of SQL Server within the local network.
        /// </summary>
        /// <returns></returns>
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
