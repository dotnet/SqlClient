// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

public class SqlClientDriverPropertiesTests
{
    [Theory]
    [InlineData(false, false, false, 0UL)]
    [InlineData(true, false, false, 1UL)]
    [InlineData(false, true, false, 2UL)]
    [InlineData(true, true, false, 3UL)]
    [InlineData(false, false, true, 4UL)]
    [InlineData(true, false, true, 5UL)]
    [InlineData(false, true, true, 6UL)]
    [InlineData(true, true, true, 7UL)]
    public void DriverProperties_Reports_Configured_Switches(
        bool useConnectionPoolV2,
        bool useCompatibilityAsyncBehaviour,
        bool useCompatibilityProcessSni,
        ulong expected)
    {
        Assert.Equal(expected, (ulong)SqlClientDriverPropertiesResolver.Resolve(
            useConnectionPoolV2, useCompatibilityAsyncBehaviour, useCompatibilityProcessSni));
    }

    [Fact]
    public void DriverProperties_UnderlyingType_Is_ULong()
    {
        Assert.Equal(typeof(ulong), Enum.GetUnderlyingType(typeof(SqlClientDriverProperties)));
    }

    [Fact]
    public void DriverProperties_Current_Matches_Switches()
    {
        ulong expected =
            (LocalAppContextSwitches.UseConnectionPoolV2 ? 1UL : 0UL) |
            (LocalAppContextSwitches.ConfiguredUseCompatibilityAsyncBehaviour ? 2UL : 0UL) |
            (LocalAppContextSwitches.UseCompatibilityProcessSni ? 4UL : 0UL);

        Assert.Equal(expected, (ulong)SqlClientDriverPropertiesResolver.Current);
    }
}
