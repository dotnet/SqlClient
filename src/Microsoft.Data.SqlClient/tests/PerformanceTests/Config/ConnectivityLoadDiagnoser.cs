// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Validators;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public sealed class ConnectivityLoadConfig : ManualConfig
    {
        public ConnectivityLoadConfig() => AddDiagnoser(ConnectivityLoadDiagnoser.Instance);
    }

    internal sealed class ConnectivityLoadDiagnoser : IDiagnoser
    {
        internal static readonly ConnectivityLoadDiagnoser Instance = new();
        private readonly Dictionary<BenchmarkCase, ConnectivityLoadResult> _results = new();
        private ConnectivityLoadResult _current;
        private bool _capturing;

        public IEnumerable<string> Ids => new[] { nameof(ConnectivityLoadDiagnoser) };
        public IEnumerable<IExporter> Exporters => Array.Empty<IExporter>();
        public IEnumerable<IAnalyser> Analysers => Array.Empty<IAnalyser>();
        public RunMode GetRunMode(BenchmarkCase benchmarkCase) => RunMode.NoOverhead;
        public void DisplayResults(ILogger logger) { }

        public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
        {
            if (signal == HostSignal.BeforeActualRun)
            {
                _current = default;
                _capturing = true;
            }
            else if (signal == HostSignal.AfterActualRun)
            {
                _capturing = false;
                if (_current.SuccessfulOpens > 0)
                {
                    BenchmarkCase benchmark = parameters.BenchmarkCase;
                    _results[benchmark] = _results.TryGetValue(benchmark, out ConnectivityLoadResult previous)
                        ? previous.Combine(_current)
                        : _current;
                }
            }
            else if (signal == HostSignal.AfterAll)
            {
                _capturing = false;
            }
        }

        internal void Record(ConnectivityLoadResult result)
        {
            if (_capturing)
            {
                _current = _current.Combine(result);
            }
        }

        public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
        {
            if (!_results.TryGetValue(results.BenchmarkCase, out ConnectivityLoadResult result))
            {
                throw new InvalidOperationException("No measured connectivity scorecard was captured.");
            }

            yield return Create("AverageOccupiedWorkers", "Avg workers", "Average occupied thread-pool workers", "workers", result.AverageOccupiedWorkers);
            yield return Create("WorkerMillisecondsPerOpen", "Worker ms/open", "Occupied worker milliseconds per successful open", "ms/open", result.WorkerMillisecondsPerOpen);
            yield return Create("OpensPerSecond", "Opens/sec", "Successful opens per second", "opens/s", result.OpensPerSecond, greaterIsBetter: true);
            yield return Create("PeakOccupiedWorkers", "Peak workers", "Peak occupied thread-pool workers", "workers", result.PeakOccupiedWorkers);
            yield return Create("PeakPoolThreads", "Peak TP threads", "Peak existing thread-pool threads including idle threads", "threads", result.PeakPoolThreads);
            yield return Create("PeakProcessThreads", "Peak process threads", "Peak process threads including dedicated threads and the observer", "threads", result.PeakProcessThreads);
            yield return Create("CpuMillisecondsPerOpen", "CPU ms/open", "Process CPU milliseconds per successful open", "ms/open", result.CpuMillisecondsPerOpen);
            yield return Create("SuccessfulOpens", "Opens", "Total successful opens in measured invocations", "opens", result.SuccessfulOpens, greaterIsBetter: true);
            yield return Create("OccupancySamples", "Samples", "Total occupancy samples in measured invocations", "samples", result.Samples, greaterIsBetter: true);
            yield return Create("MeasuredSeconds", "Measured sec", "Total measured seconds including in-flight operation drain", "s", result.ElapsedSeconds, greaterIsBetter: true);
        }

        public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
        {
            foreach (BenchmarkCase benchmark in validationParameters.Benchmarks)
            {
                if (benchmark.Descriptor.Type == typeof(ConnectivityLoadRunner)
                    && benchmark.Job.Infrastructure.Toolchain is not InProcessEmitToolchain)
                {
                    yield return new ValidationError(
                        true, "ConnectivityLoadDiagnoser requires the suite's in-process toolchain.", benchmark);
                }
            }
        }

        private static Metric Create(
            string id, string displayName, string legend, string unit, double value, bool greaterIsBetter = false) =>
            new(new Descriptor(id, displayName, legend, unit, greaterIsBetter), value);

        private sealed class Descriptor(
            string id, string displayName, string legend, string unit, bool greaterIsBetter) : IMetricDescriptor
        {
            public string Id => id;
            public string DisplayName => displayName;
            public string Legend => legend;
            public string NumberFormat => "0.000";
            public UnitType UnitType => UnitType.Dimensionless;
            public string Unit => unit;
            public bool TheGreaterTheBetter => greaterIsBetter;
            public int PriorityInCategory => 0;
            public bool GetIsAvailable(Metric metric) => true;
        }
    }
}
