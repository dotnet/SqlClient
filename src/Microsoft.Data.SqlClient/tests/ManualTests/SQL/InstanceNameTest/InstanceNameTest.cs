// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class InstanceNameTest
    {
        private const char SemicolonSeparator = ';';

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ConnectToSQLWithInstanceNameTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);

            bool proceed = true;
            string dataSourceStr = builder.DataSource.Replace("tcp:", "");
            string[] serverNamePartsByBackSlash = dataSourceStr.Split('\\');
            string hostname = serverNamePartsByBackSlash[0];
            if (!dataSourceStr.Contains(",") && serverNamePartsByBackSlash.Length == 2)
            {
                proceed = !string.IsNullOrWhiteSpace(hostname) && IsBrowserAlive(hostname);
            }

            if (proceed)
            {
                using SqlConnection connection = new(builder.ConnectionString);
                connection.Open();
                connection.Close();

                // We can only connect via IP address if we aren't doing remote Kerberos or strict TLS
                if (builder.Encrypt != SqlConnectionEncryptOption.Strict &&
                        (!builder.IntegratedSecurity || hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         hostname.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
                {
                    // Exercise the IP address-specific code in SSRP
                    IPAddress[] addresses = Dns.GetHostAddresses(hostname);
                    builder.DataSource = builder.DataSource.Replace(hostname, addresses[0].ToString());
                    builder.TrustServerCertificate = true;
                    using SqlConnection connection2 = new(builder.ConnectionString);
                    connection2.Open();
                    connection2.Close();
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true, SqlConnectionIPAddressPreference.IPv4First)]
        [InlineData(true, SqlConnectionIPAddressPreference.IPv6First)]
        [InlineData(true, SqlConnectionIPAddressPreference.UsePlatformDefault)]
        [InlineData(false, SqlConnectionIPAddressPreference.IPv4First)]
        [InlineData(false, SqlConnectionIPAddressPreference.IPv6First)]
        [InlineData(false, SqlConnectionIPAddressPreference.UsePlatformDefault)]
        public static void ConnectManagedWithInstanceNameTest(bool useMultiSubnetFailover, SqlConnectionIPAddressPreference ipPreference)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.MultiSubnetFailover = useMultiSubnetFailover;
            builder.IPAddressPreference = ipPreference;

            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out string instanceName));

            if (IsBrowserAlive(hostname) && IsValidInstance(hostname, instanceName))
            {
                builder.DataSource = hostname + "\\" + instanceName;
                using SqlConnection connection = new(builder.ConnectionString);
                connection.Open();
            }

            builder.ConnectTimeout = 2;
            instanceName = "invalidinstance3456";
            if (!IsValidInstance(hostname, instanceName))
            {
                builder.DataSource = hostname + "\\" + instanceName;

                using SqlConnection connection = new(builder.ConnectionString);
                SqlException ex = Assert.Throws<SqlException>(() => connection.Open());
                Assert.Contains("Error Locating Server/Instance Specified", ex.Message);
            }
        }

#if !NETFRAMEWORK
        [ConditionalFact(nameof(IsSPNPortNumberTestForTCP))]
        public static void SPNTestForTCPMustReturnPortNumber()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            SqlConnectionStringBuilder builder = new(connectionString);

            int port = GetNamedInstancePortNumberFromSqlBrowser(connectionString);
            Assert.True(port > 0, "Named instance must have a valid port number.");
            builder.DataSource = $"{builder.DataSource},{port}";

            PortNumberInSPNTest(connectionString: builder.ConnectionString, expectedPortNumber: port);
        }

        [ConditionalFact(nameof(IsSPNPortNumberTestForNP))]
        public static void SPNTestForNPMustReturnNamedInstance()
        {
            string connectionString = DataTestUtility.NPConnectionString;
            SqlConnectionStringBuilder builder = new(connectionString);

            DataTestUtility.ParseDataSource(builder.DataSource, out _, out _, out string instanceName);

            Assert.True(!string.IsNullOrEmpty(instanceName), "Instance name must be included in data source.");
            PortNumberInSPNTest(connectionString: builder.ConnectionString, expectedInstanceName: instanceName.ToUpper());
        }
#endif

        private static void PortNumberInSPNTest(string connectionString, int expectedPortNumber = 0, string expectedInstanceName = null)
        {
            if (DataTestUtility.IsIntegratedSecuritySetup())
            {
                string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection" };
                connectionString = DataTestUtility.RemoveKeysInConnStr(connectionString, removeKeys) + $"Integrated Security=true";
            }

            SqlConnectionStringBuilder builder = new(connectionString);

            string hostname = "";
            string instanceName = "";

            DataTestUtility.ParseDataSource(builder.DataSource, out hostname, out _, out instanceName);

            Assert.False(string.IsNullOrEmpty(hostname), "Hostname must be included in the data source.");
            Assert.False(string.IsNullOrEmpty(instanceName), "Instance name must be included in the data source.");

            using (SqlConnection connection = new(builder.ConnectionString))
            {
                connection.Open();

                string spnInfo = GetSPNInfo(builder.DataSource, instanceName);
                if (expectedPortNumber > 0)
                {
                    Assert.Matches(@"MSSQLSvc\/.*:[\d]", spnInfo);
                    string[] spnStrs = spnInfo.Split(':');
                    int portInSPN = 0;
                    if (spnStrs.Length > 1)
                    {
                        int.TryParse(spnStrs[1], out portInSPN);
                    }
                    Assert.Equal(expectedPortNumber, portInSPN);
                }
                else
                {
                    string[] spnStrs = spnInfo.Split(':');
                    Assert.Equal(expectedInstanceName, spnStrs[1].ToUpper());
                }
            }
        }

        private static string GetSPNInfo(string dataSource, string inInstanceName)
        {
            Assembly sqlConnectionAssembly = Assembly.GetAssembly(typeof(SqlConnection));

            Type sniProxyType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SNI.SNIProxy");
            Type ssrpType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SNI.SSRP");
            Type dataSourceType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SNI.DataSource");
            Type timeoutTimerType = sqlConnectionAssembly.GetType("Microsoft.Data.ProviderBase.TimeoutTimer");

            Type[] dataSourceConstructorTypesArray = new Type[] { typeof(string) };

            Type[] getSqlServerSPNsTypesArray = new Type[] { dataSourceType, typeof(string) };

            Type[] getPortByInstanceNameTypesArray = new Type[] { typeof(string), typeof(string), timeoutTimerType, typeof(bool), typeof(Microsoft.Data.SqlClient.SqlConnectionIPAddressPreference) };

            Type[] startSecondsTimeoutTypesArray = new Type[] { typeof(int) };

            ConstructorInfo sniProxyConstructor = sniProxyType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);
            ConstructorInfo SSRPConstructor = ssrpType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);
            ConstructorInfo dataSourceConstructor = dataSourceType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, dataSourceConstructorTypesArray, null);
            ConstructorInfo timeoutTimerConstructor = timeoutTimerType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);

            object sniProxyObj = sniProxyConstructor.Invoke(new object[] { });

            object dataSourceObj = dataSourceConstructor.Invoke(new object[] { dataSource });

            object ssrpObj = SSRPConstructor.Invoke(new object[] { });

            object timeoutTimerObj = timeoutTimerConstructor.Invoke(new object[] { });

            MethodInfo startSecondsTimeoutInfo = timeoutTimerObj.GetType().GetMethod("StartSecondsTimeout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, startSecondsTimeoutTypesArray, null);

            timeoutTimerObj = startSecondsTimeoutInfo.Invoke(dataSourceObj, new object[] { 30 });

            MethodInfo parseServerNameInfo = dataSourceObj.GetType().GetMethod("ParseServerName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, dataSourceConstructorTypesArray, null);
            object dataSrcInfo = parseServerNameInfo.Invoke(dataSourceObj, new object[] { dataSource });

            MethodInfo getPortByInstanceNameInfo = ssrpObj.GetType().GetMethod("GetPortByInstanceName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, getPortByInstanceNameTypesArray, null);

            PropertyInfo serverInfo = dataSrcInfo.GetType().GetProperty("ServerName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string serverName = serverInfo.GetValue(dataSrcInfo, null).ToString();
            // Set the instance name from the data source
            PropertyInfo instanceNameToSetInfo = dataSrcInfo.GetType().GetProperty("InstanceName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            instanceNameToSetInfo.SetValue(dataSrcInfo, inInstanceName, null);
            // Ensure that the instance name is set
            PropertyInfo instanceNameInfo = dataSrcInfo.GetType().GetProperty("InstanceName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string instanceName = instanceNameInfo.GetValue(dataSrcInfo, null).ToString().ToUpper();

            object port = getPortByInstanceNameInfo.Invoke(ssrpObj, parameters: new object[] { serverName, instanceName, timeoutTimerObj, false, 0 });

            PropertyInfo resolvedPortInfo = dataSrcInfo.GetType().GetProperty("ResolvedPort", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            resolvedPortInfo.SetValue(dataSrcInfo, (int)port, null);

            string serverSPN = "";
            MethodInfo getSqlServerSPNs = sniProxyObj.GetType().GetMethod("GetSqlServerSPNs", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, getSqlServerSPNsTypesArray, null);

            object resolvedSpns = getSqlServerSPNs.Invoke(sniProxyObj, new object[] { dataSrcInfo, serverSPN });

            string spnInfo = (string)resolvedSpns.GetType().GetProperty("Primary", BindingFlags.Instance | BindingFlags.Public).GetValue(resolvedSpns);

            return spnInfo;
        }

        private static bool IsSPNPortNumberTestForTCP()
        {
            return (IsInstanceNameValid(DataTestUtility.TCPConnectionString)
                 && DataTestUtility.IsUsingManagedSNI()
                 && DataTestUtility.IsNotAzureServer()
                 && DataTestUtility.IsNotAzureSynapse());
        }

        private static bool IsSPNPortNumberTestForNP()
        {
            return (IsInstanceNameValid(DataTestUtility.NPConnectionString)
                 && DataTestUtility.IsUsingManagedSNI()
                 && DataTestUtility.IsNotAzureServer()
                 && DataTestUtility.IsNotAzureSynapse());
        }
        private static bool IsInstanceNameValid(string connectionString)
        {
            string instanceName = "";

            SqlConnectionStringBuilder builder = new(connectionString);

            bool isDataSourceValid = DataTestUtility.ParseDataSource(builder.DataSource, out _, out _, out instanceName);

            return isDataSourceValid && !string.IsNullOrWhiteSpace(instanceName);
        }

        private static bool IsBrowserAlive(string browserHostname)
        {
            const byte ClntUcastEx = 0x03;

            byte[] responsePacket = QueryBrowser(browserHostname, new byte[] { ClntUcastEx });
            return responsePacket != null && responsePacket.Length > 0;
        }

        private static bool IsValidInstance(string browserHostName, string instanceName)
        {
            byte[] request = CreateInstanceInfoRequest(instanceName);
            byte[] response = QueryBrowser(browserHostName, request);
            return response != null && response.Length > 0;
        }

        private static int GetNamedInstancePortNumberFromSqlBrowser(string connectionString)
        {
            SqlConnectionStringBuilder builder = new(connectionString);

            string hostname = "";
            string instanceName = "";
            int port = 0;

            bool isDataSourceValid = DataTestUtility.ParseDataSource(builder.DataSource, out hostname, out _, out instanceName);
            Assert.True(isDataSourceValid, "DataSource is invalid");

            bool isBrowserRunning = IsBrowserAlive(hostname);
            Assert.True(isBrowserRunning, "Browser service is not running.");

            bool isInstanceExisting = IsValidInstance(hostname, instanceName);
            Assert.True(isInstanceExisting, "Instance name is invalid.");

            if (isDataSourceValid && isBrowserRunning && isInstanceExisting)
            {
                byte[] request = CreateInstanceInfoRequest(instanceName);
                byte[] response = QueryBrowser(hostname, request);

                string serverMessage = Encoding.ASCII.GetString(response, 3, response.Length - 3);

                string[] elements = serverMessage.Split(SemicolonSeparator);
                int tcpIndex = Array.IndexOf(elements, "tcp");
                if (tcpIndex < 0 || tcpIndex == elements.Length - 1)
                {
                    throw new SocketException();
                }

                port = (int)ushort.Parse(elements[tcpIndex + 1]);
            }

            return port;
        }

        private static byte[] QueryBrowser(string browserHostname, byte[] requestPacket)
        {
            const int DefaultBrowserPort = 1434;
            const int sendTimeout = 1000;
            const int receiveTimeout = 1000;
            byte[] responsePacket = null;
            using (UdpClient client = new(AddressFamily.InterNetwork))
            {
                try
                {
                    Task<int> sendTask = client.SendAsync(requestPacket, requestPacket.Length, browserHostname, DefaultBrowserPort);
                    Task<UdpReceiveResult> receiveTask = null;
                    if (sendTask.Wait(sendTimeout) && (receiveTask = client.ReceiveAsync()).Wait(receiveTimeout))
                    {
                        responsePacket = receiveTask.Result.Buffer;
                    }
                }
                catch { }
            }

            return responsePacket;
        }

        private static byte[] CreateInstanceInfoRequest(string instanceName)
        {
            const byte ClntUcastInst = 0x04;
            instanceName += char.MinValue;
            int byteCount = Encoding.ASCII.GetByteCount(instanceName);

            byte[] requestPacket = new byte[byteCount + 1];
            requestPacket[0] = ClntUcastInst;
            Encoding.ASCII.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 1);

            return requestPacket;
        }
    }
}
