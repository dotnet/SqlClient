// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for SQL Variant parameters.
    /// Tests all SqlTypes inside sql_variant to server using sql_variant parameter, SqlBulkCopy, and TVP parameter.
    /// </summary>
    [Trait("Set", "3")]
    public sealed class SqlVariantParameterTests : IDisposable
    {
        private readonly string _connStr;
        private readonly CultureInfo _previousCulture;

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
            yield return new object[] { new SqlMoney(123.123M), "System.Data.SqlTypes.SqlMoney", "money" };
        }

        /// <summary>
        /// Round trip sql_variant value as normal parameter.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData), DisableDiscoveryEnumeration = true)]
        public void SqlType_VariantParam_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // Arrange
            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @p1 AS f1, sql_variant_property(@p1,'BaseType') AS BaseType";
            cmd.Parameters.Add("@p1", SqlDbType.Variant).Value = paramValue;

            // Act
            using var dr = cmd.ExecuteReader();
            Assert.True(dr.Read(), "Expected a row from parameter query");
            string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
            string actualBaseTypeName = dr.GetString(1);

            // Assert
            Assert.Equal(expectedTypeName, actualTypeName);
            Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task AnsiVariant_OffsetChanged_PreservesCompleteValueAndCommandReuse(
            bool useAsync, bool sqlString)
        {
            SqlConnectionStringBuilder builder = new(_connStr) { Pooling = false };
            using SqlConnection connection = new(builder.ConnectionString);
            if (useAsync)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT @p, DATALENGTH(@p), " +
                "CONVERT(varchar(20), SQL_VARIANT_PROPERTY(@p, 'BaseType')), @marker";
            string text = "A" + new string('b', 4498) + "Z";
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.Variant);
            parameter.Value = sqlString ? new SqlString(text) : (object)text;
            const int Marker = 0x12345678;
            command.Parameters.Add("@marker", SqlDbType.Int).Value = Marker;

            // The ANSI variant writer ignores Offset; reusing the command must preserve that.
            foreach (int offset in new[] { 0, 1, 0 })
            {
                parameter.Offset = offset;
                using SqlDataReader reader = useAsync
                    ? await command.ExecuteReaderAsync() : command.ExecuteReader();
                Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
                Assert.Equal(text, reader.GetString(0));
                Assert.Equal(4500, reader.GetInt32(1));
                Assert.Equal("varchar", reader.GetString(2));
                Assert.Equal(Marker, reader.GetInt32(3));
                Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
                Assert.False(useAsync ? await reader.NextResultAsync() : reader.NextResult());
            }
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy with a SqlDataReader source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(BulkCopySqlTypeTestData), DisableDiscoveryEnumeration = true)]
        public void SqlType_BulkCopyFromReader_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // Arrange
            string tableName = DataTestUtility.GetLongName("bulkDest");

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using (var createCmd = new SqlCommand($"CREATE TABLE dbo.{tableName} (f1 sql_variant)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            try
            {
                // Act
                using (var dr = GetReaderForVariant(paramValue))
                {
                    using var bulkCopy = new SqlBulkCopy(conn) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = $"dbo.{tableName}" };
                    bulkCopy.WriteToServer(dr);
                }

                // Assert
                using (var verifyCmd = new SqlCommand($"SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM dbo.{tableName}", conn))
                using (var dr = verifyCmd.ExecuteReader())
                {
                    Assert.True(dr.Read(), "Expected a row from bulk copy query");
                    string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                    string actualBaseTypeName = dr.GetString(1);

                    Assert.Equal(expectedTypeName, actualTypeName);
                    Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
                }
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS dbo.{tableName}", conn);
                dropCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy with a DataTable source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(BulkCopySqlTypeTestData), DisableDiscoveryEnumeration = true)]
        public void SqlType_BulkCopyFromDataTable_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // Arrange
            string tableName = DataTestUtility.GetLongName("bulkDest");

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using (var createCmd = new SqlCommand($"CREATE TABLE dbo.{tableName} (f1 sql_variant)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            try
            {
                var table = new DataTable();
                table.Columns.Add("f1", typeof(object));
                table.Rows.Add(new object[] { paramValue });

                // Act
                using (var bulkCopy = new SqlBulkCopy(conn) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = $"dbo.{tableName}" })
                {
                    bulkCopy.WriteToServer(table, DataRowState.Added);
                }

                // Assert
                using (var verifyCmd = new SqlCommand($"SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM dbo.{tableName}", conn))
                using (var dr = verifyCmd.ExecuteReader())
                {
                    Assert.True(dr.Read(), "Expected a row from bulk copy query");
                    string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                    string actualBaseTypeName = dr.GetString(1);

                    Assert.Equal(expectedTypeName, actualTypeName);
                    Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
                }
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS dbo.{tableName}", conn);
                dropCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy with a DataRow[] source.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(BulkCopySqlTypeTestData), DisableDiscoveryEnumeration = true)]
        [Trait("category", "flaky")]
        public void SqlType_BulkCopyFromDataRow_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // Arrange
            string tableName = DataTestUtility.GetLongName("bulkDest");

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using (var createCmd = new SqlCommand($"CREATE TABLE dbo.{tableName} (f1 sql_variant)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            try
            {
                var table = new DataTable();
                table.Columns.Add("f1", typeof(object));
                table.Rows.Add(new object[] { paramValue });
                DataRow[] rows = table.Select();

                // Act
                using (var bulkCopy = new SqlBulkCopy(conn) { BulkCopyTimeout = 60, BatchSize = 1, DestinationTableName = $"dbo.{tableName}" })
                {
                    bulkCopy.WriteToServer(rows);
                }

                // Assert
                using (var verifyCmd = new SqlCommand($"SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM dbo.{tableName}", conn))
                using (var dr = verifyCmd.ExecuteReader())
                {
                    Assert.True(dr.Read(), "Expected a row from bulk copy query");
                    string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                    string actualBaseTypeName = dr.GetString(1);

                    Assert.Equal(expectedTypeName, actualTypeName);
                    Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
                }
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS dbo.{tableName}", conn);
                dropCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Round trip sql_variant value using TVP with a SqlMetaData/SqlDataRecord source.
        /// </summary>
        [Trait("category", "flaky")] // Doesn't work well on Azure.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData), DisableDiscoveryEnumeration = true)]
        public void SqlType_TvpFromSqlMetaData_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // Arrange
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using (var createCmd = new SqlCommand($"CREATE TYPE dbo.{tvpTypeName} AS TABLE (f1 sql_variant)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

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

                // Act
                using var dr = cmd.ExecuteReader();
                Assert.True(dr.Read(), "Expected a row from TVP[SqlMetaData] query");
                string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                string actualBaseTypeName = dr.GetString(1);

                // Assert
                Assert.Equal(expectedTypeName, actualTypeName);
                Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TYPE IF EXISTS dbo.{tvpTypeName}", conn);
                dropCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Round trip sql_variant value using TVP with a SqlDataReader source.
        /// </summary>
        [Trait("category", "flaky")] // Doesn't work well on Azure.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(SqlTypeTestData), DisableDiscoveryEnumeration = true)]
        public void SqlType_TvpFromSqlDataReader_RoundTripsCorrectly(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            // Arrange
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using (var createCmd = new SqlCommand($"CREATE TYPE dbo.{tvpTypeName} AS TABLE (f1 sql_variant)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            try
            {
                using var drSource = GetReaderForVariant(paramValue);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM @tvpParam";
                var p = cmd.Parameters.AddWithValue("@tvpParam", drSource);
                p.SqlDbType = SqlDbType.Structured;
                p.TypeName = $"dbo.{tvpTypeName}";

                // Act
                using var dr = cmd.ExecuteReader();
                Assert.True(dr.Read(), "Expected a row from TVP[SqlDataReader] query");
                string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
                string actualBaseTypeName = dr.GetString(1);

                // Assert
                Assert.Equal(expectedTypeName, actualTypeName);
                Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
            }
            finally
            {
                using var dropCmd = new SqlCommand($"DROP TYPE IF EXISTS dbo.{tvpTypeName}", conn);
                dropCmd.ExecuteNonQuery();
            }
        }

        #region Helpers

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
