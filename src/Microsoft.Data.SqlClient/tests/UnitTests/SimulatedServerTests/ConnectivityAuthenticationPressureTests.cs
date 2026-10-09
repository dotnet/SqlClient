// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>Reproduces #2152/#2470's synchronous worker callers, rather than only isolated dedicated-thread authentication.</summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ConnectivityAuthenticationPressureTests
{
    private readonly ITestOutputHelper _output;
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    /// <summary>Records phase evidence without token values or connection strings.</summary>
    public ConnectivityAuthenticationPressureTests(ITestOutputHelper output) => _output = output;

    /// <summary>Known defect: synchronous worker callers must not prevent cold/expired provider work from being scheduled.</summary>
    [Theory]
    [Trait("category", "failing")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Open_ProviderAuthentication_WithCappedWorkerCallers_Completes(bool poolV2, bool expired) =>
        Run($"auth-sync-{(expired ? "expired" : "cold")}-capped-{(poolV2 ? "v2" : "v1")}");

    /// <summary>Generous workers and dedicated sync callers preserve provider progress; warm checkout needs no token work.</summary>
    [Theory]
    [InlineData("auth-sync-cold-generous-v1")]
    [InlineData("auth-sync-expired-generous-v1")]
    [InlineData("auth-sync-cold-generous-v2")]
    [InlineData("auth-sync-expired-generous-v2")]
    [InlineData("auth-dedicated-expired-capped-v1")]
    [InlineData("auth-dedicated-expired-capped-v2")]
    [InlineData("auth-sync-warm-capped-v1")]
    [InlineData("auth-sync-warm-capped-v2")]
    [InlineData("auth-async-expired-generous-v1")]
    [InlineData("auth-async-expired-generous-v2")]
    public Task Open_ProviderAuthentication_HealthyControls_Complete(string scenario) => Run(scenario);

    /// <summary>Observes real fed-auth traffic before testing scheduling and always frees child workers during recovery.</summary>
    private async Task Run(string scenario)
    {
        bool expired = scenario.Contains("-expired-");
        bool warm = scenario.Contains("-warm-");
        using var server = new GatedConnectionTdsServer(TdsConnectionPhase.FederatedAuthenticationInfo,
            new TdsServerArguments { FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired });
        server.Release();
        server.Start();
        using Process child = ConnectivityProcess.Start(scenario, server.EndPoint.Port);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        bool releaseSent = false;
        bool observing = false;
        int width = 0;
        try
        {
            string ready = await Read(child);
            if (expired)
            {
                Assert.StartsWith("EXPIRED_PHYSICAL_AND_CACHE ", ready);
                ready = await Read(child);
            }
            string[] parts = ready.Split(' ');
            Assert.Equal("READY", parts[0]);
            width = int.Parse(parts[1], CultureInfo.InvariantCulture);
            Assert.Equal(scenario.Contains("-generous-") ? width * 4 : width,
                int.Parse(parts[2], CultureInfo.InvariantCulture));
            int primed = warm || expired ? width : 0;
            Assert.Equal(primed, server.Login7Count);
            child.StandardInput.WriteLine("START");
            Assert.Equal($"CALLERS_STARTED {width}", await Read(child));
            if (!warm)
            {
                await Bounded(server.WaitForArrivalsAsync(primed + 1));
            }
            child.StandardInput.WriteLine("OBSERVE");
            observing = true;
            string tokenEntry = await Read(child);
            Assert.StartsWith("AVAILABLE ", await Read(child));
            Assert.Equal(warm ? "WARM_CALLBACKS 0" : "TOKEN_ENTERED", tokenEntry);
            child.StandardInput.WriteLine("RELEASE");
            releaseSent = true;
            string complete = await Read(child);
            Assert.StartsWith($"COMPLETE {width} CALLBACKS ", complete);
            int callbacks = int.Parse(complete.Substring(complete.LastIndexOf(' ') + 1), CultureInfo.InvariantCulture);
            Assert.InRange(callbacks, warm ? 0 : 1, warm ? 0 : width);
            Assert.Equal(warm ? width : primed + width, server.Login7Count);
            await Bounded(Task.Factory.StartNew(child.WaitForExit, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default));
            Assert.Equal(0, child.ExitCode);
            Assert.Equal(string.Empty, await errors);
        }
        finally
        {
            try
            {
                if (observing && !releaseSent && !child.HasExited)
                {
                    child.StandardInput.WriteLine("RECOVER");
                    Assert.Equal("WORKERS_RELEASED", await Read(child));
                    Assert.StartsWith($"COMPLETE {width} CALLBACKS ", await Read(child));
                    await Bounded(Task.Factory.StartNew(child.WaitForExit, CancellationToken.None,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default));
                    Assert.Equal(0, child.ExitCode);
                }
            }
            finally
            {
                ConnectivityProcess.Stop(child);
                _output.WriteLine(await errors);
            }
        }
    }

    /// <summary>Protocol reads are bounded by the independent parent, not the exhausted child's workers.</summary>
    private async Task<string> Read(Process child)
    {
        string line = await ConnectivityProcess.ReadAsync(child, Watchdog);
        _output.WriteLine(line);
        return line;
    }

    /// <summary>Bounds parent-side phase observation and surfaces missing phases.</summary>
    private static async Task Bounded(Task task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(Watchdog)));
        await task;
    }
}
