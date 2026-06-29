using System.Text.Json.Serialization;

namespace Microsoft.Data.SqlClient.Analysis.AsyncRoslyn;

/// <summary>
/// A single analysis finding, deduplicated across build configurations. The
/// <see cref="Configs"/> set records every target framework / OS configuration
/// under which the finding's source location is active (i.e. not excluded by
/// <c>#if</c> directives or platform file suffixes).
/// </summary>
public sealed class Finding
{
    /// <summary>Analyzer that produced the finding (e.g. "call-site").</summary>
    public required string Kind { get; init; }

    /// <summary>Repository-relative source file path using forward slashes.</summary>
    public required string File { get; init; }

    /// <summary>1-based line number of the finding.</summary>
    public required int Line { get; init; }

    /// <summary>1-based column number of the finding.</summary>
    public required int Column { get; init; }

    /// <summary>Fully qualified "Type.Member" that contains the finding, if known.</summary>
    public string? Container { get; init; }

    /// <summary>Whether the containing member is declared <c>async</c>.</summary>
    public bool ContainerIsAsync { get; init; }

    /// <summary>Short detail describing the matched symbol or pattern.</summary>
    public required string Detail { get; init; }

    /// <summary>The single source line text, trimmed, for quick human review.</summary>
    public string? Snippet { get; init; }

    /// <summary>Build configurations under which this location is active.</summary>
    public SortedSet<string> Configs { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Deduplication key: a finding is the same fact across configs.</summary>
    [JsonIgnore]
    public string DedupeKey => $"{Kind}|{File}|{Line}|{Column}|{Detail}";
}

/// <summary>Top-level serialized report shape.</summary>
public sealed class Report
{
    public required string GeneratedUtc { get; init; }

    public required string SourceRoot { get; init; }

    public required IReadOnlyList<string> Configurations { get; init; }

    public required IReadOnlyList<string> TargetMethods { get; init; }

    public required int FilesScanned { get; init; }

    public required Dictionary<string, int> Counts { get; init; }

    public required IReadOnlyList<Finding> Findings { get; init; }
}
