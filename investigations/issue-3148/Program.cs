using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

internal static class Program
{
    private const string ChildModeEnvironmentVariable = "ISSUE3148_PATH_PROBE_CHILD";

    private static int Main(string[] args)
    {
        Options options;

        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 2;
        }

        if (options.ForceX86DotnetFirst)
        {
            return RunWithForcedDotnetPathOrdering(options);
        }

        PrintEnvironmentReport();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            Console.WriteLine();
            Console.WriteLine("No connection string provided. Skipping SqlClient open test.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Attempting SqlConnection.Open()...");

        try
        {
            using SqlConnection connection = new(options.ConnectionString);
            connection.Open();
            Console.WriteLine("SqlConnection.Open() succeeded.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SqlConnection.Open() failed.");
            PrintExceptionTree(ex);
            return 1;
        }
    }

    private static void PrintEnvironmentReport()
    {
        Console.WriteLine("Issue 3148 PATH probe");
        Console.WriteLine(new string('=', 21));
        Console.WriteLine($"OS description: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Current process path: {Environment.ProcessPath ?? "<unknown>"}");
        Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
        Console.WriteLine();

        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        string separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        string[] pathEntries = (pathValue ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Console.WriteLine("PATH entries containing 'dotnet':");
        int dotnetEntryCount = 0;

        for (int index = 0; index < pathEntries.Length; index++)
        {
            if (pathEntries[index].Contains("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                dotnetEntryCount++;
                Console.WriteLine($"  [{index}] {pathEntries[index]}");
            }
        }

        if (dotnetEntryCount == 0)
        {
            Console.WriteLine("  <none>");
        }

        Console.WriteLine();
        Console.WriteLine($"Resolved dotnet executable from PATH: {ResolveCommandFromPath(GetDotnetCommandName()) ?? "<not found>"}");
        Console.WriteLine($"PATH ordering assessment: {AssessDotnetPathOrdering(pathEntries)}");
    }

    private static string GetDotnetCommandName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

    private static string? ResolveCommandFromPath(string commandName)
    {
        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        return ResolveCommandFromPath(commandName, pathValue);
    }

    private static string? ResolveCommandFromPath(string commandName, string? pathValue)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        string separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        string[] pathEntries = pathValue.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string entry in pathEntries)
        {
            try
            {
                string candidate = Path.Combine(entry, commandName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return null;
    }

    private static void PrintExceptionTree(Exception ex)
    {
        int depth = 0;
        Exception? current = ex;

        while (current is not null)
        {
            Console.WriteLine($"[{depth}] {current.GetType().FullName}: {current.Message}");
            Console.WriteLine(current.StackTrace);
            Console.WriteLine();
            current = current.InnerException;
            depth++;
        }
    }

    private static string AssessDotnetPathOrdering(string[] pathEntries)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "non-Windows environment; x86/x64 Windows PATH ordering check not applicable";
        }

        int x64Index = FindPathEntryIndex(pathEntries, "program files\\dotnet");
        int x86Index = FindPathEntryIndex(pathEntries, "program files (x86)\\dotnet");

        if (x64Index < 0 && x86Index < 0)
        {
            return "no Windows dotnet installation directories were found in PATH";
        }

        if (x64Index >= 0 && x86Index < 0)
        {
            return "only x64 dotnet PATH entry found";
        }

        if (x64Index < 0 && x86Index >= 0)
        {
            return "only x86 dotnet PATH entry found";
        }

        return x86Index < x64Index
            ? $"warning: x86 dotnet appears before x64 in PATH (x86 index {x86Index}, x64 index {x64Index})"
            : $"x64 dotnet appears before x86 in PATH (x64 index {x64Index}, x86 index {x86Index})";
    }

    private static int FindPathEntryIndex(string[] pathEntries, string match)
    {
        for (int index = 0; index < pathEntries.Length; index++)
        {
            if (pathEntries[index].Contains(match, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Issue3148PathProbe [--connection-string <value>] [--force-x86-dotnet-first]");
    }

    private static int RunWithForcedDotnetPathOrdering(Options options)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--force-x86-dotnet-first is only supported on Windows.");
            return 2;
        }

        if (string.Equals(Environment.GetEnvironmentVariable(ChildModeEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("Child re-exec already active; skipping another forced relaunch.");
            PrintEnvironmentReport();
            return RunConnectionTest(options.ConnectionString);
        }

        string? originalPath = Environment.GetEnvironmentVariable("PATH");
        string reorderedPath = BuildForcedDotnetPath(originalPath);
        string launcherPath = ResolveCommandFromPath(GetDotnetCommandName(), reorderedPath)
            ?? throw new InvalidOperationException("Unable to resolve dotnet from the reordered PATH.");

        string assemblyPath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the current process path.");
        if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--force-x86-dotnet-first requires a framework-dependent launch via 'dotnet <app>.dll'.");
            Console.Error.WriteLine("Run the probe with 'dotnet run' or execute the built DLL through dotnet on Windows.");
            return 2;
        }

        string arguments = BuildChildArguments(options);

        Console.WriteLine("Launching child process with x86 dotnet PATH ordering...");
        Console.WriteLine($"Child launcher: {launcherPath}");

        ProcessStartInfo startInfo = new()
        {
            FileName = launcherPath,
            Arguments = $"\"{assemblyPath}\"{arguments}",
            UseShellExecute = false
        };

        startInfo.Environment["PATH"] = reorderedPath;
        startInfo.Environment[ChildModeEnvironmentVariable] = "1";

        using Process child = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the child process.");
        child.WaitForExit();
        return child.ExitCode;
    }

    private static int RunConnectionTest(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine();
            Console.WriteLine("No connection string provided. Skipping SqlClient open test.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Attempting SqlConnection.Open()...");

        try
        {
            using SqlConnection connection = new(connectionString);
            connection.Open();
            Console.WriteLine("SqlConnection.Open() succeeded.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SqlConnection.Open() failed.");
            PrintExceptionTree(ex);
            return 1;
        }
    }

    private static string BuildForcedDotnetPath(string? originalPath)
    {
        string separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        List<string> entries = (originalPath ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        List<string> dotnetEntries = entries
            .Where(entry => entry.Contains("program files", StringComparison.OrdinalIgnoreCase)
                && entry.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? x86Entry = dotnetEntries.FirstOrDefault(entry => entry.Contains("program files (x86)", StringComparison.OrdinalIgnoreCase));
        string? x64Entry = dotnetEntries.FirstOrDefault(entry => entry.Contains("program files\\dotnet", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("program files/dotnet", StringComparison.OrdinalIgnoreCase));

        if (x86Entry is null || x64Entry is null)
        {
            throw new InvalidOperationException("Both x86 and x64 dotnet PATH entries are required to force the ordering.");
        }

        entries.RemoveAll(entry => string.Equals(entry, x86Entry, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry, x64Entry, StringComparison.OrdinalIgnoreCase));

        entries.Insert(0, x64Entry);
        entries.Insert(0, x86Entry);
        return string.Join(separator, entries);
    }

    private static string BuildChildArguments(Options options)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            arguments.Add("--connection-string");
            arguments.Add(options.ConnectionString);
        }

        return arguments.Count == 0
            ? string.Empty
            : " " + string.Join(" ", arguments.Select(QuoteArgument));
    }

    private static string QuoteArgument(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private sealed class Options
    {
        public string? ConnectionString { get; private init; }
        public bool ForceX86DotnetFirst { get; private init; }

        public static Options Parse(string[] args)
        {
            string? connectionString = null;
            bool forceX86DotnetFirst = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--connection-string":
                    case "-c":
                        if (i + 1 >= args.Length)
                        {
                            throw new ArgumentException("Missing value for --connection-string.");
                        }

                        connectionString = args[++i];
                        break;

                    case "--force-x86-dotnet-first":
                        forceX86DotnetFirst = true;
                        break;

                    case "--help":
                    case "-h":
                    case "/?":
                        throw new ArgumentException("Help requested.");

                    default:
                        throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }

            return new Options
            {
                ConnectionString = connectionString,
                ForceX86DotnetFirst = forceX86DotnetFirst
            };
        }
    }
}