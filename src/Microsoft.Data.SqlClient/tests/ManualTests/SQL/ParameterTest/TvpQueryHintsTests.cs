// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for TVP query hints (sort order, uniqueness, default columns).
    /// These tests run independently with their own baseline comparison.
    /// </summary>
    [Collection("ParameterBaselineTests")]
    public class TvpQueryHintsTests
    {
        [Trait("Category", "flaky")]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void TvpQueryHintsTest()
        {
            Assert.True(RunTestAndCompareWithBaseline());
        }

        private bool RunTestAndCompareWithBaseline()
        {
            CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            try
            {
            string outputPath = "TvpQueryHints.out";
            string baselinePath;
#if DEBUG
            if (DataTestUtility.IsNotAzureServer() || DataTestUtility.IsManagedInstance)
            {
                baselinePath = "TvpQueryHints_DebugMode.bsl";
            }
            else
            {
                baselinePath = "TvpQueryHints_DebugMode_Azure.bsl";
            }
#else
            if (DataTestUtility.IsNotAzureServer() || DataTestUtility.IsManagedInstance)
            {
                baselinePath = "TvpQueryHints_ReleaseMode.bsl";
            }
            else
            {
                baselinePath = "TvpQueryHints_ReleaseMode_Azure.bsl";
            }
#endif

            var fstream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var swriter = new StreamWriter(fstream, Encoding.UTF8);
            var twriter = new TvpTest.CarriageReturnLineFeedReplacer(swriter);
            Console.SetOut(twriter);

            // Run Test
            QueryHintsTest();

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

            Console.WriteLine("TvpQueryHintsTest Failed!");
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

        private void QueryHintsTest()
        {
            using SqlConnection conn = new(DataTestUtility.TCPConnectionString);
            conn.Open();

            Guid randomizer = Guid.NewGuid();
            string typeName = string.Format("dbo.[QHint_{0}]", randomizer);
            string procName = string.Format("dbo.[QHint_Proc_{0}]", randomizer);
            string createTypeSql = string.Format(
                    "CREATE TYPE {0} AS TABLE("
                        + " c1 Int DEFAULT -1,"
                        + " c2 NVarChar(40) DEFAULT N'DEFUALT',"
                        + " c3 DateTime DEFAULT '1/1/2006',"
                        + " c4 Int DEFAULT -1)",
                        typeName);
            string createProcSql = string.Format(
                    "CREATE PROC {0}(@tvp {1} READONLY) AS SELECT TOP(2) * FROM @tvp ORDER BY c1", procName, typeName);
            string dropSql = string.Format("DROP PROC {0}; DROP TYPE {1}", procName, typeName);

            try
            {
                SqlCommand cmd = new(createTypeSql, conn);
                cmd.ExecuteNonQuery();

                cmd.CommandText = createProcSql;
                cmd.ExecuteNonQuery();

                cmd.CommandText = procName;
                cmd.CommandType = CommandType.StoredProcedure;
                SqlParameter param = cmd.Parameters.Add("@tvp", SqlDbType.Structured);

                SqlMetaData[] columnMetadata;
                List<SqlDataRecord> rows = new();
                SqlDataRecord record;

                Console.WriteLine("------- Sort order + uniqueness #1: simple -------");
                columnMetadata = new SqlMetaData[] {
                            new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 0),
                            new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 1),
                            new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                            new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
                        };

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(0, "Z-value", DateTime.Parse("03/01/2000"), 5);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(1, "Y-value", DateTime.Parse("02/01/2000"), 6);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(1, "X-value", DateTime.Parse("01/01/2000"), 7);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(1, "X-value", DateTime.Parse("04/01/2000"), 8);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(1, "X-value", DateTime.Parse("04/01/2000"), 4);
                rows.Add(record);

                param.Value = rows;
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    WriteReader(rdr);
                }
                rows.Clear();

                Console.WriteLine("------- Sort order + uniqueness #2: mixed order -------");
                columnMetadata = new SqlMetaData[] {
                            new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
                            new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 0),
                            new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                            new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 1),
                        };

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 1);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 2);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("02/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(5, "X-value", DateTime.Parse("03/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(4, "X-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                param.Value = rows;
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    WriteReader(rdr);
                }
                rows.Clear();

                Console.WriteLine("------- default column #1: outer subset -------");
                columnMetadata = new SqlMetaData[] {
                            new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.NVarChar, 40, false, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.DateTime, false, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                        };

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 1);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 2);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("02/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(5, "X-value", DateTime.Parse("03/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(4, "X-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                param.Value = rows;
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    WriteReader(rdr);
                }
                rows.Clear();

                Console.WriteLine("------- default column #1: middle subset -------");
                columnMetadata = new SqlMetaData[] {
                            new SqlMetaData("", SqlDbType.Int, false, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.NVarChar, 40, true, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.DateTime, true, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.Int, false, false, SortOrder.Unspecified, -1),
                        };

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 1);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 2);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("02/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(5, "X-value", DateTime.Parse("03/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(4, "X-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                param.Value = rows;
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    WriteReader(rdr);
                }
                rows.Clear();

                Console.WriteLine("------- default column #1: all -------");
                columnMetadata = new SqlMetaData[] {
                            new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.NVarChar, 40, true, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.DateTime, true, false, SortOrder.Unspecified, -1),
                            new SqlMetaData("", SqlDbType.Int, true, false, SortOrder.Unspecified, -1),
                        };

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 1);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Z-value", DateTime.Parse("01/01/2000"), 2);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(6, "Y-value", DateTime.Parse("02/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(5, "X-value", DateTime.Parse("03/01/2000"), 3);
                rows.Add(record);

                record = new SqlDataRecord(columnMetadata);
                record.SetValues(4, "X-value", DateTime.Parse("01/01/2000"), 3);
                rows.Add(record);

                param.Value = rows;
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    WriteReader(rdr);
                }
                rows.Clear();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                SqlCommand cmd = new(dropSql, conn);
                cmd.ExecuteNonQuery();
            }
        }

        private static void WriteReader(SqlDataReader rdr)
        {
            int colCount = rdr.FieldCount;

            do
            {
                Console.WriteLine("-------------");
                while (rdr.Read())
                {
                    for (int i = 0; i < colCount; i++)
                    {
                        Console.Write("{0}  ", DataTestUtility.GetValueString(rdr.GetValue(i)));
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();
                Console.WriteLine("-------------");
            }
            while (rdr.NextResult());
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
    }
}
