// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;

namespace Microsoft.Data.SqlClient.Extensions.OpenTelemetry.Test;

/// <summary>
/// Ensures private endpoint processors only forward activities belonging to their route.
/// </summary>
[Collection("Endpoint telemetry")]
public class EndpointTelemetryTests
{
    /// <summary>Concurrent opens to one endpoint share a single private pipeline.</summary>
    [Fact]
    public void Begin_ConcurrentSameEndpoint_SharesPipeline()
    {
        // Arrange
        const string endpoint = "http://127.0.0.1:4318/v1/traces";
        EndpointTelemetry.Lease[] leases = new EndpointTelemetry.Lease[16];

        // Act
        Parallel.For(0, leases.Length, i =>
            leases[i] = Assert.IsType<EndpointTelemetry.Lease>(EndpointTelemetry.Begin(endpoint)));

        // Assert
        EndpointTelemetry.Pipeline pipeline = Assert.IsType<EndpointTelemetry.Pipeline>(leases[0].Pipeline);
        Assert.All(leases, lease => Assert.Same(pipeline, lease.Pipeline));
        Assert.Equal(leases.Length, pipeline.Active);

        // Cleanup
        foreach (EndpointTelemetry.Lease lease in leases)
        {
            lease.Dispose();
        }
        pipeline.LastUsed = DateTime.MinValue;
        EndpointTelemetry.Cleanup(null);
    }

    /// <summary>An active open keeps its pipeline alive until its lease is released.</summary>
    [Fact]
    public void Cleanup_ActiveAndExpiredPipeline_WaitsForLease()
    {
        // Arrange
        const string endpoint = "http://127.0.0.1:4318/v1/traces";
        EndpointTelemetry.Lease lease = Assert.IsType<EndpointTelemetry.Lease>(
            EndpointTelemetry.Begin(endpoint));
        EndpointTelemetry.Pipeline pipeline = Assert.IsType<EndpointTelemetry.Pipeline>(lease.Pipeline);
        pipeline.LastUsed = DateTime.MinValue;

        // Act
        EndpointTelemetry.Cleanup(null);
        EndpointTelemetry.Lease next = Assert.IsType<EndpointTelemetry.Lease>(
            EndpointTelemetry.Begin(endpoint));

        // Assert
        Assert.Same(pipeline, next.Pipeline);

        // Cleanup
        lease.Dispose();
        next.Dispose();
        pipeline.LastUsed = DateTime.MinValue;
        EndpointTelemetry.Cleanup(null);
        using EndpointTelemetry.Lease replacement = Assert.IsType<EndpointTelemetry.Lease>(
            EndpointTelemetry.Begin(endpoint));
        Assert.NotSame(pipeline, replacement.Pipeline);
    }

    /// <summary>The endpoint registry refuses new pipelines when the bounded cap is reached.</summary>
    [Fact]
    public void Begin_RegistryLimit_DeclinesOverflow()
    {
        // Arrange
        List<EndpointTelemetry.Lease> leases = new();
        int suffix = 0;

        // Act
        try
        {
            while (EndpointTelemetry.PipelineCount < 32)
            {
                EndpointTelemetry.Lease? lease = EndpointTelemetry.Begin(
                    $"http://127.0.0.1:4318/cap-{suffix++}/v1/traces") as EndpointTelemetry.Lease;
                if (lease == null)
                {
                    break;
                }
                leases.Add(lease);
            }

            IDisposable? overflow = EndpointTelemetry.Begin(
                $"http://127.0.0.1:4318/cap-overflow-{suffix}/v1/traces");

            // Assert
            Assert.Equal(32, EndpointTelemetry.PipelineCount);
            Assert.Null(overflow);
        }
        finally
        {
            // Cleanup
            foreach (EndpointTelemetry.Lease lease in leases)
            {
                EndpointTelemetry.Pipeline pipeline =
                    Assert.IsType<EndpointTelemetry.Pipeline>(lease.Pipeline);
                lease.Dispose();
                pipeline.LastUsed = DateTime.MinValue;
            }
            EndpointTelemetry.Cleanup(null);
        }
    }

    /// <summary>A retained lease survives its parent and preserves an unsampled decision.</summary>
    [Fact]
    public void Clone_ParentDisposed_KeepsPipelineAndSamplingDecision()
    {
        // Arrange
        using Activity parent = new Activity("parent").Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.None;
        EndpointTelemetry.Lease lease = Assert.IsType<EndpointTelemetry.Lease>(
            EndpointTelemetry.Begin("http://127.0.0.1:4318/v1/traces"));
        EndpointTelemetry.Pipeline pipeline = Assert.IsType<EndpointTelemetry.Pipeline>(lease.Pipeline);

        // Act
        using EndpointTelemetry.Lease retained = Assert.IsType<EndpointTelemetry.Lease>(
            ((ICloneable)lease).Clone());
        lease.Dispose();
        pipeline.LastUsed = DateTime.MinValue;
        EndpointTelemetry.Cleanup(null);

        // Assert
        Assert.Same(pipeline, retained.Pipeline);
        Assert.False(retained.Sampled);
        Assert.Equal(1, pipeline.Active);
        Assert.Throws<ObjectDisposedException>(() => ((ICloneable)lease).Clone());
    }

    /// <summary>
    /// The matching private pipeline forwards a sampled route to its exporter.
    /// </summary>
    [Fact]
    public void OnEnd_MatchingSampledRoute_ForwardsActivity()
    {
        // Arrange
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces");
        using TracerProviderScope scope = new(pipeline);
        CountingProcessor inner = new();
        using EndpointTelemetry.RouteProcessor processor = new(pipeline, inner);
        using Activity activity = new("test");
        activity.SetCustomProperty(EndpointTelemetry.RouteProperty,
            new EndpointTelemetry.Lease(pipeline, true));

        // Act
        processor.OnEnd(activity);

        // Assert
        Assert.Equal(1, inner.Count);
    }

    /// <summary>
    /// A different endpoint's pipeline cannot forward the activity.
    /// </summary>
    [Fact]
    public void OnEnd_DifferentRoute_DoesNotForwardActivity()
    {
        // Arrange
        EndpointTelemetry.Pipeline first = new("http://127.0.0.1:4318/v1/traces");
        EndpointTelemetry.Pipeline second = new("http://127.0.0.1:4319/v1/traces");
        using TracerProviderScope firstScope = new(first);
        using TracerProviderScope secondScope = new(second);
        CountingProcessor inner = new();
        using EndpointTelemetry.RouteProcessor processor = new(first, inner);
        using Activity activity = new("test");
        activity.SetCustomProperty(EndpointTelemetry.RouteProperty,
            new EndpointTelemetry.Lease(second, true));

        // Act
        processor.OnEnd(activity);

        // Assert
        Assert.Equal(0, inner.Count);
    }

    /// <summary>
    /// Application-only activities are not forwarded to a private collector.
    /// </summary>
    [Fact]
    public void OnEnd_NoPrivateRoute_DoesNotForwardActivity()
    {
        // Arrange
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces");
        using TracerProviderScope scope = new(pipeline);
        CountingProcessor inner = new();
        using EndpointTelemetry.RouteProcessor processor = new(pipeline, inner);
        using Activity activity = new("test");

        // Act
        processor.OnEnd(activity);

        // Assert
        Assert.Equal(0, inner.Count);
    }

    /// <summary>
    /// Another listener sampling the activity cannot override the route's unsampled parent.
    /// </summary>
    [Fact]
    public void OnEnd_UnsampledParent_DoesNotForwardActivity()
    {
        // Arrange
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces");
        using TracerProviderScope scope = new(pipeline);
        CountingProcessor inner = new();
        using EndpointTelemetry.RouteProcessor processor = new(pipeline, inner);
        using Activity activity = new("test");
        activity.SetCustomProperty(EndpointTelemetry.RouteProperty,
            new EndpointTelemetry.Lease(pipeline, false));

        // Act
        processor.OnEnd(activity);

        // Assert
        Assert.Equal(0, inner.Count);
    }

    /// <summary>A private exporter callback failure cannot escape into a connection open.</summary>
    [Fact]
    public void OnEnd_ExporterCallbackThrows_DoesNotPropagate()
    {
        // Arrange
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces");
        using TracerProviderScope scope = new(pipeline);
        using EndpointTelemetry.RouteProcessor processor = new(
            pipeline, new SimpleActivityExportProcessor(new ThrowingExporter()));
        using Activity activity = new("test");
        activity.SetCustomProperty(EndpointTelemetry.RouteProperty,
            new EndpointTelemetry.Lease(pipeline, true));

        // Act
        Exception? error = Record.Exception(() => processor.OnEnd(activity));

        // Assert
        Assert.Null(error);
    }

    /// <summary>
    /// A competing listener cannot make an unsampled parent eligible for private export.
    /// </summary>
    [Fact]
    public void Begin_UnsampledParentWithCompetingListener_RouteRemainsUnsampled()
    {
        // Arrange
        using Activity parent = new Activity("parent").Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.None;
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == "Microsoft.Data.SqlClient.Connection",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using EndpointTelemetry.Lease lease = Assert.IsType<EndpointTelemetry.Lease>(
            EndpointTelemetry.Begin("http://127.0.0.1:4318/v1/traces"));
        using ActivitySource source = new("Microsoft.Data.SqlClient.Connection");

        // Act
        using Activity? activity = source.StartActivity("connection.open");

        // Assert
        Assert.NotNull(activity);
        Assert.True(activity.Recorded);
        Assert.False(lease.Sampled);
    }

    /// <summary>Statement spans from the Command source follow the same endpoint isolation.</summary>
    [Fact]
    public void OnEnd_StatementSpanRoutes_ForwardOnlyToOwningEndpoint()
    {
        // Arrange
        EndpointTelemetry.Pipeline first = new("http://127.0.0.1:4318/v1/traces");
        EndpointTelemetry.Pipeline second = new("http://127.0.0.1:4319/v1/traces");
        using TracerProviderScope firstScope = new(first);
        using TracerProviderScope secondScope = new(second);
        CountingProcessor firstInner = new();
        CountingProcessor secondInner = new();
        using EndpointTelemetry.RouteProcessor firstProcessor = new(first, firstInner);
        using EndpointTelemetry.RouteProcessor secondProcessor = new(second, secondInner);
        using ActivitySource source = new("Microsoft.Data.SqlClient.Command");
        using ActivityListener listener = new()
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using Activity statement = Assert.IsType<Activity>(source.StartActivity("statement.execute"));
        statement.SetCustomProperty(EndpointTelemetry.RouteProperty,
            new EndpointTelemetry.Lease(second, true));
        statement.Stop();

        // Act
        firstProcessor.OnEnd(statement);
        secondProcessor.OnEnd(statement);

        // Assert
        Assert.Equal(0, firstInner.Count);
        Assert.Equal(1, secondInner.Count);
    }

    /// <summary>
    /// (a) While a private pipeline is alive, an open without an endpoint creates no activity
    /// and exports nothing, because the private listener samples only its own route.
    /// </summary>
    [Fact]
    public void Sample_UnroutedOpenWhilePipelineActive_CreatesNoActivity()
    {
        // Arrange
        CountingProcessor inner = new();
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces", inner);
        using TracerProviderScope scope = new(pipeline);
        using ActivitySource source = new("Microsoft.Data.SqlClient.Connection");

        // Act
        bool listening = source.HasListeners();
        using Activity? activity = StartLikeDriver(source, "connection.open", null);

        // Assert
        Assert.True(listening);
        Assert.Null(activity);
        Assert.Equal(0, inner.Count);
    }

    /// <summary>(b) An open routed to X is sampled and exported only by X's pipeline.</summary>
    [Fact]
    public void Sample_RoutedOpen_SampledAndExportedOnlyByOwningPipeline()
    {
        // Arrange
        CountingProcessor xInner = new();
        CountingProcessor yInner = new();
        EndpointTelemetry.Pipeline x = new("http://127.0.0.1:4318/v1/traces", xInner);
        EndpointTelemetry.Pipeline y = new("http://127.0.0.1:4319/v1/traces", yInner);
        using TracerProviderScope xScope = new(x);
        using TracerProviderScope yScope = new(y);
        using ActivitySource source = new("Microsoft.Data.SqlClient.Connection");
        EndpointTelemetry.Lease lease = new(x, true);
        KeyValuePair<string, object?>[] tags = { new(EndpointTelemetry.RouteTag, lease) };

        // Act
        Activity? activity = StartLikeDriver(source, "connection.open", lease);
        activity?.Stop();

        // Assert
        Assert.NotNull(activity);
        Assert.True(activity.Recorded);
        Assert.Equal(ActivitySamplingResult.AllDataAndRecorded, x.Sample(tags));
        Assert.Equal(ActivitySamplingResult.None, y.Sample(tags));
        Assert.Equal(1, xInner.Count);
        Assert.Equal(0, yInner.Count);
    }

    /// <summary>
    /// The export processor is owned by the private provider, so the exporter receives the
    /// provider resource and exported spans carry a service.name.
    /// </summary>
    [Fact]
    public void Pipeline_ExportProcessor_ReceivesProviderResource()
    {
        // Arrange
        CountingProcessor inner = new();

        // Act
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces", inner);
        using TracerProviderScope scope = new(pipeline);

        // Assert
        Assert.Same(pipeline.Provider, inner.ParentProvider);
        IEnumerable<KeyValuePair<string, object>> resource =
            inner.ParentProvider!.GetResource().Attributes;
        Assert.Contains(resource, attribute => attribute.Key == "service.name");
        Assert.Contains(resource, attribute =>
            attribute.Key == DriverResource.DriverName && Equals(attribute.Value, "sqlclient"));
        Assert.Contains(resource, attribute =>
            attribute.Key == DriverResource.DriverVersion &&
            !string.IsNullOrEmpty(attribute.Value as string));
    }

    /// <summary>
    /// (c) With an application-owned SDK and a private pipeline, an unrouted open goes to the
    /// application only and a routed open goes to both. The route marker is never visible on
    /// the completed activity.
    /// </summary>
    [Fact]
    public void Sample_AppAndPrivateListeners_RouteEachOpenIndependently()
    {
        // Arrange
        CountingProcessor inner = new();
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces", inner);
        using TracerProviderScope scope = new(pipeline);
        using AppListener app = new();
        using ActivitySource source = new("Microsoft.Data.SqlClient.Connection");

        // Act
        Activity? unrouted = StartLikeDriver(source, "connection.open", null);
        unrouted?.Stop();
        int privateAfterUnrouted = inner.Count;
        Activity? routed = StartLikeDriver(
            source, "connection.open", new EndpointTelemetry.Lease(pipeline, true));
        routed?.Stop();

        // Assert
        Assert.NotNull(unrouted);
        Assert.NotNull(routed);
        Assert.Equal(0, privateAfterUnrouted);
        Assert.Equal(1, inner.Count);
        Assert.Equal(new[] { unrouted, routed }, app.Stopped);
        Assert.All(app.Stopped, a => Assert.Null(a.GetTagItem(EndpointTelemetry.RouteTag)));
    }

    /// <summary>
    /// (d) Routed children follow their routed parent and are exported with it, while an
    /// unsampled route creates no private activity at all.
    /// </summary>
    [Fact]
    public void Sample_RoutedChildAndUnsampledRoute_FollowParentDecision()
    {
        // Arrange
        CountingProcessor inner = new();
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces", inner);
        using TracerProviderScope scope = new(pipeline);
        using ActivitySource source = new("Microsoft.Data.SqlClient.Connection");
        EndpointTelemetry.Lease lease = new(pipeline, true);

        // Act
        Activity root = Assert.IsType<Activity>(StartLikeDriver(source, "connection.open", lease));
        Activity? child = StartLikeDriver(source, "connection.open.attempt", lease, root.Id);
        child?.Stop();
        root.Stop();
        using Activity? unsampled = StartLikeDriver(
            source, "connection.open", new EndpointTelemetry.Lease(pipeline, false));

        // Assert
        Assert.NotNull(child);
        Assert.Equal(root.TraceId, child.TraceId);
        Assert.Equal(root.SpanId, child.ParentSpanId);
        Assert.Equal(2, inner.Count);
        Assert.Null(unsampled);
    }

    /// <summary>Private metrics use the sibling OTLP path only for /v1/traces endpoints.</summary>
    [Theory]
    [InlineData("http://127.0.0.1:4318/v1/traces", "http://127.0.0.1:4318/v1/metrics")]
    [InlineData("http://collector:4318/otel/v1/traces/", "http://collector:4318/otel/v1/metrics")]
    [InlineData("http://127.0.0.1:4317", null)]
    [InlineData("http://127.0.0.1:4318/custom/spans", null)]
    public void MetricsEndpoint_TraceEndpoint_DerivesOnlyKnownShape(
        string traceEndpoint, string? expected)
    {
        // Act
        Uri? metrics = EndpointMetrics.MetricsEndpoint(new Uri(traceEndpoint));

        // Assert
        Assert.Equal(expected, metrics?.ToString());
    }

    /// <summary>
    /// Every routed scope is counted, error and retry counts follow the failed root, and each
    /// endpoint has its own meter so routed measurements cannot appear in another endpoint's
    /// metrics.
    /// </summary>
    [Fact]
    public void Record_RootAndAttempt_CountsEveryScopeOnOwningMeter()
    {
        // Arrange
        using EndpointMetrics first = Assert.IsType<EndpointMetrics>(
            EndpointMetrics.Create(new Uri("http://127.0.0.1:4318/v1/traces"), 60000));
        using EndpointMetrics second = Assert.IsType<EndpointMetrics>(
            EndpointMetrics.Create(new Uri("http://127.0.0.1:4319/v1/traces"), 60000));
        List<(string Meter, string Instrument, long Value)> counts = new();
        using MeterListener meterListener = new()
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == first.MeterName ||
                    instrument.Meter.Name == second.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            lock (counts)
            {
                counts.Add((instrument.Meter.Name, instrument.Name, value));
            }
        });
        meterListener.Start();

        // Act
        first.Record("statement.execute", "failure", "query_syntax_semantics", 0.01, 1,
            "transient_server_error");
        first.Record("statement.attempt", "success", null, 0.005, 0, null);

        // Assert
        Assert.All(counts, c => Assert.Equal(first.MeterName, c.Meter));
        Assert.Equal(
            new[] { ("db.client.operation.count", 1L), ("db.client.operation.count", 1L),
                ("db.client.operation.error.count", 1L),
                ("db.client.operation.retry.count", 1L) },
            counts.Select(c => (c.Instrument, c.Value)).OrderBy(c => c.Instrument));
    }

    /// <summary>
    /// Routed metrics use the shared mssql.* label names and JDBC activity names, not the
    /// SqlClient span name.
    /// </summary>
    [Fact]
    public void Record_TlsSpan_UsesSharedActivityLabels()
    {
        // Arrange
        using EndpointMetrics metrics = Assert.IsType<EndpointMetrics>(
            EndpointMetrics.Create(new Uri("http://127.0.0.1:4318/v1/traces"), 60000));
        Dictionary<string, object?> tags = new();
        using MeterListener meterListener = new()
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == metrics.MeterName &&
                    instrument.Name == "db.client.operation.count")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, measured, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in measured)
            {
                tags[tag.Key] = tag.Value;
            }
        });
        meterListener.Start();

        // Act
        metrics.Record("connection.tls.setup", "success", null, 0.002, 0, null);

        // Assert
        Assert.Equal("connection.tls", tags["mssql.performance.activity"]);
        Assert.Equal("connection", tags["mssql.operation.kind"]);
        Assert.Equal("1.0", tags["mssql.telemetry.schema.version"]);
        Assert.Equal("success", tags["mssql.operation.outcome"]);
        Assert.DoesNotContain(
            tags.Keys, key => key.StartsWith("sqlclient.", StringComparison.Ordinal));
    }

    /// <summary>
    /// Metrics are never sampled: a scope ending on an unsampled lease is still counted on the
    /// owning endpoint meter, while it creates no span.
    /// </summary>
    [Fact]
    public void Record_UnsampledLease_StillRecordsEndpointMetrics()
    {
        // Arrange
        CountingProcessor inner = new();
        EndpointTelemetry.Pipeline pipeline = new("http://127.0.0.1:4318/v1/traces", inner);
        using TracerProviderScope scope = new(pipeline);
        EndpointMetrics metrics = Assert.IsType<EndpointMetrics>(pipeline.Metrics);
        List<string> instruments = new();
        using MeterListener meterListener = new()
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == metrics.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => instruments.Add(instrument.Name));
        meterListener.Start();
        EndpointTelemetry.Lease unsampled = new(pipeline, false);

        // Act
        EndpointTelemetry.Record(unsampled, "connection.open", "success", null, 0.01, 0, null);

        // Assert
        Assert.Equal(new[] { "db.client.operation.count" }, instruments);
        Assert.Equal(0, inner.Count);
    }

    /// <summary>
    /// A failed export is counted as dropped spans with the closed export_failed reason and
    /// does not propagate to the application.
    /// </summary>
    [Fact]
    public void Export_InnerExporterThrows_CountsDroppedSpans()
    {
        // Arrange
        using EndpointMetrics metrics = Assert.IsType<EndpointMetrics>(
            EndpointMetrics.Create(new Uri("http://127.0.0.1:4318/v1/traces"), 60000));
        List<(long Value, object? Reason)> dropped = new();
        using MeterListener meterListener = new()
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == metrics.MeterName &&
                    instrument.Name == EndpointMetrics.DroppedName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            object? reason = null;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == EndpointMetrics.DroppedReason)
                {
                    reason = tag.Value;
                }
            }
            dropped.Add((value, reason));
        });
        meterListener.Start();
        using EndpointTelemetry.CountingExporter exporter = new(new ThrowingExporter(), metrics);
        using Activity first = new("connection.open");
        using Activity second = new("connection.open.attempt");

        // Act
        ExportResult result = exporter.Export(new Batch<Activity>(new[] { first, second }, 2));

        // Assert
        Assert.Equal(ExportResult.Failure, result);
        Assert.Equal(new[] { (2L, (object?)EndpointMetrics.ExportFailed) }, dropped);
    }

    /// <summary>Counts forwarded activities without performing network exports.</summary>
    private sealed class CountingProcessor : BaseProcessor<Activity>
    {
        internal int Count { get; private set; }
        public override void OnEnd(Activity data) => Count++;
    }

    /// <summary>Simulates an exporter failure while accepting a completed activity.</summary>
    private sealed class ThrowingExporter : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch) =>
            throw new InvalidOperationException("private");
    }

    /// <summary>Isolates tests that exercise the shared process-wide endpoint registry.</summary>
    [CollectionDefinition("Endpoint telemetry", DisableParallelization = true)]
    public sealed class EndpointTelemetryCollection;

    /// <summary>Disposes the private pipeline's SDK and route-scoped listener.</summary>
    private sealed class TracerProviderScope(EndpointTelemetry.Pipeline pipeline) : IDisposable
    {
        public void Dispose() => pipeline.Dispose();
    }

    /// <summary>
    /// Starts an activity the way the driver does: a private lease is passed as a creation-time
    /// route tag, then the tag is removed and the lease is kept as a custom property.
    /// </summary>
    private static Activity? StartLikeDriver(
        ActivitySource source, string name, EndpointTelemetry.Lease? lease, string? parentId = null)
    {
        if (lease is null)
        {
            return parentId is null
                ? source.StartActivity(name, ActivityKind.Client)
                : source.StartActivity(name, ActivityKind.Internal, parentId);
        }

        KeyValuePair<string, object?>[] tags = { new(EndpointTelemetry.RouteTag, lease) };
        Activity? activity = parentId is null
            ? source.StartActivity(name, ActivityKind.Client, default(ActivityContext), tags)
            : source.StartActivity(name, ActivityKind.Internal, parentId, tags);
        activity?.SetTag(EndpointTelemetry.RouteTag, null);
        activity?.SetCustomProperty(EndpointTelemetry.RouteProperty, lease);
        return activity;
    }

    /// <summary>Collects activities sampled by a simulated application-owned SDK.</summary>
    private sealed class AppListener : IDisposable
    {
        private readonly ActivityListener _listener;

        internal AppListener()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name.StartsWith("Microsoft.Data.SqlClient.",
                    StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Stopped.Add
            };
            ActivitySource.AddActivityListener(_listener);
        }

        internal List<Activity> Stopped { get; } = new();

        public void Dispose() => _listener.Dispose();
    }
}
