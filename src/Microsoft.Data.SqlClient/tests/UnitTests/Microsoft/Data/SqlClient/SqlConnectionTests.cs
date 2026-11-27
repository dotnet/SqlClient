// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Unit tests for <see cref="SqlConnection"/> members that do not need a server.
/// </summary>
public class SqlConnectionTests
{
    /// <summary>
    /// A collector endpoint is accepted and remains available through the typed builder.
    /// </summary>
    [Fact]
    public void ConnectionString_CollectorEndpoint_RoundTrips()
    {
        // Arrange
        const string endpoint = "http://127.0.0.1:4318/v1/traces";
        SqlConnectionStringBuilder builder = new() { OtelEndpoint = endpoint };

        // Act
        using SqlConnection connection = new(builder.ConnectionString);

        // Assert
        Assert.Equal(endpoint, new SqlConnectionStringBuilder(connection.ConnectionString).OtelEndpoint);
    }

    /// <summary>
    /// Invalid collector addresses fail without echoing their potentially sensitive input.
    /// </summary>
    [Theory]
    [InlineData("file:///private")]
    [InlineData("https://user:secret@example.com/v1/traces")]
    [InlineData("https://example.com/v1/traces?token=secret")]
    [InlineData("https://example.com/v1/traces#secret")]
    public void ConnectionString_UnsafeCollectorEndpoint_ThrowsWithoutValue(string endpoint)
    {
        // Arrange
        SqlConnectionStringBuilder builder = new();

        // Act
        ArgumentException error = Assert.Throws<ArgumentException>(() => builder.OtelEndpoint = endpoint);

        // Assert
        Assert.DoesNotContain(endpoint, error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    /// <summary>
    /// Raw connection strings reject unsafe endpoints without exposing their values.
    /// </summary>
    [Fact]
    public void ConnectionString_UnsafeRawCollectorEndpoint_ThrowsWithoutValue()
    {
        // Act
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new SqlConnection("otelEndpoint=https://example.com/v1/traces?token=secret"));

        // Assert
        Assert.DoesNotContain("secret", error.Message);
    }

    /// <summary>
    /// The compact keyword aliases the typed property and removal restores its default.
    /// </summary>
    [Fact]
    public void ConnectionString_CollectorEndpointAliasAndRemoval_RestoresDefault()
    {
        // Arrange
        SqlConnectionStringBuilder builder = new("otelEndpoint=http://127.0.0.1:4318/v1/traces");

        // Act
        bool removed = builder.Remove("otelEndpoint");

        // Assert
        Assert.True(removed);
        Assert.Equal(string.Empty, builder.OtelEndpoint);
        Assert.False(builder.ShouldSerialize("OTel Endpoint"));
    }

    /// <summary>
    /// An absent optional telemetry extension does not prevent an otherwise successful open.
    /// </summary>
    [Fact]
    public void Open_MissingTelemetryExtension_OpensNormally()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments());
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        SqlConnectionStringBuilder builder = new(CreateConnectionString(endpoint))
        {
            OtelEndpoint = "http://127.0.0.1:4318/v1/traces"
        };
        using SqlConnection connection = new(builder.ConnectionString);

        // Act
        connection.Open();

        // Assert
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(trace.Spans).Status);
    }

    /// <summary>
    /// Synchronous client validation failures are recorded without guessing a server cause.
    /// </summary>
    [Fact]
    public void Open_MissingConnectionString_RecordsClientFailure()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection connection = new();

        // Act
        Assert.Throws<InvalidOperationException>(() => connection.Open());

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("unknown", span.GetTagItem("error.type"));
        Assert.Equal("unknown", span.GetTagItem("sqlclient.error.category"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// Immediate async failures create one span even when legacy diagnostics fire twice.
    /// </summary>
    [Fact]
    public async Task OpenAsync_MissingConnectionString_RecordsOneClientFailure()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection connection = new();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.OpenAsync());

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("unknown", span.GetTagItem("error.type"));
        Assert.Equal("unknown", span.GetTagItem("sqlclient.error.source"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// A synchronous open creates exactly one client span and restores its caller's activity.
    /// </summary>
    [Fact]
    public void Open_LoopbackServer_RecordsOneSuccessfulSpan()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments());
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        connection.Open();

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Null(span.GetTagItem("error.type"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// Async completion closes the span before returning and does not leak Activity.Current.
    /// </summary>
    [Fact]
    public async Task OpenAsync_LoopbackServer_RecordsOneSuccessfulSpan()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments());
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        Task open = connection.OpenAsync();
        Assert.Same(trace.Parent, Activity.Current);
        await open;

        // Assert
        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(trace.Spans).Status);
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>A real synchronous login has a physical-open child beneath its open invocation.</summary>
    [Fact]
    public void Open_LoopbackServer_RecordsPhysicalAndLoginPhases()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments());
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        connection.Open();

        // Assert
        Activity invocation = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.open.attempt");
        Activity physical = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.physical_open");
        Activity configuration = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.configuration");
        Activity endpointAttempt = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.endpoint_attempt");
        Activity transport = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.transport");
        Activity preloginSend = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.prelogin.send");
        Activity preloginReceive = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.prelogin.receive");
        Activity login = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.login");
        Activity initialization = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.initialization");
        Assert.Equal(invocation.SpanId, physical.ParentSpanId);
        Assert.Equal(physical.SpanId, configuration.ParentSpanId);
        Assert.Equal(physical.SpanId, endpointAttempt.ParentSpanId);
        Assert.Equal(endpointAttempt.SpanId, transport.ParentSpanId);
        Assert.Equal(endpointAttempt.SpanId, preloginSend.ParentSpanId);
        Assert.Equal(endpointAttempt.SpanId, preloginReceive.ParentSpanId);
        Assert.Equal(endpointAttempt.SpanId, login.ParentSpanId);
        Assert.Equal(login.SpanId, initialization.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Unset, login.Status);
    }

    /// <summary>A redirect creates two ordered endpoint attempts under one physical open.</summary>
    [Fact]
    public void Open_RoutedServer_RecordsOrderedEndpointAttempts()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer destination = new(new TdsServerArguments());
        using TDSServerEndPoint destinationEndpoint = new(destination)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        destinationEndpoint.Start();
        using RoutingTdsServer router = new(new RoutingTdsServerArguments
        {
            RoutingTCPHost = "127.0.0.1",
            RoutingTCPPort = (ushort)destinationEndpoint.ServerEndPoint.Port
        });
        using TDSServerEndPoint routerEndpoint = new(router)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        routerEndpoint.Start();
        SqlConnectionStringBuilder builder = new(CreateConnectionString(routerEndpoint))
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        using SqlConnection connection = new(builder.ConnectionString);

        // Act
        connection.Open();

        // Assert
        Activity physical = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.physical_open");
        Activity[] attempts = trace.AllSpans.Where(
            span => span.OperationName == "connection.endpoint_attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Activity redirect = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.redirect");
        Activity initialization = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.initialization");
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(physical.SpanId, attempt.ParentSpanId));
        Assert.Equal(physical.SpanId, redirect.ParentSpanId);
        Assert.True(attempts[0].StartTimeUtc + attempts[0].Duration <= redirect.StartTimeUtc);
        Assert.True(redirect.StartTimeUtc + redirect.Duration <= attempts[1].StartTimeUtc);
        Assert.Equal(ActivityStatusCode.Unset, initialization.Status);
    }

    /// <summary>Asynchronous redirect preserves the same physical endpoint ordering.</summary>
    [Fact]
    public async Task OpenAsync_RoutedServer_RecordsOrderedEndpointAttempts()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer destination = new(new TdsServerArguments());
        using TDSServerEndPoint destinationEndpoint = new(destination)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        destinationEndpoint.Start();
        using RoutingTdsServer router = new(new RoutingTdsServerArguments
        {
            RoutingTCPHost = "127.0.0.1",
            RoutingTCPPort = (ushort)destinationEndpoint.ServerEndPoint.Port
        });
        using TDSServerEndPoint routerEndpoint = new(router)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        routerEndpoint.Start();
        SqlConnectionStringBuilder builder = new(CreateConnectionString(routerEndpoint))
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        using SqlConnection connection = new(builder.ConnectionString);

        // Act
        await connection.OpenAsync();

        // Assert
        Activity physical = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.physical_open");
        Activity[] attempts = trace.AllSpans.Where(
            span => span.OperationName == "connection.endpoint_attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Activity redirect = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.redirect");
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(physical.SpanId, attempt.ParentSpanId));
        Assert.Equal(physical.SpanId, redirect.ParentSpanId);
        Assert.True(attempts[0].StartTimeUtc + attempts[0].Duration <= redirect.StartTimeUtc);
        Assert.True(redirect.StartTimeUtc + redirect.Duration <= attempts[1].StartTimeUtc);
    }

    /// <summary>A built-in transient retry records both endpoint attempts within one physical open.</summary>
    [Fact]
    public void Open_InternalTransientRetry_RecordsOrderedEndpointAttempts()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40613, 1, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        SqlConnectionStringBuilder builder = new(CreateConnectionString(endpoint))
        {
            ConnectRetryCount = 1,
            ConnectRetryInterval = 1
        };
        using SqlConnection connection = new(builder.ConnectionString);

        // Act
        connection.Open();

        // Assert
        Activity physical = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.physical_open");
        Activity[] attempts = trace.AllSpans.Where(
            span => span.OperationName == "connection.endpoint_attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(physical.SpanId, attempt.ParentSpanId));
        Assert.Equal("sqlserver.40613", attempts[0].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, attempts[0].Status);
        Assert.Equal(ActivityStatusCode.Unset, attempts[1].Status);
        Assert.Null(attempts[1].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(trace.Spans).Status);
    }

    /// <summary>Async built-in retry does not contaminate the successful later endpoint attempt.</summary>
    [Fact]
    public async Task OpenAsync_InternalTransientRetry_RecordsOrderedEndpointAttempts()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40613, 1, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        SqlConnectionStringBuilder builder = new(CreateConnectionString(endpoint))
        {
            ConnectRetryCount = 1,
            ConnectRetryInterval = 1
        };
        using SqlConnection connection = new(builder.ConnectionString);

        // Act
        await connection.OpenAsync();

        // Assert
        Activity physical = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.physical_open");
        Activity[] attempts = trace.AllSpans.Where(
            span => span.OperationName == "connection.endpoint_attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(physical.SpanId, attempt.ParentSpanId));
        Assert.Equal("sqlserver.40613", attempts[0].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, attempts[0].Status);
        Assert.Equal(ActivityStatusCode.Unset, attempts[1].Status);
        Assert.Null(attempts[1].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(trace.Spans).Status);
    }

    /// <summary>A real token callback is represented inside the login response processing.</summary>
    [Fact]
    public void Open_TokenCallback_RecordsTokenRequest()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments
        {
            FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired
        });
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            AccessTokenCallback = (_, _) =>
                Task.FromResult(new SqlAuthenticationToken("invalid", DateTimeOffset.MaxValue))
        };

        // Act
        connection.Open();

        // Assert
        Activity login = Assert.Single(trace.AllSpans, span => span.OperationName == "connection.login");
        Activity token = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.token_request");
        Assert.Equal(login.SpanId, token.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Unset, token.Status);
        Assert.DoesNotContain("invalid", string.Join(";", token.TagObjects));
    }

    /// <summary>Token callback failures are captured before login translates them.</summary>
    [Fact]
    public async Task OpenAsync_TokenCallbackThrows_RecordsTokenFailure()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments
        {
            FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired
        });
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            AccessTokenCallback = (_, _) =>
                Task.FromException<SqlAuthenticationToken>(new InvalidOperationException("private-message"))
        };

        // Act
        Exception? error = await Record.ExceptionAsync(() => connection.OpenAsync());

        // Assert
        Assert.NotNull(error);
        Activity token = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.token_request");
        Assert.Equal(ActivityStatusCode.Error, token.Status);
        Assert.Empty(token.Events);
        Assert.DoesNotContain("private-message", string.Join(";", token.TagObjects));
    }

    /// <summary>An asynchronous rejected login records the server error at the login origin.</summary>
    [Fact]
    public async Task OpenAsync_LoginRejected_RecordsPhysicalLoginFailure()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 18456, int.MaxValue, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

        // Assert
        Activity invocation = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.open.attempt");
        Activity physical = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.physical_open");
        Activity endpointAttempt = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.endpoint_attempt");
        Activity login = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.login");
        Assert.Equal(invocation.SpanId, physical.ParentSpanId);
        Assert.Equal(physical.SpanId, endpointAttempt.ParentSpanId);
        Assert.Equal(endpointAttempt.SpanId, login.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Error, login.Status);
        Assert.Equal("sqlserver.18456", login.GetTagItem("error.type"));
        Assert.Empty(login.Events);
    }

    /// <summary>Reusing a pooled connection does not invent a second physical-open span.</summary>
    [Fact]
    public void Open_PooledConnectionReused_DoesNotRecordAnotherPhysicalOpen()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TdsServer server = new(new TdsServerArguments());
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        SqlConnectionStringBuilder builder = new(CreateConnectionString(endpoint))
        {
            Pooling = true,
            ApplicationName = Guid.NewGuid().ToString()
        };
        using SqlConnection connection = new(builder.ConnectionString);

        try
        {
            // Act
            connection.Open();
            connection.Close();
            connection.Open();

            // Assert
            Assert.Equal(2, trace.Spans.Count);
            Assert.Single(trace.AllSpans, span => span.OperationName == "connection.physical_open");
        }
        finally
        {
            // Cleanup
            connection.Close();
            SqlConnection.ClearPool(connection);
        }
    }

    /// <summary>
    /// Real ERROR-token parsing carries server provenance into synchronous failure telemetry.
    /// </summary>
    [Theory]
    [InlineData(18456, "authentication")]
    [InlineData(4060, "database_open")]
    [InlineData(40501, "resource_throttling")]
    public void Open_ServerRejectsLogin_RecordsOneCategorizedSpan(int number, string category)
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, (uint)number, int.MaxValue, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        Assert.Throws<SqlException>(() => connection.Open());

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(category, span.GetTagItem("sqlclient.error.category"));
        Assert.Equal($"sqlserver.{number}", span.GetTagItem("error.type"));
        Assert.Empty(span.Events);
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// Async failure is classified from the actual SqlException, without duplicate terminal spans.
    /// </summary>
    [Theory]
    [InlineData(18456, "authentication")]
    [InlineData(4060, "database_open")]
    [InlineData(40501, "resource_throttling")]
    public async Task OpenAsync_ServerRejectsLogin_RecordsOneCategorizedSpan(
        int number, string category)
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, (uint)number, int.MaxValue, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint));

        // Act
        await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(category, span.GetTagItem("sqlclient.error.category"));
        Assert.Equal($"sqlserver.{number}", span.GetTagItem("error.type"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// Cancellation is an error outcome even though the legacy diagnostic hook reports After.
    /// </summary>
    [Fact]
    public async Task OpenAsync_PreCanceledToken_RecordsCancellation()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection connection = new("Server=tcp:127.0.0.1,1;Pooling=false");

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connection.OpenAsync(new CancellationToken(true)));

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("client.canceled", span.GetTagItem("error.type"));
        Assert.Single(trace.AllSpans);
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>A synchronous pre-attempt diagnostic failure is recorded without a phantom child.</summary>
    [Fact]
    public void Open_BeforeDiagnosticThrows_RecordsRootOnly()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection connection = new();
        using ThrowingOpenObserver observer = new(connection);

        // Act
        Assert.Throws<InvalidOperationException>(() => connection.Open());

        // Assert
        Activity root = Assert.Single(trace.AllSpans);
        Assert.Equal(ActivityStatusCode.Error, root.Status);
        Assert.Equal("unknown", root.GetTagItem("error.type"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>An asynchronous pre-attempt diagnostic failure is recorded without a phantom child.</summary>
    [Fact]
    public async Task OpenAsync_BeforeDiagnosticThrows_RecordsRootOnly()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection connection = new();
        using ThrowingOpenObserver observer = new(connection);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.OpenAsync());

        // Assert
        Activity root = Assert.Single(trace.AllSpans);
        Assert.Equal(ActivityStatusCode.Error, root.Status);
        Assert.Equal("unknown", root.GetTagItem("error.type"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// A recovered retry records both real invocations without marking the successful root failed.
    /// </summary>
    [Fact]
    public void Open_TransientLoginFailure_RecordsFailedAndSuccessfulAttempts()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40501, 1, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            RetryLogicProvider = CreateTwoAttemptProvider()
        };

        // Act
        connection.Open();

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Activity[] attempts = trace.AllSpans.Where(span => span.OperationName == "connection.open.attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(root.SpanId, attempt.ParentSpanId));
        Assert.All(attempts, attempt =>
        {
            Assert.True(attempt.StartTimeUtc >= root.StartTimeUtc);
            Assert.True(attempt.StartTimeUtc + attempt.Duration <= root.StartTimeUtc + root.Duration);
        });
        Assert.Equal(ActivityStatusCode.Error, attempts[0].Status);
        Assert.Equal("sqlserver.40501", attempts[0].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, attempts[1].Status);
        Assert.Null(attempts[1].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, root.Status);
        Assert.Null(root.GetTagItem("error.type"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// A recovered async retry leaves the later attempt and overall operation successful.
    /// </summary>
    [Fact]
    public async Task OpenAsync_TransientLoginFailure_RecordsFailedAndSuccessfulAttempts()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40501, 1, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            RetryLogicProvider = CreateTwoAttemptProvider()
        };

        // Act
        await connection.OpenAsync();

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Activity[] attempts = trace.AllSpans.Where(span => span.OperationName == "connection.open.attempt")
            .OrderBy(span => span.StartTimeUtc).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt => Assert.Equal(root.SpanId, attempt.ParentSpanId));
        Assert.Equal(ActivityStatusCode.Error, attempts[0].Status);
        Assert.Equal("sqlserver.40501", attempts[0].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, attempts[1].Status);
        Assert.Null(attempts[1].GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Unset, root.Status);
        Assert.Null(root.GetTagItem("error.type"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// Exhausted synchronous retries retain their underlying failures at both lifecycle levels.
    /// </summary>
    [Fact]
    public void Open_ExhaustedTransientFailures_RecordsAttemptsAndRootCause()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40501, int.MaxValue, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            RetryLogicProvider = CreateTwoAttemptProvider()
        };

        // Act
        Assert.Throws<AggregateException>(() => connection.Open());

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Activity[] attempts = trace.AllSpans.Where(span => span.OperationName == "connection.open.attempt")
            .ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt =>
        {
            Assert.Equal(root.SpanId, attempt.ParentSpanId);
            Assert.Equal(ActivityStatusCode.Error, attempt.Status);
            Assert.Equal("sqlserver.40501", attempt.GetTagItem("error.type"));
        });
        Assert.Equal(ActivityStatusCode.Error, root.Status);
        Assert.Equal("sqlserver.40501", root.GetTagItem("error.type"));
        Assert.Equal("resource_throttling", root.GetTagItem("sqlclient.error.category"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>
    /// An exhausted retry keeps actual attempt errors and summarizes them on the async root.
    /// </summary>
    [Fact]
    public async Task OpenAsync_ExhaustedTransientFailures_RecordsAttemptsAndRootCause()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        server.SetErrorBehavior(true, 40501, int.MaxValue, "private-message");
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using SqlConnection connection = new(CreateConnectionString(endpoint))
        {
            RetryLogicProvider = CreateTwoAttemptProvider()
        };

        // Act
        await Assert.ThrowsAsync<AggregateException>(() => connection.OpenAsync());

        // Assert
        Activity root = Assert.Single(trace.Spans);
        Activity[] attempts = trace.AllSpans.Where(span => span.OperationName == "connection.open.attempt")
            .ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.All(attempts, attempt =>
        {
            Assert.Equal(root.SpanId, attempt.ParentSpanId);
            Assert.Equal(ActivityStatusCode.Error, attempt.Status);
            Assert.Equal("sqlserver.40501", attempt.GetTagItem("error.type"));
            Assert.Empty(attempt.Events);
        });
        Assert.Equal(ActivityStatusCode.Error, root.Status);
        Assert.Equal("sqlserver.40501", root.GetTagItem("error.type"));
        Assert.Equal("resource_throttling", root.GetTagItem("sqlclient.error.category"));
        Assert.Same(trace.Parent, Activity.Current);
    }

    /// <summary>Creates a bounded retry policy for the simulated transient login error.</summary>
    /// <returns>A provider that invokes the open callback at most twice.</returns>
    private static SqlRetryLogicBaseProvider CreateTwoAttemptProvider() =>
        SqlConfigurableRetryFactory.CreateFixedRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 2,
            DeltaTime = TimeSpan.Zero,
            MinTimeInterval = TimeSpan.Zero,
            MaxTimeInterval = TimeSpan.Zero,
            TransientErrors = new[] { 40501 }
        });

    /// <summary>Throws only for the target connection's existing before-open diagnostic event.</summary>
    private sealed class ThrowingOpenObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly object _gate = new();
        private readonly List<IDisposable> _listeners = new();
        private readonly IDisposable _subscription;
        private bool _disposed;

        internal ThrowingOpenObserver(SqlConnection connection)
        {
            _connection = connection;
            _subscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "SqlClientDiagnosticListener")
            {
                lock (_gate)
                {
                    if (!_disposed)
                    {
                        _listeners.Add(listener.Subscribe(this));
                    }
                }
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is SqlClientConnectionOpenBefore before &&
                ReferenceEquals(before.Connection, _connection))
            {
                throw new InvalidOperationException("private diagnostic detail");
            }
        }

        public void OnError(Exception error) { }
        public void OnCompleted() { }

        public void Dispose()
        {
            _subscription.Dispose();
            lock (_gate)
            {
                _disposed = true;
                foreach (IDisposable listener in _listeners)
                {
                    listener.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Creates a bounded, credential-free connection to the simulated loopback endpoint.
    /// </summary>
    /// <param name="endpoint">The running test endpoint.</param>
    /// <returns>A connection string that disables pooling and reconnect retries.</returns>
    private static string CreateConnectionString(TDSServerEndPoint endpoint) =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:127.0.0.1,{endpoint.ServerEndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectRetryCount = 0,
            ConnectTimeout = 3
        }.ConnectionString;

    /// <summary>
    /// Verifies the getter returns a non-null empty snapshot on a new connection.
    /// </summary>
    [Fact]
    public void EncryptionInfo_NewConnection_ReturnsEmptySnapshot()
    {
        // Arrange
        using SqlConnection connection = new();

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }

    /// <summary>
    /// Verifies the getter returns a non-null empty snapshot on a connection with a connection
    /// string that has not been opened.
    /// </summary>
    [Fact]
    public void EncryptionInfo_UnopenedWithConnectionString_ReturnsEmptySnapshot()
    {
        // Arrange
        using SqlConnection connection = new("Data Source=localhost;Encrypt=Strict");

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }

    /// <summary>
    /// Verifies the getter returns a non-null empty snapshot after the connection is closed.
    /// </summary>
    [Fact]
    public void EncryptionInfo_ClosedConnection_ReturnsEmptySnapshot()
    {
        // Arrange
        using SqlConnection connection = new("Data Source=localhost");
        connection.Close();

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }

    /// <summary>
    /// Verifies the getter does not throw and returns a non-null empty snapshot after the
    /// connection is disposed.
    /// </summary>
    [Fact]
    public void EncryptionInfo_DisposedConnection_ReturnsEmptySnapshot()
    {
        // Arrange
        SqlConnection connection = new("Data Source=localhost");
        connection.Dispose();

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }
}
