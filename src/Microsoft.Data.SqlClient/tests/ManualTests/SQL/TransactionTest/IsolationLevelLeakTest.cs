// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Transactions;
using Xunit;
using IsolationLevel = System.Data.IsolationLevel;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // SqlTransaction / TransactionScope used to leave the pooled connection
    // with the elevated session isolation level. The next user of the pooled
    // connection would silently inherit it. The fix resets the session
    // isolation level to READ COMMITTED when the connection is returned to
    // the pool.
    public static class IsolationLevelLeakTest
    {
        private const string GetIsoSql = @"
SELECT CASE transaction_isolation_level
            WHEN 0 THEN 'Unspecified'
            WHEN 1 THEN 'ReadUncommitted'
            WHEN 2 THEN 'ReadCommitted'
            WHEN 3 THEN 'RepeatableRead'
            WHEN 4 THEN 'Serializable'
            WHEN 5 THEN 'Snapshot'
       END
FROM sys.dm_exec_sessions WHERE session_id = @@SPID;";

        private static string PooledMaxOneConnString =>
            new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = false,
                ApplicationName = "IsoLeakTest"
            }.ConnectionString;

        private static int GetSpid(SqlConnection c)
        {
            using SqlCommand cmd = new SqlCommand("SELECT @@SPID;", c);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static string GetIso(SqlConnection c)
        {
            using SqlCommand cmd = new SqlCommand(GetIsoSql, c);
            return (string)cmd.ExecuteScalar();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void SqlTransaction_SerializableDoesNotLeakAcrossPool()
        {
            string cs = PooledMaxOneConnString;
            int spid1;
            using (SqlConnection c = new SqlConnection(cs))
            {
                c.Open();
                spid1 = GetSpid(c);
                using SqlTransaction tx = c.BeginTransaction(IsolationLevel.Serializable);
                Assert.Equal("Serializable", GetIsoOnTx(c, tx));
                tx.Rollback();
            }

            using (SqlConnection c = new SqlConnection(cs))
            {
                c.Open();
                Assert.Equal(spid1, GetSpid(c)); // pool reuse
                Assert.Equal("ReadCommitted", GetIso(c));
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TransactionScope_SerializableDoesNotLeakAcrossPool()
        {
            string cs = PooledMaxOneConnString;
            int spid1;
            using (var scope = new TransactionScope(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable },
                TransactionScopeAsyncFlowOption.Enabled))
            using (SqlConnection c = new SqlConnection(cs))
            {
                c.Open();
                spid1 = GetSpid(c);
                Assert.Equal("Serializable", GetIso(c));
                scope.Complete();
            }

            using (SqlConnection c = new SqlConnection(cs))
            {
                c.Open();
                Assert.Equal(spid1, GetSpid(c));
                Assert.Equal("ReadCommitted", GetIso(c));
            }
        }

        // Negative test: legacy switch ON brings the old leak back. Runs in
        // an isolated AppDomain on .NET Framework / process boundary on .NET
        // would be ideal, but AppContext switches set before any pool entry
        // is created suffice when this test runs first in its own collection.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void LegacySwitch_PreservesOldLeakBehavior()
        {
            const string Switch = "Switch.Microsoft.Data.SqlClient.UseLegacyIsolationLevelBehavior";
            AppContext.SetSwitch(Switch, true);
            try
            {
                // Use a distinct app name so this test gets its own pool group
                // and isn't affected by entries created by the other tests.
                string cs = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    Pooling = true,
                    MaxPoolSize = 1,
                    MultipleActiveResultSets = false,
                    ApplicationName = "IsoLeakTest-Legacy"
                }.ConnectionString;

                int spid1;
                using (SqlConnection c = new SqlConnection(cs))
                {
                    c.Open();
                    spid1 = GetSpid(c);
                    using SqlTransaction tx = c.BeginTransaction(IsolationLevel.Serializable);
                    tx.Rollback();
                }

                using (SqlConnection c = new SqlConnection(cs))
                {
                    c.Open();
                    Assert.Equal(spid1, GetSpid(c));
                    Assert.Equal("Serializable", GetIso(c)); // legacy: leaks
                }
            }
            finally
            {
                AppContext.SetSwitch(Switch, false);
                SqlConnection.ClearAllPools();
            }
        }

        private static string GetIsoOnTx(SqlConnection c, SqlTransaction tx)
        {
            using SqlCommand cmd = new SqlCommand(GetIsoSql, c, tx);
            return (string)cmd.ExecuteScalar();
        }
    }
}
