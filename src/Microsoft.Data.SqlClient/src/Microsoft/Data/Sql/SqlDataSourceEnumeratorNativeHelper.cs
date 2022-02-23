// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Sql
{
    /// <summary>
    /// Provides a mechanism for enumerating all available instances of SQL Server within the local network
    /// </summary>
    internal class SqlDataSourceEnumeratorNativeHelper
    {
        internal const string ServerName = "ServerName";
        internal const string InstanceName = "InstanceName";
        internal const string IsClustered = "IsClustered";
        internal const string Version = "Version";
        private static readonly string s_version = "Version:";
        private static readonly string s_cluster = "Clustered:";
        private static readonly int s_clusterLength = s_cluster.Length;
        private static readonly int s_versionLength = s_version.Length;
        private const int TimeoutSeconds = ADP.DefaultCommandTimeout;
        private static long s_timeoutTime;                                // variable used for timeout computations, holds the value of the hi-res performance counter at which this request should expire

        /// <summary>
        /// Retrieves a DataTable containing information about all visible SQL Server instances
        /// </summary>
        /// <returns></returns>
        internal static DataTable GetDataSources()
        {
            (new NamedPermissionSet("FullTrust")).Demand(); // SQLBUDT 244304
            char[] buffer = null;
            StringBuilder strbldr = new();

            int bufferSize = 65536;
            int readLength = 0;
            buffer = new char[bufferSize];
            bool more = true;
            bool failure = false;
            IntPtr handle = ADP.s_ptrZero;

            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                s_timeoutTime = TdsParserStaticMethods.GetTimeoutSeconds(TimeoutSeconds);
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { }
                finally
                {
                    handle = SNINativeMethodWrapper.SNIServerEnumOpen();
                }

                if (handle != ADP.s_ptrZero)
                {
                    while (more && !TdsParserStaticMethods.TimeoutHasExpired(s_timeoutTime))
                    {
#if NETFRAMEWORK
                        readLength = SNINativeMethodWrapper.SNIServerEnumRead(handle, buffer, bufferSize, out more);
#else
                        readLength = SNINativeMethodWrapper.SNIServerEnumRead(handle, buffer, bufferSize, out more);
#endif
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlDataSourceEnumeratorNativeHelper.GetDataSources|INFO> GetDataSources:SNIServerEnumRead returned readlength {0}", readLength);
                        if (readLength > bufferSize)
                        {
                            failure = true;
                            more = false;
                        }
                        else if (0 < readLength)
                        {
                            strbldr.Append(buffer);
                        }
                    }
                }
            }
            finally
            {
                if (handle != ADP.s_ptrZero)
                {
                    SNINativeMethodWrapper.SNIServerEnumClose(handle);
                }
            }

            if (failure)
            {
                Debug.Assert(false, "GetDataSources:SNIServerEnumRead returned bad length");
                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlDataSourceEnumerator.GetDataSources|ERR> GetDataSources:SNIServerEnumRead returned bad length, requested %d, received %d", bufferSize, readLength);
                throw ADP.ArgumentOutOfRange("readLength");
            }
            return ParseServerEnumString(strbldr.ToString());
        }

        static private System.Data.DataTable ParseServerEnumString(string serverInstances)
        {
            DataTable dataTable = new("SqlDataSources");
            dataTable.Locale = CultureInfo.InvariantCulture;
            dataTable.Columns.Add(ServerName, typeof(string));
            dataTable.Columns.Add(InstanceName, typeof(string));
            dataTable.Columns.Add(IsClustered, typeof(string));
            dataTable.Columns.Add(Version, typeof(string));
            string serverName = null;
            string instanceName = null;
            string isClustered = null;
            string version = null;
            string[] serverinstanceslist = serverInstances.Split(new string[] { "\0\0\0" }, StringSplitOptions.None);
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlDataSourceEnumeratorNativeHelper.ParseServerEnumString|INFO> Number of server instances results recieved are {0}", serverinstanceslist.Length);

            // Every row comes in the format "serverName\instanceName;Clustered:[Yes|No];Version:.." 
            // Every row is terminated by a null character.
            // Process one row at a time
            foreach (string instance in serverinstanceslist)
            {
                //  string value = instance.Trim('\0'); // MDAC 91934
                string value = instance.Replace("\0", "");
                if (0 == value.Length)
                {
                    continue;
                }
                foreach (string instance2 in value.Split(';'))
                {
                    if (serverName == null)
                    {
                        foreach (string instance3 in instance2.Split('\\'))
                        {
                            if (serverName == null)
                            {
                                serverName = instance3;
                                continue;
                            }
                            Debug.Assert(instanceName == null);
                            instanceName = instance3;
                        }
                        continue;
                    }
                    if (isClustered == null)
                    {
                        Debug.Assert(string.Compare(s_cluster, 0, instance2, 0, s_clusterLength, StringComparison.OrdinalIgnoreCase) == 0);
                        isClustered = instance2.Substring(s_clusterLength);
                        continue;
                    }
                    Debug.Assert(version == null);
                    Debug.Assert(string.Compare(s_version, 0, instance2, 0, s_versionLength, StringComparison.OrdinalIgnoreCase) == 0);
                    version = instance2.Substring(s_versionLength);
                }

                string query = "ServerName='" + serverName + "'";

                if (!ADP.IsEmpty(instanceName))
                { // SQL BU DT 20006584: only append instanceName if present.
                    query += " AND InstanceName='" + instanceName + "'";
                }

                // SNI returns dupes - do not add them.  SQL BU DT 290323
                if (dataTable.Select(query).Length == 0)
                {
                    DataRow dataRow = dataTable.NewRow();
                    dataRow[0] = serverName;
                    dataRow[1] = instanceName;
                    dataRow[2] = isClustered;
                    dataRow[3] = version;
                    dataTable.Rows.Add(dataRow);
                }
                serverName = null;
                instanceName = null;
                isClustered = null;
                version = null;
            }
            foreach (DataColumn column in dataTable.Columns)
            {
                column.ReadOnly = true;
            }
            return dataTable;
        }
    }
}
