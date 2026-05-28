// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Tests.Common;
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
public class SqlConnectionInternalTimeoutTests
{
    /// <summary>
    /// Verifies the branch selection in
    /// <see cref="SqlConnectionInternal.ResolveLoginTimeout"/>:
    /// <list type="bullet">
    ///   <item>switch ON  → the caller's <see cref="TimeoutTimer"/> instance
    ///   flows through unchanged (asserted by reference identity), so any
    ///   time already consumed counts against the overall ConnectTimeout;</item>
    ///   <item>switch OFF → a fresh timer is started from
    ///   <c>ConnectTimeout</c> (asserted by inspecting
    ///   <see cref="TimeoutTimer.OriginalTicks"/>), preserving legacy
    ///   behavior where login always gets the full configured budget.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveLoginTimeout_HonorsSwitch(bool switchEnabled)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = switchEnabled,
        };
        const int ConnectTimeoutSeconds = 30;
        TimeoutTimer callerTimeout = TimeoutTimer.StartNew(TimeSpan.FromSeconds(ConnectTimeoutSeconds));

        // Act
        TimeoutTimer resolved = SqlConnectionInternal.ResolveLoginTimeout(
            callerTimeout,
            ConnectTimeoutSeconds);

        // Assert
        if (switchEnabled)
        {
            // Switch on: caller's timer must flow through unchanged so any
            // time already consumed counts against the overall budget.
            Assert.Same(callerTimeout, resolved);
        }
        else
        {
            // Switch off (legacy): a fresh timer must be started from
            // ConnectTimeout, independent of the caller's timer.
            Assert.NotSame(callerTimeout, resolved);
            Assert.Equal(TimeSpan.FromSeconds(ConnectTimeoutSeconds).Ticks, resolved.OriginalTicks);
        }
    }
}
