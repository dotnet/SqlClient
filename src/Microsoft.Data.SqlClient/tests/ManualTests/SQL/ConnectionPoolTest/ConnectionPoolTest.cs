// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class ConnectionPoolConnectionStringProvider : IEnumerable<object[]>
    {
        private static readonly string _TCPConnectionString = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = false, Pooling = true }).ConnectionString;
        private static readonly string _tcpMarsConnStr = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true, Pooling = true }).ConnectionString;

        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { _TCPConnectionString };
            if (DataTestUtility.IsNotAzureSynapse())
            {
                yield return new object[] { _tcpMarsConnStr };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // TODO Synapse: Fix these tests for Azure Synapse.
    public static class ConnectionPoolTest
    {
        /// <summary>
        /// Tests that using the same connection string results in the same pool\internal connection and a different string results in a different pool\internal connection
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void BasicConnectionPoolingTest(string connectionString)
        {
            InternalConnectionWrapper internalConnection;
            ConnectionPoolWrapper connectionPool;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                internalConnection = new InternalConnectionWrapper(connection);
                connectionPool = new ConnectionPoolWrapper(connection);
                connection.Close();
            }

            using (SqlConnection connection2 = new SqlConnection(connectionString))
            {
                connection2.Open();
                Assert.True(internalConnection.IsInternalConnectionOf(connection2), "New connection does not use same internal connection");
                Assert.True(connectionPool.ContainsConnection(connection2), "New connection is in a different pool");
                connection2.Close();
            }

            using (SqlConnection connection3 = new SqlConnection(connectionString + ";App=SqlConnectionPoolUnitTest;"))
            {
                connection3.Open();
                Assert.False(internalConnection.IsInternalConnectionOf(connection3), "Connection with different connection string uses same internal connection");
                Assert.False(connectionPool.ContainsConnection(connection3), "Connection with different connection string uses same connection pool");
                connection3.Close();
            }

            connectionPool.Cleanup();

            using (SqlConnection connection4 = new SqlConnection(connectionString))
            {
                connection4.Open();
                Assert.True(internalConnection.IsInternalConnectionOf(connection4), "New connection does not use same internal connection");
                Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in a different pool");
                connection4.Close();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAADPasswordConnStrSetup), nameof(DataTestUtility.IsAADAuthorityURLSetup))]
        public static void AccessTokenConnectionPoolingTest()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD", "Authentication" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            SqlConnection connection = new SqlConnection(connectionString);
            connection.AccessToken = DataTestUtility.GetAccessToken();
            connection.Open();
            InternalConnectionWrapper internalConnection = new InternalConnectionWrapper(connection);
            ConnectionPoolWrapper connectionPool = new ConnectionPoolWrapper(connection);
            connection.Close();

            SqlConnection connection2 = new SqlConnection(connectionString);
            connection2.AccessToken = DataTestUtility.GetAccessToken();
            connection2.Open();
            Assert.True(internalConnection.IsInternalConnectionOf(connection2), "New connection does not use same internal connection");
            Assert.True(connectionPool.ContainsConnection(connection2), "New connection is in a different pool");
            connection2.Close();

            SqlConnection connection3 = new SqlConnection(connectionString + ";App=SqlConnectionPoolUnitTest;");
            connection3.AccessToken = DataTestUtility.GetAccessToken();
            connection3.Open();
            Assert.False(internalConnection.IsInternalConnectionOf(connection3), "Connection with different connection string uses same internal connection");
            Assert.False(connectionPool.ContainsConnection(connection3), "Connection with different connection string uses same connection pool");
            connection3.Close();

            connectionPool.Cleanup();

            SqlConnection connection4 = new SqlConnection(connectionString);
            connection4.AccessToken = DataTestUtility.GetAccessToken();
            connection4.Open();
            Assert.True(internalConnection.IsInternalConnectionOf(connection4), "New connection does not use same internal connection");
            Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in a different pool");
            connection4.Close();
        }

        /// <summary>
        /// Tests if clearing all of the pools does actually remove the pools
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void ClearAllPoolsTest(string connectionString)
        {
            SqlConnection.ClearAllPools();
            Assert.True(0 == ConnectionPoolWrapper.AllConnectionPools().Length, "Pools exist after clearing all pools");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                ConnectionPoolWrapper pool = new ConnectionPoolWrapper(connection);
                connection.Close();
                ConnectionPoolWrapper[] allPools = ConnectionPoolWrapper.AllConnectionPools();
                DataTestUtility.AssertEqualsWithDescription(1, allPools.Length, "Incorrect number of pools exist.");
                Assert.True(allPools[0].Equals(pool), "Saved pool is not in the list of all pools");
                DataTestUtility.AssertEqualsWithDescription(1, pool.ConnectionCount, "Saved pool has incorrect number of connections");

                SqlConnection.ClearAllPools();
                Assert.True(0 == ConnectionPoolWrapper.AllConnectionPools().Length, "Pools exist after clearing all pools");
                DataTestUtility.AssertEqualsWithDescription(0, pool.ConnectionCount, "Saved pool has incorrect number of connections.");
            }
        }

        /// <summary>
        /// Checks if an 'emancipated' internal connection is reclaimed when a new connection is opened AND we hit max pool size
        /// NOTE: 'emancipated' means that the internal connection's SqlConnection has fallen out of scope and has no references, but was not explicitly disposed\closed
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void ReclaimEmancipatedOnOpenTest(string connectionString)
        {
            string newConnectionString = (new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = 1 }).ConnectionString;
            SqlConnection.ClearAllPools();

            InternalConnectionWrapper internalConnection = CreateEmancipatedConnection(newConnectionString);
            ConnectionPoolWrapper connectionPool = internalConnection.ConnectionPool;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            DataTestUtility.AssertEqualsWithDescription(1, connectionPool.ConnectionCount, "Wrong number of connections in the pool.");
            DataTestUtility.AssertEqualsWithDescription(0, connectionPool.FreeConnectionCount, "Wrong number of free connections in the pool.");

            using (SqlConnection connection = new SqlConnection(newConnectionString))
            {
                connection.Open();
                Assert.True(internalConnection.IsInternalConnectionOf(connection), "Connection has wrong internal connection");
                Assert.True(connectionPool.ContainsConnection(connection), "Connection is in wrong connection pool");
            }
        }

        /// <summary>
        /// Tests if, when max pool size is reached, Open() will block until a connection becomes available
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void MaxPoolWaitForConnectionTest(string connectionString)
        {
            string newConnectionString = (new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = 1 }).ConnectionString;
            SqlConnection.ClearAllPools();

            using SqlConnection connection1 = new SqlConnection(newConnectionString);
            connection1.Open();

            InternalConnectionWrapper internalConnection = new InternalConnectionWrapper(connection1);
            ConnectionPoolWrapper connectionPool = new ConnectionPoolWrapper(connection1);
            ManualResetEventSlim taskAllowedToSpeak = new ManualResetEventSlim(false);

            Task waitTask = Task.Factory.StartNew(() => MaxPoolWaitForConnectionTask(newConnectionString, internalConnection, connectionPool, taskAllowedToSpeak));
            Thread.Sleep(200);
            Assert.Equal(TaskStatus.Running, waitTask.Status);

            connection1.Close();
            taskAllowedToSpeak.Set();
            waitTask.Wait();
            Assert.Equal(TaskStatus.RanToCompletion, waitTask.Status);
        }

#if DEBUG

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUsingManagedSNI))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void ReplacementConnectionUsesSemaphoreTest(string connectionString)
        {
            string newConnectionString = (new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = 2, ConnectTimeout = 5 }).ConnectionString;
            SqlConnection.ClearAllPools();

            SqlConnection liveConnection = new SqlConnection(newConnectionString);
            SqlConnection deadConnection = new SqlConnection(newConnectionString);
            liveConnection.Open();
            deadConnection.Open();
            InternalConnectionWrapper deadConnectionInternal = new InternalConnectionWrapper(deadConnection);
            InternalConnectionWrapper liveConnectionInternal = new InternalConnectionWrapper(liveConnection);
            deadConnectionInternal.KillConnection();
            deadConnection.Close();
            liveConnection.Close();

            Task<InternalConnectionWrapper>[] tasks = new Task<InternalConnectionWrapper>[3];
            Barrier syncBarrier = new Barrier(tasks.Length);
            Func<InternalConnectionWrapper> taskFunction = (() => ReplacementConnectionUsesSemaphoreTask(newConnectionString, syncBarrier));
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew<InternalConnectionWrapper>(taskFunction);
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
                            taskWithCorrectException = true;
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
                                taskWithLiveConnection = true;
                        }
                        else if (!item.Result.Equals(deadConnectionInternal) && !taskWithNewConnection)
                            taskWithNewConnection = true;
                    }
                    else
                        Console.WriteLine("ERROR: Task in unknown state: {0}", item.Status);
                }
            });

            waitAllTask.Wait();
            Assert.True(taskWithLiveConnection && taskWithNewConnection && taskWithCorrectException, string.Format("Tasks didn't finish as expected.\nTask with live connection: {0}\nTask with new connection: {1}\nTask with correct exception: {2}\n", taskWithLiveConnection, taskWithNewConnection, taskWithCorrectException));
        }

        /// <summary>
        /// Tests if killing the connection using the InternalConnectionWrapper is working
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUsingManagedSNI))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void KillConnectionTest(string connectionString)
        {
            InternalConnectionWrapper wrapper = null;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                wrapper = new InternalConnectionWrapper(connection);

                using (SqlCommand command = new SqlCommand("SELECT 5;", connection))
                {
                    DataTestUtility.AssertEqualsWithDescription(5, command.ExecuteScalar(), "Incorrect scalar result.");
                }

                wrapper.KillConnection();
            }

            using (SqlConnection connection2 = new SqlConnection(connectionString))
            {
                connection2.Open();
                Assert.False(wrapper.IsInternalConnectionOf(connection2), "New connection has internal connection that was just killed");
                using (SqlCommand command = new SqlCommand("SELECT 5;", connection2))
                {
                    DataTestUtility.AssertEqualsWithDescription(5, command.ExecuteScalar(), "Incorrect scalar result.");
                }
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

            using SqlConnection conn1 = new SqlConnection(connectionString);
            using SqlConnection conn2 = new SqlConnection(connectionString);
            conn1.Open();
            conn2.Open();
            ConnectionPoolWrapper connectionPool = new ConnectionPoolWrapper(conn1);
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

            using SqlConnection conn3 = new SqlConnection(connectionString);
            conn3.Open();
            InternalConnectionWrapper internalConnection3 = new InternalConnectionWrapper(conn3);

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
                SqlConnection deadConnection = new SqlConnection(newConnectionString);
                deadConnection.Open();
                InternalConnectionWrapper deadConnectionInternal = new InternalConnectionWrapper(deadConnection);
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
#endif

#if NETFRAMEWORK

        /// <summary>
        /// Tests if connections in a distributed transaction are put into a transaction pool. Also checks that clearallpools 
        /// does not clear transaction connections and that the transaction root is put into "stasis" when closed
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void TransactionPoolTest(string connectionString)
        {
            ConnectionPoolWrapper connectionPool = null;

            using (TransactionScope transScope = new TransactionScope())
            {
                using SqlConnection connection1 = new SqlConnection(connectionString);
                using SqlConnection connection2 = new SqlConnection(connectionString);
                connection1.Open();
                connection2.Open();
                connectionPool = new ConnectionPoolWrapper(connection1);

                InternalConnectionWrapper internalConnection1 = new InternalConnectionWrapper(connection1);
                InternalConnectionWrapper internalConnection2 = new InternalConnectionWrapper(connection2);

                Assert.True(internalConnection1.IsEnlistedInTransaction, "First connection not in transaction");
                Assert.True(internalConnection1.IsTransactionRoot, "First connection not transaction root");
                Assert.True(internalConnection2.IsEnlistedInTransaction, "Second connection not in transaction");
                Assert.False(internalConnection2.IsTransactionRoot, "Second connection is transaction root");

                // Attempt to re-use root connection
                connection1.Close();
                using SqlConnection connection3 = new SqlConnection(connectionString);
                connection3.Open();

                Assert.True(connectionPool.ContainsConnection(connection3), "New connection in wrong pool");
                Assert.True(internalConnection1.IsInternalConnectionOf(connection3), "Root connection was not re-used");

                // Attempt to re-use non-root connection
                connection2.Close();
                using SqlConnection connection4 = new SqlConnection(connectionString);
                connection4.Open();
                Assert.True(internalConnection2.IsInternalConnectionOf(connection4), "Connection did not re-use expected internal connection");
                Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in the wrong pool");
                connection4.Close();

                // Use a different connection string
                using SqlConnection connection5 = new SqlConnection(connectionString + ";App=SqlConnectionPoolUnitTest;");
                connection5.Open();
                Assert.False(internalConnection2.IsInternalConnectionOf(connection5), "Connection with different connection string re-used internal connection");
                Assert.False(connectionPool.ContainsConnection(connection5), "Connection with different connection string is in same pool");
                connection5.Close();

                transScope.Complete();
            }

            Assert.Equal(2, connectionPool.ConnectionCount);
        }

        /// <summary>
        /// Checks that connections in the transaction pool are not cleaned out, and the root transaction is put into "stasis" when it ages
        /// </summary>
        /// <param name="connectionString"></param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void TransactionCleanupTest(string connectionString)
        {
            SqlConnection.ClearAllPools();
            ConnectionPoolWrapper connectionPool = null;

            using (TransactionScope transScope = new TransactionScope())
            {
                using SqlConnection connection1 = new SqlConnection(connectionString);
                using SqlConnection connection2 = new SqlConnection(connectionString);
                connection1.Open();
                connection2.Open();
                InternalConnectionWrapper internalConnection1 = new InternalConnectionWrapper(connection1);
                connectionPool = new ConnectionPoolWrapper(connection1);

                connectionPool.Cleanup();
                Assert.Equal(2, connectionPool.ConnectionCount);

                connection1.Close();
                connection2.Close();
                connectionPool.Cleanup();
                Assert.Equal(2, connectionPool.ConnectionCount);

                connectionPool.Cleanup();
                Assert.Equal(2, connectionPool.ConnectionCount);

                transScope.Complete();
            }
        }

#endif

        private static InternalConnectionWrapper ReplacementConnectionUsesSemaphoreTask(string connectionString, Barrier syncBarrier)
        {
            InternalConnectionWrapper internalConnection = null;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    internalConnection = new InternalConnectionWrapper(connection);
                }
                catch
                {
                    syncBarrier.SignalAndWait();
                    throw;
                }

                syncBarrier.SignalAndWait();
            }

            return internalConnection;
        }

        private static InternalConnectionWrapper CreateEmancipatedConnection(string connectionString)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return new InternalConnectionWrapper(connection);
        }

        private static void MaxPoolWaitForConnectionTask(string connectionString, InternalConnectionWrapper internalConnection, ConnectionPoolWrapper connectionPool, ManualResetEventSlim waitToSpeak)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            waitToSpeak.Wait();
            Assert.True(internalConnection.IsInternalConnectionOf(connection), "Connection has wrong internal connection");
            Assert.True(connectionPool.ContainsConnection(connection), "Connection is in wrong connection pool");
            connection.Close();
        }

        private static SqlConnection ReplacementConnectionObeys0TimeoutTask(string connectionString)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
