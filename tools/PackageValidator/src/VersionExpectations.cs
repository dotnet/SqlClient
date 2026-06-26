namespace PackageValidator;

/// <summary>
/// Caller-supplied expected version values, keyed by package id, used to confirm that inspected
/// packages and their assemblies carry the versions the build was supposed to produce. This enables
/// inter-package version-match validation: pointing every package at the same expected value proves
/// they agree, and also catches the case where every package is consistently wrong.
/// </summary>
internal sealed class VersionExpectations
{
    /// <summary>The wildcard id that matches any package when no specific id is supplied.</summary>
    public const string Wildcard = "*";

    private readonly Dictionary<string, string> _package = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _file = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _assembly = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether any expectation was supplied.</summary>
    public bool IsEmpty => _package.Count == 0 && _file.Count == 0 && _assembly.Count == 0;

    /// <summary>
    /// Parses the raw <c>--expect-*</c> option values into a <see cref="VersionExpectations"/>.
    /// </summary>
    /// <param name="packageVersion">The <c>--expect-package-version</c> specs.</param>
    /// <param name="fileVersion">The <c>--expect-file-version</c> specs.</param>
    /// <param name="assemblyVersion">The <c>--expect-assembly-version</c> specs.</param>
    /// <returns>The parsed expectations.</returns>
    /// <exception cref="FormatException">Thrown when a spec has an empty value.</exception>
    public static VersionExpectations Parse(
        string[] packageVersion, string[] fileVersion, string[] assemblyVersion)
    {
        var expectations = new VersionExpectations();
        Fill(expectations._package, packageVersion, "--expect-package-version");
        Fill(expectations._file, fileVersion, "--expect-file-version");
        Fill(expectations._assembly, assemblyVersion, "--expect-assembly-version");
        return expectations;
    }

    /// <summary>Gets the expected package version for an id, or <see langword="null"/> if none applies.</summary>
    public string? PackageVersionFor(string? id) => Resolve(_package, id);

    /// <summary>Gets the expected file version for an id, or <see langword="null"/> if none applies.</summary>
    public string? FileVersionFor(string? id) => Resolve(_file, id);

    /// <summary>Gets the expected assembly version for an id, or <see langword="null"/> if none applies.</summary>
    public string? AssemblyVersionFor(string? id) => Resolve(_assembly, id);

    /// <summary>
    /// Parses each <c>[id=]value</c> spec into a map entry, defaulting a missing id to the wildcard.
    /// </summary>
    private static void Fill(Dictionary<string, string> map, string[] specs, string optionName)
    {
        foreach (string raw in specs)
        {
            string spec = raw.Trim();
            if (spec.Length == 0)
            {
                continue;
            }

            // Package ids never contain '=' and version values never do either, so the first '='
            // unambiguously separates an optional id from the value.
            int eq = spec.IndexOf('=');
            string id = eq < 0 ? Wildcard : spec[..eq].Trim();
            string value = eq < 0 ? spec : spec[(eq + 1)..].Trim();

            if (value.Length == 0)
            {
                throw new FormatException($"{optionName}: missing value in '{raw}'.");
            }

            map[id.Length == 0 ? Wildcard : id] = value;
        }
    }

    /// <summary>
    /// Resolves an expectation for a package id, preferring a specific id over the wildcard.
    /// </summary>
    private static string? Resolve(Dictionary<string, string> map, string? id)
    {
        if (id is not null && map.TryGetValue(id, out string? specific))
        {
            return specific;
        }

        return map.TryGetValue(Wildcard, out string? wildcard) ? wildcard : null;
    }
}
