// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class InstanceNameTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ConnectToSQLWithInstanceNameTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);

            bool proceed = true;
            string dataSourceStr = builder.DataSource.Replace("tcp:", "");
            string[] serverNamePartsByBackSlash = dataSourceStr.Split('\\');
            if (!dataSourceStr.Contains(",") && serverNamePartsByBackSlash.Length == 2)
            {
                string hostname = serverNamePartsByBackSlash[0];
                proceed = !string.IsNullOrWhiteSpace(hostname) && IsBrowserAlive(hostname);
            }

            if (proceed)
            {
                using SqlConnection connection = new(builder.ConnectionString);
                connection.Open();
                connection.Close();
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer))]
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


            Assert.True(ParseDataSource(builder.DataSource, out string hostname, out _, out string instanceName));

            if (IsBrowserAlive(hostname) && IsValidInstance(hostname, instanceName))
            {
                builder.DataSource = hostname + "\\" + instanceName;
                try
                {
                    using SqlConnection connection = new(builder.ConnectionString);
                    connection.Open();
                }
                catch (Exception ex)
                {
                    Assert.True(false, "Unexpected connection failure: " + ex.Message);
                }
            }

            builder.ConnectTimeout = 2;
            instanceName = "invalidinstance3456";
            if (!IsValidInstance(hostname, instanceName))
            {
                builder.DataSource = hostname + "\\" + instanceName;
                try
                {
                    using SqlConnection connection = new(builder.ConnectionString);
                    connection.Open();
                    Assert.True(false, "Unexpected connection success against " + instanceName);
                }
                catch (Exception ex)
                {
                    Assert.Contains("Error Locating Server/Instance Specified", ex.Message);
                }
            }
        }

        private static bool ParseDataSource(string dataSource, out string hostname, out int port, out string instanceName)
        {
            hostname = string.Empty;
            port = -1;
            instanceName = string.Empty;

            if (dataSource.Contains(",") && dataSource.Contains("\\"))
                return false;

            if (dataSource.Contains(":"))
            {
                dataSource = dataSource.Substring(dataSource.IndexOf(":") + 1);
            }

            if (dataSource.Contains(","))
            {
                if (!int.TryParse(dataSource.Substring(dataSource.LastIndexOf(",") + 1), out port))
                {
                    return false;
                }
                dataSource = dataSource.Substring(0, dataSource.IndexOf(",") - 1);
            }

            if (dataSource.Contains("\\"))
            {
                instanceName = dataSource.Substring(dataSource.LastIndexOf("\\") + 1);
                dataSource = dataSource.Substring(0, dataSource.LastIndexOf("\\"));
            }

            hostname = dataSource;

            return hostname.Length > 0 && hostname.IndexOfAny(new char[] { '\\', ':', ',' }) == -1;
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
