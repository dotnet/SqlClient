// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlCommandCancelTest
    {
        // Shrink the packet size - this should make timeouts more likely
        private static readonly string tcp_connStr = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { PacketSize = 512 }).ConnectionString;
        private static readonly string np_connStr = (new SqlConnectionStringBuilder(DataTestUtility.NPConnectionString) { PacketSize = 512 }).ConnectionString;

        // Synapse: Remove dependency on Northwind database + WAITFOR not supported + ';' not supported
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void PlainCancelTest()
        {
            PlainCancel(tcp_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void PlainCancelTestNP()
        {
            PlainCancel(np_connStr);
        }
        
        // Synapse: Remove dependency on Northwind database + WAITFOR not supported + ';' not supported
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void PlainMARSCancelTest()
        {
            PlainCancel((new SqlConnectionStringBuilder(tcp_connStr) { MultipleActiveResultSets = true }).ConnectionString);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void PlainMARSCancelTestNP()
        {
            PlainCancel((new SqlConnectionStringBuilder(np_connStr) { MultipleActiveResultSets = true }).ConnectionString);
        }

        // Synapse: Remove dependency on Northwind database + WAITFOR not supported + ';' not supported
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void PlainCancelTestAsync()
        {
            PlainCancelAsync(tcp_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void PlainCancelTestAsyncNP()
        {
            PlainCancelAsync(np_connStr);
        }

        // Synapse: Remove dependency from Northwind database + WAITFOR not supported + ';' not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void PlainMARSCancelTestAsync()
        {
            PlainCancelAsync((new SqlConnectionStringBuilder(tcp_connStr) { MultipleActiveResultSets = true }).ConnectionString);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void PlainMARSCancelTestAsyncNP()
        {
            PlainCancelAsync((new SqlConnectionStringBuilder(np_connStr) { MultipleActiveResultSets = true }).ConnectionString);
        }

        private static void PlainCancel(string connString)
        {
            using (SqlConnection conn = new SqlConnection(connString))
            using (SqlCommand cmd = new SqlCommand("select * from dbo.Orders; waitfor delay '00:00:10'; select * from dbo.Orders", conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    cmd.Cancel();
                    DataTestUtility.AssertThrowsWrapper<SqlException>(
                        () =>
                        {
                            do
                            {
                                while (reader.Read())
                                {
                                }
                            }
                            while (reader.NextResult());
                        },
                        "A severe error occurred on the current command.  The results, if any, should be discarded.");
                }
            }
        }

        private static void PlainCancelAsync(string connString)
        {
            using (SqlConnection conn = new SqlConnection(connString))
            using (SqlCommand cmd = new SqlCommand("select * from dbo.Orders; waitfor delay '00:00:10'; select * from dbo.Orders", conn))
            {
                conn.Open();
                Task<SqlDataReader> readerTask = cmd.ExecuteReaderAsync();
                DataTestUtility.AssertThrowsWrapper<SqlException>(
                    () =>
                    {
                        readerTask.Wait(2000);
                        SqlDataReader reader = readerTask.Result;
                        cmd.Cancel();
                        do
                        {
                            while (reader.Read())
                            {
                            }
                        }
                        while (reader.NextResult());
                    },
                    "A severe error occurred on the current command.  The results, if any, should be discarded.");
            }
        }

        // Synapse: Remove dependency from Northwind database + WAITFOR not supported + ';' not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public static void MultiThreadedCancel_NonAsync()
        {
            MultiThreadedCancel(tcp_connStr, false);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void MultiThreadedCancel_NonAsyncNP()
        {
            MultiThreadedCancel(np_connStr, false);
        }

        // Synapse: Remove dependency from Northwind database + WAITFOR not supported + ';' not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void MultiThreadedCancel_Async()
        {
            MultiThreadedCancel(tcp_connStr, true);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void MultiThreadedCancel_AsyncNP()
        {
            MultiThreadedCancel(np_connStr, true);
        }

        // Synapse: WAITFOR not supported + ';' not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public static void TimeoutCancel()
        {
            TimeoutCancel(tcp_connStr);
        }

        [ActiveIssue(12167)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void TimeoutCancelNP()
        {
            TimeoutCancel(np_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void CancelAndDisposePreparedCommand()
        {
            CancelAndDisposePreparedCommand(tcp_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void CancelAndDisposePreparedCommandNP()
        {
            CancelAndDisposePreparedCommand(np_connStr);
        }

        [ActiveIssue(5541)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TimeOutDuringRead()
        {
            TimeOutDuringRead(tcp_connStr);
        }

        [ActiveIssue(5541)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void TimeOutDuringReadNP()
        {
            TimeOutDuringRead(np_connStr);
        }

        // Synapse: WAITFOR not supported + ';' not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CancelDoesNotWait()
        {
            CancelDoesNotWait(tcp_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void CancelDoesNotWaitNP()
        {
            CancelDoesNotWait(np_connStr);
        }

        // Synapse: WAITFOR not supported + ';' not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void AsyncCancelDoesNotWait()
        {
            AsyncCancelDoesNotWait(tcp_connStr).Wait();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void AsyncCancelDoesNotWaitNP()
        {
            AsyncCancelDoesNotWait(np_connStr).Wait();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TCPAttentionPacketTestTransaction()
        {
            CancelFollowedByTransaction(tcp_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void NPAttentionPacketTestTransaction()
        {
            CancelFollowedByTransaction(np_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public static void TCPAttentionPacketTestAlerts()
        {
            CancelFollowedByAlert(tcp_connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void NPAttentionPacketTestAlerts()
        {
            CancelFollowedByAlert(np_connStr);
        }

        private static void CancelFollowedByTransaction(string constr)
        {
            using (SqlConnection connection = new SqlConnection(constr))
            {
                connection.Open();
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT @@VERSION";
                    using (var r = cmd.ExecuteReader())
                    {
                        cmd.Cancel();
                    }
                }
                using (SqlTransaction transaction = connection.BeginTransaction())
                { }
            }
        }

        private static void CancelFollowedByAlert(string constr)
        {
            var alertName = "myAlert" + Guid.NewGuid().ToString();
            // Since Alert conditions are randomly generated, 
            // we will retry on unexpected error messages to avoid collision in pipelines.
            var n = new Random().Next(1, 100);
            bool retry = true;
            int retryAttempt = 0;
            while (retry && retryAttempt < 3)
            {
                try
                {
                    using (var conn = new SqlConnection(constr))
                    {
                        conn.Open();
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT @@VERSION";
                            using (var reader = cmd.ExecuteReader())
                            {
                                cmd.Cancel(); // Sends Attention
                            }
                        }
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $@"EXEC msdb.dbo.sp_add_alert @name=N'{alertName}',
                                        @performance_condition = N'SQLServer:General Statistics|User Connections||>|{n}'";
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = @"USE [msdb]";
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = $@"/****** Object:  Alert [{alertName}] Script Date: {DateTime.Now} ******/
                IF  EXISTS (SELECT name FROM msdb.dbo.sysalerts WHERE name = N'{alertName}')
                EXEC msdb.dbo.sp_delete_alert @name=N'{alertName}'";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (retryAttempt >= 3 || e.Message.Contains("The transaction operation cannot be performed"))
                    {
                        Assert.False(true, $"Retry Attempt: {retryAttempt} | Unexpected Exception occurred: {e.Message}");
                    }
                    else
                    {
                        retry = true;
                        retryAttempt++;
                        Console.WriteLine($"CancelFollowedByAlert Test retry attempt : {retryAttempt}");
                        Thread.Sleep(500);
                        continue;
                    }
                }
                retry = false;
            }
        }

        private static void MultiThreadedCancel(string constr, bool async)
        {
            using (SqlConnection con = new SqlConnection(constr))
            {
                con.Open();
                using (var command = con.CreateCommand())
                {
                    command.CommandText = "select * from orders; waitfor delay '00:00:08'; select * from customers";

                    Barrier threadsReady = new Barrier(2);
                    object state = new Tuple<bool, SqlCommand, Barrier>(async, command, threadsReady);

                    Task[] tasks = new Task[2];
                    tasks[0] = new Task(ExecuteCommandCancelExpected, state);
                    tasks[1] = new Task(CancelSharedCommand, state);
                    tasks[0].Start();
                    tasks[1].Start();

                    Task.WaitAll(tasks, 15 * 1000);

                    SqlCommandCancelTest.VerifyConnection(command);
                }
            }
        }

        private static void TimeoutCancel(string constr)
        {
            using (SqlConnection con = new SqlConnection(constr))
            {
                con.Open();
                using (SqlCommand cmd = con.CreateCommand())
                {
                    cmd.CommandTimeout = 1;
                    cmd.CommandText = "WAITFOR DELAY '00:00:20';select * from Customers";

                    string errorMessage = SystemDataResourceManager.Instance.SQL_Timeout_Execution;
                    DataTestUtility.ExpectFailure<SqlException>(() => ExecuteReaderOnCmd(cmd), new string[] { errorMessage });

                    VerifyConnection(cmd);
                }
            }
        }

        private static void ExecuteReaderOnCmd(SqlCommand cmd)
        {
            using (SqlDataReader reader = cmd.ExecuteReader())
            { }
        }

        //InvalidOperationException from connection.Dispose if that connection has prepared command cancelled during reading of data
        private static void CancelAndDisposePreparedCommand(string constr)
        {
            int expectedValue = 1;
            using (var connection = new SqlConnection(constr))
            {
                try
                {
                    // Generate a query with a large number of results.
                    using (var command = new SqlCommand("select @P from sysobjects a cross join sysobjects b cross join sysobjects c cross join sysobjects d cross join sysobjects e cross join sysobjects f", connection))
                    {
                        command.Parameters.Add(new SqlParameter("@P", SqlDbType.Int) { Value = expectedValue });
                        connection.Open();
                        // Prepare the query.
                        // Currently this does nothing until command.ExecuteReader is called.
                        // Ideally this should call sp_prepare up-front.
                        command.Prepare();
                        using (var reader = command.ExecuteReader(CommandBehavior.SingleResult))
                        {
                            if (reader.Read())
                            {
                                int actualValue = reader.GetInt32(0);
                                Assert.True(actualValue == expectedValue, string.Format("Got incorrect value. Expected: {0}, Actual: {1}", expectedValue, actualValue));
                            }
                            // Abandon reading the results.
                            command.Cancel();
                        }
                    }
                }
                finally
                {
                    connection.Dispose(); // before the fix, InvalidOperationException happened here
                }
            }
        }

        private static void VerifyConnection(SqlCommand cmd)
        {
            Assert.True(cmd.Connection.State == ConnectionState.Open, "FAILURE: - unexpected non-open state after Execute!");

            cmd.CommandText = "select 'ABC'"; // Verify Connection
            string value = (string)cmd.ExecuteScalar();
            Assert.True(value == "ABC", "FAILURE: upon validation execute on connection: '" + value + "'");
        }

        private static void ExecuteCommandCancelExpected(object state)
        {
            var stateTuple = (Tuple<bool, SqlCommand, Barrier>)state;
            bool async = stateTuple.Item1;
            SqlCommand command = stateTuple.Item2;
            Barrier threadsReady = stateTuple.Item3;

            string errorMessage = SystemDataResourceManager.Instance.SQL_OperationCancelled;
            string errorMessageSevereFailure = SystemDataResourceManager.Instance.SQL_SevereError;

            DataTestUtility.ExpectFailure<SqlException>(() =>
            {
                threadsReady.SignalAndWait();
                using (SqlDataReader r = command.ExecuteReader())
                {
                    do
                    {
                        while (r.Read())
                        {
                        }
                    } while (r.NextResult());
                }
            }, new string[] { errorMessage, errorMessageSevereFailure });

        }

        private static void CancelSharedCommand(object state)
        {
            var stateTuple = (Tuple<bool, SqlCommand, Barrier>)state;

            // sleep 1 seconds before cancel to ensure ExecuteReader starts and ensure it does not end before Cancel is called (command is running WAITFOR 8 seconds)
            stateTuple.Item3.SignalAndWait();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            stateTuple.Item2.Cancel();
        }

        private static void TimeOutDuringRead(string constr)
        {
            // Create the proxy
            ProxyServer proxy = ProxyServer.CreateAndStartProxy(constr, out constr);
            proxy.SimulatedPacketDelay = 100;
            proxy.SimulatedOutDelay = true;

            try
            {
                using (SqlConnection conn = new SqlConnection(constr))
                {
                    // Start the command
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT @p", conn))
                    {
                        cmd.Parameters.AddWithValue("p", new byte[20000]);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            reader.Read();

                            // Tweak the timeout to 1ms, stop the proxy from proxying and then try GetValue (which should timeout)
                            reader.SetDefaultTimeout(1);
                            proxy.PauseCopying();
                            string errorMessage = SystemDataResourceManager.Instance.SQL_Timeout_Execution;
                            Exception exception = Assert.Throws<SqlException>(() => reader.GetValue(0));
                            Assert.Contains(errorMessage, exception.Message);

                            // Return everything to normal and close
                            proxy.ResumeCopying();
                            reader.SetDefaultTimeout(30000);
                            reader.Dispose();
                        }
                    }
                }
            }
            catch
            {
                // In case of error, stop the proxy and dump its logs (hopefully this will help with debugging
                proxy.Stop();
                Console.WriteLine(proxy.GetServerEventLog());
                throw;
            }
            finally
            {
                proxy.Stop();
            }
        }

        private static void CancelDoesNotWait(string connStr)
        {
            const int delaySeconds = 30;
            const int cancelSeconds = 1;

            using (SqlConnection conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand($"WAITFOR DELAY '00:00:{delaySeconds:D2}'", conn))
            {
                conn.Open();

                // Cancel after 2 seconds as sometimes total time elapsed can be .99 in case of 1 second that causes random failures
                Task.Delay(TimeSpan.FromSeconds(cancelSeconds + 1))
                    .ContinueWith(t => cmd.Cancel());

                DateTime started = DateTime.UtcNow;
                DateTime ended = DateTime.UtcNow;
                Exception exception = null;
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                ended = DateTime.UtcNow;

                Assert.NotNull(exception);
                Assert.InRange((ended - started).TotalSeconds, cancelSeconds, delaySeconds - 1);
            }
        }

        private static async Task AsyncCancelDoesNotWait(string connStr)
        {
            const int delaySeconds = 30;
            const int cancelSeconds = 1;

            using (SqlConnection conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand($"WAITFOR DELAY '00:00:{delaySeconds:D2}'", conn))
            {
                await conn.OpenAsync();

                DateTime started = DateTime.UtcNow;
                Exception exception = null;
                try
                {
                    // Cancel after 2 seconds as sometimes total time elapsed can be .99 in case of 1 second that causes random failures
                    await cmd.ExecuteNonQueryAsync(new CancellationTokenSource(2000).Token);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                DateTime ended = DateTime.UtcNow;

                Assert.NotNull(exception);
                Assert.InRange((ended - started).TotalSeconds, cancelSeconds, delaySeconds - 1);
            }
        }
    }
}
