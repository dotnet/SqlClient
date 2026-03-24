// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks for XML column reads via SequentialAccess at increasing data sizes
    /// to detect O(N²) scaling behavior.
    /// Reproduces issue #1877.
    /// </summary>
    public class SequentialXmlReadRunner : BaseRunner
    {
        private string _tableName;
        private string _connectionString;
        private string _query;

        /// <summary>
        /// Size of the XML data in kilobytes.
        /// </summary>
        [Params(10, 100, 500, 1000)]
        public int XmlSizeKB { get; set; }

        /// <summary>
        /// Whether to use CommandBehavior.SequentialAccess.
        /// </summary>
        [Params(true, false)]
        public bool UseSequentialAccess { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connectionString = s_config.ConnectionString;
            _tableName = $"[perf_SeqXml_{Environment.MachineName}_{Guid.NewGuid():N}]";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var createCmd = new SqlCommand(
                $"CREATE TABLE {_tableName} (Id INT IDENTITY PRIMARY KEY, Data XML)", conn);
            createCmd.ExecuteNonQuery();

            // Generate XML of approximately XmlSizeKB kilobytes
            string xmlData = GenerateXml(XmlSizeKB * 1024);

            using var insertCmd = new SqlCommand(
                $"INSERT INTO {_tableName} (Data) VALUES (@data)", conn);
            insertCmd.Parameters.Add("@data", SqlDbType.Xml).Value = xmlData;
            insertCmd.ExecuteNonQuery();

            _query = $"SELECT Data FROM {_tableName}";
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_tableName}", conn);
            cmd.ExecuteNonQuery();
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ReadXml()
        {
            var behavior = UseSequentialAccess ? CommandBehavior.SequentialAccess : CommandBehavior.Default;

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(_query, conn);
            using var reader = cmd.ExecuteReader(behavior);
            while (reader.Read())
            {
                _ = reader.GetString(0);
            }
        }

        private static string GenerateXml(int targetBytes)
        {
            var sb = new StringBuilder(targetBytes + 256);
            sb.Append("<root>");
            int index = 0;
            while (sb.Length < targetBytes)
            {
                sb.Append($"<item id=\"{index}\">This is element number {index} with some padding data to increase size.</item>");
                index++;
            }
            sb.Append("</root>");
            return sb.ToString();
        }
    }
}
