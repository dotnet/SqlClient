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
        internal const string endOfServerInstanceDelimiter = ";;";
        internal const char instanceKeysDelimiter = ';';

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
            DataTable dataTable = new DataTable("SqlDataSources");
            dataTable.Locale = CultureInfo.InvariantCulture;
            dataTable.Columns.Add(ServerName, typeof(string));
            dataTable.Columns.Add(InstanceName, typeof(string));
            dataTable.Columns.Add(IsClustered, typeof(string));
            dataTable.Columns.Add(Version, typeof(string));
            DataRow dataRow;

            if (serverInstances.Length == 0)
            {
                return dataTable;
            }

            string[] numOfServerInstances = serverInstances.Split(new[] { endOfServerInstanceDelimiter }, StringSplitOptions.None);

            foreach (string currentServerInstance in numOfServerInstances)
            {
                Dictionary<string, string> InstanceDetails = new Dictionary<string, string>();
                string[] delimitedKeyValues = currentServerInstance.Split(instanceKeysDelimiter);
                string currentKey = String.Empty;

                for (int keyvalue = 0; keyvalue < delimitedKeyValues.Length; keyvalue++)
                {
                    if (keyvalue % 2 == 0)
                    {
                        currentKey = delimitedKeyValues[keyvalue];
                    }
                    else if (currentKey != String.Empty)
                    {
                        InstanceDetails.Add(currentKey, delimitedKeyValues[keyvalue]);
                    }
                }

                if (InstanceDetails.Count > 0)
                {
                    dataRow = dataTable.NewRow();
                    dataRow[0] = InstanceDetails.ContainsKey(ServerName) == true ?
                                 InstanceDetails[ServerName] : String.Empty;
                    dataRow[1] = InstanceDetails.ContainsKey(InstanceName) == true ?
                                 InstanceDetails[InstanceName] : String.Empty;
                    dataRow[2] = InstanceDetails.ContainsKey(IsClustered) == true ?
                                 InstanceDetails[IsClustered] : String.Empty;
                    dataRow[3] = InstanceDetails.ContainsKey(Version) == true ?
                                 InstanceDetails[Version] : String.Empty;

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
