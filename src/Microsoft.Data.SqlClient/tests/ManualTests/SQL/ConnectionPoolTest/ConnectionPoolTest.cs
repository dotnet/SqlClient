// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class ConnectionPoolConnectionStringProvider : IEnumerable<object[]>
    {
        private static readonly string s_tcpConnectionString = BuildConnectionString(mars: false);
        private static readonly string s_tcpMarsConnStr = BuildConnectionString(mars: true);

        private static string BuildConnectionString(bool mars)
        {
            if (string.IsNullOrEmpty(DataTestUtility.TCPConnectionString))
            {
                return string.Empty;
            }

            string connectionString = DataTestUtility.IsAzureSqlConnectionString(DataTestUtility.TCPConnectionString)
                ? DataTestUtility.TCPConnectionString.RemoveAuthAndCredsProperties()
                : DataTestUtility.TCPConnectionString;
            return new SqlConnectionStringBuilder(connectionString)
            {
                MultipleActiveResultSets = mars,
                Pooling = true
            }.ConnectionString;
        }

        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { s_tcpConnectionString };
            if (DataTestUtility.IsNotAzureSynapse())
            {
                yield return new object[] { s_tcpMarsConnStr };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class ConnectionPoolConnectionStringAndPoolVersionProvider : IEnumerable<object[]>
    {
        private static readonly string s_tcpConnectionString = BuildConnectionString(mars: false);
        private static readonly string s_tcpMarsConnStr = BuildConnectionString(mars: true);

        private static string BuildConnectionString(bool mars)
        {
            if (string.IsNullOrEmpty(DataTestUtility.TCPConnectionString))
            {
                return string.Empty;
            }

            string connectionString = DataTestUtility.IsAzureSqlConnectionString(DataTestUtility.TCPConnectionString)
                ? DataTestUtility.TCPConnectionString.RemoveAuthAndCredsProperties()
                : DataTestUtility.TCPConnectionString;
            return new SqlConnectionStringBuilder(connectionString)
            {
                MultipleActiveResultSets = mars,
                Pooling = true
            }.ConnectionString;
        }

        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { s_tcpConnectionString, false };
            yield return new object[] { s_tcpConnectionString, true };
            if (DataTestUtility.IsNotAzureSynapse())
            {
                yield return new object[] { s_tcpMarsConnStr, false };
                yield return new object[] { s_tcpMarsConnStr, true };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // TODO Synapse: Fix these tests for Azure Synapse.
    [Trait("Set", "3")]
    public static class ConnectionPoolTest
    {
        /// <summary>
        /// Supplies both MARS settings and pool versions using the existing connection provider.
        /// </summary>
        public static TheoryData<string, bool> ConnectionPoolAndVersionData
        {
            get
            {
                TheoryData<string, bool> data = new();
                ConnectionPoolConnectionStringAndPoolVersionProvider provider = new();
                foreach (object[] arguments in provider)
                {
                    data.Add((string)arguments[0], (bool)arguments[1]);
                }

                return data;
            }
        }

        /// <summary>
        /// Opens the given connection, setting an access token for Azure SQL targets.
        /// </summary>
        private static async Task OpenWithAccessTokenAsync(SqlConnection connection)
        {
            if (DataTestUtility.IsAzureConnStringSetup())
            {
                connection.AccessToken = await DataTestUtility.GetAccessTokenAsync();
            }
            connection.Open();
        }

        /// <summary>
        /// Tests that using the same connection string results in the same pool\internal connection and a different string results in a different pool\internal connection
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        [Trait("category", "flaky")]
        public static async Task BasicConnectionPoolingTest(string connectionString)
        {
            SqlConnection.ClearAllPools();

            InternalConnectionWrapper internalConnection;
            ConnectionPoolWrapper connectionPool;
            using (SqlConnection connection = new(connectionString))
            {
                await OpenWithAccessTokenAsync(connection);
                internalConnection = new InternalConnectionWrapper(connection);
                connectionPool = new ConnectionPoolWrapper(connection);
            }

            using (SqlConnection connection2 = new(connectionString))
            {
                await OpenWithAccessTokenAsync(connection2);
                Assert.True(internalConnection.IsInternalConnectionOf(connection2), "New connection does not use same internal connection");
                Assert.True(connectionPool.ContainsConnection(connection2), "New connection is in a different pool");
            }

            using (SqlConnection connection3 = new(connectionString + ";App=SqlConnectionPoolUnitTest;"))
            {
                await OpenWithAccessTokenAsync(connection3);
                Assert.False(internalConnection.IsInternalConnectionOf(connection3), "Connection with different connection string uses same internal connection");
                Assert.False(connectionPool.ContainsConnection(connection3), "Connection with different connection string uses same connection pool");
            }

            connectionPool.Cleanup();

            using (SqlConnection connection4 = new(connectionString))
            {
                await OpenWithAccessTokenAsync(connection4);
                Assert.True(internalConnection.IsInternalConnectionOf(connection4), "New connection does not use same internal connection");
                Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in a different pool");
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureConnStringSetup))]
        public static async Task AccessTokenConnectionPoolingTest()
        {
            SqlConnection.ClearAllPools();
            string accessToken = await DataTestUtility.GetAccessTokenAsync();

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString.RemoveAuthAndCredsProperties());
            connection.AccessToken = accessToken;
            connection.Open();
            InternalConnectionWrapper internalConnection = new(connection);
            ConnectionPoolWrapper connectionPool = new(connection);
            connection.Close();

            using SqlConnection connection2 = new(DataTestUtility.TCPConnectionString.RemoveAuthAndCredsProperties());
            connection2.AccessToken = accessToken;
            connection2.Open();
            Assert.True(internalConnection.IsInternalConnectionOf(connection2), "New connection does not use same internal connection");
            Assert.True(connectionPool.ContainsConnection(connection2), "New connection is in a different pool");
            connection2.Close();

            string azureConnStrWithApp = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString.RemoveAuthAndCredsProperties())
                {
                    ApplicationName = "SqlConnectionPoolUnitTest"
                }.ConnectionString;
            using SqlConnection connection3 = new(azureConnStrWithApp);
            connection3.AccessToken = accessToken;
            connection3.Open();
            Assert.False(internalConnection.IsInternalConnectionOf(connection3), "Connection with different connection string uses same internal connection");
            Assert.False(connectionPool.ContainsConnection(connection3), "Connection with different connection string uses same connection pool");
            connection3.Close();

            connectionPool.Cleanup();

            using SqlConnection connection4 = new(DataTestUtility.TCPConnectionString.RemoveAuthAndCredsProperties());
            connection4.AccessToken = accessToken;
            connection4.Open();
            Assert.True(internalConnection.IsInternalConnectionOf(connection4), "New connection does not use same internal connection");
            Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in a different pool");
            connection4.Close();
        }

        /// <summary>
        /// Tests if clearing all of the pools does actually remove the pools
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringAndPoolVersionProvider))]
        public static async Task ClearAllPoolsTest(string connectionString, bool usePoolV2)
        {
            using ConnectionPoolVersionScope poolVersion = new(usePoolV2);

            SqlConnection.ClearAllPools();
            Assert.True(0 == ConnectionPoolWrapper.AllConnectionPools().Length, "Pools exist after clearing all pools");

            using SqlConnection connection = new(connectionString);
            await OpenWithAccessTokenAsync(connection);
            ConnectionPoolWrapper pool = new(connection);
            connection.Close();
            ConnectionPoolWrapper[] allPools = ConnectionPoolWrapper.AllConnectionPools();

            DataTestUtility.AssertEqualsWithDescription(1, allPools.Length, "Incorrect number of pools exist.");
            Assert.True(allPools[0].Equals(pool), "Saved pool is not in the list of all pools");
            DataTestUtility.AssertEqualsWithDescription(1, pool.ConnectionCount, "Saved pool has incorrect number of connections");

            SqlConnection.ClearAllPools();
            Assert.True(0 == ConnectionPoolWrapper.AllConnectionPools().Length, "Pools exist after clearing all pools");
            DataTestUtility.AssertEqualsWithDescription(0, pool.ConnectionCount, "Saved pool has incorrect number of connections.");
        }

        /// <summary>
        /// Checks if an 'emancipated' internal connection is reclaimed when a new connection is opened AND we hit max pool size
        /// NOTE: 'emancipated' means that the internal connection's SqlConnection has fallen out of scope and has no references, but was not explicitly disposed\closed
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ClassData(typeof(ConnectionPoolConnectionStringAndPoolVersionProvider))]
        public static async Task ReclaimEmancipatedOnOpenTest(string connectionString, bool usePoolV2)
        {
            using ConnectionPoolVersionScope poolVersion = new(usePoolV2);

            string newConnectionString = new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = 1 }.ConnectionString;
            SqlConnection.ClearAllPools();

            InternalConnectionWrapper internalConnection = await CreateEmancipatedConnectionAsync(newConnectionString);
            ConnectionPoolWrapper connectionPool = internalConnection.ConnectionPool;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            DataTestUtility.AssertEqualsWithDescription(1, connectionPool.ConnectionCount, "Wrong number of connections in the pool.");
            DataTestUtility.AssertEqualsWithDescription(0, connectionPool.FreeConnectionCount, "Wrong number of free connections in the pool.");

            using SqlConnection connection = new(newConnectionString);
            await OpenWithAccessTokenAsync(connection);
            Assert.True(internalConnection.IsInternalConnectionOf(connection), "Connection has wrong internal connection");
            Assert.True(connectionPool.ContainsConnection(connection), "Connection is in wrong connection pool");
        }

        /// <summary>
        /// Tests if, when max pool size is reached, Open() will block until a connection becomes available
        /// </summary>
        [Trait("category", "flaky")]
        [Trait("category", "probation")]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConnectionPoolAndVersionData))]
        public static async Task MaxPoolWaitForConnectionTest(string connectionString, bool usePoolV2)
        {
            // Arrange
            using ConnectionPoolVersionScope poolVersion = new(usePoolV2);

            string newConnectionString = new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = 1 }.ConnectionString;
            SqlConnection.ClearAllPools();

            using SqlConnection connection1 = new(newConnectionString);
            await OpenWithAccessTokenAsync(connection1);

            InternalConnectionWrapper internalConnection = new(connection1);
            ConnectionPoolWrapper connectionPool = new(connection1);
            using ManualResetEventSlim workerStarted = new(false);
            using ManualResetEventSlim taskAllowedToSpeak = new(false);

            // Act
            // A blocking pool checkout must not depend on a spare thread-pool worker.
            Task waitTask = Task.Factory.StartNew(
                () => MaxPoolWaitForConnectionTask(newConnectionString, internalConnection,
                    connectionPool, workerStarted, taskAllowedToSpeak),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            try
            {
                // Assert
                Assert.True(workerStarted.Wait(TimeSpan.FromSeconds(30)), "Pool worker did not start.");
                Assert.Equal(TaskStatus.Running, waitTask.Status);
            }
            finally
            {
                // Cleanup
                connection1.Close();
                taskAllowedToSpeak.Set();
                waitTask.GetAwaiter().GetResult();
            }

            Assert.Equal(TaskStatus.RanToCompletion, waitTask.Status);
        }

        /// <summary>
        /// Verifies pool checkout does not require another worker on the caller's scheduler.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConnectionPoolAndVersionData))]
        public static async Task MaxPoolWaitForConnectionTest_SingleWorkerScheduler_ReusesConnection(
            string connectionString, bool usePoolV2)
        {
            // Arrange
            ConcurrentExclusiveSchedulerPair schedulerPair = new(
                TaskScheduler.Default, maxConcurrencyLevel: 1);

            try
            {
                // Act
                Task checkout = Task.Factory.StartNew(
                    () => MaxPoolWaitForConnectionTest(connectionString, usePoolV2),
                    CancellationToken.None, TaskCreationOptions.None,
                    schedulerPair.ExclusiveScheduler).Unwrap();
                await checkout;

                // Assert
                // The nested test also checks the physical connection and pool identities.
                Assert.Equal(TaskStatus.RanToCompletion, checkout.Status);
            }
            finally
            {
                // Cleanup
                schedulerPair.Complete();
                await schedulerPair.Completion;
            }
        }

        internal static InternalConnectionWrapper ReplacementConnectionUsesSemaphoreTask(string connectionString, Barrier syncBarrier)
        {
            InternalConnectionWrapper internalConnection = null;

            using (SqlConnection connection = new(connectionString))
            {
                try
                {
                    OpenWithAccessTokenAsync(connection).GetAwaiter().GetResult();
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

        private static async Task<InternalConnectionWrapper> CreateEmancipatedConnectionAsync(string connectionString)
        {
            SqlConnection connection = new(connectionString);
            await OpenWithAccessTokenAsync(connection);
            return new InternalConnectionWrapper(connection);
        }

        private static void MaxPoolWaitForConnectionTask(string connectionString,
            InternalConnectionWrapper internalConnection, ConnectionPoolWrapper connectionPool,
            ManualResetEventSlim workerStarted, ManualResetEventSlim waitToSpeak)
        {
            using SqlConnection connection = new(connectionString);
            workerStarted.Set();
            OpenWithAccessTokenAsync(connection).GetAwaiter().GetResult();
            waitToSpeak.Wait();
            Assert.True(internalConnection.IsInternalConnectionOf(connection), "Connection has wrong internal connection");
            Assert.True(connectionPool.ContainsConnection(connection), "Connection is in wrong connection pool");
        }

        internal static SqlConnection ReplacementConnectionObeys0TimeoutTask(string connectionString)
        {
            SqlConnection connection = new(connectionString);
            OpenWithAccessTokenAsync(connection).GetAwaiter().GetResult();
            return connection;
        }
    }
}
