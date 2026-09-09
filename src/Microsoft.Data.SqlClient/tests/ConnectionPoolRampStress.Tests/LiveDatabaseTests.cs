// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Sdk;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Opts into live connections without accessing SqlClient during discovery.</summary>
public sealed class LiveDatabaseFactAttribute : FactAttribute
{
    /// <summary>Skips unattended runs unless a connection was supplied through the environment.</summary>
    public LiveDatabaseFactAttribute()
    {
        Timeout = 30000;
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_CONNECTION")))
            Skip = "Set SQLCLIENT_RAMP_CONNECTION to opt into live SQL tests.";
    }
}

/// <summary>Checks physical session identity and ownership using the actual workload coordinator.</summary>
public sealed class LiveDatabaseTests
{
    /// <summary>Serializes caller variants under one process-cached pool mode and verifies final cleanup.</summary>
    [LiveDatabaseFact]
    public async Task WorkloadRunnerPreservesPhysicalSessionContract()
    {
        PoolMode pool = Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_POOL") switch
        {
            "legacy" => PoolMode.Legacy,
            "v2" => PoolMode.V2,
            "disabled" => PoolMode.Disabled,
            _ => throw new XunitException("Set SQLCLIENT_RAMP_POOL to legacy, v2, or disabled.")
        };
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", pool == PoolMode.V2);

        try
        {
            string input = Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_CONNECTION")!;
            foreach (CallerMode caller in Enum.GetValues<CallerMode>())
            {
                await RunSample(input, pool, caller, Workload.OpenClose);
                await RunSample(input, pool, caller, Workload.ColdRamps);
            }
            await RunSample(input, pool, CallerMode.Async, Workload.ColdRamps, incomplete: true);
        }
        catch (XunitException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never retain the original exception as an inner exception.
            throw new XunitException($"Live SQL test failed: {Failure.Describe(exception)}");
        }
    }

    /// <summary>Runs real opens without queries and releases late ownership before clearing the test pool.</summary>
    /// <param name="input">The environment-only connection string, never included in diagnostics.</param>
    /// <param name="pool">The single pool mode selected before SqlClient initialization.</param>
    /// <param name="caller">The runner's persistent caller implementation.</param>
    /// <param name="workload">Continuous reuse or held cold rounds.</param>
    /// <param name="incomplete">Whether to delay one successful open past bounded drain.</param>
    /// <returns>Completion after the coordinator and all connection owners have drained.</returns>
    private static async Task RunSample(string input, PoolMode pool, CallerMode caller,
        Workload workload, bool incomplete = false)
    {
        Settings settings = new()
        {
            DurationSeconds = 2, IntervalSeconds = 0.1, ConnectTimeoutSeconds = 3,
            StartupSeconds = 3, DrainSeconds = incomplete ? 0.1 : 3,
            CleanupSeconds = 1, DeadlineSeconds = 10
        };
        Sample sample = new(workload, pool, caller, PoolProfile.Default,
            workload == Workload.ColdRamps ? 3 : 1, 1);
        ObservedFactory factory = new(input, sample, settings, incomplete);
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(settings, sample, factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            if (incomplete)
            {
                await factory.AllOpened.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await factory.PeersSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                factory.AllowIncompleteCleanup = true;
                runner.RequestStop();
            }

            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(8));
            string context = $"{pool}/{caller}/{workload}/incomplete={incomplete}";
            Assert.True(result.Failures.Count == 0,
                $"{context}: {string.Join(", ", result.Failures.Select(f => f.Failure))}");
            Assert.Equal(sample.Concurrency, result.StartedWorkers);
            Assert.False(factory.DisposedBeforeOwnSettlement);
            Assert.False(factory.DuplicateDispose);
            Assert.False(factory.ClearWhileOwned);
            Assert.False(factory.RoundStartedBeforeClear);
            Assert.False(factory.PrematureRoundDispose);

            if (incomplete)
            {
                Assert.Equal(Outcome.Incomplete, result.Outcome);
                Assert.True(result.IncompleteRound);
                Assert.Equal(0, result.CompletedRounds);
                Assert.Equal(0, factory.Clears);
                Assert.Equal(sample.Concurrency - 1, factory.Disposed);
                Assert.Equal(1, factory.Held);
                Assert.Equal(sample.Concurrency, factory.DistinctIds);
            }
            else
            {
                Assert.True(result.Outcome == Outcome.Success, $"{context}: {result.Outcome}");
                Assert.False(result.IncompleteRound);
                Assert.Equal(factory.Created, factory.Disposed);
                Assert.Equal(factory.Opened, result.Measured.Opens.Count + result.Drain.Opens.Count);
                Assert.Equal(factory.Created, factory.Opened);
                Assert.Equal(0, factory.Held);
                if (workload == Workload.ColdRamps)
                {
                    Assert.True(result.CompletedRounds >= 2, $"{context}: fewer than two completed rounds.");
                    Assert.Equal(result.CompletedRounds, factory.Clears);
                    Assert.Equal(result.CompletedRounds * sample.Concurrency, factory.Opened);
                    Assert.Equal(sample.Concurrency, factory.PeakHeld);
                    Assert.Equal(factory.Opened, factory.DistinctIds);
                    Assert.False(factory.IncompleteClear);
                }
                else
                {
                    Assert.True(factory.Opened >= 2, $"{context}: fewer than two reuse cycles.");
                    Assert.Equal(0, factory.Clears);
                    Assert.Equal(factory.Opened, result.Measured.Cycles.Count + result.Drain.Cycles.Count);
                    Assert.Equal(pool == PoolMode.Disabled ? factory.Opened : 1, factory.DistinctIds);
                }
            }
        }
        finally
        {
            runner.RequestStop();
            factory.Release.TrySetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(5));
            await factory.WaitForDisposal();
            factory.ClearAfterRun();
        }
        Assert.Equal(factory.Created, factory.Disposed);
        Assert.False(factory.DisposedBeforeOwnSettlement);
        Assert.False(factory.DuplicateDispose);
        Assert.Equal(0, factory.Held);
    }

    /// <summary>Observes real session IDs and round ownership without issuing SQL or replacing scheduling.</summary>
    private sealed class ObservedFactory : IConnectionFactory
    {
        private readonly object _gate = new();
        private readonly string _connectionString;
        private readonly Sample _sample;
        private readonly bool _incomplete;
        private readonly HashSet<Guid> _ids = new();
        private int _created;
        private int _disposed;
        private int _opened;
        private int _settled;
        private int _held;
        private int _roundOpened;
        public int Created => Volatile.Read(ref _created);
        public int Disposed => Volatile.Read(ref _disposed);
        public int Opened => Volatile.Read(ref _opened);
        public int Held => Volatile.Read(ref _held);
        public int DistinctIds { get { lock (_gate) return _ids.Count; } }
        public int Clears { get; private set; }
        public int PeakHeld { get; private set; }
        public volatile bool AllowIncompleteCleanup;
        public bool DisposedBeforeOwnSettlement;
        public bool DuplicateDispose;
        public bool ClearWhileOwned;
        public bool RoundStartedBeforeClear;
        public bool PrematureRoundDispose;
        public bool IncompleteClear;
        public readonly TaskCompletionSource AllOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource PeersSettled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Isolates this test's pool while preserving the harness's connection normalization.</summary>
        /// <param name="input">The connection secret used only to construct connections.</param>
        /// <param name="sample">The runner's pool and concurrency selection.</param>
        /// <param name="settings">Bounded login and drain settings.</param>
        /// <param name="incomplete">Whether the final first-round open returns late.</param>
        public ObservedFactory(string input, Sample sample, Settings settings, bool incomplete)
        {
            // Retain the production factory's V2 availability check.
            _ = new ConnectionFactory(input, sample, settings);
            var builder = ConnectionFactory.Normalize(input, sample, settings);
            builder.ApplicationName = $"RampLiveTest-{Guid.NewGuid():N}";
            builder.LoadBalanceTimeout = 0;
            _connectionString = builder.ConnectionString;
            _sample = sample;
            _incomplete = incomplete;
        }

        /// <summary>Assigns admission order and detects any cold round dispatched before its predecessor cleared.</summary>
        /// <returns>A real connection whose lifetime remains owned by WorkloadRunner.</returns>
        public IConnection Create()
        {
            lock (_gate)
            {
                int id = ++_created;
                if (_sample.Workload == Workload.ColdRamps && (id - 1) / _sample.Concurrency != Clears)
                    RoundStartedBeforeClear = true;
                return new ObservedConnection(this, new SqlConnection(_connectionString), id);
            }
        }

        /// <summary>Checks every round owner released its connection before performing the runner's clear.</summary>
        public void ClearPool()
        {
            lock (_gate)
            {
                ClearWhileOwned |= _held != 0 || _settled != _created || _disposed != _created;
                IncompleteClear |= _roundOpened != _sample.Concurrency;
                ClearAfterRun();
                _roundOpened = 0;
                Clears++;
            }
        }

        /// <summary>Clears only this test's isolated pool, outside the observed workload.</summary>
        public void ClearAfterRun()
        {
            using SqlConnection key = new(_connectionString);
            SqlConnection.ClearPool(key);
        }

        /// <summary>Waits for late owners rather than disposing connections while their opens are active.</summary>
        /// <returns>Completion after all admitted connections have been disposed.</returns>
        public async Task WaitForDisposal()
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            while (Disposed != Created)
                await Task.Delay(10, timeout.Token);
        }

        /// <summary>Forwards real opens and records ownership at API settlement and disposal boundaries.</summary>
        private sealed class ObservedConnection(ObservedFactory owner, SqlConnection connection, int id) : IConnection
        {
            private bool _opened;
            private bool _settled;
            private bool _disposed;

            /// <summary>Preserves blocking Open behavior on the runner-selected worker thread.</summary>
            public void Open()
            {
                try
                {
                    connection.Open();
                    RecordOpen();
                }
                finally { Settle(); }
            }

            /// <summary>Preserves async Open and optionally delays returning one real connected session.</summary>
            /// <param name="cancellationToken">The runner's cancellation token for physical login.</param>
            /// <returns>Completion when login and the optional late-settlement gate finish.</returns>
            public async Task OpenAsync(CancellationToken cancellationToken)
            {
                try
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    RecordOpen();
                    if (owner._incomplete && id == owner._sample.Concurrency)
                    {
                        // This gate deliberately outlives runner cancellation to test late ownership.
                        await owner.Release.Task.ConfigureAwait(false);
                    }
                }
                finally { Settle(); }
            }

            /// <summary>Captures ClientConnectionId while the connection is open without executing a query.</summary>
            private void RecordOpen()
            {
                Guid clientId = connection.ClientConnectionId;
                if (clientId == Guid.Empty) throw new InvalidOperationException();
                lock (owner._gate)
                {
                    _opened = true;
                    owner._ids.Add(clientId);
                    owner._opened++;
                    owner._roundOpened++;
                    owner.PeakHeld = Math.Max(owner.PeakHeld, ++owner._held);
                    if (owner._opened == owner._sample.Concurrency) owner.AllOpened.TrySetResult();
                }
            }

            /// <summary>Records returned or failed API calls separately from still-held physical sessions.</summary>
            private void Settle()
            {
                lock (owner._gate)
                {
                    _settled = true;
                    if (++owner._settled == owner._sample.Concurrency - 1)
                        owner.PeersSettled.TrySetResult();
                }
            }

            /// <summary>Checks round-wide settlement before normal disposal and own settlement during incomplete cleanup.</summary>
            public void Dispose()
            {
                lock (owner._gate)
                {
                    owner.DisposedBeforeOwnSettlement |= !_settled;
                    if (_disposed)
                    {
                        owner.DuplicateDispose = true;
                        return;
                    }
                    _disposed = true;
                    if (_opened && owner._sample.Workload == Workload.ColdRamps && !owner.AllowIncompleteCleanup)
                    {
                        int expected = ((id - 1) / owner._sample.Concurrency + 1) * owner._sample.Concurrency;
                        owner.PrematureRoundDispose |= owner._settled < expected;
                    }
                    connection.Dispose();
                    if (_opened) owner._held--;
                    owner._disposed++;
                }
            }
        }
    }
}
