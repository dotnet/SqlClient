// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ConnectivityTestHost;

/// <summary>Test-owned client process: global worker caps and deadlocks must not poison xUnit.</summary>
internal static class Program
{
    /// <summary>Executes a local reproduction. The parent owns gates, assertions and the watchdog.</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Expected a scenario and one numeric argument.");
        }
        if (args[0] == "protocol-idle")
        {
            Console.WriteLine("READY");
            if (Console.ReadLine() != "STOP")
            {
                throw new InvalidOperationException("Unexpected protocol command.");
            }
            return 0;
        }
        int port = int.Parse(args[1], CultureInfo.InvariantCulture);
        if (port <= 0 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
        bool poolV2 = args[0].EndsWith("-v2", StringComparison.Ordinal);
        bool pooling = !args[0].EndsWith("-nonpooled", StringComparison.Ordinal);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", poolV2);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
        if (args[0] == "enlist-control" || args[0] == "enlist-cancel")
        {
            EnlistmentCancellationScenario.Run(port, args[0] == "enlist-cancel");
            return 0;
        }
        if (args[0] == "enlist-before-cancel" || args[0] == "enlist-disabled-cancel")
        {
            EnlistmentCancellationScenario.RunBeforeEnlistment(port, args[0] == "enlist-before-cancel");
            return 0;
        }
        if (args[0] == "attempt-cancel" || args[0] == "attempt-control")
        {
            TransportAttemptScenario.Run(port, args[0] == "attempt-cancel");
            return 0;
        }
        if (args[0].StartsWith("auth-", StringComparison.Ordinal))
        {
            AuthenticationPressureScenario.Run(port, args[0]);
            return 0;
        }
        string[] mode = args[0].Split('-');
        if (mode.Length != 3 || mode[0] != "workers" ||
            (mode[1] != "capped" && mode[1] != "generous" && mode[1] != "additional") ||
            (mode[1] == "additional" && mode[2] != "v2") ||
            (mode[2] != "v1" && mode[2] != "v2" && mode[2] != "nonpooled"))
        {
            throw new ArgumentException("Unsupported connectivity scenario.");
        }
        int width = Math.Max(Environment.ProcessorCount, 4);
        ThreadPool.GetMinThreads(out int oldMin, out int minIo);
        ThreadPool.GetMaxThreads(out _, out int maxIo);
        int maximum = mode[1] == "generous" ? width * 4 : width;
        if (!ThreadPool.SetMinThreads(Math.Min(oldMin, width), minIo) ||
            !ThreadPool.SetMaxThreads(maximum, maxIo) ||
            !ThreadPool.SetMinThreads(width, minIo))
        {
            throw new InvalidOperationException("Runtime rejected the requested worker limits.");
        }
        ThreadPool.GetMinThreads(out int actualMin, out _);
        ThreadPool.GetMaxThreads(out int actualMax, out _);
        if (actualMin != width || actualMax != maximum)
        {
            throw new InvalidOperationException("Runtime worker limits differ from the requested limits.");
        }

        // An async-only workload must progress under the same accepted cap.
        Task.WhenAll(Enumerable.Range(0, width).Select(async _ => await Task.Yield())).GetAwaiter().GetResult();
        Console.WriteLine($"READY {width} {actualMin} {actualMax}");
        var token = new TaskCompletionSource<SqlAuthenticationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        SqlConnection[] connections = Enumerable.Range(0, width).Select(_ =>
        {
            var connection = new SqlConnection(new SqlConnectionStringBuilder(ConnectionString(port)) { Pooling = pooling }.ConnectionString);
            if (mode[1] != "additional")
            {
                connection.AccessTokenCallback = (_, _) =>
                {
                    Console.WriteLine("TOKEN_ENTERED");
                    return token.Task;
                };
            }
            return connection;
        }).ToArray();
        Task[] opens = connections.Select(connection => connection.OpenAsync()).ToArray();
        Console.WriteLine("CALLS_RETURNED");
        if (Console.ReadLine() != "PROBE")
        {
            throw new InvalidOperationException("Parent did not request the occupancy probe.");
        }
        ThreadPool.GetAvailableThreads(out int available, out _);
        Console.WriteLine($"AVAILABLE {available}");
        if (mode[1] == "additional")
        {
            RunAdditionalOpen(connections, opens);
            return 0;
        }
        if (Console.ReadLine() != "RELEASE")
        {
            throw new InvalidOperationException("Parent did not release authentication.");
        }
        token.SetResult(new SqlAuthenticationToken("test-token", DateTimeOffset.UtcNow.AddHours(2)));
        Task.WhenAll(opens).GetAwaiter().GetResult();
        foreach (SqlConnection connection in connections)
        {
            connection.Dispose();
        }
        Console.WriteLine($"COMPLETE {width}");
        return 0;
    }

    /// <summary>Calls OpenAsync on the non-pool main thread after earlier opens occupy every worker.</summary>
    private static void RunAdditionalOpen(SqlConnection[] connections, Task[] opens)
    {
        string command = Console.ReadLine();
        string[] parts = command?.Split(' ');
        if (parts is null || parts.Length != 2 || parts[0] != "OPEN" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
            port <= 0 || port > 65535)
        {
            throw new InvalidOperationException("Parent did not provide the additional loopback endpoint.");
        }
        using var additional = new SqlConnection(ConnectionString(port));
        Console.WriteLine("ADDITIONAL_CALL_ENTERED");
        Task open = additional.OpenAsync();
        Console.WriteLine("ADDITIONAL_CALL_RETURNED");
        Console.WriteLine($"ADDITIONAL_TASK_PENDING {!open.IsCompleted}");
        open.GetAwaiter().GetResult();
        Console.WriteLine("ADDITIONAL_COMPLETE");
        Task.WhenAll(opens).GetAwaiter().GetResult();
        foreach (SqlConnection connection in connections)
        {
            connection.Dispose();
        }
        Console.WriteLine($"COMPLETE {connections.Length + 1}");
    }

    /// <summary>Uses only the parent's fake loopback peer; no credentials enter process arguments.</summary>
    private static string ConnectionString(int port) => new SqlConnectionStringBuilder
    {
        DataSource = $"127.0.0.1,{port}",
        Encrypt = SqlConnectionEncryptOption.Optional,
        Pooling = true,
        MaxPoolSize = Math.Max(200, Environment.ProcessorCount * 4),
        ConnectTimeout = 120,
        ConnectRetryCount = 0,
        Enlist = false
    }.ConnectionString;
}
