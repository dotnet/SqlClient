// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Running;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Describes an exact selection, without depending on diagnoser annotations or unit names.</summary>
internal sealed record BenchmarkProcessSelection(string Unit, int[] Indexes, string Fingerprint);

/// <summary>Partitions discovered cases according to the explicitly configured process boundary.</summary>
internal static class BenchmarkExecutionPlan
{
    internal static BenchmarkProcessSelection[] Create(
        string unit, BenchmarkCase[] cases, int[] selected, RunnerJob job)
    {
        job.ValidateExecution();
        ValidateIndexes(cases, selected);
        return job.ProcessIsolation == "PerCase"
            ? selected.Select(index => Selection(unit, cases, [index])).ToArray()
            : [Selection(unit, cases, selected)];
    }

    private static BenchmarkProcessSelection Selection(
        string unit, BenchmarkCase[] cases, int[] indexes) =>
        new(unit, indexes, Fingerprint(cases, indexes));

    internal static BenchmarkCase[] Select(
        BenchmarkProcessSelection selection, string unit, BenchmarkCase[] cases, RunnerJob job)
    {
        job.ValidateExecution();
        ValidateIndexes(cases, selection.Indexes);
        if (selection.Unit != unit ||
            (job.ProcessIsolation == "PerCase" && selection.Indexes.Length != 1) ||
            selection.Fingerprint != Fingerprint(cases, selection.Indexes))
        {
            throw new InvalidOperationException("Benchmark child selection changed during discovery.");
        }
        return selection.Indexes.Select(index => cases[index]).ToArray();
    }

    internal static void ValidateIndexes(BenchmarkCase[] cases, int[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        if (indexes.Length == 0 || indexes.Distinct().Count() != indexes.Length ||
            indexes.Any(index => index < 0 || index >= cases.Length))
        {
            throw new ArgumentException("Invalid benchmark case selection.", nameof(indexes));
        }
    }

    private static string Fingerprint(BenchmarkCase[] cases, IEnumerable<int> indexes)
    {
        string identity = JsonSerializer.Serialize(indexes.Select(index => new
        {
            Index = index,
            Type = cases[index].Descriptor.Type.FullName,
            Method = cases[index].Descriptor.WorkloadMethod.Name,
            Parameters = cases[index].Parameters.DisplayInfo,
            cases[index].Descriptor.OperationsPerInvoke
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    internal static BenchmarkCase WithOperationsPerInvoke(BenchmarkCase benchmark, int operations)
    {
        Descriptor old = benchmark.Descriptor;
        var descriptor = new Descriptor(old.Type, old.WorkloadMethod,
            old.GlobalSetupMethod, old.GlobalCleanupMethod,
            old.IterationSetupMethod, old.IterationCleanupMethod,
            old.WorkloadMethodDisplayInfo, old.Baseline, old.Categories,
            operations, old.MethodIndex);
        return BenchmarkCase.Create(descriptor, benchmark.Job, benchmark.Parameters, benchmark.Config);
    }
}
