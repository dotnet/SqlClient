// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Transactions;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Quantifies the cost of re-asserting the session isolation level when a pooled physical
    /// connection is re-checked-out from the transacted pool inside an open TransactionScope.
    ///
    /// Background: on that re-attach the driver emits
    /// <c>SET TRANSACTION ISOLATION LEVEL &lt;ambient&gt;;</c>, because the queued
    /// sp_reset_connection_keep_transaction does not preserve the session isolation level on
    /// every back end (notably Azure SQL DB), which silently downgraded the scope's level from
    /// the second open onwards (issue #146).
    ///
    /// The reset itself is free — it rides as a bit in the next packet's TDS header — but the
    /// SET is an additional round trip. These benchmarks isolate that round trip:
    ///
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="OpensInsideScope_Serializable"/> re-asserts on every re-checkout, so it
    ///     pays (OpensPerScope - 1) extra round trips.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="OpensInsideScope_ReadCommitted"/> takes the skip path — READ COMMITTED is
    ///     what the session reverts to after the reset anyway — so it pays none, and shows the
    ///     floor cost of the scope itself.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="OpensOutsideScope"/> is the control: identical open/execute/close work
    ///     with no ambient transaction, so it never reaches the re-attach path at all.
    ///   </description></item>
    /// </list>
    ///
    /// The delta between the Serializable and ReadCommitted variants is the cost this fix adds.
    /// Note that it is a network round trip, so a localhost run will understate it substantially
    /// compared to a cloud back end — measure against the deployment you care about.
    ///
    /// Related issue: #146
    /// </summary>
    public class TransactionScopeIsolationRunner : BaseRunner
    {
        /// <summary>
        /// Connections opened and closed inside a single scope. The first open enlists; every
        /// subsequent one is a transacted-pool re-checkout and is what this benchmark measures,
        /// so N opens exercise N-1 re-attaches.
        /// </summary>
        [Params(5)]
        public int OpensPerScope { get; set; }

        private string _connectionString;

        [GlobalSetup]
        public void Setup()
        {
            // Max Pool Size = 1 forces every open inside a scope onto the same physical
            // connection, which is what drives the transacted-pool re-checkout path.
            _connectionString = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MinPoolSize = 1,
                ApplicationName = nameof(TransactionScopeIsolationRunner)
            }.ConnectionString;

            // Establish the physical connection up front so the measured iterations are pure
            // pool checkouts rather than physical connects.
            using SqlConnection warmup = new(_connectionString);
            warmup.Open();
            Execute(warmup);
        }

        [GlobalCleanup]
        public void Cleanup() => SqlConnection.ClearAllPools();

        [Benchmark(Baseline = true)]
        public void OpensOutsideScope()
        {
            for (int i = 0; i < OpensPerScope; i++)
            {
                using SqlConnection conn = new(_connectionString);
                conn.Open();
                Execute(conn);
            }
        }

        [Benchmark]
        public void OpensInsideScope_ReadCommitted() => RunScope(IsolationLevel.ReadCommitted);

        [Benchmark]
        public void OpensInsideScope_Serializable() => RunScope(IsolationLevel.Serializable);

        [Benchmark]
        public Task OpensInsideScope_SerializableAsync() => RunScopeAsync(IsolationLevel.Serializable);

        private void RunScope(IsolationLevel level)
        {
            using TransactionScope scope = CreateScope(level);

            for (int i = 0; i < OpensPerScope; i++)
            {
                using SqlConnection conn = new(_connectionString);
                conn.Open();
                Execute(conn);
            }

            scope.Complete();
        }

        private async Task RunScopeAsync(IsolationLevel level)
        {
            using TransactionScope scope = CreateScope(level);

            for (int i = 0; i < OpensPerScope; i++)
            {
                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();
                await ExecuteAsync(conn);
            }

            scope.Complete();
        }

        private static TransactionScope CreateScope(IsolationLevel level) =>
            new(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = level },
                TransactionScopeAsyncFlowOption.Enabled);

        // A trivial command is required rather than a bare open/close: the queued reset only
        // costs anything once a packet is actually sent, so without a command the baseline
        // would never flush it and the comparison would be meaningless.
        private static void Execute(SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
        }

        private static async Task ExecuteAsync(SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
        }
    }
}
