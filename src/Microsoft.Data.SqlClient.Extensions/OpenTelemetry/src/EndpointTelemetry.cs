// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security;
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Microsoft.Data.SqlClient.Extensions.OpenTelemetry;

internal static class EndpointTelemetry
{
    internal const string RouteProperty = "Microsoft.Data.SqlClient.Connection.Route";
    // Must match the driver's ConnectionOpenActivity.RouteTag creation-time marker.
    internal const string RouteTag = "microsoft.data.sqlclient.internal.route";
    private const int MaximumEndpoints = 32;
    // Total ProcessExit budget across all pipelines; unexported data is abandoned after it.
    internal const int ShutdownTimeoutMilliseconds = 500;
    private static readonly TimeSpan s_idleTimeout = TimeSpan.FromMinutes(5);
    private static readonly object s_gate = new();
    private static readonly Dictionary<string, Pipeline> s_pipelines = new(StringComparer.Ordinal);
    private static readonly Timer s_cleanup = new(Cleanup, null, s_idleTimeout, s_idleTimeout);

    static EndpointTelemetry() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

    // Called through the driver's optional binding. The opaque lease is never exported as a tag.
    internal static IDisposable? Begin(string endpoint)
    {
        lock (s_gate)
        {
            if (!s_pipelines.TryGetValue(endpoint, out Pipeline? pipeline))
            {
                if (s_pipelines.Count >= MaximumEndpoints)
                {
                    Trace.TraceWarning("SqlClient OpenTelemetry endpoint limit reached.");
                    return null;
                }

                pipeline = new Pipeline(endpoint);
                s_pipelines.Add(endpoint, pipeline);
            }

            pipeline.Active++;
            Activity? parent = Activity.Current;
            bool sampled = parent is null ||
                (parent.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0;
            return new Lease(pipeline, sampled);
        }
    }

    // Called through the driver's optional binding when a routed scope ends. Metrics are never
    // sampled, so they are recorded whether or not the lease's spans are.
    internal static void Record(IDisposable? route, string name, string outcome,
        string? category, double seconds, int retries, string? retryReason)
    {
        if (route is Lease { Pipeline.Metrics: { } metrics })
        {
            try
            {
                metrics.Record(name, outcome, category, seconds, retries, retryReason);
            }
            catch (Exception ex) when (RouteProcessor.IsCatchable(ex))
            {
                Trace.TraceWarning("SqlClient OpenTelemetry metrics failed: {0}",
                    ex.GetType().Name);
            }
        }
    }

    internal static int PipelineCount
    {
        get
        {
            lock (s_gate)
            {
                return s_pipelines.Count;
            }
        }
    }

    internal static void Cleanup(object? state)
    {
        List<Pipeline> expired = new();
        lock (s_gate)
        {
            DateTime now = DateTime.UtcNow;
            foreach (KeyValuePair<string, Pipeline> entry in s_pipelines.ToArray())
            {
                if (entry.Value.Active == 0 && now - entry.Value.LastUsed >= s_idleTimeout)
                {
                    s_pipelines.Remove(entry.Key);
                    expired.Add(entry.Value);
                }
            }
        }

        foreach (Pipeline pipeline in expired)
        {
            pipeline.Dispose();
        }
    }

    private static void Shutdown()
    {
        Pipeline[] pipelines;
        lock (s_gate)
        {
            pipelines = s_pipelines.Values.ToArray();
            s_pipelines.Clear();
        }

        Stopwatch shutdownBudget = Stopwatch.StartNew();
        foreach (Pipeline pipeline in pipelines)
        {
            long remainingMilliseconds = Math.Max(0L,
                ShutdownTimeoutMilliseconds - shutdownBudget.ElapsedMilliseconds);
            pipeline.Shutdown((int)remainingMilliseconds);
        }
    }

    internal sealed class Pipeline
    {
        internal const int MetricExportIntervalMilliseconds = 10000;
        private const string ConnectionSource = "Microsoft.Data.SqlClient.Connection";
        private const string CommandSource = "Microsoft.Data.SqlClient.Command";
        private readonly ActivityListener _listener;
        private readonly RouteProcessor _processor;

        internal Pipeline(string endpoint, BaseProcessor<Activity>? exportProcessor = null)
        {
            Uri uri = new(endpoint);
            OtlpExporterOptions options = new()
            {
                Endpoint = uri,
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Headers = string.Empty
            };
            Metrics = EndpointMetrics.Create(uri, MetricExportIntervalMilliseconds);
            BaseProcessor<Activity> export = exportProcessor ??
                new BatchActivityExportProcessor(
                    new CountingExporter(new OtlpTraceExporter(options), Metrics));
            _processor = new(this, export);
            // The SDK provider has no sources, so it registers no listener. It owns the export
            // processor so the exporter receives the provider resource (service.name, including
            // OTEL_SERVICE_NAME), and it owns flush and shutdown. The route filter is not
            // registered; the route-scoped listener below feeds it, so unrouted or other-route
            // opens never create an activity here.
            Provider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(DriverResource.Create())
                .AddProcessor(export)
                .Build();
            _listener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == ConnectionSource || source.Name == CommandSource,
                Sample = (ref ActivityCreationOptions<ActivityContext> creation) =>
                    Sample(creation.Tags),
                SampleUsingParentId = (ref ActivityCreationOptions<string> creation) =>
                    Sample(creation.Tags),
                ActivityStopped = activity => _processor.OnEnd(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        internal TracerProvider Provider { get; }
        internal EndpointMetrics? Metrics { get; }
        internal int Active { get; set; }
        internal DateTime LastUsed { get; set; } = DateTime.UtcNow;

        // Records only activities created with this pipeline's sampled lease as the route tag.
        // Children carry the same lease, so they follow their routed parent.
        internal ActivitySamplingResult Sample(IEnumerable<KeyValuePair<string, object?>>? tags)
        {
            if (tags is not null)
            {
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == RouteTag && tag.Value is Lease { Sampled: true } lease &&
                        ReferenceEquals(lease.Pipeline, this))
                    {
                        return ActivitySamplingResult.AllDataAndRecorded;
                    }
                }
            }
            return ActivitySamplingResult.None;
        }

        internal void Shutdown(int timeoutMilliseconds)
        {
            _listener.Dispose();
            Stopwatch budget = Stopwatch.StartNew();
            Provider.Shutdown(timeoutMilliseconds);
            Metrics?.Shutdown((int)Math.Max(0L,
                timeoutMilliseconds - budget.ElapsedMilliseconds));
            Dispose();
        }

        internal void Dispose()
        {
            _listener.Dispose();
            Provider.Dispose();
            Metrics?.Dispose();
        }
    }

    internal sealed class Lease(Pipeline pipeline, bool sampled) : IDisposable, ICloneable
    {
        private Pipeline? _pipeline = pipeline;
        internal Pipeline? Pipeline => _pipeline;
        internal bool Sampled { get; } = sampled;

        public object Clone()
        {
            lock (s_gate)
            {
                if (_pipeline is null)
                {
                    throw new ObjectDisposedException(nameof(Lease));
                }
                _pipeline.Active++;
                return new Lease(_pipeline, Sampled);
            }
        }

        public void Dispose()
        {
            lock (s_gate)
            {
                if (_pipeline is not null)
                {
                    _pipeline.Active--;
                    _pipeline.LastUsed = DateTime.UtcNow;
                    _pipeline = null;
                }
            }
        }
    }

    internal sealed class RouteProcessor(Pipeline pipeline, BaseProcessor<Activity> inner)
        : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.GetCustomProperty(RouteProperty) is Lease { Sampled: true } lease &&
                ReferenceEquals(lease.Pipeline, pipeline))
            {
                try
                {
                    inner.OnEnd(data);
                }
                catch (Exception ex) when (IsCatchable(ex))
                {
                    Trace.TraceWarning("SqlClient OpenTelemetry processor failed: {0}",
                        ex.GetType().Name);
                }
            }
        }

        internal static bool IsCatchable(Exception exception)
        {
            Type type = exception.GetType();
            return type != typeof(StackOverflowException) &&
                type != typeof(OutOfMemoryException) &&
                type != typeof(ThreadAbortException) &&
                type != typeof(NullReferenceException) &&
                type != typeof(AccessViolationException) &&
                !typeof(SecurityException).IsAssignableFrom(type);
        }

        protected override bool OnForceFlush(int timeoutMilliseconds) =>
            inner.ForceFlush(timeoutMilliseconds);

        protected override bool OnShutdown(int timeoutMilliseconds) =>
            inner.Shutdown(timeoutMilliseconds);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Counts spans lost by a failed export. The batch processor's queue-full drop count is
    // internal to the SDK, so queue_full is not reported (see doc/opentelemetry-poc.md).
    internal sealed class CountingExporter(BaseExporter<Activity> inner, EndpointMetrics? metrics)
        : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            ExportResult result;
            try
            {
                result = inner.Export(batch);
            }
            catch (Exception ex) when (RouteProcessor.IsCatchable(ex))
            {
                result = ExportResult.Failure;
            }

            if (result != ExportResult.Success)
            {
                metrics?.RecordDropped(batch.Count, EndpointMetrics.ExportFailed);
            }
            return result;
        }

        protected override bool OnForceFlush(int timeoutMilliseconds) =>
            inner.ForceFlush(timeoutMilliseconds);

        protected override bool OnShutdown(int timeoutMilliseconds) =>
            inner.Shutdown(timeoutMilliseconds);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
