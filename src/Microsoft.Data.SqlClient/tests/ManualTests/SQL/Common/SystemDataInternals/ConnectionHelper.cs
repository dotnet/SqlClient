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

        // DbConnectionInternal
        private static readonly Type s_dbConnectionInternal = s_MicrosoftDotData.GetType("Microsoft.Data.ProviderBase.DbConnectionInternal");
        private static readonly MethodInfo s_dbConnectionInternal_IsConnectionAlive =
            s_dbConnectionInternal.GetMethod("IsConnectionAlive", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo s_dbConnectionInternal_IsTransationRoot =
            s_dbConnectionInternal.GetProperty("IsTransactionRoot", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo s_dbConnectionInternal_IsTxRootWaitingForTxEnd =
            s_dbConnectionInternal.GetProperty("IsTxRootWaitingForTxEnd", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo s_dbConnectionInternal_Pool =
            s_dbConnectionInternal.GetProperty("Pool", BindingFlags.Instance | BindingFlags.NonPublic);

        // SqlConnection
        private static readonly Type s_sqlConnection = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.SqlConnection");
        private static readonly PropertyInfo s_sqlConnection_InnerConnection =
            s_sqlConnection.GetProperty("InnerConnection", BindingFlags.Instance | BindingFlags.NonPublic);

        // SqlConnectionInternal
        private static readonly Type s_sqlConnectionInternal = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.Connection.SqlConnectionInternal");
        private static readonly PropertyInfo s_sqlConnectionInternal_EnlistedTransaction =
            s_sqlConnectionInternal.GetProperty("EnlistedTransaction", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo s_sqlConnectionInternal_Parser =
            s_sqlConnectionInternal.GetProperty("Parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_sqlConnectionInternal_pendingSQLDNSObject =
            s_sqlConnectionInternal.GetField("pendingSQLDNSObject", BindingFlags.Instance | BindingFlags.NonPublic);

        // SQLDNSInfo
        private static readonly Type s_SQLDNSInfo = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.SQLDNSInfo");
        private static readonly PropertyInfo s_SQLDNSInfo_AddrIPv4 =
            s_SQLDNSInfo.GetProperty("AddrIPv4", BindingFlags.Instance | BindingFlags.Public);
        private static readonly PropertyInfo s_SQLDNSInfo_AddrIPv6 =
            s_SQLDNSInfo.GetProperty("AddrIPv6", BindingFlags.Instance | BindingFlags.Public);
        private static readonly PropertyInfo s_SQLDNSInfo_FQDN =
            s_SQLDNSInfo.GetProperty("FQDN", BindingFlags.Instance | BindingFlags.Public);
        private static readonly PropertyInfo s_SQLDNSInfo_Port =
            s_SQLDNSInfo.GetProperty("Port", BindingFlags.Instance | BindingFlags.Public);

        // TdsParser
        private static readonly Type s_tdsParser = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.TdsParser");
        private static readonly FieldInfo s_tdsParser_physicalStateObj =
            s_tdsParser.GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic);

        // TdsParserStateObject
        private static readonly Type s_tdsParserStateObject = s_MicrosoftDotData.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
        private static readonly FieldInfo s_tdsParserStateObject_enforcedTimeoutDelayInMilliSeconds =
            s_tdsParserStateObject.GetField("_enforcedTimeoutDelayInMilliSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_tdsParserStateObject_enforceTimeoutDelay =
            s_tdsParserStateObject.GetField("_enforceTimeoutDelay", BindingFlags.Instance | BindingFlags.NonPublic);

        public static object GetConnectionPool(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return s_dbConnectionInternal_Pool.GetValue(internalConnection, null);
        }

        public static object GetInternalConnection(this SqlConnection connection)
        {
            VerifyObjectIsConnection(connection);
            object internalConnection = s_sqlConnection_InnerConnection.GetValue(connection, null);
            Debug.Assert(((internalConnection != null) && (s_dbConnectionInternal.IsInstanceOfType(internalConnection))), "Connection provided has an invalid internal connection");
            return internalConnection;
        }

        public static bool IsConnectionAlive(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)s_dbConnectionInternal_IsConnectionAlive.Invoke(internalConnection, new object[] { false });
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
            return (s_sqlConnectionInternal_EnlistedTransaction.GetValue(internalConnection, null) != null);
        }

        public static bool IsTransactionRoot(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)s_dbConnectionInternal_IsTransationRoot.GetValue(internalConnection, null);
        }
        
        public static bool IsTxRootWaitingForTxEnd(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return (bool)s_dbConnectionInternal_IsTxRootWaitingForTxEnd.GetValue(internalConnection, null);
        }

        public static object GetParser(object internalConnection)
        {
            VerifyObjectIsInternalConnection(internalConnection);
            return s_sqlConnectionInternal_Parser.GetValue(internalConnection);
        }

        public static void SetEnforcedTimeout(this SqlConnection connection, bool enforce, int timeout)
        {
            VerifyObjectIsConnection(connection);

            var innerConnection = s_sqlConnection_InnerConnection.GetValue(connection, null);
            var parser = s_sqlConnectionInternal_Parser.GetValue(innerConnection, null);
            var stateObj = s_tdsParser_physicalStateObj.GetValue(parser);

            s_tdsParserStateObject_enforceTimeoutDelay.SetValue(stateObj, enforce);
            s_tdsParserStateObject_enforcedTimeoutDelayInMilliSeconds.SetValue(stateObj, timeout);
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
            object pendingSQLDNSInfo = s_sqlConnectionInternal_pendingSQLDNSObject.GetValue(internalConnection);
            string fqdn = s_SQLDNSInfo_FQDN.GetValue(pendingSQLDNSInfo) as string;
            string ipv4 = s_SQLDNSInfo_AddrIPv4.GetValue(pendingSQLDNSInfo) as string;
            string ipv6 = s_SQLDNSInfo_AddrIPv6.GetValue(pendingSQLDNSInfo) as string;
            string port = s_SQLDNSInfo_Port.GetValue(pendingSQLDNSInfo) as string;
            return new Tuple<string, string, string, string>(fqdn, ipv4, ipv6, port);
        }
    }
}
