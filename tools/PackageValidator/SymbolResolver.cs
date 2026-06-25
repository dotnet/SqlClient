using System.IO.Compression;

namespace PackageValidator;

/// <summary>
/// Cross-checks a package's sibling <c>.snupkg</c> symbol package against the assemblies in the
/// main package, matching PDBs by debug GUID, verifying them by checksum where possible, and
/// detecting orphaned or mismatched symbol files.
/// </summary>
internal static class SymbolResolver
{
    /// <summary>
    /// Evaluates symbol coverage for the discovered binaries. Embedded symbols are always assessed;
    /// the sibling <c>.snupkg</c> symbol package is additionally cross-checked unless disabled.
    /// </summary>
    /// <param name="packagePath">The path to the inspected <c>.nupkg</c>.</param>
    /// <param name="binaries">The binaries discovered in the package (modified in place).</param>
    /// <param name="processSnupkg">Whether to look for and process a separate symbol package.</param>
    /// <returns>A <see cref="SymbolPackageInfo"/> describing the symbol state.</returns>
    public static SymbolPackageInfo Resolve(
        string packagePath, List<BinaryReport> binaries, bool processSnupkg)
    {
        // Symbols apply only to managed assemblies. Embedded-symbol state was already captured during
        // inspection and is always honored, independent of the --no-snupkg switch.
        List<BinaryReport> managed = binaries.Where(b => b.IsManagedAssembly).ToList();

        string status;
        string? file = null;
        var matchedPdbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allPdbPaths = new List<string>();

        if (!processSnupkg)
        {
            // The separate symbol package is intentionally not consulted; embedded symbols below are
            // still evaluated.
            status = "skipped";
        }
        else
        {
            // The symbol package is, by NuGet convention, named identically but with a .snupkg
            // extension sitting right beside the .nupkg.
            string snupkgPath = Path.ChangeExtension(packagePath, ".snupkg");
            if (!File.Exists(snupkgPath))
            {
                status = "missing";
            }
            else
            {
                status = "present";
                file = Path.GetFileName(snupkgPath);
                MatchSymbolPackage(snupkgPath, managed, matchedPdbs, allPdbPaths);
            }
        }

        // Combine the two symbol sources per assembly.
        foreach (BinaryReport asm in managed)
        {
            bool embedded = asm.HasEmbeddedSymbols == true;
            bool packageMatches = asm.SymbolPackageSymbolsMatch == true;
            asm.HasSymbols = embedded || packageMatches;
        }

        // Coverage and match quality are assessed only over implementation assemblies; reference
        // assemblies and satellite resource assemblies legitimately ship without symbols.
        List<BinaryReport> implementation = managed.Where(b => b.Kind == BinaryKind.Implementation).ToList();
        bool? allHaveSymbols = implementation.Count == 0
            ? null
            : implementation.All(a => a.HasSymbols == true);

        bool? allSymbolsMatch = null;
        List<string>? orphans = null;
        if (status == "present")
        {
            List<BinaryReport> withPackageSymbols =
                managed.Where(a => a.HasSymbolPackageSymbols == true).ToList();
            allSymbolsMatch = withPackageSymbols.Count == 0
                ? null
                : withPackageSymbols.All(a => a.SymbolPackageSymbolsMatch == true);

            List<string> unmatched = allPdbPaths
                .Where(p => !matchedPdbs.Contains(p))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            orphans = unmatched.Count == 0 ? null : unmatched;
        }

        return new SymbolPackageInfo
        {
            Status = status,
            File = file,
            AllImplementationAssembliesHaveSymbols = allHaveSymbols,
            AllSymbolsMatch = allSymbolsMatch,
            OrphanSymbolFiles = orphans,
        };
    }

    /// <summary>
    /// Indexes the PDBs in a symbol package and resolves each managed assembly against them.
    /// </summary>
    /// <param name="snupkgPath">The path to the sibling symbol package.</param>
    /// <param name="managed">The managed assemblies to resolve (modified in place).</param>
    /// <param name="matchedPdbs">Receives the set of PDB paths that were matched to an assembly.</param>
    /// <param name="allPdbPaths">Receives every PDB path found in the symbol package.</param>
    private static void MatchSymbolPackage(
        string snupkgPath,
        List<BinaryReport> managed,
        HashSet<string> matchedPdbs,
        List<string> allPdbPaths)
    {
        using ZipArchive archive = ZipFile.OpenRead(snupkgPath);

        // Index every PDB by its GUID (authoritative identity match) and by its path with the
        // extension stripped (fallback to detect a present-but-mismatched symbol file). The bytes are
        // retained so a GUID match can be further verified against the assembly's PDB checksum.
        var pdbByGuid = new Dictionary<Guid, string>();
        var pdbByPathKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pdbBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            allPdbPaths.Add(entry.FullName);
            pdbByPathKey[StripExtension(entry.FullName)] = entry.FullName;

            byte[] bytes = ReadEntry(entry);
            pdbBytes[entry.FullName] = bytes;

            Guid? guid = PortablePdb.TryReadGuid(bytes);
            if (guid is Guid value && !pdbByGuid.ContainsKey(value))
            {
                pdbByGuid[value] = entry.FullName;
            }
        }

        foreach (BinaryReport asm in managed)
        {
            if (asm.CodeViewGuid is Guid guid && pdbByGuid.TryGetValue(guid, out string? byGuid))
            {
                // A symbol-package PDB shares this assembly's debug GUID.
                asm.HasSymbolPackageSymbols = true;
                asm.SymbolPackageSymbolsMatch = true;
                asm.SymbolPackageFile = byGuid;
                asm.SymbolPackageVerifiedByChecksum = VerifyChecksum(asm, pdbBytes[byGuid]);
                matchedPdbs.Add(byGuid);
            }
            else if (pdbByPathKey.TryGetValue(StripExtension(asm.Path), out string? byPath))
            {
                // A PDB sits where this assembly's symbols should be, but its GUID does not match,
                // meaning the symbols belong to a different build.
                asm.HasSymbolPackageSymbols = true;
                asm.SymbolPackageSymbolsMatch = false;
                asm.SymbolPackageFile = byPath;
                matchedPdbs.Add(byPath);
            }
            else
            {
                asm.HasSymbolPackageSymbols = false;
            }
        }
    }

    /// <summary>
    /// Verifies a matched PDB against the assembly's recorded PDB checksums.
    /// </summary>
    /// <param name="asm">The assembly whose checksums drive verification.</param>
    /// <param name="pdb">The matched PDB bytes.</param>
    /// <returns>
    /// <see langword="true"/> if any recorded checksum matches, <see langword="false"/> if checksums
    /// were available but none matched, and <see langword="null"/> if no checksum could be evaluated.
    /// </returns>
    private static bool? VerifyChecksum(BinaryReport asm, byte[] pdb)
    {
        if (asm.Checksums is not { Count: > 0 } checksums)
        {
            return null;
        }

        bool anyEvaluated = false;
        foreach (PdbChecksum checksum in checksums)
        {
            bool? result = PortablePdb.TryVerifyChecksum(pdb, checksum);
            if (result is true)
            {
                return true;
            }
            if (result is false)
            {
                anyEvaluated = true;
            }
        }

        // If at least one checksum was evaluated and none matched, the PDB does not correspond to the
        // assembly's build despite the GUID; otherwise verification was inconclusive.
        return anyEvaluated ? false : null;
    }

    /// <summary>Reads all bytes of an archive entry into a buffer.</summary>
    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var buffer = new MemoryStream();
        using (Stream stream = entry.Open())
        {
            stream.CopyTo(buffer);
        }
        return buffer.ToArray();
    }

    /// <summary>
    /// Removes the final file extension from an archive path, yielding a key that aligns a DLL with
    /// its co-located PDB.
    /// </summary>
    private static string StripExtension(string path) =>
        path[..^Path.GetExtension(path).Length];
}
