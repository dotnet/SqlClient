using System.Linq;
using Xunit;

namespace PackageValidator.Tests;

public class ValidatorTests
{
    private static SymbolPackageInfo SkippedSymbols() => new()
    {
        Status = "skipped",
    };

    private static PackageReport Package(params BinaryReport[] binaries) => new()
    {
        PackageFile = "Test.1.0.0.nupkg",
        PackageId = "Test",
        PackageVersion = "1.0.0",
        IsSigned = true,
        Binaries = binaries.ToList(),
        SymbolPackage = SkippedSymbols(),
    };

    [Fact]
    public void Flags_inconsistent_assembly_versions_as_error()
    {
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

        Validator.Validate(report);

        Assert.NotNull(report.Findings);
        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.VersionInconsistency && f.Severity == Severity.Error);
    }

    [Fact]
    public void Flags_inconsistent_file_versions_as_warning()
    {
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

        Validator.Validate(report);

        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.VersionInconsistency && f.Severity == Severity.Warning);
    }

    [Fact]
    public void Flags_missing_symbols_only_for_implementation_assemblies()
    {
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

        Validator.Validate(report);

        List<Finding> missing = report.Findings!
            .Where(f => f.Category == Categories.MissingSymbols)
            .ToList();
        Assert.Single(missing);
        Assert.Equal("lib/net8.0/Foo.dll", missing[0].Target);
    }

    [Fact]
    public void Flags_delay_signed_assembly()
    {
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

        Validator.Validate(report);

        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.DelaySigned && f.Severity == Severity.Warning);
    }

    [Fact]
    public void Flags_symbol_checksum_mismatch_as_error()
    {
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

        Validator.Validate(report);

        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.SymbolChecksumMismatch && f.Severity == Severity.Error);
    }

    [Fact]
    public void Flags_unsigned_package()
    {
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

        Validator.Validate(report);

        Assert.Contains(report.Findings!, f => f.Category == Categories.PackageUnsigned);
    }

    [Fact]
    public void Batch_flags_dependency_version_inconsistency()
    {
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

        List<Finding> findings = Validator.ValidateBatch([consumer, lib]);

        Assert.Contains(findings, f => f.Category == Categories.DependencyInconsistency);
    }

    [Fact]
    public void Clean_package_produces_no_error_findings()
    {
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

        Validator.Validate(report);

        Assert.DoesNotContain(report.Findings ?? [], f => f.Severity == Severity.Error);
    }
}
