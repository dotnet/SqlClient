// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PackageValidator;

/// <summary>
/// The top-level result of a validation run over one or more NuGet packages.
/// </summary>
internal sealed class ValidationRun
{
    /// <summary>Gets the per-package reports, ordered by package file name.</summary>
    public required List<PackageReport> Packages { get; init; }

    /// <summary>
    /// Gets findings that span multiple packages (for example dependency version inconsistencies
    /// between packages in the same batch), or <see langword="null"/> when there are none.
    /// </summary>
    public List<Finding>? CrossPackageFindings { get; set; }

    /// <summary>Gets the run-level summary counts.</summary>
    public required ValidationSummary Summary { get; set; }
}

/// <summary>
/// Aggregate counts describing the outcome of a validation run.
/// </summary>
internal sealed class ValidationSummary
{
    /// <summary>Gets the number of packages inspected.</summary>
    public required int PackageCount { get; init; }

    /// <summary>Gets the total number of <see cref="Severity.Error"/> findings across the run.</summary>
    public required int ErrorCount { get; set; }

    /// <summary>Gets the total number of <see cref="Severity.Warning"/> findings across the run.</summary>
    public required int WarningCount { get; set; }

    /// <summary>Gets the total number of <see cref="Severity.Info"/> findings across the run.</summary>
    public required int InfoCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the run failed its configured <c>--fail-on</c> gate.
    /// <see langword="false"/> when no gate was configured or nothing matched it.
    /// </summary>
    public bool Failed { get; set; }
}

/// <summary>
/// The result of inspecting and validating a single NuGet package: its identity, dependencies,
/// signature state, a report for each DLL it contains, the state of its sibling symbol package,
/// and the findings produced by the rules engine.
/// </summary>
internal sealed class PackageReport
{
    /// <summary>Gets the file name of the inspected <c>.nupkg</c>.</summary>
    public required string PackageFile { get; init; }

    /// <summary>Gets the package id from the <c>.nuspec</c>, or <see langword="null"/> if unavailable.</summary>
    public string? PackageId { get; init; }

    /// <summary>Gets the package version from the <c>.nuspec</c>, or <see langword="null"/> if unavailable.</summary>
    public string? PackageVersion { get; init; }

    /// <summary>Gets a value indicating whether the package carries a NuGet author/repository signature.</summary>
    public bool IsSigned { get; init; }

    /// <summary>Gets the dependency groups declared in the <c>.nuspec</c>, or <see langword="null"/> if none.</summary>
    public List<DependencyGroup>? DependencyGroups { get; init; }

    /// <summary>Gets the per-DLL reports, ordered by their path within the package.</summary>
    public required List<BinaryReport> Binaries { get; init; }

    /// <summary>Gets the state and findings of the sibling <c>.snupkg</c> symbol package.</summary>
    public required SymbolPackageInfo SymbolPackage { get; init; }

    /// <summary>Gets the validation findings for this package, or <see langword="null"/> when there are none.</summary>
    public List<Finding>? Findings { get; set; }
}

/// <summary>
/// Package-level summary of the sibling <c>.snupkg</c> symbol package and how well its contents
/// correspond to the assemblies in the main package.
/// </summary>
internal sealed class SymbolPackageInfo
{
    /// <summary>
    /// Gets the processing state: <c>"present"</c> (a sibling symbol package was found and checked),
    /// <c>"missing"</c> (no sibling symbol package exists), or <c>"skipped"</c> (processing was
    /// disabled via <c>--no-snupkg</c>).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Gets the symbol package file name, or <see langword="null"/> unless <see cref="Status"/> is <c>"present"</c>.</summary>
    public string? File { get; init; }

    /// <summary>
    /// Gets whether every implementation assembly has usable symbols (embedded, or a matching
    /// symbol-package PDB). <see langword="null"/> when there are no implementation assemblies.
    /// </summary>
    public bool? AllImplementationAssembliesHaveSymbols { get; init; }

    /// <summary>
    /// Gets whether every assembly with symbol-package symbols has symbols that match its build.
    /// <see langword="null"/> when no symbol package was processed or none of its symbols apply.
    /// </summary>
    public bool? AllSymbolsMatch { get; init; }

    /// <summary>
    /// Gets the symbol package PDB paths that did not correspond to any assembly, or
    /// <see langword="null"/> when there are none.
    /// </summary>
    public List<string>? OrphanSymbolFiles { get; init; }
}

/// <summary>
/// Version, identity, signing, and symbol information for a single DLL found inside a NuGet
/// package. For native or non-assembly DLLs, the managed fields are <see langword="null"/> and
/// <see cref="NativeVersion"/> carries any Win32 version-resource data instead.
/// </summary>
internal sealed class BinaryReport
{
    /// <summary>Gets the path of the DLL within the package archive.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the role this DLL plays inside the package.</summary>
    public required BinaryKind Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the DLL is a managed assembly with a manifest. When
    /// <see langword="false"/>, the managed version fields are <see langword="null"/>.
    /// </summary>
    public bool IsManagedAssembly { get; init; }

    /// <summary>Gets the simple assembly name from the manifest.</summary>
    public string? AssemblyName { get; init; }

    /// <summary>Gets the four-part assembly version (the .NET assembly version).</summary>
    public string? AssemblyVersion { get; init; }

    /// <summary>Gets the value of <see cref="System.Reflection.AssemblyFileVersionAttribute"/>, if present.</summary>
    public string? FileVersion { get; init; }

    /// <summary>Gets the value of <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/>, if present.</summary>
    public string? InformationalVersion { get; init; }

    /// <summary>Gets the value of <see cref="System.Runtime.Versioning.TargetFrameworkAttribute"/>, if present.</summary>
    public string? TargetFramework { get; init; }

    /// <summary>Gets the assembly culture, or <c>"neutral"</c> for culture-independent assemblies.</summary>
    public string? Culture { get; init; }

    /// <summary>Gets the strong-name public key token, or <see langword="null"/> if unsigned.</summary>
    public string? PublicKeyToken { get; init; }

    /// <summary>Gets the strong-name signing state of the assembly.</summary>
    public SigningStatus? SigningStatus { get; init; }

    /// <summary>Gets the full strong-name display name (name, version, culture, public key token).</summary>
    public string? StrongName { get; init; }

    /// <summary>Gets the Win32 version-resource information for a native binary, if any.</summary>
    public NativeVersionInfo? NativeVersion { get; init; }

    /// <summary>
    /// Gets the assembly's CodeView debug GUID used to match it to a PDB. Excluded from serialized
    /// output; the formatted <see cref="DebugId"/> is emitted instead.
    /// </summary>
    [JsonIgnore]
    public Guid? CodeViewGuid { get; init; }

    /// <summary>Gets the assembly's debug (PDB) identity GUID as a string, or <see langword="null"/> if absent.</summary>
    public string? DebugId => CodeViewGuid?.ToString();

    /// <summary>
    /// Gets the PDB checksums recorded in the assembly's debug directory, used to verify a matched
    /// PDB. Excluded from JSON; <see cref="PdbChecksums"/> exposes a display-friendly view.
    /// </summary>
    [JsonIgnore]
    public List<PdbChecksum>? Checksums { get; init; }

    /// <summary>Gets the PDB checksum algorithms recorded for this assembly, for display, or <see langword="null"/> if none.</summary>
    public List<string>? PdbChecksums =>
        Checksums is { Count: > 0 } ? Checksums.Select(c => $"{c.Algorithm}:{c.HashHex}").ToList() : null;

    /// <summary>
    /// Gets a value indicating whether the assembly embeds its own portable PDB. Always evaluated,
    /// independent of symbol-package processing.
    /// </summary>
    public bool? HasEmbeddedSymbols { get; init; }

    /// <summary>
    /// Gets a value indicating whether usable symbols are available for this assembly, either
    /// embedded or via a matching symbol-package PDB.
    /// </summary>
    public bool? HasSymbols { get; set; }

    /// <summary>
    /// Gets a value indicating whether the symbol package contains a PDB for this assembly.
    /// <see langword="null"/> when no symbol package was processed.
    /// </summary>
    public bool? HasSymbolPackageSymbols { get; set; }

    /// <summary>
    /// Gets a value indicating whether the symbol-package PDB matches this assembly's build (by
    /// debug GUID). <see langword="null"/> when the symbol package has no PDB for this assembly or
    /// was not processed.
    /// </summary>
    public bool? SymbolPackageSymbolsMatch { get; set; }

    /// <summary>
    /// Gets a value indicating whether the matched symbol-package PDB was verified byte-for-byte
    /// against the assembly's recorded PDB checksum. <see langword="true"/> when a checksum matched,
    /// <see langword="false"/> when a checksum was available but did not match, and
    /// <see langword="null"/> when no checksum could be evaluated.
    /// </summary>
    public bool? SymbolPackageVerifiedByChecksum { get; set; }

    /// <summary>Gets the matching PDB's path within the symbol package, if any.</summary>
    public string? SymbolPackageFile { get; set; }

    /// <summary>
    /// Creates a report for a native or non-assembly DLL, recording its path, native version info,
    /// and marking it as unmanaged.
    /// </summary>
    /// <param name="path">The DLL's path within the package archive.</param>
    /// <param name="nativeVersion">Win32 version-resource information, if any was read.</param>
    /// <returns>A <see cref="BinaryReport"/> with <see cref="IsManagedAssembly"/> set to <see langword="false"/>.</returns>
    public static BinaryReport Native(string path, NativeVersionInfo? nativeVersion = null) => new()
    {
        Path = path,
        Kind = BinaryKind.Native,
        IsManagedAssembly = false,
        NativeVersion = nativeVersion,
    };
}
