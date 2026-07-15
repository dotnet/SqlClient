using System.Globalization;

namespace PackageValidator;

/// <summary>
/// A minimal evaluator for NuGet dependency version ranges, sufficient to check whether a concrete
/// package version falls inside a declared range. Comparison follows SemVer 2.0 precedence rules,
/// including prerelease ordering; build metadata is ignored (it does not affect precedence).
/// </summary>
internal static class VersionRange
{
    /// <summary>
    /// Determines whether a concrete version satisfies a NuGet version range.
    /// </summary>
    /// <param name="range">The declared version range (for example <c>"1.2.3"</c> or <c>"[1.0.0,2.0.0)"</c>).</param>
    /// <param name="version">The concrete version to test.</param>
    /// <returns>
    /// <see langword="true"/> or <see langword="false"/> when the relationship can be evaluated, and
    /// <see langword="null"/> when the range or version cannot be parsed (caller should not treat
    /// this as a failure).
    /// </returns>
    public static bool? Satisfies(string? range, string version)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return null;
        }

        SemanticVersion? target = Parse(version);
        if (target is null)
        {
            return null;
        }

        range = range.Trim();

        // A bare version (no brackets) is a minimum-inclusive bound in NuGet semantics.
        if (range[0] != '[' && range[0] != '(')
        {
            SemanticVersion? min = Parse(range);
            return min is null ? null : Compare(target, min) >= 0;
        }

        // A bracketed range must have a matching closing delimiter; otherwise it is unparseable.
        char last = range[^1];
        if (range.Length < 2 || (last != ']' && last != ')'))
        {
            return null;
        }

        bool minInclusive = range[0] == '[';
        bool maxInclusive = last == ']';
        string inner = range[1..^1];

        int comma = inner.IndexOf(',');
        if (comma < 0)
        {
            // "[1.0.0]" denotes an exact version.
            SemanticVersion? exact = Parse(inner);
            return exact is null ? null : Compare(target, exact) == 0;
        }

        string lower = inner[..comma].Trim();
        string upper = inner[(comma + 1)..].Trim();

        if (lower.Length > 0)
        {
            SemanticVersion? min = Parse(lower);
            if (min is null)
            {
                return null;
            }
            int cmp = Compare(target, min);
            if (cmp < 0 || (cmp == 0 && !minInclusive))
            {
                return false;
            }
        }

        if (upper.Length > 0)
        {
            SemanticVersion? max = Parse(upper);
            if (max is null)
            {
                return null;
            }
            int cmp = Compare(target, max);
            if (cmp > 0 || (cmp == 0 && !maxInclusive))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses a version string into its numeric release components and optional prerelease
    /// identifiers, discarding any build-metadata suffix.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The parsed version, or <see langword="null"/> if the release portion is unparseable.</returns>
    private static SemanticVersion? Parse(string version)
    {
        string text = version.Trim();

        // Build metadata ("+...") does not affect precedence; drop it first.
        int plus = text.IndexOf('+');
        if (plus >= 0)
        {
            text = text[..plus];
        }

        // A prerelease ("-...") follows the release; split on the first hyphen.
        string release;
        string[]? prerelease = null;
        int hyphen = text.IndexOf('-');
        if (hyphen >= 0)
        {
            release = text[..hyphen];
            string pre = text[(hyphen + 1)..];

            // A hyphen introduces a prerelease, so a trailing hyphen (empty prerelease) is malformed
            // per SemVer/NuGet rather than "no prerelease"; reject it.
            if (pre.Length == 0)
            {
                return null;
            }

            prerelease = pre.Split('.');

            // Every prerelease identifier must be non-empty (e.g. "alpha..1" is malformed).
            foreach (string identifier in prerelease)
            {
                if (identifier.Length == 0)
                {
                    return null;
                }
            }
        }
        else
        {
            release = text;
        }

        // Keep empty components (do not use RemoveEmptyEntries) so malformed inputs such as "1..0"
        // are rejected below by the numeric parse rather than silently collapsing to "1.0".
        string[] parts = release.Split('.');
        if (parts.Length == 0)
        {
            return null;
        }

        var numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return new SemanticVersion(numbers, prerelease);
    }

    /// <summary>
    /// Compares two versions using SemVer 2.0 precedence: release components first (missing trailing
    /// components treated as zero), then prerelease precedence (a prerelease version is lower than
    /// the same release without one).
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>A negative, zero, or positive value per standard comparison semantics.</returns>
    private static int Compare(SemanticVersion a, SemanticVersion b)
    {
        int length = Math.Max(a.Release.Length, b.Release.Length);
        for (int i = 0; i < length; i++)
        {
            int left = i < a.Release.Length ? a.Release[i] : 0;
            int right = i < b.Release.Length ? b.Release[i] : 0;
            int cmp = left.CompareTo(right);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return ComparePrerelease(a.Prerelease, b.Prerelease);
    }

    /// <summary>
    /// Compares two prerelease identifier sets per SemVer 2.0 precedence rules.
    /// </summary>
    /// <param name="a">The first version's prerelease identifiers, or <see langword="null"/> if none.</param>
    /// <param name="b">The second version's prerelease identifiers, or <see langword="null"/> if none.</param>
    /// <returns>A negative, zero, or positive value per standard comparison semantics.</returns>
    private static int ComparePrerelease(string[]? a, string[]? b)
    {
        // A version without a prerelease has higher precedence than one with a prerelease.
        if (a is null && b is null)
        {
            return 0;
        }
        if (a is null)
        {
            return 1;
        }
        if (b is null)
        {
            return -1;
        }

        int shared = Math.Min(a.Length, b.Length);
        for (int i = 0; i < shared; i++)
        {
            int cmp = CompareIdentifier(a[i], b[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        // When all shared identifiers are equal, the larger set has higher precedence.
        return a.Length.CompareTo(b.Length);
    }

    /// <summary>
    /// Compares two prerelease identifiers: numeric identifiers compare numerically and rank lower
    /// than alphanumeric identifiers, which compare by ASCII order.
    /// </summary>
    private static int CompareIdentifier(string a, string b)
    {
        bool aNumeric = IsNumeric(a);
        bool bNumeric = IsNumeric(b);

        if (aNumeric && bNumeric)
        {
            if (long.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out long na)
                && long.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out long nb))
            {
                return na.CompareTo(nb);
            }

            // Fall back for identifiers too large for long: compare by significant length, then text.
            string ta = a.TrimStart('0');
            string tb = b.TrimStart('0');
            int byLength = ta.Length.CompareTo(tb.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(ta, tb);
        }

        // Numeric identifiers always have lower precedence than alphanumeric ones.
        if (aNumeric)
        {
            return -1;
        }
        if (bNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(a, b);
    }

    /// <summary>Determines whether an identifier consists solely of ASCII digits.</summary>
    private static bool IsNumeric(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A parsed semantic version: numeric release components plus optional prerelease identifiers.
    /// </summary>
    /// <param name="Release">The numeric release components (for example <c>1.2.3</c>).</param>
    /// <param name="Prerelease">The dot-separated prerelease identifiers, or <see langword="null"/> for a release version.</param>
    private sealed record SemanticVersion(int[] Release, string[]? Prerelease);
}
