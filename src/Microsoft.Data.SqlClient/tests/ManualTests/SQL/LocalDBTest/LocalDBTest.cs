// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class LocalDBTest
    {
        private static bool IsLocalDBEnvironmentSet() => DataTestUtility.IsLocalDBInstalled();

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet))]
        [MemberData(nameof(LocalDbSourceProvider))]
        public static void LocalDBConnectionTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new(connectionString)
            {
                IntegratedSecurity = true,
                ConnectTimeout = 2,
                Encrypt = false
            };
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet))]
        [MemberData(nameof(LocalDbSourceProvider))]
        public static void LocalDBMarsTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString)
            {
                IntegratedSecurity = true,
                MultipleActiveResultSets = true,
                ConnectTimeout = 2,
                Encrypt = false
            };
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet))]
        [MemberData(nameof(LocalDbSourceProvider))]
        public static void InvalidDBTest(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            DataTestUtility.AssertThrowsWrapper<SqlException>(() => connection.Open());
        }

        private static void OpenConnection(string connString)
        {
            using SqlConnection connection = new(connString);
            connection.Open();
            using SqlCommand command = new SqlCommand("SELECT @@SERVERNAME", connection);
            var result = command.ExecuteScalar();
            Assert.NotNull(result);
        }

        public static IEnumerable<object[]> LocalDbSourceProvider()
        {
            return (IEnumerable<object[]>)DataTestUtility.LocalDbDataSources;
        }
    }
}
