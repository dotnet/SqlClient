// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;
using Xunit;

#nullable enable
namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public enum TestVariations {
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
    };

    public struct TestResult
    {
        public object Value { get; }
        public string BaseTypeName { get; }

        public TestResult(object value, string baseTypeName)
        {
            Value = value;
            BaseTypeName = baseTypeName;
        }
    }

    public delegate bool ExceptionChecker(Exception e, object paramValue);

    /// <summary>
    /// Tests for DateTime variant parameters with different date/time types.
    /// </summary>
    public class DateTimeVariantTests
    {
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
            try
            {
                TestResult result = action(paramValue, expectedBaseTypeName, connStr);

                expectedValueOverrides.TryGetValue(tag, out var expectedValueOverride);
                expectedBaseTypeOverrides.TryGetValue(tag, out var expectedBaseTypeOverride);
                
                Assert.Equal(expectedValueOverride ?? paramValue, result.Value);
                Assert.Equal(expectedBaseTypeOverride ?? expectedBaseTypeName, result.BaseTypeName);
            }
            catch (Exception e)
            {
                if (expectedExceptions.TryGetValue(tag, out var isExpectedException))
                {
                    Assert.True(isExpectedException(e, paramValue), e.Message);
                }
                else {
                    Assert.Fail($"Unexpected exception was thrown for test variation {tag} with parameter value {paramValue}. Exception: {e}");
                }
            }
        }

        private static SqlDbType GetSqlDbType(string expectedBaseTypeName)
        {
            return expectedBaseTypeName.ToLowerInvariant() switch
            {
                "time" => SqlDbType.Time,
                "date" => SqlDbType.Date,
                "smalldatetime" => SqlDbType.SmallDateTime,
                "datetime" => SqlDbType.DateTime,
                "datetime2" => SqlDbType.DateTime2,
                "datetimeoffset" => SqlDbType.DateTimeOffset,
                _ => SqlDbType.Variant,
            };
        }

        private static void xsql(SqlConnection conn, string sql)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static void DropStoredProcedure(SqlConnection conn, string procName)
        {
            xsql(conn, string.Format("if exists(select 1 from sys.procedures where name='{0}') begin drop proc {1} end", procName.Substring(1, procName.Length - 2), procName));
        }

        private static void DropTable(SqlConnection conn, string tableName)
        {
            xsql(conn, string.Format("if exists(select 1 from sys.tables where name='{0}') begin drop table {1} end", tableName.Substring(1, tableName.Length - 2), tableName));
        }

        private static void DropType(SqlConnection conn, string typeName)
        {
            xsql(conn, string.Format("if exists(select 1 from sys.types where name='{0}') begin drop type {1} end", typeName.Substring(1, typeName.Length - 2), typeName));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string procName = DataTestUtility.GetLongName("paramProc1");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropStoredProcedure(conn, procName);
                        xsql(conn, string.Format("create proc {0} (@param {1}) as begin select @param, sql_variant_property(@param,'BaseType') as BaseType end;", procName, expectedBaseTypeName));

                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = procName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        SqlParameter p = cmd.Parameters.AddWithValue("@param", paramValue);
                        cmd.Parameters[0].SqlDbType = GetSqlDbType(expectedBaseTypeName);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                    finally
                    {
                        DropStoredProcedure(conn, procName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string procName = DataTestUtility.GetLongName("paramProc2");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropStoredProcedure(conn, procName);
                        xsql(conn, string.Format("create proc {0} (@param sql_variant) as begin select @param, sql_variant_property(@param,'BaseType') as BaseType end;", procName));

                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = procName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        SqlParameter p = cmd.Parameters.AddWithValue("@param", paramValue);
                        cmd.Parameters[0].SqlDbType = SqlDbType.Variant;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                    finally
                    {
                        DropStoredProcedure(conn, procName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string tvpTypeName = DataTestUtility.GetLongName("tvpType");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropType(conn, tvpTypeName);
                        xsql(conn, string.Format("create type dbo.{0} as table (f1 {1})", tvpTypeName, expectedBaseTypeName));

                        // Send TVP using SqlMetaData.
                        SqlMetaData[] metadata = new SqlMetaData[1];
                        metadata[0] = new SqlMetaData("f1", GetSqlDbType(expectedBaseTypeName));
                        SqlDataRecord[] record = new SqlDataRecord[1];
                        record[0] = new SqlDataRecord(metadata);
                        record[0].SetValue(0, paramValue);

                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                        SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", record);
                        p.SqlDbType = SqlDbType.Structured;
                        p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                    finally
                    {
                        DropType(conn, tvpTypeName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropType(conn, tvpTypeName);
                        xsql(conn, string.Format("create type dbo.{0} as table (f1 sql_variant)", tvpTypeName));

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
                        p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                    finally
                    {
                        DropType(conn, tvpTypeName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string tvpTypeName = DataTestUtility.GetLongName("tvpType");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropType(conn, tvpTypeName);
                        xsql(conn, string.Format("create type dbo.{0} as table (f1 {1})", tvpTypeName, expectedBaseTypeName));
                        using (SqlConnection connInput = new(connStr))
                        {
                            connInput.Open();
                            using (SqlCommand cmdInput = connInput.CreateCommand())
                            {
                                cmdInput.CommandText = "select @p1 as f1";
                                cmdInput.Parameters.Add("@p1", GetSqlDbType(expectedBaseTypeName));
                                cmdInput.Parameters["@p1"].Value = paramValue;
                                using SqlDataReader drInput = cmdInput.ExecuteReader(CommandBehavior.CloseConnection);
                                using SqlCommand cmd = conn.CreateCommand();
                                cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                                SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", drInput);
                                p.SqlDbType = SqlDbType.Structured;
                                p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                                using (SqlDataReader dr = cmd.ExecuteReader())
                                {
                                    dr.Read();
                                    return new TestResult(dr[0], dr.GetString(1));
                                }
                            }
                        }
                    }
                    finally
                    {
                        DropType(conn, tvpTypeName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropType(conn, tvpTypeName);
                        xsql(conn, string.Format("create type dbo.{0} as table (f1 sql_variant)", tvpTypeName));

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
                                p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                                using (SqlDataReader dr = cmd.ExecuteReader())
                                {
                                    dr.Read();
                                    return new TestResult(dr[0], dr.GetString(1));
                                }
                            }
                        }
                    }
                    finally
                    {
                        DropType(conn, tvpTypeName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string tvpTypeName = DataTestUtility.GetLongName("tvpType");
                    string InputTableName = DataTestUtility.GetLongName("InputTable");
                    string OutputTableName = DataTestUtility.GetLongName("OutputTable");
                    string ProcName = DataTestUtility.GetLongName("spTVPProc");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();

                        DropStoredProcedure(conn, ProcName);
                        DropTable(conn, InputTableName);
                        DropTable(conn, OutputTableName);
                        DropType(conn, $"dbo.{tvpTypeName}");

                        xsql(conn, string.Format("create type dbo.{0} as table (f1 {1})", tvpTypeName, expectedBaseTypeName));
                        xsql(conn, string.Format("create table {0} (f1 {1})", InputTableName, expectedBaseTypeName));
                        xsql(conn, string.Format("create table {0} (f1 {1})", OutputTableName, expectedBaseTypeName));

                        string value = string.Empty;
                        if (paramValue.GetType() == typeof(DateTimeOffset))
                        {
                            DateTime dt = ((DateTimeOffset)paramValue).UtcDateTime;
                            value = dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
                        }
                        else if (paramValue.GetType() == typeof(TimeSpan))
                        {
                            value = ((TimeSpan)paramValue).ToString();
                        }
                        else
                        {
                            value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
                        }
                        xsql(conn, string.Format("insert into {0} values(CAST('{1}' AS {2}))", InputTableName, value, expectedBaseTypeName));
                        xsql(conn, string.Format("create proc {0} (@P {1} READONLY) as begin insert into {2} select * from @P; end", ProcName, tvpTypeName, OutputTableName));

                        SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("SELECT * FROM {0}", InputTableName);
                        using SqlDataReader r = cmd.ExecuteReader();
                        using (SqlConnection conn2 = new(connStr))
                        {
                            conn2.Open();
                            SqlCommand cmd2 = new(ProcName, conn2)
                            {
                                CommandType = CommandType.StoredProcedure
                            };
                            SqlParameter p = cmd2.Parameters.AddWithValue("@P", r);
                            p.SqlDbType = SqlDbType.Structured;
                            p.TypeName = tvpTypeName;
                            cmd2.ExecuteNonQuery();

                            cmd2.CommandText = string.Format("SELECT f1, sql_variant_property(f1,'BaseType') as BaseType FROM {0}", OutputTableName);
                            cmd2.CommandType = CommandType.Text;
                            using (SqlDataReader dr = cmd2.ExecuteReader())
                            {
                                dr.Read();
                                return new TestResult(dr[0], dr.GetString(1));
                            }
                        }
                    }
                    finally
                    {
                        DropStoredProcedure(conn, ProcName);
                        DropTable(conn, InputTableName);
                        DropTable(conn, OutputTableName);
                        DropType(conn, tvpTypeName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string tvpTypeName = DataTestUtility.GetLongName("tvpVariant_DRdrTVPVar");
                    string InputTableName = DataTestUtility.GetLongName("InputTable");
                    string OutputTableName = DataTestUtility.GetLongName("OutputTable");
                    string ProcName = DataTestUtility.GetLongName("spTVPProc_DRdrTVPVar");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();

                        DropStoredProcedure(conn, ProcName);
                        DropTable(conn, InputTableName);
                        DropTable(conn, OutputTableName);
                        DropType(conn, tvpTypeName);

                        xsql(conn, string.Format("create type {0} as table (f1 sql_variant)", tvpTypeName));
                        xsql(conn, string.Format("create table {0} (f1 sql_variant)", InputTableName));
                        xsql(conn, string.Format("create table {0} (f1 sql_variant)", OutputTableName));

                        string value = string.Empty;
                        if (paramValue.GetType() == typeof(DateTimeOffset))
                        {
                            DateTime dt = ((DateTimeOffset)paramValue).UtcDateTime;
                            value = dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
                        }
                        else if (paramValue.GetType() == typeof(TimeSpan))
                        {
                            value = ((TimeSpan)paramValue).ToString();
                        }
                        else
                        {
                            value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
                        }
                        xsql(conn, string.Format("insert into {0} values(CAST('{1}' AS {2}))", InputTableName, value, expectedBaseTypeName));
                        xsql(conn, string.Format("create proc {0} (@P {1} READONLY) as begin insert into {2} select * from @P; end", ProcName, tvpTypeName, OutputTableName));

                        SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("SELECT * FROM {0}", InputTableName);
                        using SqlDataReader r = cmd.ExecuteReader();
                        using (SqlConnection conn2 = new(connStr))
                        {
                            conn2.Open();
                            using (SqlCommand cmd2 = new(ProcName, conn2))
                            {
                                cmd2.CommandType = CommandType.StoredProcedure;
                                SqlParameter p = cmd2.Parameters.AddWithValue("@P", r);
                                p.SqlDbType = SqlDbType.Structured;
                                p.TypeName = tvpTypeName;
                                cmd2.ExecuteNonQuery();

                                cmd2.CommandText = string.Format("SELECT f1, sql_variant_property(f1,'BaseType') as BaseType FROM {0}", OutputTableName);
                                cmd2.CommandType = CommandType.Text;
                                using (SqlDataReader dr = cmd2.ExecuteReader())
                                {
                                    dr.Read();
                                    return new TestResult(dr[0], dr.GetString(1));
                                }
                            }
                        }
                    }
                    finally
                    {
                        DropStoredProcedure(conn, ProcName);
                        DropTable(conn, InputTableName);
                        DropTable(conn, OutputTableName);
                        DropType(conn, tvpTypeName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string inputTable = DataTestUtility.GetLongName("inputTable");
                    string procName = DataTestUtility.GetLongName("paramProc3");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, inputTable);
                        DropStoredProcedure(conn, procName);

                        string value = string.Empty;
                        if (paramValue.GetType() == typeof(DateTimeOffset))
                        {
                            DateTime dt = ((DateTimeOffset)paramValue).UtcDateTime;
                            value = dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
                        }
                        else if (paramValue.GetType() == typeof(TimeSpan))
                        {
                            value = ((TimeSpan)paramValue).ToString();
                        }
                        else
                        {
                            value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
                        }
                        xsql(conn, string.Format("create table {0} (f1 {1})", inputTable, expectedBaseTypeName));
                        xsql(conn, string.Format("insert into {0}(f1) values('{1}');", inputTable, value));
                        xsql(conn, string.Format("create proc {0} as begin select f1, sql_variant_property(f1,'BaseType') as BaseType from {1} end;", procName, inputTable));

                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = procName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                    finally
                    {
                        DropStoredProcedure(conn, procName);
                        DropTable(conn, inputTable);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string inputTable = DataTestUtility.GetLongName("inputTable");
                    string procName = DataTestUtility.GetLongName("paramProc4");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, inputTable);
                        DropStoredProcedure(conn, procName);

                        string value = string.Empty;
                        if (paramValue.GetType() == typeof(DateTimeOffset))
                        {
                            DateTime dt = ((DateTimeOffset)paramValue).UtcDateTime;
                            value = dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
                        }
                        else if (paramValue.GetType() == typeof(TimeSpan))
                        {
                            value = ((TimeSpan)paramValue).ToString();
                        }
                        else
                        {
                            value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
                        }
                        xsql(conn, string.Format("create table {0} (f1 sql_variant)", inputTable));
                        xsql(conn, string.Format("insert into {0}(f1) values(CAST('{1}' AS {2}));", inputTable, value, expectedBaseTypeName));
                        xsql(conn, string.Format("create proc {0} as begin select f1, sql_variant_property(f1,'BaseType') as BaseType from {1} end;", procName, inputTable));

                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = procName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], dr.GetString(1));
                        }
                    }
                    finally
                    {
                        DropStoredProcedure(conn, procName);
                        DropTable(conn, inputTable);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string bulkCopySrcTableName = DataTestUtility.GetLongName("bulkSrcTable");
                    string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestTable");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, bulkCopyTableName);
                        xsql(conn, string.Format("create table {0} (f1 {1})", bulkCopyTableName, expectedBaseTypeName));

                        DropTable(conn, bulkCopySrcTableName);
                        xsql(conn, string.Format("create table {0} (f1 {1})", bulkCopySrcTableName, expectedBaseTypeName));
                        string value = string.Empty;
                        if (paramValue.GetType() == typeof(DateTimeOffset))
                        {
                            DateTime dt = ((DateTimeOffset)paramValue).UtcDateTime;
                            value = dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
                        }
                        else if (paramValue.GetType() == typeof(TimeSpan))
                        {
                            value = ((TimeSpan)paramValue).ToString();
                        }
                        else
                        {
                            value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
                        }
                        xsql(conn, string.Format("insert into {0}(f1) values(CAST('{1}' AS {2}));", bulkCopySrcTableName, value, expectedBaseTypeName));

                        using SqlConnection connInput = new(connStr);
                        connInput.Open();
                        using (SqlCommand cmdInput = connInput.CreateCommand())
                        {
                            cmdInput.CommandText = string.Format("select * from {0}", bulkCopySrcTableName);
                            using SqlDataReader drInput = cmdInput.ExecuteReader();
                            // Perform bulk copy to target.
                            using (SqlBulkCopy bulkCopy = new(conn))
                            {
                                bulkCopy.BulkCopyTimeout = 60;
                                bulkCopy.BatchSize = 1;
                                bulkCopy.DestinationTableName = bulkCopyTableName;
                                bulkCopy.WriteToServer(drInput);
                            }

                            // Verify target.
                            using SqlCommand cmd = conn.CreateCommand();
                            cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                            using (SqlDataReader drVerify = cmd.ExecuteReader())
                            {
                                drVerify.Read();
                                return new TestResult(drVerify[0], drVerify.GetString(1));
                            }
                        }
                    }
                    finally
                    {
                        DropTable(conn, bulkCopyTableName);
                        DropTable(conn, bulkCopySrcTableName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string bulkCopySrcTableName = DataTestUtility.GetLongName("bulkSrcTable");
                    string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestTable");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, bulkCopyTableName);
                        xsql(conn, string.Format("create table {0} (f1 sql_variant)", bulkCopyTableName));

                        DropTable(conn, bulkCopySrcTableName);
                        xsql(conn, string.Format("create table {0} (f1 {1})", bulkCopySrcTableName, expectedBaseTypeName));
                        string value = string.Empty;
                        if (paramValue.GetType() == typeof(DateTimeOffset))
                        {
                            DateTime dt = ((DateTimeOffset)paramValue).UtcDateTime;
                            value = dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
                        }
                        else if (paramValue.GetType() == typeof(TimeSpan))
                        {
                            value = ((TimeSpan)paramValue).ToString();
                        }
                        else
                        {
                            value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
                        }
                        xsql(conn, string.Format("insert into {0}(f1) values(CAST('{1}' AS {2}));", bulkCopySrcTableName, value, expectedBaseTypeName));

                        using (SqlConnection connInput = new(connStr))
                        {
                            connInput.Open();
                            using (SqlCommand cmdInput = connInput.CreateCommand())
                            {
                                cmdInput.CommandText = string.Format("select * from {0}", bulkCopySrcTableName);
                                using SqlDataReader drInput = cmdInput.ExecuteReader();
                                {
                                    // Perform bulk copy to target.
                                    using (SqlBulkCopy bulkCopy = new(conn))
                                    {
                                        bulkCopy.BulkCopyTimeout = 60;
                                        bulkCopy.BatchSize = 1;
                                        bulkCopy.DestinationTableName = bulkCopyTableName;
                                        bulkCopy.WriteToServer(drInput);
                                    }

                                    // Verify target.
                                    using SqlCommand cmd = conn.CreateCommand();
                                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                                    using (SqlDataReader drVerify = cmd.ExecuteReader())
                                    {
                                        drVerify.Read();
                                        return new TestResult(drVerify[0], drVerify.GetString(1));
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        DropTable(conn, bulkCopyTableName);
                        DropTable(conn, bulkCopySrcTableName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestType");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, bulkCopyTableName);
                        xsql(conn, string.Format("create table {0} (f1 {1})", bulkCopyTableName, expectedBaseTypeName));

                        // Send using DataTable as source.
                        DataTable t = new();
                        t.Columns.Add("f1", paramValue.GetType());
                        t.Rows.Add(new object[] { paramValue });

                        // Perform bulk copy to target.
                        using (SqlBulkCopy bulkCopy = new(conn))
                        {
                            bulkCopy.BulkCopyTimeout = 60;
                            bulkCopy.BatchSize = 1;
                            bulkCopy.DestinationTableName = bulkCopyTableName;
                            bulkCopy.WriteToServer(t, DataRowState.Added);
                        }

                        // Verify target.
                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                        using (SqlDataReader drVerify = cmd.ExecuteReader())
                        {
                            drVerify.Read();
                            return new TestResult(drVerify[0], drVerify.GetString(1));
                        }
                    }
                    finally
                    {
                        DropTable(conn, bulkCopyTableName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestVariant");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, bulkCopyTableName);
                        xsql(conn, string.Format("create table {0} (f1 sql_variant)", bulkCopyTableName));

                        // Send using DataTable as source.
                        DataTable t = new();
                        t.Columns.Add("f1", typeof(object));
                        t.Rows.Add(new object[] { paramValue });

                        // Perform bulk copy to target.
                        using (SqlBulkCopy bulkCopy = new(conn))
                        {
                            bulkCopy.BulkCopyTimeout = 60;
                            bulkCopy.BatchSize = 1;
                            bulkCopy.DestinationTableName = bulkCopyTableName;
                            bulkCopy.WriteToServer(t, DataRowState.Added);
                        }

                        // Verify target.
                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                        using (SqlDataReader drVerify = cmd.ExecuteReader())
                        {
                            drVerify.Read();
                            return new TestResult(drVerify[0], drVerify.GetString(1));
                        }
                    }
                    finally
                    {
                        DropTable(conn, bulkCopyTableName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestType");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, bulkCopyTableName);
                        xsql(conn, string.Format("create table {0} (f1 {1})", bulkCopyTableName, expectedBaseTypeName));
                        DataTable t = new();
                        t.Columns.Add("f1", paramValue.GetType());
                        t.Rows.Add(new object[] { paramValue });
                        DataRow[] rowToSend = t.Select();
                        using (SqlBulkCopy bulkCopy = new(conn))
                        {
                            bulkCopy.BulkCopyTimeout = 60;
                            bulkCopy.BatchSize = 1;
                            bulkCopy.DestinationTableName = bulkCopyTableName;
                            bulkCopy.WriteToServer(rowToSend);
                        }
                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                        using (SqlDataReader drVerify = cmd.ExecuteReader())
                        {
                            drVerify.Read();
                            return new TestResult(drVerify[0], drVerify.GetString(1));
                        }
                    }
                    finally
                    {
                        DropTable(conn, bulkCopyTableName);
                    }
                },
                paramValue, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedValueOverrides,
                expectedBaseTypeOverrides);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
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
                    string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestVariant");

                    using SqlConnection conn = new(connStr);
                    try
                    {
                        conn.Open();
                        DropTable(conn, bulkCopyTableName);
                        xsql(conn, string.Format("create table {0} (f1 sql_variant)", bulkCopyTableName));
                        DataTable t = new();
                        t.Columns.Add("f1", typeof(object));
                        t.Rows.Add(new object[] { paramValue });
                        DataRow[] rowToSend = t.Select();
                        using (SqlBulkCopy bulkCopy = new(conn))
                        {
                            bulkCopy.BulkCopyTimeout = 60;
                            bulkCopy.BatchSize = 1;
                            bulkCopy.DestinationTableName = bulkCopyTableName;
                            bulkCopy.WriteToServer(rowToSend);
                        }
                        using SqlCommand cmd = conn.CreateCommand();
                        cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                        using (SqlDataReader drVerify = cmd.ExecuteReader())
                        {
                            drVerify.Read();
                            return new TestResult(drVerify[0], drVerify.GetString(1));
                        }
                    }
                    finally
                    {
                        DropTable(conn, bulkCopyTableName);
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
        /// Gets parameter combinations as indices for MemberData.
        /// Using indices for xUnit serialization compatibility.
        /// </summary>
        public static IEnumerable<object[]> GetParameterCombinations()
        {
            yield return new object[] { DateTime.MinValue, "date",
                new Dictionary<TestVariations, ExceptionChecker> {
                    { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, 
                new Dictionary<TestVariations, object>(), 
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.MaxValue, "date", 
                new Dictionary<TestVariations, ExceptionChecker>(), 
                new Dictionary<TestVariations, object>
                {
                    { TestVariations.TestSimpleParameter_Type, new DateTime(3155378112000000000) },
                    { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, new DateTime(3155378112000000000) },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, new DateTime(3155378112000000000) },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReader_TVP_Type, new DateTime(3155378112000000000) },
                    { TestVariations.TestSqlDataReader_TVP_Variant, new DateTime(3155378112000000000) },
                    { TestVariations.TestSimpleDataReader_Type, new DateTime(3155378112000000000) },
                    { TestVariations.TestSimpleDataReader_Variant, new DateTime(3155378112000000000) },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, new DateTime(3155378112000000000) },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, new DateTime(3155378112000000000) },
                    { TestVariations.SqlBulkCopyDataTable_Type, new DateTime(3155378112000000000) },
                    { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataRow_Type, new DateTime(3155378112000000000) },
                    { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
                },
                new Dictionary<TestVariations, string>
                {
                    {TestVariations.TestSimpleParameter_Variant, "datetime"},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                }
            };
            yield return new object[] { DateTime.MinValue, "datetime2",
                new Dictionary<TestVariations, ExceptionChecker> {
                    { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, 
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.MaxValue, "datetime2", 
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object> {
                    {TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000)}
                },
                new Dictionary<TestVariations, string>
                {
                    {TestVariations.TestSimpleParameter_Variant, "datetime"},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                }};
            yield return new object[] { DateTime.MinValue, "datetime", 
                new Dictionary<TestVariations, ExceptionChecker> { 
                    { TestVariations.TestSimpleParameter_Type, SqlDateTimeOverflow },
                    { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReader_TVP_Type, VarcharToDateTimeOutOfRange},
                    { TestVariations.TestSqlDataReader_TVP_Variant, VarcharToDateTimeOutOfRange},
                    { TestVariations.TestSimpleDataReader_Type, VarcharToDateTimeOutOfRange},
                    { TestVariations.TestSimpleDataReader_Variant, VarcharToDateTimeOutOfRange},
                    { TestVariations.SqlBulkCopySqlDataReader_Type, VarcharToDateTimeOutOfRange},
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, VarcharToDateTimeOutOfRange}, 
                    { TestVariations.SqlBulkCopyDataTable_Type, SqlDateTimeOverflow},
                    { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow},
                    { TestVariations.SqlBulkCopyDataRow_Type, SqlDateTimeOverflow},
                    { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow}},
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.MaxValue, "datetime", 
                new Dictionary<TestVariations, ExceptionChecker> { 
                    { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime},
                    { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime},
                    { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime},
                    { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime},
                    { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime},
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime}}, 
                new Dictionary<TestVariations, object>
                {
                    { TestVariations.TestSimpleParameter_Type, new DateTime(3155378975999970000) },
                    { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReader_TVP_Type, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReader_TVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSimpleDataReader_Type, new DateTime(3155378975999970000) },
                    { TestVariations.TestSimpleDataReader_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataTable_Type, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataRow_Type, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
                },
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTimeOffset.MinValue, "datetimeoffset",
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTimeOffset.MaxValue, "datetimeoffset",
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTimeOffset.Parse("12/31/1999 23:59:59.9999999 -08:30"), "datetimeoffset",
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.Parse("1998-01-01 23:59:59.995"), "datetime2",
                new Dictionary<TestVariations, ExceptionChecker>(),
                new Dictionary<TestVariations, object>
                {
                    {TestVariations.TestSimpleParameter_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                    {TestVariations.SqlBulkCopyDataTable_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                    {TestVariations.SqlBulkCopyDataRow_Variant, DateTime.Parse("1998-01-01 23:59:59.997")}
                },
                new Dictionary<TestVariations, string>
                {
                    {TestVariations.TestSimpleParameter_Variant, "datetime"},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                }};
            yield return new object[] { DateTime.MinValue, "smalldatetime",
                new Dictionary<TestVariations, ExceptionChecker> {
                    { TestVariations.TestSimpleParameter_Type, SqlDateTimeOverflow },
                    { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReader_TVP_Type, VarcharToSmallDateTimeOutOfRange },
                    { TestVariations.TestSqlDataReader_TVP_Variant, VarcharToSmallDateTimeOutOfRange },
                    { TestVariations.TestSimpleDataReader_Type, VarcharToSmallDateTimeOutOfRange },
                    { TestVariations.TestSimpleDataReader_Variant, VarcharToSmallDateTimeOutOfRange },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, VarcharToSmallDateTimeOutOfRange },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, VarcharToSmallDateTimeOutOfRange },
                    { TestVariations.SqlBulkCopyDataTable_Type, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Type, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }},
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.MaxValue, "smalldatetime",
                new Dictionary<TestVariations, ExceptionChecker> {
                    { TestVariations.TestSimpleParameter_Type, UnRepresentableDateTime },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, UnRepresentableDateTime },
                    { TestVariations.TestSqlDataReader_TVP_Type, ConversionFailedCharStringToSmallDateTime },
                    { TestVariations.TestSqlDataReader_TVP_Variant, ConversionFailedCharStringToSmallDateTime },
                    { TestVariations.TestSimpleDataReader_Type, ConversionFailedCharStringToSmallDateTime },
                    { TestVariations.TestSimpleDataReader_Variant, ConversionFailedCharStringToSmallDateTime },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, ConversionFailedCharStringToSmallDateTime },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, ConversionFailedCharStringToSmallDateTime },
                    { TestVariations.SqlBulkCopyDataTable_Type, UnRepresentableDateTime },
                    { TestVariations.SqlBulkCopyDataRow_Type, UnRepresentableDateTime }}, 
                new Dictionary<TestVariations, object> {
                    { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                    { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
                },
                new Dictionary<TestVariations, string>
                {
                    {TestVariations.TestSimpleParameter_Variant, "datetime"},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                }};
            yield return new object[] { TimeSpan.MinValue, "time",
                new Dictionary<TestVariations, ExceptionChecker> {
                    { TestVariations.TestSimpleParameter_Type, TimeOverflow },
                    { TestVariations.TestSimpleParameter_Variant, TimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, TimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, TimeOverflow },
                    { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.SqlBulkCopyDataTable_Type, TimeOverflow },
                    { TestVariations.SqlBulkCopyDataTable_Variant, TimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Type, TimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Variant, TimeOverflow }},
                new Dictionary<TestVariations, object> {
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, TimeSpan.Zero},
                },
                new Dictionary<TestVariations, string>()};
            yield return new object[] { TimeSpan.MaxValue, "time",
                new Dictionary<TestVariations, ExceptionChecker> {
                    { TestVariations.TestSimpleParameter_Type, TimeOverflow },
                    { TestVariations.TestSimpleParameter_Variant, TimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, TdsRpcProtocolStreamIncorrect },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, TimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, TimeOverflow },
                    { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                    { TestVariations.SqlBulkCopyDataTable_Type, TimeOverflow },
                    { TestVariations.SqlBulkCopyDataTable_Variant, TimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Type, TimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Variant, TimeOverflow }},
                new Dictionary<TestVariations, object>(),
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.MinValue, "time",
                new Dictionary<TestVariations, ExceptionChecker> { 
                    { TestVariations.SqlBulkCopyDataTable_Type, CannotConvertMinDateTimeToTime},
                    { TestVariations.SqlBulkCopyDataRow_Type, CannotConvertMinDateTimeToTime},
                    { TestVariations.TestSimpleParameter_Type, InvalidCastDateTimeToTimeSpan },
                    { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidCastNotValid },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, InvalidCastDateTimeToTimeSpan },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                    { TestVariations.TestSqlDataReader_TVP_Type, InvalidCastNotValid },
                    { TestVariations.TestSqlDataReader_TVP_Variant, InvalidCastNotValid },
                    { TestVariations.TestSimpleDataReader_Type, InvalidCastNotValid },
                    { TestVariations.TestSimpleDataReader_Variant, InvalidCastNotValid },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, InvalidCastNotValid },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, InvalidCastNotValid },
                    { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                    { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, 
                new Dictionary<TestVariations, object> {
                    {TestVariations.SqlBulkCopySqlDataReader_Type, TimeSpan.Zero},
                    {TestVariations.SqlBulkCopySqlDataReader_Variant, TimeSpan.Zero},
                    {TestVariations.TestSqlDataReader_TVP_Type, TimeSpan.Zero},
                    {TestVariations.TestSqlDataReader_TVP_Variant, TimeSpan.Zero},
                    {TestVariations.TestSimpleDataReader_Type, TimeSpan.Zero},
                    {TestVariations.TestSimpleDataReader_Variant, TimeSpan.Zero},
                },
                new Dictionary<TestVariations, string>()};
            yield return new object[] { DateTime.MaxValue, "time",
                new Dictionary<TestVariations, ExceptionChecker> { 
                    { TestVariations.SqlBulkCopyDataTable_Type, CannotConvertMaxDateTimeToTime },
                    { TestVariations.SqlBulkCopyDataRow_Type, CannotConvertMaxDateTimeToTime },
                    { TestVariations.TestSimpleParameter_Type, InvalidCastDateTimeToTimeSpan },
                    { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidCastNotValid },
                    { TestVariations.TestSqlDataReaderParameterToTVP_Type, InvalidCastDateTimeToTimeSpan },
                    { TestVariations.TestSqlDataReader_TVP_Type, InvalidCastNotValid },
                    { TestVariations.TestSqlDataReader_TVP_Variant, InvalidCastNotValid },
                    { TestVariations.TestSimpleDataReader_Type, InvalidCastNotValid },
                    { TestVariations.TestSimpleDataReader_Variant, InvalidCastNotValid },
                    { TestVariations.SqlBulkCopySqlDataReader_Type, InvalidCastNotValid },
                    { TestVariations.SqlBulkCopySqlDataReader_Variant, InvalidCastNotValid }}, 
                new Dictionary<TestVariations, object> {
                    {TestVariations.TestSqlDataReader_TVP_Variant, TimeSpan.Parse("23:59:59.9999999")},
                    {TestVariations.SqlBulkCopySqlDataReader_Type, TimeSpan.Parse("23:59:59.9999999")},
                    {TestVariations.SqlBulkCopySqlDataReader_Variant, TimeSpan.Parse("23:59:59.9999999")},
                    {TestVariations.TestSqlDataReader_TVP_Type, TimeSpan.Parse("23:59:59.9999999")},
                    {TestVariations.TestSimpleDataReader_Type, TimeSpan.Parse("23:59:59.9999999")},
                    {TestVariations.TestSimpleDataReader_Variant, TimeSpan.Parse("23:59:59.9999999")},
                    {TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000)},
                    {TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000)}
                },
                new Dictionary<TestVariations, string>
                {
                    {TestVariations.TestSimpleParameter_Variant, "datetime"},
                    {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                    {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                    {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                }};
        }

        private static ExceptionChecker SqlDateTimeOverflow = (e, paramValue) =>
            (e.GetType() == typeof(System.Data.SqlTypes.SqlTypeException)) &&
            e.Message.Contains("SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.");

        private static ExceptionChecker VarcharToDateTimeOutOfRange = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("The conversion of a varchar data type to a datetime data type resulted in an out-of-range value.");

        private static ExceptionChecker CannotConvertMinDateTimeToTime = (e, paramValue) =>
            (e.GetType() == typeof(InvalidOperationException)) &&
            e.Message.Contains("The given value '1/1/0001 12:00:00 AM' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.");

        private static ExceptionChecker CannotConvertMaxDateTimeToTime = (e, paramValue) =>
            (e.GetType() == typeof(InvalidOperationException)) &&
            e.Message.Contains("The given value '12/31/9999 11:59:59 PM' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.");

        private static ExceptionChecker CannotConvertCharacterStringToDateOrTime = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("Conversion failed when converting date and/or time from character string.");

        private static ExceptionChecker InvalidValueForMetadata = (e, paramValue) =>
            (e.GetType() == typeof(ArgumentException)) &&
            e.Message.Contains("Invalid value for this metadata.");

        private static ExceptionChecker VarcharToSmallDateTimeOutOfRange = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.");

        private static ExceptionChecker ConversionFailedCharStringToSmallDateTime = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("Conversion failed when converting character string to smalldatetime data type.");

        private static ExceptionChecker UnRepresentableDateTime = (e, paramValue) =>
            (e.GetType() == typeof(ArgumentOutOfRangeException)) &&
            e.Message.Contains("The added or subtracted value results in an un-representable DateTime.");

        private static ExceptionChecker TimeOverflow = (e, paramValue) =>
            (e.GetType() == typeof(OverflowException)) &&
            e.Message.Contains("SqlDbType.Time overflow.");

        private static ExceptionChecker InvalidCastDateTimeToTimeSpan = (e, paramValue) =>
            (e.GetType() == typeof(InvalidCastException)) &&
            e.Message.Contains("Failed to convert parameter value from a DateTime to a TimeSpan.");

        private static ExceptionChecker InvalidCastNotValid = (e, paramValue) =>
            (e.GetType() == typeof(InvalidCastException)) &&
            e.Message.Contains("Specified cast is not valid.");

        private static ExceptionChecker TdsRpcProtocolStreamIncorrect = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("The incoming tabular data stream (TDS) remote procedure call (RPC) protocol stream is incorrect.");

    }
}
