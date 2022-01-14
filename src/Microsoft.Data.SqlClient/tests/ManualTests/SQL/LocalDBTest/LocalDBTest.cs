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
        private static bool IsLocalDBEnvironmentSet() => DataTestUtility.IsLocalDBInstalled();
        private static bool IsLocalDbSharedInstanceSet() => DataTestUtility.IsLocalDbSharedInstanceSetup();
        private static readonly string s_localDbConnectionString = @$"server=(localdb)\{DataTestUtility.LocalDbAppName}";
        private static readonly string[] s_sharedLocalDbInstances = new string[] { @$"server=(localdb)\.\{DataTestUtility.LocalDbSharedInstanceName}", @$"server=(localdb)\." };
        private static readonly string s_badConnectionString = $@"server=(localdb)\{DataTestUtility.LocalDbAppName};Database=DOES_NOT_EXIST;Pooling=false;";
        private static readonly string s_commandPrompt = "cmd.exe";
        private static readonly string s_sqlLocalDbInfo = @$"/c SqlLocalDb info {DataTestUtility.LocalDbAppName}";
        private static readonly string s_startLocalDbCommand = @$"/c SqlLocalDb start {DataTestUtility.LocalDbAppName}";
        private static readonly string s_localDbNamedPipeConnectionString = @$"server={GetLocalDbNamedPipe()}";

        #region LocalDbTests
        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void SqlLocalDbConnectionTest()
        {
            ConnectionTest(s_localDbConnectionString);
            ConnectionTest(s_localDbNamedPipeConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBEncryptionNotSupportedTest()
        {
            // Encryption is not supported by SQL Local DB.
            // But connection should succeed as encryption is disabled by driver.
            ConnectionWithEncryptionTest(s_localDbConnectionString);
            ConnectionWithEncryptionTest(s_localDbNamedPipeConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBMarsTest()
        {
            ConnectionWithMarsTest(s_localDbConnectionString);
            ConnectionWithMarsTest(s_localDbNamedPipeConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void InvalidLocalDBTest()
        {
            using var connection = new SqlConnection(s_badConnectionString);
            DataTestUtility.AssertThrowsWrapper<SqlException>(() => connection.Open());
        }
        #endregion

        #region SharedLocalDb tests
        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
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

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDbSharedInstanceSet))]
        public static void SharedLocalDbMarsTest()
        {
            foreach (string connectionString in s_sharedLocalDbInstances)
            {
                ConnectionWithMarsTest(connectionString);
            }
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDbSharedInstanceSet))]
        public static void SqlLocalDbSharedInstanceConnectionTest()
        {
            foreach (string connectionString in s_sharedLocalDbInstances)
            {
                ConnectionTest(connectionString);
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
                Encrypt = true
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
            string state = ExecuteLocalDBCommandProcess(s_commandPrompt, s_sqlLocalDbInfo, "state");
            while (state.Equals("stopped", StringComparison.InvariantCultureIgnoreCase))
            {
                state = ExecuteLocalDBCommandProcess(s_commandPrompt, s_startLocalDbCommand, "state");
                Thread.Sleep(2000);
            }
            return ExecuteLocalDBCommandProcess(s_commandPrompt, s_sqlLocalDbInfo, "pipeName");
        }

        private static string ExecuteLocalDBCommandProcess(string filename, string arguments, string infoType)
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
            if (infoType.Equals("state"))
            {
                return lines[5].Split(':')[1].Trim();
            }
            else if (infoType.Equals("pipeName"))
            {
                return lines[7].Split(new string[] { "Instance pipe name:" }, StringSplitOptions.None)[1].Trim();
            }
            return null;
        }
    }
}
