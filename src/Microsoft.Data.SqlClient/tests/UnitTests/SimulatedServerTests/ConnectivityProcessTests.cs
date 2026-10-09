// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>Parent-owned isolation for the two retained worker-starvation reproductions.</summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ConnectivityProcessTests
{
    private readonly ITestOutputHelper _output;
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    /// <summary>Routes child output into the existing xUnit result stream.</summary>
    public ConnectivityProcessTests(ITestOutputHelper output) => _output = output;

    /// <summary>A ready child deliberately receives no command: a missed protocol phase fails and cleanup reaps it.</summary>
    [Fact]
    public async Task Process_MissingProtocolPhase_TimesOutAndIsReaped()
    {
        using Process child = Start("protocol-idle", 0);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        try
        {
            Assert.Equal("READY", await Read(child));
            await Assert.ThrowsAsync<TimeoutException>(() =>
                ConnectivityProcess.ReadAsync(child, TimeSpan.FromSeconds(1)));
        }
        finally
        {
            Stop(child);
            Assert.True(child.HasExited);
            Assert.Equal(string.Empty, await errors);
        }
    }

    /// <summary>The same cold token burst completes when enough workers remain for token tasks.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public Task OpenAsync_TokenBurst_WithSufficientWorkers_Completes(bool poolV2, bool pooling) =>
        TokenBurst(false, poolV2, pooling);

    /// <summary>Dedicated V1/non-pooled creation is compared with V2 under the same accepted cap.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public Task OpenAsync_TokenBurst_WithCappedDedicatedCreation_Completes(bool poolV2, bool pooling) =>
        TokenBurst(true, poolV2, pooling);

    /// <summary>
    /// Known #3459 repro: all V2 creation workers reach Login7 before authentication is released.
    /// Keep explicitly selected until physical open/token acquisition no longer blocks workers.
    /// </summary>
    [Fact]
    [Trait("category", "failing")]
    public Task OpenAsync_TokenBurst_WithCappedWorkers_Completes() => TokenBurst(true, true, true);

    /// <summary>Covers the #3459/#3118 worker-starvation mechanism: an independent open must progress while earlier logins hold every V2 worker.</summary>
    [Fact]
    [Trait("category", "failing")]
    public Task OpenAsync_AdditionalOpen_WithCappedWorkers_MakesProgress() => AdditionalOpen(false);

    /// <summary>Releasing held plain logins frees workers and permits the additional call to finish without changing the cap.</summary>
    [Fact]
    public Task OpenAsync_AdditionalOpen_AfterHeldLoginsReleased_Completes() => AdditionalOpen(true);

    /// <summary>Separates invocation return from wire progress; a second unheld endpoint excludes pool capacity and server gating.</summary>
    private async Task AdditionalOpen(bool releaseHeldLogins)
    {
        using var held = new GatedConnectionTdsServer(TdsConnectionPhase.Login7);
        using var additional = new GatedConnectionTdsServer(TdsConnectionPhase.Login7);
        additional.Release();
        held.Start();
        additional.Start();
        using Process child = Start("workers-additional-v2", held.EndPoint.Port);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        Task additionalArrival = additional.WaitForArrivalsAsync(1);
        bool callReturned = false;
        bool additionalCompleted = false;
        int width = 0;
        try
        {
            string[] ready = (await Read(child)).Split(' ');
            Assert.Equal("READY", ready[0]);
            width = int.Parse(ready[1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(width.ToString(System.Globalization.CultureInfo.InvariantCulture), ready[2]);
            Assert.Equal(width.ToString(System.Globalization.CultureInfo.InvariantCulture), ready[3]);
            Assert.Equal("CALLS_RETURNED", await Read(child));
            await Bounded(held.WaitForArrivalsAsync(width));
            child.StandardInput.WriteLine("PROBE");
            string occupancy = await Read(child);
            Assert.StartsWith("AVAILABLE ", occupancy);
            Assert.InRange(int.Parse(occupancy.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture), 0, width);

            child.StandardInput.WriteLine($"OPEN {additional.EndPoint.Port}");
            Assert.Equal("ADDITIONAL_CALL_ENTERED", await Read(child));
            Assert.Equal("ADDITIONAL_CALL_RETURNED", await Read(child));
            callReturned = true;
            string pending = await Read(child);
            Assert.StartsWith("ADDITIONAL_TASK_PENDING ", pending);
            Assert.True(bool.TryParse(pending.Split(' ')[1], out _));
            _output.WriteLine($"ADDITIONAL_LOGIN_ARRIVALS_BEFORE_RELEASE {additional.ArrivalCount}");
            if (releaseHeldLogins)
            {
                _output.WriteLine("RELEASING_HELD_LOGINS");
                held.Release();
            }
            await Bounded(additionalArrival);
            Assert.Equal("ADDITIONAL_COMPLETE", await Read(child));
            additionalCompleted = true;
        }
        finally
        {
            held.Release();
            _output.WriteLine("HELD_LOGINS_RELEASED_DURING_CLEANUP");
            try
            {
                if (callReturned)
                {
                    await Bounded(additionalArrival);
                    if (!additionalCompleted)
                    {
                        Assert.Equal("ADDITIONAL_COMPLETE", await Read(child));
                    }
                    Assert.Equal($"COMPLETE {width + 1}", await Read(child));
                    Assert.Equal(width, held.ArrivalCount);
                    Assert.Equal(1, additional.ArrivalCount);
                    await Bounded(Task.Factory.StartNew(child.WaitForExit, CancellationToken.None,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default));
                    Assert.Equal(0, child.ExitCode);
                    Assert.Equal(string.Empty, await errors);
                }
            }
            finally
            {
                Stop(child);
                _output.WriteLine(await errors);
            }
        }
    }

    /// <summary>Runs a gated cold burst, checking accepted limits and actual occupied workers.</summary>
    private async Task TokenBurst(bool capped, bool poolV2, bool pooling)
    {
        using var server = new GatedConnectionTdsServer(TdsConnectionPhase.Login7, new TdsServerArguments
        {
            FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired
        });
        server.Start();
        string mode = pooling ? poolV2 ? "v2" : "v1" : "nonpooled";
        using Process child = Start($"workers-{(capped ? "capped" : "generous")}-{mode}", server.EndPoint.Port);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        try
        {
            string[] ready = (await Read(child)).Split(' ');
            Assert.Equal("READY", ready[0]);
            int width = int.Parse(ready[1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(width.ToString(System.Globalization.CultureInfo.InvariantCulture), ready[2]);
            Assert.Equal(capped ? width : width * 4, int.Parse(ready[3], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("CALLS_RETURNED", await Read(child));
            await Bounded(server.WaitForArrivalsAsync(poolV2 && pooling ? width : 1));
            child.StandardInput.WriteLine("PROBE");
            string occupancy = await Read(child);
            Assert.StartsWith("AVAILABLE ", occupancy);
            Assert.InRange(int.Parse(occupancy.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture),
                0, capped ? width : width * 4);
            server.Release();
            Assert.Equal("TOKEN_ENTERED", await Read(child));
            child.StandardInput.WriteLine("RELEASE");
            int tokenCalls = 1;
            string progress;
            while ((progress = await Read(child)) == "TOKEN_ENTERED")
            {
                tokenCalls++;
            }
            Assert.InRange(tokenCalls, 1, width);
            Assert.Equal($"COMPLETE {width}", progress);
            Assert.Equal(width, server.ArrivalCount);
            await Bounded(Task.Factory.StartNew(child.WaitForExit, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default));
            Assert.Equal(0, child.ExitCode);
            Assert.Equal(string.Empty, await errors);
        }
        finally
        {
            Stop(child);
            server.Release();
            _output.WriteLine(await errors);
        }
    }

    /// <summary>Launches only the copied test helper, never a shell or a server on a remote interface.</summary>
    private static Process Start(string scenario, int port)
        => ConnectivityProcess.Start(scenario, port);

    /// <summary>Reads bounded protocol lines and fails on EOF; missing phases are never a passing repro.</summary>
    private async Task<string> Read(Process process)
    {
        string line = await ConnectivityProcess.ReadAsync(process, Watchdog);
        _output.WriteLine(line);
        return line;
    }

    /// <summary>Uses a parent watchdog which is independent of the capped/deadlocked client.</summary>
    private static async Task Bounded(Task task)
    {
        Task winner = await Task.WhenAny(task, Task.Delay(Watchdog));
        if (!ReferenceEquals(task, winner))
        {
            _ = task.ContinueWith(failed => { _ = failed.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        Assert.True(ReferenceEquals(task, winner),
            "Connectivity client missed the expected phase/completion within the parent watchdog.");
        await task;
    }

    /// <summary>Reaps the owned process on success or failure before stopping the separately hosted server.</summary>
    private static void Stop(Process process) => ConnectivityProcess.Stop(process);
}
