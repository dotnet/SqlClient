// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Globalization;

namespace Microsoft.Data.Sql
{
    /// <summary>
    /// const values for SqlDataSourceEnumerator
    /// </summary>
    internal static class SqlDataSourceEnumeratorUtil
    {
        internal const string ServerNameCol = "ServerName";
        internal const string InstanceNameCol = "InstanceName";
        internal const string IsClusteredCol = "IsClustered";
        internal const string VersionNameCol = "Version";

        internal const string Version = "Version:";
        internal const string Clustered = "Clustered:";
        internal static readonly int s_versionLength = Version.Length;
        internal static readonly int s_clusteredLength = Clustered.Length;

        internal static readonly string[] s_endOfServerInstanceDelimiter_Managed = new[] { ";;" };
        internal const char EndOfServerInstanceDelimiter_Native = '\0';
        internal const char InstanceKeysDelimiter = ';';
        internal const char ServerNamesAndInstanceDelimiter = '\\';

        internal static DataTable PrepareDataTable()
        {
            DataTable dataTable = new("SqlDataSources");
            dataTable.Locale = CultureInfo.InvariantCulture;
            dataTable.Columns.Add(ServerNameCol, typeof(string));
            dataTable.Columns.Add(InstanceNameCol, typeof(string));
            dataTable.Columns.Add(IsClusteredCol, typeof(string));
            dataTable.Columns.Add(VersionNameCol, typeof(string));

            return dataTable;
        }

        /// <summary>
        /// Sets all columns read-only.
        /// </summary>
        internal static DataTable SetColumnsReadOnly(this DataTable dataTable)
        {
            foreach (DataColumn column in dataTable.Columns)
            {
                column.ReadOnly = true;
            }
            return dataTable;
        }
    }
}
