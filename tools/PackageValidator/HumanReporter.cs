namespace PackageValidator;

/// <summary>
/// Renders a <see cref="ValidationRun"/> to standard output in a human-readable layout.
/// </summary>
internal static class HumanReporter
{
    /// <summary>
    /// Writes the full run: each package's details and findings, then any cross-package findings and
    /// the run summary.
    /// </summary>
    /// <param name="run">The validation run to render.</param>
    public static void Print(ValidationRun run)
    {
        for (int i = 0; i < run.Packages.Count; i++)
        {
            if (i > 0)
            {
                Console.WriteLine(new string('=', 78));
                Console.WriteLine();
            }

            PrintPackage(run.Packages[i]);
        }

        if (run.CrossPackageFindings is { Count: > 0 } cross)
        {
            Console.WriteLine(new string('=', 78));
            Console.WriteLine();
            Console.WriteLine("Cross-package findings:");
            foreach (Finding finding in cross)
            {
                Console.WriteLine($"  {FormatFinding(finding)}");
            }
            Console.WriteLine();
        }

        PrintSummary(run.Summary);
    }

    /// <summary>
    /// Writes a single package's details and findings.
    /// </summary>
    /// <param name="report">The package report to render.</param>
    private static void PrintPackage(PackageReport report)
    {
        SymbolPackageInfo sym = report.SymbolPackage;

        Console.WriteLine($"Package file:     {report.PackageFile}");
        Console.WriteLine($"NuGet package id: {report.PackageId ?? "(unknown)"}");
        Console.WriteLine($"NuGet version:    {report.PackageVersion ?? "(unknown)"}");
        Console.WriteLine($"Package signed:   {(report.IsSigned ? "yes" : "no")}");
        Console.WriteLine($"Symbol package:   {DescribeSymbolPackage(sym)}");

        PrintDependencies(report.DependencyGroups);
        Console.WriteLine();

        if (report.Binaries.Count == 0)
        {
            Console.WriteLine("No .dll files found in package.");
        }
        else
        {
            foreach (BinaryReport asm in report.Binaries)
            {
                PrintBinary(asm, sym);
            }

            PrintSymbolSummary(sym);
        }

        PrintFindings(report.Findings);
        Console.WriteLine();
    }

    /// <summary>Writes a package's declared dependencies.</summary>
    private static void PrintDependencies(List<DependencyGroup>? groups)
    {
        if (groups is not { Count: > 0 })
        {
            return;
        }

        Console.WriteLine("Dependencies:");
        foreach (DependencyGroup group in groups)
        {
            string tfm = group.TargetFramework ?? "(all frameworks)";
            Console.WriteLine($"  [{tfm}]");
            if (group.Dependencies.Count == 0)
            {
                Console.WriteLine("    (none)");
                continue;
            }
            foreach (DependencyInfo dependency in group.Dependencies)
            {
                Console.WriteLine($"    {dependency.Id} {dependency.VersionRange ?? "(any)"}");
            }
        }
    }

    /// <summary>Writes a single binary's details.</summary>
    private static void PrintBinary(BinaryReport asm, SymbolPackageInfo sym)
    {
        Console.WriteLine($"{asm.Path}  [{asm.Kind}]");

        if (!asm.IsManagedAssembly)
        {
            Console.WriteLine("  (native / unmanaged DLL - no managed metadata)");
            if (asm.NativeVersion is { } native)
            {
                Console.WriteLine($"  File version:           {native.FileVersion ?? "(none)"}");
                Console.WriteLine($"  Product version:        {native.ProductVersion ?? "(none)"}");
                Console.WriteLine($"  Product name:           {native.ProductName ?? "(none)"}");
                Console.WriteLine($"  Architecture:           {native.Architecture ?? "(unknown)"}");
            }
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"  Assembly version:       {asm.AssemblyVersion}");
        Console.WriteLine($"  File version:           {asm.FileVersion ?? "(none)"}");
        Console.WriteLine($"  Informational version:  {asm.InformationalVersion ?? "(none)"}");
        Console.WriteLine($"  Target framework:       {asm.TargetFramework ?? "(none)"}");
        Console.WriteLine($"  Public key token:       {asm.PublicKeyToken ?? "(none)"}");
        Console.WriteLine($"  Signing status:         {asm.SigningStatus?.ToString() ?? "(unknown)"}");
        Console.WriteLine($"  Strong name:            {asm.StrongName}");
        Console.WriteLine($"  Debug (PDB) id:         {asm.DebugId ?? "(none)"}");
        Console.WriteLine($"  Embedded symbols:       {FormatTriState(asm.HasEmbeddedSymbols)}");

        if (sym.Status == "present")
        {
            Console.WriteLine($"  Symbol package PDB:     {DescribeSymbolPackagePdb(asm)}");
        }

        Console.WriteLine();
    }

    /// <summary>Renders the package header's symbol-package line.</summary>
    private static string DescribeSymbolPackage(SymbolPackageInfo sym) => sym.Status switch
    {
        "present" => sym.File!,
        "missing" => "(no sibling .snupkg found beside the package)",
        "skipped" => "(symbol package processing disabled via --no-snupkg)",
        _ => sym.Status,
    };

    /// <summary>Describes whether the symbol package supplies a matching PDB for an assembly.</summary>
    private static string DescribeSymbolPackagePdb(BinaryReport asm)
    {
        if (asm.HasSymbolPackageSymbols != true)
        {
            return "none";
        }

        if (asm.SymbolPackageSymbolsMatch != true)
        {
            return $"{asm.SymbolPackageFile} (MISMATCH - symbols are from a different build)";
        }

        string checksum = asm.SymbolPackageVerifiedByChecksum switch
        {
            true => "checksum verified",
            false => "CHECKSUM MISMATCH",
            null => "GUID match, checksum not verified",
        };
        return $"{asm.SymbolPackageFile} (matches assembly; {checksum})";
    }

    /// <summary>Writes the trailing symbol-coverage summary block.</summary>
    private static void PrintSymbolSummary(SymbolPackageInfo sym)
    {
        Console.WriteLine("Symbol summary:");
        Console.WriteLine($"  Implementation assemblies have symbols: {FormatTriState(sym.AllImplementationAssembliesHaveSymbols)}");

        switch (sym.Status)
        {
            case "present":
                Console.WriteLine($"  All symbol-package symbols match:       {FormatTriState(sym.AllSymbolsMatch)}");
                if (sym.OrphanSymbolFiles is { Count: > 0 } orphans)
                {
                    Console.WriteLine("  Symbol files with no matching assembly:");
                    foreach (string orphan in orphans)
                    {
                        Console.WriteLine($"    {orphan}");
                    }
                }
                break;

            case "missing":
                Console.WriteLine("  Symbol package: none found beside the package (embedded symbols still evaluated).");
                break;

            case "skipped":
                Console.WriteLine("  Symbol package: processing disabled via --no-snupkg (embedded symbols still evaluated).");
                break;
        }

        Console.WriteLine();
    }

    /// <summary>Writes a package's findings, grouped under a heading.</summary>
    private static void PrintFindings(List<Finding>? findings)
    {
        if (findings is not { Count: > 0 })
        {
            Console.WriteLine("Findings: none");
            return;
        }

        Console.WriteLine("Findings:");
        foreach (Finding finding in findings)
        {
            Console.WriteLine($"  {FormatFinding(finding)}");
        }
    }

    /// <summary>Formats a single finding as a one-line entry.</summary>
    private static string FormatFinding(Finding finding) =>
        $"[{finding.Severity.ToString().ToUpperInvariant()}] {finding.Category}: {finding.Target} - {finding.Message}";

    /// <summary>Writes the run-level summary.</summary>
    private static void PrintSummary(ValidationSummary summary)
    {
        Console.WriteLine("Run summary:");
        Console.WriteLine($"  Packages inspected: {summary.PackageCount}");
        Console.WriteLine($"  Errors:             {summary.ErrorCount}");
        Console.WriteLine($"  Warnings:           {summary.WarningCount}");
        Console.WriteLine($"  Info:               {summary.InfoCount}");
        Console.WriteLine($"  Gate:               {(summary.Failed ? "FAILED" : "passed")}");
    }

    /// <summary>Formats a nullable boolean as <c>yes</c>/<c>no</c>/<c>n/a</c>.</summary>
    private static string FormatTriState(bool? value) =>
        value switch { true => "yes", false => "no", null => "n/a" };
}
