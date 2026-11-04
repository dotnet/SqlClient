// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies manual-test friend access and diagnostic reads without SQL Server configuration.
    /// </summary>
    [Trait("Set", "3")]
    public class InternalsVisibleToTest
    {
        /// <summary>
        /// Guards the direct internal access used by connection diagnostics at compile time and runtime.
        /// </summary>
        [Fact]
        public void ConnectionDiagnostics_CanAccessDriverInternals()
        {
            using SqlConnection connection = new();
            DbConnectionInternal inner = connection.InnerConnection;

            Assert.Equal(ConnectionState.Closed, inner.State);
            Assert.Null(inner.Pool);
            Assert.False(inner.IsTransactionRoot);
        }

        /// <summary>
        /// Ensures repeated diagnostic snapshots do not report a different identity for the same pool.
        /// </summary>
        /// <param name="poolV2">Whether to exercise the channel pool rather than the legacy pool.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PoolDiagnostics_UnchangedPool_HasStableIdentity(bool poolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = poolV2 };
            using SqlConnection connection = new(
                $"Data Source=localhost;Min Pool Size=0;Application Name=PoolDiagnostics-{Guid.NewGuid():N}");

            try
            {
                IDbConnectionPool pool = connection.PoolGroup.GetConnectionPool(SqlConnectionFactory.Instance);
                if (poolV2)
                {
                    Assert.IsType<ChannelDbConnectionPool>(pool);
                }
                else
                {
                    Assert.IsType<WaitHandleDbConnectionPool>(pool);
                }

                string snapshot = IsolationLevelLeakTest.DescribePool(pool);
                Assert.Contains($"poolType={pool.GetType().Name}, poolIdentityHash=", snapshot);
                Assert.Equal(snapshot, IsolationLevelLeakTest.DescribePool(pool));
                Assert.Equal(0, pool.Count);
                Assert.Equal(0, pool.IdleCount);
            }
            finally
            {
                SqlConnection.ClearPool(connection);
            }
        }
    }
}
