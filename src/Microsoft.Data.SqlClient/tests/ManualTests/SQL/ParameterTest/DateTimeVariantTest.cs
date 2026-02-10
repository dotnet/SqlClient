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

    public static class DateTimeVariantTest
    {
        public static void SendInfo(
            object paramValue, 
            string expectedTypeName, 
            string expectedBaseTypeName, 
            string connStr, 
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions, 
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, object> expectedBaseTypeOverrides)
        {

            List<Tuple<TestVariations, Func<object, string, string, string, string, TestResult>>> testVariations = new() {
                new(TestVariations.TestSimpleParameter_Type, _TestSimpleParameter_Type),
                new(TestVariations.TestSimpleParameter_Variant, _TestSimpleParameter_Variant),
                new(TestVariations.TestSqlDataRecordParameterToTVP_Type, _TestSqlDataRecordParameterToTVP_Type),
                new(TestVariations.TestSqlDataRecordParameterToTVP_Variant, _TestSqlDataRecordParameterToTVP_Variant),
                new(TestVariations.TestSqlDataReaderParameterToTVP_Type, _TestSqlDataReaderParameterToTVP_Type),
                new(TestVariations.TestSqlDataReaderParameterToTVP_Variant, _TestSqlDataReaderParameterToTVP_Variant),
                new(TestVariations.TestSqlDataReader_TVP_Type, _TestSqlDataReader_TVP_Type),
                new(TestVariations.TestSqlDataReader_TVP_Variant, _TestSqlDataReader_TVP_Variant),
                new(TestVariations.TestSimpleDataReader_Type, _TestSimpleDataReader_Type),
                new(TestVariations.TestSimpleDataReader_Variant, _TestSimpleDataReader_Variant),
                new(TestVariations.SqlBulkCopySqlDataReader_Type, _SqlBulkCopySqlDataReader_Type),
                new(TestVariations.SqlBulkCopySqlDataReader_Variant, _SqlBulkCopySqlDataReader_Variant),
                new(TestVariations.SqlBulkCopyDataTable_Type, _SqlBulkCopyDataTable_Type),
                new(TestVariations.SqlBulkCopyDataTable_Variant, _SqlBulkCopyDataTable_Variant),
                new(TestVariations.SqlBulkCopyDataRow_Type, _SqlBulkCopyDataRow_Type),
                new(TestVariations.SqlBulkCopyDataRow_Variant, _SqlBulkCopyDataRow_Variant)
            };

            foreach (var test in testVariations)
            {
                (TestVariations tag, Func<object, string, string, string, string, TestResult> action) = test;

                try
                {
                    TestResult result = action(paramValue, expectedTypeName, expectedBaseTypeName, connStr, tag.ToString());
                    expectedValueOverrides.TryGetValue(tag, out var expectedValueOverride);
                    expectedBaseTypeOverrides.TryGetValue(tag, out var expectedBaseTypeOverride);
                    VerifyReaderTypeAndValue(expectedBaseTypeName, expectedTypeName, paramValue, result.Value, result.BaseTypeName, expectedValueOverride, expectedBaseTypeOverride);
                }
                catch (Exception e)
                {
                    if (expectedExceptions.TryGetValue(tag, out var isExpectedException))
                    {
                        Assert.True(isExpectedException(e, paramValue));
                    }
                    else {
                        Assert.Fail($"Unexpected exception was thrown for test variation {tag} with parameter value {paramValue}. Exception: {e}");
                    }
                }
            }
        }

        public static TestResult _TestSimpleParameter_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string procName = DataTestUtility.GetLongName("paramProc1");
            
            using SqlConnection conn = new(connStr);
            try {
                
                conn.Open();
                DropStoredProcedure(conn, procName);
                xsql(conn, string.Format("create proc {0} (@param {1}) as begin select @param end;", procName, expectedBaseTypeName));

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = procName;
                cmd.CommandType = CommandType.StoredProcedure;
                SqlParameter p = cmd.Parameters.AddWithValue("@param", paramValue);
                cmd.Parameters[0].SqlDbType = GetSqlDbType(expectedBaseTypeName);
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    dr.Read();
                    return new TestResult(dr[0], string.Empty);
                }
            }
            finally
            {
                DropStoredProcedure(conn, procName);
            }
        }

        private static TestResult _TestSimpleParameter_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string procName = DataTestUtility.GetLongName("paramProc2");

            using SqlConnection conn = new(connStr);
            try {
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
        }

        private static TestResult _TestSqlDataRecordParameterToTVP_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpType");

            using SqlConnection conn = new(connStr);
            try {
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
                cmd.CommandText = "select f1 from @tvpParam";
                SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", record);
                p.SqlDbType = SqlDbType.Structured;
                p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    dr.Read();
                    return new TestResult(dr[0], string.Empty);
                }
            }
            finally
            {
                DropType(conn, tvpTypeName);
            }
        }

        private static TestResult _TestSqlDataRecordParameterToTVP_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            using SqlConnection conn = new(connStr);
            try {
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
        }

        private static TestResult _TestSqlDataReaderParameterToTVP_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpType");

            using SqlConnection conn = new(connStr);
            try {
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
                        cmd.CommandText = "select f1 from @tvpParam";
                        SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", drInput);
                        p.SqlDbType = SqlDbType.Structured;
                        p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            dr.Read();
                            return new TestResult(dr[0], string.Empty);
                        }
                    }
                }
            }
            finally
            {
                DropType(conn, tvpTypeName);
            }
        }

        private static TestResult _TestSqlDataReaderParameterToTVP_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            using SqlConnection conn = new(connStr);
            try {
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
        }

        private static TestResult _TestSqlDataReader_TVP_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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

                    cmd2.CommandText = string.Format("SELECT f1 FROM {0}", OutputTableName);
                    cmd2.CommandType = CommandType.Text;
                    using (SqlDataReader dr = cmd2.ExecuteReader())
                    {
                        dr.Read();
                        return new TestResult(dr[0], string.Empty);
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
        }

        private static TestResult _TestSqlDataReader_TVP_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
        }

        private static TestResult _TestSimpleDataReader_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
                xsql(conn, string.Format("create proc {0} as begin select f1 from {1} end;", procName, inputTable));

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = procName;
                cmd.CommandType = CommandType.StoredProcedure;
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    dr.Read();
                    return new TestResult(dr[0], string.Empty);
                }
            }
            finally
            {
                DropStoredProcedure(conn, procName);
                DropTable(conn, inputTable);
            }
        }

        private static TestResult _TestSimpleDataReader_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
        }

        private static TestResult _SqlBulkCopySqlDataReader_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
                    cmd.CommandText = string.Format("select f1 from {0}", bulkCopyTableName);
                    using (SqlDataReader drVerify = cmd.ExecuteReader())
                    {
                        drVerify.Read();
                        return new TestResult(drVerify[0], string.Empty);
                    }
                }
            }
            finally
            {
                DropTable(conn, bulkCopyTableName);
                DropTable(conn, bulkCopySrcTableName);
            }
        }

        private static TestResult _SqlBulkCopySqlDataReader_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
        }

        private static TestResult _SqlBulkCopyDataTable_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
                cmd.CommandText = string.Format("select f1 from {0}", bulkCopyTableName);
                using (SqlDataReader drVerify = cmd.ExecuteReader())
                {
                    drVerify.Read();
                    return new TestResult(drVerify[0], string.Empty);
                }
            }
            finally
            {
                DropTable(conn, bulkCopyTableName);
            }
        }

        private static TestResult _SqlBulkCopyDataTable_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
        }

        private static TestResult _SqlBulkCopyDataRow_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
                cmd.CommandText = string.Format("select f1 from {0}", bulkCopyTableName);
                using (SqlDataReader drVerify = cmd.ExecuteReader())
                {
                    drVerify.Read();
                    return new TestResult(drVerify[0], string.Empty);
                }
            }
            finally
            {
                DropTable(conn, bulkCopyTableName);
            }
        }

        private static TestResult _SqlBulkCopyDataRow_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
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
        }


        // NOTE: Actions

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

        private static void VerifyReaderTypeAndValue(
            string expectedBaseTypeName, 
            string expectedTypeName, 
            object expectedValue, 
            object actualValue, 
            string actualBaseTypeName, 
            object expectedValueOverride, 
            object expectedBaseTypeOverride)
        {
            string actualTypeName = actualValue.GetType().ToString();

            //TODO: these are required to generate expected cast exceptions and should be removed
            if (expectedTypeName == "System.DateTime")
            {
#pragma warning disable IDE0059 // Unnecessary assignment of a value
                string a = ((DateTime)expectedValue).Ticks.ToString(), b = ((DateTime)actualValue).Ticks.ToString();
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            }

            Assert.Equal(expectedTypeName, actualTypeName);

            //TODO: pass in actualBaseType for non-variant tests to remove these IsNullOrEmpty conditionals
            if (!string.IsNullOrEmpty(actualBaseTypeName) && 
                !string.IsNullOrEmpty(expectedBaseTypeName) && 
                !actualBaseTypeName.Equals(expectedBaseTypeName))
            {
                if (expectedBaseTypeOverride is not null)
                {
                    Assert.Equal(expectedBaseTypeOverride, actualBaseTypeName);
                }
                else
                {
                    Assert.Equal(expectedBaseTypeName, actualBaseTypeName);
                }
            }

            if (!actualValue.Equals(expectedValue))
            {                        
                if (expectedValueOverride is not null)
                {
                    Assert.True(actualValue.Equals(expectedValueOverride));
                }
                else {
                    Assert.Fail();
                }
            }
        }
    }
}
