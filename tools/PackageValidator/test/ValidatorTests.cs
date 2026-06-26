using System.Linq;
using Xunit;

namespace PackageValidator.Tests;

/// <summary>
/// Tests for <see cref="Validator"/>, the intrinsic rules engine. Each test builds a synthetic
/// <see cref="PackageReport"/> so a single rule can be exercised in isolation, then asserts on the
/// findings the validator attaches.
/// </summary>
public class ValidatorTests
{
    /// <summary>
    /// Creates a symbol-package descriptor in the "skipped" state, so symbol rules stay inert and a
    /// test can focus on the rule under examination.
    /// </summary>
    /// <returns>A <see cref="SymbolPackageInfo"/> with status <c>"skipped"</c>.</returns>
    private static SymbolPackageInfo SkippedSymbols() => new()
    {
        Status = "skipped",
    };

    /// <summary>
    /// Builds a minimal, otherwise-valid signed package report wrapping the given binaries, so a
    /// test only has to specify the binaries relevant to the rule it exercises.
    /// </summary>
    /// <param name="binaries">The binaries the package contains.</param>
    /// <returns>A populated <see cref="PackageReport"/>.</returns>
    private static PackageReport Package(params BinaryReport[] binaries) => new()
    {
        PackageFile = "Test.1.0.0.nupkg",
        PackageId = "Test",
        PackageVersion = "1.0.0",
        IsSigned = true,
        Binaries = binaries.ToList(),
        SymbolPackage = SkippedSymbols(),
    };

    /// <summary>
    /// Verifies that two assemblies of the same name carrying different assembly versions raise an
    /// Error-severity version-inconsistency finding.
    /// </summary>
    [Fact]
    public void Flags_inconsistent_assembly_versions_as_error()
    {
        // Arrange: the same assembly under two TFMs with conflicting assembly versions.
        PackageReport report = Package(
            new BinaryReport
            {
                Path = "lib/net8.0/Foo.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                AssemblyName = "Foo",
                AssemblyVersion = "1.0.0.0",
                FileVersion = "1.0.0.0",
                HasSymbols = true,
            },
            new BinaryReport
            {
                Path = "lib/net462/Foo.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                AssemblyName = "Foo",
                AssemblyVersion = "2.0.0.0",
                FileVersion = "1.0.0.0",
                HasSymbols = true,
            });

        // Act.
        Validator.Validate(report);

        // Assert: an Error version-inconsistency finding is produced.
        Assert.NotNull(report.Findings);
        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.VersionInconsistency && f.Severity == Severity.Error);
    }

    /// <summary>
    /// Verifies that same-named assemblies with matching assembly versions but differing file
    /// versions raise a Warning-severity version-inconsistency finding (a weaker signal than an
    /// assembly-version conflict).
    /// </summary>
    [Fact]
    public void Flags_inconsistent_file_versions_as_warning()
    {
        // Arrange: identical assembly versions, but the file-version revision differs between TFMs.
        PackageReport report = Package(
            new BinaryReport
            {
                Path = "lib/net8.0/Foo.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                AssemblyName = "Foo",
                AssemblyVersion = "1.0.0.0",
                FileVersion = "1.0.0.0",
                HasSymbols = true,
            },
            new BinaryReport
            {
                Path = "lib/net462/Foo.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                AssemblyName = "Foo",
                AssemblyVersion = "1.0.0.0",
                FileVersion = "1.0.0.17603",
                HasSymbols = true,
            });

        // Act.
        Validator.Validate(report);

        // Assert: the mismatch surfaces as a Warning rather than an Error.
        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.VersionInconsistency && f.Severity == Severity.Warning);
    }

    /// <summary>
    /// Verifies that the missing-symbols rule applies only to implementation assemblies: a reference
    /// assembly without symbols is exempt, so exactly one finding (for the implementation DLL) is
    /// produced.
    /// </summary>
    [Fact]
    public void Flags_missing_symbols_only_for_implementation_assemblies()
    {
        // Arrange: an implementation and a reference assembly, both lacking symbols.
        PackageReport report = Package(
            new BinaryReport
            {
                Path = "lib/net8.0/Foo.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                AssemblyName = "Foo",
                AssemblyVersion = "1.0.0.0",
                HasSymbols = false,
            },
            new BinaryReport
            {
                Path = "ref/net8.0/Foo.dll",
                Kind = BinaryKind.Reference,
                IsManagedAssembly = true,
                AssemblyName = "Foo",
                AssemblyVersion = "1.0.0.0",
                HasSymbols = false,
            });

        // Act.
        Validator.Validate(report);

        // Assert: only the implementation assembly is flagged; the reference assembly is exempt.
        List<Finding> missing = report.Findings!
            .Where(f => f.Category == Categories.MissingSymbols)
            .ToList();
        Assert.Single(missing);
        Assert.Equal("lib/net8.0/Foo.dll", missing[0].Target);
    }

    /// <summary>
    /// Verifies that a delay-signed implementation assembly (public key present but the strong-name
    /// flag unset) raises a Warning-severity delay-signed finding.
    /// </summary>
    [Fact]
    public void Flags_delay_signed_assembly()
    {
        // Arrange: an assembly that carries a public key but reports DelaySigned.
        PackageReport report = Package(new BinaryReport
        {
            Path = "lib/net8.0/Foo.dll",
            Kind = BinaryKind.Implementation,
            IsManagedAssembly = true,
            AssemblyName = "Foo",
            AssemblyVersion = "1.0.0.0",
            PublicKeyToken = "23ec7fc2d6eaa4a5",
            SigningStatus = SigningStatus.DelaySigned,
            HasSymbols = true,
        });

        // Act.
        Validator.Validate(report);

        // Assert.
        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.DelaySigned && f.Severity == Severity.Warning);
    }

    /// <summary>
    /// Verifies that a symbol-package PDB which matches by GUID but fails checksum verification raises
    /// an Error-severity checksum-mismatch finding.
    /// </summary>
    [Fact]
    public void Flags_symbol_checksum_mismatch_as_error()
    {
        // Arrange: a GUID-matched symbol whose checksum verification was evaluated and failed.
        PackageReport report = Package(new BinaryReport
        {
            Path = "lib/net8.0/Foo.dll",
            Kind = BinaryKind.Implementation,
            IsManagedAssembly = true,
            AssemblyName = "Foo",
            AssemblyVersion = "1.0.0.0",
            HasSymbols = true,
            HasSymbolPackageSymbols = true,
            SymbolPackageSymbolsMatch = true,
            SymbolPackageVerifiedByChecksum = false,
            SymbolPackageFile = "lib/net8.0/Foo.pdb",
        });

        // Act.
        Validator.Validate(report);

        // Assert.
        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.SymbolChecksumMismatch && f.Severity == Severity.Error);
    }

    /// <summary>
    /// Verifies that a package without a NuGet signature raises a package-unsigned finding.
    /// </summary>
    [Fact]
    public void Flags_unsigned_package()
    {
        // Arrange: a valid package, then re-create it as unsigned (IsSigned is init-only).
        PackageReport report = Package(new BinaryReport
        {
            Path = "lib/net8.0/Foo.dll",
            Kind = BinaryKind.Implementation,
            IsManagedAssembly = true,
            AssemblyName = "Foo",
            AssemblyVersion = "1.0.0.0",
            HasSymbols = true,
        });
        report = new PackageReport
        {
            PackageFile = report.PackageFile,
            PackageId = report.PackageId,
            PackageVersion = report.PackageVersion,
            IsSigned = false,
            Binaries = report.Binaries,
            SymbolPackage = report.SymbolPackage,
        };

        // Act.
        Validator.Validate(report);

        // Assert.
        Assert.Contains(report.Findings!, f => f.Category == Categories.PackageUnsigned);
    }

    /// <summary>
    /// Verifies the cross-package rule: a dependency on an in-batch package whose actual version
    /// falls outside the declared range raises a dependency-inconsistency finding.
    /// </summary>
    [Fact]
    public void Batch_flags_dependency_version_inconsistency()
    {
        // Arrange: a consumer requiring Lib >= 2.0.0, but the Lib in the same batch is only 1.0.0.
        var consumer = new PackageReport
        {
            PackageFile = "Consumer.1.0.0.nupkg",
            PackageId = "Consumer",
            PackageVersion = "1.0.0",
            IsSigned = true,
            Binaries = [],
            SymbolPackage = SkippedSymbols(),
            DependencyGroups =
            [
                new DependencyGroup
                {
                    TargetFramework = "net8.0",
                    Dependencies = [new DependencyInfo { Id = "Lib", VersionRange = "[2.0.0,)" }],
                },
            ],
        };
        var lib = new PackageReport
        {
            PackageFile = "Lib.1.0.0.nupkg",
            PackageId = "Lib",
            PackageVersion = "1.0.0",
            IsSigned = true,
            Binaries = [],
            SymbolPackage = SkippedSymbols(),
        };

        // Act: validate the two packages together so the cross-package rule can correlate them.
        List<Finding> findings = Validator.ValidateBatch([consumer, lib]);

        // Assert.
        Assert.Contains(findings, f => f.Category == Categories.DependencyInconsistency);
    }

    /// <summary>
    /// Verifies that a consistent, signed package with matching versions and symbols produces no
    /// Error-severity findings (the informational/clean baseline).
    /// </summary>
    [Fact]
    public void Clean_package_produces_no_error_findings()
    {
        // Arrange: a signed, strong-named assembly with symbols and consistent versions.
        PackageReport report = Package(new BinaryReport
        {
            Path = "lib/net8.0/Foo.dll",
            Kind = BinaryKind.Implementation,
            IsManagedAssembly = true,
            AssemblyName = "Foo",
            AssemblyVersion = "1.0.0.0",
            FileVersion = "1.0.0.0",
            PublicKeyToken = "23ec7fc2d6eaa4a5",
            SigningStatus = SigningStatus.Signed,
            HasSymbols = true,
        });

        // Act.
        Validator.Validate(report);

        // Assert: nothing rises to Error severity (info-level findings such as package-unsigned may
        // still exist, but none here since the package is signed).
        Assert.DoesNotContain(report.Findings ?? [], f => f.Severity == Severity.Error);
    }
}
