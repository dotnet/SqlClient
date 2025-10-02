// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class MARSTest
    {
        private static readonly string _connStr = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString;

        private static Dictionary<int, string> companyNames = new()
        {
            { 1, "Exotic Liquids" },
            { 2, "New Orleans Cajun Delights" },
            { 3, "Grandma Kelly's Homestead" },
            { 4, "Tokyo Traders" },
            { 5, "Cooperativa de Quesos 'Las Cabras'" },
            { 6, "Mayumi's" },
            { 7, "Pavlova, Ltd." },
            { 8, "Specialty Biscuits, Ltd." },
            { 9, "PB Knäckebröd AB" },
            { 10, "Refrescos Americanas LTDA" },
            { 11, "Heli Süßwaren GmbH & Co. KG" },
            { 12, "Plutzer Lebensmittelgroßmärkte AG" },
            { 13, "Nord-Ost-Fisch Handelsgesellschaft mbH" },
            { 14, "Formaggi Fortini s.r.l." },
            { 15, "Norske Meierier" },
            { 16, "Bigfoot Breweries" },
            { 17, "Svensk Sjöföda AB" },
            { 18, "Aux joyeux ecclésiastiques" },
            { 19, "New England Seafood Cannery" },
            { 20, "Leka Trading" },
            { 21, "Lyngbysild" },
            { 22, "Zaanse Snoepfabriek" },
            { 23, "Karkki Oy" },
            { 24, "G'day, Mate" },
            { 25, "Ma Maison" },
            { 26, "Pasta Buttini s.r.l." },
            { 27, "Escargots Nouveaux" },
            { 28, "Gai pâturage" },
            { 29, "Forêts d'érables" },
        };

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void NamedPipesMARSTest()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.NPConnectionString);
            builder.MultipleActiveResultSets = true;
            builder.ConnectTimeout = 5;

            using (SqlConnection conn = new SqlConnection(builder.ConnectionString))
            {
                conn.Open();
                using (SqlCommand command = new SqlCommand("SELECT @@SERVERNAME", conn))
                {
                    var result = command.ExecuteScalar();
                    Assert.NotNull(result);
                }
            }
        }

#if DEBUG
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void MARSAsyncTimeoutTest()
        {
            using (SqlConnection connection = new SqlConnection(_connStr))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("WAITFOR DELAY '01:00:00';SELECT 1", connection);
                command.CommandTimeout = 1;
                Task<object> result = command.ExecuteScalarAsync();

                Assert.True(((IAsyncResult)result).AsyncWaitHandle.WaitOne(30 * 1000), "Expected timeout after one second, but no results after 30 seconds");
                Assert.True(result.IsFaulted, string.Format("Expected task result to be faulted, but instead it was {0}", result.Status));
                Assert.True(connection.State == ConnectionState.Open, string.Format("Expected connection to be open after soft timeout, but it was {0}", connection.State));

                Type type = typeof(SqlDataReader).GetTypeInfo().Assembly.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
                FieldInfo field = type.GetField("s_skipSendAttention", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.True(field != null, "Error: This test cannot succeed on retail builds because it uses the s_skipSendAttention test hook");

                field.SetValue(null, true);
                try
                {
                    SqlCommand command2 = new SqlCommand("WAITFOR DELAY '01:00:00';SELECT 1", connection);
                    command2.CommandTimeout = 1;
                    result = command2.ExecuteScalarAsync();

                    Assert.True(((IAsyncResult)result).AsyncWaitHandle.WaitOne(30 * 1000), "Expected timeout after six or so seconds, but no results after 30 seconds");
                    Assert.True(result.IsFaulted, string.Format("Expected task result to be faulted, but instead it was {0}", result.Status));

                    // Pause here to ensure that the async closing is completed
                    Thread.Sleep(200);
                    Assert.True(connection.State == ConnectionState.Closed, string.Format("Expected connection to be closed after hard timeout, but it was {0}", connection.State));
                }
                finally
                {
                    field.SetValue(null, false);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncTimeoutTest()
        {
            using (SqlConnection connection = new SqlConnection(_connStr))
            {
                connection.Open();
                SqlCommand command = new SqlCommand("WAITFOR DELAY '01:00:00';SELECT 1", connection);
                command.CommandTimeout = 1;
                bool hitException = false;
                try
                {
                    object result = command.ExecuteScalar();
                }
                catch (Exception e)
                {
                    Assert.True(e is SqlException, "Expected SqlException but found " + e);
                    hitException = true;
                }
                Assert.True(hitException, "Expected a timeout exception but ExecutScalar succeeded");

                Assert.True(connection.State == ConnectionState.Open, string.Format("Expected connection to be open after soft timeout, but it was {0}", connection.State));

                Type type = typeof(SqlDataReader).GetTypeInfo().Assembly.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
                FieldInfo field = type.GetField("s_skipSendAttention", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.True(field != null, "Error: This test cannot succeed on retail builds because it uses the s_skipSendAttention test hook");

                field.SetValue(null, true);
                hitException = false;
                try
                {
                    SqlCommand command2 = new SqlCommand("WAITFOR DELAY '01:00:00';SELECT 1", connection);
                    command2.CommandTimeout = 1;
                    try
                    {
                        object result = command2.ExecuteScalar();
                    }
                    catch (Exception e)
                    {
                        Assert.True(e is SqlException, "Expected SqlException but found " + e);
                        hitException = true;
                    }
                    Assert.True(hitException, "Expected a timeout exception but ExecutScalar succeeded");

                    Assert.True(connection.State == ConnectionState.Closed, string.Format("Expected connection to be closed after hard timeout, but it was {0}", connection.State));
                }
                finally
                {
                    field.SetValue(null, false);
                }
            }
        }
#endif
        // Synapse: Parallel query execution on the same connection is not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MARSAsyncBusyReaderTest()
        {
            using SqlConnection con = new(_connStr);
            con.Open();

            using SqlCommand com1 = new("select * from Orders", con);
            using SqlDataReader reader1 = await com1.ExecuteReaderAsync();

            int rows1 = 0;
            while (reader1.Read())
            {
                rows1++;
                if (rows1 == 415)
                {
                    break;
                }
            }
            Assert.Equal(415, rows1);

            using SqlCommand com2 = new("select * from Orders", con);
            using SqlDataReader reader2 = await com2.ExecuteReaderAsync();

            int rows2 = 0;
            while (reader2.Read())
            {
                rows2++;
                if (rows2 == 415)
                {
                    break;
                }
            }
            Assert.Equal(415, rows2);

            for (int i = 415; i < 830; i++)
            {
                Assert.True(reader1.Read() && reader2.Read(), "MARS read failure");
                Assert.Equal(reader1.GetInt32(0), reader2.GetInt32(0));
            }

            Assert.True(!reader1.Read() && !reader2.Read(), "MARS read should not have returned more rows");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncBusyReaderTest()
        {
            var query = "SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10";
            var rowCount = 10;

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (SqlDataReader reader1 = (new SqlCommand(query, conn)).ExecuteReader())
                {
                    int rows1 = 0;
                    while (reader1.Read())
                    {
                        rows1++;
                        if (rows1 == rowCount / 2)
                        {
                            break;
                        }
                    }
                    Assert.True(rows1 == rowCount / 2, "MARSSyncBusyReaderTest Failure, #1");

                    using (SqlDataReader reader2 = (new SqlCommand(query, conn)).ExecuteReader())
                    {
                        int rows2 = 0;
                        while (reader2.Read())
                        {
                            rows2++;
                            if (rows2 == rowCount / 2)
                            {
                                break;
                            }
                        }
                        Assert.True(rows2 == rowCount / 2, "MARSSyncBusyReaderTest Failure, #2");

                        for (int i = rowCount / 2; i < rowCount; i++)
                        {
                            Assert.True(reader1.Read() && reader2.Read(), "MARSSyncBusyReaderTest Failure #3");
                            Assert.Equal(reader1.GetInt32(0), reader2.GetInt32(0));
                        }

                        Assert.False(reader1.Read() || reader2.Read(), "MARSSyncBusyReaderTest, Failure #5");
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MARSAsyncExecuteNonQueryTest()
        {
            using SqlConnection con = new(_connStr);
            con.Open();

            using SqlCommand com1 = new("select * from Orders", con);
            using SqlCommand com2 = new("select * from Orders", con);
            using SqlCommand com3 = new("select * from Orders", con);
            using SqlCommand com4 = new("select * from Orders", con);
            using SqlCommand com5 = new("select * from Orders", con);

            Task result1 = com1.ExecuteNonQueryAsync();
            Task result2 = com2.ExecuteNonQueryAsync();
            Task result3 = com3.ExecuteNonQueryAsync();
            Task result4 = com4.ExecuteNonQueryAsync();
            Task result5 = com5.ExecuteNonQueryAsync();

            await result1;
            await result2;
            await result3;
            await result4;
            await result5;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncExecuteNonQueryTest()
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (SqlCommand comm1 = new SqlCommand("select 1", conn))
                using (SqlCommand comm2 = new SqlCommand("select 1", conn))
                using (SqlCommand comm3 = new SqlCommand("select 1", conn))
                using (SqlCommand comm4 = new SqlCommand("select 1", conn))
                using (SqlCommand comm5 = new SqlCommand("select 1", conn))
                {
                    comm1.ExecuteNonQuery();
                    comm2.ExecuteNonQuery();
                    comm3.ExecuteNonQuery();
                    comm4.ExecuteNonQuery();
                    comm5.ExecuteNonQuery();
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MARSAsyncExecuteReaderTest1()
        {
            using SqlConnection con = new(_connStr);
            con.Open();

            using SqlCommand com1 = new("select * from Orders", con);
            using SqlCommand com2 = new("select * from Orders", con);
            using SqlCommand com3 = new("select * from Orders", con);
            using SqlCommand com4 = new("select * from Orders", con);
            using SqlCommand com5 = new("select * from Orders", con);

            Task<SqlDataReader> result1 = com1.ExecuteReaderAsync();
            Task<SqlDataReader> result2 = com2.ExecuteReaderAsync();
            Task<SqlDataReader> result3 = com3.ExecuteReaderAsync();
            Task<SqlDataReader> result4 = com4.ExecuteReaderAsync();
            Task<SqlDataReader> result5 = com5.ExecuteReaderAsync();

            using SqlDataReader reader1 = await result1;
            using SqlDataReader reader2 = await result2;
            using SqlDataReader reader3 = await result3;
            using SqlDataReader reader4 = await result4;
            using SqlDataReader reader5 = await result5;

            int rows = 0;
            while (reader1.Read())
            {
                rows++;
            }
            Assert.Equal(830, rows);

            rows = 0;
            while (reader2.Read())
            {
                rows++;
            }
            Assert.Equal(830, rows);

            rows = 0;
            while (reader3.Read())
            {
                rows++;
            }
            Assert.Equal(830, rows);

            rows = 0;
            while (reader4.Read())
            {
                rows++;
            }
            Assert.Equal(830, rows);

            rows = 0;
            while (reader5.Read())
            {
                rows++;
            }
            Assert.Equal(830, rows);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncExecuteReaderTest1()
        {
            var query = "SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10";
            var rowCount = 10;

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (SqlDataReader reader1 = (new SqlCommand(query, conn)).ExecuteReader())
                using (SqlDataReader reader2 = (new SqlCommand(query, conn)).ExecuteReader())
                using (SqlDataReader reader3 = (new SqlCommand(query, conn)).ExecuteReader())
                using (SqlDataReader reader4 = (new SqlCommand(query, conn)).ExecuteReader())
                using (SqlDataReader reader5 = (new SqlCommand(query, conn)).ExecuteReader())
                {
                    int rows = 0;
                    while (reader1.Read())
                    {
                        rows++;
                    }
                    Assert.True(rows == rowCount, "MARSSyncExecuteReaderTest1 failure, #1");

                    rows = 0;
                    while (reader2.Read())
                    {
                        rows++;
                    }
                    Assert.True(rows == rowCount, "MARSSyncExecuteReaderTest1 failure, #2");

                    rows = 0;
                    while (reader3.Read())
                    {
                        rows++;
                    }
                    Assert.True(rows == rowCount, "MARSSyncExecuteReaderTest1 failure, #3");

                    rows = 0;
                    while (reader4.Read())
                    {
                        rows++;
                    }
                    Assert.True(rows == rowCount, "MARSSyncExecuteReaderTest1 failure, #4");

                    rows = 0;
                    while (reader5.Read())
                    {
                        rows++;
                    }
                    Assert.True(rows == rowCount, "MARSSyncExecuteReaderTest1 failure, #5");
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MARSAsyncExecuteReaderTest2()
        {
            using SqlConnection con = new(_connStr);
            con.Open();

            using SqlCommand com1 = new("select * from Orders", con);
            using SqlCommand com2 = new("select * from Orders", con);
            using SqlCommand com3 = new("select * from Orders", con);
            using SqlCommand com4 = new("select * from Orders", con);
            using SqlCommand com5 = new("select * from Orders", con);

            Task<SqlDataReader> result1 = com1.ExecuteReaderAsync();
            Task<SqlDataReader> result2 = com2.ExecuteReaderAsync();
            Task<SqlDataReader> result3 = com3.ExecuteReaderAsync();
            Task<SqlDataReader> result4 = com4.ExecuteReaderAsync();
            Task<SqlDataReader> result5 = com5.ExecuteReaderAsync();

            using SqlDataReader reader1 = await result1;
            using SqlDataReader reader2 = await result2;
            using SqlDataReader reader3 = await result3;
            using SqlDataReader reader4 = await result4;
            using SqlDataReader reader5 = await result5;

            for (int i = 0; i < 830; i++)
            {
                Assert.True(reader1.Read() && reader2.Read() && reader3.Read() && reader4.Read() && reader5.Read(), "MARSSyncExecuteReaderTest2 Failure #1");
            }

            Assert.False(reader1.Read() || reader2.Read() || reader3.Read() || reader4.Read() || reader5.Read(), "MARSSyncExecuteReaderTest2 Failure #2");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncExecuteReaderTest2()
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (SqlDataReader reader1 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader2 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader3 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader4 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader5 = (new SqlCommand("select 1", conn)).ExecuteReader())
                {
                    Assert.True(reader1.Read() && reader2.Read() && reader3.Read() && reader4.Read() && reader5.Read(), "MARSSyncExecuteReaderTest2 Failure #1");
                    Assert.False(reader1.Read() || reader2.Read() || reader3.Read() || reader4.Read() || reader5.Read(), "MARSSyncExecuteReaderTest2 Failure #2");
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MARSAsyncExecuteReaderTest3()
        {
            using SqlConnection con = new(_connStr);
            con.Open();

            using SqlCommand com1 = new("select * from Orders", con);
            using SqlCommand com2 = new("select * from Orders", con);
            using SqlCommand com3 = new("select * from Orders", con);
            using SqlCommand com4 = new("select * from Orders", con);
            using SqlCommand com5 = new("select * from Orders", con);

            Task<SqlDataReader> result1 = com1.ExecuteReaderAsync();
            Task<SqlDataReader> result2 = com2.ExecuteReaderAsync();
            Task<SqlDataReader> result3 = com3.ExecuteReaderAsync();
            Task<SqlDataReader> result4 = com4.ExecuteReaderAsync();
            Task<SqlDataReader> result5 = com5.ExecuteReaderAsync();

            using SqlDataReader reader1 = await result1;
            using SqlDataReader reader2 = await result2;
            using SqlDataReader reader3 = await result3;
            using SqlDataReader reader4 = await result4;
            using SqlDataReader reader5 = await result5;

            for (int i = 0; i < 830; i++)
            {
                Assert.True(reader1.Read() && reader2.Read() && reader3.Read() && reader4.Read() && reader5.Read(), "MARSSyncExecuteReaderTest2 Failure #1");
                Assert.Equal(reader1.GetInt32(0), reader2.GetInt32(0));
                Assert.Equal(reader2.GetInt32(0), reader3.GetInt32(0));
                Assert.Equal(reader3.GetInt32(0), reader4.GetInt32(0));
                Assert.Equal(reader4.GetInt32(0), reader5.GetInt32(0));
            }

            Assert.False(reader1.Read() || reader2.Read() || reader3.Read() || reader4.Read() || reader5.Read(), "MARSSyncExecuteReaderTest2 Failure #2");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncExecuteReaderTest3()
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (SqlDataReader reader1 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader2 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader3 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader4 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader5 = (new SqlCommand("select 1", conn)).ExecuteReader())
                {
                    Assert.True(reader1.Read() && reader2.Read() && reader3.Read() && reader4.Read() && reader5.Read(), "MARSSyncExecuteReaderTest3 Failure #1");

                    // All reads succeeded - check values.
                    Assert.Equal(reader1.GetInt32(0), reader2.GetInt32(0));
                    Assert.Equal(reader2.GetInt32(0), reader3.GetInt32(0));
                    Assert.Equal(reader3.GetInt32(0), reader4.GetInt32(0));
                    Assert.Equal(reader4.GetInt32(0), reader5.GetInt32(0));

                    Assert.False(reader1.Read() || reader2.Read() || reader3.Read() || reader4.Read() || reader5.Read(), "MARSSyncExecuteReaderTest3 Failure #3");
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MARSAsyncExecuteReaderTest4()
        {
            using SqlConnection con = new(_connStr);
            con.Open();

            using SqlCommand com1 = new("select * from Orders where OrderID = 10248", con);
            using SqlCommand com2 = new("select * from Orders where OrderID = 10249", con);
            using SqlCommand com3 = new("select * from Orders where OrderID = 10250", con);

            Task<SqlDataReader> result1 = com1.ExecuteReaderAsync();
            Task<SqlDataReader> result2 = com2.ExecuteReaderAsync();
            Task<SqlDataReader> result3 = com3.ExecuteReaderAsync();

            using SqlDataReader reader1 = await result1;
            using SqlDataReader reader2 = await result2;
            using SqlDataReader reader3 = await result3;

            Assert.True(reader1.Read() && reader2.Read() && reader3.Read());

            Assert.Equal(10248, reader1.GetInt32(0));
            Assert.Equal(10249, reader2.GetInt32(0));
            Assert.Equal(10250, reader3.GetInt32(0));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSSyncExecuteReaderTest4()
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (SqlDataReader reader1 = (new SqlCommand("select 1", conn)).ExecuteReader())
                using (SqlDataReader reader2 = (new SqlCommand("select 2", conn)).ExecuteReader())
                using (SqlDataReader reader3 = (new SqlCommand("select 3", conn)).ExecuteReader())
                {
                    Assert.True(reader1.Read() && reader2.Read() && reader3.Read(), "MARSSyncExecuteReaderTest4 failure #1");

                    Assert.Equal(1, reader1.GetInt32(0));
                    Assert.Equal(2, reader2.GetInt32(0));
                    Assert.Equal(3, reader3.GetInt32(0));

                    Assert.False(reader1.Read() || reader2.Read() || reader3.Read(), "MARSSyncExecuteReaderTest4 failure #3");
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MARSMultiDataReaderErrTest()
        {
            string queryString = "SELECT 1";

            // With MARS on, one SqlCommand cannot have multiple DataReaders
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                string openReaderExistsMessage = SystemDataResourceManager.Instance.ADP_OpenReaderExists("Command");

                conn.Open();

                using (SqlCommand command = new SqlCommand(queryString, conn))
                {
                    using (SqlDataReader reader1 = command.ExecuteReader())
                    {
                        DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(() =>
                        {
                            SqlDataReader reader2 = command.ExecuteReader();
                        }, openReaderExistsMessage);
                    }
                }
            }

            // With MARS off, one SqlConnection cannot have multiple DataReaders even if they are from different SqlCommands
            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                string openReaderExistsMessage = SystemDataResourceManager.Instance.ADP_OpenReaderExists("Connection");

                conn.Open();

                using (SqlCommand command1 = new SqlCommand(queryString, conn))
                using (SqlCommand command2 = new SqlCommand(queryString, conn))
                {
                    using (SqlDataReader reader1 = command1.ExecuteReader())
                    {
                        DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(() =>
                        {
                            SqlDataReader reader2 = command2.ExecuteReader();
                        }, openReaderExistsMessage);
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MarsScenarioClientJoin()
        {
            SqlConnectionStringBuilder builder = new(_connStr);
            builder.MultipleActiveResultSets = true;
            builder.ConnectTimeout = 5;
            string connectionString = builder.ConnectionString;

            using SqlConnection con = new(connectionString);
            await con.OpenAsync();

            SqlCommand productsCommand = new("SELECT SupplierID FROM dbo.Products ORDER BY UnitsInStock", con);
            using SqlDataReader reader = await productsCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int supplier = reader.GetInt32(0);
                using SqlCommand getSupplierCommand = new("SELECT CompanyName FROM dbo.Suppliers WHERE SupplierID = @ID", con);
                getSupplierCommand.Parameters.Add(new SqlParameter("ID", SqlDbType.Int) { Value = supplier });
                string name = (string)await getSupplierCommand.ExecuteScalarAsync();
                Assert.Equal(companyNames[supplier], name);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void MarsConcurrencyTest()
        {
            var table = DataTestUtility.GenerateObjectName();
            using (var conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                conn.Open();
                using var cmd = new SqlCommand
                {
                    Connection = conn,
                    CommandText = @$"
                        DROP TABLE IF EXISTS [{table}];
                        CREATE TABLE [{table}] (
                            [Id] INTEGER,
                            [IsDeleted] BIT
                        )"
                };

                cmd.ExecuteNonQuery();
            }

            var connString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }.ConnectionString;
            using (var conn = new SqlConnection(connString))
            {
                conn.Open();
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Parallel.For(
                            0, 300,
                            i =>
                            {
                                using var cmd = new SqlCommand
                                {
                                    Connection = conn,
                                    CommandText = @$"
                                SELECT [l].[Id], [l].[IsDeleted]
                                FROM [{table}] AS [l]
                                WHERE ([l].[IsDeleted] = CAST(0 AS bit)) AND [l].[Id] IN (1, 2, 3)"
                                };

                                using SqlDataReader _ = cmd.ExecuteReader();
                            });
                    }
                }
                catch (Exception e)
                {
                    Assert.Fail("CRITIAL: Test should not fail randomly. Exception occurred: " + e.Message);
                }
                finally
                {
                    using var dropConn = new SqlConnection(DataTestUtility.TCPConnectionString);
                    dropConn.Open();
                    DataTestUtility.DropTable(dropConn, table);
                }
            }
        }
    }
}
