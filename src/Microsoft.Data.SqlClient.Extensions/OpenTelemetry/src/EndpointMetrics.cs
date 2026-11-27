// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Metrics;
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace Microsoft.Data.SqlClient.Extensions.OpenTelemetry;

/// <summary>
/// Operation metrics for one private endpoint. They are recorded only for scopes routed to
/// that endpoint, so the driver's process-wide meter (which an application-owned SDK may
/// collect) is never exported through a private route and endpoints cannot observe each other.
/// </summary>
internal sealed class EndpointMetrics : IDisposable
{
    internal const string MeterNamePrefix = "Microsoft.Data.SqlClient.Endpoint.";
    internal const string DroppedName = "mssqldriver.telemetry.dropped";
    internal const string DroppedReason = "reason";
    internal const string ExportFailed = "export_failed";
    private const string DbSystemName = "db.system.name";
    // Metric labels are shared with the JDBC driver (mssql.*). Keep these in sync with the
    // driver's OperationMetrics.
    private const string Phase = "mssql.performance.activity";
    private const string Kind = "mssql.operation.kind";
    private const string SchemaVersion = "mssql.telemetry.schema.version";
    private const string MetricOutcome = "mssql.operation.outcome";
    private const string MetricCategory = "mssql.error.category";
    private const string SchemaVersionValue = "1.0";
    private const string RetryReason = "mssql.retry.reason";
    private static int s_nextId;

    private readonly Meter _meter;
    private readonly MeterProvider _provider;
    private readonly Counter<long> _operations;
    private readonly Histogram<double> _durations;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _dropped;

    private EndpointMetrics(Uri metricsEndpoint, int exportIntervalMilliseconds)
    {
        string name = MeterNamePrefix + Interlocked.Increment(ref s_nextId);
        _meter = new Meter(name);
        _operations = _meter.CreateCounter<long>("db.client.operation.count", "{operation}");
        _durations = _meter.CreateHistogram<double>("db.client.operation.duration", "s");
        _errors = _meter.CreateCounter<long>("db.client.operation.error.count", "{error}");
        _retries = _meter.CreateCounter<long>("db.client.operation.retry.count", "{retry}");
        _dropped = _meter.CreateCounter<long>(DroppedName, "{span}");
        _provider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(DriverResource.Create())
            .AddMeter(name)
            .AddOtlpExporter((exporter, reader) =>
            {
                exporter.Endpoint = metricsEndpoint;
                exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                exporter.Headers = string.Empty;
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                    exportIntervalMilliseconds;
            })
            .AddView("db.client.operation.duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = DurationBuckets })
            .Build();
    }

    // Seconds-based boundaries matching the driver's OperationMetrics advice. Without them the
    // SDK default (0..10000, intended for milliseconds) puts every operation in one bucket.
    internal static readonly double[] DurationBuckets =
    {
        0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1,
        2.5, 5, 10, 30
    };

    internal string MeterName => _meter.Name;

    /// <summary>
    /// Creates metrics for a trace endpoint ending in /v1/traces, exporting to the sibling
    /// /v1/metrics path. Other endpoint shapes get no private metrics because the metrics path
    /// cannot be derived without guessing.
    /// </summary>
    internal static EndpointMetrics? Create(Uri traceEndpoint, int exportIntervalMilliseconds)
    {
        Uri? metricsEndpoint = MetricsEndpoint(traceEndpoint);
        return metricsEndpoint is null
            ? null
            : new EndpointMetrics(metricsEndpoint, exportIntervalMilliseconds);
    }

    internal static Uri? MetricsEndpoint(Uri traceEndpoint)
    {
        const string tracesPath = "/v1/traces";
        string path = traceEndpoint.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith(tracesPath, StringComparison.Ordinal))
        {
            return null;
        }
        UriBuilder builder = new(traceEndpoint)
        {
            Path = path.Substring(0, path.Length - tracesPath.Length) + "/v1/metrics"
        };
        return builder.Uri;
    }

    /// <summary>
    /// Records a completed routed scope (root, attempt or phase) with the same bounded labels as
    /// the driver's process-wide meter. The driver calls this at scope end, whether or not a
    /// span was sampled, so private metrics are never sampled. Retries are counted on roots only.
    /// </summary>
    internal void Record(
        string phase,
        string outcome,
        string? category,
        double seconds,
        int retries,
        string? retryReason)
    {
        string kind = phase.StartsWith("statement.", StringComparison.Ordinal)
            ? "statement"
            : "connection";

        KeyValuePair<string, object?> system = new(DbSystemName, "microsoft.sql_server");
        KeyValuePair<string, object?> phaseTag = new(Phase, ActivityName(phase));
        KeyValuePair<string, object?> kindTag = new(Kind, kind);
        KeyValuePair<string, object?> schema = new(SchemaVersion, SchemaVersionValue);
        KeyValuePair<string, object?> outcomeTag = new(MetricOutcome, outcome);

        _operations.Add(1, system, phaseTag, kindTag, schema, outcomeTag);
        _durations.Record(seconds, system, phaseTag, kindTag, schema, outcomeTag);
        if (outcome != "success")
        {
            _errors.Add(1, system, phaseTag, kindTag, schema,
                new KeyValuePair<string, object?>(MetricCategory, category ?? "unknown"));
        }
        if (retries > 0)
        {
            _retries.Add(retries, system, phaseTag, kindTag, schema,
                new KeyValuePair<string, object?>(RetryReason, retryReason ?? "not_applicable"));
        }
    }

    /// <summary>
    /// Counts telemetry items this endpoint could not deliver, with a closed reason label.
    /// </summary>
    internal void RecordDropped(long count, string reason)
    {
        if (count > 0)
        {
            _dropped.Add(count, new KeyValuePair<string, object?>(DroppedReason, reason));
        }
    }

    /// <summary>Maps a span name to the shared mssql.performance.activity value.</summary>
    internal static string ActivityName(string spanName) => spanName switch
    {
        "connection.open.attempt" => "connection.attempt",
        "connection.name_resolution" => "connection.dns",
        "connection.tls.setup" => "connection.tls",
        "connection.initialization" => "connection.initialize",
        "connection.prelogin.send" => "connection.prelogin_send",
        "connection.prelogin.receive" => "connection.prelogin_receive",
        _ => spanName
    };

    internal bool Shutdown(int timeoutMilliseconds) => _provider.Shutdown(timeoutMilliseconds);

    public void Dispose()
    {
        _provider.Dispose();
        _meter.Dispose();
    }
}
