using Xunit;

namespace PackageValidator.Tests;

/// <summary>
/// Tests for <see cref="VersionRange"/>, the SemVer 2.0-aware evaluator that decides whether a
/// concrete version falls inside a NuGet dependency version range.
/// </summary>
public class VersionRangeTests
{
    /// <summary>
    /// Verifies the bracket/interval grammar for release-only versions: bare minimum bounds, exact
    /// pins, and inclusive/exclusive interval endpoints.
    /// </summary>
    /// <param name="range">The declared version range.</param>
    /// <param name="version">The concrete version under test.</param>
    /// <param name="expected">Whether <paramref name="version"/> is expected to satisfy <paramref name="range"/>.</param>
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
        // Each row supplies its own inputs (arrange) and expected outcome (assert); the call is the act.
        Assert.Equal(expected, VersionRange.Satisfies(range, version));
    }

    /// <summary>
    /// Verifies SemVer precedence between a release and its prerelease: a prerelease ranks below the
    /// matching release.
    /// </summary>
    [Fact]
    public void Satisfies_release_outranks_prerelease_of_same_version()
    {
        // A prerelease is lower precedence than its release, so it falls below a min-inclusive bound.
        Assert.Equal(false, VersionRange.Satisfies("1.0.0", "1.0.0-alpha"));

        // A release satisfies a prerelease lower bound (release > prerelease of the same version).
        Assert.Equal(true, VersionRange.Satisfies("1.0.0-alpha", "1.0.0"));

        // A higher release still satisfies even when it carries a prerelease tag, because its release
        // components (1.2.3) already exceed the bound.
        Assert.Equal(true, VersionRange.Satisfies("1.0.0", "1.2.3-preview.1"));
    }

    /// <summary>
    /// Verifies prerelease-identifier precedence: numeric identifiers compare numerically and rank
    /// below alphanumeric ones, and a larger identifier set outranks a smaller prefix.
    /// </summary>
    /// <param name="range">The declared version range.</param>
    /// <param name="version">The concrete version under test.</param>
    /// <param name="expected">Whether <paramref name="version"/> is expected to satisfy <paramref name="range"/>.</param>
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

    /// <summary>
    /// Verifies that build metadata (the <c>+</c> suffix) is ignored, matching SemVer/NuGet, so it
    /// never changes whether a version satisfies a range.
    /// </summary>
    [Fact]
    public void Satisfies_ignores_build_metadata()
    {
        // "+abc123" / "+build.5" must not affect precedence, so the version still matches an exact
        // pin and a minimum bound respectively.
        Assert.Equal(true, VersionRange.Satisfies("[1.0.0]", "1.0.0+abc123"));
        Assert.Equal(true, VersionRange.Satisfies("1.0.0", "1.0.0+build.5"));
    }

    /// <summary>
    /// Verifies that malformed or unparseable ranges/versions return <see langword="null"/> rather
    /// than throwing, so callers can safely skip them.
    /// </summary>
    /// <param name="range">The declared version range.</param>
    /// <param name="version">The concrete version under test.</param>
    [Theory]
    [InlineData("[", "1.0.0")]            // opening bracket with no closing delimiter
    [InlineData("(", "1.0.0")]            // opening paren with no closing delimiter
    [InlineData("[1.0.0", "1.0.0")]       // missing closing delimiter
    [InlineData("(1.0.0", "1.0.0")]       // missing closing delimiter
    [InlineData("[1..0]", "1.0.0")]       // empty release component in the range
    [InlineData("1.0.0", "1..0")]         // empty release component in the version
    [InlineData("1.0.0", "1.0.0-")]       // trailing hyphen: empty prerelease
    [InlineData("1.0.0", "1.0.0-alpha..1")] // empty prerelease identifier
    public void Satisfies_returns_null_for_malformed_input(string range, string version)
    {
        Assert.Null(VersionRange.Satisfies(range, version));
    }

    /// <summary>
    /// Verifies that an absent range is treated as "no constraint" by returning <see langword="null"/>
    /// (the caller must not treat this as a failure).
    /// </summary>
    [Fact]
    public void Satisfies_returns_null_for_empty_range()
    {
        Assert.Null(VersionRange.Satisfies(null, "1.0.0"));
        Assert.Null(VersionRange.Satisfies("", "1.0.0"));
    }

    /// <summary>
    /// Verifies that versions with different component counts compare correctly, with missing
    /// trailing components treated as zero (for example <c>1.0</c> equals <c>1.0.0.0</c>).
    /// </summary>
    [Fact]
    public void Satisfies_treats_missing_components_as_zero()
    {
        Assert.Equal(true, VersionRange.Satisfies("1.0", "1.0.0.0"));
        Assert.Equal(true, VersionRange.Satisfies("[1.0]", "1.0.0"));
    }
}
