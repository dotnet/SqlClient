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
//      (rather than an external Close on another thread). PROMOTED to the driver
//      test suite as SNICloseHandshakeCancellationTest (linked in via the
//      harness csproj), so it also runs against the legacy driver here.
//   4. OpenUnderStarvation   - does client thread-pool starvation cause a
//      connection/handshake failure? Against a REAL server it does NOT: the
//      driver's Open() is robust. (An in-process test server shares the pool
//      and would be co-starved - a test artifact, not driver behavior.)
//
// After the ICM dump was obtained, the real mechanism was identified: a
// RE-ENTRANT SNIClose (Close() called synchronously from within
// ReadAsyncCallback via the command-timeout -> failed-attention-write path, so
// SNIClose spins in WaitForActiveCallbacks waiting for the callback it runs in).
// Two further attempts target that mechanism:
//
//   5. CallbackReentrancy    - async read + short command timeout + a proxy that
//      breaks the client's write. A regression guard for the MDS asyncClose fix;
//      does not deterministically force the legacy re-entrancy (see its notes).
//   6. ReentrantSNICloseSoak - many concurrent workers RST connections at ~the
//      command-timeout instant, hunting the race probabilistically. OPT-IN
//      (set SNICLOSE_SOAK_ENABLE) because it is heavy and, when it fires, poisons
//      the process. It fired ONCE in a full-suite run on the legacy in-box driver
//      (net462/47x/48x) but NOT on the Core NuGet lineage (net10.0) - a clean
//      driver-lineage split consistent with the real bug - yet it did not recur
//      in subsequent runs. So it confirms the mechanism is reachable but is not a
//      reliable reproduction (matching the SQL EE's "very difficult to capture").
//      The definitive evidence remains the dump + the asyncClose code review.
//
// All waits are bounded, so a genuine deadlock is reported as a failed
// bounded-wait rather than hanging the run.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
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

    /// <summary>
    /// A transparent TCP proxy that forwards bytes between a client and a backend
    /// TDS server, and can - on demand - break the CLIENT's write path while
    /// leaving its pending read outstanding. It does this via
    /// <c>Shutdown(SocketShutdown.Receive)</c> on the client-facing socket: the
    /// client's next write (the timeout attention) then arrives at a
    /// receive-shutdown socket and is answered with a TCP RST, so the client's
    /// <c>SNIWritePacket</c> fails - the exact condition from the ICM dump.
    /// </summary>
    internal sealed class AttentionBreakingProxy : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly IPEndPoint _backend;
        private Socket? _clientSocket;
        private Socket? _backendSocket;
        private volatile bool _broken;

        public int Port { get; }

        public AttentionBreakingProxy(IPEndPoint backend)
        {
            _backend = backend;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Task.Run(AcceptAndPump);
        }

        /// <summary>
        /// Break the client's write direction: subsequent client writes RST while
        /// its pending read stays outstanding (the backend sends nothing).
        /// </summary>
        public void BreakClientWrites()
        {
            _broken = true;
            try
            {
                // SD_RECEIVE: data the client sends afterwards (the attention) is
                // answered with a TCP RST, failing the client's write, while the
                // client's pending read remains outstanding.
                _clientSocket?.Shutdown(SocketShutdown.Receive);
            }
            catch
            {
                // best effort
            }
        }

        private void AcceptAndPump()
        {
            try
            {
                _clientSocket = _listener.AcceptSocket();
                _backendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _backendSocket.Connect(_backend);

                // client -> backend on a worker; backend -> client on this thread.
                Task.Run(() => Pump(_clientSocket, _backendSocket, clientToBackend: true));
                Pump(_backendSocket, _clientSocket, clientToBackend: false);
            }
            catch
            {
                // Teardown races are expected; ignore.
            }
        }

        private void Pump(Socket from, Socket to, bool clientToBackend)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (true)
                {
                    int n = from.Receive(buffer);
                    if (n <= 0)
                    {
                        break;
                    }

                    // Once broken, stop forwarding client bytes to the backend; the
                    // client's write will RST on its receive-shutdown socket.
                    if (clientToBackend && _broken)
                    {
                        break;
                    }

                    to.Send(buffer, 0, n, SocketFlags.None);
                }
            }
            catch
            {
                // Expected on RST / teardown.
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { /* ignore */ }
            try { _clientSocket?.Dispose(); } catch { /* ignore */ }
            try { _backendSocket?.Dispose(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Attempt 5 (matches the ICM dump's mechanism): the reported deadlock is a
    /// RE-ENTRANT SNIClose. An async read times out; the driver sends an
    /// attention from within <c>ReadAsyncCallback</c>; the attention
    /// <c>SNIWritePacket</c> fails; the legacy driver then calls
    /// <c>SqlConnection.Close()</c> SYNCHRONOUSLY on the callback thread, so
    /// <c>SNIClose</c> spins in <c>SNI_Conn::WaitForActiveCallbacks()</c> waiting
    /// for the very callback it is running within - a self-deadlock.
    ///
    /// <para>
    /// Microsoft.Data.SqlClient defers the close off the callback thread via its
    /// <c>asyncClose</c> path (ThrowExceptionAndWarning wraps the close in
    /// <c>Task.Factory.StartNew</c>), so the read settles promptly and this test
    /// passes - a regression guard for that fix.
    /// </para>
    ///
    /// <para>
    /// NOTE: this exercises the async-read-timeout + broken-write path and asserts
    /// no deadlock, but it does NOT deterministically force the re-entrant close
    /// on the legacy driver: that requires the exact TCP state from the dump
    /// (the attention write fails while the read stays pending - a half-broken
    /// connection), which is very hard to synthesize (a RST kills both
    /// directions; the SQL EE analysis likewise noted it is "very difficult to
    /// capture"). The definitive evidence is the dump plus the code review of the
    /// <c>asyncClose</c> deferral in Microsoft.Data.SqlClient.
    /// </para>
    /// </summary>
    public class SNICloseCallbackReentrancyReproTests
    {
        private static readonly TimeSpan SettleBudget = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan BatchBudget = TimeSpan.FromSeconds(30);

        [Fact]
        public void CloseFromReadCallbackOnAttentionFailure_DoesNotDeadlock()
        {
            using ManualResetEventSlim batchReceived = new(false);
            using ManualResetEventSlim releaseResponse = new(false);

            TdsServerArguments arguments = new();
            using TdsServer server = new(
                new ReproSupport.StallingQueryEngine(arguments, batchReceived, releaseResponse),
                arguments);
            server.Start();

            using AttentionBreakingProxy proxy = new(new IPEndPoint(IPAddress.Loopback, server.EndPoint.Port));

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"127.0.0.1,{proxy.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                // MARS off and pooling off, matching the reported configuration and
                // ensuring Close() tears down the physical connection (reaching SNIClose).
                Pooling = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            };

            SqlConnection connection = new(builder.ConnectionString);
            SqlCommand? command = null;
            Task<SqlDataReader>? readTask = null;
            bool settledInTime = false;
            try
            {
                connection.Open();

                // Short command timeout so the pending async read times out, which
                // drives OnTimeoutCore -> SendAttention from within ReadAsyncCallback.
                command = new("SELECT 1", connection) { CommandTimeout = 2 };
                readTask = command.ExecuteReaderAsync();

                Assert.True(
                    batchReceived.Wait(BatchBudget),
                    "The server never received the batch; the async read was not in flight.");

                // Break the client's write path so the timeout attention's
                // SNIWritePacket fails, forcing Close() from within ReadAsyncCallback.
                proxy.BreakClientWrites();

                try
                {
                    settledInTime = readTask.Wait(SettleBudget);
                }
                catch (AggregateException)
                {
                    // Faulting/cancelling is the healthy outcome; what matters is it settled.
                    settledInTime = true;
                }

                Assert.True(
                    settledInTime,
                    $"ExecuteReaderAsync did not settle within {SettleBudget.TotalSeconds:N0}s after the " +
                    "command-timeout attention write failed. This indicates the re-entrant SNIClose " +
                    "deadlock: Close() called from within ReadAsyncCallback -> SNIClose -> " +
                    "WaitForActiveCallbacks (ADO.Net #43847 / ICM 775308542).");
            }
            finally
            {
                releaseResponse.Set();
                command?.Dispose();
                // Only dispose the connection if we did NOT deadlock; a deadlocked
                // connection's Dispose() would also hang in SNIClose.
                if (settledInTime)
                {
                    try { connection.Dispose(); } catch { /* ignore */ }
                }
            }
        }
    }

    /// <summary>
    /// A transparent TCP proxy that relays bytes between a client and a backend
    /// TDS server and can hard-RST the client connection on demand
    /// (LingerState 0 + Close), used by the soak to break the connection at a
    /// timed offset relative to the async read's command timeout.
    /// </summary>
    internal sealed class RstProxy : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly IPEndPoint _backend;
        private Socket? _clientSocket;
        private Socket? _backendSocket;

        public int Port { get; }

        public RstProxy(IPEndPoint backend)
        {
            _backend = backend;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Task.Run(AcceptAndPump);
        }

        /// <summary>Hard-reset the client connection (sends a TCP RST).</summary>
        public void Rst()
        {
            try
            {
                Socket? s = _clientSocket;
                if (s != null)
                {
                    s.LingerState = new LingerOption(true, 0);
                    s.Close();
                }
            }
            catch
            {
                // best effort
            }
        }

        private void AcceptAndPump()
        {
            try
            {
                _clientSocket = _listener.AcceptSocket();
                _backendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _backendSocket.Connect(_backend);
                Task.Run(() => Pump(_clientSocket, _backendSocket));
                Pump(_backendSocket, _clientSocket);
            }
            catch
            {
                // Teardown races are expected; ignore.
            }
        }

        private static void Pump(Socket from, Socket to)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (true)
                {
                    int n = from.Receive(buffer);
                    if (n <= 0)
                    {
                        break;
                    }
                    to.Send(buffer, 0, n, SocketFlags.None);
                }
            }
            catch
            {
                // Expected on RST / teardown.
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { /* ignore */ }
            try { _clientSocket?.Dispose(); } catch { /* ignore */ }
            try { _backendSocket?.Dispose(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Soak that hunts for the re-entrant SNIClose deadlock probabilistically.
    /// Many concurrent workers repeatedly open a connection, start an async read
    /// that the server stalls, and RST the connection at ~the command-timeout
    /// instant (with jitter), trying to land in the tiny window where the SNI
    /// read timeout has fired but the timeout attention has not yet been written -
    /// so the attention <c>SNIWritePacket</c> fails and the legacy driver closes
    /// synchronously from within <c>ReadAsyncCallback</c>. A hang (a read that
    /// never settles) means the re-entrant <c>SNIClose</c> was hit.
    ///
    /// <para>
    /// Tunable via environment variables:
    ///   SNICLOSE_SOAK_WORKERS (default 16; the ICM saw a "~36-thread" condition),
    ///   SNICLOSE_SOAK_SECONDS (default 60).
    /// The test fails if it reproduces (green = did not reproduce this run).
    /// </para>
    /// </summary>
    public class ReentrantSNICloseSoakTests
    {
        private sealed class BoundedStallQueryEngine : QueryEngine
        {
            private readonly int _stallMs;

            public BoundedStallQueryEngine(TdsServerArguments arguments, int stallMs)
                : base(arguments)
            {
                _stallMs = stallMs;
            }

            protected override TDSMessageCollection CreateQueryResponse(
                ITDSServerSession session,
                TDSSQLBatchToken batchRequest)
            {
                // Stall every query long enough that the client's short command
                // timeout fires first; the (eventual) response goes to an already
                // RST-torn-down connection and the handler thread unwinds.
                Thread.Sleep(_stallMs);
                return base.CreateQueryResponse(session, batchRequest);
            }
        }

        private static int EnvInt(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;

        /// <summary>
        /// Gate: the soak is opt-in (set SNICLOSE_SOAK_ENABLE) because it is
        /// heavy, rarely fires, and - when it DOES reproduce - poisons the
        /// process (hung SNIClose threads cascade into later tests). Skipped by
        /// default so normal harness runs stay clean and deterministic.
        /// </summary>
        public static bool SoakEnabled() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNICLOSE_SOAK_ENABLE"));

        [ConditionalFact(typeof(ReentrantSNICloseSoakTests), nameof(SoakEnabled))]
        public void ConcurrentTimeoutWithRstNearTimeout_DoesNotReentrantlyDeadlock()
        {
            int workers = EnvInt("SNICLOSE_SOAK_WORKERS", 16);
            TimeSpan soak = TimeSpan.FromSeconds(EnvInt("SNICLOSE_SOAK_SECONDS", 60));
            const int commandTimeoutSec = 1;
            const int stallMs = 6000;
            TimeSpan perOpBudget = TimeSpan.FromSeconds(20);

            TdsServerArguments arguments = new();
            using TdsServer server = new(new BoundedStallQueryEngine(arguments, stallMs), arguments);
            server.Start();
            IPEndPoint backend = new(IPAddress.Loopback, server.EndPoint.Port);

            using CancellationTokenSource cts = new(soak);
            long attempts = 0;
            string? hangInfo = null;
            object gate = new();

            void Worker(int id)
            {
                Random rng = new(unchecked(id * 397 ^ Environment.TickCount));
                while (!cts.IsCancellationRequested && Volatile.Read(ref hangInfo) is null)
                {
                    RstProxy? proxy = null;
                    SqlConnection? conn = null;
                    SqlCommand? cmd = null;
                    Task<SqlDataReader>? readTask = null;
                    bool settled = false;
                    try
                    {
                        proxy = new RstProxy(backend);
                        string cs = new SqlConnectionStringBuilder
                        {
                            DataSource = $"127.0.0.1,{proxy.Port}",
                            Encrypt = SqlConnectionEncryptOption.Optional,
                            Pooling = false,
                            ConnectTimeout = 15,
#if NETFRAMEWORK
                            TransparentNetworkIPResolution = false,
#endif
                        }.ConnectionString;

                        conn = new SqlConnection(cs);
                        conn.Open();
                        cmd = new SqlCommand("SELECT 1", conn) { CommandTimeout = commandTimeoutSec };
                        readTask = cmd.ExecuteReaderAsync();
                        Interlocked.Increment(ref attempts);

                        // Fire the RST at ~the command-timeout instant, with jitter,
                        // trying to land just after the SNI read timeout but before
                        // (or during) the attention write.
                        int rstDelayMs = commandTimeoutSec * 1000 + rng.Next(-40, 100);
                        RstProxy captured = proxy;
                        _ = Task.Delay(rstDelayMs).ContinueWith(_ =>
                        {
                            try { captured.Rst(); } catch { /* ignore */ }
                        });

                        try
                        {
                            settled = readTask.Wait(perOpBudget);
                        }
                        catch (AggregateException)
                        {
                            settled = true; // faulted/cancelled = healthy
                        }

                        if (!settled)
                        {
                            lock (gate)
                            {
                                hangInfo ??= $"worker {id}: ExecuteReaderAsync did not settle within " +
                                    $"{perOpBudget.TotalSeconds:N0}s (attempt #{Interlocked.Read(ref attempts)})";
                            }
                        }
                    }
                    catch
                    {
                        // Per-iteration connect/read failures under the RST storm are expected.
                    }
                    finally
                    {
                        cmd?.Dispose();
                        // Only tear down if this iteration settled; a hung connection's
                        // Dispose() would also block in SNIClose.
                        if (settled)
                        {
                            try { conn?.Dispose(); } catch { /* ignore */ }
                            try { proxy?.Dispose(); } catch { /* ignore */ }
                        }
                    }
                }
            }

            Thread[] threads = new Thread[workers];
            for (int i = 0; i < workers; i++)
            {
                int id = i;
                threads[i] = new Thread(() => Worker(id)) { IsBackground = true, Name = $"SoakWorker{id}" };
                threads[i].Start();
            }

            // Wait until the soak window elapses or a hang is detected, then give
            // workers a moment to unwind.
            DateTime deadline = DateTime.UtcNow + soak + perOpBudget + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline
                   && Volatile.Read(ref hangInfo) is null
                   && threads.Any(t => t.IsAlive))
            {
                Thread.Sleep(500);
            }
            foreach (Thread t in threads)
            {
                t.Join(TimeSpan.FromSeconds(2));
            }

            Assert.True(
                Volatile.Read(ref hangInfo) is null,
                $"Reproduced the re-entrant SNIClose deadlock: {hangInfo}. Total attempts: " +
                $"{Interlocked.Read(ref attempts)}. (close from within ReadAsyncCallback -> SNIClose -> " +
                "WaitForActiveCallbacks; ADO.Net #43847 / ICM 775308542).");
        }
    }
}
