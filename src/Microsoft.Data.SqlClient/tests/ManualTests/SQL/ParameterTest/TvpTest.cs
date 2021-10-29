// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Server;
using Xunit;
using System.Linq;
using System.Reflection;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class TvpTest
    {
        private const string TvpName = "@tvp";
        private static readonly IList<SteAttributeKey> BoundariesTestKeys = new List<SteAttributeKey>(
                new SteAttributeKey[] {
                    SteAttributeKey.SqlDbType,
                    SteAttributeKey.MultiValued,
                    SteAttributeKey.MaxLength,
                    SteAttributeKey.Precision,
                    SteAttributeKey.Scale,
                    SteAttributeKey.LocaleId,
                    SteAttributeKey.CompareOptions,
                    SteAttributeKey.TypeName,
                    SteAttributeKey.Type,
                    SteAttributeKey.Fields,
                    SteAttributeKey.Value
                }).AsReadOnly();

        // data value and server consts
        private string _connStr;

        // Synapse: The statement failed. Column 'blob' has a data type that cannot participate in a columnstore index.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void TestMain()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US"); // To keep things consistent since we output dates as strings

            // This test is additionally affected by #26, where a Cancel throws SqlException instead of InvalidOperationException on Linux.
            RunTestCoreAndCompareWithBaseline();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public async Task TestPacketNumberWraparound()
        {
            // this test uses a specifically crafted sql record enumerator and data to put the TdsParserStateObject.WritePacket(byte,bool)
            // into a state where it can't differentiate between a packet in the middle of a large packet-set after a byte counter wraparound
            // and the first packet of the connection and in doing so trips over a check for packet length from the input which has been 
            // forced to tell it that there is no output buffer space left, this causes an uncancellable infinite loop

            // if the enumerator is completely read to the end then the bug is no longer present and the packet creation task returns,
            // if the timeout occurs it is probable (but not absolute) that the write is stuck

            var enumerator = new WraparoundRowEnumerator(1000000);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            await RunPacketNumberWraparound(enumerator);

            stopwatch.Stop();
            Assert.True(enumerator.MaxCount == enumerator.Count);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void TestConnectionIsSafeToReuse()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);

            //Bad Scenario -exception expected.
            try
            {
                List<Item> list = new()
                {
                    // this will go as null as we pass 0 to it.
                    new Item(0),
                    new Item(1),
                    new Item(2),
                    new Item(3),
                    new Item(4),
                    new Item(5)
                };

                IEnumerable<int> Ids = list.Select(x => x.id.GetValueOrDefault()).Distinct();

                var sqlParam = new SqlParameter("@ids", SqlDbType.Structured)
                {
                    TypeName = "TableOfIntId",
                    SqlValue = Ids.Select(x =>
                    {
                        SqlDataRecord rec = new(new[] { new SqlMetaData("Id", SqlDbType.Int) });
                        rec.SetInt32(0, x);
                        return rec;
                    })
                };

                var parameters = new List<SqlParameter>() { sqlParam };
                const string SQL = @"SELECT * FROM information_schema.COLUMNS cols INNER JOIN  @ids Ids on Ids.id = cols.ORDINAL_POSITION";
                using SqlCommand cmd = new(SQL, connection);
                cmd.CommandTimeout = 100;
                AddCommandParameters(cmd, parameters);

                // I changed the exception as it comes InvalidOperationException saying null objects must have a value.
                Assert.Throws<SqlException>(() => new SqlDataAdapter(cmd).Fill(new("BadFunc")));
                //Assert.False(true, "Expected exception did not occur");
            }
            catch (Exception e)
            {
                // Ignore this exception as it's deliberately introduced.
                Assert.True(e.Message.Contains("Object reference not set to an instance of an object"), "Expected exception did not occur");
            }

            // Good Scenario - No failure expected.
            try
            {
                const string SQL = @"SELECT * FROM information_schema.tables WHERE TABLE_NAME = @TableName";
                var parameters = new List<SqlParameter>() { new SqlParameter("@TableName", "Temp") };
                using SqlCommand cmd = new(SQL, connection);
                cmd.CommandTimeout = 100;
                AddCommandParameters(cmd, parameters);
                new SqlDataAdapter(cmd).Fill(new("GoodFunc"));
            }
            catch (Exception e)
            {
                Assert.False(true, $"Unexpected error occurred: {e.Message}");
            }
        }

        private class Item
        {
            public Item(int? v)
            {
                id = v == 0 ? null : v;
            }
            public int? id { get; set; }
        }

        static internal void AddCommandParameters(SqlCommand command, IEnumerable parameters)
        {
            if (parameters == null)
                return;

            foreach (SqlParameter p in parameters)
            {
                if (p == null)
                    continue;

                if (p.Value == null)
                {
                    var clone = (SqlParameter)((ICloneable)p).Clone();
                    clone.Value = DBNull.Value;
                    command.Parameters.Add(clone);
                }
                else
                {
                    command.Parameters.Add(p);
                }
            }
        }

        public TvpTest()
        {
            _connStr = DataTestUtility.TCPConnectionString;
        }

        private void RunTest()
        {
            ColumnBoundariesTest();
            QueryHintsTest();
            SqlVariantParam.SendAllSqlTypesInsideVariant(_connStr);
            DateTimeVariantTest.TestAllDateTimeWithDataTypeAndVariant(_connStr);
            OutputParameter.Run(_connStr);
        }

        private void RunTestCoreAndCompareWithBaseline()
        {
            RunTest();
        }

        private sealed class CarriageReturnLineFeedReplacer : TextWriter
        {
            private TextWriter _output;
            private int _lineFeedCount;
            private bool _hasCarriageReturn;

            internal CarriageReturnLineFeedReplacer(TextWriter output)
            {
                if (output == null)
                    throw new ArgumentNullException(nameof(output));

                _output = output;
            }

            public int LineFeedCount
            {
                get { return _lineFeedCount; }
            }

            public override Encoding Encoding
            {
                get { return _output.Encoding; }
            }

            public override IFormatProvider FormatProvider
            {
                get { return _output.FormatProvider; }
            }

            public override string NewLine
            {
                get { return _output.NewLine; }
                set { _output.NewLine = value; }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    ((IDisposable)_output).Dispose();
                }
                _output = null;
            }

            public override void Flush()
            {
                _output.Flush();
            }

            public override void Write(char value)
            {
                if ('\n' == value)
                {
                    _lineFeedCount++;
                    if (!_hasCarriageReturn)
                    {   // X'\n'Y -> X'\r\n'Y
                        _output.Write('\r');
                    }
                }
                _hasCarriageReturn = '\r' == value;
                _output.Write(value);
            }
        }

        #region Main test methods
        private void ColumnBoundariesTest()
        {
            _ = SteStructuredTypeBoundaries.AllColumnTypesExceptUdts.GetEnumerator(
                        BoundariesTestKeys);
            TestTVPPermutations(SteStructuredTypeBoundaries.AllColumnTypesExceptUdts, false);
        }

        private void TestTVPPermutations(SteStructuredTypeBoundaries bounds, bool runOnlyDataRecordTest)
        {
            IEnumerator<StePermutation> boundsMD = bounds.GetEnumerator(BoundariesTestKeys);

            object[][] baseValues = SteStructuredTypeBoundaries.GetSeparateValues(boundsMD);
            IList<DataTable> dtList = GenerateDataTables(baseValues);

            TransactionOptions opts = new TransactionOptions
            {
                IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted
            };

            // for each unique pattern of metadata
            int iter = 0;
            while (boundsMD.MoveNext())
            {
                StePermutation tvpPerm = boundsMD.Current;

                // Set up base command
                SqlParameter param;
                SqlCommand cmd = new(GetProcName(tvpPerm))
                {
                    CommandType = CommandType.StoredProcedure
                };
                param = cmd.Parameters.Add(TvpName, SqlDbType.Structured);
                param.TypeName = GetTypeName(tvpPerm);

                // set up the server
                try
                {
                    CreateServerObjects(tvpPerm);
                }
                catch (SqlException)
                {
                    DropServerObjects(tvpPerm);

                    iter++;
                    continue;
                    throw;
                }

                // Send list of SqlDataRecords as value
                try
                {
                    param.Value = CreateListOfRecords(tvpPerm, baseValues);
                    ExecuteAndVerify(cmd, tvpPerm, baseValues, null);
                }
                catch (ArgumentException ae)
                {
                    Assert.True(false, $"Argument exception caught at {GetType()}.{MethodInfo.GetCurrentMethod()}. Message: {ae.Message}");
                }

                if (!runOnlyDataRecordTest)
                {
                    // send DbDataReader
                    try
                    {
                        param.Value = new TvpRestartableReader(CreateListOfRecords(tvpPerm, baseValues));
                        ExecuteAndVerify(cmd, tvpPerm, baseValues, null);
                    }
                    catch (ArgumentException ae)
                    {
                        Console.WriteLine(ae.Message);
                        throw;
                    }

                    // send datasets
                    // This is failing due to mismatch of length in source and target while setting datatable in  dt  at
                    // GenerateDataTables(baseValues).
                    // Needs more investigation to see which one is on purpose and which is oversight

                    //foreach (DataTable d in dtList)
                    //{
                    //    param.Value = d;
                    //    ExecuteAndVerify(cmd, tvpPerm, null, d);
                    //}
                }

                // And clean up
                //DropServerObjects(tvpPerm);

                iter++;
            }
        }

        private void QueryHintsTest()
        {
            using SqlConnection conn = new(_connStr);
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

                columnMetadata = new SqlMetaData[] {
                            new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Ascending, 0),
                            new SqlMetaData("", SqlDbType.NVarChar, 40, false, true, SortOrder.Descending, 1),
                            new SqlMetaData("", SqlDbType.DateTime, false, true, SortOrder.Ascending, 2),
                            new SqlMetaData("", SqlDbType.Int, false, true, SortOrder.Descending, 3),
                        };

                SqlDataRecord record = new(columnMetadata);
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
                    do
                    {
                        int rowCounter = 0;
                        while (rdr.Read())
                        {
                            // We are only selecting top 2 rows
                            // Field counts are 4
                            //  // Based on sortOrder the values we are expecting is
                            //  "0  Z-value  3/1/2000 12:00:00 AM  5"
                            //  "1  X-value  1/1/2000 12:00:00 AM  7"
                            if (rowCounter == 0)
                            {
                                Assert.Equal("0", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("Z-value", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("3/1/2000 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("5", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            if (rowCounter == 1)
                            {
                                Assert.Equal("1", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("X-value", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2000 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("7", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }

                            rowCounter++;
                        }
                    }
                    while (rdr.NextResult());
                }
                rows.Clear();

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
                    do
                    {
                        int rowCounter = 0;
                        while (rdr.Read())
                        {
                            // We are only selecting top 2 rows
                            // Field counts are 4
                            //  // Based on sortOrder the values we are expecting is
                            //  "4  X-value  1/1/2000 12:00:00 AM  3"
                            //  "5  X-value  3/1/2000 12:00:00 AM  3"
                            if (rowCounter == 0)
                            {
                                Assert.Equal("4", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("X-value", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2000 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("3", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            if (rowCounter == 1)
                            {
                                Assert.Equal("5", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("X-value", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("3/1/2000 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("3", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            rowCounter++;
                        }
                    }
                    while (rdr.NextResult());
                }
                rows.Clear();

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
                    do
                    {
                        int rowCounter = 0;
                        while (rdr.Read())
                        {
                            // We are only selecting top 2 rows
                            // Field counts are 4
                            //  // Based on sortOrder the values we are expecting is
                            //  "-1  Y-value  1/1/2000 12:00:00 AM  -1"
                            //  "-1  Z-value  1/1/2000 12:00:00 AM  -1"
                            if (rowCounter == 0)
                            {
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("Y-value", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2000 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            if (rowCounter == 1)
                            {
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("Z-value", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2000 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            rowCounter++;
                        }
                    }
                    while (rdr.NextResult());
                }
                rows.Clear();


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
                    do
                    {
                        int rowCounter = 0;
                        while (rdr.Read())
                        {
                            // We are only selecting top 2 rows
                            // Field counts are 4
                            //  // Based on sortOrder the values we are expecting is
                            //  "4  DEFUALT  1/1/2006 12:00:00 AM  3"
                            //  "5  DEFUALT  1/1/2006 12:00:00 AM  3"
                            if (rowCounter == 0)
                            {
                                Assert.Equal("4", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("DEFUALT", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2006 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("3", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            if (rowCounter == 1)
                            {
                                Assert.Equal("5", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("DEFUALT", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2006 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("3", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            rowCounter++;
                        }
                    }
                    while (rdr.NextResult());
                }
                rows.Clear();

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
                    do
                    {
                        int rowCounter = 0;
                        while (rdr.Read())
                        {
                            // We are only selecting top 2 rows
                            // Field counts are 4
                            //  // Based on sortOrder the values we are expecting is
                            //  "-1  DEFUALT  1/1/2006 12:00:00 AM  -1"
                            //  "-1  DEFUALT  1/1/2006 12:00:00 AM  -1"
                            if (rowCounter == 0)
                            {
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("DEFUALT", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2006 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            if (rowCounter == 1)
                            {
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(0)));
                                Assert.Equal("DEFUALT", DataTestUtility.GetValueString(rdr.GetValue(1)));
                                Assert.Equal("1/1/2006 12:00:00 AM", DataTestUtility.GetValueString(rdr.GetValue(2)));
                                Assert.Equal("-1", DataTestUtility.GetValueString(rdr.GetValue(3)));
                            }
                            rowCounter++;
                        }
                    }
                    while (rdr.NextResult());
                }
                rows.Clear();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                SqlCommand cmd = new SqlCommand(dropSql, conn);
                cmd.ExecuteNonQuery();
            }
        }

        private static async Task RunPacketNumberWraparound(WraparoundRowEnumerator enumerator)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            using SqlCommand cmd = new("unimportant")
            {
                CommandType = System.Data.CommandType.StoredProcedure,
                Connection = connection,
            };
            await cmd.Connection.OpenAsync();
            cmd.Parameters.Add(new SqlParameter("@rows", SqlDbType.Structured)
            {
                TypeName = "unimportant",
                Value = enumerator,
            });
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception)
            {
                // ignore the errors caused by the sproc and table type not existing
            }
        }

        #endregion

        #region Utility Methods

        private bool AllowableDifference(string source, object result, StePermutation metadata)
        {
            bool returnValue = false;

            // turn result into a string
            string resultStr = null;
            if (result.GetType() == typeof(string))
            {
                resultStr = (string)result;
            }
            else if (result.GetType() == typeof(char[]))
            {
                resultStr = new string((char[])result);
            }
            else if (result.GetType() == typeof(SqlChars))
            {
                resultStr = new string(((SqlChars)result).Value);
            }

            if (resultStr != null)
            {
                if (source.Equals(resultStr))
                {
                    returnValue = true;
                }
                else if (metadata.TryGetValue(SteAttributeKey.MaxLength, out object value) && value != SteTypeBoundaries.s_doNotUseMarker)
                {
                    int maxLength = (int)value;

                    if (maxLength < source.Length &&
                            source.Substring(0, maxLength).Equals(resultStr))
                    {
                        returnValue = true;
                    }
                    // Check for length extension due to fixed-length type
                    else if (maxLength > source.Length &&
                                resultStr.Length == maxLength &&
                                metadata.TryGetValue(SteAttributeKey.SqlDbType, out value) &&
                                value != SteTypeBoundaries.s_doNotUseMarker &&
                                (SqlDbType.Char == ((SqlDbType)value) ||
                                  SqlDbType.NChar == ((SqlDbType)value)))
                    {
                        returnValue = true;
                    }
                }
            }

            return returnValue;
        }

        private bool AllowableDifference(byte[] source, object result, StePermutation metadata)
        {
            bool returnValue = false;

            // turn result into byte array
            byte[] resultBytes = null;
            if (result.GetType() == typeof(byte[]))
            {
                resultBytes = (byte[])result;
            }
            else if (result.GetType() == typeof(SqlBytes))
            {
                resultBytes = ((SqlBytes)result).Value;
            }

            if (resultBytes != null)
            {
                if (source.Equals(resultBytes) || resultBytes.Length == source.Length)
                {
                    returnValue = true;
                }
                else if (metadata.TryGetValue(SteAttributeKey.MaxLength, out object value) && value != SteTypeBoundaries.s_doNotUseMarker)
                {
                    int maxLength = (int)value;

                    // allowable max-length adjustments
                    if (maxLength == resultBytes.Length)
                    {  // a bit optimistic, but what the heck.
                       // truncation
                        if (maxLength <= source.Length)
                        {
                            returnValue = true;
                        }
                        // Check for length extension due to fixed-length type
                        else if (metadata.TryGetValue(SteAttributeKey.SqlDbType, out value) && value != SteTypeBoundaries.s_doNotUseMarker &&
                                (SqlDbType.Binary == ((SqlDbType)value)))
                        {
                            returnValue = true;
                        }
                    }
                }
            }
            return returnValue;
        }

        private bool AllowableDifference(SqlDecimal source, object result, StePermutation metadata)
        {
            bool returnValue = false;

            // turn result into SqlDecimal
            SqlDecimal resultValue = SqlDecimal.Null;
            if (result.GetType() == typeof(SqlDecimal))
            {
                resultValue = (SqlDecimal)result;
            }
            else if (result.GetType() == typeof(decimal))
            {
                resultValue = new SqlDecimal((decimal)result);
            }
            else if (result.GetType() == typeof(SqlMoney))
            {
                resultValue = new SqlDecimal(((SqlMoney)result).Value);
            }

            if (!resultValue.IsNull)
            {
                if (source.Equals(resultValue))
                {
                    returnValue = true;
                }
                else if (metadata.TryGetValue(SteAttributeKey.SqlDbType, out object value) &&
                        SteTypeBoundaries.s_doNotUseMarker != value &&
                        (SqlDbType.SmallMoney == (SqlDbType)value ||
                         SqlDbType.Money == (SqlDbType)value))
                {
                    // Some server conversions seem to lose the decimal places
                    // TODO: Investigate and validate that this is acceptable!
                    SqlDecimal tmp = SqlDecimal.ConvertToPrecScale(source, source.Precision, 0);
                    if (tmp.Equals(resultValue))
                    {
                        returnValue = true;
                    }
                    else
                    {
                        tmp = SqlDecimal.ConvertToPrecScale(resultValue, resultValue.Precision, 0);
                        returnValue = tmp.Equals(source);
                    }
                }
                // check if value was altered by precision/scale conversion
                else if (metadata.TryGetValue(SteAttributeKey.SqlDbType, out value) &&
                        SteTypeBoundaries.s_doNotUseMarker != value &&
                        SqlDbType.Decimal == (SqlDbType)value)
                {
                    if (metadata.TryGetValue(SteAttributeKey.Scale, out value) &&
                            metadata.TryGetValue(SteAttributeKey.Precision, out object value2) &&
                            SteTypeBoundaries.s_doNotUseMarker != value &&
                            SteTypeBoundaries.s_doNotUseMarker != value2)
                    {
                        SqlDecimal tmp = SqlDecimal.ConvertToPrecScale(source, (byte)value2, (byte)value);

                        returnValue = tmp.Equals(resultValue);
                    }

                    // check if value was changed to 1 by the restartable reader
                    //   due to exceeding size limits of System.Decimal
                    if (resultValue == (SqlDecimal)1M)
                    {
                        try
                        {
                            decimal dummy = source.Value;
                        }
                        catch (OverflowException)
                        {
                            returnValue = true;
                        }
                    }
                }
            }
            return returnValue;
        }

        private bool CompareValue(object result, object source, StePermutation metadata)
        {
            bool isMatch = false;
            if (!IsNull(source))
            {
                if (!IsNull(result))
                {
                    if (source.Equals(result) || result.Equals(source))
                    {
                        isMatch = true;
                    }
                    else
                    {
                        switch (Type.GetTypeCode(source.GetType()))
                        {
                            case TypeCode.String:
                                isMatch = AllowableDifference((string)source, result, metadata);
                                break;
                            case TypeCode.Object:
                                {
                                    if (source is char[])
                                    {
                                        source = new string((char[])source);
                                        isMatch = AllowableDifference((string)source, result, metadata);
                                    }
                                    else if (source is byte[])
                                    {
                                        isMatch = AllowableDifference((byte[])source, result, metadata);
                                    }
                                    else if (source is SqlBytes)
                                    {
                                        isMatch = AllowableDifference(((SqlBytes)source).Value, result, metadata);
                                    }
                                    else if (source is SqlChars)
                                    {
                                        source = new string(((SqlChars)source).Value);
                                        isMatch = AllowableDifference((string)source, result, metadata);
                                    }
                                    else if (source is SqlInt64 && result is long)
                                    {
                                        isMatch = result.Equals(((SqlInt64)source).Value);
                                    }
                                    else if (source is SqlInt32 && result is int)
                                    {
                                        isMatch = result.Equals(((SqlInt32)source).Value);
                                    }
                                    else if (source is SqlInt16 @int && result is short)
                                    {
                                        isMatch = result.Equals(@int.Value);
                                    }
                                    else if (source is SqlSingle && result is float)
                                    {
                                        isMatch = result.Equals(((SqlSingle)source).Value);
                                    }
                                    else if (source is SqlDouble && result is double)
                                    {
                                        isMatch = result.Equals(((SqlDouble)source).Value);
                                    }
                                    else if (source is SqlDateTime && result is DateTime)
                                    {
                                        isMatch = result.Equals(((SqlDateTime)source).Value);
                                    }
                                    else if (source is SqlMoney)
                                    {
                                        isMatch = AllowableDifference(new SqlDecimal(((SqlMoney)source).Value), result, metadata);
                                    }
                                    else if (source is SqlDecimal)
                                    {
                                        isMatch = AllowableDifference((SqlDecimal)source, result, metadata);
                                    }
                                }
                                break;
                            case TypeCode.Decimal:
                                if (result is SqlDecimal || result is decimal || result is SqlMoney)
                                {
                                    isMatch = AllowableDifference(new SqlDecimal((decimal)source), result, metadata);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            else
            {
                if (IsNull(result))
                {
                    isMatch = true;
                }
            }

            if (!isMatch)
            {
                ReportMismatch(source, result, metadata);
            }
            return isMatch;
        }

        private IList<SqlDataRecord> CreateListOfRecords(StePermutation tvpPerm, object[][] baseValues)
        {
            IList<StePermutation> fields = GetFields(tvpPerm);
            SqlMetaData[] fieldMetadata = new SqlMetaData[fields.Count];
            int i = 0;
            foreach (StePermutation perm in fields)
            {
                fieldMetadata[i] = PermToSqlMetaData(perm);
                i++;
            }

            List<SqlDataRecord> records = new List<SqlDataRecord>(baseValues.Length);
            for (int rowOrd = 0; rowOrd < baseValues.Length; rowOrd++)
            {
                object[] row = baseValues[rowOrd];
                SqlDataRecord rec = new(fieldMetadata);
                records.Add(rec); // Call SetValue *after* Add to ensure record is put in list
                for (int colOrd = 0; colOrd < row.Length; colOrd++)
                {
                    // Set value in try-catch to prevent some errors from aborting run.
                    try
                    {
                        rec.SetValue(colOrd, row[colOrd]);
                    }
                    catch (OverflowException oe)
                    {
                        Console.WriteLine(oe.Message);
                        throw;
                    }
                    catch (ArgumentException ae)
                    {
                        Console.WriteLine(ae.Message);
                        throw;
                    }
                }
            }
            return records;
        }

        private DataTable CreateNewTable(object[] row, ref Type[] lastRowTypes)
        {
            DataTable dt = new();
            for (int i = 0; i < row.Length; i++)
            {
                object value = row[i];
                Type t;
                if ((null == value || DBNull.Value == value))
                {
                    if (lastRowTypes[i] == null)
                    {
                        return null;
                    }
                    else
                    {
                        t = lastRowTypes[i];
                    }
                }
                else
                {
                    t = value.GetType();
                }

                dt.Columns.Add(new DataColumn("Col" + i + "_" + t.Name, t));

                lastRowTypes[i] = t;
            }
            return dt;
        }

        // create table type and proc that uses that type at the server
        private void CreateServerObjects(StePermutation tvpPerm)
        {
            // Create the table type tsql
            StringBuilder tsql = new StringBuilder();
            tsql.Append("CREATE TYPE ");
            tsql.Append(GetTypeName(tvpPerm));
            tsql.Append(" AS TABLE(");
            bool addSeparator = false;
            int colOrdinal = 1;
            foreach (StePermutation perm in GetFields(tvpPerm))
            {
                if (addSeparator)
                {
                    tsql.Append(", ");
                }
                else
                {
                    addSeparator = true;
                }

                // column name
                tsql.Append("column");
                tsql.Append(colOrdinal);
                tsql.Append(" ");

                // column type
                SqlDbType dbType = (SqlDbType)perm[SteAttributeKey.SqlDbType];
                switch (dbType)
                {
                    case SqlDbType.BigInt:
                        tsql.Append("Bigint");
                        break;
                    case SqlDbType.Binary:
                        tsql.Append("Binary(");
                        object maxLenObj = perm[SteAttributeKey.MaxLength];
                        int maxLen;
                        if (maxLenObj == SteTypeBoundaries.s_doNotUseMarker)
                        {
                            maxLen = 8000;
                        }
                        else
                        {
                            maxLen = (int)maxLenObj;
                        }
                        tsql.Append(maxLen);
                        tsql.Append(")");
                        break;
                    case SqlDbType.Bit:
                        tsql.Append("Bit");
                        break;
                    case SqlDbType.Char:
                        tsql.Append("Char(");
                        tsql.Append(perm[SteAttributeKey.MaxLength]);
                        tsql.Append(")");
                        break;
                    case SqlDbType.DateTime:
                        tsql.Append("DateTime");
                        break;
                    case SqlDbType.Decimal:
                        tsql.Append("Decimal(");
                        tsql.Append(perm[SteAttributeKey.Precision]);
                        tsql.Append(", ");
                        tsql.Append(perm[SteAttributeKey.Scale]);
                        tsql.Append(")");
                        break;
                    case SqlDbType.Float:
                        tsql.Append("Float");
                        break;
                    case SqlDbType.Image:
                        tsql.Append("Image");
                        break;
                    case SqlDbType.Int:
                        tsql.Append("Int");
                        break;
                    case SqlDbType.Money:
                        tsql.Append("Money");
                        break;
                    case SqlDbType.NChar:
                        tsql.Append("NChar(");
                        tsql.Append(perm[SteAttributeKey.MaxLength]);
                        tsql.Append(")");
                        break;
                    case SqlDbType.NText:
                        tsql.Append("NText");
                        break;
                    case SqlDbType.NVarChar:
                        tsql.Append("NVarChar(");
                        tsql.Append(perm[SteAttributeKey.MaxLength]);
                        tsql.Append(")");
                        break;
                    case SqlDbType.Real:
                        tsql.Append("Real");
                        break;
                    case SqlDbType.UniqueIdentifier:
                        tsql.Append("UniqueIdentifier");
                        break;
                    case SqlDbType.SmallDateTime:
                        tsql.Append("SmallDateTime");
                        break;
                    case SqlDbType.SmallInt:
                        tsql.Append("SmallInt");
                        break;
                    case SqlDbType.SmallMoney:
                        tsql.Append("SmallMoney");
                        break;
                    case SqlDbType.Text:
                        tsql.Append("Text");
                        break;
                    case SqlDbType.Timestamp:
                        tsql.Append("Timestamp");
                        break;
                    case SqlDbType.TinyInt:
                        tsql.Append("TinyInt");
                        break;
                    case SqlDbType.VarBinary:
                        tsql.Append("VarBinary(");
                        tsql.Append(perm[SteAttributeKey.MaxLength]);
                        tsql.Append(")");
                        break;
                    case SqlDbType.VarChar:
                        tsql.Append("VarChar(");
                        tsql.Append(perm[SteAttributeKey.MaxLength]);
                        tsql.Append(")");
                        break;
                    case SqlDbType.Variant:
                        tsql.Append("Variant");
                        break;
                    case SqlDbType.Xml:
                        tsql.Append("Xml");
                        break;
                    case SqlDbType.Udt:
                        string typeName = (string)perm[SteAttributeKey.TypeName];
                        tsql.Append(typeName);
                        break;
                    case SqlDbType.Structured:
                        throw new NotSupportedException("Not supported");
                }
                colOrdinal++;
            }

            tsql.Append(")");

            using SqlConnection conn = new SqlConnection(_connStr);
            conn.Open();

            // execute it to create the type
            SqlCommand cmd = new SqlCommand(tsql.ToString(), conn);
            cmd.ExecuteNonQuery();

            // and create the proc that uses the type            
            cmd.CommandText = string.Format("CREATE PROC {0}(@tvp {1} READONLY) AS SELECT * FROM @tvp order by {2}",
                    GetProcName(tvpPerm), GetTypeName(tvpPerm), colOrdinal - 1);
            cmd.ExecuteNonQuery();
        }

        private bool DoesRowMatchMetadata(object[] row, DataTable table)
        {
            bool result = true;
            if (row.Length != table.Columns.Count)
            {
                result = false;
            }
            else
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (null != row[i] && DBNull.Value != row[i] && row[i].GetType() != table.Columns[i].DataType)
                    {
                        result = false;
                    }
                }
            }
            return result;
        }

        private void DropServerObjects(StePermutation tvpPerm)
        {
            string dropText = $"DROP PROC IF EXISTS { GetProcName(tvpPerm)} ;  DROP TYPE IF EXISTS {GetTypeName(tvpPerm)} ";
            using SqlConnection conn = new(_connStr);
            conn.Open();

            SqlCommand cmd = new(dropText, conn);
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SqlException e)
            {
                Console.WriteLine("SqlException dropping objects: {0}", e.Number);
                throw;
            }
        }

        private void ExecuteAndVerify(SqlCommand cmd, StePermutation tvpPerm, object[][] objValues, DataTable dtValues)
        {
            using SqlConnection conn = new(_connStr);
            conn.Open();
            cmd.Connection = conn;
            if (DataTestUtility.IsNotAzureServer())
            {
                // Choose the 2628 error message instead of 8152 in SQL Server 2016 & 2017
                // Azure SQL Managed Instance supports the following global Trace Flags:
                // 460, 2301, 2389, 2390, 2453, 2467, 7471, 8207, 9389, 10316, and 11024.
                // Session trace-flags are not yet supported in Managed Instance. Related link:
                // https://docs.microsoft.com/en-us/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql?view=sql-server-ver15#trace-flags
                // Flag 460 replaces message 8152: 'String or binary data would be truncated.'to
                // 2628: String or binary data would be truncated in table '%.*ls', column '%.*ls'. Truncated value: '%.*ls'.
                // Starting with SQL Server 2019 (15.x), to accomplish this at the database level,
                // see the VERBOSE_TRUNCATION_WARNINGS option in ALTER DATABASE SCOPED CONFIGURATION (Transact-SQL).
                // This trace flag applies to SQL Server 2017(14.x) CU12 and higher builds.
                //Starting with database compatibility level 150, message ID 2628 is the default and this trace flag has no effect.
                using SqlCommand cmdFix = new("DBCC TRACEON(460)", conn);
                cmdFix.ExecuteNonQuery();
            }

            try
            {
                using SqlDataReader rdr = cmd.ExecuteReader();
                VerifyColumnBoundaries(rdr, GetFields(tvpPerm), objValues, dtValues);
            }
            catch (SqlException se)
            {
                Console.WriteLine("SqlException. Error Code: {0} with ", se.Number);
                throw;
            }
            catch (InvalidOperationException ioe)
            {
                Console.WriteLine("InvalidOp: {0}", ioe.Message);
                throw;
            }
            catch (ArgumentException ae)
            {
                Console.WriteLine("ArgumentException: {0}", ae.Message);
                throw;
            }
        }

        private IList<DataTable> GenerateDataTables(object[][] values)
        {
            List<DataTable> dtList = new List<DataTable>();
            Type[] valueTypes = new Type[values[0].Length];
            foreach (object[] row in values)
            {
                DataTable targetTable = null;
                if (0 < dtList.Count)
                {
                    // shortcut for matching last table (most common scenario)
                    if (DoesRowMatchMetadata(row, dtList[dtList.Count - 1]))
                    {
                        targetTable = dtList[dtList.Count - 1];
                    }
                    else
                    {
                        foreach (DataTable candidate in dtList)
                        {
                            if (DoesRowMatchMetadata(row, candidate))
                            {
                                targetTable = candidate;
                                break;
                            }
                        }
                    }
                }

                if (null == targetTable)
                {
                    targetTable = CreateNewTable(row, ref valueTypes);
                    if (null != targetTable)
                    {
                        dtList.Add(targetTable);
                    }
                }

                if (null != targetTable)
                {
                    targetTable.Rows.Add(row);

                }
            }
            return dtList;
        }

        private IList<StePermutation> GetFields(StePermutation tvpPerm) => (IList<StePermutation>)tvpPerm[SteAttributeKey.Fields];

        private string GetProcName(StePermutation tvpPerm) => "dbo.[Proc_" + (string)tvpPerm[SteAttributeKey.TypeName] + "]";

        private string GetTypeName(StePermutation tvpPerm) => "dbo.[" + (string)tvpPerm[SteAttributeKey.TypeName] + "]";

        private bool IsNull(object value) => null == value ||
                    DBNull.Value == value ||
                    (value is INullable nullableValue &&
                     nullableValue.IsNull);

        private SqlMetaData PermToSqlMetaData(StePermutation perm)
        {
            SqlDbType sqlDbType;
            int maxLength = 0;
            byte precision = 0;
            byte scale = 0;
            string typeName = null;
            Type type = null;
            long localeId = 0;
            SqlCompareOptions opts = SqlCompareOptions.IgnoreCase | SqlCompareOptions.IgnoreKanaType | SqlCompareOptions.IgnoreWidth;
            if (perm.TryGetValue(SteAttributeKey.SqlDbType, out object attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                sqlDbType = (SqlDbType)attr;
            }
            else
            {
                throw new InvalidOperationException("PermToSqlMetaData: No SqlDbType available!");
            }

            if (perm.TryGetValue(SteAttributeKey.MaxLength, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                maxLength = (int)attr;
            }

            if (perm.TryGetValue(SteAttributeKey.Precision, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                precision = (byte)attr;
            }

            if (perm.TryGetValue(SteAttributeKey.Scale, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                scale = (byte)attr;
            }

            if (perm.TryGetValue(SteAttributeKey.LocaleId, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                localeId = (int)attr;
            }

            if (perm.TryGetValue(SteAttributeKey.CompareOptions, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                opts = (SqlCompareOptions)attr;
            }

            if (perm.TryGetValue(SteAttributeKey.TypeName, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                typeName = (string)attr;
            }

            if (perm.TryGetValue(SteAttributeKey.Type, out attr) && (attr != SteTypeBoundaries.s_doNotUseMarker))
            {
                type = (Type)attr;
            }

            //if (SqlDbType.Udt == sqlDbType)
            //{
            //    return new SqlMetaData("", sqlDbType, type, typeName);
            //}
            //else
            //{
            return new SqlMetaData("", sqlDbType, maxLength, precision, scale, localeId, opts, type);
            //}
        }

        private void ReportMismatch(object source, object result, StePermutation perm)
        {
            if (null == source)
            {
                source = "(null)";
            }
            if (null == result)
            {
                result = "(null)";
            }
        }

        private void VerifyColumnBoundaries(SqlDataReader rdr, IList<StePermutation> fieldMetaData, object[][] values, DataTable dt)
        {
            int rowOrd = 0;
            int matches = 0;
            while (rdr.Read())
            {
                for (int columnOrd = 0; columnOrd < rdr.FieldCount; columnOrd++)
                {
                    object value;
                    // Special case to handle decimal values that may be too large for GetValue
                    if (!rdr.IsDBNull(columnOrd) && rdr.GetFieldType(columnOrd) == typeof(decimal))
                    {
                        value = rdr.GetSqlValue(columnOrd);
                    }
                    else
                    {
                        value = rdr.GetValue(columnOrd);
                    }
                    if (null != values)
                    {
                        if (CompareValue(value, values[rowOrd][columnOrd], fieldMetaData[columnOrd]))
                        {
                            matches++;
                        }
                    }
                    else
                    {
                        if (CompareValue(value, dt.Rows[rowOrd][columnOrd], fieldMetaData[columnOrd]))
                        {
                            matches++;
                        }
                    }
                }
                rowOrd++;
            }
            var expectedValue = rowOrd * rdr.FieldCount;
            Assert.Equal(expectedValue, matches);
        }
        #endregion
    }

    internal class TvpRestartableReader : DbDataReader
    {
        private IList<SqlDataRecord> _sourceData;
        int _currentRow;

        internal TvpRestartableReader(IList<SqlDataRecord> source) : base()
        {
            _sourceData = source;
            Restart();
        }

        public void Restart()
        {
            _currentRow = -1;
        }

        override public int Depth
        {
            get { return 0; }
        }

        override public int FieldCount
        {
            get { return _sourceData[_currentRow].FieldCount; }
        }

        override public bool HasRows
        {
            get { return _sourceData.Count > 0; }
        }

        override public bool IsClosed
        {
            get { return false; }
        }

        override public int RecordsAffected
        {
            get { return 0; }
        }

        override public object this[int ordinal]
        {
            get { return GetValue(ordinal); }
        }

        override public object this[string name]
        {
            get { return GetValue(GetOrdinal(name)); }
        }

        override public void Close()
        {
            _currentRow = _sourceData.Count;
        }

        public override string GetDataTypeName(int ordinal) => _sourceData[_currentRow].GetDataTypeName(ordinal);

        override public IEnumerator GetEnumerator() => _sourceData.GetEnumerator();

        override public Type GetFieldType(int ordinal) => _sourceData[_currentRow].GetFieldType(ordinal);

        public override string GetName(int ordinal) => _sourceData[_currentRow].GetName(ordinal);

        public override int GetOrdinal(string name) => _sourceData[_currentRow].GetOrdinal(name);

        override public DataTable GetSchemaTable()
        {
            SqlDataRecord rec = _sourceData[0];

            DataTable schemaTable = new DataTable();
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.ColumnName, typeof(string)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.ColumnOrdinal, typeof(int)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.ColumnSize, typeof(int)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.NumericPrecision, typeof(short)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.NumericScale, typeof(short)));

            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.DataType, typeof(System.Type)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableOptionalColumn.ProviderSpecificDataType, typeof(System.Type)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.NonVersionedProviderType, typeof(int)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.ProviderType, typeof(int)));

            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.IsLong, typeof(bool)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.AllowDBNull, typeof(bool)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableOptionalColumn.IsReadOnly, typeof(bool)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableOptionalColumn.IsRowVersion, typeof(bool)));

            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.IsUnique, typeof(bool)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.IsKey, typeof(bool)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableOptionalColumn.IsHidden, typeof(bool)));

            schemaTable.Columns.Add(new DataColumn(SchemaTableOptionalColumn.BaseCatalogName, typeof(string)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.BaseSchemaName, typeof(string)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.BaseTableName, typeof(string)));
            schemaTable.Columns.Add(new DataColumn(SchemaTableColumn.BaseColumnName, typeof(string)));

            for (int i = 0; i < rec.FieldCount; i++)
            {
                DataRow row = schemaTable.NewRow();
                SqlMetaData md = rec.GetSqlMetaData(i);
                row[SchemaTableColumn.ColumnName] = md.Name;
                row[SchemaTableColumn.ColumnOrdinal] = i;
                row[SchemaTableColumn.ColumnSize] = md.MaxLength;
                row[SchemaTableColumn.NumericPrecision] = md.Precision;
                row[SchemaTableColumn.NumericScale] = md.Scale;
                row[SchemaTableColumn.DataType] = rec.GetFieldType(i);
                row[SchemaTableOptionalColumn.ProviderSpecificDataType] = rec.GetFieldType(i);
                row[SchemaTableColumn.NonVersionedProviderType] = (int)md.SqlDbType;
                row[SchemaTableColumn.ProviderType] = (int)md.SqlDbType;
                row[SchemaTableColumn.IsLong] = md.MaxLength == SqlMetaData.Max || md.MaxLength > 8000;
                row[SchemaTableColumn.AllowDBNull] = true;
                row[SchemaTableOptionalColumn.IsReadOnly] = true;
                row[SchemaTableOptionalColumn.IsRowVersion] = md.SqlDbType == SqlDbType.Timestamp;
                row[SchemaTableColumn.IsUnique] = false;
                row[SchemaTableColumn.IsKey] = false;
                row[SchemaTableOptionalColumn.IsAutoIncrement] = false;
                row[SchemaTableOptionalColumn.IsHidden] = false;
                row[SchemaTableOptionalColumn.BaseCatalogName] = null;
                row[SchemaTableColumn.BaseSchemaName] = null;
                row[SchemaTableColumn.BaseTableName] = null;
                row[SchemaTableColumn.BaseColumnName] = md.Name;
                schemaTable.Rows.Add(row);
            }
            return schemaTable;
        }

        // zero reference
        public override bool GetBoolean(int ordinal) => _sourceData[_currentRow].GetBoolean(ordinal);

        public override byte GetByte(int ordinal) => _sourceData[_currentRow].GetByte(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) =>
            _sourceData[_currentRow].GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

        public override char GetChar(int ordinal) => _sourceData[_currentRow].GetChar(ordinal);

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) =>
            _sourceData[_currentRow].GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

        override public DateTime GetDateTime(int ordinal) => _sourceData[_currentRow].GetDateTime(ordinal);

        override public decimal GetDecimal(int ordinal)
        {
            // DataRecord may have illegal values for Decimal...
            decimal result;
            try
            {
                result = _sourceData[_currentRow].GetDecimal(ordinal);
            }
            catch (OverflowException)
            {
                result = (decimal)1;
            }
            return result;
        }

        public override double GetDouble(int ordinal) => _sourceData[_currentRow].GetDouble(ordinal);

        public override float GetFloat(int ordinal) => _sourceData[_currentRow].GetFloat(ordinal);

        public override Guid GetGuid(int ordinal) => _sourceData[_currentRow].GetGuid(ordinal);

        public override short GetInt16(int ordinal) => _sourceData[_currentRow].GetInt16(ordinal);

        public override int GetInt32(int ordinal) => _sourceData[_currentRow].GetInt32(ordinal);

        public override long GetInt64(int ordinal) => _sourceData[_currentRow].GetInt64(ordinal);

        public override string GetString(int ordinal) => _sourceData[_currentRow].GetString(ordinal);

        public override object GetValue(int ordinal) => _sourceData[_currentRow].GetValue(ordinal);

        public override int GetValues(object[] values) => _sourceData[_currentRow].GetValues(values);

        public override bool IsDBNull(int ordinal) => _sourceData[_currentRow].IsDBNull(ordinal);

        public override bool NextResult()
        {
            Close();
            return false;
        }

        public override bool Read()
        {
            _currentRow++;

            return _currentRow < _sourceData.Count;
        }
    }

    internal class WraparoundRowEnumerator : IEnumerable<SqlDataRecord>, IEnumerator<SqlDataRecord>
    {
        private int _count;
        private int _maxCount;
        private SqlDataRecord _record;

        public WraparoundRowEnumerator(int maxCount)
        {
            _maxCount = maxCount;
            _record = new SqlDataRecord(new SqlMetaData("someData", SqlDbType.VarBinary, 8000));

            // 56 31 0 0 is result of calling BitConverter.GetBytes((int)7992)
            // The rest of the bytes are just padding to get 56, 31, 0, 0 to be in bytes 8-11 of TdsParserStatObject._outBuff after the 256th packet
            _record.SetBytes(
                0,
                0,
                new byte[] { 1, 2, 56, 31, 0, 0, 7, 8, 9, 10, 11, 12, 13, 14 },
                0,
                14);

            // change any of the 56 31 0 0 bytes and this program completes as expected in a couple seconds
        }

        public bool MoveNext()
        {
            _count++;
            return _count < _maxCount;
        }

        public SqlDataRecord Current => _record;

        object IEnumerator.Current => Current;

        public int Count { get => _count; set => _count = value; }
        public int MaxCount { get => _maxCount; set => _maxCount = value; }

        public IEnumerator<SqlDataRecord> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose() { }
        public void Reset() { }
    }
}
