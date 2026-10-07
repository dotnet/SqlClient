// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Resources;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Ensures pool validation messages are resources without changing exception metadata.
/// </summary>
public class PoolValidationResourceTest
{
    /// <summary>
    /// Invalid slot capacities preserve the parameter name and neutral English resource text.
    /// </summary>
    [Theory]
    [InlineData(0u, "SQL_PoolCapacityZero", "Capacity must be greater than zero.")]
    [InlineData(uint.MaxValue, "SQL_PoolCapacityTooLarge", "Capacity must be less than or equal to Int32.MaxValue.")]
    public void Slots_InvalidCapacity_UsesResource(uint capacity, string key, string expected)
    {
        ResourceManager resources = new(
            "Microsoft.Data.SqlClient.Resources.Strings", typeof(SqlConnection).Assembly);
        Assert.Equal(expected, resources.GetString(key, CultureInfo.InvariantCulture));

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionPoolSlots(capacity));
        Assert.Equal("fixedCapacity", exception.ParamName);
        string? message = resources.GetString(key);
        Assert.NotNull(message);
        Assert.Contains(message, exception.Message);
    }

    /// <summary>
    /// Negative idle timeouts preserve their actual value and use the localized message.
    /// </summary>
    [Fact]
    public void Options_NegativeIdleTimeout_UsesResource()
    {
        ResourceManager resources = new(
            "Microsoft.Data.SqlClient.Resources.Strings", typeof(SqlConnection).Assembly);
        Assert.Equal("Idle timeout cannot be negative.",
            resources.GetString("SQL_PoolIdleTimeoutNegative", CultureInfo.InvariantCulture));

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DbConnectionPoolGroupOptions(false, 0, 100, 15000, 0, false, -1));
        Assert.Equal("idleTimeout", exception.ParamName);
        Assert.Equal(-1, exception.ActualValue);
        string? message = resources.GetString("SQL_PoolIdleTimeoutNegative");
        Assert.NotNull(message);
        Assert.Contains(message, exception.Message);
    }
}
