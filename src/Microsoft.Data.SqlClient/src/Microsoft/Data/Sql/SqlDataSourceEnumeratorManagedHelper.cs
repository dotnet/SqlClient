// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient.SNI;

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
            return ParseServerEnumString(SSRP.SendBroadcastUDPRequest());
        }

        static private System.Data.DataTable ParseServerEnumString(string serverInstances)
        {
            DataTable dataTable = new("SqlDataSources")
            {
                Locale = CultureInfo.InvariantCulture
            };
            dataTable.Columns.Add(SqlDataSourceEnumeratorUtil.ServerName, typeof(string));
            dataTable.Columns.Add(SqlDataSourceEnumeratorUtil.InstanceName, typeof(string));
            dataTable.Columns.Add(SqlDataSourceEnumeratorUtil.IsClustered, typeof(string));
            dataTable.Columns.Add(SqlDataSourceEnumeratorUtil.Version, typeof(string));
            DataRow dataRow;

            if (serverInstances.Length == 0)
            {
                return dataTable;
            }

            string[] numOfServerInstances = serverInstances.Split(new[] { SqlDataSourceEnumeratorUtil.EndOfServerInstanceDelimiterManaged }, StringSplitOptions.None);
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlDataSourceEnumeratorManagedHelper.ParseServerEnumString|INFO> Number of server instances results recieved are {0}", numOfServerInstances.Length);

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
                    dataRow[0] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.ServerName) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.ServerName] : string.Empty;
                    dataRow[1] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.InstanceName) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.InstanceName] : string.Empty;
                    dataRow[2] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.IsClustered) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.IsClustered] : string.Empty;
                    dataRow[3] = InstanceDetails.ContainsKey(SqlDataSourceEnumeratorUtil.Version) == true ?
                                 InstanceDetails[SqlDataSourceEnumeratorUtil.Version] : string.Empty;

                    dataTable.Rows.Add(dataRow);
                }
            }

            foreach (DataColumn column in dataTable.Columns)
            {
                column.ReadOnly = true;
            }
            return dataTable;
        }
    }
}
