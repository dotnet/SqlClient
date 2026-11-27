// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies the statement error pipeline: captured evidence, the JDBC-derived classification
/// table, cause-walk precedence, ambiguity handling and the exported attribute allow-list.
/// </summary>
public class StatementErrorClassifierTests
{
    /// <summary>Confirmed SQL Server numbers map through the statement registry.</summary>
    [Theory]
    [InlineData(208, "query_syntax_semantics")]
    [InlineData(102, "query_syntax_semantics")]
    [InlineData(2627, "constraint_violation")]
    [InlineData(547, "constraint_violation")]
    [InlineData(229, "authorization")]
    [InlineData(1205, "concurrency_conflict")]
    [InlineData(40613, "server_availability")]
    [InlineData(40501, "resource_throttling")]
    [InlineData(49918, "resource_throttling")]
    [InlineData(50000, "database_error")]
    public void Classify_ServerError_UsesRegistry(int number, string category)
    {
        // Arrange
        SqlException exception = Server(number);

        // Act
        StatementErrorClassification result = Classify(exception);

        // Assert
        Assert.Equal(category, StatementErrorAttributePolicy.CategoryName(result.Category));
        Assert.Equal($"sqlserver.{number}", result.ErrorType);
        Assert.Equal($"sqlserver:{number}", result.ErrorCode);
        Assert.Equal("execute", result.Phase);
    }

    /// <summary>A client-created error with a server-looking number is not server evidence.</summary>
    [Fact]
    public void Classify_ClientNumberCollision_RemainsUnknown()
    {
        // Arrange
        SqlError error = new(208, 1, 16, "server", "Invalid object name", "", 0);
        SqlException exception = SqlException.CreateException(new SqlErrorCollection { error }, "");

        // Act
        StatementErrorClassification result = Classify(exception);

        // Assert
        Assert.Equal(StatementErrorCategory.Unknown, result.Category);
        Assert.Equal("unknown", result.ErrorType);
    }

    /// <summary>Driver-set timeout and cancel flags win over the error number.</summary>
    [Theory]
    [InlineData(true, false, "timeout")]
    [InlineData(false, true, "canceled")]
    public void Classify_DriverFlags_UsesFlagCategory(bool timeout, bool cancel, string category)
    {
        // Arrange
        SqlError error = new(-2, 0, 11, "server", "private", "", 0)
        {
            IsTimeout = timeout,
            IsCancellation = cancel
        };
        SqlException exception = SqlException.CreateException(new SqlErrorCollection { error }, "");

        // Act
        StatementErrorClassification result = Classify(exception);

        // Assert
        Assert.Equal(category, StatementErrorAttributePolicy.CategoryName(result.Category));
        Assert.Equal("client." + category, result.ErrorType);
        Assert.Equal(ConnectionErrorSource.Client, result.Source);
    }

    /// <summary>Observed client exception kinds classify without any message inspection.</summary>
    [Fact]
    public void Classify_ClientExceptionKinds_UseObservedType()
    {
        // Arrange
        Exception[] exceptions =
        [
            new OperationCanceledException(),
            new TimeoutException(),
            new ArgumentException("private"),
            new InvalidCastException("private"),
            new ObjectDisposedException("private")
        ];

        // Act
        string[] categories = exceptions
            .Select(e => StatementErrorAttributePolicy.CategoryName(Classify(e).Category))
            .ToArray();

        // Assert
        Assert.Equal(
            ["canceled", "timeout", "parameter_validation", "data_conversion",
                "connection_lifecycle"],
            categories);
    }

    /// <summary>The innermost structured cause wins over a wrapper exception.</summary>
    [Fact]
    public void Classify_WrappedServerError_UsesInnerCause()
    {
        // Arrange
        Exception wrapper = new InvalidOperationException("private", Server(2627));
        AggregateException aggregate = new(wrapper);

        // Act
        StatementErrorClassification result = Classify(aggregate);

        // Assert
        Assert.Equal(StatementErrorCategory.ConstraintViolation, result.Category);
    }

    /// <summary>
    /// Conflicting causes at the same depth are ambiguous, but sibling errors within one
    /// SqlException describe the same failure and use the primary error.
    /// </summary>
    [Fact]
    public void Classify_ConflictingCauses_IsUnknownButSiblingsUsePrimary()
    {
        // Arrange
        AggregateException conflict = new(Server(208), Server(1205));
        SqlErrorCollection siblings = new()
        {
            new SqlError(1205, 1, 13, "s", "m", "", 0) { IsServerError = true },
            new SqlError(3621, 1, 16, "s", "m", "", 0) { IsServerError = true }
        };

        // Act
        StatementErrorClassification ambiguous = Classify(conflict);
        StatementErrorClassification primary = Classify(
            SqlException.CreateException(siblings, ""));

        // Assert
        Assert.Equal(StatementErrorCategory.Unknown, ambiguous.Category);
        Assert.Equal("unknown", ambiguous.ErrorType);
        Assert.Equal(StatementErrorCategory.ConcurrencyConflict, primary.Category);
    }

    /// <summary>Unrecognized phases and unknown exceptions stay within the allow-list.</summary>
    [Fact]
    public void Classify_UnknownExceptionAndPhase_IsUnknown()
    {
        // Arrange
        StatementErrorEvidence evidence = StatementErrorEvidence.Capture(
            new Exception("private"), "connection.open");

        // Act
        StatementErrorClassification result = StatementErrorClassifier.Classify(evidence);

        // Assert
        Assert.Equal(StatementErrorCategory.Unknown, result.Category);
        Assert.Equal("unknown", result.Phase);
    }

    /// <summary>The policy emits only allow-listed keys and never message or SQLSTATE data.</summary>
    [Fact]
    public void Create_ServerError_EmitsOnlyAllowListedAttributes()
    {
        // Arrange
        StatementErrorClassification classification = Classify(Server(208));

        // Act
        IReadOnlyDictionary<string, string> attributes =
            StatementErrorAttributePolicy.Create(classification);

        // Assert
        Assert.Equal(
            new[]
            {
                "error.type", "sqlclient.error.category", "sqlclient.error.code",
                "sqlclient.error.phase", "sqlclient.error.source"
            },
            attributes.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.DoesNotContain(attributes.Values, value => value.Contains("private"));
    }

    private static StatementErrorClassification Classify(Exception exception) =>
        StatementErrorClassifier.Classify(
            StatementErrorEvidence.Capture(exception, "statement.execute"));

    private static SqlException Server(int number)
    {
        SqlError error = new(number, 1, 16, "private-server", "private-message",
            "private-procedure", 1) { IsServerError = true };
        return SqlException.CreateException(new SqlErrorCollection { error }, "");
    }
}
