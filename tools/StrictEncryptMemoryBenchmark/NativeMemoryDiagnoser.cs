using System.Diagnostics;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace StrictEncryptMemoryBenchmark;

/// <summary>
/// Custom BenchmarkDotNet diagnoser that captures native/private memory metrics.
/// This is critical for detecting the SChannel TLS session ticket cache leak,
/// which manifests in native heap (not managed GC heap).
/// </summary>
public class NativeMemoryDiagnoser : IDiagnoser
{
    private long _privateBytesBefore;
    private long _privateBytesAfter;
    private long _workingSetBefore;
    private long _workingSetAfter;

    public IEnumerable<string> Ids => ["NativeMemory"];

    public IEnumerable<IExporter> Exporters => [];

    public IEnumerable<IAnalyser> Analysers => [];

    public RunMode GetRunMode(BenchmarkCase benchmarkCase) => RunMode.NoOverhead;

    public bool RequiresBlockingAcknowledgments(BenchmarkCase benchmarkCase) => false;

    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        var process = Process.GetCurrentProcess();

        switch (signal)
        {
            case HostSignal.BeforeActualRun:
                process.Refresh();
                _privateBytesBefore = process.PrivateMemorySize64;
                _workingSetBefore = process.WorkingSet64;
                break;

            case HostSignal.AfterActualRun:
                process.Refresh();
                _privateBytesAfter = process.PrivateMemorySize64;
                _workingSetAfter = process.WorkingSet64;
                break;
        }
    }

    public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
    {
        yield return new Metric(
            new MetricDescriptor("NativeMemGrowth", "Native Mem Growth", "MB"),
            (_privateBytesAfter - _privateBytesBefore) / (1024.0 * 1024.0));

        yield return new Metric(
            new MetricDescriptor("PrivateBytesAfter", "Private Bytes After", "MB"),
            _privateBytesAfter / (1024.0 * 1024.0));

        yield return new Metric(
            new MetricDescriptor("WorkingSetAfter", "Working Set After", "MB"),
            _workingSetAfter / (1024.0 * 1024.0));
    }

    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => [];

    public void DisplayResults(ILogger logger)
    {
        logger.WriteLineInfo($"Native Memory Growth: {(_privateBytesAfter - _privateBytesBefore) / (1024.0 * 1024.0):F2} MB");
        logger.WriteLineInfo($"Private Bytes After:  {_privateBytesAfter / (1024.0 * 1024.0):F2} MB");
    }

    private class MetricDescriptor : IMetricDescriptor
    {
        public MetricDescriptor(string id, string displayName, string unit)
        {
            Id = id;
            DisplayName = displayName;
            Unit = unit;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Unit { get; }
        public string Legend => $"{DisplayName} ({Unit})";
        public string NumberFormat => "F2";
        public UnitType UnitType => UnitType.Size;
        public bool TheGreaterTheBetter => false;
        public int PriorityInCategory => 0;
        public bool GetIsAvailable(Metric metric) => true;
    }
}
