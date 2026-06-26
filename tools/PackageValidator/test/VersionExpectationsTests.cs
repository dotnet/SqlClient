using System.Linq;
using Xunit;

namespace PackageValidator.Tests;

/// <summary>
/// Tests for the <c>--expect-*</c> assertions wired through <see cref="VersionExpectations"/> and
/// <see cref="Validator"/>: confirming package, file, and assembly versions against caller-supplied
/// expected values, including wildcard/per-id precedence and missing-version handling.
/// </summary>
public class VersionExpectationsTests
{
    /// <summary>
    /// Creates a symbol-package descriptor in the "skipped" state so symbol rules stay inert.
    /// </summary>
    /// <returns>A <see cref="SymbolPackageInfo"/> with status <c>"skipped"</c>.</returns>
    private static SymbolPackageInfo SkippedSymbols() => new() { Status = "skipped" };

    /// <summary>
    /// Builds a single-assembly package report with the given identity and versions, used to model a
    /// family member under expected-version assertions.
    /// </summary>
    /// <param name="id">The package id.</param>
    /// <param name="pkgVer">The package version.</param>
    /// <param name="fileVer">The contained assembly's file version.</param>
    /// <param name="asmVer">The contained assembly's assembly version.</param>
    /// <returns>A populated <see cref="PackageReport"/>.</returns>
    private static PackageReport Family(string id, string pkgVer, string fileVer, string asmVer) => new()
    {
        PackageFile = $"{id}.{pkgVer}.nupkg",
        PackageId = id,
        PackageVersion = pkgVer,
        IsSigned = true,
        Binaries =
        [
            new BinaryReport
            {
                Path = "lib/net8.0/X.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                AssemblyName = "X",
                AssemblyVersion = asmVer,
                FileVersion = fileVer,
                HasSymbols = true,
            },
        ],
        SymbolPackage = SkippedSymbols(),
    };

    /// <summary>
    /// Verifies a wildcard file-version expectation: the matching package passes while a package
    /// whose file version differs is flagged with an Error-severity finding.
    /// </summary>
    [Fact]
    public void Wildcard_file_version_flags_mismatch()
    {
        // Arrange: a wildcard expectation, one conforming package and one that does not.
        VersionExpectations exp = VersionExpectations.Parse([], ["7.1.0.17604"], []);
        PackageReport ok = Family("MDS", "7.1.0", "7.1.0.17604", "7.0.0.0");
        PackageReport bad = Family("Ext", "7.1.0", "7.1.0.0", "7.0.0.0");

        // Act.
        Validator.Validate(ok, exp);
        Validator.Validate(bad, exp);

        // Assert: only the non-conforming package raises an unexpected-file-version finding.
        Assert.DoesNotContain(ok.Findings ?? [], f => f.Category == Categories.UnexpectedFileVersion);
        Assert.Contains(bad.Findings!, f =>
            f.Category == Categories.UnexpectedFileVersion && f.Severity == Severity.Error);
    }

    /// <summary>
    /// Verifies that a per-id expectation overrides the wildcard, so a package matching its specific
    /// expected value is not flagged even though it differs from the wildcard value.
    /// </summary>
    [Fact]
    public void Specific_id_overrides_wildcard()
    {
        // Arrange: the family expects .17604, but SqlServer is allowed its own value.
        VersionExpectations exp = VersionExpectations.Parse(
            [], ["*=7.1.0.17604", "Microsoft.SqlServer.Server=1.1.0.17604"], []);
        PackageReport sqlServer = Family("Microsoft.SqlServer.Server", "1.1.0", "1.1.0.17604", "1.0.0.0");

        // Act.
        Validator.Validate(sqlServer, exp);

        // Assert: the per-id expectation is honored, so no mismatch is reported.
        Assert.DoesNotContain(sqlServer.Findings ?? [], f => f.Category == Categories.UnexpectedFileVersion);
    }

    /// <summary>
    /// Verifies that an assembly with no <c>AssemblyFileVersion</c> is flagged (reported as
    /// <c>(none)</c>) against a file-version expectation, so the assertion never silently passes when
    /// the version attribute is absent.
    /// </summary>
    [Fact]
    public void Missing_file_version_is_flagged_against_expectation()
    {
        // Arrange: an expectation plus an implementation assembly that carries no file version.
        VersionExpectations exp = VersionExpectations.Parse([], ["7.1.0.17604"], []);
        var report = new PackageReport
        {
            PackageFile = "MDS.7.1.0.nupkg",
            PackageId = "MDS",
            PackageVersion = "7.1.0",
            IsSigned = true,
            Binaries =
            [
                new BinaryReport
                {
                    Path = "lib/net8.0/X.dll",
                    Kind = BinaryKind.Implementation,
                    IsManagedAssembly = true,
                    AssemblyName = "X",
                    AssemblyVersion = "7.0.0.0",
                    FileVersion = null,
                    HasSymbols = true,
                },
            ],
            SymbolPackage = SkippedSymbols(),
        };

        // Act.
        Validator.Validate(report, exp);

        // Assert: the missing version is reported as an Error mentioning "(none)".
        Assert.Contains(report.Findings!, f =>
            f.Category == Categories.UnexpectedFileVersion
                && f.Severity == Severity.Error
                && f.Message.Contains("(none)"));
    }

    /// <summary>
    /// Verifies that a package whose version differs from the expected package version is flagged.
    /// </summary>
    [Fact]
    public void Package_version_mismatch_is_flagged()
    {
        // Arrange: expect a -pr build, but the package is a -ci build.
        VersionExpectations exp = VersionExpectations.Parse(["7.1.0-preview1-pr17604"], [], []);
        PackageReport bad = Family("MDS", "7.1.0-preview1-ci17621", "7.1.0.17621", "7.0.0.0");

        // Act.
        Validator.Validate(bad, exp);

        // Assert.
        Assert.Contains(bad.Findings!, f => f.Category == Categories.UnexpectedPackageVersion);
    }

    /// <summary>
    /// Verifies that when no expectations are supplied, the expected-version rules are inert: the
    /// expectations are empty and no unexpected-version findings are produced.
    /// </summary>
    [Fact]
    public void Empty_expectations_produce_no_expected_version_findings()
    {
        // Arrange: no expectations at all.
        VersionExpectations exp = VersionExpectations.Parse([], [], []);
        Assert.True(exp.IsEmpty);

        // Act: validate a package whose versions would mismatch were any expectation set.
        PackageReport p = Family("MDS", "7.1.0", "7.1.0.0", "7.0.0.0");
        Validator.Validate(p, exp);

        // Assert: none of the expected-version categories appear.
        Assert.DoesNotContain(p.Findings ?? [], f =>
            f.Category is Categories.UnexpectedFileVersion
                or Categories.UnexpectedPackageVersion
                or Categories.UnexpectedAssemblyVersion);
    }

    /// <summary>
    /// Verifies that an <c>id=</c> spec with an empty value is rejected at parse time rather than
    /// being interpreted as a real expectation.
    /// </summary>
    [Fact]
    public void Parse_rejects_empty_value()
    {
        Assert.Throws<FormatException>(() => VersionExpectations.Parse([], ["MDS="], []));
    }
}
