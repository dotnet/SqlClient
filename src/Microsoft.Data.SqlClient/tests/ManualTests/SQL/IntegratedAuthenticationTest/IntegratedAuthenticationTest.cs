// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class IntegratedAuthenticationTest
    {
        private static bool IsIntegratedSecurityEnvironmentSet() => DataTestUtility.IsIntegratedSecuritySetup();

        private static bool AreConnectionStringsSetup() => DataTestUtility.AreConnStringsSetup();

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTestWithConnectionPooling()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            builder.Pooling = true;
            TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString);
        }

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTestWithOutConnectionPooling()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            builder.Pooling = false;
            TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString);
        }

        [ActiveIssue(21707)]
        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTest_InvalidServerSPN()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            builder.ServerSPN = "InvalidServerSPN";
            SqlException ex = Assert.Throws<SqlException>(() => TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString));
            Assert.Contains("generate SSPI context.", ex.Message);
        }

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTest_ServerSPN()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out int port, out string instanceName));
            // Build the SPN for the server we are connecting to
            builder.ServerSPN = $"MSSQLSvc/{DataTestUtility.GetMachineFQDN(hostname)}";
            if (!string.IsNullOrWhiteSpace(instanceName))
            {
                builder.ServerSPN += ":" + instanceName;
            }
            TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString);
        }

        private static void TryOpenConnectionWithIntegratedAuthentication(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
            }
        }
    }
}
