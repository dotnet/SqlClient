// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class OpenAsyncScoreboardTests
    {
        [Fact]
        public void MatrixHasUniqueSelectorsAndScenarioSpecificShapes()
        {
            OpenAsyncScoreboardCase[] cases = new OpenAsyncScoreboardOptions().GetCases();
            Assert.Equal(24, cases.Length);
            Assert.Equal(cases.Length, cases.Select(c => c.CaseId).Distinct().Count());
            Assert.All(cases.Where(c => c.Scenario == "E4"), c =>
            {
                Assert.Equal(1, c.OperationsPerWorker);
                Assert.Equal(c.Concurrency, c.PoolSize);
            });
            Assert.All(cases.Where(c => c.Scenario == "E2"), c =>
            {
                Assert.Equal(4, c.PoolSize);
                Assert.Equal(10, c.HoldMilliseconds);
            });
            Assert.All(cases.Where(c => c.Scenario == "E3"), c => Assert.Equal(0, c.PoolSize));
        }

        [Fact]
        public void InvalidMatricesAreRejected()
        {
            var invalid = new[]
            {
                new OpenAsyncScoreboardOptions { Scenarios = new[] { "E5" } },
                new OpenAsyncScoreboardOptions { Scenarios = Array.Empty<string>() },
                new OpenAsyncScoreboardOptions { Apis = new[] { "Async", "Async" } },
                new OpenAsyncScoreboardOptions { Apis = new[] { "async" } },
                new OpenAsyncScoreboardOptions { Concurrency = new[] { 0 } },
                new OpenAsyncScoreboardOptions { Concurrency = new[] { 1, 1 } },
                new OpenAsyncScoreboardOptions { OperationsPerWorker = 0 },
                new OpenAsyncScoreboardOptions { SaturatedPoolSize = 0 },
                new OpenAsyncScoreboardOptions { HoldMilliseconds = -1 }
            };
            Assert.All(invalid, options => Assert.Throws<ArgumentException>(() => options.GetCases()));
            Assert.Throws<OverflowException>(() => new OpenAsyncScoreboardOptions
            {
                Concurrency = new[] { int.MaxValue }
            }.GetCases());
        }

        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task BothApisUseThreadPoolWorkersAndAccountForEveryAttempt(string api)
        {
            OpenAsyncScoreboardCase configuration = CreateCase("E1", api, 4, 3);
            var connections = new ConcurrentBag<FakeConnection>();
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration, () =>
            {
                var connection = new FakeConnection();
                connections.Add(connection);
                return connection;
            }, watchdog.Token);

            result.EnsureValid(configuration);
            Assert.Equal(12, result.Samples.Length);
            Assert.Equal(12, connections.Count);
            Assert.All(connections, c =>
            {
                Assert.True(c.OpenedOnThreadPool);
                Assert.Equal(api == "Async", c.OpenedAsynchronously);
                Assert.True(c.IsDisposed);
            });
            Assert.Equal(4, result.AdmissionMilliseconds.Length);
            Assert.Equal(12, result.Samples.Select(s => (s.Worker, s.Operation)).Distinct().Count());
            Assert.All(result.Samples, sample =>
            {
                Assert.InRange(sample.StartMilliseconds, 0, sample.CompletionMilliseconds);
                if (api == "Async")
                {
                    Assert.True(sample.CompletedAtReturn);
                    Assert.NotNull(sample.TaskReturnMilliseconds);
                    Assert.InRange(sample.TaskReturnMilliseconds.Value, sample.StartMilliseconds, sample.CompletionMilliseconds);
                }
                else
                {
                    Assert.Null(sample.CompletedAtReturn);
                    Assert.Null(sample.TaskReturnMilliseconds);
                }
            });
        }

        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task ColdRampRetainsConnectionsUntilEveryOpenCompletes(string api)
        {
            OpenAsyncScoreboardCase configuration = CreateCase("E4", api, 4);
            int opened = 0;
            var countsAtClose = new ConcurrentBag<int>();
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration,
                () => new FakeConnection(onOpened: () => Interlocked.Increment(ref opened),
                    onClose: () => countsAtClose.Add(Volatile.Read(ref opened))), watchdog.Token);

            result.EnsureValid(configuration);
            Assert.Equal(4, result.PeakOpenConnections);
            Assert.Equal(4, countsAtClose.Count);
            Assert.All(countsAtClose, count => Assert.Equal(4, count));
        }

        [Fact]
        public async Task HeldAsyncOpenDoesNotCompleteTheWorkloadBeforeRelease()
        {
            OpenAsyncScoreboardCase configuration = CreateCase("E3", "Async", 1, 1);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Task<OpenAsyncInvocation> pending = OpenAsyncWorkload.RunAsync(configuration, () =>
                new FakeConnection(async token =>
                {
                    entered.SetResult(true);
                    await release.Task.WaitAsync(token);
                }), watchdog.Token);

            await entered.Task.WaitAsync(watchdog.Token);
            Assert.False(pending.IsCompleted);
            release.SetResult(true);
            OpenAsyncInvocation result = await pending;
            result.EnsureValid(configuration);
            OpenAsyncSample sample = Assert.Single(result.Samples);
            Assert.NotNull(sample.TaskReturnMilliseconds);
            Assert.InRange(sample.TaskReturnMilliseconds.Value, sample.StartMilliseconds, sample.CompletionMilliseconds);
        }

        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task FailedRampOpenReleasesPeersButInvalidatesTheInvocation(string api)
        {
            OpenAsyncScoreboardCase configuration = CreateCase("E4", api, 4);
            int factories = 0;
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration, () =>
            {
                bool fail = Interlocked.Increment(ref factories) == 1;
                return new FakeConnection(_ => fail
                    ? Task.FromException(new InvalidOperationException("Deliberate test failure"))
                    : Task.CompletedTask);
            }, watchdog.Token);

            Assert.Equal(4, result.Samples.Length);
            OpenAsyncSample failed = Assert.Single(result.Samples, s => s.Outcome == "Failed");
            Assert.Equal(typeof(InvalidOperationException).FullName, failed.ErrorType);
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
        }

        [Theory]
        [InlineData("Sync")]
        [InlineData("Async")]
        public async Task SaturatedPoolUsesMatchedHoldsAndReleasesAllCapacity(string api)
        {
            var configuration = new OpenAsyncScoreboardOptions
            {
                Scenarios = new[] { "E2" },
                Apis = new[] { api },
                Concurrency = new[] { 6 },
                OperationsPerWorker = 2,
                SaturatedPoolSize = 2,
                HoldMilliseconds = 1
            }.GetCases().Single();
            using var pool = new SemaphoreSlim(2);
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration, () =>
                new FakeConnection(_ => pool.WaitAsync(watchdog.Token), onClose: () => pool.Release()), watchdog.Token);
            result.EnsureValid(configuration);
            Assert.Equal(12, result.Samples.Length);
            Assert.InRange(result.PeakOpenConnections, 1, 2);
            Assert.Equal(2, pool.CurrentCount);
        }

        [Fact]
        public async Task CancellationIsRecordedAsFailureNotSuccessfulThroughput()
        {
            OpenAsyncScoreboardCase configuration = CreateCase("E3", "Async", 1, 1);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            OpenAsyncInvocation result = await OpenAsyncWorkload.RunAsync(configuration,
                () => new FakeConnection(), cancellation.Token);
            Assert.Equal("Canceled", Assert.Single(result.Samples).Outcome);
            Assert.Throws<InvalidOperationException>(() => result.EnsureValid(configuration));
        }

        private static OpenAsyncScoreboardCase CreateCase(string scenario, string api, int concurrency,
            int operations = 1) => new OpenAsyncScoreboardOptions
            {
                Scenarios = new[] { scenario },
                Apis = new[] { api },
                Concurrency = new[] { concurrency },
                OperationsPerWorker = operations
            }.GetCases().Single();

        private sealed class FakeConnection : DbConnection
        {
            private readonly Func<CancellationToken, Task> _open;
            private readonly Action? _onOpened;
            private readonly Action? _onClose;
            private ConnectionState _state;

            public FakeConnection(Func<CancellationToken, Task>? open = null, Action? onOpened = null, Action? onClose = null)
            {
                _open = open ?? (_ => Task.CompletedTask);
                _onOpened = onOpened;
                _onClose = onClose;
            }

            public bool OpenedOnThreadPool { get; private set; }
            public bool OpenedAsynchronously { get; private set; }
            public bool IsDisposed { get; private set; }
            [AllowNull]
            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => "test";
            public override string DataSource => "test";
            public override string ServerVersion => "test";
            public override ConnectionState State => _state;
            public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
            protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

            public override void Open()
            {
                OpenedOnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                _open(CancellationToken.None).GetAwaiter().GetResult();
                _state = ConnectionState.Open;
                _onOpened?.Invoke();
            }

            public override async Task OpenAsync(CancellationToken cancellationToken)
            {
                OpenedOnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                OpenedAsynchronously = true;
                await _open(cancellationToken);
                _state = ConnectionState.Open;
                _onOpened?.Invoke();
            }

            public override void Close()
            {
                if (_state == ConnectionState.Open)
                {
                    _onClose?.Invoke();
                    _state = ConnectionState.Closed;
                }
            }

            protected override void Dispose(bool disposing)
            {
                Close();
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
    #endif
}
