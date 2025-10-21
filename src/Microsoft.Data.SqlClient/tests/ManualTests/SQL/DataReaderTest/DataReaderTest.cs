// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;
using System.Text;
using System.Threading;
using Xunit;

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
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CollatedDataReaderTest()
        {
            string dbName = DataTestUtility.GetShortName("CollationTest", false);

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
                cmd.CommandText = $"CREATE DATABASE {dbName} COLLATE KAZAKH_90_CI_AI";
                cmd.ExecuteNonQuery();

                //Create connection without pooling in order to delete database later.
                using (SqlConnection dbCon = new(builder.ConnectionString))
                using (SqlCommand dbCmd = dbCon.CreateCommand())
                {
                    var data = "TestData";

                    dbCon.Open();
                    dbCmd.CommandText = $"SELECT '{data}'";
                    using SqlDataReader reader = dbCmd.ExecuteReader();
                    reader.Read();
                    Assert.Equal(data, reader.GetString(0));
                }

                // Let connection close safely before dropping database for slow servers.
                Thread.Sleep(500);
            }
            catch (SqlException e)
            {
                Assert.True(false, $"Unexpected Exception occurred: {e.Message}");
            }
            finally
            {
                cmd.CommandText = $"DROP DATABASE {dbName}";
                cmd.ExecuteNonQuery();
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
                bool? originalValue = SetLegacyRowVersionNullBehavior(false);
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

        // Synapse: Cannot find data type 'rowversion'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CheckLegacyNullRowVersionIsEmptyArray()
        {
            lock (s_rowVersionLock)
            {
                bool? originalValue = SetLegacyRowVersionNullBehavior(true);
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

        private static bool? SetLegacyRowVersionNullBehavior(bool? value)
        {
            Type switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
            FieldInfo switchField = switchesType.GetField("s_LegacyRowVersionNullBehavior", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            bool? originalValue = (bool?)switchField.GetValue(null);
            switchField.SetValue(null, value);
            return originalValue;
        }
    }
}
