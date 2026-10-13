// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;
using FakeConnection = Microsoft.Data.SqlClient.UnitTests.OpenAsyncScoreboardTests.FakeConnection;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>Verifies matched E5 queries and independently offered E6 load using observable gates, not speed gates.</summary>
    public class OpenAsyncExtendedWorkloadTests
    {
        /// <summary>Legacy selectors stay stable; diagnostics and load-specific concurrency are explicit matrix dimensions.</summary>
        [Fact]
        public void ExtendedMatrixPreservesSampledSelectorsAndLoadDefaults()
        {
            var cases = new OpenAsyncScoreboardOptions
            {
                Scenarios = new[] { "E1", "E5", "E6" },
                InstrumentationModes = new[] { "Sampled", "Diagnostic" }
            }.GetCases();
            Assert.Equal(40, cases.Length);
            Assert.Equal(cases.Length, cases.Select(c => c.CaseId).Distinct().Count());
            Assert.Contains(cases, c => c.CaseId == "OpenAsyncScoreboard-E1-Async-32");
            Assert.Contains(cases, c => c.CaseId == "OpenAsyncScoreboard-E1-Async-32-Diagnostic");
            Assert.All(cases.Where(c => c.Scenario == "E5"), c =>
            {
                Assert.Equal(c.Concurrency, c.PoolSize);
                Assert.Equal(0, c.HoldMilliseconds);
            });
            Assert.All(cases.Where(c => c.Scenario == "E6"), c =>
            {
                Assert.Equal(4, c.PoolSize);
                Assert.Equal(10, c.HoldMilliseconds);
                Assert.Equal(2000, c.LoadPhaseMilliseconds);
                Assert.Equal(64, c.LoadQueueCapacity);
                Assert.Equal(25, c.LoadBaseRate);
                Assert.Equal(800, c.LoadOverloadRate);
            });
            Assert.Equal(new[] { 1, 8, 32, 64 }, cases.Where(c => c.Scenario == "E6")
                .Select(c => c.Concurrency).Distinct().ToArray());
        }

        /// <summary>Both opening APIs run the same asynchronous query once after every successful open.</summary>
        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task QueryIsMatchedAndTimedSeparately(string api)
        {
            var configuration = CreateCase("E5", api);
            int calls = 0;
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration,
                () => new FakeConnection(), watchdog.Token, async (connection, token) =>
                {
                    Assert.Equal(ConnectionState.Open, connection.State);
                    Interlocked.Increment(ref calls);
                    await Task.Yield();
                    token.ThrowIfCancellationRequested();
                });
            result.EnsureValid(configuration);
            Assert.Equal(configuration.OperationCount, calls);
            Assert.Equal(calls, result.QuerySamples.Length);
            foreach (OpenAsyncSample sample in result.Samples)
            {
                OpenAsyncQuerySample query = Assert.Single(result.QuerySamples, q => q.RequestId == sample.RequestId);
                Assert.True(query.StartMilliseconds >= sample.CompletionMilliseconds);
                Assert.True(query.CompletionMilliseconds >= query.StartMilliseconds);
                Assert.True(sample.LifecycleCompletionMilliseconds >= query.CompletionMilliseconds);
            }
        }

        /// <summary>A query failure invalidates E5 without rewriting a successful acquisition.</summary>
        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task QueryFailureDoesNotOverwriteOpenOutcome(string api)
        {
            var configuration = CreateCase("E5", api);
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration,
                () => new FakeConnection(), CancellationToken.None,
                (_, _) => Task.FromException(new InvalidOperationException("Deliberate query failure")));
            Assert.All(result.Samples, sample => Assert.Equal("Success", sample.Outcome));
            Assert.All(result.QuerySamples, query =>
            {
                Assert.Equal("Failed", query.Outcome);
                Assert.Equal(typeof(InvalidOperationException).FullName, query.ErrorType);
            });
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
            await Assert.ThrowsAsync<ArgumentException>(() => OpenAsyncWorkload.RunAsync(configuration,
                () => new FakeConnection(), CancellationToken.None));
        }

        /// <summary>Cleanup failures remain visible independently of acquisition and query success.</summary>
        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task CleanupFailureInvalidatesSuccessfulOpen(string api)
        {
            var configuration = CreateCase("E5", api);
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration,
                () => new FakeConnection(onClose: () => throw new InvalidOperationException("Deliberate close failure")),
                CancellationToken.None, (_, _) => Task.CompletedTask);
            Assert.All(result.Samples, sample =>
            {
                Assert.Equal("Success", sample.Outcome);
                Assert.Equal("Failed", sample.LifecycleOutcome);
                Assert.NotNull(sample.LifecycleCompletionMilliseconds);
            });
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
        }

        /// <summary>A factory failure cannot strand retained cold-ramp peers at their rendezvous.</summary>
        [Fact]
        public async Task FailedFactoryReleasesRetainedRamp()
        {
            var configuration = new OpenAsyncScoreboardOptions
            {
                Scenarios = new[] { "E4" }, Apis = new[] { "Async" }, Concurrency = new[] { 3 }
            }.GetCases().Single();
            int factories = 0;
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration,
                () => Interlocked.Increment(ref factories) == 1
                    ? throw new ArgumentException("Deliberate factory failure")
                    : new FakeConnection(), watchdog.Token);
            Assert.Single(result.Samples, sample => sample.Outcome == "Failed");
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
        }

        /// <summary>The fixed phase schedule includes every tick, including nonintegral endpoint rates.</summary>
        [Fact]
        public void ScheduleUsesFixedPhaseBoundariesAndUniqueIds()
        {
            OpenAsyncLoadOffer[] offers = OpenAsyncLoadWorkload.Schedule(CreateCase("E6", "Async")).ToArray();
            Assert.Equal(8, offers.Length);
            Assert.Equal(Enumerable.Range(0, 8), offers.Select(offer => offer.RequestId));
            Assert.Equal(new[] { "Baseline", "Overload", "Recovery" }, offers.Select(o => o.Phase).Distinct());
            Assert.Equal(new[] { 0.0, 5, 10, 12.5, 15, 17.5, 20, 25 },
                offers.Select(offer => offer.ScheduledMilliseconds));
        }

        /// <summary>All phases are offered while the only worker is held; the bounded queue rejects instead of backpressuring.</summary>
        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task ArrivalsContinueWhileWorkerHeldAndAllAcceptedDrain(string api)
        {
            var configuration = CreateCase("E6", api);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var entered = new ManualResetEventSlim();
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var connections = new ConcurrentBag<FakeConnection>();
            Func<(int Outstanding, int Owned)>? counters = null;
            int completed = 0;
            OpenAsyncInvocation result = await OpenAsyncLoadWorkload.RunCoreAsync(configuration, () =>
            {
                var connection = new FakeConnection(async _ =>
                {
                    entered.Set();
                    await release.Task.WaitAsync(watchdog.Token);
                });
                connections.Add(connection);
                return connection;
            }, watchdog.Token, onStarted: (_, read) =>
            {
                counters = read;
                Assert.Equal((0, 0), read());
            }, onCompleted: () =>
            {
                Assert.Equal((0, 0), counters!());
                Assert.All(connections, connection => Assert.True(connection.IsDisposed));
                Interlocked.Increment(ref completed);
            }, onOffered: offer =>
            {
                if (offer.RequestId == 0)
                {
                    entered.Wait(watchdog.Token);
                }
                if (offer.RequestId == 7)
                {
                    Assert.False(release.Task.IsCompleted);
                    Assert.Equal((1, 0), counters!());
                    release.SetResult(true);
                }
            });
            result.EnsureValid(configuration);
            Assert.Equal(1, completed);
            Assert.NotNull(result.Load);
            Assert.Equal(8, result.Load.Offers.Length);
            Assert.Equal(result.Load.GenerationStartMilliseconds, result.Load.Offers[0].ScheduledMilliseconds);
            Assert.Equal(result.Load.GenerationStartMilliseconds + configuration.LoadPhaseMilliseconds,
                result.Load.Offers.First(offer => offer.Phase == "Overload").ScheduledMilliseconds);
            Assert.Equal(result.Load.GenerationStartMilliseconds + 2 * configuration.LoadPhaseMilliseconds,
                result.Load.Offers.First(offer => offer.Phase == "Recovery").ScheduledMilliseconds);
            Assert.True(result.Load.GenerationCompletedMilliseconds >=
                result.Load.GenerationStartMilliseconds + 3 * configuration.LoadPhaseMilliseconds);
            Assert.Equal(2, result.Load.Offers.Count(offer => offer.Accepted));
            Assert.Equal(6, result.Load.Offers.Count(offer => !offer.Accepted));
            Assert.Equal(result.Load.Offers.Where(o => o.Accepted).Select(o => o.RequestId),
                result.Samples.Select(s => s.RequestId));
            Assert.All(connections, connection =>
            {
                Assert.True(connection.OpenedOnThreadPool);
                Assert.Equal(api == "Async", connection.OpenedAsynchronously);
            });
            Assert.All(result.Samples, sample =>
            {
                Assert.NotNull(sample.OfferedMilliseconds);
                Assert.NotNull(sample.Phase);
                Assert.True(sample.LifecycleCompletionMilliseconds <= result.Load.DrainCompletedMilliseconds);
            });
            Assert.Empty(result.QuerySamples);
            // Accounting mutations must be rejected, not presented as successful throughput.
            result.Load.Offers[1].Accepted = false;
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
        }

        /// <summary>Cancellation drains every accepted arrival into failure evidence and cannot validate as throughput.</summary>
        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task WatchdogCancellationDoesNotPass(string api)
        {
            var configuration = CreateCase("E6", api);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            int completed = 0;
            OpenAsyncInvocation result = await OpenAsyncLoadWorkload.RunCoreAsync(configuration,
                () => new FakeConnection(_ => Task.Delay(Timeout.Infinite, cancellation.Token)),
                cancellation.Token, onCompleted: () => completed++, onOffered: offer =>
                {
                    if (offer.RequestId == 1)
                    {
                        cancellation.Cancel();
                    }
                });
            Assert.Equal(1, completed);
            Assert.True(result.WasCanceled);
            Assert.Equal(result.Load!.Offers.Count(offer => offer.Accepted), result.Samples.Length);
            Assert.All(result.Samples, sample => Assert.Equal("Canceled", sample.Outcome));
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
        }

        /// <summary>Startup failures release waiting workers and run completion observation without opening a connection.</summary>
        [Theory]
        [InlineData("E1")]
        [InlineData("E6")]
        public async Task StartupObserverFailureDoesNotStrandWorkers(string scenario)
        {
            var configuration = CreateCase(scenario, "Async");
            int completed = 0, factories = 0;
            Func<System.Data.Common.DbConnection> factory = () =>
            {
                factories++;
                return new FakeConnection();
            };
            Action<long, Func<(int Outstanding, int Owned)>> started = (_, _) =>
                throw new InvalidOperationException("Deliberate observer failure");
            await Assert.ThrowsAsync<InvalidOperationException>(() => scenario == "E6"
                ? OpenAsyncLoadWorkload.RunAsync(configuration, factory, CancellationToken.None,
                    onStarted: started, onCompleted: () => completed++)
                : OpenAsyncWorkload.RunAsync(configuration, factory, CancellationToken.None,
                    onStarted: started, onCompleted: () => completed++));
            Assert.Equal(1, completed);
            Assert.Equal(0, factories);
        }

        /// <summary>Completion observation starts after drain and elapsed capture, excluding observer export from workload timing.</summary>
        [Theory]
        [InlineData("E1", "Sync")]
        [InlineData("E1", "Async")]
        [InlineData("E6", "Sync")]
        [InlineData("E6", "Async")]
        public async Task CompletionObserverRunsAfterDrainAndElapsedCapture(string scenario, string api)
        {
            var configuration = CreateCase(scenario, api);
            var connections = new ConcurrentBag<FakeConnection>();
            long epoch = 0;
            double completionObserverStarted = -1;
            Func<(int Outstanding, int Owned)>? readCounters = null;
            Func<System.Data.Common.DbConnection> factory = () =>
            {
                var connection = new FakeConnection();
                connections.Add(connection);
                return connection;
            };
            Action<long, Func<(int Outstanding, int Owned)>> started = (start, read) =>
            {
                epoch = start;
                readCounters = read;
                Assert.Equal((0, 0), read());
            };
            Action completed = () =>
            {
                completionObserverStarted = Stopwatch.GetElapsedTime(epoch).TotalMilliseconds;
                Assert.Equal((0, 0), readCounters!());
                Assert.All(connections, connection => Assert.True(connection.IsDisposed));
            };
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = scenario == "E6"
                ? await OpenAsyncLoadWorkload.RunAsync(configuration, factory, watchdog.Token,
                    onStarted: started, onCompleted: completed)
                : await OpenAsyncWorkload.RunAsync(configuration, factory, watchdog.Token,
                    onStarted: started, onCompleted: completed);
            result.EnsureValid(configuration);
            Assert.InRange(result.ElapsedMilliseconds, 0, completionObserverStarted);
            Assert.All(result.Samples, sample =>
                Assert.True(sample.LifecycleCompletionMilliseconds <= result.ElapsedMilliseconds));
            if (result.Load != null)
            {
                Assert.True(result.Load.GenerationCompletedMilliseconds <= result.Load.DrainCompletedMilliseconds);
                Assert.True(result.Load.DrainCompletedMilliseconds <= result.ElapsedMilliseconds);
            }
        }

        /// <summary>Creates small deterministic schedules; wall-clock watchdogs only guard hangs, not performance.</summary>
        private static OpenAsyncScoreboardCase CreateCase(string scenario, string api) =>
            new OpenAsyncScoreboardOptions
            {
                Scenarios = new[] { scenario },
                Apis = new[] { api },
                Concurrency = new[] { 1 },
                LoadConcurrency = new[] { 1 },
                OperationsPerWorker = 2,
                LoadPhaseMilliseconds = 10,
                LoadQueueCapacity = 1,
                LoadBaseRate = 200,
                LoadOverloadRate = 400,
                HoldMilliseconds = 1
            }.GetCases().Single();
    }
}
#endif
