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
    public class MetricsTest
    {
#if NETFRAMEWORK
        private readonly static TraceSwitch s_perfCtrSwitch = new TraceSwitch("ConnectionPoolPerformanceCounterDetail", "level of detail to track with connection pool performance counters");
#endif

        public MetricsTest()
        {
            ClearConnectionPools();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void NonPooledConnectionsCounters_Functional()
        {
            //create a non-pooled connection
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { Pooling = false };

            var ahc = SqlClientEventSourceProps.ActiveHardConnections;
            var npc = SqlClientEventSourceProps.NonPooledConnections;

            using (var conn = new SqlConnection(stringBuilder.ToString()))
            {
                if (SupportsActiveConnectionCounters)
                {
                    //initially we have no open physical connections
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                }

                conn.Open();

                //when the connection gets opened, the real physical connection appears
                if (SupportsActiveConnectionCounters)
                {
                    Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                }
                Assert.Equal(npc + 1, SqlClientEventSourceProps.NonPooledConnections);

                conn.Close();

                //when the connection gets closed, the real physical connection is also closed
                if (SupportsActiveConnectionCounters)
                {
                    Assert.Equal(ahc, SqlClientEventSourceProps.ActiveHardConnections);
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                }
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void PooledConnectionsCounters_Functional()
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
                if (SupportsActiveConnectionCounters)
                {
                    //initially we have no open physical connections
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                    Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                        SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);
                }

                conn.Open();

                //when the connection gets opened, the real physical connection appears
                //and the appropriate pooling infrastructure gets deployed
                if (SupportsActiveConnectionCounters)
                {
                    Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                    Assert.Equal(asc + 1, SqlClientEventSourceProps.ActiveSoftConnections);
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                    Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                        SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);
                }
                Assert.Equal(pc + 1, SqlClientEventSourceProps.PooledConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(acp + 1, SqlClientEventSourceProps.ActiveConnectionPools);
                if (VerboseActiveConnectionCountersEnabled)
                {
                    Assert.Equal(ac + 1, SqlClientEventSourceProps.ActiveConnections);
                    Assert.Equal(fc, SqlClientEventSourceProps.FreeConnections);
                }

                conn.Close();

                //when the connection gets closed, the real physical connection gets returned to the pool
                if (SupportsActiveConnectionCounters)
                {
                    Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                    Assert.Equal(asc, SqlClientEventSourceProps.ActiveSoftConnections);
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                    Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                        SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);
                }
                Assert.Equal(pc + 1, SqlClientEventSourceProps.PooledConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(acp + 1, SqlClientEventSourceProps.ActiveConnectionPools);
                if (VerboseActiveConnectionCountersEnabled)
                {
                    Assert.Equal(ac, SqlClientEventSourceProps.ActiveConnections);
                    Assert.Equal(fc + 1, SqlClientEventSourceProps.FreeConnections);
                }
            }

            using (var conn2 = new SqlConnection(stringBuilder.ToString()))
            {
                conn2.Open();

                //the next open connection will reuse the underlying physical connection
                if (SupportsActiveConnectionCounters)
                {
                    Assert.Equal(ahc + 1, SqlClientEventSourceProps.ActiveHardConnections);
                    Assert.Equal(asc + 1, SqlClientEventSourceProps.ActiveSoftConnections);
                    Assert.Equal(SqlClientEventSourceProps.ActiveHardConnections,
                        SqlClientEventSourceProps.HardConnects - SqlClientEventSourceProps.HardDisconnects);
                    Assert.Equal(SqlClientEventSourceProps.ActiveSoftConnections,
                        SqlClientEventSourceProps.SoftConnects - SqlClientEventSourceProps.SoftDisconnects);
                }
                Assert.Equal(pc + 1, SqlClientEventSourceProps.PooledConnections);
                Assert.Equal(npc, SqlClientEventSourceProps.NonPooledConnections);
                Assert.Equal(acp + 1, SqlClientEventSourceProps.ActiveConnectionPools);
                if (VerboseActiveConnectionCountersEnabled)
                {
                    Assert.Equal(ac + 1, SqlClientEventSourceProps.ActiveConnections);
                    Assert.Equal(fc, SqlClientEventSourceProps.FreeConnections);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void StasisCounters_Functional()
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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void TransactedConnectionPool_VerifyActiveConnectionCounters()
        {
            // This test verifies that the active connection count metric never goes negative
            // when connections are returned to the pool while enlisted in a transaction.
            // This is a regression test for issue #3640 where an extra DeactivateConnection
            // call was causing the active connection count to go negative.

            // Arrange
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                Enlist = false,
                MinPoolSize = 0,
                MaxPoolSize = 10
            };

            // Clear pools to start fresh
            ClearConnectionPools();

            long initialActiveSoftConnections = SqlClientEventSourceProps.ActiveSoftConnections;
            long initialActiveHardConnections = SqlClientEventSourceProps.ActiveHardConnections;
            long initialActiveConnections = SqlClientEventSourceProps.ActiveConnections;

            // Act and Assert
            // Verify counters at each step in the lifecycle of a transacted connection
            using (var txScope = new TransactionScope())
            {
                using (var conn = new SqlConnection(stringBuilder.ToString()))
                {
                    conn.Open();
                    conn.EnlistTransaction(System.Transactions.Transaction.Current);

                    if (SupportsActiveConnectionCounters)
                    {
                        // Connection should be active
                        Assert.Equal(initialActiveSoftConnections + 1, SqlClientEventSourceProps.ActiveSoftConnections);
                        Assert.Equal(initialActiveHardConnections + 1, SqlClientEventSourceProps.ActiveHardConnections);
                        Assert.Equal(initialActiveConnections + 1, SqlClientEventSourceProps.ActiveConnections);
                    }

                    conn.Close();

                    // Connection is returned to pool but still in transaction (stasis)
                    if (SupportsActiveConnectionCounters)
                    {
                        // Connection should be deactivated (returned to pool)
                        Assert.Equal(initialActiveSoftConnections, SqlClientEventSourceProps.ActiveSoftConnections);
                        Assert.Equal(initialActiveHardConnections + 1, SqlClientEventSourceProps.ActiveHardConnections);
                        Assert.Equal(initialActiveConnections, SqlClientEventSourceProps.ActiveConnections);
                    }
                }

                // Completing the transaction after the connection is closed ensures that the connection
                // is in the transacted pool at the time the transaction ends. This verifies that the
                // transition from the transacted pool back to the main pool properly updates the counters.
                txScope.Complete();
            }

            if (SupportsActiveConnectionCounters)
            {
                Assert.Equal(initialActiveSoftConnections, SqlClientEventSourceProps.ActiveSoftConnections);
                Assert.Equal(initialActiveHardConnections+1, SqlClientEventSourceProps.ActiveHardConnections);
                Assert.Equal(initialActiveConnections, SqlClientEventSourceProps.ActiveConnections);
            }
        }

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3031")]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ReclaimedConnectionsCounter_Functional()
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
        public void ConnectionPoolGroupsCounter_Functional()
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

        // Only the .NET Core build supports the active-hard-connections and active-soft-connects counters. The .NET Framework
        // build doesn't have comparable performance counters.
        private static bool SupportsActiveConnectionCounters =>
#if NET
            true;
#else
            false;
#endif

        private static bool VerboseActiveConnectionCountersEnabled =>
            SupportsActiveConnectionCounters ||
#if NET
            true;
#else
            s_perfCtrSwitch.Level == TraceLevel.Verbose;
#endif
    }

    internal static class SqlClientEventSourceProps
    {
        private static readonly object s_log;
        private static readonly Func<long> s_getActiveHardConnections;
        private static readonly Func<long> s_getHardConnects;
        private static readonly Func<long> s_getHardDisconnects;
        private static readonly Func<long> s_getActiveSoftConnections;
        private static readonly Func<long> s_getSoftConnects;
        private static readonly Func<long> s_getSoftDisconnects;
        private static readonly Func<long> s_getNonPooledConnections;
        private static readonly Func<long> s_getPooledConnections;
        private static readonly Func<long> s_getActiveConnectionPoolGroups;
        private static readonly Func<long> s_getInactiveConnectionPoolGroups;
        private static readonly Func<long> s_getActiveConnectionPools;
        private static readonly Func<long> s_getInactiveConnectionPools;
        private static readonly Func<long> s_getActiveConnections;
        private static readonly Func<long> s_getFreeConnections;
        private static readonly Func<long> s_getStasisConnections;
        private static readonly Func<long> s_getReclaimedConnections;

        static SqlClientEventSourceProps()
        {
            Type sqlClientEventSourceType =
                Assembly.GetAssembly(typeof(SqlConnection))!.GetType("Microsoft.Data.SqlClient.SqlClientEventSource");
            Debug.Assert(sqlClientEventSourceType != null);
            FieldInfo metricsField = sqlClientEventSourceType.GetField("Metrics", BindingFlags.Static | BindingFlags.Public);
            Debug.Assert(metricsField != null);
            Type sqlClientMetricsType = metricsField.FieldType;
            s_log = metricsField.GetValue(null);

#if NETFRAMEWORK
            Func<long> notApplicableFunction = static () => -1;

            // .NET Framework doesn't have performance counters for the number of hard and soft connections.
            s_getActiveHardConnections = notApplicableFunction;
            s_getActiveSoftConnections = notApplicableFunction;
#endif
            s_getActiveHardConnections = GenerateFieldGetter("_activeHardConnections");
            s_getHardConnects = GenerateFieldGetter("_hardConnectsRate");
            s_getHardDisconnects = GenerateFieldGetter("_hardDisconnectsRate");
            s_getActiveSoftConnections = GenerateFieldGetter("_activeSoftConnections");
            s_getSoftConnects = GenerateFieldGetter("_softConnectsRate");
            s_getSoftDisconnects = GenerateFieldGetter("_softDisconnectsRate");
            s_getNonPooledConnections = GenerateFieldGetter("_nonPooledConnections");
            s_getPooledConnections = GenerateFieldGetter("_pooledConnections");
            s_getActiveConnectionPoolGroups = GenerateFieldGetter("_activeConnectionPoolGroups");
            s_getInactiveConnectionPoolGroups = GenerateFieldGetter("_inactiveConnectionPoolGroups");
            s_getActiveConnectionPools = GenerateFieldGetter("_activeConnectionPools");
            s_getInactiveConnectionPools = GenerateFieldGetter("_inactiveConnectionPools");
            s_getActiveConnections = GenerateFieldGetter("_activeConnections");
            s_getFreeConnections = GenerateFieldGetter("_freeConnections");
            s_getStasisConnections = GenerateFieldGetter("_stasisConnections");
            s_getReclaimedConnections = GenerateFieldGetter("_reclaimedConnections");

#if NET
            static Func<long> GenerateFieldGetter(string fieldName)
            {
                FieldInfo counterField = s_log.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                Debug.Assert(counterField != null);
                return () => (long)counterField.GetValue(s_log)!;
            }
#else
            static Func<long> GenerateFieldGetter(string fieldName)
            {
                FieldInfo counterField = s_log.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Debug.Assert(counterField != null);

                PerformanceCounter counter = counterField?.GetValue(s_log) as PerformanceCounter;
                return () => counter is null ? -1 : counter.RawValue;
            }
#endif
        }

        public static long ActiveHardConnections => s_getActiveHardConnections();

        public static long HardConnects => s_getHardConnects();

        public static long HardDisconnects => s_getHardDisconnects();

        public static long ActiveSoftConnections => s_getActiveSoftConnections();

        public static long SoftConnects => s_getSoftConnects();

        public static long SoftDisconnects => s_getSoftDisconnects();

        public static long NonPooledConnections => s_getNonPooledConnections();

        public static long PooledConnections => s_getPooledConnections();

        public static long ActiveConnectionPoolGroups => s_getActiveConnectionPoolGroups();

        public static long InactiveConnectionPoolGroups => s_getInactiveConnectionPoolGroups();

        public static long ActiveConnectionPools => s_getActiveConnectionPools();

        public static long InactiveConnectionPools => s_getInactiveConnectionPools();

        public static long ActiveConnections => s_getActiveConnections();

        public static long FreeConnections => s_getFreeConnections();

        public static long StasisConnections => s_getStasisConnections();

        public static long ReclaimedConnections => s_getReclaimedConnections();
    }
}
