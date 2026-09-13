// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

/// <summary>
/// Tests for parsing and de-duplication in <see cref="SqlDataSourceEnumeratorNativeHelper.ParseServerEnumString"/>.
/// </summary>
public class SqlDataSourceEnumeratorNativeHelperTest
{
    [Fact]
    public void DistinctEntries_AreAllReturned()
    {
        DataTable table = Parse(Entry("server1", "inst1", "Yes", "16.0.1000.6"), Entry("server2", null));

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("server1", table.Rows[0][SqlDataSourceEnumeratorUtil.ServerNameCol]);
        Assert.Equal("inst1", table.Rows[0][SqlDataSourceEnumeratorUtil.InstanceNameCol]);
        Assert.Equal("Yes", table.Rows[0][SqlDataSourceEnumeratorUtil.IsClusteredCol]);
        Assert.Equal("16.0.1000.6", table.Rows[0][SqlDataSourceEnumeratorUtil.VersionNameCol]);
        Assert.Equal("server2", table.Rows[1][SqlDataSourceEnumeratorUtil.ServerNameCol]);
        Assert.Equal(DBNull.Value, table.Rows[1][SqlDataSourceEnumeratorUtil.InstanceNameCol]);
    }

    [Theory]
    [InlineData("server", "inst")]
    [InlineData("SERVER", "INST")]
    [InlineData("server  ", "inst ")]
    public void EquivalentEntry_IsDropped(string serverName, string instanceName)
    {
        DataTable table = Parse(Entry("server", "inst"), Entry(serverName, instanceName));

        Assert.Equal(1, table.Rows.Count);
    }

    [Fact]
    public void SameServerWithDifferentInstance_IsKept()
    {
        DataTable table = Parse(Entry("server", "inst1"), Entry("server", "inst2"));

        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void EntryWithoutInstance_MatchesExistingServerWithAnyInstance()
    {
        DataTable table = Parse(Entry("server", "inst"), Entry("server", null));

        Assert.Equal(1, table.Rows.Count);
    }

    [Fact]
    public void EntryWithInstance_DoesNotMatchExistingServerWithoutInstance()
    {
        DataTable table = Parse(Entry("server", null), Entry("server", "inst"));

        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void NamesContainingQuotes_AreParsedAndDeduplicated()
    {
        DataTable table = Parse(Entry("o'brien", "inst'1"), Entry("o'brien", "inst'1"));

        Assert.Equal(1, table.Rows.Count);
        Assert.Equal("o'brien", table.Rows[0][SqlDataSourceEnumeratorUtil.ServerNameCol]);
    }

    private static DataTable Parse(params string[] entries) =>
        SqlDataSourceEnumeratorNativeHelper.ParseServerEnumString(
            string.Join(SqlDataSourceEnumeratorUtil.EndOfServerInstanceDelimiter_Native.ToString(), entries));

    // Native SNI format: "serverName[\instanceName];Clustered:[Yes|No];Version:..."
    private static string Entry(string serverName, string? instanceName, string clustered = "No", string version = "15.0.2000.5") =>
        (instanceName is null ? serverName : serverName + SqlDataSourceEnumeratorUtil.ServerNamesAndInstanceDelimiter + instanceName) +
        SqlDataSourceEnumeratorUtil.InstanceKeysDelimiter + SqlDataSourceEnumeratorUtil.Clustered + clustered +
        SqlDataSourceEnumeratorUtil.InstanceKeysDelimiter + SqlDataSourceEnumeratorUtil.Version + version;
}
