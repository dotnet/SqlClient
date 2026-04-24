using System.Diagnostics;
using System.Reflection.PortableExecutable;
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

        if (options.StressLoadAbsent)
        {
            return RunStressTest(options);
        }

        if (options.ForceX86DotnetFirst)
        {
            return RunWithForcedDotnetPathOrdering(options);
        }

        if (options.ProbeSniPaths)
        {
            return RunProbeSniPaths();
        }

        if (options.ProbeArchMatch)
        {
            return RunProbeArchMatch();
        }

        if (options.ProbeNativeLoad)
        {
            return RunProbeNativeLoad();
        }

        if (options.ProbeExtractionRace)
        {
            return RunProbeExtractionRace(options);
        }

        if (options.ProbeLazyLoad)
        {
            return RunProbeLazyLoad(options);
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
        Console.WriteLine("  Issue3148PathProbe [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --connection-string, -c <value>");
        Console.WriteLine("    SQL Server connection string for SqlClient.Open() test or stress test.");
        Console.WriteLine("    If not provided, only environment information will be displayed.");
        Console.WriteLine();
        Console.WriteLine("  --force-x86-dotnet-first");
        Console.WriteLine("    Prioritize x86 dotnet in PATH and re-launch probe in subprocess.");
        Console.WriteLine("    Useful for testing x86 runtime behavior on x64 systems.");
        Console.WriteLine("    Only works on Windows with both x86 and x64 dotnet installed.");
        Console.WriteLine();
        Console.WriteLine("  --probe-sni-paths");
        Console.WriteLine("    Enumerate every location the runtime will probe for SNI.dll and report");
        Console.WriteLine("    existence and file size. Covers scenario A (DLL not deployed).");
        Console.WriteLine("    Windows-only. No connection string required.");
        Console.WriteLine();
        Console.WriteLine("  --probe-arch-match");
        Console.WriteLine("    Read the PE header of each SNI.dll found on disk and compare its");
        Console.WriteLine("    machine type to the current process architecture.");
        Console.WriteLine("    Reports MATCH or MISMATCH for each copy. Covers scenario B (arch mismatch).");
        Console.WriteLine("    Windows-only. No connection string required.");
        Console.WriteLine();
        Console.WriteLine("  --probe-native-load");
        Console.WriteLine("    Call NativeLibrary.TryLoad() against each SNI.dll found on disk.");
        Console.WriteLine("    A file that exists but fails to load indicates a missing VC++ dependency.");
        Console.WriteLine("    Reports the Win32 error code on failure. Covers scenario C (missing deps).");
        Console.WriteLine("    Windows-only. No connection string required.");
        Console.WriteLine();
        Console.WriteLine("  --probe-extraction-race");
        Console.WriteLine("    Delete extracted SNI.dll copies under %TEMP% then call SqlConnection.Open().");
        Console.WriteLine("    Reproduces scenario D (single-file extraction race / temp cleanup).");
        Console.WriteLine("    Only meaningful when run from a single-file published exe.");
        Console.WriteLine("    Windows-only. Requires --connection-string.");
        Console.WriteLine();
        Console.WriteLine("  --probe-lazy-load");
        Console.WriteLine("    Hold the process idle, then perform the first SqlConnection.Open().");
        Console.WriteLine("    Simulates long-running services where SNI is first loaded much later.");
        Console.WriteLine("    Windows-only. Requires --connection-string.");
        Console.WriteLine();
        Console.WriteLine("  --lazy-load-delay <seconds>");
        Console.WriteLine("    Delay before first SqlConnection.Open() in --probe-lazy-load mode.");
        Console.WriteLine("    Default: 60");
        Console.WriteLine();
        Console.WriteLine("  --lazy-load-disturb-sni");
        Console.WriteLine("    Disturb SNI load environment before first Open() by renaming every");
        Console.WriteLine("    discovered Microsoft.Data.SqlClient.SNI.dll file, then restoring after");
        Console.WriteLine("    the first Open() attempt completes.");
        Console.WriteLine("    Only applies when --probe-lazy-load is used.");
        Console.WriteLine();
        Console.WriteLine("  --stress-load-absent");
        Console.WriteLine("    Load-time absent probe (child-process mode).");
        Console.WriteLine("    Proves that error 0x8007007E is elicited when SNI is absent at LoadLibrary time.");
        Console.WriteLine("    Spawns fresh child processes so SNI is loaded from scratch each time.");
        Console.WriteLine("    Parent renames the SNI DLL before each child starts to create a genuine absent-at-load window.");
        Console.WriteLine("    Requires --connection-string and only supported on Windows.");
        Console.WriteLine();
        Console.WriteLine("  --stress-duration <seconds>");
        Console.WriteLine("    Duration of stress test in seconds. Default: 30");
        Console.WriteLine("    Only applies when --stress-load-absent is used.");
        Console.WriteLine();
        Console.WriteLine("  -h, --help, /?");
        Console.WriteLine("    Display this help message.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Display environment information only");
        Console.WriteLine("  Issue3148PathProbe");
        Console.WriteLine();
        Console.WriteLine("  # Test SQL connection");
        Console.WriteLine("  Issue3148PathProbe --connection-string \"Server=localhost;User ID=sa;Password=xxx\"");
        Console.WriteLine();
        Console.WriteLine("  # Enumerate SNI search paths");
        Console.WriteLine("  Issue3148PathProbe --probe-sni-paths");
        Console.WriteLine();
        Console.WriteLine("  # Check SNI architecture matches process");
        Console.WriteLine("  Issue3148PathProbe --probe-arch-match");
        Console.WriteLine();
        Console.WriteLine("  # Check VC++ dependencies load correctly");
        Console.WriteLine("  Issue3148PathProbe --probe-native-load");
        Console.WriteLine();
        Console.WriteLine("  # Single-file extraction race (run from published exe)");
        Console.WriteLine("  Issue3148PathProbe --probe-extraction-race --connection-string \"...\"");
        Console.WriteLine();
        Console.WriteLine("  # Lazy first-load repro (delay then first Open)");
        Console.WriteLine("  Issue3148PathProbe --probe-lazy-load --lazy-load-delay 600 --connection-string \"...\"");
        Console.WriteLine();
        Console.WriteLine("  # Lazy first-load repro with intentional disturbance");
        Console.WriteLine("  Issue3148PathProbe --probe-lazy-load --lazy-load-delay 30 --lazy-load-disturb-sni --connection-string \"...\"");
        Console.WriteLine();
        Console.WriteLine("  # Load-time absent probe for 60 seconds");
        Console.WriteLine("  Issue3148PathProbe --stress-load-absent --connection-string \"...\" --stress-duration 60");
        Console.WriteLine();
        Console.WriteLine("  # Force x86 dotnet and test");
        Console.WriteLine("  Issue3148PathProbe --force-x86-dotnet-first --connection-string \"...\"");
        Console.WriteLine();
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

    // -------------------------------------------------------------------------
    // Probe A: enumerate every candidate SNI path and report existence + size
    // -------------------------------------------------------------------------
    private static int RunProbeSniPaths()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--probe-sni-paths is only supported on Windows.");
            return 2;
        }

        Console.WriteLine("Probe: SNI path survey");
        Console.WriteLine(new string('=', 22));
        Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine();

        var paths = FindSniDllPaths().ToList();

        if (paths.Count == 0)
        {
            Console.WriteLine("[ABSENT] No Microsoft.Data.SqlClient.SNI.dll found in any search location.");
            Console.WriteLine();
            Console.WriteLine("Search roots checked:");
            foreach (string root in GetSniSearchRoots())
            {
                Console.WriteLine($"  {root}  (exists={Directory.Exists(root)})");
            }
            return 1;
        }

        foreach (string dll in paths)
        {
            long size = new FileInfo(dll).Length;
            Console.WriteLine($"[FOUND] {dll}");
            Console.WriteLine($"        size={size} bytes");
        }

        Console.WriteLine();
        return 0;
    }

    // -------------------------------------------------------------------------
    // Probe B: PE header inspection — architecture match
    // -------------------------------------------------------------------------
    private static int RunProbeArchMatch()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--probe-arch-match is only supported on Windows.");
            return 2;
        }

        Console.WriteLine("Probe: SNI architecture match");
        Console.WriteLine(new string('=', 29));

        Architecture processArch = RuntimeInformation.ProcessArchitecture;
        Console.WriteLine($"Process architecture : {processArch}");
        Console.WriteLine();

        var paths = FindSniDllPaths().ToList();
        if (paths.Count == 0)
        {
            Console.WriteLine("[ABSENT] No Microsoft.Data.SqlClient.SNI.dll found — cannot check architecture.");
            return 1;
        }

        int exitCode = 0;
        foreach (string dll in paths)
        {
            Machine? machine = ReadPeMachine(dll);
            if (machine is null)
            {
                Console.WriteLine($"[ERROR ] {dll}");
                Console.WriteLine($"         Could not read PE header.");
                exitCode = 1;
                continue;
            }

            string dllArch = machine.Value switch
            {
                Machine.I386   => "X86",
                Machine.Amd64  => "X64",
                Machine.Arm    => "Arm",
                Machine.Arm64  => "Arm64",
                _              => $"unknown (0x{(ushort)machine.Value:X4})"
            };

            bool match = processArch switch
            {
                Architecture.X86   => machine.Value == Machine.I386,
                Architecture.X64   => machine.Value == Machine.Amd64,
                Architecture.Arm   => machine.Value == Machine.Arm,
                Architecture.Arm64 => machine.Value == Machine.Arm64,
                _                  => false
            };

            string verdict = match ? "[MATCH    ]" : "[MISMATCH ]";
            Console.WriteLine($"{verdict} {dll}");
            Console.WriteLine($"             DLL arch={dllArch}  process arch={processArch}");

            if (!match)
            {
                exitCode = 1;
            }
        }

        Console.WriteLine();
        return exitCode;
    }

    private static Machine? ReadPeMachine(string dllPath)
    {
        try
        {
            using FileStream fs = File.OpenRead(dllPath);
            using PEReader pe = new(fs);
            return pe.PEHeaders.CoffHeader.Machine;
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Probe C: NativeLibrary.TryLoad — surfaces missing VC++ dependencies
    // -------------------------------------------------------------------------
    private static int RunProbeNativeLoad()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--probe-native-load is only supported on Windows.");
            return 2;
        }

        Console.WriteLine("Probe: NativeLibrary.TryLoad");
        Console.WriteLine(new string('=', 27));
        Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine();

        var paths = FindSniDllPaths().ToList();
        if (paths.Count == 0)
        {
            Console.WriteLine("[ABSENT] No Microsoft.Data.SqlClient.SNI.dll found — nothing to load.");
            return 1;
        }

        int exitCode = 0;
        foreach (string dll in paths)
        {
            bool loaded = NativeLibrary.TryLoad(dll, out IntPtr handle);
            if (loaded)
            {
                Console.WriteLine($"[OK    ] {dll}");
                NativeLibrary.Free(handle);
            }
            else
            {
                // NativeLibrary.TryLoad routes through managed exception handling, so the
                // raw Win32 error is not accessible via GetLastWin32Error().  Diagnose the
                // failure using our own PE header inspection instead.
                Machine? machine = ReadPeMachine(dll);
                Architecture processArch = RuntimeInformation.ProcessArchitecture;
                bool archMismatch = machine is not null && !(processArch switch
                {
                    Architecture.X86   => machine.Value == Machine.I386,
                    Architecture.X64   => machine.Value == Machine.Amd64,
                    Architecture.Arm   => machine.Value == Machine.Arm,
                    Architecture.Arm64 => machine.Value == Machine.Arm64,
                    _                  => false
                });

                Console.WriteLine($"[FAIL  ] {dll}");
                if (archMismatch)
                {
                    string dllArch = machine!.Value switch
                    {
                        Machine.I386   => "X86",
                        Machine.Amd64  => "X64",
                        Machine.Arm    => "Arm",
                        Machine.Arm64  => "Arm64",
                        _              => $"0x{(ushort)machine.Value:X4}"
                    };
                    Console.WriteLine($"         Architecture mismatch: DLL={dllArch}, process={processArch}.");
                    Console.WriteLine($"         Windows will report ERROR_BAD_EXE_FORMAT (193 / 0xC1) to the CLR.");
                    Console.WriteLine($"         Use --probe-arch-match for a full arch survey.");
                }
                else
                {
                    Console.WriteLine($"         DLL exists and arch looks correct but load failed.");
                    Console.WriteLine($"         Likely cause: a VC++ or other import-table dependency is missing.");
                    Console.WriteLine($"         Windows returns ERROR_MOD_NOT_FOUND (126 / 0x7E) to the CLR.");
                    Console.WriteLine($"         Install the Visual C++ redistributable matching the SNI DLL version.");
                }
                exitCode = 1;
            }
        }

        Console.WriteLine();
        return exitCode;
    }

    // -------------------------------------------------------------------------
    // Probe D: single-file extraction race — delete extracted SNI then Open()
    // -------------------------------------------------------------------------
    private static int RunProbeExtractionRace(Options options)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--probe-extraction-race is only supported on Windows.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            Console.Error.WriteLine("--probe-extraction-race requires --connection-string.");
            return 2;
        }

        Console.WriteLine("Probe: single-file extraction race");
        Console.WriteLine(new string('=', 34));
        Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine();

        // Find SNI DLLs under %TEMP% only — those are single-file extraction candidates.
        string tempRoot = Path.GetTempPath();
        List<string> extracted = [];
        try
        {
            extracted = Directory.GetFiles(tempRoot, "Microsoft.Data.SqlClient.SNI.dll", SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not enumerate temp directory: {ex.Message}");
        }

        if (extracted.Count == 0)
        {
            Console.WriteLine("No extracted SNI DLL found under %TEMP%.");
            Console.WriteLine("This probe is only meaningful for single-file published apps.");
            Console.WriteLine("Publish with -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true,");
            Console.WriteLine("run the published exe once so the runtime extracts SNI to %TEMP%,");
            Console.WriteLine("then re-run this probe to delete the extracted copy before SqlConnection.Open().");
            return 2;
        }

        // Delete the extracted copies. This process has never loaded SNI, so no sharing violation.
        List<string> deleted = [];
        foreach (string dll in extracted)
        {
            try
            {
                File.Delete(dll);
                deleted.Add(dll);
                Console.WriteLine($"[DELETED] {dll}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SKIP   ] {dll} — {ex.Message}");
            }
        }

        if (deleted.Count == 0)
        {
            Console.WriteLine("Could not delete any extracted SNI DLL (files may be in use by another process).");
            return 2;
        }

        Console.WriteLine();
        Console.WriteLine($"Deleted {deleted.Count} extracted SNI DLL(s). Attempting SqlConnection.Open()...");
        Console.WriteLine("If this is a framework-dependent app the runtime will find SNI via the packs cache and succeed.");
        Console.WriteLine("Run this probe from a single-file published exe to reproduce the actual race.");
        Console.WriteLine();

        try
        {
            using SqlConnection connection = new(options.ConnectionString);
            connection.Open();
            Console.WriteLine("SqlConnection.Open() succeeded (SNI found via fallback search path).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SqlConnection.Open() failed — SNI absent at LoadLibrary time.");
            PrintExceptionTree(ex);
            return 1;
        }
    }

    // -------------------------------------------------------------------------
    // Probe E: delayed first load — long-running service simulation
    // -------------------------------------------------------------------------
    private static int RunProbeLazyLoad(Options options)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--probe-lazy-load is only supported on Windows.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            Console.Error.WriteLine("--probe-lazy-load requires --connection-string.");
            return 2;
        }

        int delaySeconds = options.LazyLoadDelaySeconds;
        if (delaySeconds < 0)
        {
            Console.Error.WriteLine("--lazy-load-delay must be >= 0.");
            return 2;
        }

        PrintEnvironmentReport();
        Console.WriteLine();
        Console.WriteLine("Probe: delayed first SNI load");
        Console.WriteLine(new string('=', 30));
        Console.WriteLine($"PID: {Environment.ProcessId}");
        Console.WriteLine($"Delay before first SqlConnection.Open(): {delaySeconds} seconds");
        Console.WriteLine("During this delay, you can simulate environment drift (temp cleanup, deploy changes, AV actions).");
        Console.WriteLine();

        DateTime start = DateTime.UtcNow;
        DateTime end = start.AddSeconds(delaySeconds);

        while (DateTime.UtcNow < end)
        {
            int remaining = (int)Math.Ceiling((end - DateTime.UtcNow).TotalSeconds);
            Console.WriteLine($"Waiting... {remaining}s remaining");
            System.Threading.Thread.Sleep(1000);
        }

        List<(string Original, string Temp)> disturbed = [];
        if (options.LazyLoadDisturbSni)
        {
            Console.WriteLine();
            Console.WriteLine("Disturbance enabled: renaming discovered SNI DLL files before first Open()...");
            disturbed = RenameAllSniDllsForLazyLoad(".lazy_disturb");
            if (disturbed.Count == 0)
            {
                Console.WriteLine("No SNI files were renamed. Disturbance may have had no effect.");
            }
            else
            {
                Console.WriteLine($"Renamed {disturbed.Count} SNI file(s). First Open() will run while they are absent.");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Delay complete. Attempting first SqlConnection.Open() now...");

        int exitCode;
        try
        {
            exitCode = RunConnectionTest(options.ConnectionString);
        }
        finally
        {
            if (disturbed.Count > 0)
            {
                RestoreSniDlls(disturbed);
                Console.WriteLine();
                Console.WriteLine($"Restored {disturbed.Count} disturbed SNI file(s).");
            }
        }

        return exitCode;
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

    private static int RunStressTest(Options options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            Console.Error.WriteLine("--stress-load-absent requires --connection-string.");
            return 2;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("--stress-load-absent is only supported on Windows.");
            return 2;
        }

        // Find the assembly to re-launch as children.
        // When running as `dotnet foo.dll`, Environment.ProcessPath is the dotnet host, not the
        // assembly. Use Assembly.Location which always points to the .dll (empty in single-file).
        string assemblyLocation = typeof(Program).Assembly.Location;
        bool isDll = !string.IsNullOrEmpty(assemblyLocation)
            && assemblyLocation.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

        string childTarget;
        string launcher;

        if (isDll)
        {
            childTarget = assemblyLocation;
            launcher = ResolveCommandFromPath(GetDotnetCommandName())
                ?? throw new InvalidOperationException("Cannot resolve dotnet executable from PATH.");
        }
        else
        {
            // Self-contained or single-file exe — the process itself is the launcher.
            childTarget = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine the current process path.");
            launcher = childTarget;
        }

        PrintEnvironmentReport();
        Console.WriteLine();
        Console.WriteLine("Starting load-time absent probe (child-process mode)...");
        Console.WriteLine($"Duration: {options.StressDurationSeconds} seconds");
        Console.WriteLine("Each iteration spawns a fresh child process so SNI loads from scratch.");
        Console.WriteLine("Parent renames SNI DLLs before each child starts; the child sees SNI absent at LoadLibrary time.");
        Console.WriteLine();

        DateTime endTime = DateTime.UtcNow.AddSeconds(options.StressDurationSeconds);
        int successCount = 0;
        int failureCount = 0;
        int renameWindows = 0;
        int iteration = 0;

        while (DateTime.UtcNow < endTime)
        {
            iteration++;

            // Rename SNI DLLs so they are temporarily unavailable when the child process starts.
            // The parent can do this because it has never loaded SNI itself.
            List<(string Original, string Temp)> renamed = RenameAllSniDlls(".stress_rename");
            if (renamed.Count > 0)
            {
                renameWindows++;
            }

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    UseShellExecute = false,
                    // Redirect child output so it doesn't flood the parent console.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (isDll)
                {
                    startInfo.FileName = launcher;
                    startInfo.ArgumentList.Add(childTarget);
                }
                else
                {
                    startInfo.FileName = launcher;
                }

                startInfo.ArgumentList.Add("--connection-string");
                startInfo.ArgumentList.Add(options.ConnectionString);

                using Process child = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Failed to start child process.");

                // Drain stdout/stderr async to prevent deadlock if the pipe buffer fills.
                var stdoutDrain = child.StandardOutput.ReadToEndAsync();
                var stderrDrain = child.StandardError.ReadToEndAsync();

                // Hold the rename window for up to 2 seconds — long enough for the child to
                // start the CLR and call LoadLibrary for SNI — then restore so that if SNI
                // is not yet loaded, the child can still pick it up from disk.
                System.Threading.Thread.Sleep(2000);
                RestoreSniDlls(renamed);
                renamed = [];

                child.WaitForExit();
                stdoutDrain.GetAwaiter().GetResult();
                stderrDrain.GetAwaiter().GetResult();

                if (child.ExitCode == 0)
                {
                    successCount++;
                    Console.WriteLine($"[OK]   iteration={iteration}");
                }
                else
                {
                    failureCount++;
                    Console.WriteLine($"[FAIL] iteration={iteration} exitCode={child.ExitCode}");
                }
            }
            finally
            {
                // Always restore — even if Process.Start or WaitForExit throws.
                RestoreSniDlls(renamed);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Stress test complete.");
        Console.WriteLine($"Child process launches: {iteration}");
        Console.WriteLine($"  Successful: {successCount}");
        Console.WriteLine($"  Failed: {failureCount}");
        Console.WriteLine($"Iterations with rename window: {renameWindows} of {iteration}");

        return failureCount > 0 ? 1 : 0;
    }

    // Returns every SNI DLL found in on-disk search locations (packs cache and temp extraction
    // directories), renamed to a temporary name. The parent process — which has never loaded
    // SqlClient — can rename these without hitting a sharing violation.
    private static List<(string Original, string Temp)> RenameAllSniDlls(string suffix)
        => RenameSniDlls(FindSniDllPaths(), suffix);

    private static List<(string Original, string Temp)> RenameAllSniDllsForLazyLoad(string suffix)
        => RenameSniDlls(FindSniDllPathsForLazyLoadDisturbance(), suffix);

    private static List<(string Original, string Temp)> RenameSniDlls(IEnumerable<string> dllPaths, string suffix)
    {
        List<(string, string)> renamed = [];

        foreach (string dll in dllPaths)
        {
            string temp = dll + suffix;
            try
            {
                File.Move(dll, temp);
                renamed.Add((dll, temp));
            }
            catch (Exception)
            {
                // File in use (loaded by another process), inaccessible, or permission-denied — skip.
            }
        }

        return renamed;
    }

    private static IEnumerable<string> FindSniDllPathsForLazyLoadDisturbance()
    {
        HashSet<string> discovered = new(StringComparer.OrdinalIgnoreCase);

        // Include the baseline roots used by other probes.
        foreach (string dll in FindSniDllPaths())
        {
            discovered.Add(dll);
        }

        // Include direct app-local candidates where framework-dependent outputs place native assets.
        string[] directCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Microsoft.Data.SqlClient.SNI.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "Microsoft.Data.SqlClient.SNI.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x86", "native", "Microsoft.Data.SqlClient.SNI.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-arm64", "native", "Microsoft.Data.SqlClient.SNI.dll")
        ];

        foreach (string candidate in directCandidates)
        {
            if (File.Exists(candidate))
            {
                discovered.Add(candidate);
            }
        }

        // Include runtime-directed native search paths and common NuGet package roots.
        foreach (string root in GetLazyLoadDisturbanceRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "Microsoft.Data.SqlClient.SNI.dll", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (string file in files)
            {
                discovered.Add(file);
            }
        }

        return discovered;
    }

    private static IEnumerable<string> GetLazyLoadDisturbanceRoots()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase)
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "runtimes")
        };

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            roots.Add(Path.Combine(userProfile, ".nuget", "packages", "microsoft.data.sqlclient.sni.runtime"));
            roots.Add(Path.Combine(userProfile, ".nuget", "packages", "microsoft.data.sqlclient.sni"));
        }

        string? nativeSearchDirs = Environment.GetEnvironmentVariable("NATIVE_DLL_SEARCH_DIRECTORIES");
        if (!string.IsNullOrWhiteSpace(nativeSearchDirs))
        {
            string[] entries = nativeSearchDirs
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string entry in entries)
            {
                roots.Add(entry);
            }
        }

        return roots;
    }

    private static void RestoreSniDlls(List<(string Original, string Temp)> renamed)
    {
        foreach ((string original, string temp) in renamed)
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Move(temp, original, overwrite: true);
                }
            }
            catch (Exception)
            {
                // Best-effort; log nothing so as not to pollute stress output.
            }
        }
    }

    private static string[] GetSniSearchRoots() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "packs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "packs"),
        Path.GetTempPath()
    ];

    private static IEnumerable<string> FindSniDllPaths()
    {
        foreach (string root in GetSniSearchRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "Microsoft.Data.SqlClient.SNI.dll", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }
        }
    }

    private sealed class Options
    {
        public string? ConnectionString { get; private init; }
        public bool ForceX86DotnetFirst { get; private init; }
        public bool ProbeSniPaths { get; private init; }
        public bool ProbeArchMatch { get; private init; }
        public bool ProbeNativeLoad { get; private init; }
        public bool ProbeExtractionRace { get; private init; }
        public bool ProbeLazyLoad { get; private init; }
        public int LazyLoadDelaySeconds { get; private init; } = 60;
        public bool LazyLoadDisturbSni { get; private init; }
        public bool StressLoadAbsent { get; private init; }
        public int StressDurationSeconds { get; private init; } = 30;

        public static Options Parse(string[] args)
        {
            string? connectionString = null;
            bool forceX86DotnetFirst = false;
            bool probeSniPaths = false;
            bool probeArchMatch = false;
            bool probeNativeLoad = false;
            bool probeExtractionRace = false;
            bool probeLazyLoad = false;
            int lazyLoadDelaySeconds = 60;
            bool lazyLoadDisturbSni = false;
            bool stressTestSni = false;
            int stressDurationSeconds = 30;

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

                    case "--probe-sni-paths":
                        probeSniPaths = true;
                        break;

                    case "--probe-arch-match":
                        probeArchMatch = true;
                        break;

                    case "--probe-native-load":
                        probeNativeLoad = true;
                        break;

                    case "--probe-extraction-race":
                        probeExtractionRace = true;
                        break;

                    case "--probe-lazy-load":
                        probeLazyLoad = true;
                        break;

                    case "--lazy-load-delay":
                        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int lazyDelay))
                        {
                            throw new ArgumentException("Invalid or missing value for --lazy-load-delay.");
                        }

                        if (lazyDelay < 0)
                        {
                            throw new ArgumentException("--lazy-load-delay must be >= 0.");
                        }

                        lazyLoadDelaySeconds = lazyDelay;
                        i++;
                        break;

                    case "--lazy-load-disturb-sni":
                        lazyLoadDisturbSni = true;
                        break;

                    case "--stress-load-absent":
                        stressTestSni = true;
                        break;

                    case "--stress-duration":
                        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int duration))
                        {
                            throw new ArgumentException("Invalid or missing value for --stress-duration.");
                        }

                        stressDurationSeconds = duration;
                        i++;
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
                ForceX86DotnetFirst = forceX86DotnetFirst,
                ProbeSniPaths = probeSniPaths,
                ProbeArchMatch = probeArchMatch,
                ProbeNativeLoad = probeNativeLoad,
                ProbeExtractionRace = probeExtractionRace,
                ProbeLazyLoad = probeLazyLoad,
                LazyLoadDelaySeconds = lazyLoadDelaySeconds,
                LazyLoadDisturbSni = lazyLoadDisturbSni,
                StressLoadAbsent = stressTestSni,
                StressDurationSeconds = stressDurationSeconds
            };
        }
    }
}
