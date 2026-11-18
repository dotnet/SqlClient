// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Measures one checkout of a connection that has sat idle in the pool long enough to be
    /// probed for liveness before it is handed out.
    ///
    /// The pool skips the liveness probe for a connection that did I/O or passed a probe within
    /// the last 5 ms (<c>TdsParserStateObject.CheckConnectionWindow</c>). In the rapid open/close
    /// loop of <see cref="ConnectionPoolChurnRunner"/> at most one checkout in every 5 ms is
    /// probed, and its cost is averaged over the rest. Here every iteration waits past that window
    /// outside the measurement, then times a single open, so every measured checkout pays for the
    /// probe. Under managed SNI the probe is
    /// <c>SniTcpHandle.CheckConnection</c>, whose socket poll must not wait: Windows rounds any
    /// positive poll timeout up to the timer tick, about 15 ms by default.
    ///
    /// The pool holds a single connection, so every iteration probes the same idle socket. Set
    /// <c>UseManagedSniOnWindows</c> to measure the managed probe on Windows; on other operating
    /// systems the managed implementation is the only one.
    ///
    /// Related issue: #5070
    /// </summary>
    public class ConnectionPoolIdleCheckoutRunner : BaseRunner
    {
        /// <summary>
        /// How long the connection sits idle before each measured checkout: twice the 5 ms window,
        /// so clock granularity cannot leave it inside the window.
        /// </summary>
        private static readonly TimeSpan s_idleTime = TimeSpan.FromMilliseconds(10);

        private string _connectionString;

        [GlobalSetup]
        public void Setup()
        {
            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                // One connection, kept open, so each checkout probes the same idle socket rather
                // than opening a new physical connection.
                MinPoolSize = 1,
                MaxPoolSize = 1
            };
            _connectionString = builder.ConnectionString;

            // Opened and released before measurement, so no measured open is a physical connect.
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
        }

        /// <summary>
        /// Leaves the pooled connection idle past the liveness window, outside the measurement.
        /// </summary>
        [IterationSetup]
        public void IterationSetup()
        {
            Thread.Sleep(s_idleTime);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(conn);
        }

        [Benchmark]
        public void OpenIdlePooledConnection()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            // Dispose returns the connection to the pool.
        }

        [Benchmark]
        public async Task OpenIdlePooledConnectionAsync()
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            // Dispose returns the connection to the pool.
        }
    }
}
