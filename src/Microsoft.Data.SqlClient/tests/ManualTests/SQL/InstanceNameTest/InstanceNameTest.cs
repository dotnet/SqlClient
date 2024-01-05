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

#if NETCOREAPP
        [ActiveIssue("27981")] // DataSource.InferNamedPipesInformation is not initializing InstanceName field
        [ConditionalTheory(nameof(IsSPNPortNumberTestForNP))]
        [InlineData("")]
        [InlineData("44444")] // Named Instance Sql Server Port will be setup in the pipeline to 44444 as well
        public static void PortNumberInSPNTestForNP(string port)
        {
            string connectionString = DataTestUtility.NPConnectionString;
            SqlConnectionStringBuilder builder = new(connectionString);

            if (!string.IsNullOrWhiteSpace(port))
                builder.DataSource = $"{builder.DataSource},{port}";

            PortNumberInSPNTest(builder.ConnectionString);
        }

        [ConditionalTheory(nameof(IsSPNPortNumberTestForTCP))]
        [InlineData("")]
        [InlineData("44444")] // Named Instance Sql Server Port will be setup in the pipeline to 44444 as well
        public static void PortNumberInSPNTestForTCP(string port)
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            SqlConnectionStringBuilder builder = new(connectionString);

            if (!string.IsNullOrWhiteSpace(port))
                builder.DataSource = $"{builder.DataSource},{port}";

            PortNumberInSPNTest(builder.ConnectionString);
        }
#endif

        private static void PortNumberInSPNTest(string connectionString)
        {
            // If config.json.Supports IntegratedSecurity = true, replace all keys defined below with Integrated Security=true 
            if (DataTestUtility.IsIntegratedSecuritySetup())
            {
                string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection" };
                connectionString = DataTestUtility.RemoveKeysInConnStr(connectionString, removeKeys) + $"Integrated Security=true";
            }

            SqlConnectionStringBuilder builder = new(connectionString);

            string hostname = "";
            string instanceName = "";
            int port = -1;

            // Named pipe protocol data source does not support port number
            string dataSource = builder.DataSource.ToUpper();
            if ((dataSource.Contains(@"\MSSQL$") && builder.DataSource.ToUpper().Contains(@"\SQL\QUERY") && dataSource.Contains(",")) ||
               (dataSource.Contains(@"NP:") && dataSource.Contains(",")))
            {
                port = -2;
            }
            Assert.False(port == -2, "Named pipe protocol in data source does not support port number.");

            DataTestUtility.ParseDataSource(builder.DataSource, out hostname, out port, out instanceName);
            
            Assert.False(string.IsNullOrEmpty(hostname), "Hostname must be included in the data source.");
            Assert.False(string.IsNullOrEmpty(instanceName), "Instance name must be included in the data source.");

            bool isBrowserRunning = IsBrowserAlive(hostname);
            Assert.True(isBrowserRunning, "Browser service is not running.");

            bool isInstanceExisting = IsValidInstance(hostname, instanceName);
            Assert.True(isInstanceExisting, "Instance name is invalid.");

            if (isBrowserRunning && isInstanceExisting)
            {
                // Create a connection object to ensure SPN info is available via reflection
                SqlConnection connection = new(builder.ConnectionString);
                connection.Open();

                // Get the SPN info using reflection
                string spnInfo = GetSPNInfo(builder.DataSource);

                // The expected output to validate is supposed to be in the format "MSSQLSvc/machine.domain.tld:spnPort".
                // So, we want to get the port number from the SPN and ensure it is a valid port number.
                string[] spnStrs = spnInfo.Split(':');
                int portInSPN = 0;
                if (spnStrs.Length > 1)
                {
                    int.TryParse(spnStrs[1], out portInSPN);
                }
                Assert.True(portInSPN > 0, "The expected SPN must include a valid port number.");

                connection.Close();
            }
        }

        private static string GetSPNInfo(string dataSource)
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

            // Instantiate dataSource 
            object dataSourceObj = dataSourceCtor.Invoke(new object[] { dataSource });

            // Instantiate SSRP
            object ssrpObj = SSRPCtor.Invoke(new object[] { });

            // Instantiate TimeoutTimer
            object timeoutTimerObj = timeoutTimerCtor.Invoke(new object[] { });

            // Get TimeoutTimer.StartSecondsTimeout Method
            MethodInfo startSecondsTimeout = timeoutTimerObj.GetType().GetMethod("StartSecondsTimeout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, startSecondsTimeoutTypesArray, null);
            
            // Create a timeoutTimer that expires in 30 seconds
            timeoutTimerObj = startSecondsTimeout.Invoke(dataSourceObj, new object[] { 30 });

            // Parse the dataSource to separate the server name and instance name
            MethodInfo ParseServerName = dataSourceObj.GetType().GetMethod("ParseServerName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, dataSourceConstructorTypesArray, null);
            object dataSrcInfo = ParseServerName.Invoke(dataSourceObj, new object[] { dataSource });

            // Get the GetPortByInstanceName method of SSRP
            MethodInfo getPortByInstanceName = ssrpObj.GetType().GetMethod("GetPortByInstanceName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, getPortByInstanceNameTypesArray, null);

            // Get the server name
            PropertyInfo serverInfo = dataSrcInfo.GetType().GetProperty("ServerName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string serverName = serverInfo.GetValue(dataSrcInfo, null).ToString();

            // Get the instance name
            PropertyInfo instanceNameInfo = dataSrcInfo.GetType().GetProperty("InstanceName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string instanceName = instanceNameInfo.GetValue(dataSrcInfo, null).ToString();

            // Get the port number using the GetPortByInstanceName method of SSRP
            object port = getPortByInstanceName.Invoke(ssrpObj, parameters: new object[] { serverName, instanceName, timeoutTimerObj, false, 0 });

            // Set the resolved port property of dataSource
            PropertyInfo resolvedPortInfo = dataSrcInfo.GetType().GetProperty("ResolvedPort", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            resolvedPortInfo.SetValue(dataSrcInfo, (int)port, null);

            // Prepare the GetSqlServerSPNs method
            string serverSPN = "";
            MethodInfo getSqlServerSPNs = sniProxy.GetType().GetMethod("GetSqlServerSPNs", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, getSqlServerSPNsTypesArray, null);

            // Finally call GetSqlServerSPNs
            byte[][] result = (byte[][])getSqlServerSPNs.Invoke(sniProxy, new object[] { dataSrcInfo, serverSPN });

            // Example result: MSSQLSvc/machine.domain.tld:port"
            string spnInfo = Encoding.Unicode.GetString(result[0]);

            return spnInfo;
        }

        private static bool IsSPNPortNumberTestForTCP()
        {
            return (IsInstanceNameValid(DataTestUtility.TCPConnectionString)
                 && DataTestUtility.AreConnStringsSetup()
                 && DataTestUtility.IsUsingManagedSNI()
                 && DataTestUtility.IsNotAzureServer()
                 && DataTestUtility.IsNotAzureSynapse());
        }

        private static bool IsSPNPortNumberTestForNP()
        {
            return (IsInstanceNameValid(DataTestUtility.NPConnectionString)
                 && DataTestUtility.AreConnStringsSetup()
                 && DataTestUtility.IsUsingManagedSNI()
                 && DataTestUtility.IsNotAzureServer()
                 && DataTestUtility.IsNotAzureSynapse());
        }

        private static bool IsInstanceNameValid(string connectionString)
        {
            string hostname = "";
            string instanceName = "";
            int port = -1;

            SqlConnectionStringBuilder builder = new(connectionString);
            
            DataTestUtility.ParseDataSource(builder.DataSource, out hostname, out port, out instanceName);

            return !string.IsNullOrWhiteSpace(instanceName);
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
