// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.NetCore.UnitTests.Util;
using Microsoft.Data.SqlClientX;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers.Prelogin
{
    public class PoolTests
    {
        private TestBase testBase;

        public PoolTests()
        {
            testBase = new TestBase();
        }

        [Fact]
        public async Task MinPoolSize_equals_MaxPoolSize()
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MinPoolSize = 30;
                csb.MaxPoolSize = 30;
            });
            await using var conn = await dataSource.OpenConnectionAsync();
        }

        [Fact]
        public void MinPoolSize_bigger_than_MaxPoolSize_throws()
            => Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using var dataSource = testBase.CreateDataSource(csb =>
                {
                    csb.MinPoolSize = 2;
                    csb.MaxPoolSize = 1;
                });
            });

        [Fact]
        public async Task Reuse_connector_before_creating_new()
        {
            await using var dataSource = testBase.CreateDataSource();
            await using var conn = await dataSource.OpenConnectionAsync();
            var backendId = conn.InternalConnection!.ServerProcessId;
            Debug.WriteLine($"server process id: {backendId}");
            await conn.CloseAsync();
            await conn.OpenAsync();
            Assert.Equal(conn.InternalConnection!.ServerProcessId, backendId);
        }

        public static readonly object[][] AsyncParams = new object[][] { 
            new object[] { true }, 
            new object[] { false } 
        };

        [Theory]
        [MemberData(nameof(AsyncParams))]
        public async Task Get_connector_from_exhausted_pool(bool async)
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MaxPoolSize = 1;
                csb.ConnectTimeout = 0;
            });

            await using var conn1 = await dataSource.OpenConnectionAsync();

            // Pool is exhausted
            await using var conn2 = dataSource.CreateConnection();
            _ = Task.Delay(1000).ContinueWith(async _ =>
            {
                if (async)
                    await conn1.CloseAsync();
                else
                    conn1.Close();
            });
            if (async)
                await conn2.OpenAsync();
            else
                conn2.Open();
        }
        
        [Theory]
        [MemberData(nameof(AsyncParams))]
        public async Task Timeout_getting_connector_from_exhausted_pool(bool async)
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MaxPoolSize = 1;
                csb.ConnectTimeout = 2;
            });

            await using (var conn1 = dataSource.CreateConnection())
            {
                await conn1.OpenAsync();
                // Pool is now exhausted

                await using var conn2 = dataSource.CreateConnection();
                var e = async
                    ? await Assert.ThrowsAsync<Exception>(async () => await conn2.OpenAsync())!
                    : Assert.Throws<Exception>(() => conn2.Open())!;

                Assert.IsType<TimeoutException>(e.InnerException);
            }

            // conn1 should now be back in the pool as idle
            await using var conn3 = await dataSource.OpenConnectionAsync();
        }
        
        [Fact]
        //[Explicit("Timing-based")]
        public async Task OpenAsync_cancel()
        {
            await using var dataSource = testBase.CreateDataSource(csb => csb.MaxPoolSize = 1);
            await using var conn1 = await dataSource.OpenConnectionAsync();

            AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 0);

            // Pool is exhausted
            await using (var conn2 = dataSource.CreateConnection())
            {
                var cts = new CancellationTokenSource(1000);
                var openTask = conn2.OpenAsync(cts.Token);
                AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 0);
                await Assert.ThrowsAsync<OperationCanceledException>(async () => await openTask);
            }

            AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 0);
            await using (var conn2 = dataSource.CreateConnection())
            await using (new Timer(o => conn1.Close(), null, 1000, Timeout.Infinite))
            {
                await conn2.OpenAsync();
                AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 0);
            }
            AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 1);
        }
        /*
        [Fact, Description("Makes sure that when a pooled connection is closed it's properly reset, and that parameter settings aren't leaked")]
        public async Task ResetOnClose()
        {
            await using var dataSource = testBase.CreateDataSource(csb => csb.SearchPath = "public");
            await using var conn = await dataSource.OpenConnectionAsync();
            Assert.That(await conn.ExecuteScalarAsync("SHOW search_path"), Is.Not.Contains("pg_temp"));
            var backendId = conn.Connector!.BackendProcessId;
            await conn.ExecuteNonQueryAsync("SET search_path=pg_temp");
            await conn.CloseAsync();

            await conn.OpenAsync();
            Assert.That(conn.Connector.BackendProcessId, Is.EqualTo(backendId));
            Assert.That(await conn.ExecuteScalarAsync("SHOW search_path"), Is.EqualTo("public"));
        }
        
        [Fact]
        public void ConnectionPruningInterval_zero_throws()
            => Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using var dataSource = testBase.CreateDataSource(csb => csb.ConnectionPruningInterval = 0);
            });
        
        [Fact]
        public void ConnectionPruningInterval_bigger_than_ConnectionIdleLifetime_throws()
            => Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using var dataSource = testBase.CreateDataSource(csb =>
                {
                    csb.ConnectionIdleLifetime = 1;
                    csb.ConnectionPruningInterval = 2;
                });
            });

        [Theory, Explicit("Slow, and flaky under pressure, based on timing")]
        [TestCase(0, 2, 1, 2)] // min pool size 0, sample twice
        [TestCase(1, 2, 1, 2)] // min pool size 1, sample twice
        [TestCase(2, 2, 1, 2)] // min pool size 2, sample twice
        [TestCase(2, 3, 2, 2)] // test rounding up, should sample twice.
        [TestCase(2, 1, 1, 1)] // test sample once.
        [TestCase(2, 20, 3, 7)] // test high samples.
        public async Task Prune_idle_connectors(int minPoolSize, int connectionIdleLifeTime, int connectionPruningInterval, int samples)
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MinPoolSize = minPoolSize;
                csb.ConnectionIdleLifetime = connectionIdleLifeTime;
                csb.ConnectionPruningInterval = connectionPruningInterval;
            });

            var connectionPruningIntervalMs = connectionPruningInterval * 1000;

            await using var conn1 = await dataSource.OpenConnectionAsync();
            await using var conn2 = await dataSource.OpenConnectionAsync();
            await using var conn3 = await dataSource.OpenConnectionAsync();

            await conn1.CloseAsync();
            await conn2.CloseAsync();
            AssertPoolState(dataSource!, open: 3, idle: 2);

            var paddingMs = 100; // 100ms
            var sleepInterval = connectionPruningIntervalMs + paddingMs;
            var total = 0;

            for (var i = 0; i < samples - 1; i++)
            {
                total += sleepInterval;
                Thread.Sleep(sleepInterval);
                // ConnectionIdleLifetime not yet reached.
                AssertPoolState(dataSource, open: 3, idle: 2);
            }

            // final cycle to do pruning.
            Thread.Sleep(Math.Max(sleepInterval, (connectionIdleLifeTime * 1000) - total));

            // ConnectionIdleLifetime reached, we still have one connection open minimum,
            // and as a result we have minPoolSize - 1 idle connections.
            AssertPoolState(dataSource, open: Math.Max(1, minPoolSize), idle: Math.Max(0, minPoolSize - 1));
        }

        [Fact]
        [Explicit("Timing-based")]
        public async Task Prune_counts_max_lifetime_exceeded()
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MinPoolSize = 0;
                // Idle lifetime 2 seconds, 2 samples
                csb.ConnectionIdleLifetime = 2;
                csb.ConnectionPruningInterval = 1;
                csb.ConnectionLifetime = 5;
            });

            // conn1 will exceed max lifetime
            await using var conn1 = await dataSource.OpenConnectionAsync();

            // make conn1 4 seconds older than the others, so it exceeds max lifetime
            Thread.Sleep(4000);

            await using var conn2 = await dataSource.OpenConnectionAsync();
            await using var conn3 = await dataSource.OpenConnectionAsync();

            await conn1.CloseAsync();
            await conn2.CloseAsync();
            AssertPoolState(dataSource, open: 3, idle: 2);

            // wait for 1 sample
            Thread.Sleep(1000);
            // ConnectionIdleLifetime not yet reached.
            AssertPoolState(dataSource, open: 3, idle: 2);

            // close conn3, so we can see if too many connectors get pruned
            await conn3.CloseAsync();

            // wait for last sample + a bit more time for reliability
            Thread.Sleep(1500);

            // ConnectionIdleLifetime reached
            // - conn1 should have been closed due to max lifetime (but this should count as pruning)
            // - conn2 or conn3 should have been closed due to idle pruning
            // - conn3 or conn2 should remain
            AssertPoolState(dataSource, open: 1, idle: 1);
        }*/

        //Makes sure that when a waiting async open is given a connection, the continuation is executed in the TP rather than on the closing thread
        [Fact]
        public async Task Close_releases_waiter_on_another_thread()
        {
            await using var dataSource = testBase.CreateDataSource(csb => csb.MaxPoolSize = 1);
            await using var conn1 = await dataSource.OpenConnectionAsync(); // Pool is now exhausted

            AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 0);

            Func<Task<int>> asyncOpener = async () =>
            {
                using (var conn2 = dataSource.CreateConnection())
                {
                    await conn2.OpenAsync();
                    AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 0);
                }
                AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 1);
                return Environment.CurrentManagedThreadId;
            };

            // Start an async open which will not complete as the pool is exhausted.
            var asyncOpenerTask = asyncOpener();
            conn1.Close();  // Complete the async open by closing conn1
            var asyncOpenerThreadId = asyncOpenerTask.GetAwaiter().GetResult();
            AssertPoolState(dataSource, expectedOpen: 1, expectedIdle: 1);

            Assert.NotEqual(Environment.CurrentManagedThreadId, asyncOpenerThreadId);
        }

        /*[Fact] //TODO: inject internal connection creation logic to force throw an error and make this test offline compatible
        public async Task Release_waiter_on_connection_failure()
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MaxPoolSize = 1;
            });

            var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(async () =>
            {
                await using var conn = await dataSource.OpenConnectionAsync();
            })).ToArray();

            var ex = Assert.Throws<AggregateException>(() => Task.WaitAll(tasks))!;
            Assert.Equal(2, ex.InnerExceptions.Count);
            foreach (var inner in ex.InnerExceptions)
                Assert.IsType<Exception>(inner);
        }*/

        /* TODO: implement clear
        [Fact]
        [TestCase(1)]
        [TestCase(2)]
        public void ClearPool(int iterations)
        {
            var connString = new NpgsqlConnectionStringBuilder(ConnectionString)
            {
                ApplicationName = nameof(ClearPool) + iterations
            }.ToString();

            NpgsqlConnection? conn = null;
            try
            {
                for (var i = 0; i < iterations; i++)
                {
                    using (conn = new NpgsqlConnection(connString))
                    {
                        conn.Open();
                    }

                    // Now have one connection in the pool
                    Assert.True(PoolManager.Pools.TryGetValue(connString, out var pool));
                    AssertPoolState(pool, open: 1, idle: 1);

                    NpgsqlConnection.ClearPool(conn);
                    AssertPoolState(pool, open: 0, idle: 0);
                }
            }
            finally
            {
                if (conn is not null)
                    NpgsqlConnection.ClearPool(conn);
            }
        }
        
        [Fact]
        public void ClearPool_with_busy()
        {
            var connString = new NpgsqlConnectionStringBuilder(ConnectionString)
            {
                ApplicationName = nameof(ClearPool_with_busy)
            }.ToString();

            var conn = new NpgsqlConnection(connString);
            try
            {
                NpgsqlDataSource? pool;
                using (conn)
                {
                    conn.Open();
                    NpgsqlConnection.ClearPool(conn);
                    // conn is still busy but should get closed when returned to the pool

                    Assert.True(PoolManager.Pools.TryGetValue(connString, out pool));
                    AssertPoolState(pool, open: 1, idle: 0);
                }

                AssertPoolState(pool, open: 0, idle: 0);
            }
            finally
            {
                NpgsqlConnection.ClearPool(conn);
            }
        }

        [Fact]
        public void ClearPool_with_no_pool()
        {
            var connString = new NpgsqlConnectionStringBuilder(ConnectionString)
            {
                ApplicationName = nameof(ClearPool_with_no_pool)
            }.ToString();
            using var conn = new NpgsqlConnection(connString);
            NpgsqlConnection.ClearPool(conn);
        }
        */

        /*TODO: inject failure
        [Fact, Description("https://github.com/npgsql/npgsql/commit/45e33ecef21f75f51a625c7b919a50da3ed8e920#r28239653")]
        public void Open_physical_failure()
        {
            using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.Port = 44444;
                csb.MaxPoolSize = 1;
            });
            using var conn = dataSource.CreateConnection();
            for (var i = 0; i < 1; i++)
                Assert.That(() => conn.Open(), Throws.Exception
                    .TypeOf<NpgsqlException>()
                    .With.InnerException.TypeOf<SocketException>());
            AssertPoolState(dataSource, open: 0, idle: 0);
        }*/

        //[Test, Explicit]
        //[TestCase(10, 10, 30, true)]
        //[TestCase(10, 10, 30, false)]
        //[TestCase(10, 20, 30, true)]
        //[TestCase(10, 20, 30, false)]
        public static object[][] PoolExerciseCases = new object[][]
        {
            new object[] {10, 10, 30, false },
            new object[] {10, 10, 30, true },
            new object[] {10, 20, 30, false },
            new object[] {10, 20, 30, true }
        };

        [Theory]
        [MemberData(nameof(PoolExerciseCases))]
        public async Task Exercise_pool(int maxPoolSize, int numTasks, int seconds, bool async)
        {
            await using var dataSource = testBase.CreateDataSource(csb => csb.MaxPoolSize = maxPoolSize);

            Console.WriteLine($"Spinning up {numTasks} parallel tasks for {seconds} seconds (MaxPoolSize={maxPoolSize})...");
            StopFlag = 0;
            var tasks = Enumerable.Range(0, numTasks).Select(i => Task.Run(async () =>
            {
                while (StopFlag == 0)
                {
                    await using (var conn = dataSource.CreateConnection())
                    {
                        try
                        {
                            if (async)
                                await conn.OpenAsync();
                            else
                                conn.Open();
                            Thread.Sleep(200);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"caught error: {ex.ToString()}, pool stats: {dataSource.Statistics}");
                        }
                    }
                    
                }
            })).ToArray();

            Thread.Sleep(seconds * 1000);
            Interlocked.Exchange(ref StopFlag, 1);
            Console.WriteLine("Stopped. Waiting for all tasks to stop...");
            Task.WaitAll(tasks);
            Console.WriteLine("Done");
        }

        /* TODO: respect connection lifetime
        [Fact]
        public async Task ConnectionLifetime()
        {
            await using var dataSource = testBase.CreateDataSource(csb => csb.ConnectionLifetime = 1);
            await using var conn = await dataSource.OpenConnectionAsync();
            var processId = conn.ProcessID;
            await conn.CloseAsync();

            await Task.Delay(2000);

            await conn.OpenAsync();
            Assert.That(conn.ProcessID, Is.Not.EqualTo(processId));
        }
        */
        #region Support

        volatile int StopFlag;

        void AssertPoolState(SqlDataSource? pool, int expectedOpen, int expectedIdle)
        {
            ArgumentNullException.ThrowIfNull(pool, nameof(pool));

            var (openState, idleState, _) = pool.Statistics;
            Assert.Equal(expectedOpen, openState);
            Assert.Equal(expectedIdle, idleState);
        }
        #endregion Support
        /*
        // With MaxPoolSize=1, opens many connections in parallel and executes a simple SELECT. Since there's only one
        // physical connection, all operations will be completely serialized
        [Fact]
        public async Task OnePhysicalConnectionManyCommands()
        {
            const int numParallelCommands = 10000;

            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MaxPoolSize = 1;
                csb.MaxAutoPrepare = 5;
                csb.AutoPrepareMinUsages = 5;
                csb.Timeout = 0;
            });

            await Task.WhenAll(Enumerable.Range(0, numParallelCommands)
                .Select(async i =>
                {
                    await using var conn = await dataSource.OpenConnectionAsync();
                    await using var cmd = new NpgsqlCommand("SELECT " + i, conn);
                    var result = await cmd.ExecuteScalarAsync();
                    Assert.That(result, Is.EqualTo(i));
                }));
        }

        // When multiplexing, and the pool is totally saturated (at Max Pool Size and 0 idle connectors), we select
        // the connector with the least commands in flight and execute on it. We must never select a connector with
        // a pending transaction on it.
        // TODO: Test not tested
        [Fact]
        [Ignore("Multiplexing: fails")]
        public async Task MultiplexedCommandDoesntGetExecutedOnTransactionedConnector()
        {
            await using var dataSource = testBase.CreateDataSource(csb =>
            {
                csb.MaxPoolSize = 1;
                csb.Timeout = 1;
            });

            await using var connWithTx = await dataSource.OpenConnectionAsync();
            await using var tx = await connWithTx.BeginTransactionAsync();
            // connWithTx should now be bound with the only physical connector available.
            // Any commands execute should timeout

            await using var conn2 = await dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1", conn2);
            Assert.ThrowsAsync<NpgsqlException>(() => cmd.ExecuteScalarAsync());
        }

        //#endregion
        */
    }

}

#endif
