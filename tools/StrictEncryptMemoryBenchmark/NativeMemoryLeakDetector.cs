using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

namespace StrictEncryptMemoryBenchmark;

/// <summary>
/// Snapshot of SNI_Packet lifecycle counters exported by
/// Microsoft.Data.SqlClient.SNI.dll. Field order MUST match
/// struct SNIPacketStatsSnapshot in sni_io.hpp.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SniPacketStats
{
    public long Ctor;        // SNI_Packet ctor calls (real heap allocations)
    public long Dtor;        // SNI_Packet dtor calls (real heap frees)
    public long CachePop;    // SNIMemRegion::Pop returned non-NULL (cache hit)
    public long CachePush;   // SNIMemRegion::Push pushed onto freelist
    public long CacheReject; // SNIMemRegion::Push hit cap -> SNIPacketDelete
    public long FinalRel;    // SNIPacketRelease that hit refcount==0
    public long AddRef;      // SNIPacketAddRef calls
    public long RelTotal;    // SNIPacketRelease calls (total)
    // Write path counters — used to confirm whether the leak comes from
    // SNIWriteAsync deferred refs being orphaned by a provider's WriteDone.
    public long WriteAsyncTotal;
    public long WriteAsyncSyncReleased;
    public long WriteAsyncDeferred;
    public long WriteDoneTotal;
    public long WriteDonePacketKept;
    public long WriteDonePacketConsumed;
    // Connection lifecycle
    public long SniConnDtor;
    public long CryptoBaseDtor;
    public long SniCloseTotal;
    // Provider-chain lifecycle (added 2026-05-13 to locate why CryptoBase::~CryptoBase never runs)
    public long SniConnCtor;
    public long CryptoBaseRelease;
    public long SniRemoveProvider;
    public long ConnReleaseToZero;
    public long ConnReleaseChainWalk;
    public long TcpCloseSyncReadHeld;
    // Tcp::ReadDone abort-path tracers (added 2026-05-13 to confirm whether the ICM 792529661
    // LEAK-FIX branches actually execute). TcpReadDoneTotal>0 confirms Tcp::ReadDone runs;
    // AbortFix1/Fix2>0 confirms our two abort-path releases execute.
    public long TcpReadDoneTotal;
    public long TcpReadDoneErr;
    public long TcpReadDoneAbortFix1;
    public long TcpReadDoneAbortFix2;
    // Per-refType histogram of refs held when SNIClose is entered.
    // Size MUST match SNI_LEAK_DIAG_MAX_REF_TYPES = 16 in sni_io.hpp.
    [System.Runtime.InteropServices.MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public long[] ConnCloseRefHeld;
}

/// <summary>
/// One slot of the AddRef caller histogram. <see cref="RvaOrZero"/> is the
/// caller's return address relative to the SNI module base. Field order
/// MUST match struct SNIAddRefCallerSlot in sni_io.hpp.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SniAddRefCallerSlot
{
    public long RvaOrZero;
    public long Hits;
}

file static class SniDiag
{
    private const string SniDll = "Microsoft.Data.SqlClient.SNI.dll";
    private const int MaxAddRefCallers = 16;

    [DllImport(SniDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SNIDumpPacketStats(out SniPacketStats stats);

    [DllImport(SniDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SNIResetPacketStats();

    [DllImport(SniDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SNIDumpAddRefCallers(
        [Out] SniAddRefCallerSlot[] slots,
        out int slotCount);

    public static bool TryGetStats(out SniPacketStats stats)
    {
        try
        {
            SNIDumpPacketStats(out stats);
            return true;
        }
        catch (DllNotFoundException) { stats = default; return false; }
        catch (EntryPointNotFoundException) { stats = default; return false; }
    }

    public static bool TryGetAddRefCallers(out SniAddRefCallerSlot[] slots)
    {
        slots = new SniAddRefCallerSlot[MaxAddRefCallers];
        try
        {
            SNIDumpAddRefCallers(slots, out _);
            return true;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    public static void PrintStats(SniPacketStats s, int connections)
    {
        long alive = s.Ctor - s.Dtor;
        long cached = s.CachePush - s.CachePop;
        Console.WriteLine();
        Console.WriteLine("--- SNI_Packet lifecycle counters ---");
        Console.WriteLine($"  Ctor (heap alloc) : {s.Ctor,12}    perConn={(double)s.Ctor / connections:F2}");
        Console.WriteLine($"  Dtor (heap free)  : {s.Dtor,12}");
        Console.WriteLine($"  Alive on heap     : {alive,12}    (Ctor - Dtor)");
        Console.WriteLine($"  Cache Pop (hit)   : {s.CachePop,12}");
        Console.WriteLine($"  Cache Push        : {s.CachePush,12}");
        Console.WriteLine($"  Cache currently   : {cached,12}    (Push - Pop)");
        Console.WriteLine($"  Cache Reject (cap): {s.CacheReject,12}");
        Console.WriteLine($"  Final Release(=0) : {s.FinalRel,12}");
        Console.WriteLine($"  AddRef            : {s.AddRef,12}");
        Console.WriteLine($"  Release total     : {s.RelTotal,12}");
        long stuck = alive - cached;
        Console.WriteLine($"  *** Stuck refs    : {stuck,12}    (alive - cached, refcount>0)");

        Console.WriteLine();
        Console.WriteLine("  --- SNIWriteAsync / SNIWriteDone path ---");
        Console.WriteLine($"  WriteAsync total       : {s.WriteAsyncTotal,12}    perConn={(double)s.WriteAsyncTotal / connections:F2}");
        Console.WriteLine($"  WriteAsync sync release: {s.WriteAsyncSyncReleased,12}");
        Console.WriteLine($"  WriteAsync deferred    : {s.WriteAsyncDeferred,12}    (returned IO_PENDING; release deferred to WriteDone)");
        Console.WriteLine($"  WriteDone total        : {s.WriteDoneTotal,12}    (compare to WriteAsync deferred)");
        Console.WriteLine($"  WriteDone packet kept  : {s.WriteDonePacketKept,12}    (provider chain returned packet — normal path)");
        Console.WriteLine($"  WriteDone packet consumed: {s.WriteDonePacketConsumed,12}    (provider chain NULL'd packet — SUSPECTED LEAK)");
        long unmatchedDeferred = s.WriteAsyncDeferred - s.WriteDoneTotal;
        Console.WriteLine($"  Deferred not yet done  : {unmatchedDeferred,12}    (WriteAsync deferred - WriteDone total)");

        Console.WriteLine();
        Console.WriteLine("  --- Connection lifecycle ---");
        Console.WriteLine($"  SNIClose calls         : {s.SniCloseTotal,12}    perConn={(double)s.SniCloseTotal / connections:F2}");
        Console.WriteLine($"  SNI_Conn ctors         : {s.SniConnCtor,12}    perConn={(double)s.SniConnCtor / connections:F2}    (# of SNI_Conn objects created)");
        Console.WriteLine($"  SNI_Conn destructors   : {s.SniConnDtor,12}    perConn={(double)s.SniConnDtor / connections:F2}    (lower than ctors => leaked conns)");
        Console.WriteLine($"  SNI_Conn rel->0        : {s.ConnReleaseToZero,12}    perConn={(double)s.ConnReleaseToZero / connections:F2}    (Release() reached cRef==0)");
        Console.WriteLine($"  SNI_Conn chain walk    : {s.ConnReleaseChainWalk,12}    perConn={(double)s.ConnReleaseChainWalk / connections:F2}    (of those, m_pProvHead was non-NULL)");
        Console.WriteLine($"  SNIRemoveProvider      : {s.SniRemoveProvider,12}    perConn={(double)s.SniRemoveProvider / connections:F2}    (mid-life provider detach)");
        Console.WriteLine($"  CryptoBase::Release    : {s.CryptoBaseRelease,12}    perConn={(double)s.CryptoBaseRelease / connections:F2}    (Release entries; should == CryptoBase dtor)");
        Console.WriteLine($"  CryptoBase destructors : {s.CryptoBaseDtor,12}    perConn={(double)s.CryptoBaseDtor / connections:F2}    (lower than CryptoBase::Release => unreachable delete)");
        Console.WriteLine($"  Tcp::Close w/sync read : {s.TcpCloseSyncReadHeld,12}    perConn={(double)s.TcpCloseSyncReadHeld / connections:F2}    (Tcp::Close entries with m_pSyncReadPacket non-NULL — suspected leak path)");
        Console.WriteLine($"  Tcp::ReadDone total    : {s.TcpReadDoneTotal,12}    perConn={(double)s.TcpReadDoneTotal / connections:F2}    (all entries; 0 means ReadDone is never reached)");
        Console.WriteLine($"  Tcp::ReadDone error    : {s.TcpReadDoneErr,12}    perConn={(double)s.TcpReadDoneErr / connections:F2}    (entries with dwError != 0)");
        Console.WriteLine($"  Tcp::ReadDone AbortFix1: {s.TcpReadDoneAbortFix1,12}    perConn={(double)s.TcpReadDoneAbortFix1 / connections:F2}    (LEAK-FIX m_lCloseCalled==1 branch)");
        Console.WriteLine($"  Tcp::ReadDone AbortFix2: {s.TcpReadDoneAbortFix2,12}    perConn={(double)s.TcpReadDoneAbortFix2 / connections:F2}    (LEAK-FIX !fRefAdded branch)");

        // Per-refType histogram of refs held when SNIClose was entered.
        // Index order MUST match the SNI_REF enum (REF_Active=0, REF_InternalActive=1,
        // REF_Packet=2, REF_Read=3, REF_InternalRead=4, REF_Write=5, REF_InternalWrite=6,
        // REF_ActiveCallbacks=7, REF_PacketNotOwningBuf=8). Any non-zero bucket near
        // the connection count is the one pinning the leaked SNI_Conn.
        if (s.ConnCloseRefHeld is { Length: > 0 })
        {
            string[] refNames = new[]
            {
                "REF_Active            ", "REF_InternalActive    ",
                "REF_Packet            ", "REF_Read              ",
                "REF_InternalRead      ", "REF_Write             ",
                "REF_InternalWrite     ", "REF_ActiveCallbacks   ",
                "REF_PacketNotOwningBuf",
            };
            bool anyNonZero = s.ConnCloseRefHeld.Any(v => v != 0);
            if (anyNonZero)
            {
                Console.WriteLine();
                Console.WriteLine("  --- Refs held when SNIClose entered (per-refType histogram) ---");
                for (int i = 0; i < refNames.Length && i < s.ConnCloseRefHeld.Length; i++)
                {
                    long held = s.ConnCloseRefHeld[i];
                    if (held == 0) continue;
                    Console.WriteLine($"    [{i}] {refNames[i]}: {held,8}    perConn={(double)held / connections:F2}");
                }
            }
        }

        if (TryGetAddRefCallers(out var slots))
        {
            var nonEmpty = slots.Where(slot => slot.RvaOrZero != 0)
                                 .OrderByDescending(slot => slot.Hits)
                                 .ToArray();
            if (nonEmpty.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  AddRef caller histogram (caller return-address RVA in SNI DLL):");
                foreach (var slot in nonEmpty)
                {
                    double perConn = (double)slot.Hits / connections;
                    Console.WriteLine($"    RVA 0x{slot.RvaOrZero:X8}  hits={slot.Hits,8}  perConn={perConn:F2}");
                }
            }
        }

        // LEAK-DIAG (ICM 792529661): managed-side dispose counters set by
        // TdsParserStateObjectNative.Dispose() via static fields. Read via
        // reflection because the fields are `internal`. If `disposeCount`
        // is 0, Dispose() is not being called on the physical state object
        // at all. Otherwise, the non-null counts tell us which packet field
        // is pinning the leaked SNI_Conn.
        TryPrintManagedDisposeDiagnostics(connections);

        Console.WriteLine();
    }

    private static void TryPrintManagedDisposeDiagnostics(int connections)
    {
        try
        {
            var asm = typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly;
            var t = asm.GetType("Microsoft.Data.SqlClient.TdsParserStateObjectNative");
            if (t is null)
            {
                return;
            }

            long ReadSimple(string name)
            {
                var f = t.GetField(name,
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (f is null) return -1;
                object v = f.GetValue(null);
                return v is long l ? l : -1;
            }

            long disposeCount = ReadSimple("s_disposeCount");
            if (disposeCount < 0)
            {
                return; // counters not present in this build
            }

            long sniPacket = ReadSimple("s_disposeSniPacketNonNull");
            long sniAttn = ReadSimple("s_disposeSniAsyncAttnPacketNonNull");
            long session = ReadSimple("s_disposeSessionHandleNonNull");
            long pending = ReadSimple("s_disposePendingWritePacketsCount");
            long cache = ReadSimple("s_disposeWritePacketCacheCount");

            double pc = connections > 0 ? 1.0 / connections : 0;
            Console.WriteLine();
            Console.WriteLine("  --- TdsParserStateObjectNative.Dispose() diagnostics (managed-side) ---");
            Console.WriteLine($"  Dispose() entries           : {disposeCount,12}    perConn={disposeCount * pc:F2}");
            Console.WriteLine($"  _sniPacket non-null         : {sniPacket,12}    perConn={sniPacket * pc:F2}");
            Console.WriteLine($"  _sniAsyncAttnPacket non-null: {sniAttn,12}    perConn={sniAttn * pc:F2}");
            Console.WriteLine($"  _sessionHandle non-null     : {session,12}    perConn={session * pc:F2}");
            Console.WriteLine($"  _pendingWritePackets total  : {pending,12}    perConn={pending * pc:F2}");
            Console.WriteLine($"  _writePacketCache total     : {cache,12}    perConn={cache * pc:F2}");
            if (disposeCount == 0)
            {
                Console.WriteLine($"  >>> Dispose() is NEVER called — the leak is that the physical state object's Dispose() is bypassed.");
            }
            else if (sniPacket == 0 && sniAttn == 0 && session == 0 && pending == 0 && cache == 0)
            {
                Console.WriteLine($"  >>> Dispose() runs and all packet fields are NULL at entry. The pinning packet is held elsewhere.");
            }
        }
        catch
        {
            // Best-effort diagnostic. Never fail the bench.
        }
    }
}

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
        int pauseAt = GetArgValue(args, "--pause-at", 0); // 0 = no pause
        string encryptMode = GetArgString(args, "--encrypt", "Strict");
        bool pooling = !args.Any(a => a.Equals("--no-pooling", StringComparison.OrdinalIgnoreCase));
        bool useManagedSni = args.Any(a => a.Equals("--managed-sni", StringComparison.OrdinalIgnoreCase));
        bool uniqueConnStr = args.Any(a => a.Equals("--unique-connstr", StringComparison.OrdinalIgnoreCase));
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
        Console.WriteLine($"Unique connstr:  {uniqueConnStr}");
        Console.WriteLine($"Total conns:     {totalConnections}");
        Console.WriteLine($"Batch size:      {batchSize}");
        Console.WriteLine($"Authentication:  SqlPassword");
        if (pauseAt > 0)
        {
            Console.WriteLine($"Pause at:        every {pauseAt} conns (PID {Environment.ProcessId})");
        }
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
                    // When --unique-connstr is set, vary ApplicationName per iteration
                    // so SqlClient's pool sees each connection as a distinct pool group.
                    // This isolates whether the leak is per-fresh-handshake (will leak
                    // either way) vs. per-retained-pool-entry (would only leak here when
                    // pooling is also true).
                    string iterConnStr = uniqueConnStr
                        ? $"{connectionString};Application Name=bench_{connectionsDone + i}"
                        : connectionString;

                    using var connection = new SqlConnection(iterConnStr);
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

            // Pause point for external memory snapshot tools (UMDH, VS profiler,
            // dotnet-dump, etc.). Prints PID + current connection count, then waits
            // for Enter so the operator can take a heap snapshot at a known,
            // repeatable point. Includes the final batch (== totalConnections) so a
            // snapshot can be taken at end-of-run before the process exits.
            //
            // Force a full GC + finalizer run BEFORE pausing so that surviving
            // managed objects in the snapshot are genuinely retained, not just
            // pending finalization. Without this, ~one-batch-worth of SafeHandles
            // appear "leaked" in dotnet-dump when in reality they are awaiting
            // finalizer thread execution.
            if (pauseAt > 0 && connectionsDone % pauseAt == 0)
            {
                // Allow any in-flight IOCP completions (SNIReadDone /
                // SNIWriteDone running on worker threads) to drain before
                // we measure. Without this, packets that are mid-release
                // are still alive at snapshot time and look leaked when
                // they would actually be released a few ms later.
                Thread.Sleep(2000);

                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
                GC.WaitForPendingFinalizers();

                Thread.Sleep(1000);

                // Read native SNI packet lifecycle counters and print them so the
                // operator can compare native heap activity with the dotnet-dump
                // snapshot without rebuilding the bench.
                if (!useManagedSni && SniDiag.TryGetStats(out var sniStats))
                {
                    SniDiag.PrintStats(sniStats, connectionsDone);
                }

                Console.WriteLine();
                Console.WriteLine($">>> PAUSE @ {connectionsDone} conns (PID {Environment.ProcessId}, post-GC). Take snapshot, then press Enter to continue...");
                Console.ReadLine();
            }
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

        // Print SNI diagnostic counters at end of run so they appear even when
        // --pause-at is not used. This is the canonical place to inspect whether
        // the ICM 792529661 LEAK-FIX branches in Tcp::ReadDone actually fired.
        if (!useManagedSni && SniDiag.TryGetStats(out var finalSniStats))
        {
            SniDiag.PrintStats(finalSniStats, connectionsDone);
        }

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
