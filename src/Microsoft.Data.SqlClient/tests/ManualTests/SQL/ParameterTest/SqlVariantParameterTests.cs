// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for SQL Variant parameters.
    /// Tests all SqlTypes inside sql_variant to server using sql_variant parameter, SqlBulkCopy, and TVP parameter.
    /// </summary>
    public sealed class SqlVariantParameterTests : IDisposable
    {
        private readonly string _connStr;
        private readonly List<string> _bulkCopyTablesToCleanup = new();
        private readonly List<string> _tvpTypesToCleanup = new();

        public SqlVariantParameterTests()
        {
            _connStr = DataTestUtility.TCPConnectionString;
        }

        public void Dispose()
        {
            // Clean up any tables/types that may have been left behind
            using var conn = new SqlConnection(_connStr);
            conn.Open();
            foreach (var table in _bulkCopyTablesToCleanup)
            {
                try
                {
                    using var cmd = new SqlCommand($"DROP TABLE IF EXISTS {table}", conn);
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }
            foreach (var type in _tvpTypesToCleanup)
            {
                try
                {
                    using var cmd = new SqlCommand($"DROP TYPE IF EXISTS {type}", conn);
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }
        }

        public static IEnumerable<object[]> SqlTypeTestData()
        {
            yield return new object[] { new SqlSingle((float)123.45), "System.Data.SqlTypes.SqlSingle", "real" };
            yield return new object[] { new SqlSingle((double)123.45), "System.Data.SqlTypes.SqlSingle", "real" };
            yield return new object[] { new SqlString("hello"), "System.Data.SqlTypes.SqlString", "nvarchar" };
            yield return new object[] { new SqlDouble(123.45), "System.Data.SqlTypes.SqlDouble", "float" };
            yield return new object[] { new SqlBinary(new byte[] { 0x00, 0x11, 0x22 }), "System.Data.SqlTypes.SqlBinary", "varbinary" };
            yield return new object[] { new SqlGuid(Guid.NewGuid()), "System.Data.SqlTypes.SqlGuid", "uniqueidentifier" };
            yield return new object[] { new SqlBoolean(true), "System.Data.SqlTypes.SqlBoolean", "bit" };
            yield return new object[] { new SqlBoolean(1), "System.Data.SqlTypes.SqlBoolean", "bit" };
            yield return new object[] { new SqlByte(1), "System.Data.SqlTypes.SqlByte", "tinyint" };
            yield return new object[] { new SqlInt16(1), "System.Data.SqlTypes.SqlInt16", "smallint" };
            yield return new object[] { new SqlInt32(1), "System.Data.SqlTypes.SqlInt32", "int" };
            yield return new object[] { new SqlInt64(1), "System.Data.SqlTypes.SqlInt64", "bigint" };
            yield return new object[] { new SqlDecimal(1234.123M), "System.Data.SqlTypes.SqlDecimal", "numeric" };
            yield return new object[] { new SqlDateTime(DateTime.Now), "System.Data.SqlTypes.SqlDateTime", "datetime" };
            yield return new object[] { new SqlMoney(123.123M), "System.Data.SqlTypes.SqlMoney", "money" };
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData))]
        public void SqlType_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // SqlMoney has a known limitation with BulkCopy where it converts to SqlDecimal/numeric.
            // For BulkCopy, we expect SqlDecimal/numeric for SqlMoney values.
            // The TvpTests used to handle this by including an expected exception in the baseline file!
            bool isSqlMoney = paramValue is SqlMoney;
            string bulkCopyExpectedType = isSqlMoney ? "System.Data.SqlTypes.SqlDecimal" : expectedTypeName;
            string bulkCopyExpectedBaseType = isSqlMoney ? "numeric" : expectedBaseTypeName;

            VerifyVariantBulkCopy(paramValue, bulkCopyExpectedType, bulkCopyExpectedBaseType);
            VerifyVariantParam(paramValue, expectedTypeName, expectedBaseTypeName);
            VerifyVariantTvp(paramValue, expectedTypeName, expectedBaseTypeName);
        }

        /// <summary>
        /// Round trip sql_variant value as normal parameter.
        /// </summary>
        private void VerifyVariantParam(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @p1 AS f1, sql_variant_property(@p1,'BaseType') AS BaseType";
            cmd.Parameters.Add("@p1", SqlDbType.Variant).Value = paramValue;

            using var dr = cmd.ExecuteReader();
            Assert.True(dr.Read(), "Expected a row from parameter query");

            string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
            string actualBaseTypeName = dr.GetString(1);

            Assert.Equal(expectedTypeName, actualTypeName);
            Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy.
        /// Tests all three BulkCopy sources: SqlDataReader, DataTable, and DataRow.
        /// </summary>
        private void VerifyVariantBulkCopy(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDest");
            _bulkCopyTablesToCleanup.Add(bulkCopyTableName);

            using var connBulk = new SqlConnection(_connStr);
            connBulk.Open();

            // Create target table
            using (var cmd = new SqlCommand($"CREATE TABLE dbo.{bulkCopyTableName} (f1 sql_variant)", connBulk))
            {
                cmd.ExecuteNonQuery();
            }

            try
            {
                // Test 1: BulkCopy from SqlDataReader
                using (var dr = GetReaderForVariant(paramValue, false))
                {
                    using var bulkCopy = new SqlBulkCopy(connBulk) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = bulkCopyTableName };
                    bulkCopy.WriteToServer(dr);
                }
                VerifyBulkCopyResult(connBulk, bulkCopyTableName, expectedTypeName, expectedBaseTypeName, "SqlDataReader");
                TruncateTable(connBulk, bulkCopyTableName);

                // Test 2: BulkCopy from DataTable
                var table = new DataTable();
                table.Columns.Add("f1", typeof(object));
                table.Rows.Add(new object[] { paramValue });

                using (var bulkCopy = new SqlBulkCopy(connBulk) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = bulkCopyTableName })
                {
                    bulkCopy.WriteToServer(table, DataRowState.Added);
                }
                VerifyBulkCopyResult(connBulk, bulkCopyTableName, expectedTypeName, expectedBaseTypeName, "DataTable");
                TruncateTable(connBulk, bulkCopyTableName);

                // Test 3: BulkCopy from DataRow[]
                DataRow[] rowToSend = table.Select();
                using (var bulkCopy = new SqlBulkCopy(connBulk) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = bulkCopyTableName })
                {
                    bulkCopy.WriteToServer(rowToSend);
                }
                VerifyBulkCopyResult(connBulk, bulkCopyTableName, expectedTypeName, expectedBaseTypeName, "DataRow");
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TABLE {bulkCopyTableName}", connBulk);
                dropCmd.ExecuteNonQuery();
                _bulkCopyTablesToCleanup.Remove(bulkCopyTableName);
            }
        }

        private void VerifyBulkCopyResult(SqlConnection conn, string tableName, string expectedTypeName, string expectedBaseTypeName, string sourceType)
        {
            using var cmd = new SqlCommand($"SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM {tableName}", conn);
            using var dr = cmd.ExecuteReader();
            Assert.True(dr.Read(), $"Expected a row from BulkCopy[{sourceType}] query");

            string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
            string actualBaseTypeName = dr.GetString(1);

            Assert.Equal(expectedTypeName, actualTypeName);
            Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
        }

        private static void TruncateTable(SqlConnection conn, string tableName)
        {
            using var cmd = new SqlCommand($"TRUNCATE TABLE {tableName}", conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Round trip sql_variant value using TVP.
        /// Tests both SqlMetaData and SqlDataReader as TVP sources.
        /// </summary>
        private void VerifyVariantTvp(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");
            _tvpTypesToCleanup.Add(tvpTypeName);

            using var connTvp = new SqlConnection(_connStr);
            connTvp.Open();

            // Create TVP type
            using (var cmd = new SqlCommand($"CREATE TYPE dbo.{tvpTypeName} AS TABLE (f1 sql_variant)", connTvp))
            {
                cmd.ExecuteNonQuery();
            }

            try
            {
                // Test 1: TVP using SqlMetaData
                var metadata = new SqlMetaData[] { new SqlMetaData("f1", SqlDbType.Variant) };
                var record = new SqlDataRecord(metadata);
                record.SetValue(0, paramValue);

                using (var cmd = connTvp.CreateCommand())
                {
                    cmd.CommandText = "SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM @tvpParam";
                    var p = cmd.Parameters.AddWithValue("@tvpParam", new[] { record });
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = $"dbo.{tvpTypeName}";

                    using var dr = cmd.ExecuteReader();
                    Assert.True(dr.Read(), "Expected a row from TVP[SqlMetaData] query");

                    string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                    string actualBaseTypeName = dr.GetString(1);

                    Assert.Equal(expectedTypeName, actualTypeName);
                    Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
                }

                // Test 2: TVP using SqlDataReader
                using (var drSource = GetReaderForVariant(paramValue, false))
                {
                    using var cmd = connTvp.CreateCommand();
                    cmd.CommandText = "SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM @tvpParam";
                    var p = cmd.Parameters.AddWithValue("@tvpParam", drSource);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = $"dbo.{tvpTypeName}";

                    using var dr = cmd.ExecuteReader();
                    Assert.True(dr.Read(), "Expected a row from TVP[SqlDataReader] query");

                    string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                    string actualBaseTypeName = dr.GetString(1);

                    Assert.Equal(expectedTypeName, actualTypeName);
                    Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
                }
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TYPE dbo.{tvpTypeName}", connTvp);
                dropCmd.ExecuteNonQuery();
                _tvpTypesToCleanup.Remove(tvpTypeName);
            }
        }

        /// <summary>
        /// Returns a SqlDataReader with embedded sql_variant column.
        /// </summary>
        private SqlDataReader GetReaderForVariant(object paramValue, bool includeBaseType)
        {
            var conn = new SqlConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @p1 AS f1";
            if (includeBaseType)
            {
                cmd.CommandText += ", sql_variant_property(@p1,'BaseType') AS BaseType";
            }

            cmd.Parameters.Add("@p1", SqlDbType.Variant).Value = paramValue;
            return cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }
    }
}
