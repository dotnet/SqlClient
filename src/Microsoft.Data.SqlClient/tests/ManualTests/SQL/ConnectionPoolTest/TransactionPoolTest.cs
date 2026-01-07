// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class TransactionPoolTest
    {
        /// <summary>
        /// Tests if connections in a distributed transaction are put into a transaction pool. Also checks that clearallpools 
        /// does not clear transaction connections and that the transaction root is put into "stasis" when closed
        /// Synapse: only supports local transaction request.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void BasicTransactionPoolTest(string connectionString)
        {
            SqlConnection.ClearAllPools();
            ConnectionPoolWrapper connectionPool = null;

            using (TransactionScope transScope = new())
            {
                using SqlConnection connection1 = new(connectionString);
                using SqlConnection connection2 = new(connectionString);
                connection1.Open();
                connection2.Open();
                connectionPool = new ConnectionPoolWrapper(connection1);

                InternalConnectionWrapper internalConnection1 = new(connection1);
                InternalConnectionWrapper internalConnection2 = new(connection2);

                Assert.True(internalConnection1.IsEnlistedInTransaction, "First connection not in transaction");
                Assert.True(internalConnection1.IsTransactionRoot, "First connection not transaction root");
                Assert.True(internalConnection2.IsEnlistedInTransaction, "Second connection not in transaction");
                Assert.False(internalConnection2.IsTransactionRoot, "Second connection is transaction root");

                // Attempt to re-use root connection
                connection1.Close();
                using SqlConnection connection3 = new(connectionString);
                connection3.Open();

                Assert.True(connectionPool.ContainsConnection(connection3), "New connection in wrong pool");
                Assert.True(internalConnection1.IsInternalConnectionOf(connection3), "Root connection was not re-used");

                // Attempt to re-use non-root connection
                connection2.Close();
                using SqlConnection connection4 = new(connectionString);
                connection4.Open();
                Assert.True(internalConnection2.IsInternalConnectionOf(connection4), "Connection did not re-use expected internal connection");
                Assert.True(connectionPool.ContainsConnection(connection4), "New connection is in the wrong pool");
                connection4.Close();

                // Use a different connection string
                using SqlConnection connection5 = new(connectionString + ";App=SqlConnectionPoolUnitTest;");
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
        /// Synapse: only supports local transaction request.
        /// </summary>
        /// <param name="connectionString"></param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [ClassData(typeof(ConnectionPoolConnectionStringProvider))]
        public static void TransactionCleanupTest(string connectionString)
        {
            SqlConnection.ClearAllPools();
            ConnectionPoolWrapper connectionPool = null;

            using (TransactionScope transScope = new())
            {
                using SqlConnection connection1 = new(connectionString);
                using SqlConnection connection2 = new(connectionString);
                connection1.Open();
                connection2.Open();
                InternalConnectionWrapper internalConnection1 = new(connection1);
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
    }
}
