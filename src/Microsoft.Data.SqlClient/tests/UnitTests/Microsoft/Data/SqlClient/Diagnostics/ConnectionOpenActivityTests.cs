// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Diagnostics;
#if NET
using Microsoft.Data.SqlClient.ManagedSni;
#endif
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies conservative classification and the privacy boundary on connection-open spans.
/// </summary>
public class ConnectionOpenActivityTests
{
    /// <summary>
    /// A private route is visible to samplers at creation only, then is removed from the tags
    /// and kept as a non-exported custom property; unrouted starts pass no creation tags.
    /// </summary>
    [Fact]
    public void StartRouted_RouteAndNoRoute_PassesMarkerOnlyAtCreation()
    {
        // Arrange
        using ActivitySource source = new("Microsoft.Data.SqlClient.Test.Routed");
        object? seen = null;
        int untaggedStarts = 0;
        using ActivityListener listener = new()
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                if (options.Tags is null)
                {
                    untaggedStarts++;
                    return ActivitySamplingResult.AllDataAndRecorded;
                }
                foreach (var tag in options.Tags)
                {
                    if (tag.Key == ConnectionOpenActivity.RouteTag)
                    {
                        seen = tag.Value;
                    }
                }
                return ActivitySamplingResult.AllDataAndRecorded;
            }
        };
        ActivitySource.AddActivityListener(listener);
        using RouteStub route = new();

        // Act
        using Activity? routed = ConnectionOpenActivity.StartRouted(
            source, "connection.open", ActivityKind.Client, null, route);
        using Activity? unrouted = ConnectionOpenActivity.StartRouted(
            source, "connection.open", ActivityKind.Client, null, null);

        // Assert
        Assert.Same(route, seen);
        Assert.Equal(1, untaggedStarts);
        Assert.NotNull(routed);
        Assert.Null(routed.GetTagItem(ConnectionOpenActivity.RouteTag));
        Assert.Same(route, routed.GetCustomProperty(ConnectionOpenActivity.RouteProperty));
        Assert.NotNull(unrouted);
        Assert.Empty(unrouted.TagObjects);
        Assert.Null(unrouted.GetCustomProperty(ConnectionOpenActivity.RouteProperty));
    }

    /// <summary>Stands in for an opaque private-route lease.</summary>
    private sealed class RouteStub : IDisposable
    {
        public void Dispose()
        {
        }
    }
    /// <summary>Physical work cannot attach to a different or completed open invocation.</summary>
    [Fact]
    public void StartPhysical_WrongOrCompletedOwner_DoesNotStart()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection owner = new();
        using SqlConnection other = new();
        using OperationScope? root = ConnectionOpenActivity.Start();
        using OperationScope? attempt = ConnectionOpenActivity.StartAttempt(root, owner);

        // Act
        OperationScope? wrong = ConnectionOpenActivity.StartPhysical(other, out IDisposable? wrongRoute);
        ConnectionOpenActivity.FinishAttempt(attempt);
        OperationScope? late = ConnectionOpenActivity.StartPhysical(owner, out IDisposable? lateRoute);

        // Assert
        Assert.Null(wrong);
        Assert.Null(wrongRoute);
        Assert.Null(late);
        Assert.Null(lateRoute);
    }

#if NET
    /// <summary>The managed resolver records its actual call beneath physical transport work.</summary>
    [Fact]
    public void ResolveHostAddresses_NumericAddress_RecordsResolutionPhase()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection owner = new();
        using OperationScope? root = ConnectionOpenActivity.Start();
        using OperationScope? attempt = ConnectionOpenActivity.StartAttempt(root, owner);
        using OperationScope? physical = ConnectionOpenActivity.StartPhysical(owner, out IDisposable? route);

        // Act
        System.Net.IPAddress[] addresses = SniCommon.ResolveHostAddresses("127.0.0.1");

        // Assert
        Assert.NotEmpty(addresses);
        Activity resolution = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.name_resolution");
        Assert.Equal(physical?.Activity?.SpanId, resolution.ParentSpanId);

        // Cleanup
        route?.Dispose();
        ConnectionOpenActivity.FinishAttempt(attempt);
    }
#endif
    /// <summary>
    /// Uses only confirmed ERROR tokens for server categories and namespaced error types.
    /// </summary>
    [Theory]
    [InlineData(18456, "authentication")]
    [InlineData(4060, "database_open")]
    [InlineData(40501, "resource_throttling")]
    [InlineData(10928, "resource_throttling")]
    [InlineData(10929, "resource_throttling")]
    [InlineData(50000, "unknown")]
    public void RecordError_ServerError_RecordsCategoryWithoutSensitiveData(int number, string category)
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using OperationScope? activity = ConnectionOpenActivity.Start();
        SqlError error = new(number, 1, 16, "private-server", "private-message",
            "private-procedure", 42) { IsServerError = true };
        SqlException exception = SqlException.CreateException(new SqlErrorCollection { error }, "");

        // Act
        ConnectionOpenActivity.RecordError(activity, exception);
        activity?.Dispose();

        // Assert
        Activity span = Assert.Single(trace.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal($"sqlserver.{number}",
            span.GetTagItem("error.type"));
        Assert.Equal(category, span.GetTagItem("sqlclient.error.category"));
        Assert.Equal("sql_server", span.GetTagItem("sqlclient.error.source"));
        Assert.Null(span.StatusDescription);
        Assert.Empty(span.Events);
        Assert.Equal("failure", span.GetTagItem("sqlclient.operation.outcome"));
        Assert.Equal(8, System.Linq.Enumerable.Count(span.TagObjects));
        Assert.DoesNotContain("private", string.Join(";", span.TagObjects));
    }

    /// <summary>
    /// A matching client-generated number is not evidence that SQL Server sent that error.
    /// </summary>
    [Theory]
    [InlineData(18456)]
    [InlineData(4060)]
    [InlineData(40501)]
    [InlineData(-2)]
    public void RecordError_ClientNumberCollision_RemainsUnknown(int number)
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using OperationScope? activity = ConnectionOpenActivity.Start();
        SqlError error = new(number, 1, 20, "server", "18456 authentication timeout", "", 0);
        SqlException exception = SqlException.CreateException(new SqlErrorCollection { error }, "");

        // Act
        ConnectionOpenActivity.RecordError(activity, exception);
        activity?.Dispose();

        // Assert
        Assert.Equal("unknown", activity?.Activity?.GetTagItem("sqlclient.error.category"));
        Assert.Equal("unknown", activity?.Activity?.GetTagItem("error.type"));
        Assert.Equal("unknown", activity?.Activity?.GetTagItem("sqlclient.error.source"));
    }

    /// <summary>
    /// Supplemental server errors do not override the first error-severity entry.
    /// </summary>
    [Fact]
    public void RecordError_MultipleServerErrors_UsesPrimaryError()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using OperationScope? activity = ConnectionOpenActivity.Start();
        SqlErrorCollection errors = new()
        {
            new SqlError(18456, 1, 16, "", "", "", 0) { IsServerError = true },
            new SqlError(4060, 1, 16, "", "", "", 0) { IsServerError = true }
        };

        // Act
        ConnectionOpenActivity.RecordError(activity, SqlException.CreateException(errors, ""));
        activity?.Dispose();

        // Assert
        Assert.Equal("sqlserver.18456", activity?.Activity?.GetTagItem("error.type"));
        Assert.Equal("authentication", activity?.Activity?.GetTagItem("sqlclient.error.category"));
    }

    /// <summary>
    /// The explicit SNI timeout code distinguishes a client timeout from a number collision.
    /// </summary>
    [Fact]
    public void RecordError_Win32WaitTimeout_RecordsNamespacedTimeout()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using OperationScope? activity = ConnectionOpenActivity.Start();
        SqlError error = new(-2, 0, 11, "", "private-message", "", 0, 258);

        // Act
        ConnectionOpenActivity.RecordError(activity,
            SqlException.CreateException(new SqlErrorCollection { error }, ""));
        activity?.Dispose();

        // Assert
        Assert.Equal("os.258", activity?.Activity?.GetTagItem("error.type"));
        Assert.Equal("timeout", activity?.Activity?.GetTagItem("sqlclient.error.category"));
        Assert.Equal("os", activity?.Activity?.GetTagItem("sqlclient.error.source"));
    }

    /// <summary>Conflicting failures in a retry aggregate must not be assigned one cause.</summary>
    [Fact]
    public void RecordError_ConflictingAggregate_RemainsUnknown()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using OperationScope? activity = ConnectionOpenActivity.Start();
        SqlError first = new(18456, 1, 16, "", "", "", 0) { IsServerError = true };
        SqlError second = new(4060, 1, 16, "", "", "", 0) { IsServerError = true };
        AggregateException exception = new(
            SqlException.CreateException(new SqlErrorCollection { first }, ""),
            SqlException.CreateException(new SqlErrorCollection { second }, ""));

        // Act
        ConnectionOpenActivity.RecordError(activity, exception);

        // Assert
        Assert.Equal("unknown", activity?.Activity?.GetTagItem("error.type"));
        Assert.Equal("unknown", activity?.Activity?.GetTagItem("sqlclient.error.category"));
    }

    /// <summary>
    /// The retry reason comes from the driver's login transient-error numbers and the timeout
    /// category only, so it stays within a closed value set.
    /// </summary>
    [Theory]
    [InlineData((int)ConnectionErrorSource.SqlServer, 4060,
        (int)ConnectionErrorCategory.DatabaseOpen, "transient_server_error")]
    [InlineData((int)ConnectionErrorSource.SqlServer, 40613,
        (int)ConnectionErrorCategory.Unknown, "transient_server_error")]
    [InlineData((int)ConnectionErrorSource.Client, null,
        (int)ConnectionErrorCategory.Timeout, "timeout")]
    [InlineData((int)ConnectionErrorSource.SqlServer, 18456,
        (int)ConnectionErrorCategory.Authentication, "other")]
    [InlineData((int)ConnectionErrorSource.Sni, 4060,
        (int)ConnectionErrorCategory.Unknown, "other")]
    public void RetryReason_Evidence_ReturnsClosedReason(
        int source, int? code, int category, string expected)
    {
        // Arrange
        ConnectionErrorEvidence evidence =
            ConnectionErrorEvidence.Create(
                (ConnectionErrorSource)source, code, null, "connection.login");

        // Act
        string reason = ConnectionOpenActivity.RetryReason(
            evidence, (ConnectionErrorCategory)category);

        // Assert
        Assert.Equal(expected, reason);
    }
}