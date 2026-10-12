// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Restricts supplementary samples to BDN's actual workload stage, not warmup or extra statistics.</summary>
    public sealed class OpenAsyncMeasurementDiagnoser : IDiagnoser
    {
        internal static volatile bool IsActual;
        public IEnumerable<string> Ids => new[] { nameof(OpenAsyncMeasurementDiagnoser) };
        public IEnumerable<IExporter> Exporters => Array.Empty<IExporter>();
        public IEnumerable<IAnalyser> Analysers => Array.Empty<IAnalyser>();
        public RunMode GetRunMode(BenchmarkCase benchmarkCase) => RunMode.NoOverhead;

        public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
        {
            if (signal == HostSignal.BeforeActualRun)
            {
                IsActual = true;
            }
            else if (signal == HostSignal.BeforeAnythingElse || signal == HostSignal.AfterActualRun ||
                signal == HostSignal.AfterAll || signal == HostSignal.AfterProcessExit)
            {
                IsActual = false;
            }
        }

        public IEnumerable<Metric> ProcessResults(DiagnoserResults results) => Array.Empty<Metric>();
        public void DisplayResults(ILogger logger) { }
        public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => Array.Empty<ValidationError>();
    }
}
