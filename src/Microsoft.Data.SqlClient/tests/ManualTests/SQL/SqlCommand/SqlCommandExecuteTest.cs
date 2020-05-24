// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlCommandExecuteTest
    {
        [Theory]
        [MemberData(nameof(GetConnectionStrings))]
        public static void ExecuteReaderAsyncTest(string connectionString)
        {
            int counter = 100;
            while (counter-- > 0)
            {
                ExecuteReaderAsync(connectionString, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        [Theory]
        [MemberData(nameof(GetConnectionStrings))]
        public static void ExecuteScalarAsyncTest(string connectionString)
        {
            int counter = 100;
            while (counter-- > 0)
            {
                ExecuteScalarAsync(connectionString, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        [Theory]
        [MemberData(nameof(GetConnectionStrings))]
        public static void ExecuteNonQueryAsyncTest(string connectionString)
        {
            int counter = 100;
            while (counter-- > 0)
            {
                ExecuteNonQueryAsync(connectionString, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        [Theory]
        [MemberData(nameof(GetConnectionStrings))]
        public static void ExecuteXmlReaderAsyncTest(string connectionString)
        {
            int counter = 100;
            while (counter-- > 0)
            {
                ExecuteXmlReaderAsync(connectionString, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        #region Execute Async
        private static async Task ExecuteReaderAsync(string connectionString, CancellationToken token)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var cmd = GetCommand(connection))
                {
                    var r = await cmd.ExecuteReaderAsync(token);
                    while (await r.ReadAsync(token))
                    {
                        await r.GetFieldValueAsync<string>(0);
                        await r.GetFieldValueAsync<string>(1);
                        await r.GetFieldValueAsync<string>(2);
                    }
                }
            }
        }

        private static async Task ExecuteScalarAsync(string connectionString, CancellationToken token)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var cmd = GetCommand(connection))
                {
                    await cmd.ExecuteScalarAsync(token);
                }
            }
        }

        private static async Task ExecuteNonQueryAsync(string connectionString, CancellationToken token)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var cmd = GetCommand(connection))
                {
                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        private static async Task ExecuteXmlReaderAsync(string connectionString, CancellationToken token)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var cmd = GetCommand(connection))
                {
                    cmd.CommandText += " FOR XML AUTO, XMLDATA";
                    var r = await cmd.ExecuteXmlReaderAsync(token);
                }
            }
        }

        private static SqlCommand GetCommand(SqlConnection cnn)
        {
            string aRecord = "('2455cf1b-ebcf-418d-8cce-88e21e1683e3', 'something', 'updated'),";
            string query = "SELECT * FROM (VALUES"
                + string.Concat(Enumerable.Repeat(aRecord, 200)).Substring(0, (aRecord.Length * 200) - 1)
                + ") tbl_A ([Id], [Name], [State])";
            cnn.Open();
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = query;
                return cmd;
            }
        }
        #endregion

        public static IEnumerable<object[]> GetConnectionStrings()
        {
            SqlConnectionStringBuilder builder;
            foreach (var item in DataTestUtility.ConnectionStrings)
            {
                builder = new SqlConnectionStringBuilder(item)
                {
                    TrustServerCertificate = true,
                    Encrypt = true,
                    MultipleActiveResultSets = false,
                    ConnectTimeout = 10,
                    ConnectRetryCount = 3,
                    ConnectRetryInterval = 10,
                    LoadBalanceTimeout = 60,
                    MaxPoolSize = 10,
                    MinPoolSize = 0
                };
                yield return new object[] { builder.ConnectionString };

                builder.TrustServerCertificate = false;
                builder.Encrypt = false;
                yield return new object[] { builder.ConnectionString };
            }
        }
    }
}
