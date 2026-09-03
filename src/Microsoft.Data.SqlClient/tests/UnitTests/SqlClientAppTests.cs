// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests for the <see cref="SqlClientApp"/> application identifier registry.
/// </summary>
public class SqlClientAppTests
{
    /// <summary>
    /// Verifies the enum is CLS-compliant, so it is usable from every .NET
    /// language.
    /// </summary>
    [Fact]
    public void UnderlyingType_Is_Int()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(SqlClientApp)));
    }

    /// <summary>
    /// Verifies the default value reports no application identity.
    /// </summary>
    [Fact]
    public void Default_Is_Unknown()
    {
        Assert.Equal(SqlClientApp.Unknown, default(SqlClientApp));
        Assert.Equal(0, (int)SqlClientApp.Unknown);
    }

    /// <summary>
    /// Verifies the reserved identifiers keep their assigned values, since
    /// changing one would silently re-map an application's telemetry.
    /// </summary>
    [Theory]
    [InlineData(SqlClientApp.EntityFramework, 0x0001)]
    [InlineData(SqlClientApp.SemanticKernel, 0x0002)]
    [InlineData(SqlClientApp.ManagementStudio, 0x0003)]
    [InlineData(SqlClientApp.SqlManagementObjects, 0x0004)]
    [InlineData(SqlClientApp.DataTierApplicationFramework, 0x0005)]
    [InlineData(SqlClientApp.SqlToolsService, 0x0006)]
    [InlineData(SqlClientApp.AspNetCoreDistributedSqlServerCache, 0x0007)]
    [InlineData(SqlClientApp.EntityFramework6, 0x0008)]
    [InlineData(SqlClientApp.AzureFunctionsSqlExtension, 0x0009)]
    [InlineData(SqlClientApp.OrleansAdoNet, 0x000A)]
    [InlineData(SqlClientApp.DurableTaskSqlServer, 0x000B)]
    [InlineData(SqlClientApp.SqlPackage, 0x000C)]
    public void Members_Have_Stable_Values(SqlClientApp app, int expected)
    {
        Assert.Equal(expected, (int)app);
    }

    /// <summary>
    /// Verifies an unregistered identifier can be reported by casting, which
    /// keeps the API forward compatible with identifiers added later.
    /// </summary>
    [Fact]
    public void Unregistered_Identifier_Is_Accepted()
    {
        SqlClientApp app = (SqlClientApp)0xC001;

        Assert.False(Enum.IsDefined(typeof(SqlClientApp), app));

        using SqlConnection connection = new();
        connection.SqlClientAppId = app;

        Assert.Equal(app, connection.SqlClientAppId);
    }

    /// <summary>
    /// Verifies the boundaries of the 16-bit identifier space are accepted,
    /// since the payload reports the identifier in exactly 16 bits.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(ushort.MaxValue)]
    public void Identifier_In_Range_Is_Accepted(int value)
    {
        using SqlConnection connection = new();

        connection.SqlClientAppId = (SqlClientApp)value;

        Assert.Equal(value, (int)connection.SqlClientAppId);
    }

    /// <summary>
    /// Verifies an identifier outside the 16-bit space is rejected rather than
    /// silently truncated when the payload is built.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(ushort.MaxValue + 1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Identifier_Out_Of_Range_Throws(int value)
    {
        using SqlConnection connection = new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => connection.SqlClientAppId = (SqlClientApp)value);

        // The rejected value is not retained.
        Assert.Equal(SqlClientApp.Unknown, connection.SqlClientAppId);
    }

    /// <summary>
    /// Verifies the connection reports no application identity until one is
    /// assigned, and round-trips the value it is given.
    /// </summary>
    [Fact]
    public void SqlConnection_SqlClientAppId_RoundTrips()
    {
        using SqlConnection connection = new();

        Assert.Equal(SqlClientApp.Unknown, connection.SqlClientAppId);

        connection.SqlClientAppId = SqlClientApp.SemanticKernel;

        Assert.Equal(SqlClientApp.SemanticKernel, connection.SqlClientAppId);
    }

    /// <summary>
    /// Verifies a cloned connection keeps the application identity of the
    /// connection it was cloned from, so cloning does not silently drop the
    /// identity back to <see cref="SqlClientApp.Unknown"/>.
    /// </summary>
    [Fact]
    public void Clone_Preserves_SqlClientAppId()
    {
        using SqlConnection connection = new();
        connection.SqlClientAppId = SqlClientApp.SqlPackage;

        using SqlConnection clone = (SqlConnection)((ICloneable)connection).Clone();

        Assert.Equal(SqlClientApp.SqlPackage, clone.SqlClientAppId);
    }

    /// <summary>
    /// Verifies the driver properties part reports the connection pool V2 flag
    /// when, and only when, that implementation is enabled.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DriverProperties_Reports_ConnectionPoolV2(bool useConnectionPoolV2)
    {
        SqlClientDriverProperties expected = useConnectionPoolV2
            ? SqlClientDriverProperties.ConnectionPoolV2
            : SqlClientDriverProperties.None;

        Assert.Equal(expected, SqlClientDriverPropertiesResolver.Resolve(useConnectionPoolV2));
    }

    /// <summary>
    /// Verifies the flags reported for this process agree with the switch they
    /// are derived from, so <see cref="SqlClientDriverPropertiesResolver.Current"/>
    /// cannot drift from the mapping it delegates to.
    /// </summary>
    [Fact]
    public void DriverProperties_Current_Matches_Switch()
    {
        SqlClientDriverProperties expected =
            SqlClientDriverPropertiesResolver.Resolve(LocalAppContextSwitches.UseConnectionPoolV2);

        Assert.Equal(expected, SqlClientDriverPropertiesResolver.Current);
    }
}
