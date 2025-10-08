// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class ConnectionHelper
    {
        private static Assembly s_MicrosoftDotData = Assembly.Load(new AssemblyName(typeof(SqlConnection).GetTypeInfo().Assembly.FullName));
        private static Type s_sqlConnection = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.SqlConnection");
        private static Type s_sqlInternalConnection = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.SqlInternalConnection");
        private static Type s_sqlInternalConnectionTds = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.SqlInternalConnectionTds");
        private static Type s_dbConnectionInternal = s_MicrosoftDotData.GetType("Microsoft.Data.ProviderBase.DbConnectionInternal");
        private static Type s_tdsParser = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.TdsParser");
        private static Type s_tdsParserStateObject = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
        private static Type s_SQLDNSInfo = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.SQLDNSInfo");
        private static PropertyInfo s_sqlConnectionInternalConnection = s_sqlConnection.GetProperty("InnerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_dbConnectionInternalPool = s_dbConnectionInternal.GetProperty("Pool", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo s_dbConnectionInternalIsConnectionAlive = s_dbConnectionInternal.GetMethod("IsConnectionAlive", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_sqlInternalConnectionTdsParser = s_sqlInternalConnectionTds.GetField("_parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_innerConnectionProperty = s_sqlConnection.GetProperty("InnerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_tdsParserProperty = s_sqlInternalConnectionTds.GetProperty("Parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_tdsParserStateObjectProperty = s_tdsParser.GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_enforceTimeoutDelayProperty = s_tdsParserStateObject.GetField("_enforceTimeoutDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_enforcedTimeoutDelayInMilliSeconds = s_tdsParserStateObject.GetField("_enforcedTimeoutDelayInMilliSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_pendingSQLDNSObject = s_sqlInternalConnectionTds.GetField("pendingSQLDNSObject", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_pendingSQLDNS_FQDN = s_SQLDNSInfo.GetProperty("FQDN", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo s_pendingSQLDNS_AddrIPv4 = s_SQLDNSInfo.GetProperty("AddrIPv4", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo s_pendingSQLDNS_AddrIPv6 = s_SQLDNSInfo.GetProperty("AddrIPv6", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo s_pendingSQLDNS_Port = s_SQLDNSInfo.GetProperty("Port", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo dbConnectionInternalIsTransRoot = s_dbConnectionInternal.GetProperty("IsTransactionRoot", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo dbConnectionInternalEnlistedTrans = s_sqlInternalConnection.GetProperty("EnlistedTransaction", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo dbConnectionInternalIsTxRootWaitingForTxEnd = s_dbConnectionInternal.GetProperty("IsTxRootWaitingForTxEnd", BindingFlags.Instance | BindingFlags.NonPublic);

        public static object GetConnectionPool(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return s_dbConnectionInternalPool.GetValue(internalConnection, null);
        }

        public static object GetInternalConnection(this SqlConnection connection)
        {
            VerifyObjectIsConnection(connection);
            object internalConnection = s_sqlConnectionInternalConnection.GetValue(connection, null);
            Debug.Assert(((internalConnection != null) && (s_dbConnectionInternal.IsInstanceOfType(internalConnection))), "Connection provided has an invalid internal connection");
            return internalConnection;
        }

        public static bool IsConnectionAlive(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)s_dbConnectionInternalIsConnectionAlive.Invoke(internalConnection, new object[] { false });
        }

        private static void VerifyObjectIsInternalConnection(object internalConnection)
        {
            if (internalConnection == null)
            {
                throw new ArgumentNullException(nameof(internalConnection));
            }

            if (!s_dbConnectionInternal.IsInstanceOfType(internalConnection))
            {
                throw new ArgumentException("Object provided was not a DbConnectionInternal", nameof(internalConnection));
            }
        }

        private static void VerifyObjectIsConnection(object connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (!s_sqlConnection.IsInstanceOfType(connection))
            {
                throw new ArgumentException("Object provided was not a SqlConnection", nameof(connection));
            }
        }

        public static bool IsEnlistedInTransaction(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (dbConnectionInternalEnlistedTrans.GetValue(internalConnection, null) != null);
        }

        public static bool IsTransactionRoot(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)dbConnectionInternalIsTransRoot.GetValue(internalConnection, null);
        }
        
        public static bool IsTxRootWaitingForTxEnd(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)dbConnectionInternalIsTxRootWaitingForTxEnd.GetValue(internalConnection, null);
        }

        public static object GetParser(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return s_sqlInternalConnectionTdsParser.GetValue(internalConnection);
        }

        public static void SetEnforcedTimeout(this SqlConnection connection, bool enforce, int timeout)
        {
            VerifyObjectIsConnection(connection);
            var stateObj = s_tdsParserStateObjectProperty.GetValue(
                            s_tdsParserProperty.GetValue(
                                s_innerConnectionProperty.GetValue(
                                    connection, null), null));
            s_enforceTimeoutDelayProperty.SetValue(stateObj, enforce);
            s_enforcedTimeoutDelayInMilliSeconds.SetValue(stateObj, timeout);
        }

        /// <summary>
        /// Resolve the established socket end point information for TCP protocol.
        /// </summary>
        /// <param name="connection">Active connection to extract the requested data</param>
        /// <returns>FQDN, AddrIPv4, AddrIPv6, and Port in sequence</returns>
        public static Tuple<string, string, string, string> GetSQLDNSInfo(this SqlConnection connection)
        {
            object internalConnection = GetInternalConnection(connection);
            VerifyObjectIsInternalConnection(internalConnection);
            object pendingSQLDNSInfo = s_pendingSQLDNSObject.GetValue(internalConnection);
            string fqdn = s_pendingSQLDNS_FQDN.GetValue(pendingSQLDNSInfo) as string;
            string ipv4 = s_pendingSQLDNS_AddrIPv4.GetValue(pendingSQLDNSInfo) as string;
            string ipv6 = s_pendingSQLDNS_AddrIPv6.GetValue(pendingSQLDNSInfo) as string;
            string port = s_pendingSQLDNS_Port.GetValue(pendingSQLDNSInfo) as string;
            return new Tuple<string, string, string, string>(fqdn, ipv4, ipv6, port);
        }
    }
}
