using System.Data.Odbc;
using System.Diagnostics;
using System.Runtime.InteropServices;

// =============================================================================
// ODBC Driver Memory Benchmark
// =============================================================================
// Compares native memory behavior of ODBC Driver 17/18 for SQL Server under
// different encryption modes. This mirrors the StrictEncryptMemoryBenchmark
// for Microsoft.Data.SqlClient to determine if the SChannel TLS 1.3 session
// ticket cache leak also affects the ODBC driver.
//
// Environment variables:
//   BENCHMARK_SERVER   - SQL Server hostname (required)
//   BENCHMARK_DATABASE - Database name (default: master)
//   BENCHMARK_USER     - SQL Auth username (required)
//   BENCHMARK_PASSWORD - SQL Auth password (required)
//
// Usage:
//   dotnet run -c Release
//   dotnet run -c Release -- --connections 10000 --encrypt Strict
//   dotnet run -c Release -- --connections 5000 --encrypt Mandatory --driver "ODBC Driver 18 for SQL Server"
//   dotnet run -c Release -- --connections 5000 --encrypt Strict --no-pooling
//   dotnet run -c Release -- --connections 5000 --encrypt Strict --no-pooling --dm-no-pool
//
// --no-pooling disables ADO.NET-level pooling AND appends a unique APP= per
// iteration. However, System.Data.Odbc unconditionally sets
// SQL_ATTR_CONNECTION_POOLING = SQL_CP_ONE_PER_HENV on its global env handle,
// so the ODBC Driver Manager *still* pools connections, accumulating one
// retained HDBC per unique conn string. Each retained HDBC keeps its SChannel
// context/cert chain alive on the process heap.
//
// --dm-no-pool calls SQLSetEnvAttr(SQL_NULL_HENV, SQL_CP_OFF) BEFORE the first
// OdbcConnection is constructed. SQL_ATTR_CONNECTION_POOLING is process-wide
// and must be set on SQL_NULL_HENV before any env handle is allocated; once
// set, System.Data.Odbc's later SQLSetEnvAttr(thisEnv, SQL_CP_ONE_PER_HENV)
// becomes a no-op (per docs that attribute is invalid on existing env handles).
// Combine with --no-pooling for a genuine no-pool .NET test that matches the
// native C++ --no-pooling baseline.
// =============================================================================

string? server = Environment.GetEnvironmentVariable("BENCHMARK_SERVER");
string database = Environment.GetEnvironmentVariable("BENCHMARK_DATABASE") ?? "master";
string? user = Environment.GetEnvironmentVariable("BENCHMARK_USER");
string? password = Environment.GetEnvironmentVariable("BENCHMARK_PASSWORD");

if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Set environment variables before running:");
    Console.WriteLine("  BENCHMARK_SERVER   - SQL Server hostname");
    Console.WriteLine("  BENCHMARK_USER     - SQL Auth username");
    Console.WriteLine("  BENCHMARK_PASSWORD - SQL Auth password");
    Console.WriteLine();
    Console.WriteLine("Optional:");
    Console.WriteLine("  BENCHMARK_DATABASE - Database (default: master)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -c Release");
    Console.WriteLine("  dotnet run -c Release -- --connections 10000 --encrypt Strict");
    Console.WriteLine("  dotnet run -c Release -- --encrypt Mandatory --driver \"ODBC Driver 18 for SQL Server\"");
    Console.WriteLine("  dotnet run -c Release -- --encrypt Strict --no-pooling");
    Console.WriteLine("  dotnet run -c Release -- --encrypt Strict --no-pooling --dm-no-pool");
    return;
}

int totalConnections = GetArgValue(args, "--connections", 5000);
int batchSize = GetArgValue(args, "--batch", 100);
string encryptMode = GetArgString(args, "--encrypt", "Strict");
string driver = GetArgString(args, "--driver", "ODBC Driver 18 for SQL Server");
bool pooling = !args.Any(a => a.Equals("--no-pooling", StringComparison.OrdinalIgnoreCase));
bool dmNoPool = args.Any(a => a.Equals("--dm-no-pool", StringComparison.OrdinalIgnoreCase));
string? csvPath = GetArgString(args, "--csv", null);

// ─── Preempt System.Data.Odbc's hardcoded DM pooling (if requested) ───
// OdbcEnvironmentHandle ctor in dotnet/runtime calls:
//     SQLSetEnvAttr(this, SQL_ATTR_CONNECTION_POOLING, SQL_CP_ONE_PER_HENV, ...)
// with no opt-out. SQL_ATTR_CONNECTION_POOLING is process-wide and must be set
// on SQL_NULL_HENV BEFORE allocating any env handle. So set SQL_CP_OFF here,
// before the first OdbcConnection is constructed. The DM remembers our value;
// System.Data.Odbc's later call on the existing env handle is a no-op per docs.
if (dmNoPool)
{
    short rc = NativeOdbcInterop.SQLSetEnvAttr(
        IntPtr.Zero,
        NativeOdbcInterop.SQL_ATTR_CONNECTION_POOLING,
        new IntPtr(NativeOdbcInterop.SQL_CP_OFF),
        NativeOdbcInterop.SQL_IS_INTEGER);
    Console.WriteLine($"[INFO] SQLSetEnvAttr(SQL_NULL_HENV, SQL_CP_OFF) returned {rc} (0=SUCCESS, 1=SUCCESS_WITH_INFO)");
    if (rc != 0 && rc != 1)
    {
        Console.WriteLine("[WARN] DM pooling override may not have taken effect; continuing anyway.");
    }
}

// Build ODBC connection string
// ODBC Driver 18 keywords:
//   Encrypt = Strict | Mandatory | Optional | yes | no
//   TrustServerCertificate = yes | no
//   Connection Pooling = yes | no  (OLE DB style; ODBC uses driver manager pooling)
//
// For ODBC, pooling is controlled at the driver-manager level.
// To disable it, we set "Connection Pooling" and also use unique connection strings.

string connectionString = BuildConnectionString(driver, server, database, user, password, encryptMode, pooling);

Console.WriteLine("=== ODBC Driver — SChannel TLS Session Cache Leak Detector ===");
Console.WriteLine($"Driver:          {driver}");
Console.WriteLine($"Server:          {server}");
Console.WriteLine($"Database:        {database}");
Console.WriteLine($"User:            {user}");
Console.WriteLine($"Encrypt:         {encryptMode}");
Console.WriteLine($"Pooling:         {pooling}");
Console.WriteLine($"DM pool off:     {dmNoPool}");
Console.WriteLine($"Total conns:     {totalConnections}");
Console.WriteLine($"Batch size:      {batchSize}");
Console.WriteLine();
Console.WriteLine($"Connection string (sanitized):");
Console.WriteLine($"  {connectionString.Replace(password, "****")}");
Console.WriteLine();

// Validate connectivity
try
{
    using var testConn = new OdbcConnection(connectionString);
    testConn.Open();

    // Print actual driver version and server info
    using var cmd = testConn.CreateCommand();
    cmd.CommandText = "SELECT @@VERSION";
    string? serverVersion = cmd.ExecuteScalar()?.ToString();
    Console.WriteLine($"[OK] Connected. Server: {serverVersion?.Split('\n').FirstOrDefault()?.Trim()}");

    // Query encryption, TDS version, and connection info from DMVs
    using var connInfoCmd = testConn.CreateCommand();
    connInfoCmd.CommandText = @"
        SELECT c.encrypt_option, c.net_transport, c.protocol_version,
               s.client_interface_name
        FROM sys.dm_exec_connections c
        JOIN sys.dm_exec_sessions s ON c.session_id = s.session_id
        WHERE c.session_id = @@SPID";
    using var reader = connInfoCmd.ExecuteReader();
    if (reader.Read())
    {
        int protocolVersion = reader.GetInt32(reader.GetOrdinal("protocol_version"));
        string tdsDisplay = FormatTdsVersion(protocolVersion);
        Console.WriteLine($"[OK] Encryption: {reader["encrypt_option"]}, " +
                          $"Transport: {reader["net_transport"]}, " +
                          $"TDS: {tdsDisplay} (0x{protocolVersion:X8}), " +
                          $"Client: {reader["client_interface_name"]}");
    }

    // Try to get TLS version from the SQL Server error log
    // Entries look like: "A TLS 1.3 handshake ..." or "TLS 1.2 handshake ..."
    string tlsVersion = "unknown";
    try
    {
        using var tlsCmd = testConn.CreateCommand();
        // Look for the most recent TLS handshake log entry for our connection
        // xp_readerrorlog: log# 0 = current, type 1 = SQL error log
        tlsCmd.CommandText = @"
            CREATE TABLE #tls_log (LogDate DATETIME, ProcessInfo NVARCHAR(50), Text NVARCHAR(MAX));
            INSERT INTO #tls_log EXEC xp_readerrorlog 0, 1, N'TLS';
            SELECT TOP 1 Text FROM #tls_log ORDER BY LogDate DESC;
            DROP TABLE #tls_log;";
        object? tlsResult = tlsCmd.ExecuteScalar();
        if (tlsResult is string tlsText)
        {
            // Extract TLS version from text like "TLS 1.2 handshake" or "TLS 1.3"
            if (tlsText.Contains("TLS 1.3", StringComparison.OrdinalIgnoreCase))
                tlsVersion = "TLS 1.3";
            else if (tlsText.Contains("TLS 1.2", StringComparison.OrdinalIgnoreCase))
                tlsVersion = "TLS 1.2";
            else
                tlsVersion = $"see log: {tlsText[..Math.Min(80, tlsText.Length)]}";
        }
    }
    catch { /* requires sysadmin, may not be available */ }

    // Fallback: query sys.dm_tls_connections (available in very recent SQL builds)
    if (tlsVersion == "unknown")
    {
        try
        {
            using var tlsFallbackCmd = testConn.CreateCommand();
            tlsFallbackCmd.CommandText = @"
                SELECT tls_version
                FROM sys.dm_exec_connections
                WHERE session_id = @@SPID";
            object? result = tlsFallbackCmd.ExecuteScalar();
            if (result is string v && !string.IsNullOrEmpty(v))
                tlsVersion = v;
        }
        catch { /* column doesn't exist on this build */ }
    }

    Console.WriteLine($"[OK] TLS Version: {tlsVersion}");
}
catch (Exception ex)
{
    Console.WriteLine($"[FATAL] Cannot connect: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Troubleshooting:");
    Console.WriteLine("  1. Ensure the ODBC driver is installed:");
    Console.WriteLine("     https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server");
    Console.WriteLine("  2. List installed drivers: odbcad32.exe or 'Get-OdbcDriver' in PowerShell");
    Console.WriteLine($"  3. Verify the driver name matches: --driver \"{driver}\"");
    Console.WriteLine("  4. For Encrypt=Strict, ensure SQL Server 2022+ with TDS 8.0 support");
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
        csvWriter.WriteLine("Timestamp,Driver,EncryptMode,Pooling,ConnectionsDone,PrivateMB,DeltaPrivateMB,WorkingSetMB,ManagedMB,DeltaManagedMB,PerConnKB,BatchMs");
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
            // ODBC Driver Manager pools by exact connection string match.
            // To force truly new connections, append a unique value to each
            // connection string so the DM never finds a pool match.
            string connStr = pooling
                ? connectionString
                : $"{connectionString};APP=bench_{connectionsDone + i}";

            using var connection = new OdbcConnection(connStr);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
        }
        catch (OdbcException)
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
        $"{driver}," +
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
Console.WriteLine($"Driver:                {driver}");
Console.WriteLine($"Encrypt mode:          {encryptMode}");
Console.WriteLine($"Pooling:               {pooling}");
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

if (errors > 0)
{
    Console.WriteLine();
    Console.WriteLine($"⚠ {errors} connection errors occurred.");
}

double perConnFinalKb = (finalPrivate - baselinePrivateBytes) / (1024.0 * connectionsDone);
Console.WriteLine();
if (perConnFinalKb > 20)
{
    Console.WriteLine($"⚠ LEAK DETECTED: ~{perConnFinalKb:F1} KB per connection native growth.");
    Console.WriteLine("  This is consistent with SChannel TLS 1.3 session ticket caching.");
}
else if (perConnFinalKb > 5)
{
    Console.WriteLine($"⚡ MODERATE GROWTH: ~{perConnFinalKb:F1} KB per connection. May be caching, monitor with more connections.");
}
else
{
    Console.WriteLine($"✓ MINIMAL GROWTH: ~{perConnFinalKb:F1} KB per connection. No significant native memory leak detected.");
}

csvWriter?.Flush();
csvWriter?.Dispose();


// ─── Helper methods ───

static string FormatTdsVersion(int protocolVersion)
{
    // TDS protocol_version encoding in sys.dm_exec_connections:
    //   0x70000000 = TDS 7.0
    //   0x71000001 = TDS 7.1
    //   0x72090002 = TDS 7.2
    //   0x730B0003 = TDS 7.3
    //   0x74000004 = TDS 7.4
    //   0x08000000 = TDS 8.0 (Strict / ALPN)
    int firstByte = (protocolVersion >> 24) & 0xFF;
    return firstByte switch
    {
        0x08 => "8.0",
        0x74 => "7.4",
        0x73 => "7.3",
        0x72 => "7.2",
        0x71 => "7.1",
        0x70 => "7.0",
        _ => $"{firstByte}.?"
    };
}

static string BuildConnectionString(string driver, string server, string database,
    string user, string password, string encryptMode, bool pooling)
{
    // ODBC Driver 18 for SQL Server connection string keywords:
    // https://learn.microsoft.com/sql/connect/odbc/dsn-connection-string-attribute

    var parts = new List<string>
    {
        $"Driver={{{driver}}}",
        $"Server={server}",
        $"Database={database}",
        $"UID={user}",
        $"PWD={password}",
    };

    // Encryption settings
    switch (encryptMode.ToLowerInvariant())
    {
        case "strict":
            // ODBC Driver 18: Encrypt=Strict enables TDS 8.0
            parts.Add("Encrypt=Strict");
            break;
        case "mandatory":
        case "yes":
            parts.Add("Encrypt=Mandatory");
            parts.Add("TrustServerCertificate=yes");
            break;
        case "optional":
        case "no":
            parts.Add("Encrypt=Optional");
            break;
        default:
            parts.Add($"Encrypt={encryptMode}");
            break;
    }

    // Connection timeout
    parts.Add("Connection Timeout=30");

    // Note: ODBC Driver Manager pooling is defeated when --no-pooling is used
    // by appending a unique APP= value per connection at the call site.

    return string.Join(";", parts);
}

static int GetArgValue(string[] args, string name, int defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out int val))
        {
            return val;
        }
    }
    return defaultValue;
}

static string GetArgString(string[] args, string name, string? defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return defaultValue ?? "";
}

// P/Invoke shims so we can override ODBC Driver Manager pooling BEFORE
// System.Data.Odbc allocates its global env handle (which hardcodes
// SQL_CP_ONE_PER_HENV with no opt-out).
internal static class NativeOdbcInterop
{
    public const int SQL_ATTR_CONNECTION_POOLING = 201;
    public const int SQL_CP_OFF                  = 0;
    public const int SQL_CP_ONE_PER_DRIVER       = 1;
    public const int SQL_CP_ONE_PER_HENV         = 2;
    public const int SQL_IS_INTEGER              = -6;

    [DllImport("odbc32.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern short SQLSetEnvAttr(
        IntPtr EnvironmentHandle,
        int    Attribute,
        IntPtr Value,
        int    StringLength);
}
