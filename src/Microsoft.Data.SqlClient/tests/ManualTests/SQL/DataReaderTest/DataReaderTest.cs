// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DataReaderTest
    {
        private static readonly object s_rowVersionLock = new();

        // this enum must mirror the definition in LocalAppContextSwitches
        private enum Tristate : byte
        {
            NotInitialized = 0,
            False = 1,
            True = 2
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void LoadReaderIntoDataTableToTestGetSchemaTable()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            var dt = new DataTable();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = "select 3 as [three], 4 as [four]";
            // Datatables internally call IDataReader.GetSchemaTable()
            dt.Load(command.ExecuteReader());
            Assert.Equal(2, dt.Columns.Count);
            Assert.Equal("three", dt.Columns[0].ColumnName);
            Assert.Equal("four", dt.Columns[1].ColumnName);
            Assert.Equal(1, dt.Rows.Count);
            Assert.Equal(3, (int)dt.Rows[0][0]);
            Assert.Equal(4, (int)dt.Rows[0][1]);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MultiQuerySchema()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            // Use multiple queries
            command.CommandText = "SELECT 1 as ColInteger;  SELECT 'STRING' as ColString";
            using SqlDataReader reader = command.ExecuteReader();
            HashSet<string> columnNames = new();
            do
            {
                DataTable schemaTable = reader.GetSchemaTable();
                foreach (DataRow myField in schemaTable.Rows)
                {
                    columnNames.Add(myField["ColumnName"].ToString());
                }

            } while (reader.NextResult());
            Assert.Contains("ColInteger", columnNames);
            Assert.Contains("ColString", columnNames);
        }

        // Checks for the IsColumnSet bit in the GetSchemaTable for Sparse columns
        // TODO Synapse:  Cannot find data type 'xml'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CheckSparseColumnBit()
        {
            const int sparseColumns = 4095;
            string tempTableName = DataTestUtility.GenerateObjectName();

            // TSQL for "CREATE TABLE" with sparse columns
            // table name will be provided as an argument
            StringBuilder createBuilder = new("CREATE TABLE {0} ([ID] int PRIMARY KEY, [CSET] xml COLUMN_SET FOR ALL_SPARSE_COLUMNS NULL");

            // TSQL to create the same table, but without the column set column and without sparse
            // also, it has only 1024 columns, which is the server limit in this case
            StringBuilder createNonSparseBuilder = new("CREATE TABLE {0} ([ID] int PRIMARY KEY");

            // TSQL to select all columns from the sparse table, without columnset one
            StringBuilder selectBuilder = new("SELECT [ID]");

            // TSQL to select all columns from the sparse table, with a limit of 1024 (for bulk-copy test)
            StringBuilder selectNonSparseBuilder = new("SELECT [ID]");

            // add sparse columns
            for (int c = 0; c < sparseColumns; c++)
            {
                createBuilder.AppendFormat(", [C{0}] int SPARSE NULL", c);
                selectBuilder.AppendFormat(", [C{0}]", c);
            }

            createBuilder.Append(")");
            // table name provided as an argument
            selectBuilder.Append(" FROM {0}");

            string selectStatementFormat = selectBuilder.ToString();
            string createStatementFormat = createBuilder.ToString();

            // add a row with nulls only
            using SqlConnection con = new SqlConnection(DataTestUtility.TCPConnectionString);
            using SqlCommand cmd = con.CreateCommand();
            try
            {
                con.Open();

                cmd.CommandType = CommandType.Text;
                cmd.CommandText = string.Format(createStatementFormat, tempTableName);
                cmd.ExecuteNonQuery();

                cmd.CommandText = string.Format("INSERT INTO {0} ([ID]) VALUES (0)", tempTableName);// insert row with values set to their defaults (DBNULL)
                cmd.ExecuteNonQuery();

                // run the test cases
                Assert.True(IsColumnBitSet(con, string.Format("SELECT [ID], [CSET], [C1] FROM {0}", tempTableName), indexOfColumnSet: 1));
            }
            finally
            {
                // drop the temp table to release its resources
                cmd.CommandText = "DROP TABLE " + tempTableName;
                cmd.ExecuteNonQuery();
            }
        }

        // Synapse: Statement 'Drop Database' is not supported in this version of SQL Server.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("KAZAKH_90_CI_AI")]
        [InlineData("Georgian_Modern_Sort_CI_AS")]
        public static void CollatedDataReaderTest(string collation)
        {
            string dbName = DataTestUtility.GetUniqueName("CollationTest", false);

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = dbName,
                Pooling = false
            };

            using SqlConnection con = new(DataTestUtility.TCPConnectionString);
            using SqlCommand cmd = con.CreateCommand();
            try
            {
                con.Open();

                // Create collated database
                cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {collation}";
                cmd.ExecuteNonQuery();

                //Create connection without pooling in order to delete database later.
                using (SqlConnection dbCon = new(builder.ConnectionString))
                using (SqlCommand dbCmd = dbCon.CreateCommand())
                {
                    string data = Guid.NewGuid().ToString();

                    dbCon.Open();
                    dbCmd.CommandText = $"SELECT '{data}'";
                    using SqlDataReader reader = dbCmd.ExecuteReader();
                    reader.Read();
                    Assert.Equal(data, reader.GetString(0));
                }
            }
            finally
            {
                // Let connection close safely before dropping database for slow servers.
                Thread.Sleep(500);
                DataTestUtility.DropDatabase(con, dbName);
            }
        }

        private static bool IsColumnBitSet(SqlConnection con, string selectQuery, int indexOfColumnSet)
        {
            bool columnSetPresent = false;
            {
                using SqlCommand cmd = con.CreateCommand();
                cmd.CommandText = selectQuery;
                using SqlDataReader reader = cmd.ExecuteReader();
                DataTable schemaTable = reader.GetSchemaTable();
                for (int i = 0; i < schemaTable.Rows.Count; i++)
                {
                    bool isColumnSet = (bool)schemaTable.Rows[i]["IsColumnSet"];

                    if (indexOfColumnSet == i)
                    {
                        columnSetPresent = true;
                    }
                }
            }
            return columnSetPresent;
        }

        // Synapse: Enforced unique constraints are not supported. To create an unenforced unique constraint you must include the NOT ENFORCED syntax as part of your statement.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CheckHiddenColumns()
        {
            // hidden columns can be found by using CommandBehavior.KeyInfo or at the sql level
            // by using the FOR BROWSE option. These features return the column information requested and
            // also include any key information required to be able to find the row containing the data
            // requested. The additional key information is provided as hidden columns and can be seen using
            // the difference between VisibleFieldCount and FieldCount on the reader

            string tempTableName = DataTestUtility.GenerateObjectName();

            string createQuery = $@"
                create table [{tempTableName}] (
	                user_id int not null identity(1,1),
	                first_name varchar(100) null,
	                last_name varchar(100) null);

                alter table [{tempTableName}] add constraint pk_{tempTableName}_user_id primary key (user_id);

                insert into [{tempTableName}] (first_name,last_name) values ('Joe','Smith')
                ";

            string dataQuery = $@"select first_name, last_name from [{tempTableName}]";

            int fieldCount = 0;
            int visibleFieldCount = 0;
            Type[] types = null;
            string[] names = null;

            using (SqlConnection connection = new(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                try
                {
                    using (SqlCommand createCommand = new(createQuery, connection))
                    {
                        createCommand.ExecuteNonQuery();
                    }
                    using SqlCommand queryCommand = new(dataQuery, connection);
                    using SqlDataReader reader = queryCommand.ExecuteReader(CommandBehavior.KeyInfo);
                    fieldCount = reader.FieldCount;
                    visibleFieldCount = reader.VisibleFieldCount;
                    types = new Type[fieldCount];
                    names = new string[fieldCount];
                    for (int index = 0; index < fieldCount; index++)
                    {
                        types[index] = reader.GetFieldType(index);
                        names[index] = reader.GetName(index);
                    }
                }
                finally
                {
                    DataTestUtility.DropTable(connection, tempTableName);
                }
            }

            Assert.Equal(3, fieldCount);
            Assert.Equal(2, visibleFieldCount);
            Assert.NotNull(types);
            Assert.NotNull(names);

            // requested fields
            Assert.Contains("first_name", names, StringComparer.Ordinal);
            Assert.Contains("last_name", names, StringComparer.Ordinal);

            // hidden field
            Assert.Contains("user_id", names, StringComparer.Ordinal);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CheckNullRowVersionIsBDNull()
        {
            lock (s_rowVersionLock)
            {
                Tristate originalValue = SetLegacyRowVersionNullBehavior(Tristate.False);
                try
                {
                    using SqlConnection con = new(DataTestUtility.TCPConnectionString);
                    con.Open();
                    using SqlCommand command = con.CreateCommand();
                    command.CommandText = "select cast(null as rowversion) rv";
                    using SqlDataReader reader = command.ExecuteReader();
                    reader.Read();
                    Assert.True(reader.IsDBNull(0));
                    Assert.Equal(DBNull.Value, reader[0]);
                    var result = reader.GetValue(0);
                    Assert.IsType<DBNull>(result);
                    Assert.Equal(result, reader.GetFieldValue<DBNull>(0));
                    Assert.Throws<SqlNullValueException>(() => reader.GetFieldValue<byte[]>(0));

                    SqlBinary binary = reader.GetSqlBinary(0);
                    Assert.True(binary.IsNull);

                    SqlBytes bytes = reader.GetSqlBytes(0);
                    Assert.True(bytes.IsNull);
                    Assert.Null(bytes.Buffer);

                }
                finally
                {
                    SetLegacyRowVersionNullBehavior(originalValue);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static int CanReadEmployeesTableCompletely()
        {
            int counter = 0;

            using (var conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                using (var cmd = new SqlCommand("SELECT EmployeeID,LastName,FirstName,Title,TitleOfCourtesy,BirthDate,HireDate,Address,City,Region,PostalCode,Country,HomePhone,Extension,Photo,Notes,ReportsTo,PhotoPath FROM Employees WHERE ReportsTo = @p0 OR (ReportsTo IS NULL AND @p0 IS NULL)", conn))
                {
                    cmd.Parameters.AddWithValue("@p0", 5);

                    conn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            for (int index = 0; index < reader.FieldCount; index++)
                            {
                                if (!reader.IsDBNull(index))
                                {
                                    object value = reader[index];
                                    counter += 1;
                                }
                            }
                        }
                    }
                }
            }

            return counter;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task<int> CanReadEmployeesTableCompletelyAsync()
        {
            int counter = 0;

            using (var conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                using (var cmd = new SqlCommand("SELECT EmployeeID,LastName,FirstName,Title,TitleOfCourtesy,BirthDate,HireDate,Address,City,Region,PostalCode,Country,HomePhone,Extension,Photo,Notes,ReportsTo,PhotoPath FROM Employees WHERE ReportsTo = @p0 OR (ReportsTo IS NULL AND @p0 IS NULL)", conn))
                {
                    cmd.Parameters.AddWithValue("@p0", 5);

                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int index = 0; index < reader.FieldCount; index++)
                            {
                                if (!await reader.IsDBNullAsync(index))
                                {
                                    object value = reader[index];
                                    counter += 1;
                                }
                            }
                        }
                    }
                }
            }

            return counter;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task<int> CanReadEmployeesTableCompletelyWithNullImageAsync()
        {
            int counter = 0;

            using (var conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                using (var cmd = new SqlCommand("SELECT EmployeeID,LastName,FirstName,Title,TitleOfCourtesy,BirthDate,HireDate,Address,City,Region,PostalCode,Country,HomePhone,Extension,convert(image,NULL) as Photo,Notes,ReportsTo,PhotoPath FROM Employees WHERE ReportsTo = @p0 OR (ReportsTo IS NULL AND @p0 IS NULL)", conn))
                {
                    cmd.Parameters.AddWithValue("@p0", 5);

                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int index = 0; index < reader.FieldCount; index++)
                            {
                                if (!await reader.IsDBNullAsync(index))
                                {
                                    object value = reader[index];
                                    counter += 1;
                                }
                            }
                        }
                    }
                }
            }

            return counter;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task<int> CanReadXmlData()
        {
            const string xml = @"<catalog id1=""00000000-0000-0000-0000-000000000000"" id2=""00000000-0000-0000-0000-000000000000"" id3=""00000000-0000-0000-0000-000000000000"" id4=""00000000-0000-0000-0000-000000000000"" id5=""00000000-0000-0000-0000-000000000000"">
   <book id=""bk101"">
      <author>Gambardella, Matthew</author>
      <title>XML Developer's Guide</title>
      <genre>Computer</genre>
      <price>44.95</price>
      <publish_date>2000-10-01</publish_date>
      <description>An in-depth look at creating applications 
      with XML.</description>
   </book>
   <book id=""bk102"">
      <author>Ralls, Kim</author>
      <title>Midnight Rain</title>
      <genre>Fantasy</genre>
      <price>5.95</price>
      <publish_date>2000-12-16</publish_date>
      <description>A former architect battles corporate zombies, 
      an evil sorceress, and her own childhood to become queen 
      of the world.</description>
   </book>
   <book id=""bk103"">
      <author>Corets, Eva</author>
      <title>Maeve Ascendant</title>
      <genre>Fantasy</genre>
      <price>5.95</price>
      <publish_date>2000-11-17</publish_date>
      <description>After the collapse of a nanotechnology 
      society in England, the young survivors lay the 
      foundation for a new society.</description>
   </book>
   <book id=""bk104"">
      <author>Corets, Eva</author>
      <title>Oberon's Legacy</title>
      <genre>Fantasy</genre>
      <price>5.95</price>
      <publish_date>2001-03-10</publish_date>
      <description>In post-apocalypse England, the mysterious 
      agent known only as Oberon helps to create a new life 
      for the inhabitants of London. Sequel to Maeve 
      Ascendant.</description>
   </book>
   <book id=""bk105"">
      <author>Corets, Eva</author>
      <title>The Sundered Grail</title>
      <genre>Fantasy</genre>
      <price>5.95</price>
      <publish_date>2001-09-10</publish_date>
      <description>The two daughters of Maeve, half-sisters, 
      battle one another for control of England. Sequel to 
      Oberon's Legacy.</description>
   </book>
   <book id=""bk106"">
      <author>Randall, Cynthia</author>
      <title>Lover Birds</title>
      <genre>Romance</genre>
      <price>4.95</price>
      <publish_date>2000-09-02</publish_date>
      <description>When Carla meets Paul at an ornithology 
      conference, tempers fly as feathers get ruffled.</description>
   </book>
   <book id=""bk107"">
      <author>Thurman, Paula</author>
      <title>Splish Splash</title>
      <genre>Romance</genre>
      <price>4.95</price>
      <publish_date>2000-11-02</publish_date>
      <description>A deep sea diver finds true love twenty 
      thousand leagues beneath the sea.</description>
   </book>
   <book id=""bk108"">
      <author>Knorr, Stefan</author>
      <title>Creepy Crawlies</title>
      <genre>Horror</genre>
      <price>4.95</price>
      <publish_date>2000-12-06</publish_date>
      <description>An anthology of horror stories about roaches,
      centipedes, scorpions  and other insects.</description>
   </book>
   <book id=""bk109"">
      <author>Kress, Peter</author>
      <title>Paradox Lost</title>
      <genre>Science Fiction</genre>
      <price>6.95</price>
      <publish_date>2000-11-02</publish_date>
      <description>After an inadvertant trip through a Heisenberg
      Uncertainty Device, James Salway discovers the problems 
      of being quantum.</description>
   </book>
   <book id=""bk110"">
      <author>O'Brien, Tim</author>
      <title>Microsoft .NET: The Programming Bible</title>
      <genre>Computer</genre>
      <price>36.95</price>
      <publish_date>2000-12-09</publish_date>
      <description>Microsoft's .NET initiative is explored in 
      detail in this deep programmer's reference.</description>
   </book>
   <book id=""bk111"">
      <author>O'Brien, Tim</author>
      <title>MSXML3: A Comprehensive Guide</title>
      <genre>Computer</genre>
      <price>36.95</price>
      <publish_date>2000-12-01</publish_date>
      <description>The Microsoft MSXML3 parser is covered in 
      detail, with attention to XML DOM interfaces, XSLT processing, 
      SAX and more.</description>
   </book>
   <book id=""bk112"">
      <author>Galos, Mike</author>
      <title>Visual Studio 7: A Comprehensive Guide</title>
      <genre>Computer</genre>
      <price>49.95</price>
      <publish_date>2001-04-16</publish_date>
      <description>Microsoft Visual Studio 7 is explored in depth,
      looking at how Visual Basic, Visual C++, C#, and ASP+ are 
      integrated into a comprehensive development 
      environment.</description>
   </book>
</catalog>";

            string tableName = DataTestUtility.GenerateObjectName();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.PersistSecurityInfo = true;
            builder.PacketSize = 3096; // should reproduce failure that this tests for in <100 reads

            using (var connection = new SqlConnection(builder.ToString()))
            {
                await connection.OpenAsync();

                try
                {
                    // setup
                    using (var dropCommand = connection.CreateCommand())
                    {
                        dropCommand.CommandText = $"DROP TABLE IF EXISTS [{tableName}]";
                        dropCommand.ExecuteNonQuery();
                    }

                    using (var createCommand = connection.CreateCommand())
                    {
                        createCommand.CommandText = $"CREATE TABLE [{tableName}] (Id int PRIMARY KEY, Data xml NOT NULL)";
                        createCommand.ExecuteNonQuery();
                    }

                    for (var i = 0; i < 100; i++)
                    {
                        using (var insertCommand = connection.CreateCommand())
                        {
                            insertCommand.CommandText = $"INSERT INTO [{tableName}] (Id, Data) VALUES (@id, @data)";
                            insertCommand.Parameters.AddWithValue("@id", i);
                            insertCommand.Parameters.AddWithValue("@data", xml);
                            insertCommand.ExecuteNonQuery();
                        }
                    }

                    // execute
                    int id = 0;

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"SELECT Data FROM [{tableName}] ORDER BY Id";
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            string expectedResult = null;
                            while (await reader.ReadAsync())
                            {
                                var result = reader.GetString(0);
                                if (expectedResult == null)
                                {
                                    expectedResult = result;
                                }
                                else
                                {
                                    Assert.Equal(expectedResult, result);
                                }
                                id++;
                            }
                        }
                    }
                    return id;

                }
                finally
                {
                    try
                    {
                        using (var dropCommand = connection.CreateCommand())
                        {
                            dropCommand.CommandText = $"DROP TABLE IF EXISTS [{tableName}]";
                            dropCommand.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task CanReadSequentialDecreasingChunks()
        {
            const string baseString = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            StringBuilder inputBuilder = new StringBuilder();
            while (inputBuilder.Length < (64 * 1024))
            {
                inputBuilder.Append(baseString);
                inputBuilder.Append(' ');
            }

            string input = inputBuilder.ToString();

            StringBuilder resultBuilder = new StringBuilder();
            CancellationTokenSource cts = new CancellationTokenSource();
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync(cts.Token);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT CONVERT(varchar(max),@str) as a";
                    command.Parameters.AddWithValue("@str", input);

                    using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cts.Token))
                    {
                        if (await reader.ReadAsync(cts.Token))
                        {
                            using (var textReader = reader.GetTextReader(0))
                            {
                                var buffer = new char[4096];
                                var charsReadCount = -1;
                                var start = 0;
                                while (charsReadCount != 0)
                                {
                                    charsReadCount = await textReader.ReadAsync(buffer, start, buffer.Length - start);
                                    resultBuilder.Append(buffer, start, charsReadCount);
                                    start++;
                                }
                            }
                        }
                    }
                }
            }

            string result = resultBuilder.ToString();

            Assert.Equal(input, result);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task CanReadBinaryData()
        {
            const int Size = 20_000;

            byte[] data = Enumerable.Range(0, Size)
                .Select(i => (byte)(i % 256))
                .ToArray();
            string tableName = DataTestUtility.GenerateObjectName();

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                try
                {
                    using (var createCommand = connection.CreateCommand())
                    {
                        createCommand.CommandText = $@"
DROP TABLE IF EXISTS [{tableName}]
CREATE TABLE [{tableName}] (Id INT IDENTITY(1,1) PRIMARY KEY, Data VARBINARY(MAX));
INSERT INTO [{tableName}] (Data) VALUES (@data);";
                        createCommand.Parameters.Add(new SqlParameter("@data", SqlDbType.VarBinary, Size) { Value = data });
                        await createCommand.ExecuteNonQueryAsync();
                    }

                    using (var command = connection.CreateCommand())
                    {

                        command.CommandText = $"SELECT Data FROM [{tableName}]";
                        command.Parameters.Clear();
                        var result = (byte[])await command.ExecuteScalarAsync();

                        Assert.Equal(data, result);
                    }

                }
                finally
                {
                    try
                    {
                        using (var dropCommand = connection.CreateCommand())
                        {
                            dropCommand.CommandText = $"DROP TABLE IF EXISTS [{tableName}]";
                            dropCommand.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Synapse: Cannot find data type 'rowversion'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CheckLegacyNullRowVersionIsEmptyArray()
        {
            lock (s_rowVersionLock)
            {
                Tristate originalValue = SetLegacyRowVersionNullBehavior(Tristate.True);
                try
                {
                    using SqlConnection con = new(DataTestUtility.TCPConnectionString);
                    con.Open();
                    using SqlCommand command = con.CreateCommand();
                    command.CommandText = "select cast(null as rowversion) rv";
                    using SqlDataReader reader = command.ExecuteReader();
                    reader.Read();
                    Assert.False(reader.IsDBNull(0));
                    SqlBinary value = reader.GetSqlBinary(0);
                    Assert.False(value.IsNull);
                    Assert.Equal(0, value.Length);
                    Assert.NotNull(value.Value);
                    var result = reader.GetValue(0);
                    Assert.IsType<byte[]>(result);
                    Assert.Equal(result, reader.GetFieldValue<byte[]>(0));
                }
                finally
                {
                    SetLegacyRowVersionNullBehavior(originalValue);
                }
            }
        }

        private static Tristate SetLegacyRowVersionNullBehavior(Tristate value)
        {
            Type switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
            FieldInfo switchField = switchesType.GetField("s_legacyRowVersionNullBehavior", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Tristate originalValue = (Tristate)switchField.GetValue(null);
            switchField.SetValue(null, value);
            return originalValue;
        }
    }
}
