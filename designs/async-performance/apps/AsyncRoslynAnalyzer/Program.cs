using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Data.SqlClient.Analysis.AsyncRoslyn;

/// <summary>
/// Roslyn (syntactic) analyzer for the Microsoft.Data.SqlClient async hot path.
///
/// Unlike a tree-sitter / regex pass, this parses each source file once per
/// shipping build configuration (TFM x OS) with the matching preprocessor
/// symbols and platform file-suffix rules, so <c>#if</c> branches and
/// <c>.netfx/.netcore/.windows/.unix</c> files are resolved the way the real
/// build resolves them. Findings are deduplicated and annotated with the set of
/// configurations in which they are active.
/// </summary>
public static class Program
{
    /// <summary>
    /// Methods whose exact call sites we want to confirm. The first three are
    /// the ones the graphify tree-sitter pass dropped (the "cross-cutting
    /// caveat"); the rest are the other anchors referenced by the quick-wins.
    /// </summary>
    private static readonly string[] s_defaultTargets =
    {
        "TryReadNetworkPacket",
        "ReadSniSyncOverAsync",
        "TryProcessDone",
        "ConsumePreLoginHandshake",
        "TryConnectParallel",
        "AuthenticateAsClient",
        "GetHostAddresses",
    };

    public static int Main(string[] args)
    {
        string? src = GetOption(args, "--src");
        string? outDir = GetOption(args, "--out");

        if (src is null || outDir is null)
        {
            Console.Error.WriteLine(
                "Usage: AsyncRoslynAnalyzer --src <sqlclient-src-root> --out <output-dir> "
                + "[--targets Name1,Name2]");
            return 2;
        }

        string srcRoot = Path.GetFullPath(src);
        if (!Directory.Exists(srcRoot))
        {
            Console.Error.WriteLine($"Source root not found: {srcRoot}");
            return 2;
        }

        string[] targets = GetOption(args, "--targets")?.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? s_defaultTargets;

        Directory.CreateDirectory(outDir);

        List<string> files = EnumerateSourceFiles(srcRoot);
        Console.WriteLine($"Scanning {files.Count} source files across "
            + $"{BuildConfig.Standard.Count} configurations...");

        // Deduplicated findings keyed by (kind|file|line|col|detail).
        Dictionary<string, Finding> merged = new(StringComparer.Ordinal);

        foreach (BuildConfig config in BuildConfig.Standard)
        {
            CSharpParseOptions parseOptions = new(
                languageVersion: LanguageVersion.Latest,
                preprocessorSymbols: config.PreprocessorSymbols);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (!config.IncludesFile(fileName))
                {
                    continue;
                }

                string relative = Relative(srcRoot, file);
                SourceText text = SourceText.From(File.ReadAllText(file));
                SyntaxNode root = CSharpSyntaxTree.ParseText(text, parseOptions, path: file)
                    .GetRoot();

                List<Finding> local = new();

                // Call sites are searched across the whole tree.
                new CallSiteWalker(targets, relative, text, local).Visit(root);

                // The remaining analyzers focus on the async hot-path files.
                if (IsFocusPath(relative))
                {
                    new SyncOverAsyncWalker(relative, text, local).Visit(root);
                    new BlockingWalker(relative, text, local).Visit(root);
                    new AllocationWalker(relative, text, local).Visit(root);
                    new ConfigureAwaitWalker(relative, text, local).Visit(root);
                }

                MergeFindings(merged, local, config.Name);
            }
        }

        List<Finding> findings = merged.Values
            .OrderBy(f => f.Kind, StringComparer.Ordinal)
            .ThenBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ToList();

        Dictionary<string, int> counts = findings
            .GroupBy(f => f.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Report report = new()
        {
            GeneratedUtc = DateTime.UtcNow.ToString("u"),
            SourceRoot = src,
            Configurations = BuildConfig.Standard.Select(c => c.Name).ToList(),
            TargetMethods = targets,
            FilesScanned = files.Count,
            Counts = counts,
            Findings = findings,
        };

        WriteJson(Path.Combine(outDir, "findings.json"), report);
        WriteMarkdown(Path.Combine(outDir, "analyzer-output.md"), report);

        Console.WriteLine($"Wrote {findings.Count} findings to {outDir}");
        foreach ((string kind, int count) in counts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {kind,-26} {count}");
        }

        return 0;
    }

    private static void MergeFindings(
        Dictionary<string, Finding> merged, List<Finding> local, string configName)
    {
        foreach (Finding finding in local)
        {
            if (merged.TryGetValue(finding.DedupeKey, out Finding? existing))
            {
                existing.Configs.Add(configName);
            }
            else
            {
                finding.Configs.Add(configName);
                merged[finding.DedupeKey] = finding;
            }
        }
    }

    /// <summary>
    /// The async hot-path files referenced by the quick-wins analysis: managed
    /// SNI plus the TDS read/state and reader/command hubs.
    /// </summary>
    private static bool IsFocusPath(string relative)
    {
        string normalized = relative.Replace('\\', '/');
        if (normalized.Contains("/ManagedSni/", StringComparison.Ordinal))
        {
            return true;
        }

        string name = Path.GetFileName(normalized);
        return name.StartsWith("TdsParser", StringComparison.Ordinal)
            || name.StartsWith("TdsParserStateObject", StringComparison.Ordinal)
            || name.StartsWith("SqlDataReader", StringComparison.Ordinal)
            || name.StartsWith("SqlCommand", StringComparison.Ordinal)
            || name.StartsWith("SqlInternalConnectionTds", StringComparison.Ordinal)
            || name.StartsWith("ValueUtilsSmi", StringComparison.Ordinal);
    }

    private static List<string> EnumerateSourceFiles(string srcRoot)
    {
        return Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p =>
            {
                string n = p.Replace('\\', '/');
                return !n.Contains("/obj/", StringComparison.Ordinal)
                    && !n.Contains("/bin/", StringComparison.Ordinal)
                    && !n.EndsWith(".g.cs", StringComparison.Ordinal)
                    && !n.EndsWith(".Designer.cs", StringComparison.Ordinal);
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private static string Relative(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void WriteJson(string path, Report report)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
    }

    private static void WriteMarkdown(string path, Report report)
    {
        StringBuilder sb = new();
        sb.AppendLine("# Async Roslyn Analyzer — raw output");
        sb.AppendLine();
        sb.AppendLine($"- Generated (UTC): {report.GeneratedUtc}");
        sb.AppendLine($"- Files scanned: {report.FilesScanned}");
        sb.AppendLine($"- Configurations: {string.Join(", ", report.Configurations)}");
        sb.AppendLine($"- Target methods: {string.Join(", ", report.TargetMethods)}");
        sb.AppendLine();

        sb.AppendLine("## Counts by analyzer");
        sb.AppendLine();
        sb.AppendLine("| Analyzer | Findings |");
        sb.AppendLine("| --- | --- |");
        foreach ((string kind, int count) in report.Counts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"| {kind} | {count} |");
        }
        sb.AppendLine();

        foreach (IGrouping<string, Finding> group in report.Findings
            .GroupBy(f => f.Kind, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Container | Async | Detail | Configs |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (Finding f in group.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line))
            {
                string configs = report.Configurations.Count == f.Configs.Count
                    ? "all"
                    : string.Join(" ", f.Configs);
                sb.AppendLine(
                    $"| {f.File} | {f.Line} | {f.Container ?? ""} | "
                    + $"{(f.ContainerIsAsync ? "Y" : "")} | {Escape(f.Detail)} | {configs} |");
            }
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|");
    }
}
