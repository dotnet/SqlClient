// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks comparing query execution with MARS enabled vs disabled.
    /// Reproduces issues #422 and #1283.
    /// </summary>
    public class MarsOverheadRunner : BaseRunner
    {
        private static long s_rowCount;
        private Table _table;
        private string _query;

        /// <summary>
        /// Whether MARS is enabled on the connection string.
        /// </summary>
        [Params(true, false)]
        public bool MARS { get; set; }

        private string ConnectionString => s_config.ConnectionString + $";MultipleActiveResultSets={MARS}";

        [GlobalSetup]
        public void Setup()
        {
            s_rowCount = s_config.Benchmarks.MarsOverheadRunnerConfig.RowCount;

            using var conn = new SqlConnection(s_config.ConnectionString);
            conn.Open();

            _table = TablePatterns.TableAll25Columns(s_datatypes, nameof(MarsOverheadRunner))
                .CreateTable(conn)
                .InsertBulkRows(s_rowCount, conn);

            _query = $"SELECT * FROM {_table.Name}";
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(s_config.ConnectionString);
            conn.Open();
            _table.DropTable(conn);
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ExecuteReaderWithMars()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new SqlCommand(_query, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task ExecuteReaderAsyncWithMars()
        {
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(_query, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            { }
        }
    }
}
