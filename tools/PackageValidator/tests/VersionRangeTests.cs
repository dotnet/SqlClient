using Xunit;

namespace PackageValidator.Tests;

public class VersionRangeTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]   // bare version is a minimum-inclusive bound
    [InlineData("1.0.0", "1.5.0", true)]
    [InlineData("1.0.0", "0.9.0", false)]
    [InlineData("[1.0.0]", "1.0.0", true)] // exact
    [InlineData("[1.0.0]", "1.0.1", false)]
    [InlineData("[1.0.0,2.0.0)", "1.5.0", true)]
    [InlineData("[1.0.0,2.0.0)", "2.0.0", false)] // upper exclusive
    [InlineData("[1.0.0,2.0.0]", "2.0.0", true)]  // upper inclusive
    [InlineData("(1.0.0,)", "1.0.0", false)]      // lower exclusive
    [InlineData("(1.0.0,)", "1.0.1", true)]
    [InlineData("(,2.0.0]", "1.0.0", true)]
    [InlineData("(,2.0.0]", "2.0.1", false)]
    public void Satisfies_evaluates_ranges(string range, string version, bool expected)
    {
        Assert.Equal(expected, VersionRange.Satisfies(range, version));
    }

    [Fact]
    public void Satisfies_ignores_prerelease_suffix()
    {
        Assert.Equal(true, VersionRange.Satisfies("1.0.0", "1.2.3-preview.1"));
    }

    [Fact]
    public void Satisfies_returns_null_for_empty_range()
    {
        Assert.Null(VersionRange.Satisfies(null, "1.0.0"));
        Assert.Null(VersionRange.Satisfies("", "1.0.0"));
    }

    [Fact]
    public void Satisfies_treats_missing_components_as_zero()
    {
        Assert.Equal(true, VersionRange.Satisfies("1.0", "1.0.0.0"));
        Assert.Equal(true, VersionRange.Satisfies("[1.0]", "1.0.0"));
    }
}
