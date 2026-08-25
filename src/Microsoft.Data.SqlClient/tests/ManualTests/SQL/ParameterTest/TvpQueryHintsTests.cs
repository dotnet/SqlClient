// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Creates the table type and stored procedure used by <see cref="TvpQueryHintsTests"/> exactly
    /// once for the whole test class.
    /// </summary>
    /// <remarks>
    /// Every test in the class needs an identically shaped table type and procedure, so creating them
    /// per test only multiplies the amount of DDL issued against the (shared) test database. On Azure
    /// SQL Database the resulting schema-modification lock contention was enough to push
    /// CREATE/DROP TYPE and CREATE/DROP PROCEDURE past the default 30 second command timeout, which
    /// showed up as sporadic "Execution Timeout Expired" failures in the manual test legs. Sharing a
    /// single type/procedure via a class fixture cuts the DDL statement count by 5x, and the extended
    /// command timeout below absorbs the contention that remains.
    /// </remarks>
    public sealed class TvpQueryHintsFixture : IDisposable
    {
        /// <summary>
        /// Command timeout (in seconds) applied to every command issued on <see cref="Connection"/>.
        /// Deliberately generous: DDL against a shared Azure SQL Database can block for a long time
        /// behind concurrently executing test legs.
        /// </summary>
        private const int CommandTimeoutSeconds = 120;

        private readonly UserDefinedType _tableType;
        private readonly StoredProcedure _procedure;

        public SqlConnection Connection { get; }

        public string ProcedureName => _procedure.Name;

        public TvpQueryHintsFixture()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                CommandTimeout = CommandTimeoutSeconds
            };

            Connection = new SqlConnection(builder.ConnectionString);
            Connection.Open();

            _tableType = new UserDefinedType(Connection, "QHint",
                "TABLE("
                    + " c1 Int DEFAULT -1,"
                    + " c2 NVarChar(40) DEFAULT N'DEFUALT',"
                    + " c3 DateTime DEFAULT '1/1/2006',"
                    + " c4 Int DEFAULT -1)");

            _procedure = new StoredProcedure(Connection, "QHint_Proc",
                $"(@tvp {_tableType.Name} READONLY) AS SELECT TOP(2) * FROM @tvp ORDER BY c1");
        }

        public void Dispose()
        {
            _procedure.Dispose();
            _tableType.Dispose();
            Connection.Dispose();
        }
    }

    /// <summary>
    /// Tests for TVP query hints (sort order, uniqueness, default columns).
    /// </summary>
    [Trait("Set", "3")]
    public sealed class TvpQueryHintsTests : IClassFixture<TvpQueryHintsFixture>, IDisposable
    {
        private readonly SqlCommand _cmd;
        private readonly SqlParameter _param;

        public TvpQueryHintsTests(TvpQueryHintsFixture fixture)
        {
            _cmd = new SqlCommand(fixture.ProcedureName, fixture.Connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            _param = _cmd.Parameters.Add("@tvp", SqlDbType.Structured);
        }

        public void Dispose() => _cmd.Dispose();

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void SortOrderSimple()
        {
            // Arrange
            List<SqlDataRecord> rows = new();

            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 0),
                new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 1),
                new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
            };

            AddRow(rows, columnMetadata, 0, "Z-value", DateTime.Parse("03/01/2000"), 5);
            AddRow(rows, columnMetadata, 1, "Y-value", DateTime.Parse("02/01/2000"), 6);
            AddRow(rows, columnMetadata, 1, "X-value", DateTime.Parse("01/01/2000"), 7);
            AddRow(rows, columnMetadata, 1, "X-value", DateTime.Parse("04/01/2000"), 8);
            AddRow(rows, columnMetadata, 1, "X-value", DateTime.Parse("04/01/2000"), 4);

            _param.Value = rows;

            // Act
            List<QueryHintResult> results = ExecuteAndGetResults(_cmd);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(0, results[0].C1);
            Assert.Equal("Z-value", results[0].C2);
            Assert.Equal(new DateTime(2000, 3, 1), results[0].C3);
            Assert.Equal(5, results[0].C4);
            Assert.Equal(1, results[1].C1);
            Assert.Equal("X-value", results[1].C2);
            Assert.Equal(new DateTime(2000, 1, 1), results[1].C3);
            Assert.Equal(7, results[1].C4);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void SortOrderMixed()
        {
            // Arrange
            List<SqlDataRecord> rows = new();

            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
                new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 0),
                new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 1),
            };

            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            _param.Value = rows;

            // Act
            List<QueryHintResult> results = ExecuteAndGetResults(_cmd);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(4, results[0].C1);
            Assert.Equal("X-value", results[0].C2);
            Assert.Equal(new DateTime(2000, 1, 1), results[0].C3);
            Assert.Equal(3, results[0].C4);
            Assert.Equal(5, results[1].C1);
            Assert.Equal("X-value", results[1].C2);
            Assert.Equal(new DateTime(2000, 3, 1), results[1].C3);
            Assert.Equal(3, results[1].C4);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DefaultColumnOuterSubset()
        {
            // Arrange
            List<SqlDataRecord> rows = new();

            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.NVarChar, 40, false, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.DateTime, false, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
            };

            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            _param.Value = rows;

            // Act
            List<QueryHintResult> results = ExecuteAndGetResults(_cmd);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(-1, results[0].C1);
            Assert.Equal("Y-value", results[0].C2);
            Assert.Equal(new DateTime(2000, 1, 1), results[0].C3);
            Assert.Equal(-1, results[0].C4);
            Assert.Equal(-1, results[1].C1);
            Assert.Equal("Z-value", results[1].C2);
            Assert.Equal(new DateTime(2000, 1, 1), results[1].C3);
            Assert.Equal(-1, results[1].C4);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DefaultColumnMiddleSubset()
        {
            // Arrange
            List<SqlDataRecord> rows = new();

            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, false, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.NVarChar, 40, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.DateTime, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.Int, false, false, SortOrder.Unspecified, -1),
            };

            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            _param.Value = rows;

            // Act
            List<QueryHintResult> results = ExecuteAndGetResults(_cmd);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(4, results[0].C1);
            Assert.Equal("DEFUALT", results[0].C2);
            Assert.Equal(new DateTime(2006, 1, 1), results[0].C3);
            Assert.Equal(3, results[0].C4);
            Assert.Equal(5, results[1].C1);
            Assert.Equal("DEFUALT", results[1].C2);
            Assert.Equal(new DateTime(2006, 1, 1), results[1].C3);
            Assert.Equal(3, results[1].C4);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DefaultColumnAll()
        {
            // Arrange
            List<SqlDataRecord> rows = new();

            SqlMetaData[] columnMetadata = new SqlMetaData[]
            {
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.NVarChar, 40, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.DateTime, true, false, SortOrder.Unspecified, -1),
                new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
            };

            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 1);
            AddRow(rows, columnMetadata, 6, "Z-value", DateTime.Parse("01/01/2000"), 2);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("01/01/2000"), 3);
            AddRow(rows, columnMetadata, 6, "Y-value", DateTime.Parse("02/01/2000"), 3);
            AddRow(rows, columnMetadata, 5, "X-value", DateTime.Parse("03/01/2000"), 3);
            AddRow(rows, columnMetadata, 4, "X-value", DateTime.Parse("01/01/2000"), 3);

            _param.Value = rows;

            // Act
            List<QueryHintResult> results = ExecuteAndGetResults(_cmd);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(-1, results[0].C1);
            Assert.Equal("DEFUALT", results[0].C2);
            Assert.Equal(new DateTime(2006, 1, 1), results[0].C3);
            Assert.Equal(-1, results[0].C4);
            Assert.Equal(-1, results[1].C1);
            Assert.Equal("DEFUALT", results[1].C2);
            Assert.Equal(new DateTime(2006, 1, 1), results[1].C3);
            Assert.Equal(-1, results[1].C4);
        }

        private static void AddRow(List<SqlDataRecord> rows, SqlMetaData[] metadata, params object[] values)
        {
            SqlDataRecord record = new(metadata);
            record.SetValues(values);
            rows.Add(record);
        }

        private static List<QueryHintResult> ExecuteAndGetResults(SqlCommand cmd)
        {
            List<QueryHintResult> results = new();
            using SqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                results.Add(new QueryHintResult(
                    rdr.GetInt32(0),
                    rdr.GetString(1),
                    rdr.GetDateTime(2),
                    rdr.GetInt32(3)));
            }
            return results;
        }

        private sealed class QueryHintResult
        {
            public int C1 { get; }
            public string C2 { get; }
            public DateTime C3 { get; }
            public int C4 { get; }

            public QueryHintResult(int c1, string c2, DateTime c3, int c4)
            {
                C1 = c1;
                C2 = c2;
                C3 = c3;
                C4 = c4;
            }
        }
    }
}
