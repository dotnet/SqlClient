// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class SqlCommandRunner : BaseRunner
    {
        private static SqlConnection s_sqlConnection;
        private Table _table;
        private string _query;

        [GlobalSetup]
        public void Setup()
        {
            s_sqlConnection = new(s_config.ConnectionString);
            s_sqlConnection.Open();

            _table = TablePatterns.TableAll25Columns(s_datatypes, nameof(SqlCommandRunner))
                    .CreateTable(s_sqlConnection)
                    .InsertBulkRows(s_config.Benchmarks.SqlCommandRunnerConfig.RowCount, s_sqlConnection);

            _query = $"SELECT * FROM {_table.Name}";
        }

        [GlobalCleanup]
        public void Dispose()
        {
            _table.DropTable(s_sqlConnection);
            s_sqlConnection.Close();
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ExecuteReader()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            using SqlDataReader reader = sqlCommand.ExecuteReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task ExecuteReaderAsync()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            { }
        }

        [Benchmark]
        public void ExecuteScalar()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            _ = sqlCommand.ExecuteScalar();
        }

        [Benchmark]
        public async Task ExecuteScalarAsync()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            _ = await sqlCommand.ExecuteScalarAsync();
        }

        [Benchmark]
        public void ExecuteNonQuery()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            sqlCommand.ExecuteNonQuery();
        }

        [Benchmark]
        public async Task ExecuteNonQueryAsync()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            await sqlCommand.ExecuteNonQueryAsync();
        }

        [Benchmark]
        public void ExecuteXmlReader()
        {
            using SqlCommand sqlCommand = new(_query + " FOR XML AUTO, BINARY BASE64", s_sqlConnection);
            using XmlReader reader = sqlCommand.ExecuteXmlReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task ExecuteXmlReaderAsync()
        {
            using SqlCommand sqlCommand = new(_query + " FOR XML AUTO, BINARY BASE64", s_sqlConnection);
            using XmlReader reader = await sqlCommand.ExecuteXmlReaderAsync();
            while (await reader.ReadAsync())
            { }
        }

        /// <summary>
        /// Fire 64 concurrent ExecuteReaderAsync + ReadAsync loops, each on
        /// its own pooled connection. The flood of I/O completions and
        /// continuations saturates the thread pool.
        /// </summary>
        [Benchmark]
        public async Task ConcurrentReaderAsyncStarvation()
        {
            const int concurrency = 64;
            var tasks = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var conn = new SqlConnection(s_config.ConnectionString + ";Pooling=true");
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(_query, conn);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) { }
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Sync-over-async pattern: block 64 thread pool threads each doing
        /// sync ExecuteReader + Read. This forces the thread pool to grow
        /// and exposes contention in TdsParser and connection pool layers.
        /// </summary>
        [Benchmark]
        public void ConcurrentReaderSyncStarvation()
        {
            const int concurrency = 64;
            var tasks = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    using var conn = new SqlConnection(s_config.ConnectionString + ";Pooling=true");
                    conn.Open();
                    using var cmd = new SqlCommand(_query, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) { }
                });
            }
            Task.WaitAll(tasks);
        }

        /// <summary>
        /// Mix sync and async readers on overlapping thread pool threads.
        /// Sync readers block threads while async completions queue behind
        /// them, creating worst-case starvation.
        /// </summary>
        [Benchmark]
        public async Task MixedSyncAsyncReaderStarvation()
        {
            const int concurrency = 64;
            var tasks = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                if (i % 2 == 0)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        using var conn = new SqlConnection(s_config.ConnectionString + ";Pooling=true");
                        conn.Open();
                        using var cmd = new SqlCommand(_query, conn);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read()) { }
                    });
                }
                else
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        using var conn = new SqlConnection(s_config.ConnectionString + ";Pooling=true");
                        await conn.OpenAsync();
                        using var cmd = new SqlCommand(_query, conn);
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync()) { }
                    });
                }
            }
            await Task.WhenAll(tasks);
        }
    }
}
