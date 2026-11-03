// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies execution timer release when no timer is running.
/// </summary>
public sealed class SqlStatisticsTest
{
    /// <summary>
    /// Releasing a null start timestamp must leave accumulated execution time unchanged.
    /// </summary>
    [Fact]
    public void ReleaseAndUpdateExecutionTimer_NullStart_DoesNothing()
    {
        var statistics = new SqlStatistics
        {
            _startExecutionTimestamp = null,
            _executionTime = 42
        };

        statistics.ReleaseAndUpdateExecutionTimer();

        Assert.Null(statistics._startExecutionTimestamp);
        Assert.Equal(42L, statistics._executionTime);
    }
}
