// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

#nullable enable
namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.ParameterTest
{
    public enum TestVariations
    {
        TestSimpleParameter_Type,
        TestSimpleParameter_Variant,
        TestSqlDataRecordParameterToTVP_Type,
        TestSqlDataRecordParameterToTVP_Variant,
        TestSqlDataReaderParameterToTVP_Type,
        TestSqlDataReaderParameterToTVP_Variant,
        TestSqlDataReader_TVP_Type,
        TestSqlDataReader_TVP_Variant,
        TestSimpleDataReader_Type,
        TestSimpleDataReader_Variant,
        SqlBulkCopySqlDataReader_Type,
        SqlBulkCopySqlDataReader_Variant,
        SqlBulkCopyDataTable_Type,
        SqlBulkCopyDataTable_Variant,
        SqlBulkCopyDataRow_Type,
        SqlBulkCopyDataRow_Variant
    }

    public delegate bool ExceptionChecker(Exception e, object paramValue);

    /// <summary>
    /// Base class for all data passed to a type derived from <see cref="DataTypeTestBase{TTestData}"/>.
    /// </summary>
    public abstract class DataTypeTestDataBase
    {
        public abstract IEnumerable<object?[]> TestData { get; }

        /// <summary>
        /// Generates test data for a case where an input value successfully roundtrips between the
        /// client and the server as the same type.
        /// </summary>
        /// <typeparam name="T">Type of the data being roundtripped.</typeparam>
        /// <param name="value">Value to roundtrip.</param>
        /// <param name="sqlTypeName">SQL Server type name.</param>
        /// <returns>Test case to be returned from <see cref="TestData"/>.</returns>
        protected static object?[] GenerateSuccessCase<T>(T value, string sqlTypeName)
            where T : notnull =>
            [
                value,
                sqlTypeName,
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()
            ];

        /// <summary>
        /// Generates test data for a case where an input value successfully roundtrips between the
        /// client and the server, but as different types (such as <see cref="byte"/> and
        /// <see cref="System.Data.SqlTypes.SqlByte"/>.)
        /// </summary>
        /// <typeparam name="TInput">Type of the data sent to the server.</typeparam>
        /// <typeparam name="TOutput">Type of the data received from the server.</typeparam>
        /// <param name="inputValue">Value to send to the server.</param>
        /// <param name="outputValue">Value expected from the server.</param>
        /// <param name="sqlTypeName">SQL Server type name.</param>
        /// <returns>Test case to be returned from <see cref="TestData"/>.</returns>
        protected static object?[] GenerateSuccessCase<TInput, TOutput>(TInput inputValue, TOutput outputValue, string sqlTypeName)
            where TInput : notnull
            where TOutput : notnull =>
            [
                inputValue,
                sqlTypeName,
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object>()
                {
                    { TestVariations.TestSimpleParameter_Type, outputValue },
                    { TestVariations.TestSimpleParameter_Variant, outputValue },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, outputValue },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, outputValue },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, outputValue },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, outputValue },
                    { TestVariations.TestSqlDataReader_TVP_Type, outputValue },
                    { TestVariations.TestSqlDataReader_TVP_Variant, outputValue },
                    { TestVariations.TestSimpleDataReader_Type, outputValue },
                    { TestVariations.TestSimpleDataReader_Variant, outputValue },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, outputValue },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, outputValue },
                    { TestVariations.SqlBulkCopyDataTable_Type, outputValue },
                    { TestVariations.SqlBulkCopyDataTable_Variant, outputValue },
                    { TestVariations.SqlBulkCopyDataRow_Type, outputValue },
                    { TestVariations.SqlBulkCopyDataRow_Variant, outputValue }
                },
                new Dictionary<TestVariations, string>()
            ];
    }

    /// <summary>
    /// Base class for all tests for variant parameters with different data types.
    /// </summary>
    public abstract class DataTypeTestBase<TTestData>
        where TTestData : DataTypeTestDataBase, new()
    {
        private readonly struct TestResult
        {
            public object Value { get; }
            public string BaseTypeName { get; }

            public TestResult(object value, string baseTypeName)
            {
                Value = value;
                BaseTypeName = baseTypeName;
            }
        }

        // xUnit only allows MemberData for a test to point to static methods, properties and variables.
        // This presents a problem when the sample data needs to change based upon the data type, so
        // this compromises: it instantiates a class derived from DataTypeTestDataBase, then projects
        // the TestData property from it as a static property in this base class.
        private static TTestData TestDataInstance =>
            field ??= new();

        /// <summary>
        /// Gets parameter combinations for MemberData. Each entry yields the full parameter value,
        /// expected base type name, expected exceptions, expected value overrides, and expected base
        /// type overrides. <see cref="MemberDataAttribute.DisableDiscoveryEnumeration"/> is set to
        /// avoid xUnit serialization issues with the complex types used in each row.
        /// </summary>
        public static IEnumerable<object?[]> ParameterCombinations =>
            TestDataInstance.TestData;

        private static void RunTest(
            TestVariations tag,
            Func<object, string, string, TestResult> action,
            object paramValue,
            string expectedBaseTypeName,
            string connStr,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            TestResult? result = null;
            Exception? caughtException = null;
            try
            {
                result = action(paramValue, expectedBaseTypeName, connStr);
            }
            catch (Exception e)
            {
                caughtException = e;
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }

            if (caughtException != null)
            {
                if (expectedExceptions.TryGetValue(tag, out var isExpectedException))
                {
                    Assert.True(isExpectedException(caughtException, paramValue), caughtException.Message);
                }
                else
                {
                    Assert.Fail($"Unexpected exception was thrown for test variation {tag} with parameter value {paramValue}. Exception: {caughtException}");
                }
            }
            else
            {
                expectedValueOverrides.TryGetValue(tag, out var expectedValueOverride);
                expectedBaseTypeOverrides.TryGetValue(tag, out var expectedBaseTypeOverride);

                TestResult testResult = result!.Value;
                string expectedBaseTypeWithoutPrecision = expectedBaseTypeName.IndexOf('(') != -1
                    ? expectedBaseTypeName.Substring(0, expectedBaseTypeName.IndexOf('('))
                    : expectedBaseTypeName;

                Assert.Equal(expectedValueOverride ?? paramValue, testResult.Value);
                Assert.Equal(expectedBaseTypeOverride ?? expectedBaseTypeWithoutPrecision, testResult.BaseTypeName);
            }
        }

        private static void xsql(SqlConnection conn, string sql)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static SqlDbType GetSqlDbType(string expectedBaseTypeName) =>
            expectedBaseTypeName.ToLowerInvariant() switch
            {
                "bigint" => SqlDbType.BigInt,
                "binary" => SqlDbType.Binary,
                "bit" => SqlDbType.Bit,
                "char" => SqlDbType.Char,
                "datetime" => SqlDbType.DateTime,
                "decimal" => SqlDbType.Decimal,
                "numeric" => SqlDbType.Decimal,
                "float" => SqlDbType.Float,
                "image" => SqlDbType.Image,
                "int" => SqlDbType.Int,
                "money" => SqlDbType.Money,
                "nchar" => SqlDbType.NChar,
                "ntext" => SqlDbType.NText,
                "nvarchar" => SqlDbType.NVarChar,
                "real" => SqlDbType.Real,
                "uniqueidentifier" => SqlDbType.UniqueIdentifier,
                "smalldatetime" => SqlDbType.SmallDateTime,
                "smallint" => SqlDbType.SmallInt,
                "smallmoney" => SqlDbType.SmallMoney,
                "text" => SqlDbType.Text,
                "timestamp" => SqlDbType.Timestamp,
                "tinyint" => SqlDbType.TinyInt,
                "varbinary" => SqlDbType.VarBinary,
                "varchar" => SqlDbType.VarChar,
                "sql_variant" => SqlDbType.Variant,
                "xml" => SqlDbType.Xml,
                "date" => SqlDbType.Date,
                "time" => SqlDbType.Time,
                "datetime2" => SqlDbType.DateTime2,
                "datetimeoffset" => SqlDbType.DateTimeOffset,
                _ => throw new ArgumentOutOfRangeException(nameof(expectedBaseTypeName))
            };

        /// <summary>
        /// Parses a base type name (such as numeric(29,2) or time(3)) to a resultant SqlDbType and
        /// the first and second components of its precision, length or scale.
        /// </summary>
        /// <param name="baseTypeName">Test-specific SQL Server base type.</param>
        /// <param name="sqlDbType">Resultant SqlDbType enum value.</param>
        /// <param name="firstElement">
        /// If specified, a precision, (if <paramref name="sqlDbType"/> is <see cref="SqlDbType.Decimal"/>)
        /// a scale (if <paramref name="sqlDbType"/> is <see cref="SqlDbType.Time"/>,
        /// <see cref="SqlDbType.DateTime2"/> or <see cref="SqlDbType.DateTimeOffset"/>) or a length
        /// (if <paramref name="sqlDbType"/> is <see cref="SqlDbType.VarChar"/>,
        /// <see cref="SqlDbType.NVarChar"/>, <see cref="SqlDbType.Char"/>, <see cref="SqlDbType.NChar"/>,
        /// <see cref="SqlDbType.Binary"/> or <see cref="SqlDbType.VarBinary"/>.)
        /// </param>
        /// <param name="secondElement">
        /// If specified, a scale (if <paramref name="sqlDbType"/> is <see cref="SqlDbType.Decimal"/>.)
        /// </param>
        /// <param name="elementsSpecified">
        /// Whether a precision, length or scale were explicitly specified in the type name.
        /// </param>
        private static void ParseBaseTypeName(
            string baseTypeName,
            out SqlDbType sqlDbType,
            out long firstElement,
            out byte secondElement,
            out bool elementsSpecified)
        {
            int startBracketIdx = baseTypeName.IndexOf('(');

            // A type may legitimately not specify any precision or scale.
            if (startBracketIdx == -1)
            {
                sqlDbType = GetSqlDbType(baseTypeName);
                firstElement = 0;
                secondElement = 0;
                elementsSpecified = false;
                return;
            }

            // Getting here means that the test is supplying a precision/scale/length. Extract this.
            Assert.Contains(")", baseTypeName);

            string preBracketType = baseTypeName.Substring(0, startBracketIdx);
            sqlDbType = GetSqlDbType(preBracketType);

            int endBracketIdx = baseTypeName.IndexOf(')', startBracketIdx);
            string typePrecisionScale = baseTypeName.Substring(startBracketIdx + 1, endBracketIdx - startBracketIdx - 1);
            // If the test value supplies a precision and a scale, extract them both.
            int commaIdx = typePrecisionScale.IndexOf(',');
            (string preComma, string postComma) =
                commaIdx == -1
                ? (typePrecisionScale.Trim(), "0")
                : (typePrecisionScale.Substring(0, commaIdx), typePrecisionScale.Substring(commaIdx + 1));

            (firstElement, secondElement) = (long.Parse(preComma), byte.Parse(postComma));
            elementsSpecified = true;
        }

        /// <summary>
        /// Generates a <see cref="SqlMetaData"/> which represents a column with a specific
        /// SQL Server type (including scale, precision or length.)
        /// </summary>
        /// <param name="expectedBaseTypeName">The expected SQL Server type name.</param>
        /// <returns>A <see cref="SqlMetaData"/> instance.</returns>
        private static SqlMetaData GenerateTypedSqlMetaData(string expectedBaseTypeName)
        {
            const string ColumnName = "f1";

            // This parses a base type name of "numeric(12, 2)" or "varchar(50)" and generates an
            // appropriate SqlMetaData instance. It's necessary in order to provide a test harness
            // for decimal.MaxValue (which exceeds the maximum precision of 18 which is the default
            // of SqlDbType.Decimal.)
            ParseBaseTypeName(expectedBaseTypeName,
                out SqlDbType sqlDbType,
                out long firstElement,
                out byte secondElement,
                out bool elementsSpecified);

            if (!elementsSpecified)
            {
                return new SqlMetaData(ColumnName, sqlDbType);
            }

            // Decimal uses both precision and scale. The variable-scale date/time types only use
            // a scale. Other types are text or binary, and the first element is actually a length.
            return sqlDbType switch
            {
                SqlDbType.Decimal =>
                    new SqlMetaData(ColumnName, sqlDbType, precision: checked((byte)firstElement), scale: secondElement),
                SqlDbType.Time or SqlDbType.DateTime2 or SqlDbType.DateTimeOffset =>
                    new SqlMetaData(ColumnName, sqlDbType, precision: 0, scale: checked((byte)firstElement)),
                _ => new SqlMetaData(ColumnName, sqlDbType, maxLength: firstElement)
            };
        }

        /// <summary>
        /// Converts a managed object to a string which can be passed to the SQL Server <c>CAST</c>
        /// function and converted back to a value of the correct database type.
        /// </summary>
        /// <param name="value">The managed object to convert to a string.</param>
        /// <returns>The string to be passed to SQL Server.</returns>
        protected abstract string ManagedTypeToString(object value);

        /// <summary>
        /// Verifies that a scalar parameter sent using its declared SQL Server type
        /// round-trips through a stored procedure with the expected value and base type.
        /// </summary>
        /// <param name="paramValue">The value to send to SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSimpleParameter_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSimpleParameter_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();
                    using StoredProcedure proc = new(conn, "paramProc1", $"(@param {expectedBaseTypeName}) AS BEGIN SELECT @param, sql_variant_property(@param,'BaseType') AS BaseType END;");

                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = proc.Name;
                    cmd.CommandType = CommandType.StoredProcedure;

                    ParseBaseTypeName(expectedBaseTypeName, out SqlDbType sqlDbType, out _, out _, out _);
                    SqlParameter p = cmd.Parameters.AddWithValue("@param", paramValue);
                    cmd.Parameters[0].SqlDbType = sqlDbType;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], dr.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a scalar parameter sent as <c>sql_variant</c> round-trips
        /// through a stored procedure with the expected value and underlying SQL Server base type.
        /// </summary>
        /// <param name="paramValue">The value to send to SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSimpleParameter_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSimpleParameter_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();
                    using StoredProcedure proc = new(conn, "paramProc2", "(@param sql_variant) AS BEGIN SELECT @param, sql_variant_property(@param,'BaseType') AS BaseType END;");

                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = proc.Name;
                    cmd.CommandType = CommandType.StoredProcedure;
                    SqlParameter p = cmd.Parameters.AddWithValue("@param", paramValue);
                    cmd.Parameters[0].SqlDbType = SqlDbType.Variant;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], dr.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a table-valued parameter populated using <see cref="SqlDataRecord"/>
        /// preserves the expected value and base type when its column has the declared SQL Server type.
        /// </summary>
        /// <param name="paramValue">The value to send to SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSqlDataRecordParameterToTVP_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSqlDataRecordParameterToTVP_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();
                    using UserDefinedType tvpType = new(conn, "tvpType", $"TABLE (f1 {expectedBaseTypeName})");

                    // Send TVP using SqlMetaData.
                    SqlMetaData[] metadata = new SqlMetaData[1];
                    metadata[0] = GenerateTypedSqlMetaData(expectedBaseTypeName);
                    SqlDataRecord[] record = new SqlDataRecord[1];
                    record[0] = new SqlDataRecord(metadata);
                    record[0].SetValue(0, paramValue);

                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                    SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", record);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = tvpType.Name;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], dr.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a table-valued parameter populated using <see cref="SqlDataRecord"/>
        /// preserves the expected value and underlying base type when its column is <c>sql_variant</c>.
        /// </summary>
        /// <param name="paramValue">The value to send to SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSqlDataRecordParameterToTVP_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSqlDataRecordParameterToTVP_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();
                    using UserDefinedType tvpType = new(conn, "tvpVariant", "TABLE (f1 sql_variant)");

                    // Send TVP using SqlMetaData.
                    SqlMetaData[] metadata = new SqlMetaData[1];
                    metadata[0] = new SqlMetaData("f1", SqlDbType.Variant);
                    SqlDataRecord[] record = new SqlDataRecord[1];
                    record[0] = new SqlDataRecord(metadata);
                    record[0].SetValue(0, paramValue);

                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                    SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", record);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = tvpType.Name;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], dr.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a <see cref="SqlDataReader"/> used to populate a table-valued parameter
        /// preserves the expected value and base type in a strongly typed TVP column.
        /// </summary>
        /// <param name="paramValue">The value to send to SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSqlDataReaderParameterToTVP_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSqlDataReaderParameterToTVP_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();
                    using UserDefinedType tvpType = new(conn, "tvpType", $"TABLE (f1 {expectedBaseTypeName})");
                    using (SqlConnection connInput = new(connStr))
                    {
                        connInput.Open();
                        using (SqlCommand cmdInput = connInput.CreateCommand())
                        {
                            ParseBaseTypeName(expectedBaseTypeName, out SqlDbType sqlDbType, out _, out _, out _);

                            cmdInput.CommandText = "select @p1 as f1";
                            cmdInput.Parameters.Add("@p1", sqlDbType);
                            cmdInput.Parameters["@p1"].Value = paramValue;
                            using SqlDataReader drInput = cmdInput.ExecuteReader(CommandBehavior.CloseConnection);
                            using SqlCommand cmd = conn.CreateCommand();
                            cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                            SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", drInput);
                            p.SqlDbType = SqlDbType.Structured;
                            p.TypeName = tvpType.Name;
                            using (SqlDataReader dr = cmd.ExecuteReader())
                            {
                                dr.Read();
                                return new TestResult(dr[0], dr.GetString(1));
                            }
                        }
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a <see cref="SqlDataReader"/> used to populate a table-valued parameter
        /// preserves the expected value and underlying base type in a <c>sql_variant</c> column.
        /// </summary>
        /// <param name="paramValue">The value to send to SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSqlDataReaderParameterToTVP_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSqlDataReaderParameterToTVP_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();
                    using UserDefinedType tvpType = new(conn, "tvpVariant", "TABLE (f1 sql_variant)");

                    // Send TVP using SqlDataReader.
                    using (SqlConnection connInput = new(connStr))
                    {
                        connInput.Open();
                        using (SqlCommand cmdInput = connInput.CreateCommand())
                        {
                            cmdInput.CommandText = "select @p1 as f1";
                            cmdInput.Parameters.Add("@p1", SqlDbType.Variant);
                            cmdInput.Parameters["@p1"].Value = paramValue;
                            using SqlDataReader drInput = cmdInput.ExecuteReader(CommandBehavior.CloseConnection);
                            using SqlCommand cmd = conn.CreateCommand();
                            cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                            SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", drInput);
                            p.SqlDbType = SqlDbType.Structured;
                            p.TypeName = tvpType.Name;
                            using (SqlDataReader dr = cmd.ExecuteReader())
                            {
                                dr.Read();
                                return new TestResult(dr[0], dr.GetString(1));
                            }
                        }
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that values read from a source table and passed through a table-valued parameter
        /// preserve their values and base types when the TVP column is strongly typed.
        /// </summary>
        /// <param name="paramValue">The value to test.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSqlDataReader_TVP_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSqlDataReader_TVP_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using UserDefinedType tvpType = new(conn, "tvpType", $"TABLE (f1 {expectedBaseTypeName})");
                    using Table inputTable = new(conn, "InputTable", $"(f1 {expectedBaseTypeName})");
                    using Table outputTable = new(conn, "OutputTable", $"(f1 {expectedBaseTypeName})");

                    string value = ManagedTypeToString(paramValue);
                    xsql(conn, string.Format("insert into {0} values(CAST('{1}' AS {2}))", inputTable.Name, value, expectedBaseTypeName));
                    using StoredProcedure proc = new(conn, "spTVPProc", $"(@P {tvpType.Name} READONLY) AS BEGIN INSERT INTO {outputTable.Name} SELECT * FROM @P; END");

                    SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format("SELECT * FROM {0}", inputTable.Name);
                    using SqlDataReader r = cmd.ExecuteReader();
                    using (SqlConnection conn2 = new(connStr))
                    {
                        conn2.Open();
                        SqlCommand cmd2 = new(proc.Name, conn2)
                        {
                            CommandType = CommandType.StoredProcedure
                        };
                        SqlParameter p = cmd2.Parameters.AddWithValue("@P", r);
                        p.SqlDbType = SqlDbType.Structured;
                        p.TypeName = tvpType.Name;
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = string.Format("SELECT f1, sql_variant_property(f1,'BaseType') as BaseType FROM {0}", outputTable.Name);
                        cmd2.CommandType = CommandType.Text;
                        using (SqlDataReader dr = cmd2.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that values read from a source table and passed through a table-valued parameter
        /// preserve their values and underlying base types when the TVP column is <c>sql_variant</c>.
        /// </summary>
        /// <param name="paramValue">The value to test.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSqlDataReader_TVP_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSqlDataReader_TVP_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using UserDefinedType tvpType = new(conn, "tvpVariant_DRdrTVPVar", "TABLE (f1 sql_variant)");
                    using Table inputTable = new(conn, "InputTable", "(f1 sql_variant)");
                    using Table outputTable = new(conn, "OutputTable", "(f1 sql_variant)");

                    string value = ManagedTypeToString(paramValue);
                    xsql(conn, string.Format("insert into {0} values(CAST('{1}' AS {2}))", inputTable.Name, value, expectedBaseTypeName));
                    using StoredProcedure proc = new(conn, "spTVPProc_DRdrTVPVar", $"(@P {tvpType.Name} READONLY) AS BEGIN INSERT INTO {outputTable.Name} SELECT * FROM @P; END");

                    SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format("SELECT * FROM {0}", inputTable.Name);
                    using SqlDataReader r = cmd.ExecuteReader();
                    using (SqlConnection conn2 = new(connStr))
                    {
                        conn2.Open();
                        using (SqlCommand cmd2 = new(proc.Name, conn2))
                        {
                            cmd2.CommandType = CommandType.StoredProcedure;
                            SqlParameter p = cmd2.Parameters.AddWithValue("@P", r);
                            p.SqlDbType = SqlDbType.Structured;
                            p.TypeName = tvpType.Name;
                            cmd2.ExecuteNonQuery();

                            cmd2.CommandText = string.Format("SELECT f1, sql_variant_property(f1,'BaseType') as BaseType FROM {0}", outputTable.Name);
                            cmd2.CommandType = CommandType.Text;
                            using (SqlDataReader dr = cmd2.ExecuteReader())
                            {
                                dr.Read();
                                return new TestResult(dr[0], dr.GetString(1));
                            }
                        }
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a <see cref="SqlDataReader"/> returns the expected value and SQL Server
        /// base type when reading a column with the declared SQL Server type.
        /// </summary>
        /// <param name="paramValue">The value to store and read from SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSimpleDataReader_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSimpleDataReader_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table inputTable = new(conn, "inputTable", $"(f1 {expectedBaseTypeName})");
                    string value = ManagedTypeToString(paramValue);
                    xsql(conn, string.Format("insert into {0}(f1) values('{1}');", inputTable.Name, value));

                    using StoredProcedure proc = new(conn, "paramProc3", $"AS BEGIN SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM {inputTable.Name} END;");

                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = proc.Name;
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], dr.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that a <see cref="SqlDataReader"/> returns the expected value and underlying
        /// SQL Server base type when reading a <c>sql_variant</c> column.
        /// </summary>
        /// <param name="paramValue">The value to store and read from SQL Server.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void TestSimpleDataReader_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.TestSimpleDataReader_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table inputTable = new(conn, "inputTable", "(f1 sql_variant)");
                    string value = ManagedTypeToString(paramValue);
                    xsql(conn, string.Format("insert into {0}(f1) values(CAST('{1}' AS {2}));", inputTable.Name, value, expectedBaseTypeName));

                    using StoredProcedure proc = new(conn, "paramProc4", $"AS BEGIN SELECT f1, sql_variant_property(f1,'BaseType') AS BaseType FROM {inputTable.Name} END;");

                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = proc.Name;
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], dr.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that bulk copying from a <see cref="SqlDataReader"/> into a destination column
        /// of the declared SQL Server type preserves the expected value and base type.
        /// </summary>
        /// <param name="paramValue">The value to bulk copy.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void SqlBulkCopySqlDataReader_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.SqlBulkCopySqlDataReader_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table bulkCopyDestTable = new(conn, "bulkDestTable", $"(f1 {expectedBaseTypeName})");
                    using Table bulkCopySrcTable = new(conn, "bulkSrcTable", $"(f1 {expectedBaseTypeName})");

                    string value = ManagedTypeToString(paramValue);
                    xsql(conn, string.Format("insert into {0}(f1) values(CAST('{1}' AS {2}));", bulkCopySrcTable.Name, value, expectedBaseTypeName));

                    using SqlConnection connInput = new(connStr);
                    connInput.Open();
                    using (SqlCommand cmdInput = connInput.CreateCommand())
                    {
                        cmdInput.CommandText = string.Format("select * from {0}", bulkCopySrcTable.Name);
                        using SqlDataReader drInput = cmdInput.ExecuteReader();
                        // Perform bulk copy to target.
                        using (SqlBulkCopy bulkCopy = new(conn))
                        {
                            bulkCopy.BulkCopyTimeout = 60;
                            bulkCopy.BatchSize = 1;
                            bulkCopy.DestinationTableName = bulkCopyDestTable.Name;
                            bulkCopy.WriteToServer(drInput);
                        }

                        // Verify target.
                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyDestTable.Name);
                        using (SqlDataReader drVerify = cmd.ExecuteReader())
                        {
                            drVerify.Read();
                            return new TestResult(drVerify[0], drVerify.GetString(1));
                        }
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that bulk copying from a <see cref="SqlDataReader"/> into a <c>sql_variant</c>
        /// destination column preserves the expected value and underlying base type.
        /// </summary>
        /// <param name="paramValue">The value to bulk copy.</param>
        /// <param name="expectedBaseTypeName">The expected underlying SQL Server type, optionally including precision, scale, or length.</param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">Expected returned values that differ from the input value for specific variations.</param>
        /// <param name="expectedBaseTypeOverrides">Expected SQL Server base types that differ from the specified type for specific variations.</param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void SqlBulkCopySqlDataReader_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.SqlBulkCopySqlDataReader_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table bulkCopyDestTable = new(conn, "bulkDestTable", "(f1 sql_variant)");
                    using Table bulkCopySrcTable = new(conn, "bulkSrcTable", $"(f1 {expectedBaseTypeName})");

                    string value = ManagedTypeToString(paramValue);
                    xsql(conn, string.Format("insert into {0}(f1) values(CAST('{1}' AS {2}));", bulkCopySrcTable.Name, value, expectedBaseTypeName));

                    using (SqlConnection connInput = new(connStr))
                    {
                        connInput.Open();
                        using (SqlCommand cmdInput = connInput.CreateCommand())
                        {
                            cmdInput.CommandText = string.Format("select * from {0}", bulkCopySrcTable.Name);
                            using SqlDataReader drInput = cmdInput.ExecuteReader();
                            {
                                // Perform bulk copy to target.
                                using (SqlBulkCopy bulkCopy = new(conn))
                                {
                                    bulkCopy.BulkCopyTimeout = 60;
                                    bulkCopy.BatchSize = 1;
                                    bulkCopy.DestinationTableName = bulkCopyDestTable.Name;
                                    bulkCopy.WriteToServer(drInput);
                                }

                                // Verify target.
                                using SqlCommand cmd = conn.CreateCommand();
                                cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyDestTable.Name);
                                using (SqlDataReader drVerify = cmd.ExecuteReader())
                                {
                                    drVerify.Read();
                                    return new TestResult(drVerify[0], drVerify.GetString(1));
                                }
                            }
                        }
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that bulk copying a <see cref="DataTable"/> into a destination column of the
        /// declared SQL Server type preserves the expected value and base type.
        /// </summary>
        /// <param name="paramValue">The value to bulk copy.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void SqlBulkCopyDataTable_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.SqlBulkCopyDataTable_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table bulkCopyTable = new(conn, "bulkDestType", $"(f1 {expectedBaseTypeName})");

                    // Send using DataTable as source.
                    DataTable t = new();
                    t.Columns.Add("f1", paramValue.GetType());
                    t.Rows.Add(new object[] { paramValue });

                    // Perform bulk copy to target.
                    using (SqlBulkCopy bulkCopy = new(conn))
                    {
                        bulkCopy.BulkCopyTimeout = 60;
                        bulkCopy.BatchSize = 1;
                        bulkCopy.DestinationTableName = bulkCopyTable.Name;
                        bulkCopy.WriteToServer(t, DataRowState.Added);
                    }

                    // Verify target.
                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTable.Name);
                    using (SqlDataReader drVerify = cmd.ExecuteReader())
                    {
                        drVerify.Read();
                        return new TestResult(drVerify[0], drVerify.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that bulk copying a <see cref="DataTable"/> into a <c>sql_variant</c>
        /// destination column preserves the expected value and underlying base type.
        /// </summary>
        /// <param name="paramValue">The value to bulk copy.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void SqlBulkCopyDataTable_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.SqlBulkCopyDataTable_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table bulkCopyTable = new(conn, "bulkDestVariant", "(f1 sql_variant)");

                    // Send using DataTable as source.
                    DataTable t = new();
                    t.Columns.Add("f1", typeof(object));
                    t.Rows.Add(new object[] { paramValue });

                    // Perform bulk copy to target.
                    using (SqlBulkCopy bulkCopy = new(conn))
                    {
                        bulkCopy.BulkCopyTimeout = 60;
                        bulkCopy.BatchSize = 1;
                        bulkCopy.DestinationTableName = bulkCopyTable.Name;
                        bulkCopy.WriteToServer(t, DataRowState.Added);
                    }

                    // Verify target.
                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTable.Name);
                    using (SqlDataReader drVerify = cmd.ExecuteReader())
                    {
                        drVerify.Read();
                        return new TestResult(drVerify[0], drVerify.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that bulk copying a <see cref="DataRow"/> into a destination column of the
        /// declared SQL Server type preserves the expected value and base type.
        /// </summary>
        /// <param name="paramValue">The value to bulk copy.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void SqlBulkCopyDataRow_Type(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.SqlBulkCopyDataRow_Type,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table bulkCopyTable = new(conn, "bulkDestType", $"(f1 {expectedBaseTypeName})");
                    DataTable t = new();
                    t.Columns.Add("f1", paramValue.GetType());
                    t.Rows.Add(new object[] { paramValue });
                    DataRow[] rowToSend = t.Select();
                    using (SqlBulkCopy bulkCopy = new(conn))
                    {
                        bulkCopy.BulkCopyTimeout = 60;
                        bulkCopy.BatchSize = 1;
                        bulkCopy.DestinationTableName = bulkCopyTable.Name;
                        bulkCopy.WriteToServer(rowToSend);
                    }
                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTable.Name);
                    using (SqlDataReader drVerify = cmd.ExecuteReader())
                    {
                        drVerify.Read();
                        return new TestResult(drVerify[0], drVerify.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        /// <summary>
        /// Verifies that bulk copying a <see cref="DataRow"/> into a <c>sql_variant</c> destination
        /// column preserves the expected value and underlying base type.
        /// </summary>
        /// <param name="paramValue">The value to bulk copy.</param>
        /// <param name="expectedBaseTypeName">
        /// The expected underlying SQL Server type, optionally including precision, scale, or length.
        /// </param>
        /// <param name="expectedExceptions">The exceptions expected for individual test variations.</param>
        /// <param name="expectedValueOverrides">
        /// Expected returned values that differ from the input value for specific variations.
        /// </param>
        /// <param name="expectedBaseTypeOverrides">
        /// Expected SQL Server base types that differ from the specified type for specific variations.
        /// </param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ParameterCombinations), DisableDiscoveryEnumeration = true)]
        public void SqlBulkCopyDataRow_Variant(
            object paramValue,
            string expectedBaseTypeName,
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, string> expectedBaseTypeOverrides)
        {
            RunTest(
                TestVariations.SqlBulkCopyDataRow_Variant,
                (paramValue, expectedBaseTypeName, connStr) =>
                {
                    using SqlConnection conn = new(connStr);
                    conn.Open();

                    using Table bulkCopyTable = new(conn, "bulkDestVariant", "(f1 sql_variant)");
                    DataTable t = new();
                    t.Columns.Add("f1", typeof(object));
                    t.Rows.Add(new object[] { paramValue });
                    DataRow[] rowToSend = t.Select();
                    using (SqlBulkCopy bulkCopy = new(conn))
                    {
                        bulkCopy.BulkCopyTimeout = 60;
                        bulkCopy.BatchSize = 1;
                        bulkCopy.DestinationTableName = bulkCopyTable.Name;
                        bulkCopy.WriteToServer(rowToSend);
                    }
                    using SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTable.Name);
                    using (SqlDataReader drVerify = cmd.ExecuteReader())
                    {
                        drVerify.Read();
                        return new TestResult(drVerify[0], drVerify.GetString(1));
                    }
                },
                paramValue,
                expectedBaseTypeName,
                DataTestUtility.TCPConnectionString,
                expectedExceptions,
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }
    }
}
