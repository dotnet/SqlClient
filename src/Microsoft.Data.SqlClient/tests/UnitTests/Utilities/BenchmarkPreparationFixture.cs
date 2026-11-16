// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>Counts real BDN calls so extra preparation cannot silently multiply SQL-style batches.</summary>
public class BenchmarkPreparationFixture
{
    internal static int Calls;

    /// <summary>Represents a batch body whose calls must follow the configured preparation policy.</summary>
    [Benchmark]
    public void ExecuteBatch()
    {
        Interlocked.Increment(ref Calls);
        Thread.SpinWait(1000);
    }
}
#endif
