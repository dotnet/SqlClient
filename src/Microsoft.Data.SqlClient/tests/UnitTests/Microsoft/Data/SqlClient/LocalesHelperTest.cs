// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests which validate the behaviour of LocalesHelper.
/// </summary>
public class LocalesHelperTest
{
    /// <summary>
    /// Verifies that a sort ID which would be out of the array's range is provided,
    /// the method returns false rather than throwing an IndexOutOfRangeException.
    /// </summary>
    /// <param name="sortId">The sort ID.</param>
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public void OutOfRangeSortId_ReturnsFalse(int sortId) =>
        AssertFailingLookup(lcid: 0, sortId);

    /// <summary>
    /// Verifies that if an invalid sort ID is provided, the method returns false.
    /// </summary>
    [Fact]
    public void InvalidSortId_ReturnsFalse() =>
        AssertFailingLookup(lcid: 0, sortId: 37);

    /// <summary>
    /// Verifies that if a valid sort ID which is known to map to code page 437 is
    /// provided, a code page is returned.
    /// </summary>
    [Fact]
    public void ValidSortId_ReturnsCodePage() =>
        AssertSuccessfulLookup(lcid: 0, sortId: 30, expectedCodePage: 437);

    /// <summary>
    /// Verifies that if both a valid sort ID and a valid LCID are provided, the
    /// code page associated with the sort ID takes priority.
    /// </summary>
    [Fact]
    public void ValidSortIdCodePage_PrioritisedOverLcidCodePage()
    {
        bool sortIdCodePageRetrieved = LocalesHelper.TryGetCodePage(lcid: 0, sortId: 30, out int sortIdCodePage);
        bool lcidCodePageRetrieved = LocalesHelper.TryGetCodePage(lcid: 0x0409, sortId: 0, out int lcidCodePage);
        bool lcidAndSortIdCodePageRetrieved = LocalesHelper.TryGetCodePage(lcid: 0x0409, sortId: 30, out int sortIdAndLcidCodePage);

        Assert.True(sortIdCodePageRetrieved);
        Assert.True(lcidCodePageRetrieved);
        Assert.True(lcidAndSortIdCodePageRetrieved);

        Assert.Equal(437, sortIdCodePage);
        Assert.Equal(1252, lcidCodePage);

        Assert.Equal(437, sortIdAndLcidCodePage);
    }

    /// <summary>
    /// Verifies that if an invalid LCID is provided, the method returns false.
    /// </summary>
    [Fact]
    public void MissingLcid_ReturnsFalse() =>
        AssertFailingLookup(lcid: 0xFFFF, sortId: 0);

    /// <summary>
    /// Verifies that if a valid LCID which is known to map to code page 1252 is provided,
    /// this code page is provided.
    /// </summary>
    /// <param name="lcid">The LCID which maps to code page 1252</param>
    [Theory]
    // en_US
    [InlineData(0x0409)]
    // en_US, but with other bits in the collation identifier set.
    [InlineData(0x0001_0409)]
    public void ValidLcid_ReturnsCodePage(int lcid) =>
        AssertSuccessfulLookup(lcid, sortId: 0, expectedCodePage: 1252);

    /// <summary>
    /// Verifies that if an invalid LCID which overlaps with a code page ID is provided,
    /// the method returns false.
    /// </summary>
    [Fact]
    public void InvalidLcidOverlappingCodePageId_ReturnsFalse() =>
        AssertFailingLookup(lcid: 1200, sortId: 0);

    private static void AssertFailingLookup(int lcid, int sortId)
    {
        bool codePageRetrieved = LocalesHelper.TryGetCodePage(lcid, sortId, out _);

        Assert.False(codePageRetrieved);
    }

    private static void AssertSuccessfulLookup(int lcid, int sortId, int expectedCodePage)
    {
        bool codePageRetrieved = LocalesHelper.TryGetCodePage(lcid, sortId, out int codePage);

        Assert.True(codePageRetrieved);
        Assert.Equal(expectedCodePage, codePage);
    }
}
