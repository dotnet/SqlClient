// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>Validates structured connection error classification and its export allowlist.</summary>
public class ConnectionErrorClassifierTests
{
    /// <summary>Known SQL Server errors use the documented SqlClient category mapping.</summary>
    [Theory]
    [InlineData(18456, "authentication")]
    [InlineData(18486, "authentication")]
    [InlineData(18488, "authentication")]
    [InlineData(4060, "database_open")]
    [InlineData(40615, "access_policy")]
    [InlineData(40501, "resource_throttling")]
    [InlineData(10928, "resource_throttling")]
    [InlineData(10929, "resource_throttling")]
    [InlineData(49918, "resource_throttling")]
    [InlineData(49919, "resource_throttling")]
    [InlineData(49920, "resource_throttling")]
    [InlineData(40197, "server_availability")]
    [InlineData(10054, "network_connectivity")]
    [InlineData(50000, "unknown")]
    public void Classify_ServerError_UsesSourceNamespacedCode(int number, string expectedCategory)
    {
        // Arrange
        ConnectionErrorEvidence evidence = CaptureServerError(number);

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(evidence);

        // Assert
        Assert.Equal(expectedCategory, ConnectionErrorAttributePolicy.Create(result)[
            ConnectionErrorAttributePolicy.Category]);
        Assert.Equal($"sqlserver.{number}", ConnectionErrorAttributePolicy.Create(result)[
            ConnectionErrorAttributePolicy.ErrorType]);
        Assert.Equal("sql_server", ConnectionErrorAttributePolicy.Create(result)[
            ConnectionErrorAttributePolicy.Source]);
    }

    /// <summary>A database-open error takes precedence over its accompanying login error.</summary>
    [Fact]
    public void Classify_DatabaseOpenWithLoginError_UsesPrimaryError()
    {
        // Arrange
        SqlException exception = SqlException.CreateException(
            new SqlErrorCollection { ServerError(4060), ServerError(18456) }, "test");

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.login"));

        // Assert
        Assert.Equal(ConnectionErrorCategory.DatabaseOpen, result.Category);
        Assert.Equal("sqlserver.4060", result.ErrorType);
        Assert.Equal(ConnectionErrorSource.SqlServer, result.Source);
    }

    /// <summary>Informational entries cannot displace the first error-severity entry.</summary>
    [Fact]
    public void Classify_InformationalEntriesAroundError_UsesFirstErrorSeverityEntry()
    {
        // Arrange
        SqlError information = new(18456, 1, 10, "", "private-info", "", 0)
        {
            SniErrorNumber = 31
        };
        SqlException exception = SqlException.CreateException(
            new SqlErrorCollection { information, ServerError(4060), information }, "test");

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.login"));

        // Assert
        Assert.Equal(ConnectionErrorCategory.DatabaseOpen, result.Category);
        Assert.Equal("sqlserver.4060", result.ErrorType);
        Assert.Equal(ConnectionErrorSource.SqlServer, result.Source);
    }

    /// <summary>Informational entries alone do not establish failure evidence.</summary>
    [Fact]
    public void Classify_OnlyInformationalEntries_ReturnsUnknown()
    {
        // Arrange
        SqlException exception = SqlException.CreateException(
            new SqlErrorCollection
            {
                new SqlError(4060, 1, 10, "", "private-info", "", 0) { SniErrorNumber = 31 }
            }, "test");

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.open"));

        // Assert
        Assert.Equal(ConnectionErrorCategory.Unknown, result.Category);
        Assert.Equal("unknown", result.ErrorType);
        Assert.Equal(ConnectionErrorSource.Unknown, result.Source);
    }

    /// <summary>SNI codes are retained separately from native and server error numbers.</summary>
    [Theory]
    [InlineData(11, "timeout")]
    [InlineData(31, "tls_security")]
    [InlineData(35, "internal_error")]
    [InlineData(26, "unknown")]
    public void Classify_SniError_UsesSniNamespace(int code, string expectedCategory)
    {
        // Arrange
        SqlError error = new(0, 0, 20, "", "", "", 0) { SniErrorNumber = (uint)code };
        SqlException exception = SqlException.CreateException(
            new SqlErrorCollection { error }, "test");

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.transport"));

        // Assert
        Assert.Equal(expectedCategory, ConnectionErrorAttributePolicy.Create(result)[
            ConnectionErrorAttributePolicy.Category]);
        Assert.Equal($"sni.{code}", ConnectionErrorAttributePolicy.Create(result)[
            ConnectionErrorAttributePolicy.ErrorType]);
    }

    /// <summary>Socket failures classify from the stable OS error code, not exception text.</summary>
    [Fact]
    public void Classify_SocketHostNotFound_UsesOsEvidence()
    {
        // Arrange
        SocketException exception = new((int)SocketError.HostNotFound);

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.socket_connect"));
        IReadOnlyDictionary<string, string> attributes =
            ConnectionErrorAttributePolicy.Create(result);

        // Assert
        Assert.Equal("name_resolution", attributes[ConnectionErrorAttributePolicy.Category]);
        Assert.Equal($"os.{(int)SocketError.HostNotFound}",
            attributes[ConnectionErrorAttributePolicy.ErrorType]);
        Assert.Equal("os", attributes[ConnectionErrorAttributePolicy.Source]);
    }

    /// <summary>A structured inner cause takes precedence over a wrapping SqlException.</summary>
    [Fact]
    public void Classify_InnerSocketCauseOverridesServerWrapper()
    {
        // Arrange
        SocketException cause = new((int)SocketError.HostNotFound);
        SqlException wrapper = SqlException.CreateException(
            new SqlErrorCollection { ServerError(18456) }, "test", Guid.Empty, cause);

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(wrapper, "connection.open"));

        // Assert
        IReadOnlyDictionary<string, string> attributes =
            ConnectionErrorAttributePolicy.Create(result);
        Assert.Equal("name_resolution", attributes[ConnectionErrorAttributePolicy.Category]);
        Assert.Equal("os", attributes[ConnectionErrorAttributePolicy.Source]);
    }

    /// <summary>Conflicting aggregate branches produce unknown evidence.</summary>
    [Fact]
    public void Classify_ConflictingAggregateBranches_ReturnsUnknown()
    {
        // Arrange
        AggregateException exception = new(
            SqlException.CreateException(new SqlErrorCollection { ServerError(18456) }, "test"),
            SqlException.CreateException(new SqlErrorCollection { ServerError(4060) }, "test"));

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.open"));

        // Assert
        IReadOnlyDictionary<string, string> attributes =
            ConnectionErrorAttributePolicy.Create(result);
        Assert.Equal("unknown", attributes[ConnectionErrorAttributePolicy.Category]);
        Assert.Equal("unknown", attributes[ConnectionErrorAttributePolicy.ErrorType]);
        Assert.Equal("unknown", attributes[ConnectionErrorAttributePolicy.Source]);
    }

    /// <summary>Cancellation uses the JDBC-aligned single-l spelling.</summary>
    [Fact]
    public void Classify_Cancellation_UsesCanceledCategory()
    {
        // Arrange
        OperationCanceledException exception = new("private-message");

        // Act
        ConnectionErrorClassification result = ConnectionErrorClassifier.Classify(
            ConnectionErrorEvidence.Capture(exception, "connection.open"));
        IReadOnlyDictionary<string, string> attributes =
            ConnectionErrorAttributePolicy.Create(result);

        // Assert
        Assert.Equal("canceled", attributes[ConnectionErrorAttributePolicy.Category]);
        Assert.Equal("client.canceled", attributes[ConnectionErrorAttributePolicy.ErrorType]);
    }

    /// <summary>The attribute policy rejects arbitrary values and never exports exception data.</summary>
    [Fact]
    public void Create_UnapprovedClassificationValues_EmitsOnlySafeAttributes()
    {
        // Arrange
        ConnectionErrorClassification classification = new(
            ConnectionErrorCategory.Unknown,
            ConnectionErrorSource.Unknown,
            "private-message",
            "private-code",
            "connection.private-message");

        // Act
        IReadOnlyDictionary<string, string> attributes =
            ConnectionErrorAttributePolicy.Create(classification);

        // Assert
        Assert.Equal(
            new[]
            {
                ConnectionErrorAttributePolicy.Category,
                ConnectionErrorAttributePolicy.ErrorType,
                ConnectionErrorAttributePolicy.Phase,
                ConnectionErrorAttributePolicy.Source
            }.OrderBy(key => key),
            attributes.Keys.OrderBy(key => key));
        Assert.DoesNotContain("private", string.Join(";", attributes.Values));
        Assert.DoesNotContain("exception", attributes.Keys);
    }

    /// <summary>Connection failure evidence retains no exception or message reference.</summary>
    [Fact]
    public void Capture_InnerException_ReturnsImmutableScalarEvidence()
    {
        // Arrange
        InvalidOperationException exception = new(
            "private-message", new TimeoutException("private-inner-message"));

        // Act
        ConnectionErrorEvidence evidence =
            ConnectionErrorEvidence.Capture(exception, "connection.open");

        // Assert
        Assert.Equal(ConnectionErrorSource.Client, evidence.Source);
        Assert.Equal(ConnectionErrorCategory.Timeout, evidence.Kind);
        Assert.Equal("open", evidence.Phase);
        Assert.Null(evidence.Code);
    }

    private static ConnectionErrorEvidence CaptureServerError(int number) =>
        ConnectionErrorEvidence.Capture(
            SqlException.CreateException(new SqlErrorCollection { ServerError(number) }, "test"),
            "connection.login");

    private static SqlError ServerError(int number) =>
        new(number, 1, 16, "", "private-message", "", 0) { IsServerError = true };
}
