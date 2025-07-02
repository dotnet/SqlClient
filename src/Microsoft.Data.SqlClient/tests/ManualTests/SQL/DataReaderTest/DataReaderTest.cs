// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
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

using SwitchesHelper = Microsoft.Data.SqlClient.Tests.Common.LocalAppContextSwitchesHelper;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DataReaderTest
    {
        private static readonly object s_rowVersionLock = new();

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
        public static void CheckNullRowVersionIsDBNull()
        {
            lock (s_rowVersionLock)
            {
                using SwitchesHelper helper = new();
                helper.LegacyRowVersionNullBehaviorField = SwitchesHelper.Tristate.False;

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
            // pattern repeat input allows you to more easily identify if chunks are incorrectly
            //  related to each other by seeing the start and end of sequential chunks and checking
            //  if they correctly move to the next char while debugging
            // simply repeating a single char can't tell you where in the string it went wrong.
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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task CanGetCharsSequentially()
        {
            const CommandBehavior commandBehavior = CommandBehavior.SequentialAccess | CommandBehavior.SingleResult;
            const int length = 32000;
            const string sqlCharWithArg = "SELECT CONVERT(BIGINT, 1) AS [Id], CONVERT(NVARCHAR(MAX), @input) AS [Value];";

            using (var sqlConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await sqlConnection.OpenAsync();

                StringBuilder inputBuilder = new StringBuilder(length);
                Random random = new Random();
                for (int i = 0; i < length; i++)
                {
                    inputBuilder.Append((char)random.Next(0x30, 0x5A));
                }
                string input = inputBuilder.ToString();

                using (var sqlCommand = new SqlCommand())
                {
                    sqlCommand.Connection = sqlConnection;
                    sqlCommand.CommandTimeout = 0;
                    sqlCommand.CommandText = sqlCharWithArg;
                    sqlCommand.Parameters.Add(new SqlParameter("@input", SqlDbType.NVarChar, -1) { Value = input });

                    using (var sqlReader = await sqlCommand.ExecuteReaderAsync(commandBehavior))
                    {
                        if (await sqlReader.ReadAsync())
                        {
                            long id = sqlReader.GetInt64(0);
                            if (id != 1)
                            {
                                Assert.Fail("Id not 1");
                            }

                            var sliced = GetPooledChars(sqlReader, 1, input);
                            if (!sliced.SequenceEqual(input.ToCharArray()))
                            {
                                Assert.Fail("sliced != input");
                            }
                        }
                    }
                }
            }

            static char[] GetPooledChars(SqlDataReader sqlDataReader, int ordinal, string input)
            {
                var buffer = ArrayPool<char>.Shared.Rent(8192);
                int offset = 0;
                while (true)
                {
                    int read = (int)sqlDataReader.GetChars(ordinal, offset, buffer, offset, buffer.Length - offset);
                    if (read == 0)
                    {
                        break;
                    }

                    ReadOnlySpan<char> fetched = buffer.AsSpan(offset, read);
                    ReadOnlySpan<char> origin = input.AsSpan(offset, read);

                    if (!fetched.Equals(origin, StringComparison.Ordinal))
                    {
                        Assert.Fail($"chunk (start:{offset}, for:{read}), is not the same as the input");
                    }

                    offset += read;

                    if (buffer.Length - offset < 128)
                    {
                        buffer = Resize(buffer);
                    }
                }

                var sliced = buffer.AsSpan(0, offset).ToArray();
                ArrayPool<char>.Shared.Return(buffer);
                return sliced;

                static char[] Resize(char[] buffer)
                {
                    var newBuffer = ArrayPool<char>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    ArrayPool<char>.Shared.Return(buffer);
                    return newBuffer;
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task CanReadLargeNTextColumn()
        {
            string stringValue = "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NAoFbTozNjIKBm06NDkwMAoLbTo5OTk4MDMxNDQKBW06Mzk2CgZtOjQ5MzQKBG06NzUKC206OTk5ODA2MDA0CgZtOjYxNDAKBm06NjEzOQoGbTo2MTIwCgVtOjExOAoEbTo3OAoGbTo5MTIxCgZtOjYxMjEKBm06NjEyOAoGbTo2MTMwCgZtOjYxMzEKBm06NjEzMgoGbTo2MTI5CgZtOjYxMjYKBm06NjEyNAoGbTo2MTI1CgZtOjkxNTkKBm06NjEyMwoGbTo2MTIyCgZtOjYxMzMKBW06MTY2CgVtOjE2NwoFbToxNjgKBG06MTcKBm06NTEwOQoGbTo1MTE4CgZtOjUxMTAKBW06MTcxCgVtOjE3MgoGbTo2NDA0CgZtOjkwNDkKBm06OTE2NAoGbTo5MDQ3CgZtOjkxNjIKBm06OTA0OAoGbTo5MTYzCgZtOjkwNTUKBm06OTA1NgoGbTo5MDU3CgZtOjkwNTgKBm06OTA1OQoGbTo5MDYwCgZtOjkxNzQKBm06OTE3MgoGbTo5MTczCgZtOjkxMTMKCm06OTk5ODAyMjkKBW06MzEzCgZtOjQ2MTMKBm06OTEwNgoEbTo3MQoFbTo0MTkKBm06NTYzMwoFbToxMjEKBW06MjE5CgVtOjIxOAoFbToyMjEKBW06MjIwCgVtOjIyMwoFbToyMjIKBm06NDU5OQoGbTo0NTk4CgVtOjI2MAoGbTo0MTA3CgZtOjkwNTMKBW06MjYxCgZtOjQxMDgKBm06OTA1NAoGbTo0NTkzCgZtOjQ1OTIKBm06NDU5MQoGbTo0NTkwCgZtOjQ1ODkKBm06NDU4OAoGbTo0NTk1CgZtOjQ1OTQKBm06NDU5NwoGbTo0NTk2CgZtOjQzNTAKBW06MjU2CgZtOjQxMDMKBm06NDYwMQoGbTo0NjAwCgZtOjkwODcKBm06OTA4OAoFbToyOTQKBm06NDM2OQoGbTo2MTQ1CgZtOjYxNDYKBW06Mjc2CgZtOjQxMjMKBG06NzQKBG06NzMKBm06OTA1MgoGbTo5MTY3CgZtOjkwNTAKBm06OTE2NQoGbTo5MDUxCgZtOjkxNjYKBW06MTYzCgVtOjE2NAoFbToxNjUKBm06OTA2MQoGbTo5MDYyCgZtOjkwNjMKBm06OTA2NAoGbTo5MDY1CgZtOjkwNjYKBm06OTA2OQoGbTo5MDcwCgZtOjkwNDEKBm06OTA0MgoGbTo5MDQzCgZtOjkwNDQKBm06OTA0NQoGbTo5MDQ2CgZtOjYxMTkKBG06MjIKBG06MjMKBG06MjQKBm06NDM2NQoGbTo5MDgzCgZtOjQzNjYKBm06OTA4NAoKbTo5OTk4MDEzNQoFbTo0MTcKBm06NTM3NwoFbToyMDAKBG06NjkKBm06OTE2MAoGbTo5MTYxCgZtOjQ1ODYKBm06NDU4NQoGbTo2NDAyCgZtOjY0MDMKC206OTk5ODAzMTIwCgZtOjYxMzcKBW06MjAxCgVtOjIxNQoGbTo5MTU2CgZtOjkxNTUKBm06OTE1MgoGbTo5MTUxCgZtOjkxNTgKBm06OTE0OAoGbTo5MTU0CgZtOjkxNTMKBm06OTE0NwoGbTo5MTQ2CgZtOjkxNTcKBW06MTc4CgVtOjE3NwoGbTo2MTM0CgZtOjkxNTAKBm06OTE0OQoGbTo2MTM1CgZtOjYxMzYKBW06MjEwCgRtOjc2CgRtOjc3CgRtOjgyCgZtOjUwOTgKBm06NjEyNwoEbTozMgoGbTo1MzcwCgZtOjkxMzgKBm06OTE0MAoGbTo5MTM5CgZtOjUzNzMKBG06ODAKBW06NDM4CgVtOjQzNwoFbTo0MzYKBW06NDM1CgZtOjY0MDEKBm06NjQwNgoGbTo2NDA1CgZtOjkxNzcKBm06OTE3NQoGbTo5MTc2CgZtOjEwMDAKBm06MTAwMQoEbTo2NAoKbTo5OTk4MDEzNgoFbToxOTUKBW06MTE3CgttOjk5OTgwNDAwNQoFbTo0MTYKBm06NTEyNAoFbTo0MTUKBm06NTEyMwoGbTo5MTcwCgZtOjkxNjgKBm06OTE2OQoGbTo5MTcxCgZtOjY0MDcKBm06NjQwOAoFbTozMjkKBm06NDg2NwoGbTo0OTI4CgVtOjM4OQoGbTo5MTI3CgZtOjUxMDQKBW06NDE4CgZtOjUzNzgKBW06MjUxCgZtOjkwODkKBm06OTA5MAoFbToyOTYKBW06Mjk3CgVtOjI2MgoFbToyNjMKBm06OTA3MQoGbTo5MDcyCgZtOjkwOTUKBm06OTA5NgoFbTozMDIKBW06MzAzCgVtOjI2OAoFbToyNjkKBW06MjUwCgVtOjI3OAoFbToyNzkKBW06MjUzCgZtOjkwOTEKBm06OTA5MgoFbToyOTgKBW06Mjk5CgVtOjI2NAoFbToyNjUKBm06OTA3MwoGbTo5MDc0CgZtOjkwOTcKBm06OTA5OAoFbTozMDQKBW06MzA1CgVtOjI3MAoFbToyNzEKBW06MjUyCgVtOjI4MAoFbToyODEKBW06MjU1CgZtOjkwOTMKBm06OTA5NAoFbTozMDAKBW06MzAxCgVtOjI2NgoFbToyNjcKBm06OTA3NQoGbTo5MDc2CgZtOjkwOTkKBm06OTEwMAoFbTozMDYKBW06MzA3CgVtOjI3MgoFbToyNzMKBW06MjU0CgVtOjI4MgoFbToyODMKBm06NDA5OAoGbTo0MzcxCgZtOjQzNzIKBm06NDEwOQoGbTo0MTEwCgZtOjQzNzcKBm06NDM3OAoGbTo0MTE1CgZtOjQxMTYKBm06NDA5NwoGbTo0MzUzCgZtOjQzNTQKBm06NDEwMAoGbTo0MzczCgZtOjQzNzQKBm06NDExMQoGbTo0MTEyCgZtOjQzNzkKBm06NDM4MAoGbTo0MTE3CgZtOjQxMTgKBm06NDA5OQoGbTo0MzU1CgZtOjQzNTYKBm06NDEwMgoGbTo0Mzc1CgZtOjQzNzYKBm06NDExMwoGbTo0MTE0CgZtOjQzODEKBm06NDM4MgoGbTo0MTE5CgZtOjQxMjAKBm06NDEwMQoGbTo0MzU3CgZtOjQzNTgKBW06Mzc3CgZtOjQ5MTUKBm06OTE5OQoGbTo5MTk4CgVtOjQyNgoGbTo1ODk0CgVtOjQyNQoGbTo1ODkzCgVtOjEzNwoFbToxMzUKBW06MTM2CgZtOjkxMzUKBm06OTEzNAoGbTo5MTM2CgNkOjcKA2Q6NAoDZDoxCgNkOjgKA2Q6OQoDZDoyCgNkOjUKA2Q6MwoDZDo2CgRkcDoyCgVkcDozNgoFZHA6MjkKBWRwOjE3CgRkcDo4CgVkcDozMAoEZHA6NwoEZHA6MwoFZHA6MjgKBWRwOjMyCgVkcDoxMAoFZHA6MjcKBWRwOjMzCgVkcDoyMQoFZHA6MTMKBWRwOjI2CgVkcDozOAoFZHA6MTgKBWRwOjI0CgRkcDo5CgVkcDozMQoFZHA6MTYKBWRwOjExCgVkcDoxOQoFZHA6MjIKBWRwOjIzCgVkcDoyMAoFZHA6MzQKBWRwOjM1CgRkcDo2CgVkcDozNwoEZHA6NQoFZHA6MTUKBWRwOjI1CgRkcDo0CgRkcDoxCgVkcDoxMgoFZHA6MTQKBGk6MjYKBWk6MTY0CgVpOjIyMQoFaToyMjIKBGk6MTQKBWk6MTgxCgNpOjQKBGk6MTAKBGk6MTYKBWk6MTUxCgRpOjE3CgVpOjE0OQoEaTo5NAoEaTo5MwoFaToyMjUKBWk6MjE4CgVpOjIxNgoFaToyNjQKBWk6MjY1CgVpOjI1OQoFaToxODUKBGk6OTEKBGk6OTIKBGk6MjEKBWk6Mjc4CgRpOjcwCgRpOjcxCgRpOjU2CgRpOjU1CgVpOjI4MgoFaToyMDIKBWk6MjA2CgRpOjc0CgRpOjUwCgRpOjgzCgRpOjgyCgRpOjgxCgVpOjI4OQoEaTozNAoFaToyNzIKBWk6MjE3CgVpOjI0NwoFaToyNDYKBWk6MTYxCgRpOjg4CgVpOjIwNwoFaToyMDgKBGk6NDMKBGk6NjYKBWk6MjUzCgVpOjI1NAoEaToxMQoEaToxMgoEaTo5MAoEaTo1MwoEaTozOAoFaToyODEKBWk6MTc5CgVpOjE4OQoFaToyMjYKBWk6MjAxCgRpOjI0CgRpOjI1CgVpOjE1MwoDaToxCgRpOjk2CgRpOjk1CgVpOjEzMQoFaToxMzMKBWk6MTMyCgVpOjE5NQoFaToyNTgKBGk6MzAKBGk6NzcKBGk6MzUKBGk6MjcKBGk6NDkKBGk6MzkKBWk6MjYzCgRpOjQ1CgRpOjQ2CgRpOjQ0CgVpOjI4NQoFaToyODQKBWk6MTQyCgVpOjE0NQoFaToxNTIKBWk6MTY2CgVpOjE2NQoFaToxNTgKBWk6MTYzCgVpOjE0NAoFaToxNDMKBWk6MjczCgVpOjI3NAoFaToyNzUKBWk6MjQyCgVpOjEwMwoFaToxNzIKBWk6MTcxCgVpOjI3MQoFaToxMDAKBWk6MTAxCgRpOjk4CgRpOjk5CgRpOjg3CgVpOjEyOAoFaToxMTAKBWk6MTA5CgVpOjEwNgoFaToxMjMKBWk6MTI3CgVpOjExMgoFaToxMDgKBWk6MTExCgVpOjExNgoFaToxMTUKBWk6MTI5CgVpOjEwNwoFaToxMTkKBWk6MTEzCgVpOjExNAoFaToxMTcKBWk6MTIwCgVpOjExOAoFaToxMjEKBWk6MTIyCgVpOjEyNgoFaToxMjQKBWk6MTI1CgRpOjc1CgVpOjI2OAoFaToyNjkKBWk6MTU5CgVpOjI2NgoFaToyNzAKBWk6MTY4CgRpOjEzCgVpOjE1MAoFaToxNjcKBWk6MjU3CgVpOjIxOQoEaTo2MAoEaTo1OQoFaToxMzcKBWk6MTM4CgVpOjIyNwoDaTo5CgVpOjI0MwoEaTo2NQoFaToyMjMKBWk6MjI0CgRpOjY4CgRpOjY0CgVpOjE2MAoFaToyMDMKBGk6ODAKBWk6MTk3CgVpOjE4MgoEaToyOAoEaTo3OQoEaTo4NQoFaToyNjcKBGk6NTEKBGk6NzMKBGk6NzgKBGk6ODQKBGk6ODkKBWk6MTM0CgVpOjE3MwoFaToxMDQKA2k6NwoEaToyMgoEaTo1MgoFaToxNDYKBWk6MTk0CgRpOjM2CgRpOjQwCgVpOjE2MgoFaToyNDUKBWk6MTk5CgRpOjY5CgRpOjY3CgVpOjE1NAoFaToxNzYKBWk6MjIwCgVpOjIzMwoFaToxNTYKBWk6MTkzCgRpOjg2CgVpOjE5OAoFaToyNzcKBWk6MjA5CgVpOjIxMAoFaToyMzIKBWk6MjE1CgRpOjYyCgRpOjMyCgVpOjE3MAoFaToxNTcKBWk6MjUyCgRpOjU0CgRpOjQxCgVpOjE1NQoFaToyNzkKBWk6MTY5CgVpOjE0NwoFaToxNDgKBGk6MzcKBGk6MjkKBWk6MTc4CgVpOjE3NwoFaToyMTMKBGk6MzMKBWk6MjgzCgVpOjE3NQoEaTo1OAoEaToyMAoEaTo2MwoEaToxNQoDaTozCgVpOjIwMAoDaToyCgVpOjI2MQoFaToyODYKBWk6MTM1CgVpOjEzNgoFaToyNzYKBGk6NDcKA2k6OAoEaTo5NwoFaToyODgKBWk6MTM5CgVpOjI4NwoFaToxOTAKBWk6MTkyCgVpOjE5MQoEaToxOAoEaTo3NgoFaToxMzAKBWk6MjA0CgVpOjI2MAoFaToyMzEKBWk6MjE0CgRpOjYxCgVpOjE0MQoFaToyODAKBWk6MjI4CgVpOjIyOQoFaToyMzQKBWk6MjM1CgVpOjI1MAoFaToyNTEKBWk6MTg2CgVpOjE4OAoFaToxODcKBWk6MjMwCgRpOjQyCgVpOjE5NgoFaToyNDAKBWk6MjM5CgVpOjE0MAoEaToyMwoFaToyNDEKBWk6MTc0CgRpOjcyCgVpOjIwNQoFaToyNTYKBWk6MjU1CgRpOjE5CgRpOjQ4CgNpOjYKBWk6MTAyCgVpOjIxMQoFaToyMTIKBGk6NTcKBWk6MjYyCgVpOjI0NAoFaToyMzYKBWk6MjM3CgVpOjIzOAoDaTo1CgVpOjEwNQoFaToyNDgKBWk6MjQ5CgVpOjE4MwoFaToxODQKBGk6MzEKBWk6MTgwCgN0OjMKBHQ6MTEKBHQ6MTAKBHQ6MjEKA3Q6NAoDdDo2CgN0OjkKA3Q6NwoDdDo4CgR0OjEzCgR0OjE4CgR0OjEyCgR0OjE5CgR0OjE3CgN0OjUKBHQ6MTUKA3Q6MQoEdDoyMAoDdDoyCgR0OjE2CgR0OjE0";

            using (var cn = new Microsoft.Data.SqlClient.SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await cn.OpenAsync();

                string tableName = DataTestUtility.GenerateObjectName();

                try
                {
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = $"""
                   create table [{tableName}]
                   (
                       User_ID            varchar(22)      not null,
                       StringName         varchar(255)     not null,
                       IsGlobal           bit              not null,
                       List               ntext,
                       UseProtoSerializer bit,
                       ModuleNameForUse   tinyint,
                       IsReadOnly         bit              not null,
                       VersionNumber      smallint         not null,
                       UserGlobalSet_ID   uniqueidentifier not null,
                       IsProtoCorrected   bit default 1
                   )

                      insert into [{tableName}](User_ID, StringName, IsGlobal, List, UseProtoSerializer, ModuleNameForUse,
                                                             IsReadOnly, VersionNumber, UserGlobalSet_ID, IsProtoCorrected)
                      values ('80004Q4WZ1350KO8NT59RM', '_', 1, '{stringValue}', 1, 2, 1, 1, newid(), 1);

                   """;

                        await cmd.ExecuteNonQueryAsync();

                        cmd.CommandText = $""""
                  SELECT
                  	[gs].[List],
                  	[gs].[UseProtoSerializer] 
                  FROM
                  	[{tableName}] [gs]
                  WHERE
                  	([gs].[IsGlobal] = 1 OR [gs].[User_ID] = '{"80004Q4WZ1350KO8NT59RM"}') AND
                  	([gs].[ModuleNameForUse] IS NULL OR [gs].[ModuleNameForUse] = {2})
                  
                  """";

                        var l = (string)await cmd.ExecuteScalarAsync();
                        Assert.Equal(stringValue, l);

                    }
                }
                finally
                {
                    try
                    {
                        using (var dropCommand = cn.CreateCommand())
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
                using SwitchesHelper helper = new();
                helper.LegacyRowVersionNullBehaviorField = SwitchesHelper.Tristate.True;

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
        }
    }
}
