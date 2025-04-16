// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// This unit test is just valid for .NetCore 3.0 and above
    /// </summary>
    public class EventCounterTest
    {
        public EventCounterTest()
        {
            ClearConnectionPools();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventCounter_HardConnectionsCounters_Functional()
        {
            //create a non-pooled connection
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = false };

            var ahc = SqlClientEventSourceProps.ActiveHardConnections;
            var npc = SqlClientEventSourceProps.NonPooledConnections;

            using (var conn = new SqlConnection(stringBuilder.ToString()))
            {
                //initially we have no open physical connections
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);

                conn.Open();

                //when the connection gets opened, the real physical connection appears
                Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                Assert.Equal(npc + 1, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);

                conn.Close();

                //when the connection gets closed, the real physical connection is also closed
                Assert.Equal(ahc, SqlClientEventSourceProps.ActiveHardConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventCounter_SoftConnectionsCounters_Functional()
        {
            //create a pooled connection
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = true };

            var ahc = SqlClientEventSourceProps.ActiveHardConnections;
            var asc = SqlClientEventSourceProps.ActiveSoftConnections;
            var pc = SqlClientEventSourceProps.PooledConnections;
            var npc = SqlClientEventSourceProps.NonPooledConnections;
            var acp = SqlClientEventSourceProps.ActiveConnectionPools;
            var ac = SqlClientEventSourceProps.ActiveConnections;
            var fc = SqlClientEventSourceProps.FreeConnections;

            using (var conn = new SqlConnection(stringBuilder.ToString()))
            {
                //initially we have no open physical connections
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                    SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);

                conn.Open();

                //when the connection gets opened, the real physical connection appears
                //and the appropriate pooling infrastructure gets deployed
                Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                Assert.Equal(asc + 1, SqlClientEventSourceProps.ActiveSoftConnections);
                Assert.Equal(pc + 1, SqlClientEventSourceProps.PooledConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(acp + 1, SqlClientEventSourceProps.ActiveConnectionPools);
                Assert.Equal(ac + 1, SqlClientEventSourceProps.ActiveConnections);
                Assert.Equal(fc, SqlClientEventSourceProps.FreeConnections);
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                    SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);

                conn.Close();

                //when the connection gets closed, the real physical connection gets returned to the pool
                Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                Assert.Equal(asc, SqlClientEventSourceProps.ActiveSoftConnections);
                Assert.Equal(pc + 1, SqlClientEventSourceProps.PooledConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(acp + 1, SqlClientEventSourceProps.ActiveConnectionPools);
                Assert.Equal(ac, SqlClientEventSourceProps.ActiveConnections);
                Assert.Equal(fc + 1, SqlClientEventSourceProps.FreeConnections);
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                    SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);
            }

            using (var conn2 = new SqlConnection(stringBuilder.ToString()))
            {
                conn2.Open();

                //the next open connection will reuse the underlying physical connection
                Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                Assert.Equal(asc + 1, SqlClientEventSourceProps.ActiveSoftConnections);
                Assert.Equal(pc + 1, SqlClientEventSourceProps.PooledConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(acp + 1, SqlClientEventSourceProps.ActiveConnectionPools);
                Assert.Equal(ac + 1, SqlClientEventSourceProps.ActiveConnections);
                Assert.Equal(fc, SqlClientEventSourceProps.FreeConnections);
                Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                    SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                    SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void EventCounter_StasisCounters_Functional()
        {
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = false, Enlist = false };

            using (var conn = new SqlConnection(stringBuilder.ToString()))
            using (new TransactionScope())
            {
                conn.Open();
                conn.EnlistTransaction(System.Transactions.Transaction.Current);
                conn.Close();

                //when the connection gets closed, but the ambient transaction is still in prigress
                //the physical connection gets in stasis, until the transaction ends
                Assert.Equal(1, SqlClientEventSourceProps.StasisConnections);
            }

            //when the transaction finally ends, the physical connection is returned from stasis
            Assert.Equal(0, SqlClientEventSourceProps.StasisConnections);
        }

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3031")]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventCounter_ReclaimedConnectionsCounter_Functional()
        {
            // clean pools and pool groups
            ClearConnectionPools();
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = true, MaxPoolSize = 1 };
            stringBuilder.ConnectTimeout = Math.Max(stringBuilder.ConnectTimeout, 30);

            long rc = SqlClientEventSourceProps.ReclaimedConnections;

            int gcNumber = GC.GetGeneration(CreateEmancipatedConnection(stringBuilder.ToString()));
            // Specifying the generation number makes it to run faster by avoiding a full GC process
            GC.Collect(gcNumber);
            GC.WaitForPendingFinalizers();
            System.Threading.Thread.Sleep(200); // give the pooler some time to reclaim the connection and avoid the conflict.

            using (SqlConnection conn = new SqlConnection(stringBuilder.ToString()))
            {
                conn.Open();

                // when calling open, the connection could be reclaimed.
                if (GC.GetGeneration(conn) == gcNumber)
                {
                    Assert.Equal(rc + 1, SqlClientEventSourceProps.ReclaimedConnections);
                }
                else
                {
                    Assert.Equal(rc, SqlClientEventSourceProps.ReclaimedConnections);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventCounter_ConnectionPoolGroupsCounter_Functional()
        {
            SqlConnection.ClearAllPools();

            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = true };

            long acpg = SqlClientEventSourceProps.ActiveConnectionPoolGroups;
            long iacpg = SqlClientEventSourceProps.InactiveConnectionPoolGroups;

            using (SqlConnection conn = new SqlConnection(stringBuilder.ToString()))
            {
                conn.Open();

                // when calling open, we have 1 more active connection pool group
                Assert.Equal(acpg + 1, SqlClientEventSourceProps.ActiveConnectionPoolGroups);

                conn.Close();
            }

            SqlConnection.ClearAllPools();

            // poolGroup state is changed from Active to Idle
            PruneConnectionPoolGroups();

            // poolGroup state is changed from Idle to Disabled
            PruneConnectionPoolGroups();
            Assert.Equal(acpg, SqlClientEventSourceProps.ActiveConnectionPoolGroups);
            Assert.Equal(iacpg + 1, SqlClientEventSourceProps.InactiveConnectionPoolGroups);

            // Remove poolGroup from poolGroupsToRelease list
            PruneConnectionPoolGroups();
            Assert.Equal(iacpg, SqlClientEventSourceProps.ActiveConnectionPoolGroups);
        }

        private static InternalConnectionWrapper CreateEmancipatedConnection(string connectionString)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return new InternalConnectionWrapper(connection);
        }

        private void ClearConnectionPools()
        {
            //ClearAllPoos kills all the existing pooled connection thus deactivating all the active pools
            long liveConnectionPools = SqlClientEventSourceProps.ActiveConnectionPools +
                                      SqlClientEventSourceProps.InactiveConnectionPools;
            SqlConnection.ClearAllPools();
            Assert.InRange(SqlClientEventSourceProps.InactiveConnectionPools, 0, liveConnectionPools);
            Assert.Equal(0, SqlClientEventSourceProps.ActiveConnectionPools);

            long icp = SqlClientEventSourceProps.InactiveConnectionPools;

            // The 1st PruneConnectionPoolGroups call cleans the dangling inactive connection pools.
            PruneConnectionPoolGroups();
            // If the pool isn't empty, it's because there are active connections or distributed transactions that need it.
            Assert.InRange(SqlClientEventSourceProps.InactiveConnectionPools, 0, icp);

            //the 2nd call deactivates the dangling connection pool groups
            long liveConnectionPoolGroups = SqlClientEventSourceProps.ActiveConnectionPoolGroups +
                                           SqlClientEventSourceProps.InactiveConnectionPoolGroups;
            long acpg = SqlClientEventSourceProps.ActiveConnectionPoolGroups;
            PruneConnectionPoolGroups();
            Assert.InRange(SqlClientEventSourceProps.InactiveConnectionPoolGroups, 0, liveConnectionPoolGroups);
            // If the pool entry isn't empty, it's because there are active pools that need it.
            Assert.InRange(SqlClientEventSourceProps.ActiveConnectionPoolGroups, 0, acpg);

            long icpg = SqlClientEventSourceProps.InactiveConnectionPoolGroups;
            //the 3rd call cleans the dangling connection pool groups
            PruneConnectionPoolGroups();
            Assert.InRange(SqlClientEventSourceProps.InactiveConnectionPoolGroups, 0, icpg);
        }

        private static void PruneConnectionPoolGroups()
        {
            FieldInfo connectionFactoryField = GetConnectionFactoryField();
            MethodInfo pruneConnectionPoolGroupsMethod =
                connectionFactoryField.FieldType.GetMethod("PruneConnectionPoolGroups",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Assert(pruneConnectionPoolGroupsMethod != null);
            pruneConnectionPoolGroupsMethod.Invoke(connectionFactoryField.GetValue(null), new[] { (object)null });
        }

        private static FieldInfo GetConnectionFactoryField()
        {
            FieldInfo connectionFactoryField =
                typeof(SqlConnection).GetField("s_connectionFactory", BindingFlags.Static | BindingFlags.NonPublic);
            Debug.Assert(connectionFactoryField != null);
            return connectionFactoryField;
        }
    }

    internal static class SqlClientEventSourceProps
    {
        private static readonly object s_log;
        private static readonly FieldInfo _activeHardConnectionsCounter;
        private static readonly FieldInfo _hardConnectsCounter;
        private static readonly FieldInfo _hardDisconnectsCounter;
        private static readonly FieldInfo _activeSoftConnectionsCounter;
        private static readonly FieldInfo _softConnectsCounter;
        private static readonly FieldInfo _softDisconnectsCounter;
        private static readonly FieldInfo _nonPooledConnectionsCounter;
        private static readonly FieldInfo _pooledConnectionsCounter;
        private static readonly FieldInfo _activeConnectionPoolGroupsCounter;
        private static readonly FieldInfo _inactiveConnectionPoolGroupsCounter;
        private static readonly FieldInfo _activeConnectionPoolsCounter;
        private static readonly FieldInfo _inactiveConnectionPoolsCounter;
        private static readonly FieldInfo _activeConnectionsCounter;
        private static readonly FieldInfo _freeConnectionsCounter;
        private static readonly FieldInfo _stasisConnectionsCounter;
        private static readonly FieldInfo _reclaimedConnectionsCounter;

        static SqlClientEventSourceProps()
        {
            Type sqlClientEventSourceType =
                Assembly.GetAssembly(typeof(SqlConnection))!.GetType("Microsoft.Data.SqlClient.SqlClientEventSource");
            Debug.Assert(sqlClientEventSourceType != null);
            FieldInfo logField = sqlClientEventSourceType.GetField("Log", BindingFlags.Static | BindingFlags.NonPublic);
            Debug.Assert(logField != null);
            s_log = logField.GetValue(null);

            BindingFlags _bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            _activeHardConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_activeHardConnectionsCounter), _bindingFlags);
            Debug.Assert(_activeHardConnectionsCounter != null);
            _hardConnectsCounter =
                sqlClientEventSourceType.GetField(nameof(_hardConnectsCounter), _bindingFlags);
            Debug.Assert(_hardConnectsCounter != null);
            _hardDisconnectsCounter =
                sqlClientEventSourceType.GetField(nameof(_hardDisconnectsCounter), _bindingFlags);
            Debug.Assert(_hardDisconnectsCounter != null);
            _activeSoftConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_activeSoftConnectionsCounter), _bindingFlags);
            Debug.Assert(_activeSoftConnectionsCounter != null);
            _softConnectsCounter =
                sqlClientEventSourceType.GetField(nameof(_softConnectsCounter), _bindingFlags);
            Debug.Assert(_softConnectsCounter != null);
            _softDisconnectsCounter =
                sqlClientEventSourceType.GetField(nameof(_softDisconnectsCounter), _bindingFlags);
            Debug.Assert(_softDisconnectsCounter != null);
            _nonPooledConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_nonPooledConnectionsCounter), _bindingFlags);
            Debug.Assert(_nonPooledConnectionsCounter != null);
            _pooledConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_pooledConnectionsCounter), _bindingFlags);
            Debug.Assert(_pooledConnectionsCounter != null);
            _activeConnectionPoolGroupsCounter =
                sqlClientEventSourceType.GetField(nameof(_activeConnectionPoolGroupsCounter), _bindingFlags);
            Debug.Assert(_activeConnectionPoolGroupsCounter != null);
            _inactiveConnectionPoolGroupsCounter =
                sqlClientEventSourceType.GetField(nameof(_inactiveConnectionPoolGroupsCounter), _bindingFlags);
            Debug.Assert(_inactiveConnectionPoolGroupsCounter != null);
            _activeConnectionPoolsCounter =
                sqlClientEventSourceType.GetField(nameof(_activeConnectionPoolsCounter), _bindingFlags);
            Debug.Assert(_activeConnectionPoolsCounter != null);
            _inactiveConnectionPoolsCounter =
                sqlClientEventSourceType.GetField(nameof(_inactiveConnectionPoolsCounter), _bindingFlags);
            Debug.Assert(_inactiveConnectionPoolsCounter != null);
            _activeConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_activeConnectionsCounter), _bindingFlags);
            Debug.Assert(_activeConnectionsCounter != null);
            _freeConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_freeConnectionsCounter), _bindingFlags);
            Debug.Assert(_freeConnectionsCounter != null);
            _stasisConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_stasisConnectionsCounter), _bindingFlags);
            Debug.Assert(_stasisConnectionsCounter != null);
            _reclaimedConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(_reclaimedConnectionsCounter), _bindingFlags);
            Debug.Assert(_reclaimedConnectionsCounter != null);
        }

        public static long ActiveHardConnections => (long)_activeHardConnectionsCounter.GetValue(s_log)!;

        public static long HardConnects => (long)_hardConnectsCounter.GetValue(s_log)!;

        public static long HardDisconnects => (long)_hardDisconnectsCounter.GetValue(s_log)!;

        public static long ActiveSoftConnections => (long)_activeSoftConnectionsCounter.GetValue(s_log)!;

        public static long SoftConnects => (long)_softConnectsCounter.GetValue(s_log)!;

        public static long SoftDisconnects => (long)_softDisconnectsCounter.GetValue(s_log)!;

        public static long NonPooledConnections => (long)_nonPooledConnectionsCounter.GetValue(s_log)!;

        public static long PooledConnections => (long)_pooledConnectionsCounter.GetValue(s_log)!;

        public static long ActiveConnectionPoolGroups => (long)_activeConnectionPoolGroupsCounter.GetValue(s_log)!;

        public static long InactiveConnectionPoolGroups => (long)_inactiveConnectionPoolGroupsCounter.GetValue(s_log)!;

        public static long ActiveConnectionPools => (long)_activeConnectionPoolsCounter.GetValue(s_log)!;

        public static long InactiveConnectionPools => (long)_inactiveConnectionPoolsCounter.GetValue(s_log)!;

        public static long ActiveConnections => (long)_activeConnectionsCounter.GetValue(s_log)!;

        public static long FreeConnections => (long)_freeConnectionsCounter.GetValue(s_log)!;

        public static long StasisConnections => (long)_stasisConnectionsCounter.GetValue(s_log)!;

        public static long ReclaimedConnections => (long)_reclaimedConnectionsCounter.GetValue(s_log)!;
    }
}
