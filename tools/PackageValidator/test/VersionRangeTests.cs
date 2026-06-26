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
    public void Satisfies_release_outranks_prerelease_of_same_version()
    {
        // A prerelease is lower precedence than its release, so it falls below a min-inclusive bound.
        Assert.Equal(false, VersionRange.Satisfies("1.0.0", "1.0.0-alpha"));
        // A release satisfies a prerelease lower bound.
        Assert.Equal(true, VersionRange.Satisfies("1.0.0-alpha", "1.0.0"));
        // A higher release still satisfies even when it carries a prerelease tag.
        Assert.Equal(true, VersionRange.Satisfies("1.0.0", "1.2.3-preview.1"));
    }

    [Theory]
    [InlineData("[1.0.0-alpha,1.0.0]", "1.0.0-beta", true)]   // alpha < beta < release
    [InlineData("[1.0.0-alpha.1,)", "1.0.0-alpha.2", true)]   // numeric identifier ordering
    [InlineData("[1.0.0-alpha.2,)", "1.0.0-alpha.1", false)]
    [InlineData("[1.0.0-alpha,)", "1.0.0-alpha.1", true)]     // larger identifier set ranks higher
    [InlineData("[1.0.0-alpha,)", "1.0.0-1", false)]          // numeric ranks below alphanumeric
    public void Satisfies_orders_prerelease_identifiers(string range, string version, bool expected)
    {
        Assert.Equal(expected, VersionRange.Satisfies(range, version));
    }

    [Fact]
    public void Satisfies_ignores_build_metadata()
    {
        Assert.Equal(true, VersionRange.Satisfies("[1.0.0]", "1.0.0+abc123"));
        Assert.Equal(true, VersionRange.Satisfies("1.0.0", "1.0.0+build.5"));
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
