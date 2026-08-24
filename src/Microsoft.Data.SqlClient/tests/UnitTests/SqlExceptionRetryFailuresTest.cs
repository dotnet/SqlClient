// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.UnitTests.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Verifies the public connection-open retry ledger on <see cref="SqlException"/>.
    /// </summary>
    public sealed class SqlExceptionRetryFailuresTest
    {
        /// <summary>
        /// Verifies exceptions that did not pass through a connection-open retry expose an empty
        /// immutable ledger.
        /// </summary>
        [Fact]
        public void ConnectionOpenRetryFailures_NoRetries_IsEmpty()
        {
            SqlException exception = SqlExceptionHelper.CreateSqlException("terminal failure");

            Assert.Empty(exception.ConnectionOpenRetryFailures);
        }

        /// <summary>
        /// Verifies enriching a terminal failure retains its existing SQL errors, native inner
        /// exception, connection metadata, and data entries.
        /// </summary>
        [Fact]
        public void SetConnectionOpenRetryFailures_PreservesTerminalException()
        {
            var nativeTimeout = new Win32Exception(258);
            var errors = new SqlErrorCollection();
            errors.Add(new SqlError(-2, 0, 11, "server", "terminal timeout", "", 0, 258));
            SqlException terminal = SqlException.CreateException(
                errors,
                serverVersion: "1.0",
                conId: Guid.NewGuid(),
                innerException: nativeTimeout);
            terminal.Data["marker"] = "preserved";
            SqlException first = SqlExceptionHelper.CreateSqlException("first {transient} failure");
            SqlException second = SqlExceptionHelper.CreateSqlException("second transient failure");

            terminal.SetConnectionOpenRetryFailures(
                new[] { first, second });

            Assert.Same(errors, terminal.Errors);
            Assert.Same(nativeTimeout, terminal.InnerException);
            Assert.Equal("preserved", terminal.Data["marker"]);
            Assert.Collection(
                terminal.ConnectionOpenRetryFailures,
                failure => Assert.Same(first, failure),
                failure => Assert.Same(second, failure));
            Assert.Contains("first {transient} failure", terminal.ToString());
            Assert.Contains("second transient failure", terminal.ToString());
        }

        /// <summary>
        /// Verifies the blocking-period cache does not replay one Open call's retry ledger to later
        /// callers that fast-fail against the cached connection error.
        /// </summary>
        [Fact]
        public void BlockingPeriodCache_StripsConnectionOpenRetryFailures()
        {
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            SqlException terminal =
                SqlExceptionHelper.CreateSqlException("terminal failure");
            terminal.SetConnectionOpenRetryFailures(
                new[] { SqlExceptionHelper.CreateSqlException("transient failure") });

            state.Enter(terminal);

            SqlException replayed = Assert.Throws<SqlException>(
                () => state.ThrowIfActive());
            Assert.Empty(replayed.ConnectionOpenRetryFailures);
        }
    }
}
