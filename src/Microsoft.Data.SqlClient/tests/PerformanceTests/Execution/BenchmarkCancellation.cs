// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>
/// Converts Ctrl+C and Unix SIGTERM into a shutdown token for the perf harness. Suppressing
/// the default exit lets the runner stop its child process and dispose its network scope.
/// A forced kill bypasses this cleanup.
/// </summary>
internal sealed class BenchmarkCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly PosixSignalRegistration? _terminate;

    internal BenchmarkCancellation()
    {
        Console.CancelKeyPress += Cancel;
        if (!OperatingSystem.IsWindows())
        {
            _terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                _source.Cancel();
            });
        }
    }

    internal CancellationToken Token => _source.Token;

    private void Cancel(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        _source.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= Cancel;
        _terminate?.Dispose();
        _source.Dispose();
    }
}
