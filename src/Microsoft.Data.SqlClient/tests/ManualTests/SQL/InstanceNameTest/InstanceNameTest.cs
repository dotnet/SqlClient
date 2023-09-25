// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Pipes;
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

        [PlatformSpecific(TestPlatforms.Windows)]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("")]
        [InlineData("MSSQLSERVER02")]
        public static void NamedPipeInstanceNormalizedPipePathTest(string instance)
        {
            var instancePrefix = "MSSQL$";
            var pathSeparator = "\\";
            var defaultPipeName = "sql\\sqlquery";

            SqlConnectionStringBuilder builder = new(DataTestUtility.NPConnectionString); 

            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out string instanceName));
            instanceName = instance;

            // Mimic the SNIProxy.InferNamedPipesInformation's logic to initialize the PipeName.  It is private so can not be used here.
            string pipeName = $"{defaultPipeName}";
            if (instanceName != string.Empty)
            {
                // This is how InferNamedPipesInformation build the pipeName when there's an instance provided. 
                pipeName = $"{instancePrefix}{instanceName.ToUpper()}{pathSeparator}{defaultPipeName}";
            }

            NamedPipeClientStream pipeStream = new NamedPipeClientStream(
                    hostname,
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            string normalizedPipePath = string.Empty;

            try
            {
                // use m_normalizedPipePath as the field name
                normalizedPipePath = pipeStream
                    .GetType()
                    .GetField("m_normalizedPipePath", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(pipeStream).ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                // if not found, then use _normalizedPipePath as the field name
                if (normalizedPipePath == string.Empty)
                    normalizedPipePath = pipeStream
                        .GetType()
                        .GetField("_normalizedPipePath", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(pipeStream).ToString();
            }


            // Check if the normalized pipe path parsed by NamedPipeClientStream object from supplied 
            // host and pipename has a valid format
            if (normalizedPipePath != string.Empty)
            {
                if (instanceName != string.Empty )
                {
                    // Secondary NamedPipe Instance normalized pipe path format check
                    Assert.Matches(@"\\\\.*\\pipe\\MSSQL\$.*\\sql\\sqlquery", normalizedPipePath);
                }
                else
                {
                    // Default NamedPipe Instance normalized pipe path format check
                    Assert.Matches(@"\\\\.*\\pipe\\sql\\sqlquery", normalizedPipePath);
                }
            }
            else
            {
                Assert.Fail("Unable to extract NormalizedPipePath using reflection.");
            }
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
