// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Checks opt-in process-local authentication caching without credentials or SQL Server.</summary>
public sealed class CachedAuthenticationProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string DefaultConnection = "Server=localhost;Authentication=Active Directory Default";

    /// <summary>Concurrent connections share one acquisition and reuse its result across connection IDs.</summary>
    [Fact]
    public async Task ConcurrentRequestsShareAcquisitionAndCachedResult()
    {
        TaskCompletionSource<SqlAuthenticationToken> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeProvider source = new(_ => pending.Task);
        CachedAuthenticationProvider cache = new(source, () => Now);
        Task<SqlAuthenticationToken>[] requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => cache.AcquireTokenAsync(Parameters()))).ToArray();

        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        SqlAuthenticationToken token = new("<test-token>", Now.AddHours(1));
        pending.SetResult(token);
        SqlAuthenticationToken[] results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(results, result => Assert.Same(token, result));
        Assert.Same(token, await cache.AcquireTokenAsync(Parameters()));
        Assert.Equal(1, source.Calls);
        Assert.Equal(1, cache.AcquisitionCount);
    }

    /// <summary>Authority, resource, user, server, and database isolate cached identities.</summary>
    [Fact]
    public async Task DifferentRequestIdentitiesDoNotShareTokens()
    {
        FakeProvider source = new(_ => Task.FromResult(new SqlAuthenticationToken("<test-token>", Now.AddHours(1))));
        CachedAuthenticationProvider cache = new(source, () => Now);
        SqlAuthenticationParameters[] identities =
        [
            Parameters(), Parameters(authority: "https://authority.example/other"),
            Parameters(resource: "https://other-resource.example"), Parameters(user: "other-user"),
            Parameters(server: "other-server"), Parameters(database: "other-database")
        ];
        foreach (SqlAuthenticationParameters identity in identities)
        {
            SqlAuthenticationToken token = await cache.AcquireTokenAsync(identity);
            Assert.Same(token, await cache.AcquireTokenAsync(identity));
        }
        Assert.Equal(identities.Length, source.Calls);
        Assert.Equal(identities.Length, cache.AcquisitionCount);
    }

    /// <summary>An in-flight request for one identity does not block a different identity.</summary>
    [Fact]
    public async Task DifferentIdentitiesAcquireIndependently()
    {
        TaskCompletionSource<SqlAuthenticationToken> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SqlAuthenticationToken token = new("<test-token>", Now.AddHours(1));
        FakeProvider source = new(parameters => parameters.UserId == "waiting-user"
            ? pending.Task : Task.FromResult(token));
        CachedAuthenticationProvider cache = new(source, () => Now);
        Task<SqlAuthenticationToken> waiting = cache.AcquireTokenAsync(Parameters(user: "waiting-user"));
        try
        {
            Assert.Same(token, await cache.AcquireTokenAsync(Parameters()).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(waiting.IsCompleted);
            Assert.Equal(2, source.Calls);
            Assert.Equal(2, cache.AcquisitionCount);
        }
        finally
        {
            pending.SetResult(token);
            await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>The refresh margin forces reacquisition without waiting for actual expiry.</summary>
    [Fact]
    public async Task NearExpiryRefreshesAndFreshTokenIsReused()
    {
        DateTimeOffset now = Now;
        FakeProvider source = new(_ => Task.FromResult(new SqlAuthenticationToken("<test-token>", now.AddHours(1))));
        CachedAuthenticationProvider cache = new(source, () => now);
        SqlAuthenticationToken first = await cache.AcquireTokenAsync(Parameters());
        now = Now.AddMinutes(54);
        Assert.Same(first, await cache.AcquireTokenAsync(Parameters()));
        now = Now.AddMinutes(55);
        SqlAuthenticationToken refreshed = await cache.AcquireTokenAsync(Parameters());
        Assert.NotSame(first, refreshed);
        Assert.Same(refreshed, await cache.AcquireTokenAsync(Parameters()));
        Assert.Equal(2, source.Calls);
        Assert.Equal(2, cache.AcquisitionCount);
    }

    /// <summary>Tokens already inside the refresh margin are returned but not retained for reuse.</summary>
    [Fact]
    public async Task NearExpiryAcquisitionIsNotCached()
    {
        FakeProvider source = new(_ => Task.FromResult(new SqlAuthenticationToken("<test-token>", Now.AddMinutes(4))));
        CachedAuthenticationProvider cache = new(source, () => Now);
        await cache.AcquireTokenAsync(Parameters());
        await cache.AcquireTokenAsync(Parameters());
        Assert.Equal(2, source.Calls);
        Assert.Equal(2, cache.AcquisitionCount);
    }

    /// <summary>Concurrent waiters receive the acquisition failure and the next request can retry.</summary>
    [Fact]
    public async Task FailureIsSharedButNotCached()
    {
        TaskCompletionSource<SqlAuthenticationToken> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeProvider source = new(_ => pending.Task);
        CachedAuthenticationProvider cache = new(source, () => Now);
        Task<SqlAuthenticationToken> first = cache.AcquireTokenAsync(Parameters());
        Task<SqlAuthenticationToken> second = cache.AcquireTokenAsync(Parameters());
        Assert.Same(first, second);
        InvalidOperationException failure = new("test acquisition failure");
        pending.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => first));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => second));

        pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SqlAuthenticationToken token = new("<test-token>", Now.AddHours(1));
        pending.SetResult(token);
        Assert.Same(token, await cache.AcquireTokenAsync(Parameters()));
        Assert.Equal(2, source.Calls);
        Assert.Equal(2, cache.AcquisitionCount);
    }

    /// <summary>Synchronous provider failures also clear the in-flight request.</summary>
    [Fact]
    public async Task SynchronousFailureCanRetry()
    {
        FakeProvider source = new(_ => throw new InvalidOperationException());
        CachedAuthenticationProvider cache = new(source, () => Now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.AcquireTokenAsync(Parameters()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.AcquireTokenAsync(Parameters()));
        Assert.Equal(2, source.Calls);
        Assert.Equal(2, cache.AcquisitionCount);
    }

    /// <summary>The wrapper rejects unsupported methods before calling the underlying provider.</summary>
    [Fact]
    public async Task UnsupportedAuthenticationIsRejected()
    {
        FakeProvider source = new(_ => throw new InvalidOperationException());
        CachedAuthenticationProvider cache = new(source, () => Now);
        Assert.True(cache.IsSupported(SqlAuthenticationMethod.ActiveDirectoryDefault));
        Assert.False(cache.IsSupported(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal));
        await Assert.ThrowsAsync<AuthenticationCacheConfigurationException>(() =>
            cache.AcquireTokenAsync(Parameters(method: SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)));
        Assert.Equal(0, source.Calls);
        Assert.Equal(0, cache.AcquisitionCount);
    }

    /// <summary>Disabled caching neither parses connection strings nor replaces the normal provider.</summary>
    [Fact]
    public void DisabledCachingLeavesProviderUnchanged()
    {
        SqlAuthenticationProvider? original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault);
        CachedAuthenticationProvider.Configure(new(), "not a connection string", "also invalid");
        Assert.Same(original, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault));
        Assert.Null(CachedAuthenticationProvider.CurrentAcquisitionCount);
    }

    /// <summary>Both connection methods are checked before installing an opt-in provider.</summary>
    [Theory]
    [InlineData("Server=localhost", null)]
    [InlineData("Server=localhost;Authentication=Active Directory Password", null)]
    [InlineData(DefaultConnection, "Server=localhost")]
    [InlineData(DefaultConnection, "invalid connection string")]
    public void InvalidWorkloadOrObserverRejectsCaching(string workload, string? observer)
    {
        SqlAuthenticationProvider? original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault);
        Assert.Throws<AuthenticationCacheConfigurationException>(() =>
            CachedAuthenticationProvider.Configure(new() { CacheAuthentication = true }, workload, observer));
        Assert.Same(original, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault));
    }

    /// <summary>Registration is opt-in and idempotent, and safe counts distinguish warm-up from cache reuse.</summary>
    [Fact]
    public async Task ExplicitOptInWrapsProviderAndReportsAcquisitionCount()
    {
        SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)!;
        FakeProvider source = new(_ => Task.FromResult(new SqlAuthenticationToken("<test-token>", DateTimeOffset.MaxValue)));
        try
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, source));
            Assert.Null(CachedAuthenticationProvider.CurrentAcquisitionCount);
            Settings settings = new() { CacheAuthentication = true };
            CachedAuthenticationProvider.Configure(settings, DefaultConnection, DefaultConnection);
            SqlAuthenticationProvider installed = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)!;
            Assert.IsType<CachedAuthenticationProvider>(installed);
            CachedAuthenticationProvider.Configure(settings, DefaultConnection);
            Assert.Same(installed, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault));
            Assert.Equal(0, source.Calls);
            Assert.Equal(0, CachedAuthenticationProvider.CurrentAcquisitionCount);
            await installed.AcquireTokenAsync(Parameters());
            Assert.Equal(1, CachedAuthenticationProvider.CurrentAcquisitionCount);
            await installed.AcquireTokenAsync(Parameters());
            Assert.Equal(1, CachedAuthenticationProvider.CurrentAcquisitionCount);
        }
        finally
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, original));
        }
    }

    /// <summary>The opt-in survives safe parent-child serialization and defaults to disabled.</summary>
    [Fact]
    public void CommandLineAndWirePreserveExplicitOptIn()
    {
        Assert.False(new Settings().CacheAuthentication);
        Assert.Equal(0, CommandLine.Run(["--list", "--cache-authentication"], (settings, list) =>
        {
            Assert.True(list);
            Assert.True(settings.CacheAuthentication);
            var config = new ChildConfiguration(settings, Helpers.Sample(), null);
            ChildConfiguration copy = JsonSerializer.Deserialize<ChildConfiguration>(
                JsonSerializer.Serialize(config, Wire.Json), Wire.Json)!;
            Assert.True(copy.Settings.CacheAuthentication);
            return 0;
        }));
        Assert.Equal(0, CommandLine.Run(["--list"], (settings, _) =>
        {
            Assert.False(settings.CacheAuthentication);
            return 0;
        }));
    }

    /// <summary>Authentication stays on the connection string and preflight stays outside the measured pool.</summary>
    [Fact]
    public void CachedAuthenticationPreservesMethodAndNonpooledPreflight()
    {
        var measured = ConnectionFactory.Normalize(DefaultConnection, Helpers.Sample(),
            new() { CacheAuthentication = true });
        var preflight = ConnectionFactory.PreflightConnection(measured.ConnectionString);
        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryDefault, measured.Authentication);
        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryDefault, preflight.Authentication);
        Assert.True(measured.Pooling);
        Assert.False(preflight.Pooling);
        Assert.NotEqual(measured.ApplicationName, preflight.ApplicationName);
    }

    /// <summary>The parent rejects either unsupported connection before creating output or opening the observer.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentRejectsUnsupportedWorkloadOrObserverBeforeStarting(bool invalidObserver)
    {
        string workloadEnvironment = $"SQLCLIENT_RAMP_TEST_{Guid.NewGuid():N}";
        string observerEnvironment = $"SQLCLIENT_RAMP_TEST_{Guid.NewGuid():N}";
        string output = $"unused-authentication-cache-{Guid.NewGuid():N}.jsonl";
        TextWriter previous = Console.Error;
        using StringWriter error = new();
        try
        {
            Environment.SetEnvironmentVariable(workloadEnvironment, invalidObserver ? DefaultConnection : "Server=localhost");
            Environment.SetEnvironmentVariable(observerEnvironment, "Server=localhost");
            Console.SetError(error);
            Assert.Equal(2, Program.Main([
                "--cache-authentication", "--connection-env", workloadEnvironment,
                "--xevents", "--xevent-connection-env", observerEnvironment, "--output", output
            ]));
            Assert.Contains(AuthenticationCacheConfigurationException.Diagnostic, error.ToString());
            Assert.False(File.Exists(output));
        }
        finally
        {
            Console.SetError(previous);
            Environment.SetEnvironmentVariable(workloadEnvironment, null);
            Environment.SetEnvironmentVariable(observerEnvironment, null);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    /// <summary>A serialized opt-in is enforced in the child before nonpooled preflight can open a connection.</summary>
    [Fact]
    public void ChildRejectsUnsupportedAuthenticationBeforePreflight()
    {
        string environment = $"SQLCLIENT_RAMP_TEST_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(environment, "Server=localhost");
            Settings settings = Helpers.Settings() with
            {
                CacheAuthentication = true,
                ConnectionEnvironment = environment,
                StartupSeconds = 10,
                DeadlineSeconds = 15
            };
            var packets = new List<Packet>();
            SupervisedSample result = new Supervisor().Run(settings, Helpers.Sample(), packets.Add);
            Assert.Equal(Outcome.SetupFailure, result.Outcome);
            Assert.Equal("ArgumentException", result.Failure?.ExceptionType);
            Assert.DoesNotContain(packets, packet => packet.Kind == "ready");
            Assert.True(result.Reaped);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environment, null);
        }
    }

    /// <summary>Creates a synthetic request with a fresh connection ID to check connection-independent reuse.</summary>
    private static SqlAuthenticationParameters Parameters(
        string authority = "https://authority.example/tenant",
        string resource = "https://resource.example",
        string user = "test-user",
        string server = "test-server",
        string database = "test-database",
        SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryDefault) =>
        new(method, server, database, resource, authority, user, null, Guid.NewGuid(), 15);

    /// <summary>Counts synthetic provider invocations and allows deterministic completion control.</summary>
    private sealed class FakeProvider(Func<SqlAuthenticationParameters, Task<SqlAuthenticationToken>> acquire) : SqlAuthenticationProvider
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) =>
            authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDefault;
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            Interlocked.Increment(ref _calls);
            Started.TrySetResult(true);
            return acquire(parameters);
        }
    }
}
