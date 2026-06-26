using System.IO.Compression;
using System.Xml.Linq;

namespace PackageValidator;

/// <summary>
/// Opens a NuGet package and builds a <see cref="PackageReport"/> describing its metadata,
/// dependencies, signature state, contained binaries, and symbol-package state.
/// </summary>
internal static class PackageInspector
{
    /// <summary>The archive entry name of a NuGet package signature.</summary>
    private const string SignatureEntryName = ".signature.p7s";

    /// <summary>
    /// Inspects a NuGet package.
    /// </summary>
    /// <param name="packagePath">The path to the <c>.nupkg</c> file on disk.</param>
    /// <param name="processSnupkg">
    /// When <see langword="true"/>, a like-named <c>.snupkg</c> beside the package is located and
    /// cross-checked against the assemblies; when <see langword="false"/>, symbol-package processing
    /// is skipped (embedded symbols are still evaluated).
    /// </param>
    /// <returns>A populated <see cref="PackageReport"/>.</returns>
    public static PackageReport Inspect(string packagePath, bool processSnupkg)
    {
        // A .nupkg is a zip archive; open it read-only and walk its entries.
        using ZipArchive archive = ZipFile.OpenRead(packagePath);

        // Package-level identity and dependencies come from the embedded .nuspec manifest.
        (string? id, string? version, List<DependencyGroup>? dependencyGroups) = ReadNuspec(archive);

        // A NuGet signature is stored as a top-level .signature.p7s archive entry.
        bool isSigned = archive.Entries.Any(
            e => e.FullName.Equals(SignatureEntryName, StringComparison.OrdinalIgnoreCase));

        // Inspect every .dll entry regardless of where it sits in the package (lib/, ref/,
        // runtimes/, etc.). Native DLLs are tolerated and reported as unmanaged.
        var binaries = new List<BinaryReport>();
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            binaries.Add(AssemblyInspector.Inspect(entry));
        }

        // Sort by archive path so output is deterministic across runs and platforms.
        binaries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        // Cross-check the sibling symbol package (if any) against the binaries just discovered. This
        // mutates the per-binary symbol fields in place.
        SymbolPackageInfo symbolPackage = SymbolResolver.Resolve(packagePath, binaries, processSnupkg);

        return new PackageReport
        {
            PackageFile = Path.GetFileName(packagePath),
            PackageId = id,
            PackageVersion = version,
            IsSigned = isSigned,
            DependencyGroups = dependencyGroups,
            Binaries = binaries,
            SymbolPackage = symbolPackage,
        };
    }

    /// <summary>
    /// Reads the package id, version, and dependencies from the <c>.nuspec</c> manifest.
    /// </summary>
    /// <param name="archive">The opened NuGet package archive.</param>
    /// <returns>The package id, version, and dependency groups, each of which may be <see langword="null"/>.</returns>
    private static (string? Id, string? Version, List<DependencyGroup>? Dependencies) ReadNuspec(ZipArchive archive)
    {
        // The canonical manifest sits at the archive root, so prefer a top-level .nuspec.
        ZipArchiveEntry? nuspec = archive.Entries.FirstOrDefault(
            e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                 && !e.FullName.Contains('/'));

        // Fall back to any .nuspec anywhere in the package for non-standard layouts.
        nuspec ??= archive.Entries.FirstOrDefault(
            e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        if (nuspec is null)
        {
            return (null, null, null);
        }

        using Stream stream = nuspec.Open();
        XDocument doc = XDocument.Load(stream);

        // .nuspec files declare an XML namespace that varies by schema version. Matching on
        // LocalName keeps this robust regardless of which namespace URI is in use.
        XElement? metadata = doc.Root?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "metadata");

        string? id = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value?.Trim();
        string? version = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "version")?.Value?.Trim();
        List<DependencyGroup>? dependencies = ReadDependencies(metadata);

        return (id, version, dependencies);
    }

    /// <summary>
    /// Parses the <c>&lt;dependencies&gt;</c> element of a <c>.nuspec</c>, supporting both the flat
    /// (ungrouped) and grouped-by-target-framework layouts.
    /// </summary>
    /// <param name="metadata">The <c>&lt;metadata&gt;</c> element, or <see langword="null"/>.</param>
    /// <returns>The parsed dependency groups, or <see langword="null"/> if none are declared.</returns>
    private static List<DependencyGroup>? ReadDependencies(XElement? metadata)
    {
        XElement? dependencies = metadata?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "dependencies");
        if (dependencies is null)
        {
            return null;
        }

        var groups = new List<DependencyGroup>();

        // Flat dependencies (no <group>) apply to all target frameworks.
        List<DependencyInfo> ungrouped = dependencies.Elements()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(ReadDependency)
            .ToList();
        if (ungrouped.Count > 0)
        {
            groups.Add(new DependencyGroup { TargetFramework = null, Dependencies = ungrouped });
        }

        // Grouped dependencies are scoped to a target framework.
        foreach (XElement group in dependencies.Elements().Where(e => e.Name.LocalName == "group"))
        {
            string? tfm = group.Attribute("targetFramework")?.Value?.Trim();
            List<DependencyInfo> deps = group.Elements()
                .Where(e => e.Name.LocalName == "dependency")
                .Select(ReadDependency)
                .ToList();
            groups.Add(new DependencyGroup { TargetFramework = tfm, Dependencies = deps });
        }

        return groups.Count == 0 ? null : groups;
    }

    /// <summary>
    /// Reads a single <c>&lt;dependency&gt;</c> element.
    /// </summary>
    /// <param name="element">The dependency element.</param>
    /// <returns>The parsed <see cref="DependencyInfo"/>.</returns>
    private static DependencyInfo ReadDependency(XElement element) => new()
    {
        Id = element.Attribute("id")?.Value?.Trim() ?? "(unknown)",
        VersionRange = element.Attribute("version")?.Value?.Trim(),
    };
}
