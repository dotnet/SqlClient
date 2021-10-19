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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventCounter_ReclaimedConnectionsCounter_Functional()
        {
            SqlConnection.ClearAllPools();
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = true, MaxPoolSize = 1 };

            long rc = SqlClientEventSourceProps.ReclaimedConnections;

            int gcNumber = GC.GetGeneration(CreateEmancipatedConnection(stringBuilder.ToString()));
            // Specifying the generation number makes it to run faster by avoiding a full GC process
            GC.Collect(gcNumber);
            GC.WaitForPendingFinalizers();

            using (SqlConnection conn = new SqlConnection(stringBuilder.ToString()))
            {
                conn.Open();

                // when calling open, the connection could reclaimed.
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
            var liveConnectionPools = SqlClientEventSourceProps.ActiveConnectionPools +
                                      SqlClientEventSourceProps.InactiveConnectionPools;
            SqlConnection.ClearAllPools();
            Assert.InRange(SqlClientEventSourceProps.InactiveConnectionPools, 0, liveConnectionPools);
            Assert.Equal(0, SqlClientEventSourceProps.ActiveConnectionPools);

            //the 1st PruneConnectionPoolGroups call cleans the dangling inactive connection pools
            PruneConnectionPoolGroups();
            Assert.Equal(0, SqlClientEventSourceProps.InactiveConnectionPools);

            //the 2nd call deactivates the dangling connection pool groups
            var liveConnectionPoolGroups = SqlClientEventSourceProps.ActiveConnectionPoolGroups +
                                           SqlClientEventSourceProps.InactiveConnectionPoolGroups;
            PruneConnectionPoolGroups();
            Assert.InRange(SqlClientEventSourceProps.InactiveConnectionPoolGroups, 0, liveConnectionPoolGroups);
            Assert.Equal(0, SqlClientEventSourceProps.ActiveConnectionPoolGroups);

            //the 3rd call cleans the dangling connection pool groups
            PruneConnectionPoolGroups();
            Assert.Equal(0, SqlClientEventSourceProps.InactiveConnectionPoolGroups);
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
        private static readonly FieldInfo s_activeHardConnectionsCounter;
        private static readonly FieldInfo s_hardConnectsCounter;
        private static readonly FieldInfo s_hardDisconnectsCounter;
        private static readonly FieldInfo s_activeSoftConnectionsCounter;
        private static readonly FieldInfo s_softConnectsCounter;
        private static readonly FieldInfo s_softDisconnectsCounter;
        private static readonly FieldInfo s_nonPooledConnectionsCounter;
        private static readonly FieldInfo s_pooledConnectionsCounter;
        private static readonly FieldInfo s_activeConnectionPoolGroupsCounter;
        private static readonly FieldInfo s_inactiveConnectionPoolGroupsCounter;
        private static readonly FieldInfo s_activeConnectionPoolsCounter;
        private static readonly FieldInfo s_inactiveConnectionPoolsCounter;
        private static readonly FieldInfo s_activeConnectionsCounter;
        private static readonly FieldInfo s_freeConnectionsCounter;
        private static readonly FieldInfo s_stasisConnectionsCounter;
        private static readonly FieldInfo s_reclaimedConnectionsCounter;

        static SqlClientEventSourceProps()
        {
            Type sqlClientEventSourceType =
                Assembly.GetAssembly(typeof(SqlConnection))!.GetType("Microsoft.Data.SqlClient.SqlClientEventSource");
            Debug.Assert(sqlClientEventSourceType != null);
            FieldInfo logField = sqlClientEventSourceType.GetField("Log", BindingFlags.Static | BindingFlags.NonPublic);
            Debug.Assert(logField != null);
            s_log = logField.GetValue(null);

            BindingFlags _bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            s_activeHardConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_activeHardConnectionsCounter), _bindingFlags);
            Debug.Assert(s_activeHardConnectionsCounter != null);
            s_hardConnectsCounter =
                sqlClientEventSourceType.GetField(nameof(s_hardConnectsCounter), _bindingFlags);
            Debug.Assert(s_hardConnectsCounter != null);
            s_hardDisconnectsCounter =
                sqlClientEventSourceType.GetField(nameof(s_hardDisconnectsCounter), _bindingFlags);
            Debug.Assert(s_hardDisconnectsCounter != null);
            s_activeSoftConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_activeSoftConnectionsCounter), _bindingFlags);
            Debug.Assert(s_activeSoftConnectionsCounter != null);
            s_softConnectsCounter =
                sqlClientEventSourceType.GetField(nameof(s_softConnectsCounter), _bindingFlags);
            Debug.Assert(s_softConnectsCounter != null);
            s_softDisconnectsCounter =
                sqlClientEventSourceType.GetField(nameof(s_softDisconnectsCounter), _bindingFlags);
            Debug.Assert(s_softDisconnectsCounter != null);
            s_nonPooledConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_nonPooledConnectionsCounter), _bindingFlags);
            Debug.Assert(s_nonPooledConnectionsCounter != null);
            s_pooledConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_pooledConnectionsCounter), _bindingFlags);
            Debug.Assert(s_pooledConnectionsCounter != null);
            s_activeConnectionPoolGroupsCounter =
                sqlClientEventSourceType.GetField(nameof(s_activeConnectionPoolGroupsCounter), _bindingFlags);
            Debug.Assert(s_activeConnectionPoolGroupsCounter != null);
            s_inactiveConnectionPoolGroupsCounter =
                sqlClientEventSourceType.GetField(nameof(s_inactiveConnectionPoolGroupsCounter), _bindingFlags);
            Debug.Assert(s_inactiveConnectionPoolGroupsCounter != null);
            s_activeConnectionPoolsCounter =
                sqlClientEventSourceType.GetField(nameof(s_activeConnectionPoolsCounter), _bindingFlags);
            Debug.Assert(s_activeConnectionPoolsCounter != null);
            s_inactiveConnectionPoolsCounter =
                sqlClientEventSourceType.GetField(nameof(s_inactiveConnectionPoolsCounter), _bindingFlags);
            Debug.Assert(s_inactiveConnectionPoolsCounter != null);
            s_activeConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_activeConnectionsCounter), _bindingFlags);
            Debug.Assert(s_activeConnectionsCounter != null);
            s_freeConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_freeConnectionsCounter), _bindingFlags);
            Debug.Assert(s_freeConnectionsCounter != null);
            s_stasisConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_stasisConnectionsCounter), _bindingFlags);
            Debug.Assert(s_stasisConnectionsCounter != null);
            s_reclaimedConnectionsCounter =
                sqlClientEventSourceType.GetField(nameof(s_reclaimedConnectionsCounter), _bindingFlags);
            Debug.Assert(s_reclaimedConnectionsCounter != null);
        }

        public static long ActiveHardConnections => (long)s_activeHardConnectionsCounter.GetValue(s_log)!;

        public static long HardConnects => (long)s_hardConnectsCounter.GetValue(s_log)!;

        public static long HardDisconnects => (long)s_hardDisconnectsCounter.GetValue(s_log)!;

        public static long ActiveSoftConnections => (long)s_activeSoftConnectionsCounter.GetValue(s_log)!;

        public static long SoftConnects => (long)s_softConnectsCounter.GetValue(s_log)!;

        public static long SoftDisconnects => (long)s_softDisconnectsCounter.GetValue(s_log)!;

        public static long NonPooledConnections => (long)s_nonPooledConnectionsCounter.GetValue(s_log)!;

        public static long PooledConnections => (long)s_pooledConnectionsCounter.GetValue(s_log)!;

        public static long ActiveConnectionPoolGroups => (long)s_activeConnectionPoolGroupsCounter.GetValue(s_log)!;

        public static long InactiveConnectionPoolGroups => (long)s_inactiveConnectionPoolGroupsCounter.GetValue(s_log)!;

        public static long ActiveConnectionPools => (long)s_activeConnectionPoolsCounter.GetValue(s_log)!;

        public static long InactiveConnectionPools => (long)s_inactiveConnectionPoolsCounter.GetValue(s_log)!;

        public static long ActiveConnections => (long)s_activeConnectionsCounter.GetValue(s_log)!;

        public static long FreeConnections => (long)s_freeConnectionsCounter.GetValue(s_log)!;

        public static long StasisConnections => (long)s_stasisConnectionsCounter.GetValue(s_log)!;

        public static long ReclaimedConnections => (long)s_reclaimedConnectionsCounter.GetValue(s_log)!;
    }
}
