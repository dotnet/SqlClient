// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
            SqlConnection.ClearAllPools();

            InternalConnectionWrapper internalConnection;
            ConnectionPoolWrapper connectionPool;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                internalConnection = new InternalConnectionWrapper(connection);
                connectionPool = new ConnectionPoolWrapper(connection);
            }

            using (SqlConnection connection2 = new SqlConnection(connectionString))
            {
                connection2.Open();
                Assert.True(internalConnection.IsInternalConnectionOf(connection2), "New connection does not use same internal connection");
                Assert.True(connectionPool.ContainsConnection(connection2), "New connection is in a different pool");
            }

            using (SqlConnection connection3 = new SqlConnection(connectionString + ";App=SqlConnectionPoolUnitTest;"))
            {
                connection3.Open();
                Assert.False(internalConnection.IsInternalConnectionOf(connection3), "Connection with different connection string uses same internal connection");
                Assert.False(connectionPool.ContainsConnection(connection3), "Connection with different connection string uses same connection pool");
            }

            connectionPool.Cleanup();

            using (SqlConnection connection4 = new SqlConnection(connectionString))
            {
                connection4.Open();
                Assert.True(internalConnection.IsInternalConnectionOf(connection4), "New connection does not use same internal connection");
                Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in a different pool");
            }
        }

        private enum Tristate : byte
        {
            NotInitialized = 0,
            False = 1,
            True = 2
        }

        /// <summary>
        /// Tests that when UseConnectionPoolV2 context switch is enabled, opening a connection throws NotImplementedException
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void UseConnectionPoolV2ThrowsNotImplemented(string connectionString)
        {
            try
            {
                Type switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
                FieldInfo switchField = switchesType.GetField("s_useConnectionPoolV2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                switchField.SetValue(null, Tristate.True);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // This should throw NotImplementedException
                    Assert.Throws<NotImplementedException>(() => connection.Open());
                }
            }
            finally
            {
                // Reset the context switch
                Type switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
                FieldInfo switchField = switchesType.GetField("s_useConnectionPoolV2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                switchField.SetValue(null, Tristate.False);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAADPasswordConnStrSetup), nameof(DataTestUtility.IsAADAuthorityURLSetup))]
        public static void AccessTokenConnectionPoolingTest()
        {
            SqlConnection.ClearAllPools();

            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD", "Authentication" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            using SqlConnection connection = new SqlConnection(connectionString);
            connection.AccessToken = DataTestUtility.GetAccessToken();
            connection.Open();
            InternalConnectionWrapper internalConnection = new InternalConnectionWrapper(connection);
            ConnectionPoolWrapper connectionPool = new ConnectionPoolWrapper(connection);
            connection.Close();

            using SqlConnection connection2 = new SqlConnection(connectionString);
            connection2.AccessToken = DataTestUtility.GetAccessToken();
            connection2.Open();
            Assert.True(internalConnection.IsInternalConnectionOf(connection2), "New connection does not use same internal connection");
            Assert.True(connectionPool.ContainsConnection(connection2), "New connection is in a different pool");
            connection2.Close();

            using SqlConnection connection3 = new SqlConnection(connectionString + ";App=SqlConnectionPoolUnitTest;");
            connection3.AccessToken = DataTestUtility.GetAccessToken();
            connection3.Open();
            Assert.False(internalConnection.IsInternalConnectionOf(connection3), "Connection with different connection string uses same internal connection");
            Assert.False(connectionPool.ContainsConnection(connection3), "Connection with different connection string uses same connection pool");
            connection3.Close();

            connectionPool.Cleanup();

            using SqlConnection connection4 = new SqlConnection(connectionString);
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
            GC.Collect();

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
            int count = 5;
            while (waitTask.Status == TaskStatus.WaitingToRun && count-- > 0)
            {
                Thread.Sleep(200);
            }
            Assert.Equal(TaskStatus.Running, waitTask.Status);

            connection1.Close();
            taskAllowedToSpeak.Set();
            waitTask.Wait();
            Assert.Equal(TaskStatus.RanToCompletion, waitTask.Status);
        }

        

        internal static InternalConnectionWrapper ReplacementConnectionUsesSemaphoreTask(string connectionString, Barrier syncBarrier)
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
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            waitToSpeak.Wait();
            Assert.True(internalConnection.IsInternalConnectionOf(connection), "Connection has wrong internal connection");
            Assert.True(connectionPool.ContainsConnection(connection), "Connection is in wrong connection pool");
        }

        internal static SqlConnection ReplacementConnectionObeys0TimeoutTask(string connectionString)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
