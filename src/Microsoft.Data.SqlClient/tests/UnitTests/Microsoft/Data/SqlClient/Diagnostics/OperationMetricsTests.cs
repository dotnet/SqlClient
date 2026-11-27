// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies per-scope operation metrics: they are recorded for every lifecycle scope without a
/// span, carry only bounded labels, count retries on roots only, and cost nothing when unheard.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class OperationMetricsTests
{
    private static readonly string[] s_labels =
    [
        "db.system.name", OperationMetrics.Phase, OperationMetrics.Kind,
        OperationMetrics.SchemaVersion, OperationMetrics.MetricOutcome
    ];

    /// <summary>Without any meter or activity listener, no scope is created.</summary>
    [Fact]
    public void Create_NoListener_ReturnsNull()
    {
        // Act
        OperationScope? created = OperationScope.Create(null, "connection.open", isRoot: true);
        OperationScope? root = ConnectionOpenActivity.Start();

        // Assert
        Assert.False(OperationMetrics.Enabled);
        Assert.Null(created);
        Assert.Null(root);
    }

#if NET
    /// <summary>Without any listener, starting and finishing scopes allocates nothing.</summary>
    [Fact]
    public void Start_NoListener_DoesNotAllocate()
    {
        // Arrange
        // Warm up so that one-time JIT and static initialization are excluded.
        StartAndFinishScopes();
        long before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            StartAndFinishScopes();
        }

        // Assert
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private static void StartAndFinishScopes()
    {
        using OperationScope? root = ConnectionOpenActivity.Start();
        using OperationScope? phase = ConnectionOpenActivity.StartPhase("connection.login");
    }
#endif

    /// <summary>A meter-only subscriber receives every synchronous connection-open scope.</summary>
    [Fact]
    public void Open_MeterOnlySuccess_RecordsEveryLifecycleScope()
    {
        // Arrange
        using MetricsCapture metrics = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        using TDSServerEndPoint endpoint = StartEndpoint(server);
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        connection.Open();

        // Assert
        AssertSuccessfulOpen(metrics);
    }

    /// <summary>
    /// A meter-only subscriber receives every asynchronous connection-open scope.
    /// </summary>
    [Fact]
    public async Task OpenAsync_MeterOnlySuccess_RecordsEveryLifecycleScope()
    {
        // Arrange
        using MetricsCapture metrics = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        using TDSServerEndPoint endpoint = StartEndpoint(server);
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        await connection.OpenAsync();

        // Assert
        AssertSuccessfulOpen(metrics);
        Assert.Null(OperationScope.CurrentFlow);
    }

    /// <summary>
    /// A recovered retry counts one extra attempt on the root only with a transient retry
    /// reason, and the failed attempt reports its error category without failing the root.
    /// </summary>
    [Fact]
    public void Open_MeterOnlyRecoveredRetry_RecordsRetryOnRootOnly()
    {
        // Arrange
        using MetricsCapture metrics = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40501, 1, "private-message");
        using TDSServerEndPoint endpoint = StartEndpoint(server);
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            RetryLogicProvider = CreateTwoAttemptProvider()
        };

        // Act
        connection.Open();

        // Assert
        Measurement retry = Assert.Single(metrics.Of(OperationMetrics.RetryCount));
        Assert.Equal(1d, retry.Value);
        Assert.Equal("connection.open", retry.Tags[OperationMetrics.Phase]);
        // 40501 is on the driver's transient login list.
        Assert.Equal("transient_server_error", retry.Tags[OperationMetrics.RetryReason]);
        Assert.Equal(
            new[] { "failure", "success" },
            metrics.Of(OperationMetrics.OperationCount, "connection.attempt")
                .Select(m => (string)m.Tags[OperationMetrics.MetricOutcome]!)
                .OrderBy(outcome => outcome, StringComparer.Ordinal));
        Measurement attemptError = Assert.Single(
            metrics.Of(OperationMetrics.ErrorCount, "connection.attempt"));
        Assert.Equal("resource_throttling", attemptError.Tags[OperationMetrics.MetricCategory]);
        Assert.Equal("success", Assert.Single(
            metrics.Of(OperationMetrics.OperationCount, "connection.open"))
            .Tags[OperationMetrics.MetricOutcome]);
        Assert.Empty(metrics.Of(OperationMetrics.ErrorCount, "connection.open"));
    }

    /// <summary>
    /// An exhausted asynchronous retry fails the root with the classified category and still
    /// records the additional attempt on the root.
    /// </summary>
    [Fact]
    public async Task OpenAsync_MeterOnlyExhaustedRetry_RecordsRootFailureCategory()
    {
        // Arrange
        using MetricsCapture metrics = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40501, int.MaxValue, "private-message");
        using TDSServerEndPoint endpoint = StartEndpoint(server);
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            RetryLogicProvider = CreateTwoAttemptProvider()
        };

        // Act
        await Assert.ThrowsAsync<AggregateException>(() => connection.OpenAsync());

        // Assert
        Assert.Equal("failure", Assert.Single(
            metrics.Of(OperationMetrics.OperationCount, "connection.open"))
            .Tags[OperationMetrics.MetricOutcome]);
        Assert.Equal("resource_throttling", Assert.Single(
            metrics.Of(OperationMetrics.ErrorCount, "connection.open"))
            .Tags[OperationMetrics.MetricCategory]);
        Assert.Equal(2, metrics.Of(OperationMetrics.ErrorCount, "connection.attempt")
            .Count());
        Assert.Equal(1d, Assert.Single(metrics.Of(OperationMetrics.RetryCount)).Value);
    }

    /// <summary>When a span exists, the root duration reuses the span duration exactly.</summary>
    [Fact]
    public void Open_WithSpan_ReusesActivityDuration()
    {
        // Arrange
        using MetricsCapture metrics = new();
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        using TDSServerEndPoint endpoint = StartEndpoint(server);
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        connection.Open();

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Measurement duration = Assert.Single(
            metrics.Of(OperationMetrics.OperationDuration, "connection.open"));
        Assert.Equal(root.Duration.TotalSeconds, duration.Value);
        Assert.Equal("success", root.GetTagItem(OperationMetrics.Outcome));
    }

    /// <summary>
    /// A meter-only subscriber receives the statement root with the statement kind.
    /// </summary>
    [Fact]
    public void ExecuteScalar_MeterOnlySuccess_RecordsStatementRoot()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        using TDSServerEndPoint endpoint = StartEndpoint(server);
        using SqlConnection connection = new(CreateConnectionString(endpoint));
        connection.Open();
        using SqlCommand command = new("select 1", connection);
        using MetricsCapture metrics = new();

        // Act
        command.ExecuteScalar();

        // Assert
        Measurement root = Assert.Single(
            metrics.Of(OperationMetrics.OperationCount, "statement.execute"));
        Assert.Equal("statement", root.Tags[OperationMetrics.Kind]);
        Assert.Equal("success", root.Tags[OperationMetrics.MetricOutcome]);
        Assert.Empty(metrics.Of(OperationMetrics.OperationCount, "connection.open"));
    }

    /// <summary>
    /// Scopes with a JDBC counterpart use JDBC's activity name; SqlClient-only scopes keep a
    /// single-segment connection.* name.
    /// </summary>
    [Theory]
    [InlineData("connection.open", "connection.open")]
    [InlineData("connection.open.attempt", "connection.attempt")]
    [InlineData("connection.name_resolution", "connection.dns")]
    [InlineData("connection.tls.setup", "connection.tls")]
    [InlineData("connection.initialization", "connection.initialize")]
    [InlineData("connection.prelogin.send", "connection.prelogin_send")]
    [InlineData("connection.prelogin.receive", "connection.prelogin_receive")]
    [InlineData("connection.endpoint_attempt", "connection.endpoint_attempt")]
    [InlineData("statement.attempt", "statement.attempt")]
    public void ActivityName_ScopeName_ReturnsSharedActivity(string scope, string expected)
    {
        // Act
        string activity = OperationMetrics.ActivityName(scope);

        // Assert
        Assert.Equal(expected, activity);
    }

    private static void AssertSuccessfulOpen(MetricsCapture metrics)
    {
        string[] phases = metrics.Of(OperationMetrics.OperationCount)
            .Select(m => (string)m.Tags[OperationMetrics.Phase]!).ToArray();
        Assert.Subset(new HashSet<string>(phases), new HashSet<string>
        {
            "connection.open", "connection.attempt", "connection.physical_open",
            "connection.endpoint_attempt", "connection.login"
        });
        Assert.Single(phases, phase => phase == "connection.open");
        Assert.All(metrics.Of(OperationMetrics.OperationCount), m =>
        {
            Assert.Equal(s_labels.OrderBy(l => l), m.Tags.Keys.OrderBy(k => k));
            Assert.Equal("connection", m.Tags[OperationMetrics.Kind]);
            Assert.Equal("1.0", m.Tags[OperationMetrics.SchemaVersion]);
            Assert.Equal("success", m.Tags[OperationMetrics.MetricOutcome]);
        });
        Assert.Equal(phases.Length, metrics.Of(OperationMetrics.OperationDuration).Count());
        Assert.Empty(metrics.Of(OperationMetrics.ErrorCount));
        Assert.Empty(metrics.Of(OperationMetrics.RetryCount));
    }

    private static TDSServerEndPoint StartEndpoint(TransientTdsErrorTdsServer server)
    {
        TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        return endpoint;
    }

    private static string CreateConnectionString(TDSServerEndPoint endpoint) =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:127.0.0.1,{endpoint.ServerEndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectRetryCount = 0,
            ConnectTimeout = 3
        }.ConnectionString;

    private static SqlRetryLogicBaseProvider CreateTwoAttemptProvider() =>
        SqlConfigurableRetryFactory.CreateFixedRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 2,
            DeltaTime = TimeSpan.Zero,
            MinTimeInterval = TimeSpan.Zero,
            MaxTimeInterval = TimeSpan.Zero,
            TransientErrors = new[] { 40501 }
        });

    /// <summary>One recorded measurement with its labels.</summary>
    private sealed record Measurement(
        string Name, double Value, IReadOnlyDictionary<string, object?> Tags);

    /// <summary>Subscribes to the SqlClient meter only, without any activity listener.</summary>
    private sealed class MetricsCapture : IDisposable
    {
        private readonly ConcurrentQueue<Measurement> _measurements = new();
        private readonly MeterListener _listener;

        internal MetricsCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == OperationMetrics.MeterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                _measurements.Enqueue(new(instrument.Name, value, ToDictionary(tags))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                _measurements.Enqueue(new(instrument.Name, value, ToDictionary(tags))));
            _listener.Start();
        }

        internal IEnumerable<Measurement> Of(string instrument, string? phase = null) =>
            _measurements.Where(m => m.Name == instrument &&
                (phase is null || Equals(m.Tags[OperationMetrics.Phase], phase)));

        public void Dispose() => _listener.Dispose();

        private static IReadOnlyDictionary<string, object?> ToDictionary(
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            Dictionary<string, object?> result = new();
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                result[tag.Key] = tag.Value;
            }
            return result;
        }
    }
}
