using System.Globalization;

namespace PackageValidator;

/// <summary>
/// A minimal evaluator for NuGet dependency version ranges, sufficient to check whether a concrete
/// package version falls inside a declared range.
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

        int[]? target = Parse(version);
        if (target is null)
        {
            return null;
        }

        range = range.Trim();

        // A bare version (no brackets) is a minimum-inclusive bound in NuGet semantics.
        if (range[0] != '[' && range[0] != '(')
        {
            int[]? min = Parse(range);
            return min is null ? null : Compare(target, min) >= 0;
        }

        bool minInclusive = range[0] == '[';
        bool maxInclusive = range[^1] == ']';
        string inner = range[1..^1];

        int comma = inner.IndexOf(',');
        if (comma < 0)
        {
            // "[1.0.0]" denotes an exact version.
            int[]? exact = Parse(inner);
            return exact is null ? null : Compare(target, exact) == 0;
        }

        string lower = inner[..comma].Trim();
        string upper = inner[(comma + 1)..].Trim();

        if (lower.Length > 0)
        {
            int[]? min = Parse(lower);
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
            int[]? max = Parse(upper);
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
    /// Parses the numeric release portion of a version string, ignoring any prerelease or build
    /// metadata suffix.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The numeric release components, or <see langword="null"/> if none could be parsed.</returns>
    private static int[]? Parse(string version)
    {
        // Drop prerelease ("-") and build metadata ("+") suffixes; only the release parts are
        // compared by this lightweight evaluator.
        int cut = version.IndexOfAny(['-', '+']);
        string release = cut < 0 ? version : version[..cut];

        string[] parts = release.Split('.', StringSplitOptions.RemoveEmptyEntries);
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

        return numbers;
    }

    /// <summary>
    /// Compares two numeric version component arrays, treating missing trailing components as zero.
    /// </summary>
    /// <param name="a">The first version's components.</param>
    /// <param name="b">The second version's components.</param>
    /// <returns>A negative, zero, or positive value per standard comparison semantics.</returns>
    private static int Compare(int[] a, int[] b)
    {
        int length = Math.Max(a.Length, b.Length);
        for (int i = 0; i < length; i++)
        {
            int left = i < a.Length ? a[i] : 0;
            int right = i < b.Length ? b[i] : 0;
            int cmp = left.CompareTo(right);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }
}
