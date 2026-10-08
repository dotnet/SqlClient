// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PackageValidator;

/// <summary>
/// Classifies a DLL by the role it plays inside a NuGet package. Symbol-coverage expectations
/// differ by kind: only <see cref="Implementation"/> assemblies are expected to ship symbols,
/// while reference assemblies and satellite resource assemblies legitimately have none.
/// </summary>
internal enum BinaryKind
{
    /// <summary>An implementation assembly under <c>lib/</c> or <c>runtimes/.../lib/</c>.</summary>
    Implementation,

    /// <summary>A reference (compile-time) assembly under <c>ref/</c>.</summary>
    Reference,

    /// <summary>A satellite resource assembly (<c>*.resources.dll</c> in a culture subfolder).</summary>
    Satellite,

    /// <summary>A native (unmanaged) binary, such as an SNI runtime.</summary>
    Native,

    /// <summary>A managed DLL that does not fit any of the other categories.</summary>
    Other,
}

/// <summary>
/// The strong-name signing state of a managed assembly.
/// </summary>
internal enum SigningStatus
{
    /// <summary>The assembly has no public key and is not strong-name signed.</summary>
    Unsigned,

    /// <summary>
    /// The assembly carries a public key but the strong-name signed flag is not set, meaning it was
    /// delay-signed and never completed signing.
    /// </summary>
    DelaySigned,

    /// <summary>The assembly carries a public key and the strong-name signed flag is set.</summary>
    Signed,
}

/// <summary>
/// The severity of a validation finding.
/// </summary>
internal enum Severity
{
    /// <summary>Informational; surfaced for visibility but not necessarily a problem.</summary>
    Info,

    /// <summary>A potential problem that may warrant attention.</summary>
    Warning,

    /// <summary>A definite problem.</summary>
    Error,
}

/// <summary>
/// A single validation result produced by the rules engine.
/// </summary>
internal sealed class Finding
{
    /// <summary>Gets the severity of the finding.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Gets the stable category key (for example <c>"missing-symbols"</c>) used by <c>--fail-on</c>.</summary>
    public required string Category { get; init; }

    /// <summary>Gets the target the finding applies to (a package file, an assembly path, etc.).</summary>
    public required string Target { get; init; }

    /// <summary>Gets the human-readable explanation of the finding.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// A NuGet package dependency declared in the <c>.nuspec</c>.
/// </summary>
internal sealed class DependencyInfo
{
    /// <summary>Gets the dependency package id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the declared version range, or <see langword="null"/> if none was specified.</summary>
    public string? VersionRange { get; init; }
}

/// <summary>
/// A group of dependencies, optionally scoped to a single target framework.
/// </summary>
internal sealed class DependencyGroup
{
    /// <summary>Gets the target framework moniker for this group, or <see langword="null"/> for the framework-agnostic group.</summary>
    public string? TargetFramework { get; init; }

    /// <summary>Gets the dependencies declared in this group.</summary>
    public required List<DependencyInfo> Dependencies { get; init; }
}

/// <summary>
/// Win32 version-resource information read from a native binary.
/// </summary>
internal sealed class NativeVersionInfo
{
    /// <summary>Gets the <c>FileVersion</c> string from the Win32 version resource, if present.</summary>
    public string? FileVersion { get; init; }

    /// <summary>Gets the <c>ProductVersion</c> string from the Win32 version resource, if present.</summary>
    public string? ProductVersion { get; init; }

    /// <summary>Gets the <c>ProductName</c> string from the Win32 version resource, if present.</summary>
    public string? ProductName { get; init; }

    /// <summary>Gets the processor architecture of the native image (for example <c>"x64"</c>), if known.</summary>
    public string? Architecture { get; init; }
}

/// <summary>
/// A PDB checksum recorded in an assembly's debug directory, used to verify a matched PDB byte-for-byte.
/// </summary>
internal sealed class PdbChecksum
{
    /// <summary>Gets the hash algorithm name (for example <c>"SHA256"</c>).</summary>
    public required string Algorithm { get; init; }

    /// <summary>Gets the checksum bytes, excluded from JSON output to keep it compact.</summary>
    [JsonIgnore]
    public required byte[] Hash { get; init; }

    /// <summary>Gets the checksum as a lowercase hex string for display.</summary>
    public string HashHex => Convert.ToHexStringLower(Hash);
}
