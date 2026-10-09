// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The attempt counter observes managed-SNI SniTcpHandle traces; .NET Framework always uses native SNI.
#if NET
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>Counts real TCP attempts after public cancellation, rather than equating a cancelled task with stopped physical work (#1619).</summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ConnectivityTransportCancellationTests
{
    private readonly ITestOutputHelper _output;
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    /// <summary>Records physical counts at the cancellation boundary and at actual creation completion.</summary>
    public ConnectivityTransportCancellationTests(ITestOutputHelper output) => _output = output;

    /// <summary>Proves that refused TCP connects reach internal retries with configured retries disabled.</summary>
    [Fact]
    public Task OpenAsync_RefusedConnect_InternalRetryControl_ReachesMultipleAttempts() => Run(false);

    /// <summary>Known defect: no new attempt for a cancelled caller should start after its held first connect failure.</summary>
    [Fact]
    [Trait("category", "failing")]
    public Task OpenAsync_RefusedConnect_AfterPublicCancellation_StartsNoNewAttempt() => Run(true);

    /// <summary>Only releases the first failed transport after public cancellation has been confirmed.</summary>
    private async Task Run(bool cancel)
    {
        using var endpoint = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        endpoint.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)endpoint.LocalEndPoint!).Port;
        // A bound, non-listening socket can silently drop SYNs on macOS; close it to produce refusal.
        endpoint.Dispose();
        using Process child = ConnectivityProcess.Start(cancel ? "attempt-cancel" : "attempt-control", port);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        try
        {
            Assert.Equal("CALL_RETURNED", await Read(child));
            Assert.Equal("FAILURE_HELD ATTEMPTS 1", await Read(child));
            child.StandardInput.WriteLine(cancel ? "CANCEL" : "CONTINUE");
            Assert.Equal(cancel ? "PUBLIC_CANCELLED" : "RETRY_ALLOWED", await Read(child));
            const int before = 1;
            _output.WriteLine($"ATTEMPTS_AT_PUBLIC_BOUNDARY {before}");
            child.StandardInput.WriteLine("FINISH");
            if (!cancel)
            {
                Assert.StartsWith("NETWORK_FAILURE ", await Read(child));
            }
            string completion = await Read(child);
            Assert.StartsWith("PHYSICAL_COMPLETE ATTEMPTS ", completion);
            int attempts = int.Parse(completion.Substring(completion.LastIndexOf(' ') + 1),
                System.Globalization.CultureInfo.InvariantCulture);
            await Bounded(Task.Factory.StartNew(child.WaitForExit, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default));
            Assert.Equal(0, child.ExitCode);
            Assert.Equal(string.Empty, await errors);
            _output.WriteLine($"ATTEMPTS_AT_PHYSICAL_COMPLETION {attempts}");
            if (cancel)
            {
                Assert.Equal(before, attempts);
            }
            else
            {
                Assert.True(attempts >= 2, "Transport faults did not reach the intended internal retry path.");
            }
        }
        finally
        {
            ConnectivityProcess.Stop(child);
            _output.WriteLine(await errors);
        }
    }

    /// <summary>Bounds child protocol reads independently of the physical connection operation.</summary>
    private async Task<string> Read(Process child)
    {
        string line = await ConnectivityProcess.ReadAsync(child, Watchdog);
        _output.WriteLine(line);
        return line;
    }

    /// <summary>Fails on a missed phase; transport-loop failures are observed rather than swallowed.</summary>
    private static async Task Bounded(Task task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(Watchdog)));
        await task;
    }
}
#endif
