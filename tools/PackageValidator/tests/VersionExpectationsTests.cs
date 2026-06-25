using System.Linq;
using Xunit;

namespace PackageValidator.Tests;

public class VersionExpectationsTests
{
    private static SymbolPackageInfo SkippedSymbols() => new() { Status = "skipped" };

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

    [Fact]
    public void Wildcard_file_version_flags_mismatch()
    {
        VersionExpectations exp = VersionExpectations.Parse([], ["7.1.0.17604"], []);
        PackageReport ok = Family("MDS", "7.1.0", "7.1.0.17604", "7.0.0.0");
        PackageReport bad = Family("Ext", "7.1.0", "7.1.0.0", "7.0.0.0");

        Validator.Validate(ok, exp);
        Validator.Validate(bad, exp);

        Assert.DoesNotContain(ok.Findings ?? [], f => f.Category == Categories.UnexpectedFileVersion);
        Assert.Contains(bad.Findings!, f =>
            f.Category == Categories.UnexpectedFileVersion && f.Severity == Severity.Error);
    }

    [Fact]
    public void Specific_id_overrides_wildcard()
    {
        // Family expects .17604, but SqlServer is allowed its own value.
        VersionExpectations exp = VersionExpectations.Parse(
            [], ["*=7.1.0.17604", "Microsoft.SqlServer.Server=1.1.0.17604"], []);
        PackageReport sqlServer = Family("Microsoft.SqlServer.Server", "1.1.0", "1.1.0.17604", "1.0.0.0");

        Validator.Validate(sqlServer, exp);

        Assert.DoesNotContain(sqlServer.Findings ?? [], f => f.Category == Categories.UnexpectedFileVersion);
    }

    [Fact]
    public void Package_version_mismatch_is_flagged()
    {
        VersionExpectations exp = VersionExpectations.Parse(["7.1.0-preview1-pr17604"], [], []);
        PackageReport bad = Family("MDS", "7.1.0-preview1-ci17621", "7.1.0.17621", "7.0.0.0");

        Validator.Validate(bad, exp);

        Assert.Contains(bad.Findings!, f => f.Category == Categories.UnexpectedPackageVersion);
    }

    [Fact]
    public void Empty_expectations_produce_no_expected_version_findings()
    {
        VersionExpectations exp = VersionExpectations.Parse([], [], []);
        Assert.True(exp.IsEmpty);

        PackageReport p = Family("MDS", "7.1.0", "7.1.0.0", "7.0.0.0");
        Validator.Validate(p, exp);

        Assert.DoesNotContain(p.Findings ?? [], f =>
            f.Category is Categories.UnexpectedFileVersion
                or Categories.UnexpectedPackageVersion
                or Categories.UnexpectedAssemblyVersion);
    }

    [Fact]
    public void Parse_rejects_empty_value()
    {
        Assert.Throws<FormatException>(() => VersionExpectations.Parse([], ["MDS="], []));
    }
}
