// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class ConnectionHelper
    {
        private static FieldInfo s_sqlInternalConnectionTdsParser = typeof(SqlInternalConnectionTds).GetField("_parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo s_tdsParserProperty = typeof(SqlInternalConnectionTds).GetProperty("Parser", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_tdsParserStateObjectProperty = typeof(TdsParser).GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_enforceTimeoutDelayProperty = typeof(TdsParserStateObject).GetField("_enforceTimeoutDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_enforcedTimeoutDelayInMilliSeconds = typeof(TdsParserStateObject).GetField("_enforcedTimeoutDelayInMilliSeconds", BindingFlags.Instance | BindingFlags.NonPublic);

        public static object GetParser(DbConnectionInternal internalConnection)
        {
            return s_sqlInternalConnectionTdsParser.GetValue(internalConnection);
        }

        public static void SetEnforcedTimeout(this SqlConnection connection, bool enforce, int timeout)
        {
            object tdsParser = s_tdsParserProperty.GetValue(connection.InnerConnection, null);
            var stateObj = s_tdsParserStateObjectProperty.GetValue(tdsParser);
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
            FieldInfo s_pendingSQLDNSObject = typeof(SqlInternalConnectionTds).GetField("pendingSQLDNSObject", BindingFlags.Instance | BindingFlags.NonPublic);
            SQLDNSInfo pendingSQLDNSInfo = (SQLDNSInfo) s_pendingSQLDNSObject.GetValue(connection.InnerConnection);

            string fqdn = pendingSQLDNSInfo.FQDN;
            string ipv4 = pendingSQLDNSInfo.AddrIPv4;
            string ipv6 = pendingSQLDNSInfo.AddrIPv6;
            string port = pendingSQLDNSInfo.Port;

            return new Tuple<string, string, string, string>(fqdn, ipv4, ipv6, port);
        }
    }
}
