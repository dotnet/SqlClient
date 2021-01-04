// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [OuterLoop("Takes minutes on some networks")]
    public static class TcpDefaultForAzureTest
    {
        private const string NP = "Named Pipes Provider";
        private const string TCP = "TCP Provider";
        private const string InvalidHostname = "invalidHostname";
        private const string ErrorMessage = "A network-related or instance-specific error occurred while establishing a connection to SQL Server.";

        private static string[] AzureExtensions;
        private static SqlConnectionStringBuilder builder;

        static TcpDefaultForAzureTest()
        {
            AzureExtensions = new string[]
                {
                    ".database.windows.net",
                    ".database.cloudapi.de",
                    ".database.usgovcloudapi.net",
                    ".database.chinacloudapi.cn"
                };

            builder = new SqlConnectionStringBuilder();
            builder.ConnectTimeout = 1;
        }

        // Synapse: Named Pipes not supported for Azure Synapse
        //          Expected protocol TCP Provider in the error message, but received: A network-related or instance-specific error occurred while establishing a connection to SQL Server.
        //          The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. 
        //          (provider: Named Pipes Provider, error: 40 - Could not open a connection to SQL Server)
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void NonAzureNoProtocolConnectionTest()
        {
            builder.DataSource = InvalidHostname;
            CheckConnectionFailure(builder.ConnectionString, DataTestUtility.IsUsingManagedSNI() ? TCP : NP);
        }

        [Fact]
        public static void NonAzureTcpConnectionTest()
        {
            builder.DataSource = "tcp:" + InvalidHostname;
            CheckConnectionFailure(builder.ConnectionString, TCP);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]  // NP NonAzure connection fails correctly on Windows
        public static void NonAzureNpConnectionTest()
        {
            builder.DataSource = "np:\\\\" + InvalidHostname + "\\pipe\\sql\\query";
            CheckConnectionFailure(builder.ConnectionString, NP);
        }

        [Fact]
        public static void AzureNoProtocolConnectionTest()
        {
            foreach (string extension in AzureExtensions)
            {
                builder.DataSource = InvalidHostname + extension;
                CheckConnectionFailure(builder.ConnectionString, TCP);
            }
        }

        [Fact]
        public static void AzureTcpConnectionTest()
        {
            foreach (string extension in AzureExtensions)
            {
                builder.DataSource = "tcp:" + InvalidHostname + extension;
                CheckConnectionFailure(builder.ConnectionString, TCP);
            }
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]  // NP Azure connection fails correctly on Windows
        public static void AzureNpConnectionTest()
        {
            foreach (string extension in AzureExtensions)
            {
                builder.DataSource = "np:\\\\" + InvalidHostname + extension + "\\pipe\\sql\\query";
                CheckConnectionFailure(builder.ConnectionString, NP);
            }
        }

        private static void CheckConnectionFailure(string connString, string protocol)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(connString));

            string errorMessage = Connect(connString);

            Assert.True(errorMessage != null, "Did not receive any error message");
            Assert.True(errorMessage.Contains(ErrorMessage), string.Format("Expected error message {0}, but received: {1}", ErrorMessage, errorMessage));

            if (protocol != null)
            {
                Assert.True(
                    errorMessage.Contains(string.Format("provider: {0}, error", protocol)),
                    string.Format("Expected protocol {0} in the error message, but received: {1}", protocol, errorMessage));
            }
        }

        private static string Connect(string connString)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(connString));

            string errorMessage = null;
            using (SqlConnection connection = new SqlConnection(connString))
            {
                try
                {
                    connection.Open();
                    connection.Close();
                }
                catch (Exception e)
                {
                    errorMessage = e.Message;
                }
            }

            return errorMessage;
        }
    }
}
