namespace Microsoft.Data.SqlClient.Analysis.AsyncRoslyn;

/// <summary>
/// Describes one target-framework / OS build configuration: the preprocessor
/// symbols Roslyn should define when parsing, and the platform file suffixes
/// that are compiled for it. This mirrors how the unified
/// <c>Microsoft.Data.SqlClient.csproj</c> selects sources per TFM/OS, so the
/// analysis honors <c>#if</c> branches and <c>.netfx/.netcore/.windows/.unix</c>
/// file suffixes the way the real build does.
/// </summary>
public sealed class BuildConfig
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> PreprocessorSymbols { get; init; }

    public required bool IsNetFramework { get; init; }

    public required bool IsWindows { get; init; }

    /// <summary>
    /// Returns the standard set of build configurations analyzed: the shipping
    /// TFMs crossed with the operating systems they target.
    /// </summary>
    public static IReadOnlyList<BuildConfig> Standard { get; } = new[]
    {
        Net("net8.0-unix", 8, isWindows: false),
        Net("net9.0-unix", 9, isWindows: false),
        Net("net8.0-windows", 8, isWindows: true),
        Net("net9.0-windows", 9, isWindows: true),
        NetFx("net462-windows"),
    };

    private static BuildConfig Net(string name, int minor, bool isWindows)
    {
        List<string> symbols = new() { "NET", "NETCOREAPP", $"NET{minor}_0" };
        // Cumulative *_OR_GREATER symbols the SDK defines for .NET Core/.NET.
        symbols.Add("NETCOREAPP3_1_OR_GREATER");
        for (int v = 5; v <= minor; v++)
        {
            symbols.Add($"NET{v}_0_OR_GREATER");
        }

        symbols.Add(isWindows ? "_WINDOWS" : "_UNIX");
        return new BuildConfig
        {
            Name = name,
            PreprocessorSymbols = symbols,
            IsNetFramework = false,
            IsWindows = isWindows,
        };
    }

    private static BuildConfig NetFx(string name)
    {
        List<string> symbols = new() { "NETFRAMEWORK", "NET462" };
        // .NET Framework targets always compile the Windows OS branch.
        symbols.Add("_WINDOWS");
        return new BuildConfig
        {
            Name = name,
            PreprocessorSymbols = symbols,
            IsNetFramework = true,
            IsWindows = true,
        };
    }

    /// <summary>
    /// Returns true when a source file participates in this configuration based
    /// on its platform suffix (e.g. <c>Foo.netfx.cs</c>, <c>Bar.unix.cs</c>).
    /// Files without a recognized suffix compile everywhere.
    /// </summary>
    public bool IncludesFile(string fileName)
    {
        string lower = fileName.ToLowerInvariant();

        if (HasSuffix(lower, "netfx") && !IsNetFramework)
        {
            return false;
        }

        if (HasSuffix(lower, "netcore") && IsNetFramework)
        {
            return false;
        }

        if (HasSuffix(lower, "windows") && !IsWindows)
        {
            return false;
        }

        if (HasSuffix(lower, "unix") && IsWindows)
        {
            return false;
        }

        return true;
    }

    private static bool HasSuffix(string lowerFileName, string token)
    {
        return lowerFileName.EndsWith($".{token}.cs", StringComparison.Ordinal);
    }
}
