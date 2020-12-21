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
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBConnectionTest()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(@"server=(localdb)\MSSQLLocalDB");
            builder.IntegratedSecurity = true;
            builder.ConnectTimeout = 2;
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBMarsTest()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(@"server=(localdb)\MSSQLLocalDB;");
            builder.IntegratedSecurity = true;
            builder.MultipleActiveResultSets = true;
            builder.ConnectTimeout = 2;
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void InvalidDBTest()
        {
            using (var connection = new SqlConnection(@"Data Source=(localdb)\MSSQLLOCALDB;Database=DOES_NOT_EXIST;Pooling=false;"))
            {
                DataTestUtility.AssertThrowsWrapper<SqlException>(() => connection.Open());
            }
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalFact(nameof(IsLocalDBEnvironmentSet))]
        public static void LocalDBLeadingWhiteSpacesTest()
        {
            string LocalDBString = @"(localdb)\MSSQLLocalDB";
           // \u0020 is space unicode
           // \u000A is line feed unicode
            var DataSourcesWithLeadingWhiteSpaces = new string[] { " ", "    ", "\n", "\t", "\u0020", "\u000A"};
            foreach (var whitespace in DataSourcesWithLeadingWhiteSpaces)
            {
                var builder = new SqlConnectionStringBuilder()
                {
                    DataSource = $"{whitespace}{LocalDBString}",
                    IntegratedSecurity = true
                };
                OpenConnection(builder.ConnectionString);
            }
            using (var conn = new SqlConnection(@"server=   (localdb)\MSSQLLOCALDB;Integrated Security=true;"))
            {
                conn.Open();
                Assert.Equal("Open", conn.State.ToString());
            }
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
