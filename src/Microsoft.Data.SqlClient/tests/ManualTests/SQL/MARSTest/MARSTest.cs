// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
                FieldInfo field = type.GetField("_skipSendAttention", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.True(field != null, "Error: This test cannot succeed on retail builds because it uses the _skipSendAttention test hook");

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
                FieldInfo field = type.GetField("_skipSendAttention", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.True(field != null, "Error: This test cannot succeed on retail builds because it uses the _skipSendAttention test hook");

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
                            break;
                    }
                    Assert.True(rows1 == rowCount / 2, "MARSSyncBusyReaderTest Failure, #1");

                    using (SqlDataReader reader2 = (new SqlCommand(query, conn)).ExecuteReader())
                    {
                        int rows2 = 0;
                        while (reader2.Read())
                        {
                            rows2++;
                            if (rows2 == rowCount / 2)
                                break;
                        }
                        Assert.True(rows2 == rowCount / 2, "MARSSyncBusyReaderTest Failure, #2");

                        for (int i = rowCount / 2; i < rowCount; i++)
                        {
                            Assert.True(reader1.Read() && reader2.Read(), "MARSSyncBusyReaderTest Failure #3");
                            Assert.True(reader1.GetInt32(0) == reader2.GetInt32(0),
                                        "MARSSyncBusyReaderTest, Failure #4" + "\n" +
                                        "reader1.GetInt32(0): " + reader1.GetInt32(0) + "\n" +
                                        "reader2.GetInt32(0): " + reader2.GetInt32(0));
                        }

                        Assert.False(reader1.Read() || reader2.Read(), "MARSSyncBusyReaderTest, Failure #5");
                    }
                }
            }
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
                    Assert.True(reader1.GetInt32(0) == reader2.GetInt32(0) &&
                                reader2.GetInt32(0) == reader3.GetInt32(0) &&
                                reader3.GetInt32(0) == reader4.GetInt32(0) &&
                                reader4.GetInt32(0) == reader5.GetInt32(0),
                                "MARSSyncExecuteReaderTest3, Failure #2" + "\n" +
                                "reader1.GetInt32(0): " + reader1.GetInt32(0) + "\n" +
                                "reader2.GetInt32(0): " + reader2.GetInt32(0) + "\n" +
                                "reader3.GetInt32(0): " + reader3.GetInt32(0) + "\n" +
                                "reader4.GetInt32(0): " + reader4.GetInt32(0) + "\n" +
                                "reader5.GetInt32(0): " + reader5.GetInt32(0));

                    Assert.False(reader1.Read() || reader2.Read() || reader3.Read() || reader4.Read() || reader5.Read(), "MARSSyncExecuteReaderTest3 Failure #3");
                }
            }
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

                    Assert.True(reader1.GetInt32(0) == 1 &&
                                reader2.GetInt32(0) == 2 &&
                                reader3.GetInt32(0) == 3,
                                "MARSSyncExecuteReaderTest4 failure #2");

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
    }
}
