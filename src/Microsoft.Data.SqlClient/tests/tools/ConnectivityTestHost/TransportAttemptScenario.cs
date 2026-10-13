// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ConnectivityTestHost;

/// <summary>Separates public cancellation from completion of the actual non-pooled physical-creation task.</summary>
internal static class TransportAttemptScenario
{
    /// <summary>Counts actual socket connect starts against an unused loopback port; no configurable retry or query is involved.</summary>
    internal static void Run(int port, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        using var trace = new ConnectTrace();
        using var connection = new SqlConnection(new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            Enlist = false,
            ConnectRetryCount = 0,
            ConnectTimeout = 10
        }.ConnectionString);
        Task open = connection.OpenAsync(cancellation.Token);
        Console.WriteLine("CALL_RETURNED");
        if (!trace.FirstFailure.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException($"The endpoint did not produce a traced connect failure. Attempts: {trace.Attempts}; last phase: {trace.LastPhase}.");
        }
        Console.WriteLine($"FAILURE_HELD ATTEMPTS {trace.Attempts}");
        string command = Console.ReadLine();
        if (command != (cancel ? "CANCEL" : "CONTINUE"))
        {
            throw new InvalidOperationException("Parent did not issue the selected attempt command.");
        }
        if (cancel)
        {
            cancellation.Cancel();
            try
            {
                open.GetAwaiter().GetResult();
                throw new InvalidOperationException("The public open unexpectedly succeeded.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                Console.WriteLine("PUBLIC_CANCELLED");
            }
        }
        else
        {
            Console.WriteLine("RETRY_ALLOWED");
        }
        if (Console.ReadLine() != "FINISH")
        {
            throw new InvalidOperationException("Parent did not release the transport failure.");
        }
        trace.Release.Set();
        WaitForPhysicalCompletion();
        if (!cancel)
        {
            try
            {
                open.GetAwaiter().GetResult();
                throw new InvalidOperationException("The unused endpoint unexpectedly accepted login.");
            }
            catch (SqlException exception)
            {
                Console.WriteLine($"NETWORK_FAILURE {exception.Number}");
            }
        }
        if (trace.GateTimedOut)
        {
            throw new TimeoutException("The controller did not release the traced connect failure.");
        }
        Console.WriteLine($"PHYSICAL_COMPLETE ATTEMPTS {trace.Attempts}");
    }

    /// <summary>Observes the source's actual non-pooled creation tasks, not just a cancelled public task.</summary>
    internal static void WaitForPhysicalCompletion()
    {
        Type factory = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.SqlConnectionFactory", throwOnError: true);
        FieldInfo field = factory.GetField("s_pendingOpenNonPooled", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("Non-pooled physical-creation tasks are unavailable.");
        if (field.GetValue(null) is not Array pending)
        {
            throw new InvalidOperationException("Non-pooled creation task array is unavailable.");
        }
        Task[] physical = pending.Cast<Task>().Where(task => task != null).ToArray();
        if (physical.Length == 0)
        {
            throw new InvalidOperationException("No physical-creation task was observed.");
        }
        if (!SpinWait.SpinUntil(() => physical.All(task => task.IsCompleted), TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("Physical creation did not finish within the cleanup watchdog.");
        }
        foreach (Task task in physical.Where(task => task.IsFaulted))
        {
            _ = task.Exception;
        }
    }

    /// <summary>Gates an existing error trace after a real refused connect; it does not replace transport or configure SQL-error retries.</summary>
    private sealed class ConnectTrace : EventListener
    {
        internal readonly ManualResetEventSlim FirstFailure = new();
        internal readonly ManualResetEventSlim Release = new();
        private int _attempts;
        private int _failed;
        internal int Attempts => Volatile.Read(ref _attempts);
        internal bool GateTimedOut;
        internal string LastPhase = "none";

        /// <summary>Enables only managed-SNI tracing; payloads are filtered in memory and never printed.</summary>
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)(2048 | 128));
            }
        }

        /// <summary>Counts actual handle connect starts and holds only the first observed socket failure.</summary>
        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if ((data.EventName != "SNITrace" && data.EventName != "AdvancedTrace") || data.Payload?.FirstOrDefault() is not string message ||
                !message.StartsWith("SniTcpHandle.", StringComparison.Ordinal))
            {
                return;
            }
            if (message.Contains("Connecting to IP address"))
            {
                Interlocked.Increment(ref _attempts);
            }
            int separator = message.IndexOf('|');
            LastPhase = separator >= 0 ? message.Substring(0, separator) : data.EventName;
            if ((message.IndexOf("ConnectionRefused", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("Connection refused", StringComparison.OrdinalIgnoreCase) >= 0) &&
                Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
            {
                FirstFailure.Set();
                GateTimedOut = !Release.Wait(TimeSpan.FromSeconds(30));
            }
        }
    }
}
