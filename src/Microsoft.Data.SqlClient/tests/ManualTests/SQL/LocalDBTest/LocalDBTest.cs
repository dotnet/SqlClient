// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class LocalDBTest
    {
        private static bool IsLocalDBEnvironmentSet() => DataTestUtility.IsLocalDBInstalled();
        private static bool IsNativeSNI() => DataTestUtility.IsUsingNativeSNI();
        private static readonly string s_localDbConnectionString = @$"server=(localdb)\{DataTestUtility.LocalDbAppName}";

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBConnectionTest()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(@$"server=(localdb)\{DataTestUtility.LocalDbAppName}");
            builder.IntegratedSecurity = true;
            builder.ConnectTimeout = 2;
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBMarsTest()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(@$"server=(localdb)\{DataTestUtility.LocalDbAppName}");
            builder.IntegratedSecurity = true;
            builder.MultipleActiveResultSets = true;
            builder.ConnectTimeout = 2;
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void InvalidDBTest()
        {
            using (var connection = new SqlConnection(@$"server=(localdb)\{DataTestUtility.LocalDbAppName};Database=DOES_NOT_EXIST;Pooling=false;"))
            {
                DataTestUtility.AssertThrowsWrapper<SqlException>(() => connection.Open());
            }
        }

        #region Failures
        // ToDo: After adding shared memory support on managed SNI, the IsNativeSNI could be taken out
        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet), nameof(IsNativeSNI))]
        [InlineData("lpc:")]
        public static void SharedMemoryAndSqlLocalDbConnectionTest(string prefix)
        {
            SqlConnectionStringBuilder stringBuilder = new(s_localDbConnectionString);
            stringBuilder.DataSource = prefix + stringBuilder.DataSource;
            SqlException ex = Assert.Throws<SqlException>(() => ConnectionTest(stringBuilder.ConnectionString));
            Assert.Contains("A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: SQL Network Interfaces, error: 41 - Cannot open a Shared Memory connection to a remote SQL server)", ex.Message);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
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

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
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
            using (SqlConnection connection = new SqlConnection(connString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("SELECT @@SERVERNAME", connection))
                {
                    var result = command.ExecuteScalar();
                    Assert.NotNull(result);
                }
            }
        }
    }
}
