// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient.PerformanceTests;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>Provides mixed marked/unmarked parameterized cases without SQL or global fixtures.</summary>
public class ProcessPlanBenchmarkFixture
{
    [Params(1, 2)]
    public int Value { get; set; }

    [Benchmark]
    public int Ordinary() => Value;

    [Benchmark]
    [AsyncHealthDiagnoser]
    public int Observed() => Value;
}
#endif
