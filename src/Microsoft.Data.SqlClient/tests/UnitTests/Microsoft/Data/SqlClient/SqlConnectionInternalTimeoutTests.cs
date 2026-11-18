// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies how <see cref="SqlConnectionInternal"/> selects the
/// <see cref="TimeoutTimer"/> that governs the login phase, driven by the
/// <c>UseOverallConnectTimeoutForPoolWait</c> AppContext switch.
///
/// When the switch is enabled the connection must reuse the caller-supplied
/// timer as-is so the remaining budget (already reflecting any time spent
/// waiting for the pool) is honored during login. When disabled it must
/// construct a fresh timer from <c>ConnectTimeout</c>, preserving legacy
/// behavior. Asserting against the extracted
/// <see cref="SqlConnectionInternal.ResolveLoginTimeout"/> helper keeps this
/// branch coverage free of any real network connection.
/// </summary>
[Collection(AppContextSwitchTestCollection.Name)]
public class SqlConnectionInternalTimeoutTests
{
    /// <summary>
    /// Ensures login uses the caller's remaining budget by default and when explicitly enabled.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public void ResolveLoginTimeout_OverallBudgetEnabled_PreservesRemainingBudget(bool? switchEnabled)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = switchEnabled,
        };
        var fakeTime = new FakeTimeProvider();
        TimeoutTimer callerTimeout = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);
        fakeTime.Advance(TimeSpan.FromSeconds(5));

        // Act
        TimeoutTimer resolved = SqlConnectionInternal.ResolveLoginTimeout(
            callerTimeout,
            connectTimeoutSeconds: 30);

        // Assert
        Assert.Same(callerTimeout, resolved);
        Assert.Equal(25_000, resolved.MillisecondsRemainingInt);
    }

    /// <summary>
    /// Ensures the explicit compatibility opt-out starts a full login budget even after
    /// the caller's pool-wait budget has expired.
    /// </summary>
    [Fact]
    public void ResolveLoginTimeout_OverallBudgetDisabled_StartsFreshBudget()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = false,
        };
        TimeoutTimer callerTimeout = TimeoutTimer.StartExpired();

        // Act
        TimeoutTimer resolved = SqlConnectionInternal.ResolveLoginTimeout(
            callerTimeout,
            connectTimeoutSeconds: 30);

        // Assert
        Assert.NotSame(callerTimeout, resolved);
        Assert.Equal(TimeSpan.FromSeconds(30).Ticks, resolved.OriginalTicks);
        Assert.False(resolved.IsExpired);
    }
}
