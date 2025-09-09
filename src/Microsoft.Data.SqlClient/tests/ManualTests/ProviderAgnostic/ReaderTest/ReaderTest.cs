// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ReaderTest
    {
        // TODO Synapse: Remove dependency from Northwind database
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestMain()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            string tempTable = DataTestUtility.GetLongName("table");

            DbProviderFactory provider = SqlClientFactory.Instance;
            try
            {
                using (DbConnection con = provider.CreateConnection())
                {
                    con.ConnectionString = connectionString;
                    con.Open();

                    using (DbCommand cmd = provider.CreateCommand())
                    {
                        cmd.Connection = con;
                        DbTransaction tx;

                        #region <<Create temp table>>
                        cmd.CommandText = "SELECT LastName, FirstName, Title, Address, City, Region, PostalCode, Country into " + tempTable + " from Employees where EmployeeID=0";
                        cmd.ExecuteNonQuery();

                        #endregion

                        tx = con.BeginTransaction();
                        cmd.Transaction = tx;

                        cmd.CommandText = "insert into " + tempTable + "(LastName, FirstName, Title, Address, City, Region, PostalCode, Country) values ('Doe', 'Jane' , 'Ms.', 'One Microsoft Way', 'Redmond', 'WA', '98052', 'USA')";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "insert into " + tempTable + "(LastName, FirstName, Title, Address, City, Region, PostalCode, Country) values ('Doe', 'John' , 'Mr.', NULL, NULL, NULL, NULL, NULL)";
                        cmd.ExecuteNonQuery();

                        tx.Commit();

                        cmd.Transaction = null;
                        string parameterName = "@p1";
                        DbParameter p1 = cmd.CreateParameter();
                        p1.ParameterName = parameterName;
                        p1.Value = "Doe";
                        cmd.Parameters.Add(p1);

                        cmd.CommandText = "select * from " + tempTable + " where LastName = " + parameterName;

                        // Test GetValue + IsDBNull
                        using (DbDataReader rdr = cmd.ExecuteReader())
                        {
                            StringBuilder actualResult = new StringBuilder();
                            int currentValue = 0;
                            string[] expectedValues =
                            {
                                "Doe,Jane,Ms.,One Microsoft Way,Redmond,WA,98052,USA",
                                "Doe,John,Mr.,(NULL),(NULL),(NULL),(NULL),(NULL)"
                            };

                            while (rdr.Read())
                            {
                                Assert.True(currentValue < expectedValues.Length, "ERROR: Received more values than expected");

                                for (int i = 0; i < rdr.FieldCount; i++)
                                {
                                    if (i > 0)
                                    {
                                        actualResult.Append(",");
                                    }
                                    if (rdr.IsDBNull(i))
                                    {
                                        actualResult.Append("(NULL)");
                                    }
                                    else
                                    {
                                        actualResult.Append(rdr.GetValue(i));
                                    }
                                }

                                DataTestUtility.AssertEqualsWithDescription(expectedValues[currentValue++], actualResult.ToString(), "FAILED: Did not receive expected data");
                                actualResult.Clear();
                            }
                        }

                        // Test GetFieldValue<T> + IsDBNull
                        using (DbDataReader rdr = cmd.ExecuteReader())
                        {
                            StringBuilder actualResult = new StringBuilder();
                            int currentValue = 0;
                            string[] expectedValues =
                            {
                                "Doe,Jane,Ms.,One Microsoft Way,Redmond,WA,98052,USA",
                                "Doe,John,Mr.,(NULL),(NULL),(NULL),(NULL),(NULL)"
                            };

                            while (rdr.Read())
                            {
                                Assert.True(currentValue < expectedValues.Length, "ERROR: Received more values than expected");

                                for (int i = 0; i < rdr.FieldCount; i++)
                                {
                                    if (i > 0)
                                    {
                                        actualResult.Append(",");
                                    }
                                    if (rdr.IsDBNull(i))
                                    {
                                        actualResult.Append("(NULL)");
                                    }
                                    else
                                    {
                                        if (rdr.GetFieldType(i) == typeof(bool))
                                        {
                                            actualResult.Append(rdr.GetFieldValue<bool>(i));
                                        }
                                        else if (rdr.GetFieldType(i) == typeof(decimal))
                                        {
                                            actualResult.Append(rdr.GetFieldValue<decimal>(i));
                                        }
                                        else if (rdr.GetFieldType(i) == typeof(int))
                                        {
                                            actualResult.Append(rdr.GetFieldValue<int>(i));
                                        }
                                        else
                                        {
                                            actualResult.Append(rdr.GetFieldValue<string>(i));
                                        }
                                    }
                                }

                                DataTestUtility.AssertEqualsWithDescription(expectedValues[currentValue++], actualResult.ToString(), "FAILED: Did not receive expected data");
                                actualResult.Clear();
                            }
                        }

                        // Test GetFieldValueAsync<T> + IsDBNullAsync
                        using (DbDataReader rdr = cmd.ExecuteReaderAsync().Result)
                        {
                            StringBuilder actualResult = new StringBuilder();
                            int currentValue = 0;
                            string[] expectedValues =
                            {
                                "Doe,Jane,Ms.,One Microsoft Way,Redmond,WA,98052,USA",
                                "Doe,John,Mr.,(NULL),(NULL),(NULL),(NULL),(NULL)"
                            };

                            while (rdr.ReadAsync().Result)
                            {
                                Assert.True(currentValue < expectedValues.Length, "ERROR: Received more values than expected");

                                for (int i = 0; i < rdr.FieldCount; i++)
                                {
                                    if (i > 0)
                                    {
                                        actualResult.Append(",");
                                    }
                                    if (rdr.IsDBNullAsync(i).Result)
                                    {
                                        actualResult.Append("(NULL)");
                                    }
                                    else
                                    {
                                        if (rdr.GetFieldType(i) == typeof(bool))
                                        {
                                            actualResult.Append(rdr.GetFieldValueAsync<bool>(i).Result);
                                        }
                                        else if (rdr.GetFieldType(i) == typeof(decimal))
                                        {
                                            actualResult.Append(rdr.GetFieldValueAsync<decimal>(i).Result);
                                        }
                                        else if (rdr.GetFieldType(i) == typeof(int))
                                        {
                                            actualResult.Append(rdr.GetFieldValue<int>(i));
                                        }
                                        else
                                        {
                                            actualResult.Append(rdr.GetFieldValueAsync<string>(i).Result);
                                        }
                                    }
                                }

                                DataTestUtility.AssertEqualsWithDescription(expectedValues[currentValue++], actualResult.ToString(), "FAILED: Did not receive expected data");
                                actualResult.Clear();
                            }
                        }
                    }

                    // GetStream
                    byte[] correctBytes = { 0x12, 0x34, 0x56, 0x78 };
                    string queryString;
                    string correctBytesAsString = "0x12345678";
                    queryString = string.Format("SELECT CAST({0} AS BINARY(20)), CAST({0} AS IMAGE), CAST({0} AS VARBINARY(20))", correctBytesAsString);
                    using (var command = provider.CreateCommand())
                    {
                        command.CommandText = queryString;
                        command.Connection = con;
                        using (var reader = command.ExecuteReader())
                        {
                            reader.Read();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                byte[] buffer = new byte[256];
                                Stream stream = reader.GetStream(i);
                                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                for (int j = 0; j < correctBytes.Length; j++)
                                {
                                    Assert.True(correctBytes[j] == buffer[j], "ERROR: Bytes do not match");
                                }
                            }
                        }
                    }

                    // GetTextReader
                    string[] correctStrings = { "Hello World", "\uFF8A\uFF9B\uFF70\uFF9C\uFF70\uFF99\uFF84\uFF9E" };
                    string[] collations = { "Latin1_General_CI_AS", "Japanese_CI_AS" };

                    for (int j = 0; j < collations.Length; j++)
                    {
                        string substring = string.Format("(N'{0}' COLLATE {1})", correctStrings[j], collations[j]);
                        queryString = string.Format("SELECT CAST({0} AS CHAR(20)), CAST({0} AS NCHAR(20)), CAST({0} AS NTEXT), CAST({0} AS NVARCHAR(20)), CAST({0} AS TEXT), CAST({0} AS VARCHAR(20))", substring);
                        using (var command = provider.CreateCommand())
                        {
                            command.CommandText = queryString;
                            command.Connection = con;
                            using (var reader = command.ExecuteReader())
                            {
                                reader.Read();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    char[] buffer = new char[256];
                                    TextReader textReader = reader.GetTextReader(i);
                                    int charsRead = textReader.Read(buffer, 0, buffer.Length);
                                    string stringRead = new string(buffer, 0, charsRead);

                                    Assert.True(stringRead == (string)reader.GetValue(i), "ERROR: Strings to not match");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                using (DbConnection con = provider.CreateConnection())
                {
                    con.ConnectionString = connectionString;
                    con.Open();

                    using (DbCommand cmd = provider.CreateCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "drop table " + tempTable;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Covers GetFieldValue<T> for SqlBuffer class
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void SqlDataReader_SqlBuffer_GetFieldValue()
        {
            string tableName = DataTestUtility.GetLongName("SqlBuffer_GetFieldValue");
            DateTimeOffset dtoffset = DateTimeOffset.Now;
            DateTime dt = DateTime.Now;
            //Exclude the millisecond because of rounding at some points by SQL Server.
            DateTime dateTime = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            //Arrange
            DbProviderFactory provider = SqlClientFactory.Instance;

            using DbConnection con = provider.CreateConnection();
            con.ConnectionString = DataTestUtility.TCPConnectionString;
            con.Open();
            string sqlQueryOne = $"CREATE TABLE {tableName} ([CustomerId] [int],[FirstName] [nvarchar](50),[BoolCol] [BIT],[ShortCol] [SMALLINT],[ByteCol] [TINYINT],[LongCol] [BIGINT]);";
            string sqlQueryTwo = $"ALTER TABLE {tableName} ADD [DoubleCol] [FLOAT],[SingleCol] [REAL],[GUIDCol] [uniqueidentifier],[DateTimeCol] [DateTime],[DecimalCol] [SmallMoney],[DateTimeOffsetCol] [DateTimeOffset], [DateCol] [Date], [TimeCol] [Time];";

            try
            {
                using (DbCommand command = provider.CreateCommand())
                {
                    command.Connection = con;
                    command.CommandText = sqlQueryOne;
                    command.ExecuteNonQuery();
                }
                using (DbCommand command = provider.CreateCommand())
                {
                    command.Connection = con;
                    command.CommandText = sqlQueryTwo;
                    command.ExecuteNonQuery();
                }

                System.Data.SqlTypes.SqlGuid sqlguid = new System.Data.SqlTypes.SqlGuid(Guid.NewGuid());

                using (SqlCommand sqlCommand = new SqlCommand("", con as SqlConnection))
                {
                    sqlCommand.CommandText = $"INSERT INTO {tableName} "
                                             + "VALUES (@CustomerId,@FirstName,@BoolCol,@ShortCol,@ByteCol,@LongCol,@DoubleCol,@SingleCol"
                                            + ",@GUIDCol,@DateTimeCol,@DecimalCol,@DateTimeOffsetCol,@DateCol,@TimeCol)";
                    sqlCommand.Parameters.AddWithValue(@"CustomerId", 1);
                    sqlCommand.Parameters.AddWithValue(@"FirstName", "Microsoft");
                    sqlCommand.Parameters.AddWithValue(@"BoolCol", true);
                    sqlCommand.Parameters.AddWithValue(@"ShortCol", 3274);
                    sqlCommand.Parameters.AddWithValue(@"ByteCol", 253);
                    sqlCommand.Parameters.AddWithValue(@"LongCol", 922222222222);
                    sqlCommand.Parameters.AddWithValue(@"DoubleCol", 10.7);
                    sqlCommand.Parameters.AddWithValue(@"SingleCol", 123.546f);
                    sqlCommand.Parameters.AddWithValue(@"GUIDCol", sqlguid);
                    sqlCommand.Parameters.AddWithValue(@"DateTimeCol", dateTime);
                    sqlCommand.Parameters.AddWithValue(@"DecimalCol", 280);
                    sqlCommand.Parameters.AddWithValue(@"DateTimeOffsetCol", dtoffset);
                    sqlCommand.Parameters.AddWithValue(@"DateCol", new DateTime(2022, 10, 23));
                    sqlCommand.Parameters.AddWithValue(@"TimeCol", new TimeSpan(0, 22, 7, 44));
                    sqlCommand.ExecuteNonQuery();
                }
                using (SqlCommand sqlCommand = new SqlCommand("", con as SqlConnection))
                {
                    sqlCommand.CommandText = "select top 1 * from " + tableName;
                    using (DbDataReader reader = sqlCommand.ExecuteReader())
                    {
                        Assert.True(reader.Read());
                        Assert.Equal(1, reader.GetFieldValue<int>(0));
                        Assert.Equal("Microsoft", reader.GetFieldValue<string>(1));
                        Assert.True(reader.GetFieldValue<bool>(2));
                        Assert.Equal(3274, reader.GetFieldValue<short>(3));
                        Assert.Equal(253, reader.GetFieldValue<byte>(4));
                        Assert.Equal(922222222222, reader.GetFieldValue<long>(5));
                        Assert.Equal(10.7, reader.GetFieldValue<double>(6));
                        Assert.Equal(123.546f, reader.GetFieldValue<float>(7));
                        Assert.Equal(sqlguid, reader.GetFieldValue<Guid>(8));
                        Assert.Equal(sqlguid.Value, reader.GetFieldValue<System.Data.SqlTypes.SqlGuid>(8).Value);
                        Assert.Equal(dateTime.ToString("dd/MM/yyyy HH:mm:ss.fff"), reader.GetFieldValue<DateTime>(9).ToString("dd/MM/yyyy HH:mm:ss.fff"));
                        Assert.Equal(280, reader.GetFieldValue<decimal>(10));
                        Assert.Equal(dtoffset, reader.GetFieldValue<DateTimeOffset>(11));
                        Assert.Equal(new DateTime(2022, 10, 23), reader.GetFieldValue<DateTime>(12));
                        Assert.Equal(new TimeSpan(0, 22, 7, 44), reader.GetFieldValue<TimeSpan>(13));
#if NET6_0_OR_GREATER
                        Assert.Equal(new DateOnly(2022, 10, 23), reader.GetFieldValue<DateOnly>(12));
                        Assert.Equal(new TimeOnly(22, 7, 44), reader.GetFieldValue<TimeOnly>(13));
#endif
                    }
                }
            }
            finally
            {
                //cleanup
                using (DbCommand cmd = provider.CreateCommand())
                {
                    cmd.Connection = con;
                    cmd.CommandText = "drop table " + tableName;
                    cmd.ExecuteNonQuery();
                }
            }
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Covers GetFieldValue<T> for SqlBuffer class
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task SqlDataReader_SqlBuffer_GetFieldValue_Async()
        {
            string tableName = DataTestUtility.GetLongName("SqlBuffer_GetFieldValue_Async");
            DateTimeOffset dtoffset = DateTimeOffset.Now;
            DateTime dt = DateTime.Now;
            //Exclude the millisecond because of rounding at some points by SQL Server.
            DateTime dateTime = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            //Arrange
            DbProviderFactory provider = SqlClientFactory.Instance;

            using DbConnection con = provider.CreateConnection();
            con.ConnectionString = DataTestUtility.TCPConnectionString;
            con.Open();
            string sqlQueryOne = $"CREATE TABLE {tableName} ([CustomerId] [int],[FirstName] [nvarchar](50),[BoolCol] [BIT],[ShortCol] [SMALLINT],[ByteCol] [TINYINT],[LongCol] [BIGINT]);";
            string sqlQueryTwo = $"ALTER TABLE {tableName} ADD [DoubleCol] [FLOAT],[SingleCol] [REAL],[GUIDCol] [uniqueidentifier],[DateTimeCol] [DateTime],[DecimalCol] [SmallMoney],[DateTimeOffsetCol] [DateTimeOffset], [DateCol] [Date], [TimeCol] [Time];";

            try
            {
                using (DbCommand command = provider.CreateCommand())
                {
                    command.Connection = con;
                    command.CommandText = sqlQueryOne;
                    await command.ExecuteNonQueryAsync();
                }
                using (DbCommand command = provider.CreateCommand())
                {
                    command.Connection = con;
                    command.CommandText = sqlQueryTwo;
                    await command.ExecuteNonQueryAsync();
                }

                System.Data.SqlTypes.SqlGuid sqlguid = new System.Data.SqlTypes.SqlGuid(Guid.NewGuid());

                using (SqlCommand sqlCommand = new SqlCommand("", con as SqlConnection))
                {
                    sqlCommand.CommandText = $"INSERT INTO {tableName} "
                                             + "VALUES (@CustomerId,@FirstName,@BoolCol,@ShortCol,@ByteCol,@LongCol,@DoubleCol,@SingleCol"
                                             + ",@GUIDCol,@DateTimeCol,@DecimalCol,@DateTimeOffsetCol,@DateCol,@TimeCol)";
                    sqlCommand.Parameters.AddWithValue(@"CustomerId", 1);
                    sqlCommand.Parameters.AddWithValue(@"FirstName", "Microsoft");
                    sqlCommand.Parameters.AddWithValue(@"BoolCol", true);
                    sqlCommand.Parameters.AddWithValue(@"ShortCol", 3274);
                    sqlCommand.Parameters.AddWithValue(@"ByteCol", 253);
                    sqlCommand.Parameters.AddWithValue(@"LongCol", 922222222222);
                    sqlCommand.Parameters.AddWithValue(@"DoubleCol", 10.7);
                    sqlCommand.Parameters.AddWithValue(@"SingleCol", 123.546f);
                    sqlCommand.Parameters.AddWithValue(@"GUIDCol", sqlguid);
                    sqlCommand.Parameters.AddWithValue(@"DateTimeCol", dateTime);
                    sqlCommand.Parameters.AddWithValue(@"DecimalCol", 280);
                    sqlCommand.Parameters.AddWithValue(@"DateTimeOffsetCol", dtoffset);
                    sqlCommand.Parameters.AddWithValue(@"DateCol", new DateOnly(2022, 10, 23));
                    sqlCommand.Parameters.AddWithValue(@"TimeCol", new TimeOnly(22, 7, 44));
                    await sqlCommand.ExecuteNonQueryAsync();
                }
                using (SqlCommand sqlCommand = new SqlCommand("", con as SqlConnection))
                {
                    sqlCommand.CommandText = "select top 1 * from " + tableName;
                    using (DbDataReader reader = await sqlCommand.ExecuteReaderAsync())
                    {
                        Assert.True(reader.Read());
                        Assert.Equal(1, await reader.GetFieldValueAsync<int>(0));
                        Assert.Equal("Microsoft", await reader.GetFieldValueAsync<string>(1));
                        Assert.True(await reader.GetFieldValueAsync<bool>(2));
                        Assert.Equal(3274, await reader.GetFieldValueAsync<short>(3));
                        Assert.Equal(253, await reader.GetFieldValueAsync<byte>(4));
                        Assert.Equal(922222222222, await reader.GetFieldValueAsync<long>(5));
                        Assert.Equal(10.7, await reader.GetFieldValueAsync<double>(6));
                        Assert.Equal(123.546f, await reader.GetFieldValueAsync<float>(7));
                        Assert.Equal(sqlguid, await reader.GetFieldValueAsync<Guid>(8));
                        Assert.Equal(sqlguid.Value, (await reader.GetFieldValueAsync<System.Data.SqlTypes.SqlGuid>(8)).Value);
                        Assert.Equal(dateTime.ToString("dd/MM/yyyy HH:mm:ss.fff"), (await reader.GetFieldValueAsync<DateTime>(9)).ToString("dd/MM/yyyy HH:mm:ss.fff"));
                        Assert.Equal(280, await reader.GetFieldValueAsync<decimal>(10));
                        Assert.Equal(dtoffset, await reader.GetFieldValueAsync<DateTimeOffset>(11));
                        Assert.Equal(new DateTime(2022, 10, 23), await reader.GetFieldValueAsync<DateTime>(12));
                        Assert.Equal(new TimeSpan(0, 22, 7, 44), await reader.GetFieldValueAsync<TimeSpan>(13));
                        Assert.Equal(new DateOnly(2022, 10, 23), await reader.GetFieldValueAsync<DateOnly>(12));
                        Assert.Equal(new TimeOnly(22, 7, 44), await reader.GetFieldValueAsync<TimeOnly>(13));
                    }
                }
            }
            finally
            {
                //cleanup
                using (DbCommand cmd = provider.CreateCommand())
                {
                    cmd.Connection = con;
                    cmd.CommandText = "drop table " + tableName;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
#endif
    }
}
