// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Threading;
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
        private readonly CultureInfo _previousCulture;
        private readonly List<string> _bulkCopyTablesToCleanup = new();
        private readonly List<string> _tvpTypesToCleanup = new();

        public SqlVariantParameterTests()
        {
            _connStr = DataTestUtility.TCPConnectionString;

            // Work around a gap in ValueUtilsSmi.GetSqlValue where reading a SqlString
            // back from a SqlDataRecord Variant column reconstructs it via new SqlString(string),
            // which uses CultureInfo.CurrentCulture.LCID. On Linux, this LCID is 127
            // (InvariantCulture), which is not a valid SQL Server collation and causes
            // "invalid TDS collation" errors in the TVP code path.
            // SqlClient doesn't support invariant mode: 
            // https://github.com/dotnet/SqlClient/issues/3742
            _previousCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = _previousCulture;

            // Clean up any tables/types that may have been left behind
            using var conn = new SqlConnection(_connStr);
            conn.Open();
            foreach (var table in _bulkCopyTablesToCleanup)
            {
                try
                {
                    using var cmd = new SqlCommand($"DROP TABLE IF EXISTS dbo.{table}", conn);
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }
            foreach (var type in _tvpTypesToCleanup)
            {
                try
                {
                    using var cmd = new SqlCommand($"DROP TYPE IF EXISTS dbo.{type}", conn);
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

        public static IEnumerable<object[]> BulkCopySqlTypeTestData()
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
            // SqlMoney is coerced to decimal (numeric) by SqlBulkCopy (see https://github.com/dotnet/SqlClient/issues/4040).
            // ValidateBulkCopyVariant strips all INullable SqlTypes to their CLR equivalents via
            // MetaType.GetComValueFromSqlVariant, which converts SqlMoney to decimal. For most types
            // the CLR value maps back to the same TDS type (e.g. SqlInt32 -> int -> SQLINT4), but
            // decimal maps to SQLNUMERICN instead of SQLMONEY. The normal parameter path works
            // around this in WriteSqlVariantValue using a length==8 heuristic, but
            // WriteSqlVariantDataRowValue (used by bulk copy) has no such recovery logic.
            yield return new object[] { new SqlMoney(123.123M), "System.Data.SqlTypes.SqlDecimal", "numeric" };
        }

        /// <summary>
        /// Round trip sql_variant value as normal parameter.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData))]
        public void SqlType_VariantParam_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
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
        /// Round trip sql_variant value using SqlBulkCopy with a SqlDataReader source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(BulkCopySqlTypeTestData))]
        public void SqlType_BulkCopyFromReader_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {

            string tableName = DataTestUtility.GetLongName("bulkDest");
            _bulkCopyTablesToCleanup.Add(tableName);

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            CreateVariantTable(conn, tableName);

            try
            {
                using (var dr = GetReaderForVariant(paramValue))
                {
                    using var bulkCopy = new SqlBulkCopy(conn) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = $"dbo.{tableName}" };
                    bulkCopy.WriteToServer(dr);
                }
                VerifyVariantResult(conn, tableName, expectedTypeName, expectedBaseTypeName);
            }
            finally
            {
                DropTable(conn, tableName);
                _bulkCopyTablesToCleanup.Remove(tableName);
            }
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy with a DataTable source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(BulkCopySqlTypeTestData))]
        public void SqlType_BulkCopyFromDataTable_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {

            string tableName = DataTestUtility.GetLongName("bulkDest");
            _bulkCopyTablesToCleanup.Add(tableName);

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            CreateVariantTable(conn, tableName);

            try
            {
                var table = new DataTable();
                table.Columns.Add("f1", typeof(object));
                table.Rows.Add(new object[] { paramValue });

                using (var bulkCopy = new SqlBulkCopy(conn) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = $"dbo.{tableName}" })
                {
                    bulkCopy.WriteToServer(table, DataRowState.Added);
                }
                VerifyVariantResult(conn, tableName, expectedTypeName, expectedBaseTypeName);
            }
            finally
            {
                DropTable(conn, tableName);
                _bulkCopyTablesToCleanup.Remove(tableName);
            }
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy with a DataRow[] source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(BulkCopySqlTypeTestData))]
        public void SqlType_BulkCopyFromDataRow_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {

            string tableName = DataTestUtility.GetLongName("bulkDest");
            _bulkCopyTablesToCleanup.Add(tableName);

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            CreateVariantTable(conn, tableName);

            try
            {
                var table = new DataTable();
                table.Columns.Add("f1", typeof(object));
                table.Rows.Add(new object[] { paramValue });
                DataRow[] rows = table.Select();

                using (var bulkCopy = new SqlBulkCopy(conn) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = $"dbo.{tableName}" })
                {
                    bulkCopy.WriteToServer(rows);
                }
                VerifyVariantResult(conn, tableName, expectedTypeName, expectedBaseTypeName);
            }
            finally
            {
                DropTable(conn, tableName);
                _bulkCopyTablesToCleanup.Remove(tableName);
            }
        }

        /// <summary>
        /// Round trip sql_variant value using TVP with a SqlMetaData/SqlDataRecord source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData))]
        public void SqlType_TvpFromSqlMetaData_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");
            _tvpTypesToCleanup.Add(tvpTypeName);

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            CreateVariantTvpType(conn, tvpTypeName);

            try
            {
                var metadata = new SqlMetaData[] { new SqlMetaData("f1", SqlDbType.Variant) };
                var record = new SqlDataRecord(metadata);
                record.SetValue(0, paramValue);

                using var cmd = conn.CreateCommand();
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
            finally
            {
                DropTvpType(conn, tvpTypeName);
                _tvpTypesToCleanup.Remove(tvpTypeName);
            }
        }

        /// <summary>
        /// Round trip sql_variant value using TVP with a SqlDataReader source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData))]
        public void SqlType_TvpFromSqlDataReader_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");
            _tvpTypesToCleanup.Add(tvpTypeName);

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            CreateVariantTvpType(conn, tvpTypeName);

            try
            {
                using var drSource = GetReaderForVariant(paramValue);
                using var cmd = conn.CreateCommand();
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
            finally
            {
                DropTvpType(conn, tvpTypeName);
                _tvpTypesToCleanup.Remove(tvpTypeName);
            }
        }

        #region Helpers

        private static void CreateVariantTable(SqlConnection conn, string tableName)
        {
            using var cmd = new SqlCommand($"CREATE TABLE dbo.{tableName} (f1 sql_variant)", conn);
            cmd.ExecuteNonQuery();
        }

        private static void DropTable(SqlConnection conn, string tableName)
        {
            using var cmd = new SqlCommand($"DROP TABLE IF EXISTS dbo.{tableName}", conn);
            cmd.ExecuteNonQuery();
        }

        private static void CreateVariantTvpType(SqlConnection conn, string tvpTypeName)
        {
            using var cmd = new SqlCommand($"CREATE TYPE dbo.{tvpTypeName} AS TABLE (f1 sql_variant)", conn);
            cmd.ExecuteNonQuery();
        }

        private static void DropTvpType(SqlConnection conn, string tvpTypeName)
        {
            using var cmd = new SqlCommand($"DROP TYPE IF EXISTS dbo.{tvpTypeName}", conn);
            cmd.ExecuteNonQuery();
        }

        private void VerifyVariantResult(SqlConnection conn, string tableName, string expectedTypeName, string expectedBaseTypeName)
        {
            using var cmd = new SqlCommand($"SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM dbo.{tableName}", conn);
            using var dr = cmd.ExecuteReader();
            Assert.True(dr.Read(), "Expected a row from query");

            string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
            string actualBaseTypeName = dr.GetString(1);

            Assert.Equal(expectedTypeName, actualTypeName);
            Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
        }

        private SqlDataReader GetReaderForVariant(object paramValue)
        {
            var conn = new SqlConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @p1 AS f1";
            cmd.Parameters.Add("@p1", SqlDbType.Variant).Value = paramValue;
            return cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }

        #endregion
    }
}
