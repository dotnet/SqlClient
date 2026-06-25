namespace PackageValidator;

/// <summary>
/// Stable finding category keys. These are the values accepted by <c>--fail-on</c>.
/// </summary>
internal static class Categories
{
    public const string VersionInconsistency = "version-inconsistency";
    public const string MissingSymbols = "missing-symbols";
    public const string SymbolMismatch = "symbol-mismatch";
    public const string SymbolChecksumMismatch = "symbol-checksum-mismatch";
    public const string SymbolOrphan = "symbol-orphan";
    public const string SymbolDuplicate = "symbol-duplicate";
    public const string DelaySigned = "delay-signed";
    public const string Unsigned = "unsigned";
    public const string PackageUnsigned = "package-unsigned";
    public const string DependencyInconsistency = "dependency-inconsistency";
    public const string UnexpectedPackageVersion = "unexpected-package-version";
    public const string UnexpectedFileVersion = "unexpected-file-version";
    public const string UnexpectedAssemblyVersion = "unexpected-assembly-version";

    /// <summary>Gets every known category key.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        VersionInconsistency, MissingSymbols, SymbolMismatch, SymbolChecksumMismatch,
        SymbolOrphan, SymbolDuplicate, DelaySigned, Unsigned, PackageUnsigned,
        DependencyInconsistency, UnexpectedPackageVersion, UnexpectedFileVersion,
        UnexpectedAssemblyVersion,
    ];
}

/// <summary>
/// Applies the intrinsic validation rules to inspected packages, producing per-package and
/// cross-package <see cref="Finding"/> instances.
/// </summary>
internal static class Validator
{
    /// <summary>
    /// Validates a single package, attaching its findings to <see cref="PackageReport.Findings"/>.
    /// </summary>
    /// <param name="report">The inspected package report (modified in place).</param>
    /// <param name="expectations">
    /// Optional caller-supplied expected version values to confirm against, or <see langword="null"/>
    /// to skip expected-version checks.
    /// </param>
    public static void Validate(PackageReport report, VersionExpectations? expectations = null)
    {
        var findings = new List<Finding>();

        CheckVersionConsistency(report, findings);
        CheckExpectedVersions(report, expectations, findings);
        CheckSymbols(report, findings);
        CheckSigning(report, findings);
        CheckPackageSignature(report, findings);

        report.Findings = findings.Count == 0 ? null : findings;
    }

    /// <summary>
    /// Confirms a package's version, and its assemblies' file and assembly versions, against the
    /// caller-supplied expected values. Pointing every package at the same expected value provides
    /// inter-package version-match validation and also catches an all-wrong-but-consistent build.
    /// </summary>
    private static void CheckExpectedVersions(
        PackageReport report, VersionExpectations? expectations, List<Finding> findings)
    {
        if (expectations is null || expectations.IsEmpty)
        {
            return;
        }

        string? expectedPackage = expectations.PackageVersionFor(report.PackageId);
        if (expectedPackage is not null
            && !string.Equals(report.PackageVersion, expectedPackage, StringComparison.Ordinal))
        {
            findings.Add(new Finding
            {
                Severity = Severity.Error,
                Category = Categories.UnexpectedPackageVersion,
                Target = report.PackageFile,
                Message =
                    $"package version is '{report.PackageVersion ?? "(none)"}', expected '{expectedPackage}'.",
            });
        }

        string? expectedFile = expectations.FileVersionFor(report.PackageId);
        string? expectedAssembly = expectations.AssemblyVersionFor(report.PackageId);

        // File and assembly versions are expected to be uniform across every managed assembly the
        // package ships, so confirm each one.
        foreach (BinaryReport asm in report.Binaries.Where(b => b.IsManagedAssembly))
        {
            if (expectedFile is not null
                && asm.FileVersion is not null
                && !string.Equals(asm.FileVersion, expectedFile, StringComparison.Ordinal))
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Error,
                    Category = Categories.UnexpectedFileVersion,
                    Target = asm.Path,
                    Message = $"file version is '{asm.FileVersion}', expected '{expectedFile}'.",
                });
            }

            if (expectedAssembly is not null
                && asm.AssemblyVersion is not null
                && !string.Equals(asm.AssemblyVersion, expectedAssembly, StringComparison.Ordinal))
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Error,
                    Category = Categories.UnexpectedAssemblyVersion,
                    Target = asm.Path,
                    Message = $"assembly version is '{asm.AssemblyVersion}', expected '{expectedAssembly}'.",
                });
            }
        }
    }

    /// <summary>
    /// Validates relationships that span multiple packages in a single run.
    /// </summary>
    /// <param name="reports">All package reports in the run.</param>
    /// <returns>The cross-package findings, or an empty list when there are none.</returns>
    public static List<Finding> ValidateBatch(IReadOnlyList<PackageReport> reports)
    {
        var findings = new List<Finding>();

        // Index packages by id so dependencies on in-batch packages can be checked for version
        // consistency.
        var byId = new Dictionary<string, PackageReport>(StringComparer.OrdinalIgnoreCase);
        foreach (PackageReport report in reports)
        {
            if (report.PackageId is { Length: > 0 } id)
            {
                byId[id] = report;
            }
        }

        foreach (PackageReport report in reports)
        {
            if (report.DependencyGroups is null)
            {
                continue;
            }

            foreach (DependencyGroup group in report.DependencyGroups)
            {
                foreach (DependencyInfo dependency in group.Dependencies)
                {
                    if (!byId.TryGetValue(dependency.Id, out PackageReport? target)
                        || target.PackageVersion is not { Length: > 0 } targetVersion)
                    {
                        continue;
                    }

                    if (VersionRange.Satisfies(dependency.VersionRange, targetVersion) == false)
                    {
                        findings.Add(new Finding
                        {
                            Severity = Severity.Warning,
                            Category = Categories.DependencyInconsistency,
                            Target = report.PackageFile,
                            Message =
                                $"depends on {dependency.Id} {dependency.VersionRange}, but the {dependency.Id} " +
                                $"package in this run is version {targetVersion}, which is outside that range.",
                        });
                    }
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Flags assemblies of the same name that carry inconsistent assembly or file versions.
    /// </summary>
    private static void CheckVersionConsistency(PackageReport report, List<Finding> findings)
    {
        // Group by assembly name, excluding satellite resource assemblies (which mirror their
        // parent and need no independent check).
        IEnumerable<IGrouping<string, BinaryReport>> groups = report.Binaries
            .Where(b => b.IsManagedAssembly && b.Kind != BinaryKind.Satellite && b.AssemblyName is not null)
            .GroupBy(b => b.AssemblyName!);

        foreach (IGrouping<string, BinaryReport> group in groups)
        {
            List<string> assemblyVersions = group
                .Select(b => b.AssemblyVersion)
                .Where(v => v is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList()!;
            if (assemblyVersions.Count > 1)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Error,
                    Category = Categories.VersionInconsistency,
                    Target = $"{report.PackageFile}:{group.Key}",
                    Message =
                        $"assembly '{group.Key}' ships with multiple assembly versions: " +
                        $"{string.Join(", ", assemblyVersions)}.",
                });
            }

            List<string> fileVersions = group
                .Select(b => b.FileVersion)
                .Where(v => v is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList()!;
            if (fileVersions.Count > 1)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Warning,
                    Category = Categories.VersionInconsistency,
                    Target = $"{report.PackageFile}:{group.Key}",
                    Message =
                        $"assembly '{group.Key}' ships with multiple file versions: " +
                        $"{string.Join(", ", fileVersions)}.",
                });
            }
        }
    }

    /// <summary>
    /// Flags missing, mismatched, checksum-failed, duplicated, and orphaned symbols.
    /// </summary>
    private static void CheckSymbols(PackageReport report, List<Finding> findings)
    {
        foreach (BinaryReport asm in report.Binaries.Where(b => b.IsManagedAssembly))
        {
            // Only implementation assemblies are expected to ship symbols.
            if (asm.Kind == BinaryKind.Implementation && asm.HasSymbols != true)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Warning,
                    Category = Categories.MissingSymbols,
                    Target = asm.Path,
                    Message = "implementation assembly has no embedded or symbol-package symbols.",
                });
            }

            if (asm.HasSymbolPackageSymbols == true && asm.SymbolPackageSymbolsMatch == false)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Error,
                    Category = Categories.SymbolMismatch,
                    Target = asm.Path,
                    Message =
                        $"symbol-package PDB '{asm.SymbolPackageFile}' does not match the assembly build (GUID differs).",
                });
            }

            if (asm.SymbolPackageVerifiedByChecksum == false)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Error,
                    Category = Categories.SymbolChecksumMismatch,
                    Target = asm.Path,
                    Message =
                        $"symbol-package PDB '{asm.SymbolPackageFile}' matched by GUID but failed checksum verification.",
                });
            }

            if (asm.HasEmbeddedSymbols == true && asm.HasSymbolPackageSymbols == true)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Info,
                    Category = Categories.SymbolDuplicate,
                    Target = asm.Path,
                    Message = "symbols are present both embedded and in the symbol package.",
                });
            }
        }

        if (report.SymbolPackage.OrphanSymbolFiles is { Count: > 0 } orphans)
        {
            foreach (string orphan in orphans)
            {
                findings.Add(new Finding
                {
                    Severity = Severity.Warning,
                    Category = Categories.SymbolOrphan,
                    Target = orphan,
                    Message = "symbol-package PDB does not correspond to any assembly in the package.",
                });
            }
        }
    }

    /// <summary>
    /// Flags delay-signed and unsigned implementation assemblies.
    /// </summary>
    private static void CheckSigning(PackageReport report, List<Finding> findings)
    {
        foreach (BinaryReport asm in report.Binaries.Where(
            b => b.IsManagedAssembly && b.Kind == BinaryKind.Implementation))
        {
            switch (asm.SigningStatus)
            {
                case PackageValidator.SigningStatus.DelaySigned:
                    findings.Add(new Finding
                    {
                        Severity = Severity.Warning,
                        Category = Categories.DelaySigned,
                        Target = asm.Path,
                        Message = "assembly carries a public key but is delay-signed (strong-name flag not set).",
                    });
                    break;

                case PackageValidator.SigningStatus.Unsigned:
                    findings.Add(new Finding
                    {
                        Severity = Severity.Info,
                        Category = Categories.Unsigned,
                        Target = asm.Path,
                        Message = "assembly is not strong-name signed.",
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Flags a package that carries no NuGet author/repository signature.
    /// </summary>
    private static void CheckPackageSignature(PackageReport report, List<Finding> findings)
    {
        if (!report.IsSigned)
        {
            findings.Add(new Finding
            {
                Severity = Severity.Info,
                Category = Categories.PackageUnsigned,
                Target = report.PackageFile,
                Message = "package is not signed (no .signature.p7s entry).",
            });
        }
    }
}
