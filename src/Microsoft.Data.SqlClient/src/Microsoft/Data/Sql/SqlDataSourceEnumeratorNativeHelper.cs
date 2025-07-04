// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using static Microsoft.Data.Sql.SqlDataSourceEnumeratorUtil;

namespace Microsoft.Data.Sql
{
    /// <summary>
    /// Provides a mechanism for enumerating all available instances of SQL Server within the local network
    /// </summary>
    internal static class SqlDataSourceEnumeratorNativeHelper
    {
        /// <summary>
        /// Retrieves a DataTable containing information about all visible SQL Server instances
        /// </summary>
        /// <returns></returns>
        internal static DataTable GetDataSources()
        {
#if NETFRAMEWORK
            (new NamedPermissionSet("FullTrust")).Demand(); // SQLBUDT 244304
#endif
            char[] buffer = null;
            StringBuilder strbldr = new();

            int bufferSize = 1024;
            int readLength = 0;
            buffer = new char[bufferSize];
            bool more = true;
            bool failure = false;
            IntPtr handle = ADP.s_ptrZero;
#if NETFRAMEWORK
            RuntimeHelpers.PrepareConstrainedRegions();
#endif
            try
            {
                long s_timeoutTime = TdsParserStaticMethods.GetTimeoutSeconds(ADP.DefaultCommandTimeout);
#if NETFRAMEWORK
                RuntimeHelpers.PrepareConstrainedRegions();
#endif
                try
                { }
                finally
                {
                    handle = SniNativeWrapper.SniServerEnumOpen();
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> {2} returned handle = {3}.",
                                                           nameof(SqlDataSourceEnumeratorNativeHelper),
                                                           nameof(GetDataSources),
                                                           nameof(SniNativeWrapper.SniServerEnumOpen), handle);
                }

                if (handle != ADP.s_ptrZero)
                {
                    while (more && !TdsParserStaticMethods.TimeoutHasExpired(s_timeoutTime))
                    {
                        readLength = SniNativeWrapper.SniServerEnumRead(handle, buffer, bufferSize, out more);

                        SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> {2} returned 'readlength':{3}, and 'more':{4} with 'bufferSize' of {5}",
                                                               nameof(SqlDataSourceEnumeratorNativeHelper),
                                                               nameof(GetDataSources),
                                                               nameof(SniNativeWrapper.SniServerEnumRead),
                                                               readLength, more, bufferSize);
                        if (readLength > bufferSize)
                        {
                            failure = true;
                            more = false;
                        }
                        else if (readLength > 0)
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
                    SniNativeWrapper.SniServerEnumClose(handle);
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> {2} called.",
                                                           nameof(SqlDataSourceEnumeratorNativeHelper),
                                                           nameof(GetDataSources),
                                                           nameof(SniNativeWrapper.SniServerEnumClose));
                }
            }

            if (failure)
            {
                Debug.Assert(false, $"{nameof(GetDataSources)}:{nameof(SniNativeWrapper.SniServerEnumRead)} returned bad length");
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|ERR> {2} returned bad length, requested buffer {3}, received {4}",
                                                       nameof(SqlDataSourceEnumeratorNativeHelper),
                                                       nameof(GetDataSources),
                                                       nameof(SniNativeWrapper.SniServerEnumRead),
                                                       bufferSize, readLength);

                throw ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.ADP_ParameterValueOutOfRange, readLength), nameof(readLength));
            }
            return ParseServerEnumString(strbldr.ToString());
        }

        private static DataTable ParseServerEnumString(string serverInstances)
        {
            DataTable dataTable = PrepareDataTable();
            string serverName = null;
            string instanceName = null;
            string isClustered = null;
            string version = null;
            string[] serverinstanceslist = serverInstances.Split(EndOfServerInstanceDelimiter_Native);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Number of received server instances are {2}",
                                                   nameof(SqlDataSourceEnumeratorNativeHelper), nameof(ParseServerEnumString), serverinstanceslist.Length);

            // Every row comes in the format "serverName\instanceName;Clustered:[Yes|No];Version:.." 
            // Every row is terminated by a null character.
            // Process one row at a time
            foreach (string instance in serverinstanceslist)
            {
                string value = instance.Trim(EndOfServerInstanceDelimiter_Native); // MDAC 91934
                if (value.Length == 0)
                {
                    continue;
                }
                foreach (string instance2 in value.Split(InstanceKeysDelimiter))
                {
                    if (serverName == null)
                    {
                        foreach (string instance3 in instance2.Split(ServerNamesAndInstanceDelimiter))
                        {
                            if (serverName == null)
                            {
                                serverName = instance3;
                                continue;
                            }
                            Debug.Assert(instanceName == null, $"{nameof(instanceName)}({instanceName}) is not null.");
                            instanceName = instance3;
                        }
                        continue;
                    }
                    if (isClustered == null)
                    {
                        Debug.Assert(string.Compare(Clustered, 0, instance2, 0, s_clusteredLength, StringComparison.OrdinalIgnoreCase) == 0,
                                     $"{nameof(Clustered)} ({Clustered}) doesn't equal {nameof(instance2)} ({instance2})");
                        isClustered = instance2.Substring(s_clusteredLength);
                        continue;
                    }
                    Debug.Assert(version == null, $"{nameof(version)}({version}) is not null.");
                    Debug.Assert(string.Compare(SqlDataSourceEnumeratorUtil.Version, 0, instance2, 0, s_versionLength, StringComparison.OrdinalIgnoreCase) == 0,
                                 $"{nameof(SqlDataSourceEnumeratorUtil.Version)} ({SqlDataSourceEnumeratorUtil.Version}) doesn't equal {nameof(instance2)} ({instance2})");
                    version = instance2.Substring(s_versionLength);
                }

                string query = "ServerName='" + serverName + "'";

                if (!string.IsNullOrEmpty(instanceName))
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
            return dataTable.SetColumnsReadOnly();
        }
    }
}
