// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class LocalDBTest
    {
        private static bool IsLocalDBEnvironmentSet() => DataTestUtility.IsLocalDBInstalled();

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public static void LocalDBConnectionTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            builder.IntegratedSecurity = true;
            builder.ConnectTimeout = 2;
            builder.Encrypt = false;
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public static void LocalDBMarsTest(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            builder.IntegratedSecurity = true;
            builder.MultipleActiveResultSets = true;
            builder.ConnectTimeout = 2;
            builder.Encrypt = false;
            OpenConnection(builder.ConnectionString);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Uap)] // No Registry support on UAP
        [ConditionalTheory(nameof(IsLocalDBEnvironmentSet))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public static void InvalidDBTest(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                DataTestUtility.AssertThrowsWrapper<SqlException>(() => connection.Open());
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
    public class ConnectionStringsProvider : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (var cnnString in DataTestUtility.LocalDbConnectionStrings)
            {
                yield return new object[] { cnnString };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
