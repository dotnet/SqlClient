// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        // Note: This Unit test was tested in a domain-joined VM connecting to a remote
        //       SQL Server using Kerberos in the same domain.
        [ConditionalFact(nameof(IsKerberos))]
        public static void PortNumberInSPNTest()
        {
            string connStr = DataTestUtility.TCPConnectionString;
            // If config.json.SupportsIntegratedSecurity = true, replace all keys defined below with Integrated Security=true 
            if (DataTestUtility.IsIntegratedSecuritySetup())
            {
                string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection" };
                connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys) + $"Integrated Security=true";
            }

            SqlConnectionStringBuilder builder = new(connStr);

            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out string instanceName), "Data source to be parsed must contain a host name and instance name");

            bool condition = IsBrowserAlive(hostname) && IsValidInstance(hostname, instanceName);
            Assert.True(condition, "Browser service is not running or instance name is invalid");

            if (condition)
            {
                using SqlConnection connection = new(builder.ConnectionString);
                connection.Open();
                using SqlCommand command = new("SELECT auth_scheme, local_tcp_port from sys.dm_exec_connections where session_id = @@spid", connection);
                using SqlDataReader reader = command.ExecuteReader();
                Assert.True(reader.Read(), "Expected to receive one row data");
                Assert.Equal("KERBEROS", reader.GetString(0));
                int localTcpPort = reader.GetInt32(1);

                int spnPort = -1;
                string spnInfo = GetSPNInfo(builder.DataSource, out spnPort);

                // sample output to validate = MSSQLSvc/machine.domain.tld:spnPort"
                Assert.Contains($"MSSQLSvc/{hostname}", spnInfo);
                // the local_tcp_port should be the same as the inferred SPN port from instance name
                Assert.Equal(localTcpPort, spnPort);
            }
        }

        private static string GetSPNInfo(string datasource, out int out_port)
        {
            Assembly sqlConnectionAssembly = Assembly.GetAssembly(typeof(SqlConnection));

            // Get all required types using reflection
            Type sniProxyType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SNI.SNIProxy");
            Type ssrpType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SNI.SSRP");
            Type dataSourceType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SNI.DataSource");
            Type timeoutTimerType = sqlConnectionAssembly.GetType("Microsoft.Data.ProviderBase.TimeoutTimer");

            // Used in Datasource constructor param type array 
            Type[] dataSourceConstructorTypesArray = new Type[] { typeof(string) };

            // Used in GetSqlServerSPNs function param types array
            Type[] getSqlServerSPNsTypesArray = new Type[] { dataSourceType, typeof(string) };

            // GetPortByInstanceName parameters array
            Type[] getPortByInstanceNameTypesArray = new Type[] { typeof(string), typeof(string), timeoutTimerType, typeof(bool), typeof(Microsoft.Data.SqlClient.SqlConnectionIPAddressPreference) };

            // TimeoutTimer.StartSecondsTimeout params
            Type[] startSecondsTimeoutTypesArray = new Type[] { typeof(int) };

            // Get all types constructors
            ConstructorInfo sniProxyCtor = sniProxyType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);
            ConstructorInfo SSRPCtor = ssrpType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);
            ConstructorInfo dataSourceCtor = dataSourceType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, dataSourceConstructorTypesArray, null);
            ConstructorInfo timeoutTimerCtor = timeoutTimerType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);

            // Instantiate SNIProxy
            object sniProxy = sniProxyCtor.Invoke(new object[] { });

            // Instantiate datasource 
            object dataSourceObj = dataSourceCtor.Invoke(new object[] { datasource });

            // Instantiate SSRP
            object ssrp = SSRPCtor.Invoke(new object[] { });

            // Instantiate TimeoutTimer
            object timeoutTimer = timeoutTimerCtor.Invoke(new object[] { });

            // Get TimeoutTimer.StartSecondsTimeout Method
            MethodInfo startSecondsTimeout = timeoutTimer.GetType().GetMethod("StartSecondsTimeout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, startSecondsTimeoutTypesArray, null);
            // Create a timeoutTimer that expires in 30 seconds
            timeoutTimer = startSecondsTimeout.Invoke(dataSourceObj, new object[] { 30 });

            // Parse the datasource to separate the server name and instance name
            MethodInfo ParseServerName = dataSourceObj.GetType().GetMethod("ParseServerName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, dataSourceConstructorTypesArray, null);
            object dataSrcInfo = ParseServerName.Invoke(dataSourceObj, new object[] { datasource });

            // Get the GetPortByInstanceName method of SSRP
            MethodInfo getPortByInstanceName = ssrp.GetType().GetMethod("GetPortByInstanceName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, getPortByInstanceNameTypesArray, null);

            // Get the server name
            PropertyInfo serverInfo = dataSrcInfo.GetType().GetProperty("ServerName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string serverName = serverInfo.GetValue(dataSrcInfo, null).ToString();

            // Get the instance name
            PropertyInfo instanceNameInfo = dataSrcInfo.GetType().GetProperty("InstanceName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string instanceName = instanceNameInfo.GetValue(dataSrcInfo, null).ToString();

            // Get the port number using the GetPortByInstanceName method of SSRP
            object port = getPortByInstanceName.Invoke(ssrp, parameters: new object[] { serverName, instanceName, timeoutTimer, false, 0 });

            // Set the resolved port property of datasource
            PropertyInfo resolvedPortInfo = dataSrcInfo.GetType().GetProperty("ResolvedPort", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            resolvedPortInfo.SetValue(dataSrcInfo, (int)port, null);

            // Prepare the GetSqlServerSPNs method
            string serverSPN = "";
            MethodInfo getSqlServerSPNs = sniProxy.GetType().GetMethod("GetSqlServerSPNs", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, getSqlServerSPNsTypesArray, null);

            // Finally call GetSqlServerSPNs
            byte[][] result = (byte[][])getSqlServerSPNs.Invoke(sniProxy, new object[] { dataSrcInfo, serverSPN });

            // Example result: MSSQLSvc/machine.domain.tld:port"
            string spnInfo = Encoding.Unicode.GetString(result[0]);

            out_port = (int)port;

            return spnInfo;
        }

        private static bool IsKerberos()
        {
            return (DataTestUtility.AreConnStringsSetup() 
                 && DataTestUtility.IsNotLocalhost() 
                 && DataTestUtility.IsKerberosTest 
                 && DataTestUtility.IsNotAzureServer() 
                 && DataTestUtility.IsNotAzureSynapse());
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
