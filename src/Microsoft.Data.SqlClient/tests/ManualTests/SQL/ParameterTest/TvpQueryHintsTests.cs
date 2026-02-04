// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for TVP query hints (sort order, uniqueness, default columns).
    /// </summary>
    public sealed class TvpQueryHintsTests : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly string _typeName;
        private readonly string _procName;

        public TvpQueryHintsTests()
        {
            Guid randomizer = Guid.NewGuid();
            _typeName = $"dbo.[QHint_{randomizer}]";
            _procName = $"dbo.[QHint_Proc_{randomizer}]";

            _connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            _connection.Open();

            // Create table type and stored procedure
            string createTypeSql = $"CREATE TYPE {_typeName} AS TABLE("
                + " c1 Int DEFAULT -1,"
                + " c2 NVarChar(40) DEFAULT N'DEFUALT',"
                + " c3 DateTime DEFAULT '1/1/2006',"
                + " c4 Int DEFAULT -1)";

            string createProcSql = $"CREATE PROC {_procName}(@tvp {_typeName} READONLY) AS SELECT TOP(2) * FROM @tvp ORDER BY c1";

            using (SqlCommand cmd = new(createTypeSql, _connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (SqlCommand cmd = new(createProcSql, _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            try
            {
                using SqlCommand cmd = new($"DROP PROC {_procName}; DROP TYPE {_typeName}", _connection);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                _connection?.Dispose();
            }
        }

        /// <summary>
        /// Tests sort order and uniqueness hints with simple ascending/descending order.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void SortOrderUniqueness_SimpleOrder_ReturnsCorrectRows()
        {
            // Arrange: columns with Ascending, Descending, Ascending, Descending sort order
            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 0),
                new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 1),
                new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
            };

            List<SqlDataRecord> rows = new();
            AddRow(rows, columnMetadata, 0, "Z-value", DateTime.Parse("03/01/2000"), 5);
            AddRow(rows, columnMetadata, 1, "Y-value", DateTime.Parse("02/01/2000"), 6);
            AddRow(rows, columnMetadata, 1, "X-value", DateTime.Parse("01/01/2000"), 7);
            AddRow(rows, columnMetadata, 1, "X-value", DateTime.Parse("04/01/2000"), 8);
            AddRow(rows, columnMetadata, 1, "X-value", DateTime.Parse("04/01/2000"), 4);

            // Act
            var results = ExecuteAndGetResults(rows);

            // Assert: TOP(2) ORDER BY c1 should return 2 rows with c1 values 0 and 1
            Assert.Equal(2, results.Count);

            Assert.Equal(0, results[0].C1);
            Assert.Equal("Z-value", results[0].C2);
            Assert.Equal(DateTime.Parse("03/01/2000"), results[0].C3);
            Assert.Equal(5, results[0].C4);

            Assert.Equal(1, results[1].C1);
            Assert.Equal("X-value", results[1].C2);
            Assert.Equal(DateTime.Parse("01/01/2000"), results[1].C3);
            Assert.Equal(7, results[1].C4);
        }

        /// <summary>
        /// Tests sort order and uniqueness hints with mixed column order (sort key order differs from column order).
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void SortOrderUniqueness_MixedOrder_ReturnsCorrectRows()
        {
            // Arrange: sort order indices are 3, 0, 2, 1 (not in column order)
            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
                new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 0),
                new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 1),
            };

            List<SqlDataRecord> rows = new();
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            // Act
            var results = ExecuteAndGetResults(rows);

            // Assert: TOP(2) ORDER BY c1 should return rows with smallest c1 values (4 and 5)
            Assert.Equal(2, results.Count);

            Assert.Equal(4, results[0].C1);
            Assert.Equal("X-value", results[0].C2);
            Assert.Equal(DateTime.Parse("01/01/2000"), results[0].C3);
            Assert.Equal(3, results[0].C4);

            Assert.Equal(5, results[1].C1);
            Assert.Equal("X-value", results[1].C2);
            Assert.Equal(DateTime.Parse("03/01/2000"), results[1].C3);
            Assert.Equal(3, results[1].C4);
        }

        /// <summary>
        /// Tests default column hints with outer subset (first and last columns use defaults).
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DefaultColumn_OuterSubset_UsesDefaultValues()
        {
            // Arrange: c1 and c4 use defaults (useServerDefault = true)
            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.NVarChar, 40, false, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.DateTime, false, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
            };

            List<SqlDataRecord> rows = new();
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            // Act
            var results = ExecuteAndGetResults(rows);

            // Assert: c1 and c4 should use server defaults (-1), c2 and c3 retain provided values
            // Since all c1 values are -1, TOP(2) ORDER BY c1 returns first 2 rows with Y-value and Z-value
            Assert.Equal(2, results.Count);

            Assert.Equal(-1, results[0].C1);
            Assert.Equal("Y-value", results[0].C2);
            Assert.Equal(DateTime.Parse("01/01/2000"), results[0].C3);
            Assert.Equal(-1, results[0].C4);

            Assert.Equal(-1, results[1].C1);
            Assert.Equal("Z-value", results[1].C2);
            Assert.Equal(DateTime.Parse("01/01/2000"), results[1].C3);
            Assert.Equal(-1, results[1].C4);
        }

        /// <summary>
        /// Tests default column hints with middle subset (c2 and c3 use defaults).
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DefaultColumn_MiddleSubset_UsesDefaultValues()
        {
            // Arrange: c2 and c3 use defaults (useServerDefault = true)
            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, false, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.NVarChar, 40, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.DateTime, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.Int, false, false, SortOrder.Unspecified, -1),
            };

            List<SqlDataRecord> rows = new();
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            // Act
            var results = ExecuteAndGetResults(rows);

            // Assert: TOP(2) ORDER BY c1 returns rows with c1=4 and c1=5
            // c2 should be 'DEFUALT' and c3 should be '1/1/2006' (server defaults)
            Assert.Equal(2, results.Count);

            Assert.Equal(4, results[0].C1);
            Assert.Equal("DEFUALT", results[0].C2);
            Assert.Equal(DateTime.Parse("1/1/2006"), results[0].C3);
            Assert.Equal(3, results[0].C4);

            Assert.Equal(5, results[1].C1);
            Assert.Equal("DEFUALT", results[1].C2);
            Assert.Equal(DateTime.Parse("1/1/2006"), results[1].C3);
            Assert.Equal(3, results[1].C4);
        }

        /// <summary>
        /// Tests default column hints when all columns use defaults.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DefaultColumn_AllColumns_UsesDefaultValues()
        {
            // Arrange: all columns use defaults (useServerDefault = true)
            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.NVarChar, 40, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.DateTime, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
            };

            List<SqlDataRecord> rows = new();
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            // Act
            var results = ExecuteAndGetResults(rows);

            // Assert: all columns should use server defaults
            Assert.Equal(2, results.Count);

            Assert.Equal(-1, results[0].C1);
            Assert.Equal("DEFUALT", results[0].C2);
            Assert.Equal(DateTime.Parse("1/1/2006"), results[0].C3);
            Assert.Equal(-1, results[0].C4);

            Assert.Equal(-1, results[1].C1);
            Assert.Equal("DEFUALT", results[1].C2);
            Assert.Equal(DateTime.Parse("1/1/2006"), results[1].C3);
            Assert.Equal(-1, results[1].C4);
        }

        private static void AddRow(List<SqlDataRecord> rows, SqlMetaData[] metadata, int c1, string c2, DateTime c3, int c4)
        {
            var record = new SqlDataRecord(metadata);
            record.SetValues(c1, c2, c3, c4);
            rows.Add(record);
        }

        private List<QueryHintResult> ExecuteAndGetResults(List<SqlDataRecord> rows)
        {
            using SqlCommand cmd = new(_procName, _connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            SqlParameter param = cmd.Parameters.Add("@tvp", SqlDbType.Structured);
            param.Value = rows;

            var results = new List<QueryHintResult>();
            using (SqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    results.Add(new QueryHintResult
                    {
                        C1 = rdr.GetInt32(0),
                        C2 = rdr.GetString(1),
                        C3 = rdr.GetDateTime(2),
                        C4 = rdr.GetInt32(3)
                    });
                }
            }
            return results;
        }

        private class QueryHintResult
        {
            public int C1 { get; set; }
            public string C2 { get; set; }
            public DateTime C3 { get; set; }
            public int C4 { get; set; }
        }
    }
}
