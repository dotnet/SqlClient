// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class ConnectionHelper
    {
        private static MethodInfo s_dbConnectionInternalIsConnectionAlive = typeof(DbConnectionInternal).GetMethod("IsConnectionAlive", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_sqlInternalConnectionTdsParser = typeof(SqlInternalConnectionTds).GetField("_parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_innerConnectionProperty = typeof(SqlConnection).GetProperty("InnerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_tdsParserProperty = typeof(SqlInternalConnectionTds).GetProperty("Parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_tdsParserStateObjectProperty = typeof(TdsParser).GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_enforceTimeoutDelayProperty = typeof(TdsParserStateObject).GetField("_enforceTimeoutDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_enforcedTimeoutDelayInMilliSeconds = typeof(TdsParserStateObject).GetField("_enforcedTimeoutDelayInMilliSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_pendingSQLDNSObject = typeof(SqlInternalConnectionTds).GetField("pendingSQLDNSObject", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_pendingSQLDNS_FQDN = typeof(SQLDNSInfo).GetProperty("FQDN", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo s_pendingSQLDNS_AddrIPv4 = typeof(SQLDNSInfo).GetProperty("AddrIPv4", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo s_pendingSQLDNS_AddrIPv6 = typeof(SQLDNSInfo).GetProperty("AddrIPv6", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo s_pendingSQLDNS_Port = typeof(SQLDNSInfo).GetProperty("Port", BindingFlags.Instance | BindingFlags.Public);
        private static PropertyInfo dbConnectionInternalIsTransRoot = typeof(DbConnectionInternal).GetProperty("IsTransactionRoot", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo dbConnectionInternalEnlistedTrans = typeof(SqlInternalConnection).GetProperty("EnlistedTransaction", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo dbConnectionInternalIsTxRootWaitingForTxEnd = typeof(DbConnectionInternal).GetProperty("IsTxRootWaitingForTxEnd", BindingFlags.Instance | BindingFlags.NonPublic);

        public static DbConnectionPool GetConnectionPool(DbConnectionInternal internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return internalConnection.Pool;
        }

        public static DbConnectionInternal GetInternalConnection(this SqlConnection connection)
        {
            VerifyObjectIsConnection(connection);
            DbConnectionInternal internalConnection = connection.InnerConnection;
            Debug.Assert(((internalConnection != null) && (typeof(DbConnectionInternal).IsInstanceOfType(internalConnection))), "Connection provided has an invalid internal connection");
            return internalConnection;
        }

        public static bool IsConnectionAlive(DbConnectionInternal internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)s_dbConnectionInternalIsConnectionAlive.Invoke(internalConnection, new object[] { false });
        }

        private static void VerifyObjectIsInternalConnection(DbConnectionInternal internalConnection)
        {
            if (internalConnection == null)
                throw new ArgumentNullException(nameof(internalConnection));
            if (!typeof(DbConnectionInternal).IsInstanceOfType(internalConnection))
                throw new ArgumentException("Object provided was not a DbConnectionInternal", nameof(internalConnection));
        }

        private static void VerifyObjectIsConnection(SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (!typeof(SqlConnection).IsInstanceOfType(connection))
                throw new ArgumentException("Object provided was not a SqlConnection", nameof(connection));
        }

        public static bool IsEnlistedInTransaction(DbConnectionInternal internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (dbConnectionInternalEnlistedTrans.GetValue(internalConnection, null) != null);
        }

        public static bool IsTransactionRoot(DbConnectionInternal internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)dbConnectionInternalIsTransRoot.GetValue(internalConnection, null);
        }
        
        public static bool IsTxRootWaitingForTxEnd(DbConnectionInternal internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)dbConnectionInternalIsTxRootWaitingForTxEnd.GetValue(internalConnection, null);
        }

        public static object GetParser(DbConnectionInternal internalConnection)
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
            DbConnectionInternal internalConnection = GetInternalConnection(connection);
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
