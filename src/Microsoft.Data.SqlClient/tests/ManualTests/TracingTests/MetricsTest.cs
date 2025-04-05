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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
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
#if NET
            FieldInfo connectionFactoryField =
                typeof(SqlConnection).GetField("s_connectionFactory", BindingFlags.Static | BindingFlags.NonPublic);
#else
            FieldInfo connectionFactoryField =
                typeof(SqlConnection).GetField("_connectionFactory", BindingFlags.Static | BindingFlags.NonPublic);
#endif
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
            s_log = SqlClientEventSource.Metrics;

#if NET
            s_getActiveHardConnections = GenerateFieldGetter("_activeHardConnectionsCounter");
            s_getHardConnects = GenerateFieldGetter("_hardConnectsCounter");
            s_getHardDisconnects = GenerateFieldGetter("_hardDisconnectsCounter");
            s_getActiveSoftConnections = GenerateFieldGetter("_activeSoftConnectionsCounter");
            s_getSoftConnects = GenerateFieldGetter("_softConnectsCounter");
            s_getSoftDisconnects = GenerateFieldGetter("_softDisconnectsCounter");
            s_getNonPooledConnections = GenerateFieldGetter("_nonPooledConnectionsCounter");
            s_getPooledConnections = GenerateFieldGetter("_pooledConnectionsCounter");
            s_getActiveConnectionPoolGroups = GenerateFieldGetter("_activeConnectionPoolGroupsCounter");
            s_getInactiveConnectionPoolGroups = GenerateFieldGetter("_inactiveConnectionPoolGroupsCounter");
            s_getActiveConnectionPools = GenerateFieldGetter("_activeConnectionPoolsCounter");
            s_getInactiveConnectionPools = GenerateFieldGetter("_inactiveConnectionPoolsCounter");
            s_getActiveConnections = GenerateFieldGetter("_activeConnectionsCounter");
            s_getFreeConnections = GenerateFieldGetter("_freeConnectionsCounter");
            s_getStasisConnections = GenerateFieldGetter("_stasisConnectionsCounter");
            s_getReclaimedConnections = GenerateFieldGetter("_reclaimedConnectionsCounter");

            static Func<long> GenerateFieldGetter(string fieldName)
            {
                FieldInfo counterField = s_log.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                Debug.Assert(counterField != null);
                return () => (long)counterField.GetValue(s_log)!;
            }
#else
            Func<long> notApplicableFunction = static () => -1;

            // .NET Framework doesn't have performance counters for the number of hard and soft connections.
            s_getActiveHardConnections = notApplicableFunction;
            s_getHardConnects = GeneratePerformanceCounterGetter("_hardConnectsPerSecond");
            s_getHardDisconnects = GeneratePerformanceCounterGetter("_hardDisconnectsPerSecond");
            s_getActiveSoftConnections = notApplicableFunction;
            s_getSoftConnects = GeneratePerformanceCounterGetter("_softConnectsPerSecond");
            s_getSoftDisconnects = GeneratePerformanceCounterGetter("_softDisconnectsPerSecond");

            s_getNonPooledConnections = GeneratePerformanceCounterGetter("_numberOfNonPooledConnections");
            s_getPooledConnections = GeneratePerformanceCounterGetter("_numberOfPooledConnections");
            s_getActiveConnectionPoolGroups = GeneratePerformanceCounterGetter("_numberOfActiveConnectionPoolGroups");
            s_getInactiveConnectionPoolGroups = GeneratePerformanceCounterGetter("_numberOfInactiveConnectionPoolGroups");
            s_getActiveConnectionPools = GeneratePerformanceCounterGetter("_numberOfActiveConnectionPools");
            s_getInactiveConnectionPools = GeneratePerformanceCounterGetter("_numberOfInactiveConnectionPools");
            s_getActiveConnections = GeneratePerformanceCounterGetter("_numberOfActiveConnections");
            s_getFreeConnections = GeneratePerformanceCounterGetter("_numberOfFreeConnections");
            s_getStasisConnections = GeneratePerformanceCounterGetter("_numberOfStasisConnections");
            s_getReclaimedConnections = GeneratePerformanceCounterGetter("_numberOfReclaimedConnections");

            static Func<long> GeneratePerformanceCounterGetter(string fieldName)
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
