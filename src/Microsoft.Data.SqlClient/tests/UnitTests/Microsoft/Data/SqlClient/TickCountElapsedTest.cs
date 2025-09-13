// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests for Environment.TickCount elapsed time calculation with wraparound handling.
/// </summary>
public sealed class TickCountElapsedTest
{
    /// <summary>
    /// Verifies that normal elapsed time calculation works correctly.
    /// </summary>
    [Fact]
    public void CalculateTickCountElapsed_NormalCase_ReturnsCorrectElapsed()
    {
        uint elapsed = ADP.CalculateTickCountElapsed(1000, 1500);
        Assert.Equal(500u, elapsed);
    }

    /// <summary>
    /// Verifies that wraparound from int.MaxValue to int.MinValue is handled correctly.
    /// </summary>
    [Fact]
    public void CalculateTickCountElapsed_MaxWraparound_ReturnsOne()
    {
        uint elapsed = ADP.CalculateTickCountElapsed(int.MaxValue, int.MinValue);
        Assert.Equal(1u, elapsed);
    }

    /// <summary>
    /// Verifies that partial wraparound scenarios work correctly.
    /// </summary>
    [Theory]
    [InlineData(2147483600, -2147483600, 96u)]
    [InlineData(2147483647, -2147483647, 2u)]
    public void CalculateTickCountElapsed_PartialWraparound_ReturnsCorrectElapsed(long start, long end, uint expected)
    {
        uint elapsed = ADP.CalculateTickCountElapsed(start, end);
        Assert.Equal(expected, elapsed);
    }

    /// <summary>
    /// Verifies that zero elapsed time returns zero.
    /// </summary>
    [Fact]
    public void CalculateTickCountElapsed_ZeroElapsed_ReturnsZero()
    {
        uint elapsed = ADP.CalculateTickCountElapsed(1000, 1000);
        Assert.Equal(0u, elapsed);
    }
}
