// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;
using Xunit;
using Xunit.Sdk;

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

    public delegate bool ExceptionChecker(Exception e, object paramValue);

    public static class DateTimeVariantTest
    {
        public static void SendInfo(
            object paramValue, 
            string expectedTypeName, 
            string expectedBaseTypeName, 
            string connStr, 
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions, 
            Dictionary<TestVariations, ExceptionChecker> expectedInvalidOperationExceptions,
            Dictionary<TestVariations, ExceptionChecker> expectedButUncaughtExceptions)
        {

            List<Tuple<TestVariations, Action<object, string, string, string, string>>> testVariations = new() {
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
                (TestVariations tag, Action<object, string, string, string, string> action) = test;

                DisplayHeader(tag.ToString(), paramValue, expectedBaseTypeName);
                try
                {
                    action(paramValue, expectedTypeName, expectedBaseTypeName, connStr, tag.ToString());
                }
                catch (Exception e)
                {
                    if (expectedExceptions.TryGetValue(tag, out var isExpectedException) && isExpectedException(e, paramValue))
                    {
                        LogMessage(tag.ToString(), "[EXPECTED EXCEPTION] " + e.Message);
                    }
                    else if (expectedInvalidOperationExceptions.TryGetValue(tag, out var isExpectedInvalidOperationException) && isExpectedInvalidOperationException(e, paramValue))
                    {
                        LogMessage(tag.ToString(), "[EXPECTED INVALID OPERATION EXCEPTION] " + AmendTheGivenMessageDateValueException(e.Message, paramValue));
                    }
                    else if (expectedButUncaughtExceptions.TryGetValue(tag, out var isExpectedButUncaughtException) && isExpectedButUncaughtException(e, paramValue))
                    {
                        DisplayError(tag.ToString(), e);
                    }
                    else {
                        Assert.Fail($"Unexpected exception was thrown for test variation {tag} with parameter value {paramValue}. Exception: {e}");
                    }
                }
            }
        }

        public static void _TestSimpleParameter_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string procName = DataTestUtility.GetLongName("paramProc1");

            try {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("Test Simple Parameter [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], string.Empty);
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropStoredProcedure(conn, procName);
            }
        }

        private static void _TestSimpleParameter_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string procName = DataTestUtility.GetLongName("paramProc2");

            try {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("Test Simple Parameter [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], dr.GetString(1));
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropStoredProcedure(conn, procName);
            }
        }

        private static void _TestSqlDataRecordParameterToTVP_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpType");

            try {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("Test SqlDataRecord Parameter To TVP [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], string.Empty);
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropType(conn, tvpTypeName);
            }
        }

        private static void _TestSqlDataRecordParameterToTVP_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            try {
                                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("Test SqlDataRecord Parameter To TVP [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], dr.GetString(1));
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropType(conn, tvpTypeName);
            }
        }

        private static void _TestSqlDataReaderParameterToTVP_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpType");

            try {
                using SqlConnection conn = new(connStr);
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
                            VerifyReaderTypeAndValue("Test SqlDataReader Parameter To TVP [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], string.Empty);
                        }
                    }
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropType(conn, tvpTypeName);
            }
        }

        private static void _TestSqlDataReaderParameterToTVP_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            try {
                using SqlConnection conn = new(connStr);
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
                            VerifyReaderTypeAndValue("Test SqlDataReader Parameter To TVP [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], dr.GetString(1));
                        }
                    }
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropType(conn, tvpTypeName);
            }
        }

        private static void _TestSqlDataReader_TVP_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpType");
            string InputTableName = DataTestUtility.GetLongName("InputTable");
            string OutputTableName = DataTestUtility.GetLongName("OutputTable");
            string ProcName = DataTestUtility.GetLongName("spTVPProc");
            try
            {
                using SqlConnection conn = new(connStr);
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
                        VerifyReaderTypeAndValue("Test SqlDataReader TVP [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], string.Empty);
                    }
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropStoredProcedure(conn, ProcName);
                DropTable(conn, InputTableName);
                DropTable(conn, OutputTableName);
                DropType(conn, tvpTypeName);
            }
        }

        private static void _TestSqlDataReader_TVP_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant_DRdrTVPVar");
            string InputTableName = DataTestUtility.GetLongName("InputTable");
            string OutputTableName = DataTestUtility.GetLongName("OutputTable");
            string ProcName = DataTestUtility.GetLongName("spTVPProc_DRdrTVPVar");
            try
            {
                using SqlConnection conn = new(connStr);
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
                        ;
                        cmd2.CommandType = CommandType.Text;
                        using (SqlDataReader dr = cmd2.ExecuteReader())
                        {
                            dr.Read();
                            VerifyReaderTypeAndValue("Test SqlDataReader TVP [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], dr.GetString(1));
                        }
                    }
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropStoredProcedure(conn, ProcName);
                DropTable(conn, InputTableName);
                DropTable(conn, OutputTableName);
                DropType(conn, tvpTypeName);
            }
        }

        private static void _TestSimpleDataReader_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string inputTable = DataTestUtility.GetLongName("inputTable");
            string procName = DataTestUtility.GetLongName("paramProc3");
            try
            {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("Test Simple Data Reader [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], string.Empty);
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropStoredProcedure(conn, procName);
                DropTable(conn, inputTable);
            }
        }

        private static void _TestSimpleDataReader_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string inputTable = DataTestUtility.GetLongName("inputTable");
            string procName = DataTestUtility.GetLongName("paramProc4");
            try
            {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("Test Simple Data Reader [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, dr[0], dr.GetString(1));
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropStoredProcedure(conn, procName);
                DropTable(conn, inputTable);
            }
        }

        private static void _SqlBulkCopySqlDataReader_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string bulkCopySrcTableName = DataTestUtility.GetLongName("bulkSrcTable");
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestTable");
            try
            {
                using SqlConnection conn = new(connStr);
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
                        VerifyReaderTypeAndValue("SqlBulkCopy From SqlDataReader [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, drVerify[0], string.Empty);
                    }
                }
                connInput.Close();
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropTable(conn, bulkCopyTableName);
                DropTable(conn, bulkCopySrcTableName);
            }
        }

        private static void _SqlBulkCopySqlDataReader_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string bulkCopySrcTableName = DataTestUtility.GetLongName("bulkSrcTable");
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestTable");
            try
            {
                using SqlConnection conn = new(connStr);
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
                                VerifyReaderTypeAndValue("SqlBulkCopy From SqlDataReader [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, drVerify[0], drVerify.GetString(1));
                            }
                        }
                    }
                }

                conn.Close();
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropTable(conn, bulkCopyTableName);
                DropTable(conn, bulkCopySrcTableName);
            }
        }

        private static void _SqlBulkCopyDataTable_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestType");
            try
            {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("SqlBulkCopy From Data Table [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, drVerify[0], string.Empty);
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropTable(conn, bulkCopyTableName);
            }
        }

        private static void _SqlBulkCopyDataTable_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestVariant");
            try
            {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("SqlBulkCopy From Data Table [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, drVerify[0], drVerify.GetString(1));
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropTable(conn, bulkCopyTableName);
            }
        }

        private static void _SqlBulkCopyDataRow_Type(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestType");
            try
            {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("SqlBulkCopy From Data Row [Data Type]", expectedBaseTypeName, expectedTypeName, paramValue, drVerify[0], string.Empty);
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
                DropTable(conn, bulkCopyTableName);
            }
        }

        private static void _SqlBulkCopyDataRow_Variant(object paramValue, string expectedTypeName, string expectedBaseTypeName, string connStr, string tag) {
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDestVariant");
            try
            {
                using SqlConnection conn = new(connStr);
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
                    VerifyReaderTypeAndValue("SqlBulkCopy From Data Row [Variant Type]", expectedBaseTypeName, expectedTypeName, paramValue, drVerify[0], drVerify.GetString(1));
                }
            }
            finally
            {
                using SqlConnection conn = new(connStr);
                conn.Open();
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

        private static void VerifyReaderTypeAndValue(string tag, string expectedBaseTypeName, string expectedTypeName, object expectedValue, object actualValue, string actualBaseTypeName)
        {
            string actualTypeName = actualValue.GetType().ToString();

            LogValues(expectedTypeName, string.IsNullOrEmpty(actualBaseTypeName) ? string.Empty : expectedBaseTypeName, expectedValue, actualTypeName, actualBaseTypeName, actualValue);

            if (!actualTypeName.Equals(expectedTypeName))
            {
                string ErrorMessage = string.Format(">>> ERROR: TYPE MISMATCH!!! [Actual = {0}] [Expected = {1}]",
                    actualTypeName,
                    expectedTypeName);
                LogMessage(tag, ErrorMessage);
            }

            if (!string.IsNullOrEmpty(actualBaseTypeName) && 
                !string.IsNullOrEmpty(expectedBaseTypeName) && 
                !actualBaseTypeName.Equals(expectedBaseTypeName))
            {
                string ErrorMessage = string.Format(">>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = {0}] [Expected = {1}]",
                    actualBaseTypeName,
                    expectedBaseTypeName);
                LogMessage(tag, ErrorMessage);
            }

            if (!actualValue.Equals(expectedValue))
            {
                string ErrorMessage;
                bool isExpected;
                switch (expectedBaseTypeName)
                {
                    case "date":
                        isExpected = ((DateTime)expectedValue).Date.Equals(((DateTime)actualValue).Date);
                        break;
                    case "datetime":
                        isExpected = (((DateTime)expectedValue).Ticks == 3155378975999999999) &&
                            (((DateTime)actualValue).Ticks == 3155378975999970000);
                        break;
                    default:
                        isExpected = false;
                        break;
                }
                        
                if (isExpected)
                {
                    ErrorMessage = string.Format("[EXPECTED ERROR]: VALUE MISMATCH - [Actual = {0}] [Expected = {1}]",
                    DataTestUtility.GetValueString(actualValue),
                    DataTestUtility.GetValueString(expectedValue));
                }
                else
                {
                    ErrorMessage = string.Format(">>> ERROR: VALUE MISMATCH!!! [Actual = {0}] [Expected = {1}]",
                    DataTestUtility.GetValueString(actualValue),
                    DataTestUtility.GetValueString(expectedValue));
                }
                LogMessage(tag, ErrorMessage);
            }
        }

        private static string AmendTheGivenMessageDateValueException(string message, object paramValue)
        {
            string value;
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

            return message.Replace(paramValue.ToString(), value);
        }

        // NOTE: Logging and Display
        private static void DisplayHeader(string tag, object paramValue, string expectedBaseTypeName)
        {
            Console.WriteLine("");
            string value;
            if (paramValue.GetType() == typeof(DateTimeOffset))
            {
                DateTimeOffset dt = (DateTimeOffset)paramValue;
                value = dt.DateTime.ToString("M/d/yyyy") + " " + dt.DateTime.TimeOfDay + " " + dt.Offset;
            }
            else if (paramValue.GetType() == typeof(TimeSpan))
            {
                value = ((TimeSpan)paramValue).ToString();
            }
            else
            {
                value = ((DateTime)paramValue).ToString("M/d/yyyy") + " " + ((DateTime)paramValue).TimeOfDay;
            }

            Console.WriteLine(string.Format("------------------------------ {0} [type: {1} value:{2}] ------------------------------", tag, expectedBaseTypeName, value));
        }

        private static void DisplayError(string tag, Exception e)
        {
            string ExceptionMessage = string.Format(">>> EXCEPTION: [{0}] {1}", e.GetType(), e.Message);

            // InvalidCastException message is different between core and framework for this cast attempt.
            // Make them the same for the purposes of comparing test output.
            if (e is InvalidCastException &&
                e.Message.Equals("Unable to cast object of type 'System.TimeSpan' to type 'System.DateTime'."))
            {
                ExceptionMessage = string.Format(">>> EXCEPTION: [{0}] {1}", e.GetType(), "Specified cast is not valid.");
            }

            if (e is ArgumentOutOfRangeException &&
                e.Message.Equals("The added or subtracted value results in an un-representable DateTime. (Parameter 'value')"))
            {
                ExceptionMessage = string.Format(">>> EXCEPTION: [{0}] {1}", e.GetType(), "The added or subtracted value results in an un-representable DateTime.\nParameter name: value");
            }

            LogMessage(tag, ExceptionMessage);
        }

        private static void LogMessage(string tag, string message)
        {
            Console.WriteLine(string.Format("{0}{1}", tag, message));
        }

        private static void LogValues(string expectedTypeName, string expectedBaseTypeName, object expectedValue, string actualTypeName, string actualBaseTypeName, object actualValue)
        {
            Console.WriteLine(string.Format("Type        => Expected : Actual == {0} : {1}", expectedTypeName, actualTypeName));
            Console.WriteLine(string.Format("Base Type   => Expected : Actual == {0} : {1}", expectedBaseTypeName, actualBaseTypeName));
            if (expectedTypeName == "System.DateTimeOffset")
            {
                Console.WriteLine(string.Format("Value       => Expected : Actual == {0} : {1}", ((DateTimeOffset)expectedValue).Ticks.ToString(), ((DateTimeOffset)actualValue).Ticks.ToString()));
            }
            else if (expectedTypeName == "System.DateTime")
            {
                Console.WriteLine(string.Format("Value       => Expected : Actual == {0} : {1}", ((DateTime)expectedValue).Ticks.ToString(), ((DateTime)actualValue).Ticks.ToString()));
            }
        }
    }
}
