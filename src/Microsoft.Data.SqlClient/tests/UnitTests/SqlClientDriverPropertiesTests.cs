// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

public class SqlClientDriverPropertiesTests
{
    [Theory]
    [InlineData(false, 0UL)]
    [InlineData(true, 1UL)]
    public void DriverProperties_Reports_ConnectionPoolV2(bool useConnectionPoolV2, ulong expected)
    {
        Assert.Equal(expected, (ulong)SqlClientDriverPropertiesResolver.Resolve(useConnectionPoolV2));
    }

    [Fact]
    public void DriverProperties_UnderlyingType_Is_ULong()
    {
        Assert.Equal(typeof(ulong), Enum.GetUnderlyingType(typeof(SqlClientDriverProperties)));
    }
}
