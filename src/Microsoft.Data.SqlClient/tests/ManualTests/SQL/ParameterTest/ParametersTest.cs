// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Globalization;

#if !NETFRAMEWORK
using Microsoft.SqlServer.Types;
using Microsoft.Data.SqlClient.Server;
#endif

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ParametersTest
    {
        private static readonly string s_connString = DataTestUtility.TCPConnectionString;

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void CodeCoverageSqlClient()
        {
            SqlParameterCollection opc = new SqlCommand().Parameters;

            Assert.True(opc.Count == 0, string.Format("FAILED: Expected count: {0}. Actual count: {1}.", 0, opc.Count));
            Assert.False(((IList)opc).IsReadOnly, "FAILED: Expected collection to NOT be read only.");
            Assert.False(((IList)opc).IsFixedSize, "FAILED: Expected collection to NOT be fixed size.");
            Assert.False(((IList)opc).IsSynchronized, "FAILED: Expected collection to NOT be synchronized.");
            string expectedValue1 = "Object";
            string expectedValue2 = "List`1";
            string actualValue = ((IList)opc).SyncRoot.GetType().Name;
            var msg = string.Format("{0}\nExpected: {1} or {2}\nActual: {3}", "FAILED: Incorrect SyncRoot Name", expectedValue1, expectedValue2, actualValue);
            Assert.True(actualValue.Equals(expectedValue1) || actualValue.Equals(expectedValue2));

            {
                string failValue;
                DataTestUtility.AssertThrowsWrapper<IndexOutOfRangeException>(() => failValue = opc[0].ParameterName, "Invalid index 0 for this SqlParameterCollection with Count=0.");

                DataTestUtility.AssertThrowsWrapper<IndexOutOfRangeException>(() => failValue = opc["@p1"].ParameterName, "A SqlParameter with ParameterName '@p1' is not contained by this SqlParameterCollection.");

                DataTestUtility.AssertThrowsWrapper<IndexOutOfRangeException>(() => opc["@p1"] = null, "A SqlParameter with ParameterName '@p1' is not contained by this SqlParameterCollection.");
            }

            DataTestUtility.AssertThrowsWrapper<ArgumentNullException>(() => opc.Add(null), "The SqlParameterCollection only accepts non-null SqlParameter type objects.");

            opc.Add((object)new SqlParameter());
            IEnumerator enm = opc.GetEnumerator();
            Assert.True(enm.MoveNext(), "FAILED: Expected MoveNext to be true");
            DataTestUtility.AssertEqualsWithDescription("Parameter1", ((SqlParameter)enm.Current).ParameterName, "FAILED: Incorrect ParameterName");

            opc.Add(new SqlParameter());
            DataTestUtility.AssertEqualsWithDescription("Parameter2", opc[1].ParameterName, "FAILED: Incorrect ParameterName");

            opc.Add(new SqlParameter(null, null));
            opc.Add(null, SqlDbType.Int, 0, null);
            DataTestUtility.AssertEqualsWithDescription("Parameter4", opc["Parameter4"].ParameterName, "FAILED: Incorrect ParameterName");

            opc.Add(new SqlParameter("Parameter5", SqlDbType.NVarChar, 20));
            opc.Add(new SqlParameter(null, SqlDbType.NVarChar, 20, "a"));
            opc.RemoveAt(opc[3].ParameterName);
            DataTestUtility.AssertEqualsWithDescription(-1, opc.IndexOf(null), "FAILED: Incorrect index for null value");

            SqlParameter p = opc[0];

            DataTestUtility.AssertThrowsWrapper<ArgumentException>(() => opc.Add((object)p), "The SqlParameter is already contained by another SqlParameterCollection.");

            DataTestUtility.AssertThrowsWrapper<ArgumentException>(() => new SqlCommand().Parameters.Add(p), "The SqlParameter is already contained by another SqlParameterCollection.");

            DataTestUtility.AssertThrowsWrapper<ArgumentNullException>(() => opc.Remove(null), "The SqlParameterCollection only accepts non-null SqlParameter type objects.");

            string pname = p.ParameterName;
            p.ParameterName = pname;
            p.ParameterName = pname.ToUpper();
            p.ParameterName = pname.ToLower();
            p.ParameterName = "@p1";
            p.ParameterName = pname;

            opc.Clear();
            opc.Add(p);

            opc.Clear();
            opc.AddWithValue("@p1", null);

            DataTestUtility.AssertEqualsWithDescription(-1, opc.IndexOf(p.ParameterName), "FAILED: Incorrect index for parameter name");

            opc[0] = p;
            DataTestUtility.AssertEqualsWithDescription(0, opc.IndexOf(p.ParameterName), "FAILED: Incorrect index for parameter name");

            Assert.True(opc.Contains(p.ParameterName), "FAILED: Expected collection to contain provided parameter.");
            Assert.True(opc.Contains(opc[0]), "FAILED: Expected collection to contain provided parameter.");

            opc[0] = p;
            opc[p.ParameterName] = new SqlParameter(p.ParameterName, null);
            opc[p.ParameterName] = new SqlParameter();
            opc.RemoveAt(0);

            new SqlCommand().Parameters.Clear();
            new SqlCommand().Parameters.CopyTo(new object[0], 0);
            Assert.False(new SqlCommand().Parameters.GetEnumerator().MoveNext(), "FAILED: Expected MoveNext to be false");

            DataTestUtility.AssertThrowsWrapper<InvalidCastException>(() => new SqlCommand().Parameters.Add(0), "The SqlParameterCollection only accepts non-null Microsoft.Data.SqlClient.SqlParameter type objects, not System.Int32 objects.");

            DataTestUtility.AssertThrowsWrapper<InvalidCastException>(() => new SqlCommand().Parameters.Insert(0, 0), "The SqlParameterCollection only accepts non-null Microsoft.Data.SqlClient.SqlParameter type objects, not System.Int32 objects.");

            DataTestUtility.AssertThrowsWrapper<InvalidCastException>(() => new SqlCommand().Parameters.Remove(0), "The SqlParameterCollection only accepts non-null Microsoft.Data.SqlClient.SqlParameter type objects, not System.Int32 objects.");

            DataTestUtility.AssertThrowsWrapper<ArgumentException>(() => new SqlCommand().Parameters.Remove(new SqlParameter()), "Attempted to remove an SqlParameter that is not contained by this SqlParameterCollection.");
        }

        // TODO Synapse: Parse error at line: 1, column: 12: Incorrect syntax near 'IF'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void Test_Copy_SqlParameter()
        {
            using var conn = new SqlConnection(s_connString);
            string cTableName = DataTestUtility.GetLongName("#tmp");
            try
            {
                // Create tmp table
                var sCreateTable = "IF NOT EXISTS(";
                sCreateTable += $"SELECT * FROM sysobjects WHERE name= '{ cTableName }' and xtype = 'U')";
                sCreateTable += $"CREATE TABLE { cTableName }( BinValue binary(16)  null)";

                conn.Open();
                var cmd = new SqlCommand(sCreateTable, conn);
                cmd.ExecuteNonQuery();

                var dt = new DataTable("SourceDataTable");
                dt.Columns.Add("SourceBinValue", typeof(byte[]));

                dt.Rows.Add(Guid.NewGuid().ToByteArray());
                dt.Rows.Add(DBNull.Value);

                var cmdInsert = new SqlCommand
                {
                    UpdatedRowSource = UpdateRowSource.None,
                    Connection = conn,

                    CommandText = $"INSERT { cTableName } (BinValue) "
                };
                cmdInsert.CommandText += "Values(@BinValue)";
                cmdInsert.Parameters.Add("@BinValue", SqlDbType.Binary, 16, "SourceBinValue");

                var da = new SqlDataAdapter
                {
                    InsertCommand = cmdInsert,
                    UpdateBatchSize = 2,
                    AcceptChangesDuringUpdate = false
                };
                da.Update(dt);
            }
            finally
            {
                // End of test, cleanup tmp table;
                var sDropTable = $"DROP TABLE IF EXISTS {cTableName}";
                using SqlCommand cmd = new(sDropTable, conn);
                cmd.ExecuteNonQuery();
            }
        }

        // TODO Synapse: Remove dependency on Northwind database
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void Test_SqlParameter_Constructor()
        {
            using var conn = new SqlConnection(s_connString);
            var dataTable = new DataTable();
            var adapter = new SqlDataAdapter
            {
                SelectCommand = new SqlCommand("SELECT CustomerID, ContactTitle FROM dbo.Customers WHERE ContactTitle = @ContactTitle", conn)
            };
            var selectParam = new SqlParameter("@ContactTitle", SqlDbType.NVarChar, 30, ParameterDirection.Input, true, 0, 0, "ContactTitle", DataRowVersion.Current, "Owner");
            adapter.SelectCommand.Parameters.Add(selectParam);

            adapter.UpdateCommand = new SqlCommand("UPDATE dbo.Customers SET ContactTitle = @ContactTitle WHERE CustomerID = @CustomerID", conn);
            var titleParam = new SqlParameter("@ContactTitle", SqlDbType.NVarChar, 30, ParameterDirection.Input, true, 0, 0, "ContactTitle", DataRowVersion.Current, null);
            var idParam = new SqlParameter("@CustomerID", SqlDbType.NChar, 5, ParameterDirection.Input, false, 0, 0, "CustomerID", DataRowVersion.Current, null);
            adapter.UpdateCommand.Parameters.Add(titleParam);
            adapter.UpdateCommand.Parameters.Add(idParam);

            adapter.Fill(dataTable);
            object titleData = dataTable.Rows[0]["ContactTitle"];
            Assert.Equal("Owner", (string)titleData);

            titleData = "Test Data";
            adapter.Update(dataTable);
            adapter.Fill(dataTable);
            Assert.Equal("Test Data", (string)titleData);

            titleData = "Owner";
            adapter.Update(dataTable);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void Test_WithEnumValue_ShouldInferToUnderlyingType()
        {
            using var conn = new SqlConnection(s_connString);
            conn.Open();
            var cmd = new SqlCommand("select @input", conn);
            cmd.Parameters.AddWithValue("@input", MyEnum.B);
            object value = cmd.ExecuteScalar();
            Assert.Equal(MyEnum.B, (MyEnum)value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void Test_WithOutputEnumParameter_ShouldReturnEnum()
        {
            using var conn = new SqlConnection(s_connString);
            conn.Open();
            var cmd = new SqlCommand("set @output = @input", conn);
            cmd.Parameters.AddWithValue("@input", MyEnum.B);
            SqlParameter outputParam = cmd.CreateParameter();
            outputParam.ParameterName = "@output";
            outputParam.DbType = DbType.Int32;
            outputParam.Direction = ParameterDirection.Output;
            cmd.Parameters.Add(outputParam);
            cmd.ExecuteNonQuery();
            Assert.Equal(MyEnum.B, (MyEnum)outputParam.Value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void Test_WithDecimalValue_ShouldReturnDecimal()
        {
            using var conn = new SqlConnection(s_connString);
            conn.Open();
            var cmd = new SqlCommand("select @foo", conn);
            cmd.Parameters.AddWithValue("@foo", new SqlDecimal(0.5));
            var result = (decimal)cmd.ExecuteScalar();
            Assert.Equal((decimal)0.5, result);
        }

        // Synapse: Unsupported parameter type found while parsing RPC request. The request has been terminated.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void Test_WithGuidValue_ShouldReturnGuid()
        {
            using var conn = new SqlConnection(s_connString);
            conn.Open();
            var expectedGuid = Guid.NewGuid();
            var cmd = new SqlCommand("select @input", conn);
            cmd.Parameters.AddWithValue("@input", expectedGuid);
            var result = cmd.ExecuteScalar();
            Assert.Equal(expectedGuid, (Guid)result);
        }

        // Synapse: Parse error at line: 1, column: 8: Incorrect syntax near 'TYPE'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestParametersWithDatatablesTVPInsert()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            int x = 4, y = 5;

            DataTable table = new()
            {
                Columns = { { "x", typeof(int) }, { "y", typeof(int) } },
                Rows = { { x, y } }
            };

            using SqlConnection connection = new(builder.ConnectionString);
            string tableName = DataTestUtility.GetLongName("Table");
            string procName = DataTestUtility.GetLongName("Proc");
            string typeName = DataTestUtility.GetShortName("Type");
            try
            {
                connection.Open();
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE TYPE {typeName} AS TABLE (x INT, y INT)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = $"CREATE TABLE {tableName} (x INT, y INT)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = $"CREATE PROCEDURE {procName} @TVP {typeName} READONLY AS " +
                        $"SET NOCOUNT ON INSERT INTO {tableName}(x, y) SELECT * FROM  @TVP";
                    cmd.ExecuteNonQuery();

                }
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    // Update Data Using TVPs
                    cmd.CommandText = procName;
                    cmd.CommandType = CommandType.StoredProcedure;

                    SqlParameter parameter = cmd.Parameters.AddWithValue("@TVP", table);
                    parameter.TypeName = typeName;

                    cmd.ExecuteNonQuery();

                    // Verify if the data was updated 
                    cmd.CommandText = "select * from " + tableName;
                    cmd.CommandType = CommandType.Text;
                    using SqlDataReader reader = cmd.ExecuteReader();
                    DataTable dbData = new();
                    dbData.Load(reader);
                    Assert.Equal(1, dbData.Rows.Count);
                    Assert.Equal(x, dbData.Rows[0][0]);
                    Assert.Equal(y, dbData.Rows[0][1]);
                }
            }
            finally
            {
                using SqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = "DROP PROCEDURE " + procName;
                cmd.ExecuteNonQuery();
                cmd.CommandText = "DROP TABLE " + tableName;
                cmd.ExecuteNonQuery();
                cmd.CommandText = "DROP TYPE " + typeName;
                cmd.ExecuteNonQuery();
            }
        }

#if !NETFRAMEWORK
        // Synapse: Parse error at line: 1, column: 8: Incorrect syntax near 'TYPE'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestParametersWithSqlRecordsTVPInsert()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);

            SqlGeography geog = SqlGeography.Point(43, -81, 4326);

            SqlMetaData[] metadata = new SqlMetaData[]
            {
                new SqlMetaData("Id", SqlDbType.UniqueIdentifier),
                new SqlMetaData("geom", SqlDbType.Udt, typeof(SqlGeography), "Geography")
            };

            SqlDataRecord record1 = new SqlDataRecord(metadata);
            record1.SetValues(Guid.NewGuid(), geog);

            SqlDataRecord record2 = new SqlDataRecord(metadata);
            record2.SetValues(Guid.NewGuid(), geog);

            IList<SqlDataRecord> featureInserts = new List<SqlDataRecord>
            {
                record1,
                record2,
            };
            
            using SqlConnection connection = new(builder.ConnectionString);
            string procName = DataTestUtility.GetLongName("Proc");
            string typeName = DataTestUtility.GetShortName("Type");
            try
            {
                connection.Open();

                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE TYPE {typeName} AS TABLE([Id] [uniqueidentifier] NULL, [geom] [geography] NULL)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @$"CREATE PROCEDURE {procName}
                        @newRoads as {typeName} READONLY
                        AS
                        BEGIN
                         SELECT* FROM @newRoads
                        END";
                    cmd.ExecuteNonQuery();

                }
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    // Update Data Using TVPs
                    cmd.CommandText = procName;
                    cmd.CommandType = CommandType.StoredProcedure;

                    SqlParameter param = new SqlParameter("@newRoads", SqlDbType.Structured);
                    param.Value = featureInserts;
                    param.TypeName = typeName;

                    cmd.Parameters.Add(param);

                    using var reader = cmd.ExecuteReader();

                    Assert.True(reader.HasRows);

                    int count = 0;
                    while (reader.Read())
                    {
                        Assert.NotNull(reader[0]);
                        Assert.NotNull(reader[1]);
                        count++;
                    }

                    Assert.Equal(2, count);
                }
            }
            finally
            {
                using SqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = "DROP PROCEDURE " + procName;
                cmd.ExecuteNonQuery();
                cmd.CommandText = "DROP TYPE " + typeName;
                cmd.ExecuteNonQuery();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestDateOnlyTVPDataTable_CommandSP()
        {
            string tableTypeName = "[dbo]." + DataTestUtility.GetLongName("UDTTTestDateOnlyTVP");
            string spName = DataTestUtility.GetLongName("spTestDateOnlyTVP");
            SqlConnection connection = new(s_connString);
            try
            {
                connection.Open();
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = $"CREATE TYPE {tableTypeName} AS TABLE ([DateColumn] date NULL, [TimeColumn] time NULL)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = $"CREATE PROCEDURE {spName} (@dates {tableTypeName} READONLY) AS SELECT COUNT(*) FROM @dates";
                    cmd.ExecuteNonQuery();
                }
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = spName;
                    cmd.CommandType = CommandType.StoredProcedure;
                    
                    DataTable dtTest = new();
                    dtTest.Columns.Add(new DataColumn("DateColumn", typeof(DateOnly)));
                    dtTest.Columns.Add(new DataColumn("TimeColumn", typeof(TimeOnly)));
                    var dataRow = dtTest.NewRow();
                    dataRow["DateColumn"] = new DateOnly(2023, 11, 15);
                    dataRow["TimeColumn"] = new TimeOnly(12, 30, 45);
                    dtTest.Rows.Add(dataRow);

                    cmd.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "@dates",
                        SqlDbType = SqlDbType.Structured,
                        TypeName = tableTypeName,
                        Value = dtTest,
                    });

                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                DataTestUtility.DropStoredProcedure(connection, spName);
                DataTestUtility.DropUserDefinedType(connection, tableTypeName);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestDateOnlyTVPSqlDataRecord_CommandSP()
        {
            string tableTypeName = "[dbo]." + DataTestUtility.GetLongName("UDTTTestDateOnlySqlDataRecordTVP");
            string spName = DataTestUtility.GetLongName("spTestDateOnlySqlDataRecordTVP");
            SqlConnection connection = new(s_connString);
            try
            {
                connection.Open();
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = $"CREATE TYPE {tableTypeName} AS TABLE ([DateColumn] date NULL, [TimeColumn] time NULL)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = $"CREATE PROCEDURE {spName} (@dates {tableTypeName} READONLY) AS SELECT COUNT(*) FROM @dates";
                    cmd.ExecuteNonQuery();
                }
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = spName;
                    cmd.CommandType = CommandType.StoredProcedure;

                    SqlMetaData[] metadata = new SqlMetaData[]
                    {
                        new SqlMetaData("DateColumn", SqlDbType.Date),
                        new SqlMetaData("TimeColumn", SqlDbType.Time)
                    };

                    SqlDataRecord record1 = new SqlDataRecord(metadata);
                    record1.SetValues(new DateOnly(2023, 11, 15), new TimeOnly(12, 30, 45));

                    SqlDataRecord record2 = new SqlDataRecord(metadata);
                    record2.SetValues(new DateOnly(2025, 11, 15), new TimeOnly(13, 31, 46));

                    IList<SqlDataRecord> featureInserts = new List<SqlDataRecord>
                    {
                        record1,
                        record2,
                    };

                    cmd.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "@dates",
                        SqlDbType = SqlDbType.Structured,
                        TypeName = tableTypeName,
                        Value = featureInserts,
                    });

                    using var reader = cmd.ExecuteReader();

                    Assert.True(reader.HasRows);

                    int count = 0;
                    while (reader.Read())
                    {
                        Assert.NotNull(reader[0]);
                        count++;
                    }

                    Assert.Equal(1, count);
                }
            }
            finally
            {
                DataTestUtility.DropStoredProcedure(connection, spName);
                DataTestUtility.DropUserDefinedType(connection, tableTypeName);
            }
        }
#endif

        #region Scaled Decimal Parameter & TVP Test
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("CAST(1.0 as decimal(38, 37))", "1.0000000000000000000000000000")]
        [InlineData("CAST(7.1234567890123456789012345678 as decimal(38, 35))", "7.1234567890123456789012345678")]
        [InlineData("CAST(-7.1234567890123456789012345678 as decimal(38, 35))", "-7.1234567890123456789012345678")]
        [InlineData("CAST(-0.1234567890123456789012345678 as decimal(38, 35))", "-0.1234567890123456789012345678")]
        [InlineData("CAST(4210862852.86 as decimal(38, 20))", "4210862852.860000000000000000")]
        [InlineData("CAST(0 as decimal(38, 36))", "0.0000000000000000000000000000")]
        [InlineData("CAST(79228162514264337593543950335 as decimal(38, 9))", "79228162514264337593543950335")]
        [InlineData("CAST(-79228162514264337593543950335 as decimal(38, 9))", "-79228162514264337593543950335")]
        [InlineData("CAST(0.4210862852 as decimal(38, 38))", "0.4210862852000000000000000000")]
        [InlineData("CAST(0.1234567890123456789012345678 as decimal(38, 38))", "0.1234567890123456789012345678")]
        [InlineData("CAST(249454727.14678312032280248320 as decimal(38, 20))", "249454727.14678312032280248320")]
        [InlineData("CAST(3961408124790879675.7769715711 as decimal(38, 10))", "3961408124790879675.7769715711")]
        [InlineData("CAST(3961408124790879675776971571.1 as decimal(38, 1))", "3961408124790879675776971571.1")]
        [InlineData("CAST(79228162514264337593543950335 as decimal(38, 0))", "79228162514264337593543950335")]
        [InlineData("CAST(-79228162514264337593543950335 as decimal(38, 0))", "-79228162514264337593543950335")]
        [InlineData("CAST(0.0000000000000000000000000001 as decimal(38, 38))", "0.0000000000000000000000000001")]
        [InlineData("CAST(-0.0000000000000000000000000001 as decimal(38, 38))", "-0.0000000000000000000000000001")]
        public static void SqlDecimalConvertToDecimal_TestInRange(string sqlDecimalValue, string expectedDecimalValue)
        {
            using SqlConnection cnn = new(s_connString);
            cnn.Open();
            using SqlCommand cmd = new($"SELECT {sqlDecimalValue} val");
            cmd.Connection = cnn;
            using SqlDataReader rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read(), "SqlDataReader must have a value");
            decimal returnValue = rdr.GetDecimal(0);
            Assert.Equal(expectedDecimalValue, returnValue.ToString(CultureInfo.InvariantCulture));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("CAST(7.9999999999999999999999999999 as decimal(38, 35))")]
        [InlineData("CAST(8.1234567890123456789012345678 as decimal(38, 35))")]
        [InlineData("CAST(-8.1234567890123456789012345678 as decimal(38, 35))")]
        [InlineData("CAST(123456789012345678901234567890 as decimal(38, 0))")]
        [InlineData("CAST(7922816251426433759354395.9999 as decimal(38, 8))")]
        [InlineData("CAST(-7922816251426433759354395.9999 as decimal(38, 8))")]
        [InlineData("CAST(0.123456789012345678901234567890 as decimal(38, 36))")]
        public static void SqlDecimalConvertToDecimal_TestOutOfRange(string sqlDecimalValue)
        {
            using SqlConnection cnn = new(s_connString);
            cnn.Open();
            using SqlCommand cmd = new($"SELECT {sqlDecimalValue} val");
            cmd.Connection = cnn;
            using SqlDataReader rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read(), "SqlDataReader must have a value");
            Assert.Throws<OverflowException>(() => rdr.GetDecimal(0));
        }

        [Theory]
        [ClassData(typeof(ConnectionStringsProvider))]
        public static void TestScaledDecimalParameter_CommandInsert(string connectionString, bool truncateScaledDecimal)
        {
            using LocalAppContextSwitchesHelper appContextSwitchesHelper = new();

            string tableName = DataTestUtility.GetLongName("TestDecimalParameterCMD");
            using SqlConnection connection = InitialDatabaseTable(connectionString, tableName);
            try
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    AppContext.SetSwitch(TruncateDecimalSwitch, truncateScaledDecimal);
                    var p = new SqlParameter("@Value", null)
                    {
                        Precision = 18,
                        Scale = 2
                    };
                    cmd.Parameters.Add(p);
                    for (int i = 0; i < s_testValues.Length; i++)
                    {
                        p.Value = s_testValues[i];
                        cmd.CommandText = $"INSERT INTO {tableName} (Id, [Value]) VALUES({i}, @Value)";
                        cmd.ExecuteNonQuery();
                    }
                }
                Assert.True(ValidateInsertedValues(connection, tableName, truncateScaledDecimal), $"Invalid test happened with connection string [{connection.ConnectionString}]");
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        [Theory]
        [ClassData(typeof(ConnectionStringsProvider))]
        public static void TestScaledDecimalParameter_BulkCopy(string connectionString, bool truncateScaledDecimal)
        {
            using LocalAppContextSwitchesHelper appContextSwitchesHelper = new();

            string tableName = DataTestUtility.GetLongName("TestDecimalParameterBC");
            using SqlConnection connection = InitialDatabaseTable(connectionString, tableName);
            try
            {
                using (SqlBulkCopy bulkCopy = new(connection))
                {
                    DataTable table = new(tableName);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("Value", typeof(decimal));
                    for (int i = 0; i < s_testValues.Length; i++)
                    {
                        DataRow newRow = table.NewRow();
                        newRow["Id"] = i;
                        newRow["Value"] = s_testValues[i];
                        table.Rows.Add(newRow);
                    }

                    bulkCopy.DestinationTableName = tableName;
                    AppContext.SetSwitch(TruncateDecimalSwitch, truncateScaledDecimal);
                    bulkCopy.WriteToServer(table);
                }
                Assert.True(ValidateInsertedValues(connection, tableName, truncateScaledDecimal), $"Invalid test happened with connection string [{connection.ConnectionString}]");
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Synapse: Parse error at line: 2, column: 8: Incorrect syntax near 'TYPE'.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public static void TestScaledDecimalTVP_CommandSP(string connectionString, bool truncateScaledDecimal)
        {
            using LocalAppContextSwitchesHelper appContextSwitchesHelper = new();

            string tableName = DataTestUtility.GetLongName("TestDecimalParameterBC");
            string tableTypeName = DataTestUtility.GetLongName("UDTTTestDecimalParameterBC");
            string spName = DataTestUtility.GetLongName("spTestDecimalParameterBC");
            using SqlConnection connection = InitialDatabaseUDTT(connectionString, tableName, tableTypeName, spName);
            try
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    var p = new SqlParameter("@tvp", SqlDbType.Structured)
                    {
                        TypeName = $"dbo.{tableTypeName}"
                    };
                    cmd.CommandText = spName;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(p);

                    DataTable table = new(tableName);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("Value", typeof(decimal));
                    for (int i = 0; i < s_testValues.Length; i++)
                    {
                        DataRow newRow = table.NewRow();
                        newRow["Id"] = i;
                        newRow["Value"] = s_testValues[i];
                        table.Rows.Add(newRow);
                    }
                    p.Value = table;
                    AppContext.SetSwitch(TruncateDecimalSwitch, truncateScaledDecimal);
                    cmd.ExecuteNonQuery();
                }
                // TVP always rounds data without attention to the configuration.
                Assert.True(ValidateInsertedValues(connection, tableName, false && truncateScaledDecimal), $"Invalid test happened with connection string [{connection.ConnectionString}]");
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
                DataTestUtility.DropStoredProcedure(connection, spName);
                DataTestUtility.DropUserDefinedType(connection, tableTypeName);
            }
        }

        #region Decimal parameter test setup
        private static readonly decimal[] s_testValues = new[] { 4210862852.8600000000_0000000000m, 19.1560m, 19.1550m, 19.1549m };
        private static readonly decimal[] s_expectedRoundedValues = new[] { 4210862852.86m, 19.16m, 19.16m, 19.15m };
        private static readonly decimal[] s_expectedTruncatedValues = new[] { 4210862852.86m, 19.15m, 19.15m, 19.15m };
        private const string TruncateDecimalSwitch = "Switch.Microsoft.Data.SqlClient.TruncateScaledDecimal";

        private static SqlConnection InitialDatabaseUDTT(string cnnString, string tableName, string tableTypeName, string spName)
        {
            SqlConnection connection = new(cnnString);
            connection.Open();
            using (SqlCommand cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $"CREATE TABLE {tableName} (Id INT, Value Decimal(38, 2)) \n";
                cmd.CommandText += $"CREATE TYPE {tableTypeName} AS TABLE (Id INT, Value Decimal(38, 2)) ";
                cmd.ExecuteNonQuery();
                cmd.CommandText = $"CREATE PROCEDURE {spName} (@tvp {tableTypeName} READONLY) AS \n INSERT INTO {tableName} (Id, Value) SELECT * FROM @tvp ORDER BY Id";
                cmd.ExecuteNonQuery();
            }
            return connection;
        }

        private static SqlConnection InitialDatabaseTable(string cnnString, string tableName)
        {
            SqlConnection connection = new(cnnString);
            connection.Open();
            using (SqlCommand cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $"CREATE TABLE {tableName} (Id INT, Value Decimal(38, 2))";
                cmd.ExecuteNonQuery();
            }
            return connection;
        }

        private static bool ValidateInsertedValues(SqlConnection connection, string tableName, bool truncateScaledDecimal)
        {
            bool exceptionHit;
            decimal[] expectedValues = truncateScaledDecimal ? s_expectedTruncatedValues : s_expectedRoundedValues;

            try
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    // Verify if the data was as same as our expectation.
                    cmd.CommandText = $"SELECT [Value] FROM {tableName} ORDER BY Id ASC";
                    cmd.CommandType = CommandType.Text;
                    using SqlDataReader reader = cmd.ExecuteReader();
                    DataTable dbData = new();
                    dbData.Load(reader);
                    Assert.Equal(expectedValues.Length, dbData.Rows.Count);
                    for (int i = 0; i < expectedValues.Length; i++)
                    {
                        Assert.Equal(expectedValues[i], dbData.Rows[i][0]);
                    }
                }
                exceptionHit = false;
            }
            catch
            {
                exceptionHit = true;
            }
            return !exceptionHit;
        }

        public class ConnectionStringsProvider : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var cnnString in DataTestUtility.ConnectionStrings)
                {
                    yield return new object[] { cnnString, false };
                    yield return new object[] { cnnString, true };
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        #endregion
        #endregion

        private enum MyEnum
        {
            A = 1,
            B = 2
        }

        private static void ExecuteNonQueryCommand(string connectionString, string cmdText)
        {
            using SqlConnection conn = new(connectionString);
            using SqlCommand cmd = conn.CreateCommand();
            conn.Open();
            cmd.CommandText = cmdText;
            cmd.ExecuteNonQuery();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        private static void EnableOptimizedParameterBinding_ParametersAreUsedByName()
        {
            int firstInput = 1;
            int secondInput = 2;
            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT @Second, @First", connection);
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.AddWithValue("@First", firstInput);
            command.Parameters.AddWithValue("@Second", secondInput);

            using SqlDataReader reader = command.ExecuteReader();
            reader.Read();

            int firstOutput = reader.GetInt32(0);
            int secondOutput = reader.GetInt32(1);

            Assert.Equal(firstInput, secondOutput);
            Assert.Equal(secondInput, firstOutput);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        private static void EnableOptimizedParameterBinding_NamesMustMatch()
        {
            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT @DoesNotExist", connection);
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.AddWithValue("@Exists", 1);

            SqlException sqlException = null;
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqlException sqlEx)
            {
                sqlException = sqlEx;
            }

            Assert.NotNull(sqlException);
            Assert.Contains("Must declare the scalar variable", sqlException.Message);
            Assert.Contains("@DoesNotExist", sqlException.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        private static void EnableOptimizedParameterBinding_AllNamesMustBeDeclared()
        {
            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT @Exists, @DoesNotExist", connection);
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.AddWithValue("@Exists", 1);

            SqlException sqlException = null;
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqlException sqlEx)
            {
                sqlException = sqlEx;
            }

            Assert.NotNull(sqlException);
            Assert.Contains("Must declare the scalar variable", sqlException.Message);
            Assert.Contains("@DoesNotExist", sqlException.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        private static void EnableOptimizedParameterBinding_NamesCanBeReUsed()
        {
            int firstInput = 1;
            int secondInput = 2;
            int thirdInput = 3;

            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT @First, @Second, @First", connection);
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.AddWithValue("@First", firstInput);
            command.Parameters.AddWithValue("@Second", secondInput);
            command.Parameters.AddWithValue("@Third", thirdInput);

            using SqlDataReader reader = command.ExecuteReader();
            reader.Read();

            int firstOutput = reader.GetInt32(0);
            int secondOutput = reader.GetInt32(1);
            int thirdOutput = reader.GetInt32(2);

            Assert.Equal(firstInput, firstOutput);
            Assert.Equal(secondInput, secondOutput);
            Assert.Equal(firstInput, thirdOutput);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        private static void EnableOptimizedParameterBinding_InputOutputFails()
        {
            int firstInput = 1;
            int secondInput = 2;
            int thirdInput = 3;

            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT @Third = (@Third + @First + @Second)", connection);
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.AddWithValue("@First", firstInput);
            command.Parameters.AddWithValue("@Second", secondInput);
            SqlParameter thirdParameter = command.Parameters.AddWithValue("@Third", thirdInput);
            thirdParameter.Direction = ParameterDirection.InputOutput;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

            Assert.Contains("OptimizedParameterBinding", exception.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        private static void EnableOptimizedParameterBinding_OutputFails()
        {
            int firstInput = 1;
            int secondInput = 2;
            int thirdInput = 3;

            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT @Third = (@Third + @First + @Second)", connection);
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.AddWithValue("@First", firstInput);
            command.Parameters.AddWithValue("@Second", secondInput);
            SqlParameter thirdParameter = command.Parameters.AddWithValue("@Third", thirdInput);
            thirdParameter.Direction = ParameterDirection.Output;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

            Assert.Contains("OptimizedParameterBinding", exception.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        private static void EnableOptimizedParameterBinding_ReturnSucceeds()
        {
            int firstInput = 12;

            string sprocName = DataTestUtility.GetShortName("P");
            // input, output
            string createSprocQuery =
                "CREATE PROCEDURE " + sprocName + " @in int " +
                "AS " +
                "RETURN(@in)";

            string dropSprocQuery = "DROP PROCEDURE " + sprocName;

            try
            {
                ExecuteNonQueryCommand(DataTestUtility.TCPConnectionString, createSprocQuery);

                using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
                connection.Open();

                using var command = new SqlCommand(sprocName, connection) { CommandType = CommandType.StoredProcedure };
                command.EnableOptimizedParameterBinding = true;
                command.Parameters.AddWithValue("@in", firstInput);
                SqlParameter returnParameter = command.Parameters.AddWithValue("@retval", 0);
                returnParameter.Direction = ParameterDirection.ReturnValue;

                command.ExecuteNonQuery();

                Assert.Equal(firstInput, Convert.ToInt32(returnParameter.Value));
            }
            finally
            {
                ExecuteNonQueryCommand(DataTestUtility.TCPConnectionString, dropSprocQuery);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ClosedConnection_SqlParameterValueTest()
        {
            var tasks = new Task[100];
            for (int i = 0; i < tasks.Length; i++)
            {
                var t = Task.Factory.StartNew(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        RunParameterTest();
                    }
                }, TaskCreationOptions.LongRunning);
                tasks[i] = t;
            }
            Task.WaitAll(tasks);
        }

        private static void RunParameterTest()
        {
            var cancellationToken = new CancellationTokenSource(50);
            var expectedGuid = Guid.NewGuid();

            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand cm = connection.CreateCommand();
            cm.CommandType = CommandType.Text;
            cm.CommandText = "select @id2 = @id;";
            cm.CommandTimeout = 2;
            cm.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = expectedGuid });
            cm.Parameters.Add(new SqlParameter("@id2", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output });
            try
            {
                Task<int> task = cm.ExecuteNonQueryAsync(cancellationToken.Token);
                task.Wait();
            }
            catch (Exception)
            {
                //ignore cancellations
            }
            finally
            {
                connection.Close();
            }
            if (cm.Parameters["@id2"].Value == null)
                return;
            else if ((Guid)cm.Parameters["@id2"].Value != expectedGuid)
            {
                Assert.Fail("CRITICAL : Unexpected data found in SqlCommand parameters, this is a MAJOR issue.");
            }
        }
    }
}
