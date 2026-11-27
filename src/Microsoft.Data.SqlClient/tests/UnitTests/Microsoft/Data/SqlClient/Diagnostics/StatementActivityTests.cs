// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies statement spans against a simulated server: root and retry-attempt structure,
/// error attributes, gated query text, the disabled path and the operation metrics.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class StatementActivityTests
{
    private const string Sql = "select 1";

    /// <summary>A successful sync execute records one root under the caller's span.</summary>
    [Fact]
    public void ExecuteScalar_Success_RecordsSuccessfulRoot()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection);
        using StatementTrace trace = new();

        // Act
        command.ExecuteScalar();

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Assert.Equal("statement.execute", root.OperationName);
        Assert.Equal(ActivityKind.Client, root.Kind);
        Assert.Equal(trace.Parent.SpanId, root.ParentSpanId);
        Assert.Equal("ExecuteScalar", root.GetTagItem("db.operation.name"));
        Assert.Equal(1, root.GetTagItem("sqlclient.operation.attempt_count"));
        Assert.Equal("success", root.GetTagItem("sqlclient.operation.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, root.Status);
        Assert.Null(root.GetTagItem("db.query.text"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>ExecuteScalarAsync delegates internally but still records a single root.</summary>
    [Fact]
    public async Task ExecuteScalarAsync_Success_RecordsSingleRoot()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection);
        using StatementTrace trace = new();

        // Act
        await command.ExecuteScalarAsync();

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Assert.Equal("ExecuteScalar", root.GetTagItem("db.operation.name"));
        Assert.Equal(trace.Parent.SpanId, root.ParentSpanId);
        Assert.Equal("success", root.GetTagItem("sqlclient.operation.outcome"));
    }

    /// <summary>A sync server error records allow-listed attributes and no exception data.</summary>
    [Fact]
    public void ExecuteNonQuery_ServerError_RecordsCategoryWithoutSensitiveData()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection);
        server.SetErrorBehavior(true, 208, 1, "private-message");
        using StatementTrace trace = new();

        // Act
        SqlException exception = Assert.Throws<SqlException>(() => command.ExecuteNonQuery());

        // Assert
        Assert.Equal(208, exception.Number);
        AssertServerError(Assert.Single(trace.Spans), "execute");
    }

    /// <summary>An async server error is recorded when the returned task completes.</summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_ServerError_RecordsCategoryWithoutSensitiveData()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection);
        server.SetErrorBehavior(true, 208, 1, "private-message");
        using StatementTrace trace = new();

        // Act
        await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());

        // Assert
        AssertServerError(Assert.Single(trace.Spans), "execute");
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// A sync retry records ordered sibling attempts; only the failed attempt carries the error.
    /// </summary>
    [Fact]
    public void ExecuteNonQuery_RetryThenSuccess_RecordsOrderedAttempts()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection) { RetryLogicProvider = RetryOn208() };
        server.SetErrorBehavior(true, 208, 1, "private-message");
        using StatementTrace trace = new();

        // Act
        command.ExecuteNonQuery();

        // Assert
        AssertRetriedRoot(trace);
    }

    /// <summary>An async retry records the same ordered sibling attempts as the sync path.</summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_RetryThenSuccess_RecordsOrderedAttempts()
    {
        // Arrange
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection) { RetryLogicProvider = RetryOn208() };
        server.SetErrorBehavior(true, 208, 1, "private-message");
        using StatementTrace trace = new();

        // Act
        await command.ExecuteNonQueryAsync();

        // Assert
        AssertRetriedRoot(trace);
    }

    /// <summary>Opt-in query text is masked; without the switch no text is exported.</summary>
    [Fact]
    public void ExecuteScalar_QueryTextEnabled_RecordsMaskedText()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            EnableOpenTelemetryQueryText = true
        };
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new("select 1 /* private */ where 'secret' = 'secret'",
            connection);
        using StatementTrace trace = new();

        // Act
        command.ExecuteScalar();

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Assert.Equal("select ? where ? = ?", root.GetTagItem("db.query.text"));
    }

    /// <summary>Without a listener or private route no statement activity is created.</summary>
    [Fact]
    public void Start_NoListenerOrRoute_ReturnsNull()
    {
        // Arrange
        using SqlCommand command = new(Sql);

        // Act
        OperationScope? activity = StatementActivity.Start(command, "ExecuteScalar");

        // Assert
        Assert.Null(activity);
    }

    /// <summary>Completed roots report count, duration and the classified error category.</summary>
    [Fact]
    public void ExecuteNonQuery_ServerError_RecordsOperationMetrics()
    {
        // Arrange
        ConcurrentQueue<(string Name, IReadOnlyDictionary<string, object?> Tags)> measurements =
            new();
        using MeterListener listener = new()
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == OperationMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Enqueue((instrument.Name, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Enqueue((instrument.Name, ToDictionary(tags))));
        listener.Start();
        using TransientTdsErrorTdsServer server = StartServer();
        using SqlConnection connection = OpenConnection(server);
        using SqlCommand command = new(Sql, connection);
        server.SetErrorBehavior(true, 208, 1, "private-message");
        using StatementTrace trace = new();

        // Act
        Assert.Throws<SqlException>(() => command.ExecuteNonQuery());

        // Assert
        var statement = measurements
            .Where(m => Equals(m.Tags[OperationMetrics.Phase], "statement.execute")).ToArray();
        Assert.Equal(
            new[] { "db.client.operation.count", "db.client.operation.duration",
                "db.client.operation.error.count" },
            statement.Select(m => m.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(statement.Where(m => m.Name != "db.client.operation.error.count"),
            m => Assert.Equal("failure", m.Tags[OperationMetrics.MetricOutcome]));
        Assert.Equal("query_syntax_semantics", statement
            .Single(m => m.Name == "db.client.operation.error.count")
            .Tags[OperationMetrics.MetricCategory]);
    }

    private static void AssertServerError(Activity span, string phase)
    {
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Empty(span.Events);
        Assert.Equal("sqlserver.208", span.GetTagItem("error.type"));
        Assert.Equal("query_syntax_semantics", span.GetTagItem("sqlclient.error.category"));
        Assert.Equal("sql_server", span.GetTagItem("sqlclient.error.source"));
        Assert.Equal("sqlserver:208", span.GetTagItem("sqlclient.error.code"));
        Assert.Equal(phase, span.GetTagItem("sqlclient.error.phase"));
        Assert.DoesNotContain(span.TagObjects,
            tag => tag.Value is string value && value.Contains("private"));
    }

    private static void AssertRetriedRoot(StatementTrace trace)
    {
        Activity root = Assert.Single(trace.Spans,
            span => span.OperationName == "statement.execute");
        Activity[] attempts = trace.Spans
            .Where(span => span.OperationName == "statement.attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(root.SpanId, attempt.ParentSpanId));
        Assert.Equal(new object[] { 1, 2 },
            attempts.Select(attempt => attempt.GetTagItem("sqlclient.statement.attempt")));
        Assert.True(attempts[0].StartTimeUtc + attempts[0].Duration <= attempts[1].StartTimeUtc);
        AssertServerError(attempts[0], "attempt");
        Assert.Equal(ActivityStatusCode.Unset, attempts[1].Status);
        Assert.Null(attempts[1].GetTagItem("error.type"));
        Assert.Equal(2, root.GetTagItem("sqlclient.operation.attempt_count"));
        Assert.Equal("success", root.GetTagItem("sqlclient.operation.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, root.Status);
    }

    private static SqlRetryLogicBaseProvider RetryOn208() =>
        SqlConfigurableRetryFactory.CreateFixedRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 2,
            DeltaTime = TimeSpan.FromMilliseconds(10),
            TransientErrors = [208]
        });

    private static TransientTdsErrorTdsServer StartServer()
    {
        // Class 16 keeps the connection usable after the injected batch error.
        TransientTdsErrorTdsServer server = new(
            new TransientTdsErrorTdsServerArguments { ErrorClass = 16 });
        server.Start();
        return server;
    }

    private static SqlConnection OpenConnection(TransientTdsErrorTdsServer server)
    {
        SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
#if NETFRAMEWORK
#pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
            TransparentNetworkIPResolution = false,
#pragma warning restore 618
#endif
        }.ConnectionString);
        connection.Open();
        return connection;
    }

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
