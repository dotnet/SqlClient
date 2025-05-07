// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sql;

namespace Microsoft.Data.SqlClient.Server
{
    /// <summary>
    /// Provides a mechanism for enumerating all available instances of SQL Server within the local network
    /// </summary>
    internal static class SqlDataSourceEnumeratorManagedHelper
    {
        /// <summary>
        /// Provides a mechanism for enumerating all available instances of SQL Server within the local network.
        /// </summary>
        /// <returns>DataTable with ServerName,InstanceName,IsClustered and Version</returns>
        internal static DataTable GetDataSources()
        {
            // TODO: Implement multicast request besides the implemented broadcast request.
            throw new System.NotImplementedException(StringsHelper.net_MethodNotImplementedException);
        }

        private static DataTable ParseServerEnumString(string serverInstances)
        {
            DataTable dataTable = SqlDataSourceEnumeratorUtil.PrepareDataTable();
            DataRow dataRow;

            if (serverInstances.Length == 0)
            {
                return dataTable;
            }

            string[] numOfServerInstances = serverInstances.Split(SqlDataSourceEnumeratorUtil.s_endOfServerInstanceDelimiter_Managed, System.StringSplitOptions.None);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Number of received server instances are {2}",
                                                   nameof(SqlDataSourceEnumeratorManagedHelper), nameof(ParseServerEnumString), numOfServerInstances.Length);

            foreach (string currentServerInstance in numOfServerInstances)
            {
                Dictionary<string, string> InstanceDetails = new();
                string[] delimitedKeyValues = currentServerInstance.Split(SqlDataSourceEnumeratorUtil.InstanceKeysDelimiter);
                string currentKey = string.Empty;

                for (int keyvalue = 0; keyvalue < delimitedKeyValues.Length; keyvalue++)
                {
                    if (keyvalue % 2 == 0)
                    {
                        currentKey = delimitedKeyValues[keyvalue];
                    }
                    else if (currentKey != string.Empty)
                    {
                        InstanceDetails.Add(currentKey, delimitedKeyValues[keyvalue]);
                    }
                }

                if (InstanceDetails.Count > 0)
                {
                    dataRow = dataTable.NewRow();
                    dataRow[0] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.ServerNameCol) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.ServerNameCol] : string.Empty;
                    dataRow[1] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.InstanceNameCol) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.InstanceNameCol] : string.Empty;
                    dataRow[2] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.IsClusteredCol) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.IsClusteredCol] : string.Empty;
                    dataRow[3] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.VersionNameCol) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.VersionNameCol] : string.Empty;

                    dataTable.Rows.Add(dataRow);
                }
            }
            return dataTable.SetColumnsReadOnly();
        }
    }
}

#endif
