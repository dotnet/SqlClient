// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Text.Json;
using Xunit;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Checks optional capture isolation and cleanup without requiring server permissions.</summary>
public sealed class XEventIntegrationTests
{
    /// <summary>Capture is opt-in and dry-run parses its options without accessing credentials.</summary>
    [Fact]
    public void XEventsAreExplicitAndBounded()
    {
        Assert.False(new Settings().XEvents);
        Assert.Equal(0, CommandLine.Run(["--xevents", "--xevent-timeout", "3",
            "--xevent-connection-env", "observer-input", "--list"], (settings, list) =>
        {
            Assert.True(list);
            Assert.True(settings.XEvents);
            Assert.Equal(3, settings.XEventTimeoutSeconds);
            Assert.Equal("observer-input", settings.XEventConnectionEnvironment);
            return 0;
        }));
        foreach (Settings settings in new Settings[]
        {
            new() { XEventTimeoutSeconds = 0 }, new() { XEventTimeoutSeconds = 61 },
            new() { XEventConnectionEnvironment = "" }, new() { XEventConnectionEnvironment = "invalid=name" }
        })
            Assert.Throws<ArgumentException>(settings.Validate);
    }

    /// <summary>Count-only login capture is explicit and cannot silently replace server duration.</summary>
    [Fact]
    public void LoginCountEventRequiresExplicitSelection()
    {
        Assert.Equal("process_login_finish", new Settings().XEventEvent);
        Assert.Equal(0, CommandLine.Run(["--xevents", "--xevent-event", "login", "--list"], (settings, list) =>
        {
            Assert.True(list);
            Assert.True(settings.XEvents);
            Assert.Equal("login", settings.XEventEvent);
            return 0;
        }));
        Assert.Throws<ArgumentException>(() => new Settings { XEventEvent = "login" }.Validate());
        Assert.Throws<ArgumentException>(() => new Settings { XEvents = true, XEventEvent = "unknown" }.Validate());
    }

    /// <summary>Disabling capture creates no observer and preserves normal supervision.</summary>
    [Fact]
    public void DisabledCaptureDoesNotAccessObserver()
    {
        var result = new ObservedSampleRunner().Run(Helpers.Settings(), Helpers.Sample(), _ => { }, "success",
            (_, _, _) => throw new InvalidOperationException("Observer should not be constructed."));
        Assert.Equal(Outcome.Success, result.Outcome);
        Assert.Null(result.XEvents);
    }

    /// <summary>The parent cleans up after successful, failed, setup-failed, and killed child processes.</summary>
    [Theory]
    [InlineData("success", "Success")]
    [InlineData("failure", "Failed")]
    [InlineData("setup", "SetupFailure")]
    [InlineData("hang", "Incomplete")]
    public void CaptureSurvivesChildTermination(string fixture, string expected)
    {
        WithObserverEnvironment(settings =>
        {
            FakeCapture capture = new();
            string? application = null;
            var result = new ObservedSampleRunner().Run(settings, Helpers.Sample(), _ =>
            {
                Assert.True(capture.Started);
                Assert.False(capture.Finished);
            }, fixture, (_, name, _) =>
            {
                application = name;
                return capture;
            });
            Assert.Equal(Enum.Parse<Outcome>(expected), result.Outcome);
            Assert.True(result.Reaped);
            Assert.True(capture.Finished);
            Assert.True(result.XEvents!.SessionDropped);
            Assert.Equal(application, ConnectionFactory.WorkloadApplicationName(application));
            if (fixture == "hang") Assert.True(result.Killed);
        });
    }

    /// <summary>Unsupported telemetry prevents workload admission and still invokes observer cleanup.</summary>
    [Fact]
    public void UnsupportedCaptureIsExplicitAndDoesNotLaunchWorkload()
    {
        WithObserverEnvironment(settings =>
        {
            FakeCapture capture = new() { Unsupported = true };
            var result = new ObservedSampleRunner().Run(settings, Helpers.Sample(),
                _ => throw new InvalidOperationException("No child should be launched."),
                "success", (_, _, _) => capture);
            Assert.Equal(Outcome.SetupFailure, result.Outcome);
            Assert.Equal("unavailable", result.XEvents!.Status);
            Assert.Null(result.ExitCode);
            Assert.True(capture.Finished);
        });
    }

    /// <summary>Telemetry failure does not turn a successful workload into a false saturation boundary.</summary>
    [Fact]
    public void CaptureFailurePreservesWorkloadOutcome()
    {
        WithObserverEnvironment(settings =>
        {
            FakeCapture capture = new() { CaptureStatus = "incomplete" };
            var result = new ObservedSampleRunner().Run(settings, Helpers.Sample(), _ => { },
                "success", (_, _, _) => capture);
            Assert.Equal(Outcome.Success, result.Outcome);
            Assert.Equal("incomplete", result.XEvents!.Status);
        });
    }

    /// <summary>Each sample has its own filter identity, separate from non-pooled preflight logins.</summary>
    [Fact]
    public void SampleAndPreflightNamesAreIsolated()
    {
        HashSet<string> names = new();
        WithObserverEnvironment(settings =>
        {
            for (int i = 0; i < 2; i++)
            {
                new ObservedSampleRunner().Run(settings, Helpers.Sample(), _ => { },
                    "success", (_, name, _) =>
                    {
                        Assert.True(names.Add(name));
                        var preflight = ConnectionFactory.PreflightConnection($"Server=localhost;Application Name={name}");
                        Assert.False(preflight.Pooling);
                        Assert.NotEqual(name, preflight.ApplicationName);
                        var start = Supervisor.ChildStartInfo(settings, Helpers.Sample(), applicationName: name);
                        var config = JsonSerializer.Deserialize<ChildConfiguration>(start.Environment["SQLCLIENT_RAMP_CONFIG"]!, Wire.Json);
                        Assert.Equal(name, config!.ApplicationName);
                        return new FakeCapture();
                    });
            }
        });
        Assert.Throws<ArgumentException>(() => ConnectionFactory.WorkloadApplicationName("untrusted'input"));
    }

    /// <summary>Privileged observer credentials and their variable name never enter child configuration or results.</summary>
    [Fact]
    public void ObserverCredentialsStayInParent()
    {
        WithObserverEnvironment(settings =>
        {
            string name = settings.XEventConnectionEnvironment!;
            var start = Supervisor.ChildStartInfo(settings, Helpers.Sample());
            Assert.False(start.Environment.ContainsKey(name));
            Assert.DoesNotContain(name, start.Environment["SQLCLIENT_RAMP_CONFIG"]);
            Assert.DoesNotContain("test-observer-value", JsonSerializer.Serialize(settings, Wire.Json));
        });
    }

    /// <summary>Dedicated callers keep one real thread per worker across repeated opens and disposal.</summary>
    [Fact]
    public async Task DedicatedWorkersDoNotRunOpensOnThreadPool()
    {
        ThreadTrackingFactory factory = new();
        var result = await Helpers.Run(Helpers.Sample() with { Caller = CallerMode.SyncDedicated, Concurrency = 3 }, factory);
        Assert.Equal(Outcome.Success, result.Outcome);
        Assert.Equal(3, factory.Threads.Count);
        Assert.All(factory.Threads.Values, count => Assert.True(count > 1));
        Assert.False(factory.UsedThreadPool);
        Assert.False(factory.DisposedOnAnotherThread);
    }

    /// <summary>Restores environment state after fixture supervision without creating real connections.</summary>
    /// <param name="action">Assertions using bounded fixture settings and a non-secret observer input.</param>
    private static void WithObserverEnvironment(Action<Settings> action)
    {
        const string name = "SQLCLIENT_RAMP_TEST_OBSERVER";
        string? original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "test-observer-value");
            action(Helpers.Settings() with
            {
                XEvents = true, XEventConnectionEnvironment = name,
                StartupSeconds = 3, DeadlineSeconds = 4, CleanupSeconds = 0.2
            });
        }
        finally { Environment.SetEnvironmentVariable(name, original); }
    }

    /// <summary>Records parent-side lifecycle calls while keeping the database out of fixture tests.</summary>
    private sealed class FakeCapture : ILoginXEventCapture
    {
        public bool Unsupported { get; init; }
        public string CaptureStatus { get; init; } = "captured";
        public bool Started { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>Models unsupported server metadata after observer construction.</summary>
        public void Start()
        {
            Started = true;
            if (Unsupported) throw new NotSupportedException("test-redaction-marker");
        }

        /// <summary>Records cleanup and returns only allowlisted capture metadata.</summary>
        /// <returns>A fixture capture result independent of the workload result.</returns>
        public LoginXEventResult Finish()
        {
            Finished = true;
            return LoginXEventResult.NotStarted(CaptureStatus, null);
        }
    }

    /// <summary>Observes scheduler identity without adding task dispatch to the measured loop.</summary>
    private sealed class ThreadTrackingFactory : IConnectionFactory
    {
        public ConcurrentDictionary<int, int> Threads { get; } = new();
        public volatile bool UsedThreadPool;
        public volatile bool DisposedOnAnotherThread;

        /// <summary>Creates an operation bound to the thread that owns its lifecycle.</summary>
        /// <returns>An independently tracked connection.</returns>
        public IConnection Create() => new Connection(this, Environment.CurrentManagedThreadId);

        /// <summary>Rejects pool clearing in the continuous workload.</summary>
        public void ClearPool() => throw new InvalidOperationException();

        /// <summary>Tracks repeated synchronous opens and same-thread disposal.</summary>
        private sealed class Connection(ThreadTrackingFactory owner, int threadId) : IConnection
        {
            /// <summary>Records real thread identity with a short delay to allow peer scheduling.</summary>
            public void Open()
            {
                if (Thread.CurrentThread.IsThreadPoolThread) owner.UsedThreadPool = true;
                owner.Threads.AddOrUpdate(threadId, 1, (_, count) => count + 1);
                Thread.Sleep(1);
            }

            /// <summary>Rejects async operations in the dedicated synchronous workload.</summary>
            /// <param name="cancellationToken">Unused cancellation token.</param>
            /// <returns>No task, because this caller must use Open.</returns>
            public Task OpenAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();

            /// <summary>Checks that disposal stays on the persistent worker thread.</summary>
            public void Dispose()
            {
                if (Environment.CurrentManagedThreadId != threadId) owner.DisposedOnAnotherThread = true;
            }
        }
    }
}
