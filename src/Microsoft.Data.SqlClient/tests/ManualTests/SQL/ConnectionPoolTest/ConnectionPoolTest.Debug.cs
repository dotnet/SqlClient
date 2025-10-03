using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Data.SqlClient.ManualTesting.Tests.ConnectionPoolTest;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ConnectionPoolTestDebug
    {
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUsingManagedSNI))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void ReplacementConnectionUsesSemaphoreTest(string connectionString)
        {
            string newConnectionString = (new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = 2, ConnectTimeout = 5 }).ConnectionString;
            SqlConnection.ClearAllPools();

            using SqlConnection liveConnection = new(newConnectionString);
            using SqlConnection deadConnection = new(newConnectionString);
            liveConnection.Open();
            deadConnection.Open();
            InternalConnectionWrapper deadConnectionInternal = new(deadConnection);
            InternalConnectionWrapper liveConnectionInternal = new(liveConnection);
            deadConnectionInternal.KillConnection();
            deadConnection.Close();
            liveConnection.Close();

            Task<InternalConnectionWrapper>[] tasks = new Task<InternalConnectionWrapper>[3];
            Barrier syncBarrier = new(tasks.Length);
            Func<InternalConnectionWrapper> taskFunction = (() => ReplacementConnectionUsesSemaphoreTask(newConnectionString, syncBarrier));
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(taskFunction);
            }

            bool taskWithLiveConnection = false;
            bool taskWithNewConnection = false;
            bool taskWithCorrectException = false;

            Task waitAllTask = Task.Factory.ContinueWhenAll(tasks, (completedTasks) =>
            {
                foreach (var item in completedTasks)
                {
                    if (item.Status == TaskStatus.Faulted)
                    {
                        // One task should have a timeout exception
                        if ((!taskWithCorrectException) && (item.Exception.InnerException is InvalidOperationException) && (item.Exception.InnerException.Message.StartsWith(SystemDataResourceManager.Instance.ADP_PooledOpenTimeout)))
                        {
                            taskWithCorrectException = true;
                        }
                        else if (!taskWithCorrectException)
                        {
                            // Rethrow the unknown exception
                            ExceptionDispatchInfo exceptionInfo = ExceptionDispatchInfo.Capture(item.Exception);
                            exceptionInfo.Throw();
                        }
                    }
                    else if (item.Status == TaskStatus.RanToCompletion)
                    {
                        // One task should get the live connection
                        if (item.Result.Equals(liveConnectionInternal))
                        {
                            if (!taskWithLiveConnection)
                            {
                                taskWithLiveConnection = true;
                            }
                        }
                        else if (!item.Result.Equals(deadConnectionInternal) && !taskWithNewConnection)
                        {
                            taskWithNewConnection = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Task in unknown state: {0}", item.Status);
                    }
                }
            });

            waitAllTask.Wait();
            Assert.True(taskWithLiveConnection && taskWithNewConnection && taskWithCorrectException,
                $"Tasks didn't finish as expected.\n" +
                $"Task with live connection: {taskWithLiveConnection}\n" +
                $"Task with new connection: {taskWithNewConnection}\n" +
                $"Task with correct exception: {taskWithCorrectException}\n");
        }

        /// <summary>
        /// Tests if killing the connection using the InternalConnectionWrapper is working
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUsingManagedSNI))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void KillConnectionTest(string connectionString)
        {
            InternalConnectionWrapper wrapper = null;

            using (SqlConnection connection = new(connectionString))
            {
                connection.Open();
                wrapper = new InternalConnectionWrapper(connection);

                using SqlCommand command = new("SELECT 5;", connection);

                DataTestUtility.AssertEqualsWithDescription(5, command.ExecuteScalar(), "Incorrect scalar result.");

                wrapper.KillConnection();
            }

            using (SqlConnection connection2 = new(connectionString))
            {
                connection2.Open();
                Assert.False(wrapper.IsInternalConnectionOf(connection2), "New connection has internal connection that was just killed");
                using SqlCommand command = new("SELECT 5;", connection2);

                DataTestUtility.AssertEqualsWithDescription(5, command.ExecuteScalar(), "Incorrect scalar result.");
            }
        }

        /// <summary>
        /// Tests that cleanup removes connections that are unused for two cleanups
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUsingManagedSNI))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void CleanupTest(string connectionString)
        {
            SqlConnection.ClearAllPools();

            using SqlConnection conn1 = new(connectionString);
            using SqlConnection conn2 = new(connectionString);
            conn1.Open();
            conn2.Open();
            ConnectionPoolWrapper connectionPool = new(conn1);
            Assert.Equal(2, connectionPool.ConnectionCount);

            connectionPool.Cleanup();
            Assert.Equal(2, connectionPool.ConnectionCount);

            conn1.Close();
            connectionPool.Cleanup();
            Assert.Equal(2, connectionPool.ConnectionCount);

            conn2.Close();
            connectionPool.Cleanup();
            Assert.Equal(1, connectionPool.ConnectionCount);

            connectionPool.Cleanup();
            Assert.Equal(0, connectionPool.ConnectionCount);

            using SqlConnection conn3 = new(connectionString);
            conn3.Open();
            InternalConnectionWrapper internalConnection3 = new(conn3);

            conn3.Close();
            internalConnection3.KillConnection();
            Assert.Equal(1, connectionPool.ConnectionCount);
            Assert.False(internalConnection3.IsConnectionAlive(), "Connection should not be alive");

            connectionPool.Cleanup();
            Assert.Equal(1, connectionPool.ConnectionCount);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUsingManagedSNI))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void ReplacementConnectionObeys0TimeoutTest(string connectionString)
        {
            string newConnectionString = (new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 0 }).ConnectionString;
            SqlConnection.ClearAllPools();

            // Kick off proxy
            using (ProxyServer proxy = ProxyServer.CreateAndStartProxy(newConnectionString, out newConnectionString))
            {
                // Create one dead connection
                using SqlConnection deadConnection = new(newConnectionString);
                deadConnection.Open();
                InternalConnectionWrapper deadConnectionInternal = new(deadConnection);
                deadConnectionInternal.KillConnection();

                // Block one live connection
                proxy.PauseCopying();
                Task<SqlConnection> blockedConnectionTask = Task.Run(() => ReplacementConnectionObeys0TimeoutTask(newConnectionString));
                Thread.Sleep(100);
                Assert.Equal(TaskStatus.Running, blockedConnectionTask.Status);

                // Close and re-open the dead connection
                deadConnection.Close();
                Task<SqlConnection> newConnectionTask = Task.Run(() => ReplacementConnectionObeys0TimeoutTask(newConnectionString));
                Thread.Sleep(100);
                Assert.Equal(TaskStatus.Running, blockedConnectionTask.Status);
                Assert.Equal(TaskStatus.Running, newConnectionTask.Status);

                // restart the proxy
                proxy.ResumeCopying();

                Task.WaitAll(blockedConnectionTask, newConnectionTask);
                blockedConnectionTask.Result.Close();
                newConnectionTask.Result.Close();
            }
        }
    }
}
