// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClient.Server
{
    /// <summary>
    /// Provides a mechanism for enumerating all available instances of SQL Server within the local network
    /// </summary>
    internal static class SqlDataSourceEnumeratorManagedHelper 
    {
        internal const string ServerName = "ServerName";
        internal const string InstanceName = "InstanceName";
        internal const string IsClustered = "IsClustered";
        internal const string Version = "Version";
        internal const string EndOfServerInstanceDelimiter = ";;";
        internal const char InstanceKeysDelimiter = ';';

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
            dataTable.Columns.Add(ServerName, typeof(string));
            dataTable.Columns.Add(InstanceName, typeof(string));
            dataTable.Columns.Add(IsClustered, typeof(string));
            dataTable.Columns.Add(Version, typeof(string));
            DataRow dataRow;

            if (serverInstances.Length == 0)
            {
                return dataTable;
            }

            string[] numOfServerInstances = serverInstances.Split(new[] { EndOfServerInstanceDelimiter }, StringSplitOptions.None);

            foreach (string currentServerInstance in numOfServerInstances)
            {
                Dictionary<string, string> InstanceDetails = new();
                string[] delimitedKeyValues = currentServerInstance.Split(InstanceKeysDelimiter);
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
                    dataRow[0] = InstanceDetails.ContainsKey(ServerName) == true ?
                                 InstanceDetails[ServerName] : string.Empty;
                    dataRow[1] = InstanceDetails.ContainsKey(InstanceName) == true ?
                                 InstanceDetails[InstanceName] : string.Empty;
                    dataRow[2] = InstanceDetails.ContainsKey(IsClustered) == true ?
                                 InstanceDetails[IsClustered] : string.Empty;
                    dataRow[3] = InstanceDetails.ContainsKey(Version) == true ?
                                 InstanceDetails[Version] : string.Empty;

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
