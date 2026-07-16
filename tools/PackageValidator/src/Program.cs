// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Text.Json;

namespace PackageValidator;

/// <summary>
/// Console entry point for the PackageValidator tool, which inspects one or more NuGet packages
/// (<c>.nupkg</c>), reports version, signing, and symbol information for every binary they contain,
/// and applies a set of intrinsic validation rules that callers can use to gate pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Assemblies are inspected purely through metadata without loading them into the runtime. This
/// keeps the tool cross-platform, avoids executing any module initializers, and lets it read
/// assemblies built for target frameworks the tool itself does not run on (for example, .NET
/// Framework assemblies inspected from a .NET host).
/// </para>
/// <para>
/// To exercise the symbol-package (<c>.snupkg</c>) code path with real packages from nuget.org,
/// note that the flat-container feed only serves <c>.nupkg</c> files. The matching symbol package
/// must be downloaded from the gallery's symbol-package endpoint and saved beside the
/// <c>.nupkg</c> with the same base name:
/// <code>
/// # main package
/// https://api.nuget.org/v3-flatcontainer/{idLower}/{version}/{idLower}.{version}.nupkg
/// # sibling symbol package (note: original-cased id, no .snupkg suffix in the URL)
/// https://www.nuget.org/api/v2/symbolpackage/{Id}/{Version}
/// </code>
/// For example, the symbol package for <c>Microsoft.Data.SqlClient</c> 5.2.2 is at
/// <c>https://www.nuget.org/api/v2/symbolpackage/Microsoft.Data.SqlClient/5.2.2</c>; save it as
/// <c>microsoft.data.sqlclient.5.2.2.snupkg</c> next to the <c>.nupkg</c> so the tool finds it.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Parses command-line arguments and dispatches to the inspection and validation logic.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A process exit code: <c>0</c> on success with no gating findings, <c>1</c> on a
    /// runtime/inspection failure, <c>2</c> when the <c>--fail-on</c> gate is triggered, and a
    /// non-zero System.CommandLine code for argument parsing errors.
    /// </returns>
    private static int Main(string[] args)
    {
        // One or more paths, each a .nupkg file or a directory to scan for .nupkg files.
        var pathsArgument = new Argument<string[]>("paths")
        {
            Description = "One or more .nupkg files or directories to scan for .nupkg files.",
            Arity = ArgumentArity.OneOrMore,
        };

        // Opt-in switch that selects machine-readable JSON over the default human-readable layout.
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit machine-readable JSON instead of human-readable text.",
        };

        // Switch that suppresses looking for and processing sibling .snupkg symbol packages.
        var noSnupkgOption = new Option<bool>("--no-snupkg")
        {
            Description = "Do not look for or process sibling .snupkg symbol packages.",
        };

        // Repeatable gate: fail the run when a finding matches the given severity or category.
        var failOnOption = new Option<string[]>("--fail-on")
        {
            Description = "Exit non-zero when a finding matches a severity or category (see Notes).",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };

        // Repeatable expected-version assertions, each an optional 'id=' prefix plus a value.
        var expectPackageVersionOption = new Option<string[]>("--expect-package-version")
        {
            Description = "Confirm the package version (VALUE or id=VALUE; see Notes).",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };
        var expectFileVersionOption = new Option<string[]>("--expect-file-version")
        {
            Description = "Confirm each assembly's file version (VALUE or id=VALUE; see Notes).",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };
        var expectAssemblyVersionOption = new Option<string[]>("--expect-assembly-version")
        {
            Description = "Confirm each assembly's assembly version (VALUE or id=VALUE; see Notes).",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };

        var rootCommand = new RootCommand(
            "Inspect and validate NuGet packages: report versions, signing, and symbols, and apply " +
            "intrinsic validation rules.")
        {
            pathsArgument,
            jsonOption,
            noSnupkgOption,
            failOnOption,
            expectPackageVersionOption,
            expectFileVersionOption,
            expectAssemblyVersionOption,
        };

        // Append a free-form Notes section to the default --help output.
        ConfigureHelp(rootCommand);

        rootCommand.SetAction(parseResult =>
        {
            string[] paths = parseResult.GetValue(pathsArgument) ?? [];
            bool json = parseResult.GetValue(jsonOption);
            bool noSnupkg = parseResult.GetValue(noSnupkgOption);
            string[] failOn = parseResult.GetValue(failOnOption) ?? [];
            string[] expectPackage = parseResult.GetValue(expectPackageVersionOption) ?? [];
            string[] expectFile = parseResult.GetValue(expectFileVersionOption) ?? [];
            string[] expectAssembly = parseResult.GetValue(expectAssemblyVersionOption) ?? [];

            VersionExpectations expectations;
            try
            {
                expectations = VersionExpectations.Parse(expectPackage, expectFile, expectAssembly);
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            return Run(paths, json, !noSnupkg, failOn, expectations);
        });

        return rootCommand.Parse(args).Invoke();
    }

    /// <summary>
    /// Appends a free-form "Notes" section to the default <c>--help</c> output, documenting the
    /// accepted <c>--fail-on</c> values and the <c>VALUE</c>/<c>id=VALUE</c> form of the
    /// <c>--expect-*</c> options without crowding their one-line descriptions.
    /// </summary>
    /// <param name="rootCommand">The root command whose help is being customized.</param>
    private static void ConfigureHelp(RootCommand rootCommand)
    {
        // HelpBuilder/HelpContext are not public in this System.CommandLine version, so the free-form
        // section is appended by wrapping the help action: render the default help, then the Notes.
        foreach (Option option in rootCommand.Options)
        {
            if (option is HelpOption helpOption
                && helpOption.Action is SynchronousCommandLineAction inner)
            {
                helpOption.Action = new NotesHelpAction(inner);
            }
        }
    }

    /// <summary>
    /// A help action that renders the default help and then writes a free-form Notes section.
    /// </summary>
    private sealed class NotesHelpAction(SynchronousCommandLineAction inner) : SynchronousCommandLineAction
    {
        /// <inheritdoc />
        public override int Invoke(ParseResult parseResult)
        {
            int result = inner.Invoke(parseResult);
            Console.Out.WriteLine();
            WriteNotes(Console.Out);
            return result;
        }
    }

    /// <summary>
    /// Writes the free-form Notes help section.
    /// </summary>
    /// <param name="w">The output writer to write the section to.</param>
    private static void WriteNotes(TextWriter w)
    {
        w.WriteLine("Notes:");
        w.WriteLine();
        w.WriteLine("  --fail-on accepts a severity, the token 'any', or a finding category.");
        w.WriteLine("  A finding trips the gate when its severity name or its category matches a");
        w.WriteLine("  supplied token; 'any' matches every finding. Exit code is 2 when tripped.");
        w.WriteLine("    severities:  error  warning  info");
        w.WriteLine("    special:     any");
        w.WriteLine("    categories:");
        foreach (string category in Categories.All)
        {
            w.WriteLine($"                 {category}");
        }
        w.WriteLine();
        w.WriteLine("  --expect-package-version / --expect-file-version / --expect-assembly-version");
        w.WriteLine("  confirm versions against values the caller already computed. Each value is:");
        w.WriteLine("    VALUE        apply the expectation to every package in the run");
        w.WriteLine("    id=VALUE     apply only to the package whose id is 'id'");
        w.WriteLine("  A specific id overrides a bare VALUE (wildcard), and each option may be");
        w.WriteLine("  repeated, so a family-wide expectation can coexist with a per-package override:");
        w.WriteLine("    --expect-file-version *=7.1.0.17604 \\");
        w.WriteLine("    --expect-file-version Microsoft.SqlServer.Server=1.1.0.17604");
        w.WriteLine("  Mismatches are reported as unexpected-package-version, unexpected-file-version,");
        w.WriteLine("  or unexpected-assembly-version findings (Error severity).");
    }

    /// <summary>
    /// Inspects and validates the resolved packages and renders the result.
    /// </summary>
    /// <param name="paths">The file and directory paths supplied on the command line.</param>
    /// <param name="json">Whether to emit JSON instead of human-readable text.</param>
    /// <param name="processSnupkg">Whether to process sibling symbol packages.</param>
    /// <param name="failOn">The severity/category tokens that trigger a non-zero gate exit.</param>
    /// <param name="expectations">The caller-supplied expected version values to confirm.</param>
    /// <returns>The process exit code.</returns>
    private static int Run(
        string[] paths, bool json, bool processSnupkg, string[] failOn, VersionExpectations expectations)
    {
        List<string> packages;
        try
        {
            packages = ResolvePackages(paths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        if (packages.Count == 0)
        {
            Console.Error.WriteLine("Error: no .nupkg files found at the given path(s).");
            return 1;
        }

        var reports = new List<PackageReport>();
        try
        {
            foreach (string package in packages)
            {
                PackageReport report = PackageInspector.Inspect(package, processSnupkg);
                Validator.Validate(report, expectations);
                reports.Add(report);
            }
        }
        catch (Exception ex)
        {
            // Surface a concise message rather than a stack trace; the tool is meant to be consumed
            // by humans and scripts, not debugged by its callers.
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        List<Finding> crossPackage = Validator.ValidateBatch(reports);
        ValidationRun run = BuildRun(reports, crossPackage, failOn);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(run, JsonContext.SerializerOptions));
        }
        else
        {
            HumanReporter.Print(run);
        }

        return run.Summary.Failed ? 2 : 0;
    }

    /// <summary>
    /// Assembles a <see cref="ValidationRun"/> from inspected packages, computing summary counts and
    /// evaluating the <c>--fail-on</c> gate.
    /// </summary>
    /// <param name="reports">The per-package reports.</param>
    /// <param name="crossPackage">The cross-package findings.</param>
    /// <param name="failOn">The severity/category tokens that trigger a non-zero gate exit.</param>
    /// <returns>The assembled run.</returns>
    private static ValidationRun BuildRun(
        List<PackageReport> reports, List<Finding> crossPackage, string[] failOn)
    {
        IEnumerable<Finding> allFindings = reports
            .SelectMany(r => r.Findings ?? Enumerable.Empty<Finding>())
            .Concat(crossPackage);

        int errors = 0, warnings = 0, infos = 0;
        bool failed = false;
        var gate = new HashSet<string>(failOn.Select(f => f.Trim().ToLowerInvariant()));

        foreach (Finding finding in allFindings)
        {
            switch (finding.Severity)
            {
                case Severity.Error: errors++; break;
                case Severity.Warning: warnings++; break;
                case Severity.Info: infos++; break;
            }

            if (Matches(finding, gate))
            {
                failed = true;
            }
        }

        return new ValidationRun
        {
            Packages = reports,
            CrossPackageFindings = crossPackage.Count == 0 ? null : crossPackage,
            Summary = new ValidationSummary
            {
                PackageCount = reports.Count,
                ErrorCount = errors,
                WarningCount = warnings,
                InfoCount = infos,
                Failed = failed,
            },
        };
    }

    /// <summary>
    /// Determines whether a finding matches any of the configured <c>--fail-on</c> tokens. A token
    /// matches when it equals the finding's severity name or its category key; the special token
    /// <c>any</c> matches every finding.
    /// </summary>
    /// <param name="finding">The finding to test.</param>
    /// <param name="gate">The lowercased set of configured tokens.</param>
    /// <returns><see langword="true"/> if the finding should trip the gate.</returns>
    private static bool Matches(Finding finding, HashSet<string> gate)
    {
        if (gate.Count == 0)
        {
            return false;
        }

        return gate.Contains("any")
            || gate.Contains(finding.Severity.ToString().ToLowerInvariant())
            || gate.Contains(finding.Category);
    }

    /// <summary>
    /// Expands the supplied paths into a sorted, de-duplicated list of <c>.nupkg</c> files. Directory
    /// paths are scanned recursively; <c>.snupkg</c> files are excluded.
    /// </summary>
    /// <param name="paths">The file and directory paths supplied on the command line.</param>
    /// <returns>The resolved package file paths.</returns>
    private static List<string> ResolvePackages(string[] paths)
    {
        var resolved = new SortedSet<string>(StringComparer.Ordinal);

        // Skip directories the process cannot read rather than aborting the whole scan.
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        };

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.EnumerateFiles(path, "*.nupkg", enumeration))
                {
                    resolved.Add(Path.GetFullPath(file));
                }
            }
            else if (File.Exists(path))
            {
                if (path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                {
                    resolved.Add(Path.GetFullPath(path));
                }
                else
                {
                    Console.Error.WriteLine($"Warning: skipping non-.nupkg file: {path}");
                }
            }
            else
            {
                Console.Error.WriteLine($"Warning: path not found: {path}");
            }
        }

        return resolved.ToList();
    }
}
