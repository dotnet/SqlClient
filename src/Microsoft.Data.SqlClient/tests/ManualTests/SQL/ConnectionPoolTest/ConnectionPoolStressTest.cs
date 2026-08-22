// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Connection pool stress test to validate pool behavior under various concurrent load scenarios.
    /// </summary>
    [Trait("Set", "3")]
    public class ConnectionPoolStressTest
    {
        #region Properties

        /// <summary>
        /// Connection string
        /// </summary>
        internal string? ConnectionString { get; set; }

        /// <summary>
        /// Maximum number of connections in the pool
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// SQL WAITFOR DELAY value for simulating slow queries
        /// </summary>
        public string WaitForDelay { get; set; } = "00:00:00.100";

        /// <summary>
        /// Number of concurrent connections to create
        /// </summary>
        public int ConcurrentConnections { get; set; } = 10;

        /// <summary>
        /// Number of operations each thread should perform
        /// </summary>
        public int OperationsPerThread { get; set; } = 10;

        #endregion

        #region Connection Dooming

        // Reflection fields for accessing internal connection properties
        private readonly FieldInfo? _internalConnectionField;

        public ConnectionPoolStressTest()
        {
            try
            {
                // Cache reflection info for Microsoft.Data.SqlClient
                Type msDataConnectionType = typeof(SqlConnection);
                _internalConnectionField = msDataConnectionType.GetField("_innerConnection", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to initialize reflection for connection dooming: {ex.Message}");
            }
        }

        /// <summary>
        /// Dooms a Microsoft.Data.SqlClient connection by calling DoomThisConnection on its internal connection
        /// </summary>
        private bool DoomMicrosoftDataConnection(SqlConnection connection)
        {
            try
            {
                if(_internalConnectionField == null)
                {
                    // Fail the test if reflection setup failed
                    return false;
                }

                if (_internalConnectionField.GetValue(connection) is object internalConnection)
                {
                    MethodInfo? doomMethod = internalConnection.GetType().GetMethod("DoomThisConnection", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (doomMethod != null)
                    {
                        doomMethod.Invoke(internalConnection, null);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Sets up connection string
        /// </summary>
        /// <param name="connectionString">Connection string to be set.</param>
        internal void SetConnectionString(string connectionString)
        {
            var connectionSB = new SqlConnectionStringBuilder(connectionString)
            {
                // Min size needs to be larger than the number of concurrent connections to trigger the pool exhaustion as it will make it more likely that PoolCreateRequest will run.
                MinPoolSize = Math.Min(20, MaxPoolSize / 5), // Dynamic min pool size
                MaxPoolSize = MaxPoolSize,
                Pooling = true, // Explicitly enable pooling
                TrustServerCertificate = true
            };

            ConnectionString = connectionSB.ConnectionString;

            // Ensure adequate thread pool capacity
            ThreadPool.SetMaxThreads(Math.Max(ConcurrentConnections * 2, 100), 100);
        }

        #endregion

        #region Stress Test Methods

        /// <summary>
        /// Runs a synchronous stress test using Microsoft.Data.SqlClient with connection dooming
        /// </summary>
        internal void ConnectionPoolStress_MsData_Sync()
        {
            if (ConnectionString == null)
            {
                throw new InvalidOperationException("ConnectionString is not set. Call SetConnectionString() before running the test.");
            }

            RunStressTest(
                connectionString: ConnectionString,
                doomAction: conn => DoomMicrosoftDataConnection((SqlConnection)conn),
                async: false
            );
        }

        /// <summary>
        /// Runs asynchronous stress test using Microsoft.Data.SqlClient with connection dooming
        /// </summary>
        internal void ConnectionPoolStress_MsData_Async()
        {
            if (ConnectionString == null)
            {
                throw new InvalidOperationException("ConnectionString is not set. Call SetConnectionString() before running the test.");
            }

            RunStressTest(
                connectionString: ConnectionString,
                doomAction: conn => DoomMicrosoftDataConnection((SqlConnection)conn),
                async: true
            );
        }

        /// <summary>
        /// Generic stress test method that works with both SQL client libraries using DbConnection/DbCommand
        /// </summary>
        private void RunStressTest(
            string connectionString,
            Func<DbConnection, bool> doomAction,
            bool async = false)
        {
            var workers = new Task[ConcurrentConnections];
            using Barrier barrier = new(ConcurrentConnections);

            var command = string.IsNullOrWhiteSpace(WaitForDelay)
                ? "SELECT GETDATE()"
                : $"WAITFOR DELAY '{WaitForDelay}'; SELECT GETDATE()";

            // Create regular threads (don't doom connections)
            for (int i = 0; i < ConcurrentConnections - 1; i++)
            {
                workers[i] = CreateWorkerTask(
                    connectionString, command, barrier, doomConnections: false, async);
            }

            // Create special thread that dooms connections (if we have multiple threads)
            if (ConcurrentConnections > 1)
            {
                workers[ConcurrentConnections - 1] = CreateWorkerTask(
                    connectionString, command, barrier, doomConnections: true, async, doomAction);
            }

            Task.WhenAll(workers).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a worker task that performs database operations using DbConnection/DbCommand.
        /// </summary>
        private Task CreateWorkerTask(
            string connectionString,
            string command,
            Barrier barrier,
            bool doomConnections,
            bool async,
            Func<DbConnection, bool>? doomAction = null)
        {
            return Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        barrier.SignalAndWait(); // Initial synchronization - all threads start together

                        for (int j = 0; j < OperationsPerThread; j++)
                        {
                            if (doomConnections && doomAction != null)
                            {
                                // Dooming thread - barriers inside using block to doom before disposal
                                using var conn = new SqlConnection(connectionString);
                                if (async)
                                {
                                    await conn.OpenAsync();
                                }
                                else
                                {
                                    conn.Open();
                                }

                                await ExecuteCommand(command, async, conn);

                                // Synchronize after command execution, before dooming
                                barrier.SignalAndWait();

                                // Doom connection before it gets disposed/returned to pool
                                if (!doomAction(conn))
                                {
                                    throw new Exception("Unable to doom connection");
                                }

                                // Synchronize after dooming - ensures all threads see the effect
                                barrier.SignalAndWait();
                            }
                            else
                            {
                                // Non-dooming threads - barriers after connection is closed
                                using (var conn = new SqlConnection(connectionString))
                                {
                                    if (async)
                                    {
                                        await conn.OpenAsync();
                                    }
                                    else
                                    {
                                        conn.Open();
                                    }

                                    await ExecuteCommand(command, async, conn);

                                } // Connection is closed/returned to pool here

                                // Synchronize after connection is closed
                                barrier.SignalAndWait();

                                // Sync for coordination with dooming thread
                                barrier.SignalAndWait();
                            }
                        }
                    }
                    catch
                    {
                        barrier.RemoveParticipant();
                        throw;
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// Executes a database command with proper error handling
        /// </summary>
        private static async Task ExecuteCommand(string command, bool async, SqlConnection conn)
        {
            try
            {
                using var cmd = new SqlCommand(command, conn);
                if (async)
                {
                    await cmd.ExecuteScalarAsync();
                }
                else
                {
                    cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command execution failed: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private static bool RunSingleStressTest(Action testAction)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                testAction();
                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Stress test failed: {ex}");
                return false;
            }

            return true;
        }

        private static async Task<bool> TestConnectionPoolExhaustion(string connectionString, int maxPoolSize, bool async)
        {
            var connections = new List<SqlConnection>();

            try
            {
                for (int i = 0; i < maxPoolSize; i++)
                {
                    SqlConnection conn = new(connectionString);
                    if (async)
                    {
                        await conn.OpenAsync();
                    }
                    else
                    {
                        conn.Open();
                    }
                    connections.Add(conn);
                }
                Assert.Equal(maxPoolSize, connections.Count);
            }
            catch
            {
                return false;
            }
            finally
            {
                // Clean up all connections
                foreach (SqlConnection conn in connections)
                {
                    conn?.Dispose();
                }
            }

            return true;
        }

        #endregion

        #region Pool Exhaustion Tests
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [TestCategory("LongRunning")] // Takes around 13 seconds.
        public async Task ConnectionPoolStress_Sync()
        {
            var test = new ConnectionPoolStressTest
            {
                MaxPoolSize = 100,
                ConcurrentConnections = 10,
                WaitForDelay = "00:00:00.100",
                OperationsPerThread = 100,
            };

            test.SetConnectionString(DataTestUtility.TCPConnectionString);

            // Run the stress tests
            if (!RunSingleStressTest(test.ConnectionPoolStress_MsData_Sync))
            {
                // fail the test
                Assert.Fail("ConnectionPoolStress_MsData_Sync failed");
            }

            if (!await TestConnectionPoolExhaustion(test.ConnectionString!, test.MaxPoolSize, false))
            {
                // fail the test
                Assert.Fail("ConnectionPoolStress_MsData_Sync failed");
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [TestCategory("LongRunning")] // Takes around 11 seconds.
        public async Task ConnectionPoolStress_Async()
        {
            var test = new ConnectionPoolStressTest
            {
                MaxPoolSize = 100,
                ConcurrentConnections = 10,
                WaitForDelay = "00:00:00.100",
                OperationsPerThread = 100,
            };

            test.SetConnectionString(DataTestUtility.TCPConnectionString);

            // Test Microsoft.Data.SqlClient Async
            if (!RunSingleStressTest(test.ConnectionPoolStress_MsData_Async))
            {
                // fail the test
                Assert.Fail("ConnectionPoolStress_MsData_Async failed");
            }

            // Test connection pool exhaustion (async)
            if (!await TestConnectionPoolExhaustion(test.ConnectionString!, test.MaxPoolSize, true))
            {
                // fail the test
                Assert.Fail("ConnectionPoolStress_MsData_Async failed");
            }
        }

        #endregion
    }
}
