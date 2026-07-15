// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

// -----------------------------------------------------------------------------
// Tier-2 reproduction ATTEMPTS for ICM 775308542 / ADO.Net #43847 (harness-only,
// exploratory - not promoted to the driver test suite unless one reproduces).
//
// The shared SNIClose/MARS tests conclusively show that a single connection
// closed while one async I/O is in flight does NOT deadlock, even on the
// customer's exact in-box System.Data.dll (4.7.4081.0) at 4 CPUs. These variants
// target conditions those tests do not create, which better match the reported
// symptom ("initial TLS handshake failure" under a 4-proc production load):
//
//   1. ThreadPoolStarvation  - the worker pool is saturated (as on a small,
//      busy box) when the connection is closed, so an async completion callback
//      cannot obtain a thread while the close path runs.
//   2. SyncOverAsyncContext  - a single-threaded SynchronizationContext (as in
//      classic ASP.NET) is blocked on an async SqlClient operation via .Result;
//      if the driver captures the context for a continuation, it deadlocks.
//   3. CancelOpenDuringTls   - OpenAsync is cancelled while the TLS handshake
//      read is in flight, driving the driver's INTERNAL abort/close path
//      (rather than an external Close on another thread).
//   4. OpenUnderStarvation   - does client thread-pool starvation cause a
//      connection/handshake failure? Against a REAL server it does NOT: the
//      driver's Open() is robust. (An in-process test server shares the pool
//      and would be co-starved - a test artifact, not driver behavior.)
//
// All waits are bounded, so a genuine deadlock is reported as a failed
// bounded-wait rather than hanging the run.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Microsoft.Data.SqlClient.ManualTesting.Tests; // DataTestUtility shim (live connection string)
using Xunit;

namespace SniCloseLegacyRepro
{
    /// <summary>Shared helpers for the reproduction attempts.</summary>
    internal static class ReproSupport
    {
        public static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan HandshakeBudget = TimeSpan.FromSeconds(30);

        /// <summary>
        /// A query engine that signals when a SQL batch arrives and then withholds
        /// the response until released.
        /// </summary>
        public sealed class StallingQueryEngine : QueryEngine
        {
            private readonly ManualResetEventSlim _batchReceived;
            private readonly ManualResetEventSlim _release;

            public StallingQueryEngine(
                TdsServerArguments arguments,
                ManualResetEventSlim batchReceived,
                ManualResetEventSlim release)
                : base(arguments)
            {
                _batchReceived = batchReceived;
                _release = release;
            }

            protected override TDSMessageCollection CreateQueryResponse(
                ITDSServerSession session,
                TDSSQLBatchToken batchRequest)
            {
                _batchReceived.Set();
                _release.Wait();
                return base.CreateQueryResponse(session, batchRequest);
            }
        }

        /// <summary>
        /// A minimal, protocol-valid TDS PRELOGIN response advertising ENCRYPT_ON
        /// so a client that requested encryption proceeds into the TLS handshake.
        /// </summary>
        public static readonly byte[] PreLoginEncryptOnResponse =
        {
            0x12, 0x01, 0x00, 0x1A, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x0B, 0x00, 0x06,
            0x01, 0x00, 0x11, 0x00, 0x01,
            0xFF,
            0x11, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01,
        };
    }

    /// <summary>
    /// A single-threaded synchronization context that pumps posted callbacks on
    /// one dedicated (background) thread - the shape that makes classic ASP.NET
    /// sync-over-async deadlock.
    /// </summary>
    internal sealed class SingleThreadedSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback callback, object? state)> _queue = new();
        private readonly Thread _thread;

        public SingleThreadedSynchronizationContext()
        {
            _thread = new Thread(Pump) { IsBackground = true, Name = "SingleThreadedSyncCtx" };
            _thread.Start();
        }

        private void Pump()
        {
            SetSynchronizationContext(this);
            try
            {
                foreach ((SendOrPostCallback callback, object? state) in _queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            }
            catch (InvalidOperationException)
            {
                // Queue completed while blocked; normal shutdown.
            }
        }

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException();

        /// <summary>Queue an action to run on the context thread.</summary>
        public void Run(Action action) => Post(_ => action(), null);

        public void Dispose()
        {
            try
            {
                _queue.CompleteAdding();
            }
            catch
            {
                // Ignore.
            }
        }
    }

    /// <summary>
    /// Attempt 1: close a connection with an in-flight async read while the
    /// worker thread pool is fully saturated, so an async completion callback
    /// cannot obtain a thread while the close path runs.
    /// </summary>
    public class ThreadPoolStarvationReproTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CloseWithPendingReadUnderThreadPoolStarvation_DoesNotDeadlock(bool disposeInsteadOfClose)
        {
            ThreadPool.GetMinThreads(out int minW, out int minIo);
            ThreadPool.GetMaxThreads(out int maxW, out int maxIo);

            using ManualResetEventSlim releaseWorkers = new(false);
            using ManualResetEventSlim batchReceived = new(false);
            using ManualResetEventSlim releaseResponse = new(false);
            int busy = 0;
            bool poolConstrained = false;
            try
            {
                TdsServerArguments arguments = new();
                using TdsServer server = new(
                    new ReproSupport.StallingQueryEngine(arguments, batchReceived, releaseResponse),
                    arguments);
                server.Start();

                SqlConnectionStringBuilder builder = new()
                {
                    DataSource = $"localhost,{server.EndPoint.Port}",
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    Pooling = false,
#if NETFRAMEWORK
                    TransparentNetworkIPResolution = false,
#endif
                };

                SqlConnection connection = new(builder.ConnectionString);
                SqlCommand? command = null;
                Task<SqlDataReader>? readTask = null;
                bool closeAttempted = false;
                bool closedInTime = false;
                try
                {
                    // Open and start the read while the pool is healthy, so the
                    // starvation below isolates the CLOSE path (not connection
                    // establishment, which is starvation-sensitive on its own).
                    connection.Open();
                    command = new("SELECT 1", connection);
                    readTask = command.ExecuteReaderAsync();

                    Assert.True(
                        batchReceived.Wait(ReproSupport.HandshakeBudget),
                        "The server never received the batch; the async read was not in flight.");

                    // Now saturate a small, busy-box worker pool so any async
                    // completion the close path depends on cannot obtain a thread.
                    int poolSize = Math.Max(2, Math.Min(4, Environment.ProcessorCount));
                    ThreadPool.SetMinThreads(poolSize, poolSize);
                    ThreadPool.SetMaxThreads(poolSize, poolSize);
                    poolConstrained = true;
                    for (int i = 0; i < poolSize; i++)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            Interlocked.Increment(ref busy);
                            releaseWorkers.Wait();
                        });
                    }
                    SpinWait.SpinUntil(() => Volatile.Read(ref busy) >= poolSize, TimeSpan.FromSeconds(10));

                    closeAttempted = true;
                    Task closeTask = Task.Factory.StartNew(
                        () =>
                        {
                            if (disposeInsteadOfClose)
                            {
                                connection.Dispose();
                            }
                            else
                            {
                                connection.Close();
                            }
                        },
                        TaskCreationOptions.LongRunning);

                    closedInTime = closeTask.Wait(ReproSupport.CloseBudget);

                    Assert.True(
                        closedInTime,
                        $"{(disposeInsteadOfClose ? "Dispose()" : "Close()")} did not complete within " +
                        $"{ReproSupport.CloseBudget.TotalSeconds:N0}s while the thread pool was saturated " +
                        "(possible SNIClose deadlock / thread-starvation).");
                }
                finally
                {
                    // Free the pool first so the pending read and any close-path
                    // continuations can drain, then release the server.
                    releaseWorkers.Set();
                    if (poolConstrained)
                    {
                        ThreadPool.SetMinThreads(minW, minIo);
                        ThreadPool.SetMaxThreads(maxW, maxIo);
                    }
                    releaseResponse.Set();

                    if (readTask != null)
                    {
                        try { readTask.Wait(ReproSupport.CloseBudget); } catch { /* expected */ }
                    }
                    command?.Dispose();
                    if (!closeAttempted || closedInTime)
                    {
                        connection.Dispose();
                    }
                }
            }
            finally
            {
                releaseWorkers.Set();
                ThreadPool.SetMinThreads(minW, minIo);
                ThreadPool.SetMaxThreads(maxW, maxIo);
            }
        }
    }

    /// <summary>
    /// Attempt 2: block a single-threaded synchronization context on an async
    /// SqlClient read via <c>.Result</c>. If any driver continuation captures the
    /// context, the post-back cannot run and the operation deadlocks.
    /// </summary>
    public class SyncOverAsyncContextReproTests
    {
        [Fact]
        public void SyncOverAsyncOnSingleThreadedContext_DoesNotDeadlock()
        {
            using ManualResetEventSlim batchReceived = new(false);
            using ManualResetEventSlim releaseResponse = new(false);
            using ManualResetEventSlim scenarioDone = new(false);
            Exception? scenarioError = null;

            TdsServerArguments arguments = new();
            using TdsServer server = new(
                new ReproSupport.StallingQueryEngine(arguments, batchReceived, releaseResponse),
                arguments);
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            };

            // Once the read is in flight, release the server so its completion
            // fires and the continuation must post back to the (blocked) context.
            Task releaser = Task.Run(() =>
            {
                if (batchReceived.Wait(ReproSupport.HandshakeBudget))
                {
                    releaseResponse.Set();
                }
            });

            using SingleThreadedSynchronizationContext context = new();
            SqlConnection? connection = null;
            context.Run(() =>
            {
                try
                {
                    connection = new SqlConnection(builder.ConnectionString);
                    connection.Open();
                    using SqlCommand command = new("SELECT 1", connection);

                    // Sync-over-async on the single-threaded context: blocks the
                    // pump thread. Deadlocks iff a continuation captured the context.
                    using SqlDataReader reader = command.ExecuteReaderAsync().GetAwaiter().GetResult();
                    while (reader.Read())
                    {
                        // Drain.
                    }
                }
                catch (Exception ex)
                {
                    scenarioError = ex;
                }
                finally
                {
                    scenarioDone.Set();
                }
            });

            bool completed = scenarioDone.Wait(ReproSupport.CloseBudget);

            // Best-effort cleanup; if we deadlocked, the context thread is parked
            // (background) and the connection is stuck - do not block on it.
            try { releaser.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            if (completed)
            {
                try { connection?.Dispose(); } catch { /* ignore */ }
            }

            Assert.True(
                completed,
                $"Sync-over-async on a single-threaded SynchronizationContext did not complete within " +
                $"{ReproSupport.CloseBudget.TotalSeconds:N0}s. This indicates a captured-context deadlock in " +
                "the driver's async read path (ADO.Net #43847 / ICM 775308542).");

            // If it completed, it should have completed cleanly (or with an
            // expected SqlException), not via some other failure mode.
            Assert.True(
                scenarioError is null or System.Data.SqlClient.SqlException,
                $"Unexpected error on the context thread: {scenarioError}");
        }
    }

    /// <summary>
    /// Attempt 3: cancel <c>OpenAsync</c> while the TLS handshake read is in
    /// flight, exercising the driver's INTERNAL abort/close path concurrently
    /// with the handshake I/O (rather than an external Close on another thread).
    /// </summary>
    public class HandshakeCancellationReproTests
    {
        [Fact]
        public void CancelOpenAsyncDuringTlsHandshake_DoesNotDeadlock()
        {
            using ManualResetEventSlim handshakeInFlight = new(false);
            using ManualResetEventSlim releaseServer = new(false);

            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task serverTask = Task.Run(() =>
            {
                using TcpClient acceptedClient = listener.AcceptTcpClient();
                using NetworkStream stream = acceptedClient.GetStream();
                byte[] buffer = new byte[4096];
                try
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        return;
                    }

                    stream.Write(ReproSupport.PreLoginEncryptOnResponse, 0, ReproSupport.PreLoginEncryptOnResponse.Length);
                    stream.Flush();

                    // Read the first byte of the client's TLS ClientHello, then
                    // withhold the ServerHello so the handshake read stays pending.
                    if (stream.ReadByte() < 0)
                    {
                        return;
                    }

                    handshakeInFlight.Set();
                    releaseServer.Wait();
                }
                catch
                {
                    // Client may tear down mid-handshake; ignore.
                }
            });

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"127.0.0.1,{port}",
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = true,
                ConnectTimeout = 60,
                ConnectRetryCount = 0,
                Pooling = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            };

            SqlConnection connection = new(builder.ConnectionString);
            using CancellationTokenSource cts = new();
            Task? openTask = null;
            bool cancelAttempted = false;
            bool settledInTime = false;
            try
            {
                openTask = connection.OpenAsync(cts.Token);

                Assert.True(
                    handshakeInFlight.Wait(ReproSupport.HandshakeBudget),
                    "The client never reached the TLS handshake; no read was in flight to cancel.");

                // Cancel while the handshake read is pending: this drives the
                // driver's internal abort/close path against the in-flight I/O.
                cancelAttempted = true;
                cts.Cancel();

                try
                {
                    settledInTime = openTask.Wait(ReproSupport.CloseBudget);
                }
                catch (AggregateException)
                {
                    // OpenAsync faulting/cancelling is the expected outcome; what
                    // matters is that it *settled* rather than hanging.
                    settledInTime = true;
                }

                Assert.True(
                    settledInTime,
                    $"OpenAsync did not settle within {ReproSupport.CloseBudget.TotalSeconds:N0}s after " +
                    "cancellation while a TLS handshake read was in flight (possible SNIClose deadlock).");
            }
            finally
            {
                releaseServer.Set();
                listener.Stop();

                if (openTask != null)
                {
                    try { openTask.Wait(ReproSupport.CloseBudget); } catch { /* expected fault/cancel */ }
                }

                if (!cancelAttempted || settledInTime)
                {
                    connection.Dispose();
                }

                try { serverTask.Wait(ReproSupport.CloseBudget); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>
    /// Attempt 4: does client-side thread-pool starvation cause a CONNECTION
    /// failure? This checks the hypothesis that the reported "connection/
    /// handshake failure under load" is thread starvation rather than an
    /// SNIClose deadlock.
    ///
    /// <para>
    /// IMPORTANT: an in-process TDS test server shares the client's thread pool,
    /// so saturating the pool starves the SERVER and makes Open() time out - a
    /// test artifact, not driver behavior. These tests therefore require a real,
    /// out-of-process SQL Server (SNICLOSE_CONNSTR). The observed result is that
    /// Open() SUCCEEDS under client thread-pool starvation, i.e. starvation is
    /// NOT the reproduction.
    /// </para>
    /// </summary>
    public class OpenUnderThreadPoolStarvationReproTests
    {
        private static readonly TimeSpan HardBudget = TimeSpan.FromSeconds(60);

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)] // Encrypt = Optional
        [InlineData(true)]  // Encrypt = Mandatory (drives the TLS handshake)
        public void SyncOpenAgainstRealServerUnderThreadPoolStarvation_Succeeds(bool encrypt)
        {
            string cs = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Encrypt = encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
                TrustServerCertificate = true,
                Pooling = false,
                ConnectTimeout = 30,
            }.ConnectionString;

            RunStarvedOpen(cs, out bool settled, out bool timedOut, out Exception? error);

            Assert.True(
                settled,
                $"Open() did not settle within {HardBudget.TotalSeconds:N0}s under thread-pool starvation " +
                "(it hung).");
            Assert.True(
                error is null,
                $"Open() against a real server failed under client thread-pool starvation: {error}");
            Assert.False(
                timedOut,
                "Open() timed out under client thread-pool starvation against a real server.");
        }

        private static void RunStarvedOpen(string connectionString, out bool settled, out bool timedOut, out Exception? error)
        {
            ThreadPool.GetMinThreads(out int minW, out int minIo);
            ThreadPool.GetMaxThreads(out int maxW, out int maxIo);
            // Not disposed: saturating workers may still be parked on it when the
            // method returns; disposing would race them into ObjectDisposedException.
            ManualResetEventSlim releaseWorkers = new(false);
            int busy = 0;
            settled = false;
            timedOut = false;
            error = null;
            try
            {
                // Constrain the pool to a small, busy-box size and occupy every
                // worker thread so any continuation the driver needs is starved.
                int poolSize = Math.Max(2, Math.Min(4, Environment.ProcessorCount));
                ThreadPool.SetMinThreads(poolSize, poolSize);
                ThreadPool.SetMaxThreads(poolSize, poolSize);
                for (int i = 0; i < poolSize; i++)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Interlocked.Increment(ref busy);
                        releaseWorkers.Wait();
                    });
                }
                SpinWait.SpinUntil(() => Volatile.Read(ref busy) >= poolSize, TimeSpan.FromSeconds(10));

                Exception? captured = null;
                Thread openThread = new(() =>
                {
                    try
                    {
                        using SqlConnection conn = new(connectionString);
                        conn.Open();
                        conn.Close();
                    }
                    catch (Exception ex)
                    {
                        captured = ex;
                    }
                })
                { IsBackground = true, Name = "StarvedOpen" };

                openThread.Start();
                settled = openThread.Join(HardBudget);
                error = captured;
                timedOut = captured is System.Data.SqlClient.SqlException sqlEx
                    && (sqlEx.Number == -2
                        || sqlEx.Message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        || sqlEx.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            finally
            {
                releaseWorkers.Set();
                ThreadPool.SetMinThreads(minW, minIo);
                ThreadPool.SetMaxThreads(maxW, maxIo);
            }
        }
    }
}
