// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class LocalDBTest
    {
        private enum InfoType
        {
            pipeName,
            state
        }
        private static bool IsLocalDBEnvironmentSet() => DataTestUtility.IsLocalDBInstalled();
        private static bool IsNativeSNI() => DataTestUtility.IsUsingNativeSNI();
        private static bool IsLocalDbSharedInstanceSet() => DataTestUtility.IsLocalDbSharedInstanceSetup();
        private static readonly string s_localDbConnectionString = @$"server=(localdb)\{DataTestUtility.LocalDbAppName}";
        private static readonly string[] s_sharedLocalDbInstances = new string[] { @$"server=(localdb)\.\{DataTestUtility.LocalDbSharedInstanceName}", @$"server=(localdb)\." };
        private static readonly string s_badConnectionString = $@"server=(localdb)\{DataTestUtility.LocalDbAppName};Database=DOES_NOT_EXIST;Pooling=false;";
        private static readonly string s_commandPrompt = "cmd.exe";
        private static readonly string s_sqlLocalDbInfo = @$"/c SqlLocalDb info {DataTestUtility.LocalDbAppName}";
        private static readonly string s_startLocalDbCommand = @$"/c SqlLocalDb start {DataTestUtility.LocalDbAppName}";
        private static readonly string s_localDbNamedPipeConnectionString = @$"server={GetLocalDbNamedPipe()}";


        #region LocalDbTests

        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void SqlLocalDbConnectionTest()
        {
            ConnectionTest(s_localDbConnectionString);
        }

        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBEncryptionNotSupportedTest()
        {
            // Encryption is not supported by SQL Local DB.
            // But connection should succeed as encryption is disabled by driver.
            ConnectionWithEncryptionTest(s_localDbConnectionString);
        }

        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBMarsTest()
        {
            RestartLocalDB();
            ConnectionWithMarsTest(s_localDbConnectionString);
        }

        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void InvalidLocalDBTest()
        {
            using var connection = new SqlConnection(s_badConnectionString);
            DataTestUtility.AssertThrowsWrapper<SqlException>(() => connection.Open());
        }
#endregion

        #region SharedLocalDb tests
        [ConditionalFact(nameof(IsLocalDbSharedInstanceSet))]
        public static void SharedLocalDbEncryptionTest()
        {
            foreach (string connectionString in s_sharedLocalDbInstances)
            {
                // Encryption is not supported by SQL Local DB.
                // But connection should succeed as encryption is disabled by driver.
                ConnectionWithEncryptionTest(connectionString);
            }
        }

        [ConditionalFact(nameof(IsLocalDbSharedInstanceSet))]
        public static void SharedLocalDbMarsTest()
        {
            foreach (string connectionString in s_sharedLocalDbInstances)
            {
                ConnectionWithMarsTest(connectionString);
            }
        }

        [ConditionalFact(nameof(IsLocalDbSharedInstanceSet))]
        public static void SqlLocalDbSharedInstanceConnectionTest()
        {
            foreach (string connectionString in s_sharedLocalDbInstances)
            {
                ConnectionTest(connectionString);
            }
        }
        #endregion


        #region NamedPipeTests

        [Fact]
        [ActiveIssue("20245")] //pending pipeline configuration
        public static void SqlLocalDbNamedPipeConnectionTest()
        {
            ConnectionTest(s_localDbNamedPipeConnectionString);
        }

        [Fact]
        [ActiveIssue("20245")] //pending pipeline configuration
        public static void LocalDBNamedPipeEncryptionNotSupportedTest()
        {
            // Encryption is not supported by SQL Local DB.
            // But connection should succeed as encryption is disabled by driver.
            ConnectionWithEncryptionTest(s_localDbNamedPipeConnectionString);
        }

        [Fact]
        [ActiveIssue("20245")] //pending pipeline configuration
        public static void LocalDBNamepipeMarsTest()
        {
            ConnectionWithMarsTest(s_localDbNamedPipeConnectionString);
        }

        #endregion

        #region Failures
        // ToDo: After adding shared memory support on managed SNI, the IsNativeSNI could be taken out
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet), nameof(IsNativeSNI))]
        [InlineData("lpc:")]
        public static void SharedMemoryAndSqlLocalDbConnectionTest(string prefix)
        {
            SqlConnectionStringBuilder stringBuilder = new(s_localDbConnectionString);
            stringBuilder.DataSource = prefix + stringBuilder.DataSource;
            SqlException ex = Assert.Throws<SqlException>(() => ConnectionTest(stringBuilder.ConnectionString));
            Assert.Contains("A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: SQL Network Interfaces, error: 41 - Cannot open a Shared Memory connection to a remote SQL server)", ex.Message);
        }

        [InlineData("tcp:")]
        [InlineData("np:")]
        [InlineData("undefinded:")]
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet)/*, nameof(IsNativeSNI)*/)]
        public static void PrefixAndSqlLocalDbConnectionTest(string prefix)
        {
            SqlConnectionStringBuilder stringBuilder = new(s_localDbConnectionString);
            stringBuilder.DataSource = prefix + stringBuilder.DataSource;
            SqlException ex = Assert.Throws<SqlException>(() => ConnectionTest(stringBuilder.ConnectionString));
            Assert.Contains("A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: SQL Network Interfaces, error: 26 - Error Locating Server/Instance Specified)", ex.Message);
        }

        [ConditionalFact(nameof(IsLocalDBEnvironmentSet)/*, nameof(IsNativeSNI)*/)]
        public static void InvalidSqlLocalDbConnectionTest()
        {
            SqlConnectionStringBuilder stringBuilder = new(s_localDbConnectionString);
            stringBuilder.DataSource = stringBuilder.DataSource + "Invalid123";
            SqlException ex = Assert.Throws<SqlException>(() => ConnectionTest(stringBuilder.ConnectionString));
            Assert.Contains("A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: SQL Network Interfaces, error: 50 - Local Database Runtime error occurred.", ex.Message);
            if (IsNativeSNI())
            {
                Assert.Contains("The specified LocalDB instance does not exist.", ex.Message);
            }
        }
        #endregion

        private static void ConnectionWithMarsTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new(connectionString)
            {
                IntegratedSecurity = true,
                MultipleActiveResultSets = true,
                ConnectTimeout = 2
            };
            OpenConnection(builder.ConnectionString);
        }

        private static void ConnectionWithEncryptionTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new(connectionString)
            {
                IntegratedSecurity = true,
                ConnectTimeout = 2,
                Encrypt = SqlConnectionEncryptOption.Mandatory
            };
            OpenConnection(builder.ConnectionString);
        }

        private static void ConnectionTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new(connectionString)
            {
                IntegratedSecurity = true,
                ConnectTimeout = 2
            };
            OpenConnection(builder.ConnectionString);
        }

        private static void OpenConnection(string connString)
        {
            using SqlConnection connection = new(connString);
            connection.Open();
            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
            using SqlCommand command = new SqlCommand("SELECT @@SERVERNAME", connection);
            var result = command.ExecuteScalar();
            Assert.NotNull(result);
        }

        private static string GetLocalDbNamedPipe()
        {
            RestartLocalDB();
            string instanceName = ExecuteLocalDBCommandProcess(s_commandPrompt, s_sqlLocalDbInfo, InfoType.pipeName);
            Assert.NotNull(instanceName);
            Assert.NotEmpty(instanceName);
            return instanceName;
        }

        private static void RestartLocalDB()
        {
            string state = ExecuteLocalDBCommandProcess(s_commandPrompt, s_sqlLocalDbInfo, InfoType.state);
            int count = 5;
            while (state.Equals("stopped", StringComparison.InvariantCultureIgnoreCase) && count > 0)
            {
                count--;
                state = ExecuteLocalDBCommandProcess(s_commandPrompt, s_startLocalDbCommand, InfoType.state);
                Thread.Sleep(2000);
            }
            if (state == null || state != "Running")
            {
                throw new LocalDBNotStartedException();
            }
        }
        private static string ExecuteLocalDBCommandProcess(string filename, string arguments, InfoType infoType)
        {
            ProcessStartInfo sInfo = new()
            {
                FileName = filename,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            string[] lines = Process.Start(sInfo).StandardOutput.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            if (arguments == s_startLocalDbCommand)
            {
                Assert.Equal(2, lines.Length);
                sInfo.Arguments = s_sqlLocalDbInfo; //after start check info again
                lines = Process.Start(sInfo).StandardOutput.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            }
            Assert.Equal(9, lines.Length);
            if (infoType.Equals(InfoType.state))
            {
                return lines[5].Split(':')[1].Trim();
            }
            else if (infoType.Equals(InfoType.pipeName))
            {
                return lines[7].Split(new string[] { "Instance pipe name:" }, StringSplitOptions.None)[1].Trim();
            }
            return null;
        }
    }
    class LocalDBNotStartedException : Exception
    {
        public override string Message => "Unable to start LocalDB";
    }
}
