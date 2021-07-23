// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class SqlBulkCopyRunner : BaseRunner
    {
        private static long s_rowCount;
        private static Table s_srcTable;
        private static Table s_tgtTable;
        private static IDataReader s_reader;
        private const int BatchCount = 1000;

        private static SqlConnection s_sqlConnection;
        private static SqlCommand s_sqlCommand;
        private static SqlDataReader s_sqlReader;

        /// <summary>
        /// Number of columns in tables used for SQL Bulk Copy operation.
        /// </summary>
        [Params(7, 25, 50)]
        public int Columns { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            s_rowCount = s_config.Benchmarks.SqlBulkCopyRunnerConfig.RowCount;
            s_sqlConnection = new(s_config.ConnectionString);
            s_sqlConnection.Open();

            switch (Columns)
            {
                case 7:
                    s_srcTable = TablePatterns.Table7Columns(s_datatypes, "Source_" + nameof(SqlBulkCopyRunner))
                        .CreateTable(s_sqlConnection)
                        .InsertBulkRows(s_rowCount, s_sqlConnection);
                    break;
                case 25:
                    s_srcTable = TablePatterns.TableAll25Columns(s_datatypes, "Source_" + nameof(SqlBulkCopyRunner))
                        .CreateTable(s_sqlConnection)
                        .InsertBulkRows(s_rowCount, s_sqlConnection);
                    break;
                case 50:
                    s_srcTable = TablePatterns.TableX25Columns(Columns, s_datatypes, "Source_" + nameof(SqlBulkCopyRunner))
                        .CreateTable(s_sqlConnection)
                        .InsertBulkRows(s_rowCount, s_sqlConnection);
                    break;
            }

            s_reader = s_srcTable.AsDataTable(s_rowCount).CreateDataReader();
            s_tgtTable = s_srcTable.Clone()
                .CreateTable(s_sqlConnection);

            string cmdText = $"SELECT * FROM {s_srcTable.Name};";
            s_sqlCommand = new SqlCommand(cmdText, s_sqlConnection);
        }

        [IterationSetup]
        public void IterationSetup()
        {
            ResetTargetTable();
            s_reader = s_srcTable.AsDataTable(s_rowCount).CreateDataReader();
            s_sqlReader = s_sqlCommand.ExecuteReader();
        }

        [Benchmark]
        public void BulkCopy_IDataReader()
        {
            using SqlBulkCopy bc = GetBulkCopyWriter(s_tgtTable);
            bc.WriteToServer(s_reader);
        }

        [Benchmark]
        public async Task BulkCopyAsync_IDataReader()
        {
            using SqlBulkCopy bc = GetBulkCopyWriter(s_tgtTable);
            await bc.WriteToServerAsync(s_reader);
        }

        [Benchmark]
        public void BulkCopy_SqlDataReader()
        {
            using SqlBulkCopy bc = GetBulkCopyWriter(s_tgtTable);
            bc.WriteToServer(s_sqlReader);
        }

        [Benchmark]
        public async Task BulkCopyAsync_SqlDataReader()
        {
            using SqlBulkCopy bc = GetBulkCopyWriter(s_tgtTable);
            await bc.WriteToServerAsync(s_sqlReader);
        }

        private SqlBulkCopy GetBulkCopyWriter(Table table) =>
            new(s_config.ConnectionString)
            {
                BatchSize = BatchCount,
                DestinationTableName = table.Name,
                BulkCopyTimeout = 60
            };

        private void ResetTargetTable()
        {
            using SqlConnection con = new(s_config.ConnectionString);
            con.Open();
            using SqlCommand cmd = new($"TRUNCATE TABLE {s_tgtTable.Name};", con);
            cmd.ExecuteNonQuery();
        }

        [IterationCleanup]
        public void SqlDataReaderReset() => s_sqlReader.Dispose();

        [GlobalCleanup]
        public void Cleanup()
        {
            s_sqlCommand.Dispose();
            s_srcTable.DropTable(s_sqlConnection);
            s_tgtTable.DropTable(s_sqlConnection);
            s_sqlConnection.Close();
        }
    }
}
