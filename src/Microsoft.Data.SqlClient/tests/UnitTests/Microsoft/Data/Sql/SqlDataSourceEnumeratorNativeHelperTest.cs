// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

/// <summary>
/// Tests parsing and de-duplication of the native SNI server enumeration string in
/// <see cref="SqlDataSourceEnumeratorNativeHelper.ParseServerEnumString"/>, which must match
/// the string equality of the case-insensitive <see cref="DataTable"/> it populates.
/// </summary>
public class SqlDataSourceEnumeratorNativeHelperTest
{
    /// <summary>
    /// Gets server and instance names that a case-insensitive <see cref="DataTable"/> treats as equal to
    /// "server" and "inst": identical, differing in case, with trailing ASCII or ideographic spaces, and full-width.
    /// </summary>
    /// <see cref="EquivalentEntry_IsDropped"/>
    public static TheoryData<string, string> EquivalentNames => new()
    {
        { "server", "inst" },
        { "SERVER", "INST" },
        { "server  ", "inst " },
        { "server" + (char)0x3000, "inst" + (char)0x3000 },
        { ToFullWidth("server"), ToFullWidth("inst") },
    };

    /// <summary>
    /// Verifies that every column of distinct entries is returned, and that an entry without an instance
    /// name stores <see cref="DBNull"/> for the instance.
    /// </summary>
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

    /// <summary>
    /// Verifies that an entry equal to an earlier one under <see cref="DataTable"/> string comparison is dropped.
    /// </summary>
    /// <param name="serverName">A server name equivalent to "server".</param>
    /// <param name="instanceName">An instance name equivalent to "inst".</param>
    [Theory]
    [MemberData(nameof(EquivalentNames))]
    public void EquivalentEntry_IsDropped(string serverName, string instanceName)
    {
        DataTable table = Parse(Entry("server", "inst"), Entry(serverName, instanceName));

        Assert.Equal(1, table.Rows.Count);
    }

    /// <summary>
    /// Verifies that two instances on the same server are both returned.
    /// </summary>
    [Fact]
    public void SameServerWithDifferentInstance_IsKept()
    {
        DataTable table = Parse(Entry("server", "inst1"), Entry("server", "inst2"));

        Assert.Equal(2, table.Rows.Count);
    }

    /// <summary>
    /// Verifies that an entry without an instance name is treated as a duplicate of an earlier entry for the
    /// same server, whatever that entry's instance.
    /// </summary>
    [Fact]
    public void EntryWithoutInstance_MatchesExistingServerWithAnyInstance()
    {
        DataTable table = Parse(Entry("server", "inst"), Entry("server", null));

        Assert.Equal(1, table.Rows.Count);
    }

    /// <summary>
    /// Verifies that an entry with an instance name is not a duplicate of an earlier entry for the same server
    /// that has no instance.
    /// </summary>
    [Fact]
    public void EntryWithInstance_DoesNotMatchExistingServerWithoutInstance()
    {
        DataTable table = Parse(Entry("server", null), Entry("server", "inst"));

        Assert.Equal(2, table.Rows.Count);
    }

    /// <summary>
    /// Joins entries with the native end-of-instance delimiter and parses them.
    /// </summary>
    /// <param name="entries">Entries formatted by <see cref="Entry"/>.</param>
    /// <returns>The parsed data sources table.</returns>
    private static DataTable Parse(params string[] entries) =>
        SqlDataSourceEnumeratorNativeHelper.ParseServerEnumString(
            string.Join(SqlDataSourceEnumeratorUtil.EndOfServerInstanceDelimiter_Native.ToString(), entries));

    /// <summary>
    /// Formats one entry as native SNI returns it: "serverName[\instanceName];Clustered:[Yes|No];Version:...".
    /// </summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="instanceName">The instance name, or <see langword="null"/> for a default instance.</param>
    /// <param name="clustered">The clustered flag.</param>
    /// <param name="version">The server version.</param>
    /// <returns>The formatted entry.</returns>
    private static string Entry(string serverName, string? instanceName, string clustered = "No", string version = "15.0.2000.5") =>
        (instanceName is null ? serverName : serverName + SqlDataSourceEnumeratorUtil.ServerNamesAndInstanceDelimiter + instanceName) +
        SqlDataSourceEnumeratorUtil.InstanceKeysDelimiter + SqlDataSourceEnumeratorUtil.Clustered + clustered +
        SqlDataSourceEnumeratorUtil.InstanceKeysDelimiter + SqlDataSourceEnumeratorUtil.Version + version;

    /// <summary>
    /// Converts printable ASCII characters to their full-width forms.
    /// </summary>
    /// <param name="value">A string of printable ASCII characters.</param>
    /// <returns>The full-width string.</returns>
    private static string ToFullWidth(string value) =>
        new(value.Select(c => (char)(c + 0xFEE0)).ToArray());
}
