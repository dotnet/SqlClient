// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Turns cooperative termination into cancellation so child and network cleanup can run.</summary>
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
