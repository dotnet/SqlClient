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
        private static string _Version = "Version:";
        private static string _Cluster = "Clustered:";
        private static int _clusterLength = _Cluster.Length;
        private static int _versionLength = _Version.Length;
        private const int timeoutSeconds = ADP.DefaultCommandTimeout;
        private static long timeoutTime;                                // variable used for timeout computations, holds the value of the hi-res performance counter at which this request should expire

        /// <summary>
        /// Retrieves a DataTable containing information about all visible SQL Server instances
        /// </summary>
        /// <returns></returns>
        internal static DataTable GetDataSources()
        {
            (new NamedPermissionSet("FullTrust")).Demand(); // SQLBUDT 244304
            char[] buffer = null;
            StringBuilder strbldr = new StringBuilder();

            Int32 bufferSize = 1024;
            Int32 readLength = 0;
            buffer = new char[bufferSize];
            bool more = true;
            bool failure = false;
            IntPtr handle = ADP.s_ptrZero;

            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                timeoutTime = TdsParserStaticMethods.GetTimeoutSeconds(timeoutSeconds);
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { }
                finally
                {
                    handle = SNINativeMethodWrapper.SNIServerEnumOpen();
                }

                if (handle != ADP.s_ptrZero)
                {
                    while (more && !TdsParserStaticMethods.TimeoutHasExpired(timeoutTime))
                    {
#if NETFRAMEWORK
                        readLength = SNINativeMethodWrapper.SNIServerEnumRead(handle, buffer, bufferSize, out more);
#else
                        readLength = SNINativeMethodWrapper.SNIServerEnumRead(handle, buffer, bufferSize, out more);
#endif
                        if (readLength > bufferSize)
                        {
                            failure = true;
                            more = false;
                        }
                        else if (0 < readLength)
                        {
                            strbldr.Append(buffer, 0, readLength);
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
            DataTable dataTable = new DataTable("SqlDataSources");
            dataTable.Locale = CultureInfo.InvariantCulture;
            dataTable.Columns.Add(ServerName, typeof(string));
            dataTable.Columns.Add(InstanceName, typeof(string));
            dataTable.Columns.Add(IsClustered, typeof(string));
            dataTable.Columns.Add(Version, typeof(string));
            DataRow dataRow = null;
            string serverName = null;
            string instanceName = null;
            string isClustered = null;
            string version = null;

            // Every row comes in the format "serverName\instanceName;Clustered:[Yes|No];Version:.." 
            // Every row is terminated by a null character.
            // Process one row at a time
            foreach (string instance in serverInstances.Split(new string[] { "\0\0\0" }, StringSplitOptions.None))
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
                        Debug.Assert(String.Compare(_Cluster, 0, instance2, 0, _clusterLength, StringComparison.OrdinalIgnoreCase) == 0);
                        isClustered = instance2.Substring(_clusterLength);
                        continue;
                    }
                    Debug.Assert(version == null);
                    Debug.Assert(String.Compare(_Version, 0, instance2, 0, _versionLength, StringComparison.OrdinalIgnoreCase) == 0);
                    version = instance2.Substring(_versionLength);
                }

                string query = "ServerName='" + serverName + "'";

                if (!ADP.IsEmpty(instanceName))
                { // SQL BU DT 20006584: only append instanceName if present.
                    query += " AND InstanceName='" + instanceName + "'";
                }

                // SNI returns dupes - do not add them.  SQL BU DT 290323
                if (dataTable.Select(query).Length == 0)
                {
                    dataRow = dataTable.NewRow();
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
