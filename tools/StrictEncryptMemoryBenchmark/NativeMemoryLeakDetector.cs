using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace StrictEncryptMemoryBenchmark;

/// <summary>
/// Long-running native memory leak detector. This mode reproduces the ICM scenario:
/// thousands of connections opened/closed over time, tracking native memory (private bytes)
/// growth which reveals the SChannel TLS session ticket cache leak.
///
/// Unlike BenchmarkDotNet (which measures per-invocation), this tracks cumulative
/// native memory growth across many connections — exactly the pattern that causes OOM.
/// </summary>
internal static class NativeMemoryLeakDetector
{
    public static async Task RunAsync(string[] args)
    {
        int totalConnections = GetArgValue(args, "--connections", 10000);
        int batchSize = GetArgValue(args, "--batch", 100);
        string encryptMode = GetArgString(args, "--encrypt", "Strict");
        bool pooling = !args.Any(a => a.Equals("--no-pooling", StringComparison.OrdinalIgnoreCase));
        bool useManagedSni = args.Any(a => a.Equals("--managed-sni", StringComparison.OrdinalIgnoreCase));
        string? csvPath = GetArgString(args, "--csv", null);

        // Force managed SNI if requested (bypasses SChannel entirely)
        if (useManagedSni)
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
        }

        var server = Environment.GetEnvironmentVariable("BENCHMARK_SERVER")!;
        var database = Environment.GetEnvironmentVariable("BENCHMARK_DATABASE") ?? "master";
        var user = Environment.GetEnvironmentVariable("BENCHMARK_USER")!;
        var password = Environment.GetEnvironmentVariable("BENCHMARK_PASSWORD")!;
        var serverCert = Environment.GetEnvironmentVariable("BENCHMARK_SERVER_CERTIFICATE");
        var hostInCert = Environment.GetEnvironmentVariable("BENCHMARK_HOSTNAME_IN_CERTIFICATE");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            IntegratedSecurity = false,
            Encrypt = encryptMode switch
            {
                "Strict" => SqlConnectionEncryptOption.Strict,
                "Mandatory" => SqlConnectionEncryptOption.Mandatory,
                "Optional" => SqlConnectionEncryptOption.Optional,
                _ => SqlConnectionEncryptOption.Strict
            },
            TrustServerCertificate = encryptMode != "Strict", // Not supported with Strict
            Pooling = pooling,
            ConnectTimeout = 30,
            Authentication = SqlAuthenticationMethod.SqlPassword,
        };

        if (!string.IsNullOrEmpty(serverCert))
        {
            builder.ServerCertificate = serverCert;
        }

        if (!string.IsNullOrEmpty(hostInCert))
        {
            builder.HostNameInCertificate = hostInCert;
        }

        string connectionString = builder.ConnectionString;

        Console.WriteLine("=== SChannel TLS Session Cache Leak Detector ===");
        Console.WriteLine($"Microsoft.Data.SqlClient: {typeof(SqlConnection).Assembly.GetName().Version}");
        Console.WriteLine($"SNI:             {(useManagedSni ? "Managed (.NET SslStream)" : "Native (SChannel)")}");
        Console.WriteLine($"Server:          {server}");
        Console.WriteLine($"Database:        {database}");
        Console.WriteLine($"User:            {user}");
        Console.WriteLine($"Encrypt:         {encryptMode}");
        Console.WriteLine($"Pooling:         {pooling}");
        Console.WriteLine($"Total conns:     {totalConnections}");
        Console.WriteLine($"Batch size:      {batchSize}");
        Console.WriteLine($"Authentication:  SqlPassword");
        Console.WriteLine();

        // Validate connectivity
        try
        {
            using var testConn = new SqlConnection(connectionString);
            await testConn.OpenAsync();
            Console.WriteLine("[OK] Connection validated successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Cannot connect: {ex.Message}");
            return;
        }

        // Force GC and establish baseline
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);

        var process = Process.GetCurrentProcess();
        process.Refresh();

        long baselinePrivateBytes = process.PrivateMemorySize64;
        long baselineWorkingSet = process.WorkingSet64;
        long baselineManaged = GC.GetTotalMemory(forceFullCollection: true);

        Console.WriteLine();
        Console.WriteLine($"Baseline - Private: {baselinePrivateBytes / (1024.0 * 1024.0):F2} MB, " +
                          $"WorkingSet: {baselineWorkingSet / (1024.0 * 1024.0):F2} MB, " +
                          $"Managed: {baselineManaged / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine();

        // Header
        Console.WriteLine($"{"Conns",8}{"Private MB",12}{"Δ Priv MB",11}{"WS MB",10}{"Managed MB",12}{"Δ Mgd MB",10}{"Per-Conn KB",12}{"Batch ms",10}");
        Console.WriteLine(new string('─', 85));

        StreamWriter? csvWriter = null;
        if (!string.IsNullOrEmpty(csvPath))
        {
            bool writeHeader = !File.Exists(csvPath);
            csvWriter = new StreamWriter(csvPath, append: true);
            if (writeHeader)
            {
                csvWriter.WriteLine("Timestamp,MdsVersion,EncryptMode,Pooling,ConnectionsDone,PrivateMB,DeltaPrivateMB,WorkingSetMB,ManagedMB,DeltaManagedMB,PerConnKB,BatchMs");
            }
        }

        long previousPrivate = baselinePrivateBytes;
        long previousManaged = baselineManaged;
        int connectionsDone = 0;
        int errors = 0;
        var sw = new Stopwatch();

        int batches = totalConnections / batchSize;
        for (int batch = 0; batch < batches; batch++)
        {
            sw.Restart();

            for (int i = 0; i < batchSize; i++)
            {
                try
                {
                    using var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    await cmd.ExecuteScalarAsync();
                }
                catch (SqlException)
                {
                    errors++;
                }
            }

            sw.Stop();
            connectionsDone += batchSize;

            // Measure WITHOUT forcing GC to see actual memory pressure
            process.Refresh();
            long currentPrivate = process.PrivateMemorySize64;
            long currentWorkingSet = process.WorkingSet64;
            long currentManaged = GC.GetTotalMemory(forceFullCollection: false);

            long deltaPrivate = currentPrivate - previousPrivate;
            long deltaManaged = currentManaged - previousManaged;

            // Per-connection native memory growth (the SChannel leak metric)
            double perConnKb = (currentPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone);

            Console.WriteLine(
                $"{connectionsDone,8}" +
                $"{currentPrivate / (1024.0 * 1024.0),12:F2}" +
                $"{deltaPrivate / (1024.0 * 1024.0),11:F2}" +
                $"{currentWorkingSet / (1024.0 * 1024.0),10:F2}" +
                $"{currentManaged / (1024.0 * 1024.0),12:F2}" +
                $"{deltaManaged / (1024.0 * 1024.0),10:F2}" +
                $"{perConnKb,12:F2}" +
                $"{sw.ElapsedMilliseconds,10}");

            csvWriter?.WriteLine(
                $"{DateTime.UtcNow:O}," +
                $"{typeof(SqlConnection).Assembly.GetName().Version}," +
                $"{encryptMode},{pooling},{connectionsDone}," +
                $"{currentPrivate / (1024.0 * 1024.0):F2}," +
                $"{deltaPrivate / (1024.0 * 1024.0):F2}," +
                $"{currentWorkingSet / (1024.0 * 1024.0):F2}," +
                $"{currentManaged / (1024.0 * 1024.0):F2}," +
                $"{deltaManaged / (1024.0 * 1024.0):F2}," +
                $"{perConnKb:F2}," +
                $"{sw.ElapsedMilliseconds}");

            previousPrivate = currentPrivate;
            previousManaged = currentManaged;
        }

        // Final measurement with forced GC
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);

        process.Refresh();
        long finalPrivate = process.PrivateMemorySize64;
        long finalWorkingSet = process.WorkingSet64;
        long finalManaged = GC.GetTotalMemory(forceFullCollection: true);

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("SUMMARY (after forced GC)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"Total connections:     {connectionsDone}");
        Console.WriteLine($"Connection errors:     {errors}");
        Console.WriteLine();
        Console.WriteLine($"Private Bytes:         {finalPrivate / (1024.0 * 1024.0):F2} MB (baseline: {baselinePrivateBytes / (1024.0 * 1024.0):F2} MB)");
        Console.WriteLine($"Native growth:         {(finalPrivate - baselinePrivateBytes) / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine($"Per-conn native avg:   {(finalPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone):F2} KB");
        Console.WriteLine();
        Console.WriteLine($"Working Set:           {finalWorkingSet / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine($"Managed Heap:          {finalManaged / (1024.0 * 1024.0):F2} MB (baseline: {baselineManaged / (1024.0 * 1024.0):F2} MB)");
        Console.WriteLine($"Managed growth:        {(finalManaged - baselineManaged) / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine();

        double perConnNativeKb = (finalPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone);
        if (perConnNativeKb > 20) // >20 KB per connection suggests SChannel cache leak (32KB tickets)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"⚠ WARNING: Native memory grew {perConnNativeKb:F1} KB per connection.");
            Console.WriteLine($"  This is consistent with SChannel TLS session ticket cache leak (~32 KB/ticket).");
            Console.WriteLine($"  At this rate, {connectionsDone * 10} connections would consume ~{perConnNativeKb * connectionsDone * 10 / 1024.0 / 1024.0:F1} GB.");
            Console.ResetColor();
        }
        else if (perConnNativeKb > 5)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ NOTICE: Native memory grew {perConnNativeKb:F1} KB per connection. Monitor at higher scale.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Native memory growth is {perConnNativeKb:F1} KB per connection — appears normal.");
            Console.ResetColor();
        }

        csvWriter?.Dispose();

        if (!string.IsNullOrEmpty(csvPath))
        {
            Console.WriteLine($"\nResults written to: {csvPath}");
        }
    }

    private static int GetArgValue(string[] args, string name, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(args[i + 1]);
            }
        }
        return defaultValue;
    }

    private static string? GetArgString(string[] args, string name, string? defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return defaultValue;
    }
}
