// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for SQL Variant parameters.
    /// These tests run independently with their own baseline comparison.
    /// </summary>
    [Collection("ParameterBaselineTests")]
    public class SqlVariantParameterTests
    {
        private readonly string _connStr;

        public SqlVariantParameterTests()
        {
            _connStr = DataTestUtility.TCPConnectionString;
        }

        [Trait("Category", "flaky")]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void SqlVariantParameterTest()
        {
            Assert.True(RunTestAndCompareWithBaseline());
        }

        private bool RunTestAndCompareWithBaseline()
        {
            CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            try
            {
            string outputPath = "SqlVariantParameter.out";
            string baselinePath;
#if DEBUG
            if (DataTestUtility.IsNotAzureServer() || DataTestUtility.IsManagedInstance)
            {
                baselinePath = "SqlVariantParameter_DebugMode.bsl";
            }
            else
            {
                baselinePath = "SqlVariantParameter_DebugMode_Azure.bsl";
            }
#else
            if (DataTestUtility.IsNotAzureServer() || DataTestUtility.IsManagedInstance)
            {
                baselinePath = "SqlVariantParameter_ReleaseMode.bsl";
            }
            else
            {
                baselinePath = "SqlVariantParameter_ReleaseMode_Azure.bsl";
            }
#endif

            var fstream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var swriter = new StreamWriter(fstream, Encoding.UTF8);
            var twriter = new TvpTest.CarriageReturnLineFeedReplacer(swriter);
            Console.SetOut(twriter);

            // Run Test
            SendAllSqlTypesInsideVariant();

            Console.Out.Flush();
            Console.Out.Dispose();

            // Recover the standard output stream
            StreamWriter standardOutput = new(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);

            // Compare output file
            var comparisonResult = FindDiffFromBaseline(baselinePath, outputPath);

            if (string.IsNullOrEmpty(comparisonResult))
            {
                return true;
            }

            Console.WriteLine("SqlVariantParameterTest Failed!");
            Console.WriteLine("Please compare baseline: {0} with output: {1}", Path.GetFullPath(baselinePath), Path.GetFullPath(outputPath));
            Console.WriteLine("Comparison Results:");
            Console.WriteLine(comparisonResult);
            return false;
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }
        }

        private static string FindDiffFromBaseline(string baselinePath, string outputPath)
        {
            var expectedLines = File.ReadAllLines(baselinePath);
            var outputLines = File.ReadAllLines(outputPath);

            var comparisonSb = new StringBuilder();

            var expectedLength = expectedLines.Length;
            var outputLength = outputLines.Length;
            var findDiffLength = Math.Min(expectedLength, outputLength);

            for (var lineNo = 0; lineNo < findDiffLength; lineNo++)
            {
                if (!expectedLines[lineNo].Equals(outputLines[lineNo]))
                {
                    comparisonSb.AppendFormat("** DIFF at line {0} \n", lineNo);
                    comparisonSb.AppendFormat("A : {0} \n", outputLines[lineNo]);
                    comparisonSb.AppendFormat("E : {0} \n", expectedLines[lineNo]);
                }
            }

            var startIndex = findDiffLength - 1;
            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (findDiffLength < expectedLength)
            {
                comparisonSb.AppendFormat("** MISSING \n");
                for (var lineNo = startIndex; lineNo < expectedLength; lineNo++)
                {
                    comparisonSb.AppendFormat("{0} : {1}", lineNo, expectedLines[lineNo]);
                }
            }
            if (findDiffLength < outputLength)
            {
                comparisonSb.AppendFormat("** EXTRA \n");
                for (var lineNo = startIndex; lineNo < outputLength; lineNo++)
                {
                    comparisonSb.AppendFormat("{0} : {1}", lineNo, outputLines[lineNo]);
                }
            }

            return comparisonSb.ToString();
        }

        /// <summary>
        /// Tests all SqlTypes inside sql_variant to server using sql_variant parameter, SqlBulkCopy, and TVP parameter with sql_variant inside.
        /// </summary>
        private void SendAllSqlTypesInsideVariant()
        {
            Console.WriteLine("");
            Console.WriteLine("Starting test 'SqlVariantParam'");
            SendVariant(new SqlSingle((float)123.45), "System.Data.SqlTypes.SqlSingle", "real");
            SendVariant(new SqlSingle((double)123.45), "System.Data.SqlTypes.SqlSingle", "real");
            SendVariant(new SqlString("hello"), "System.Data.SqlTypes.SqlString", "nvarchar");
            SendVariant(new SqlDouble((double)123.45), "System.Data.SqlTypes.SqlDouble", "float");
            SendVariant(new SqlBinary(new byte[] { 0x00, 0x11, 0x22 }), "System.Data.SqlTypes.SqlBinary", "varbinary");
            SendVariant(new SqlGuid(Guid.NewGuid()), "System.Data.SqlTypes.SqlGuid", "uniqueidentifier");
            SendVariant(new SqlBoolean(true), "System.Data.SqlTypes.SqlBoolean", "bit");
            SendVariant(new SqlBoolean(1), "System.Data.SqlTypes.SqlBoolean", "bit");
            SendVariant(new SqlByte(1), "System.Data.SqlTypes.SqlByte", "tinyint");
            SendVariant(new SqlInt16(1), "System.Data.SqlTypes.SqlInt16", "smallint");
            SendVariant(new SqlInt32(1), "System.Data.SqlTypes.SqlInt32", "int");
            SendVariant(new SqlInt64(1), "System.Data.SqlTypes.SqlInt64", "bigint");
            SendVariant(new SqlDecimal(1234.123M), "System.Data.SqlTypes.SqlDecimal", "numeric");
            SendVariant(new SqlDateTime(DateTime.Now), "System.Data.SqlTypes.SqlDateTime", "datetime");
            SendVariant(new SqlMoney(123.123M), "System.Data.SqlTypes.SqlMoney", "money");
            Console.WriteLine("End test 'SqlVariantParam'");
        }

        /// <summary>
        /// Returns a SqlDataReader with embedded sql_variant column with paramValue inside.
        /// </summary>
        private SqlDataReader GetReaderForVariant(object paramValue, bool includeBaseType)
        {
            SqlConnection conn = new(_connStr);
            conn.Open();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "select @p1 as f1";
            if (includeBaseType)
            {
                cmd.CommandText += ", sql_variant_property(@p1,'BaseType') as BaseType";
            }

            cmd.Parameters.Add("@p1", SqlDbType.Variant);
            cmd.Parameters["@p1"].Value = paramValue;
            SqlDataReader dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return dr;
        }

        /// <summary>
        /// Verifies if SqlDataReader returns expected SqlType and base type.
        /// </summary>
        private static void VerifyReader(string tag, SqlDataReader dr, string expectedTypeName, string expectedBaseTypeName)
        {
            dr.Read();
            string actualTypeName = dr.GetSqlValue(0).GetType().ToString();
            string actualBaseTypeName = dr.GetString(1);
            Console.WriteLine("{0,-40} -> {1}:{2}", tag, actualTypeName, actualBaseTypeName);
            if (!actualTypeName.Equals(expectedTypeName))
            {
                Console.WriteLine("   --> ERROR: Expected type {0} does not match actual type {1}",
                    expectedTypeName, actualTypeName);
            }
            if (!actualBaseTypeName.Equals(expectedBaseTypeName))
            {
                Console.WriteLine("   --> ERROR: Expected base type {0} does not match actual base type {1}",
                    expectedBaseTypeName, actualBaseTypeName);
            }
        }

        /// <summary>
        /// Round trips a sql_variant to server and verifies result.
        /// </summary>
        private void SendVariant(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            SendVariantBulkCopy(paramValue, expectedTypeName, expectedBaseTypeName);
            SendVariantParam(paramValue, expectedTypeName, expectedBaseTypeName);
            SendVariantTvp(paramValue, expectedTypeName, expectedBaseTypeName);
        }

        /// <summary>
        /// Round trip sql_variant value as normal parameter.
        /// </summary>
        private void SendVariantParam(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            using SqlDataReader dr = GetReaderForVariant(paramValue, true);
            VerifyReader("SendVariantParam", dr, expectedTypeName, expectedBaseTypeName);
        }

        /// <summary>
        /// Round trip sql_variant value using SqlBulkCopy.
        /// </summary>
        private void SendVariantBulkCopy(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string bulkCopyTableName = DataTestUtility.GetLongName("bulkDest");

            using SqlDataReader dr = GetReaderForVariant(paramValue, false);
            using SqlConnection connBulk = new(_connStr);
            connBulk.Open();

            ExecuteSQL(connBulk, "create table dbo.{0} (f1 sql_variant)", bulkCopyTableName);
            try
            {
                using (SqlBulkCopy bulkCopy = new(connBulk))
                {
                    bulkCopy.BulkCopyTimeout = 60;
                    bulkCopy.BatchSize = 1;
                    bulkCopy.DestinationTableName = bulkCopyTableName;
                    bulkCopy.WriteToServer(dr);
                }

                using (SqlCommand cmd = connBulk.CreateCommand())
                {
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                    using SqlDataReader drVerify = cmd.ExecuteReader();
                    VerifyReader("SendVariantBulkCopy[SqlDataReader]", drVerify, expectedTypeName, expectedBaseTypeName);
                }

                ExecuteSQL(connBulk, "truncate table {0}", bulkCopyTableName);

                DataTable t = new();
                t.Columns.Add("f1", typeof(object));
                t.Rows.Add(new object[] { paramValue });

                using (SqlBulkCopy bulkCopy = new(connBulk))
                {
                    bulkCopy.BulkCopyTimeout = 60;
                    bulkCopy.BatchSize = 1;
                    bulkCopy.DestinationTableName = bulkCopyTableName;
                    bulkCopy.WriteToServer(t, DataRowState.Added);
                }

                using (SqlCommand cmd = connBulk.CreateCommand())
                {
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                    using SqlDataReader drVerify = cmd.ExecuteReader();
                    VerifyReader("SendVariantBulkCopy[DataTable]", drVerify, expectedTypeName, expectedBaseTypeName);
                }

                ExecuteSQL(connBulk, "truncate table {0}", bulkCopyTableName);

                DataRow[] rowToSend = t.Select();

                using (SqlBulkCopy bulkCopy = new(connBulk))
                {
                    bulkCopy.BulkCopyTimeout = 60;
                    bulkCopy.BatchSize = 1;
                    bulkCopy.DestinationTableName = bulkCopyTableName;
                    bulkCopy.WriteToServer(rowToSend);
                }

                using (SqlCommand cmd = connBulk.CreateCommand())
                {
                    cmd.CommandText = string.Format("select f1, sql_variant_property(f1,'BaseType') as BaseType from {0}", bulkCopyTableName);
                    using SqlDataReader drVerify = cmd.ExecuteReader();
                    VerifyReader("SendVariantBulkCopy[DataRow]", drVerify, expectedTypeName, expectedBaseTypeName);
                }
            }
            finally
            {
                ExecuteSQL(connBulk, "drop table {0}", bulkCopyTableName);
            }
        }

        /// <summary>
        /// Round trip sql_variant value using TVP.
        /// </summary>
        private void SendVariantTvp(object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string tvpTypeName = DataTestUtility.GetLongName("tvpVariant");

            using SqlConnection connTvp = new(_connStr);
            connTvp.Open();

            ExecuteSQL(connTvp, "create type dbo.{0} as table (f1 sql_variant)", tvpTypeName);
            try
            {
                SqlMetaData[] metadata = new SqlMetaData[1];
                metadata[0] = new SqlMetaData("f1", SqlDbType.Variant);
                SqlDataRecord[] record = new SqlDataRecord[1];
                record[0] = new SqlDataRecord(metadata);
                record[0].SetValue(0, paramValue);

                using (SqlCommand cmd = connTvp.CreateCommand())
                {
                    cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                    SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", record);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                    using SqlDataReader dr = cmd.ExecuteReader();
                    VerifyReader("SendVariantTvp[SqlMetaData]", dr, expectedTypeName, expectedBaseTypeName);
                }

                using (SqlDataReader dr = GetReaderForVariant(paramValue, false))
                {
                    using SqlCommand cmd = connTvp.CreateCommand();
                    cmd.CommandText = "select f1, sql_variant_property(f1,'BaseType') as BaseType from @tvpParam";
                    SqlParameter p = cmd.Parameters.AddWithValue("@tvpParam", dr);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = string.Format("dbo.{0}", tvpTypeName);
                    using SqlDataReader dr2 = cmd.ExecuteReader();
                    VerifyReader("SendVariantTvp[SqlDataReader]", dr2, expectedTypeName, expectedBaseTypeName);
                }
            }
            finally
            {
                ExecuteSQL(connTvp, "drop type {0}", tvpTypeName);
            }
        }

        /// <summary>
        /// Helper to execute t-sql with variable object name.
        /// </summary>
        private static void ExecuteSQL(SqlConnection conn, string formatSql, string objectName)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = string.Format(formatSql, objectName);
            cmd.ExecuteNonQuery();
        }
    }
}
