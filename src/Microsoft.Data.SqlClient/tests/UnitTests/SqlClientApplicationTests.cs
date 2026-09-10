// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests for the <see cref="SqlClientApplication"/> application identifier registry.
/// </summary>
public class SqlClientApplicationTests
{
    /// <summary>
    /// Verifies the enum uses the 16-bit protocol width.
    /// </summary>
    [Fact]
    public void UnderlyingType_Is_UShort()
    {
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(SqlClientApplication)));
    }

    /// <summary>
    /// Verifies the default value reports no application identity.
    /// </summary>
    [Fact]
    public void Default_Is_Unknown()
    {
        Assert.Equal(SqlClientApplication.Unknown, default(SqlClientApplication));
        Assert.Equal(0, (int)SqlClientApplication.Unknown);
    }

    /// <summary>
    /// Verifies the reserved identifiers keep their assigned values, since
    /// changing one would silently re-map an application's telemetry.
    /// </summary>
    [Theory]
    [InlineData(SqlClientApplication.EntityFrameworkCore, 1)]
    [InlineData(SqlClientApplication.SemanticKernel, 2)]
    [InlineData(SqlClientApplication.ManagementStudio, 3)]
    [InlineData(SqlClientApplication.SqlManagementObjects, 4)]
    [InlineData(SqlClientApplication.DataTierApplicationFramework, 5)]
    [InlineData(SqlClientApplication.SqlToolsService, 6)]
    [InlineData(SqlClientApplication.AspNetCoreDistributedSqlServerCache, 7)]
    [InlineData(SqlClientApplication.EntityFramework, 8)]
    [InlineData(SqlClientApplication.AzureFunctionsSqlExtension, 9)]
    [InlineData(SqlClientApplication.OrleansAdoNet, 10)]
    [InlineData(SqlClientApplication.DurableTaskSqlServer, 11)]
    [InlineData(SqlClientApplication.SqlPackage, 12)]
    [InlineData(SqlClientApplication.DataApiBuilder, 13)]
    public void Members_Have_Stable_Values(SqlClientApplication app, int expected)
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
        SqlClientApplication app = (SqlClientApplication)0xC001;

        Assert.False(Enum.IsDefined(typeof(SqlClientApplication), app));

        using SqlConnection connection = new();
        connection.RegisteredApplication = app;

        Assert.Equal(app, connection.RegisteredApplication);
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

        connection.RegisteredApplication = (SqlClientApplication)value;

        Assert.Equal(value, (int)connection.RegisteredApplication);
    }

    /// <summary>
    /// Verifies the connection reports no application identity until one is
    /// assigned, and round-trips the value it is given.
    /// </summary>
    [Fact]
    public void SqlConnection_RegisteredApplication_RoundTrips()
    {
        using SqlConnection connection = new();

        Assert.Equal(SqlClientApplication.Unknown, connection.RegisteredApplication);

        connection.RegisteredApplication = SqlClientApplication.SemanticKernel;

        Assert.Equal(SqlClientApplication.SemanticKernel, connection.RegisteredApplication);
    }

    /// <summary>
    /// Verifies a cloned connection keeps the application identity of the
    /// connection it was cloned from, so cloning does not silently drop the
    /// identity back to <see cref="SqlClientApplication.Unknown"/>.
    /// </summary>
    [Fact]
    public void Clone_Preserves_RegisteredApplication()
    {
        using SqlConnection connection = new();
        connection.RegisteredApplication = SqlClientApplication.SqlPackage;

        using SqlConnection clone = (SqlConnection)((ICloneable)connection).Clone();

        Assert.Equal(SqlClientApplication.SqlPackage, clone.RegisteredApplication);
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
    /// Verifies driver properties reserve 64 bits for feature flags.
    /// </summary>
    [Fact]
    public void DriverProperties_UnderlyingType_Is_ULong()
    {
        Assert.Equal(typeof(ulong), Enum.GetUnderlyingType(typeof(SqlClientDriverProperties)));
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
