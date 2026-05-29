// Licensed under the MIT license.
//
// Native repro for Microsoft.Data.SqlClient.SNI.dll memory leak (ICM 792529661).
//
// What this app does:
//   - Loads Microsoft.Data.SqlClient.SNI.dll
//   - Resolves the same exports SqlClient calls via P/Invoke
//   - Runs a tight loop of: SNIInitialize -> SNIOpenSyncEx -> SNIAddProvider(SSL_PROV) -> SNIClose
//   - Tracks Private Bytes growth per iteration
//
// Why this exists:
//   The managed bench (StrictEncryptMemoryBenchmark) shows ~52 KB/conn native leak
//   on Encrypt=Strict + TLS 1.3 + --no-pooling. The xperf heap trace points to
//   SNI_Packet allocations from Tcp::ReadSync that are never released. By calling
//   SNI APIs directly without any managed runtime in the way, we can:
//     (a) confirm the leak is intrinsic to native SNI when used SqlClient-style;
//     (b) use Application Verifier / VS Memory Diagnostics / DrMemory to point
//         the finger at the exact alloc-without-free pair without managed-side noise.
//
// Build:
//   Open SniNativeRepro.sln in Visual Studio, build Release|x64.
//   Or from a VS Developer Command Prompt:
//     msbuild SniNativeRepro.vcxproj /p:Configuration=Release /p:Platform=x64
//
// Run:
//   Copy Microsoft.Data.SqlClient.SNI.dll next to SniNativeRepro.exe (or set PATH),
//   then:
//     SniNativeRepro.exe <server> <iterations>
//   Example:
//     SniNativeRepro.exe localhost,1433 1000
//
// Run under Application Verifier (catches double-free / use-after-free in SNI.dll):
//     appverif /verify SniNativeRepro.exe /faults
//     SniNativeRepro.exe localhost 100
//
// Run under VS Memory Diagnostics:
//     Debug -> Performance Profiler -> Memory Usage -> launch SniNativeRepro.exe
//     Take a snapshot at iteration 100, another at 1000, diff.

#include <windows.h>
#include <psapi.h>
#include <schannel.h>   // for SP_PROT_TLS1_3_CLIENT etc.
#include <stdio.h>
#include <stdlib.h>
#include <string>

#pragma comment(lib, "psapi.lib")

// ============================================================================
// Native SNI types (must match struct layouts in dev-ad-strict-oom managed code:
// src/Microsoft.Data.SqlClient/src/Interop/Windows/Sni/*.cs)
// ============================================================================

// ProviderNum enum (SSL_PROV = 6 per SniNativeMethods.netcore.cs / sni.hpp)
enum Provider : int {
    HTTP_PROV    = 0,
    NP_PROV      = 1,
    SESSION_PROV = 2,
    SIGN_PROV    = 3,
    SM_PROV      = 4,
    SMUX_PROV    = 5,
    SSL_PROV     = 6,
    TCP_PROV     = 7,
    VIA_PROV     = 8,
    CTAIP_PROV   = 9,
    MAX_PROVS    = 10,
    INVALID_PROV = 11,
};

// Prefix enum (from Prefix.cs)
enum Prefix : int {
    UNKNOWN_PREFIX = 0,
    SM_PREFIX      = 1,
    TCP_PREFIX     = 2,
    NP_PREFIX      = 3,
    VIA_PREFIX     = 4,
    INVALID_PREFIX = 5,
};

// TransparentNetworkResolutionMode (use Disabled for the basic repro)
enum TransparentNetworkResolutionMode : int {
    DisabledMode = 0,
    SequentialMode = 1,
    ParallelMode = 2,
};

// SqlConnectionIPAddressPreference (managed enum)
enum SqlConnectionIPAddressPreference : int {
    IPv4First = 0,
    IPv6First = 1,
    UsePlatformDefault = 2,
};

// QueryType — only the values we need to verify TLS 1.3 was negotiated
enum QueryType : int {
    SNI_QUERY_CONN_SSL_PROTOCOL_VERSION    = 36,
};

// SniConsumerInfo — exact layout from src/Interop/Windows/Sni/SniConsumerInfo.cs
struct SniConsumerInfo {
    int     DefaultUserDataLength;
    void*   ConsumerKey;
    void*   fnReadComp;
    void*   fnWriteComp;
    void*   fnTrace;
    void*   fnAcceptComp;
    UINT32  dwNumProts;
    void*   rgListenInfo;
    void*   NodeAffinity;
};

// SniDnsCacheInfo — four wide-string pointers
struct SniDnsCacheInfo {
    LPCWSTR wszCachedFQDN;
    LPCWSTR wszCachedTcpIPv4;
    LPCWSTR wszCachedTcpIPv6;
    LPCWSTR wszCachedTcpPort;
};

// SniClientConsumerInfo — exact layout from SniClientConsumerInfo.cs.
// NOTE: the embedded SniConsumerInfo MUST come first.
struct SniClientConsumerInfo {
    SniConsumerInfo ConsumerInfo;
    LPCWSTR wszConnectionString;
    LPCWSTR HostNameInCertificate;
    int     networkLibrary;          // Prefix
    BYTE*   szSPN;
    UINT32  cchSPN;
    BYTE*   szInstanceName;
    UINT32  cchInstanceName;
    BOOL    fOverrideLastConnectCache;
    BOOL    fSynchronousConnection;
    int     timeout;
    BOOL    fParallel;
    int     transparentNetworkResolution;   // TransparentNetworkResolutionMode
    int     totalTimeout;
    BOOL    isAzureSqlServerEndpoint;
    int     ipAddressPreference;            // SqlConnectionIPAddressPreference
    SniDnsCacheInfo DNSCacheInfo;
};

// AuthProviderInfo — exact layout from AuthProviderInfo.cs.
// Field order MUST match the managed [StructLayout(LayoutKind.Sequential)] decl.
struct AuthProviderInfo {
    UINT32  flags;
    BOOL    tlsFirst;
    void*   certContext;
    LPCWSTR certId;
    BOOL    certHash;
    void*   clientCertificateCallbackContext;
    void*   clientCertificateCallback;
    LPCWSTR serverCertFileName;
};

#define SNI_SSL_VALIDATE_CERTIFICATE   0x00000001U
#define SNI_SSL_USE_SCHANNEL_CACHE     0x00000002U
#define SNI_SSL_IGNORE_CHANNEL_BINDINGS 0x00000004U

// ============================================================================
// Function pointer table for SNI exports.
// NAMES MUST MATCH THE EXPORTED SYMBOLS IN Microsoft.Data.SqlClient.SNI.dll.
// These names come from src/Interop/Windows/Sni/SniNativeMethods.netcore.cs.
// Most have a "Wrapper" suffix because the DLL exports thin wrappers around
// the internal C++ functions.
// ============================================================================

typedef UINT32 (__cdecl *PFN_SNIInitialize)(void* pmo);
typedef UINT32 (__cdecl *PFN_SNITerminate)(void);
typedef UINT32 (__cdecl *PFN_SNIOpenSyncExWrapper)(SniClientConsumerInfo* pClientConsumerInfo, void** ppConn);
typedef UINT32 (__cdecl *PFN_SNICloseWrapper)(void* pConn);
typedef UINT32 (__cdecl *PFN_SNIAddProviderWrapper)(void* pConn, Provider provider, AuthProviderInfo* pInfo);
typedef UINT32 (__cdecl *PFN_SNIRemoveProviderWrapper)(void* pConn, Provider provider);
typedef UINT32 (__cdecl *PFN_SNIGetInfoWrapper)(void* pConn, QueryType queryType, UINT32* pInfo);
typedef void   (__cdecl *PFN_SNIGetLastError)(void* pErrorStruct);
typedef UINT32 (__cdecl *PFN_SNIReadAsyncWrapper)(void* pConn, void** ppNewPacket);
typedef UINT32 (__cdecl *PFN_SNIReadSyncOverAsync)(void* pConn, void** ppNewPacket, int timeout);
typedef void   (__cdecl *PFN_SNIPacketRelease)(void* pPacket);

// SqlAsyncCallbackDelegate signature (from managed P/Invoke):
//   void Callback(IntPtr key, IntPtr packet, uint error)
typedef void   (__cdecl *PFN_SNIAsyncCallback)(void* pKey, void* pPacket, UINT32 dwError);

// Optional diagnostic exports from our instrumented SNI build.
struct SNIPacketStatsSnapshot {
    long long cCtor;
    long long cDtor;
    long long cCachePop;
    long long cCachePush;
    long long cCacheReject;
    long long cFinalRel;
    long long cAddRef;
    long long cRelTotal;
    long long cWriteAsyncTotal;
    long long cWriteAsyncSyncReleased;
    long long cWriteAsyncDeferred;
    long long cWriteDoneTotal;
    long long cWriteDonePacketKept;
    long long cWriteDonePacketConsumed;
    long long cSniConnDtor;
    long long cCryptoBaseDtor;
    long long cSniCloseTotal;
    long long cSniConnCtor;
    long long cCryptoBaseRelease;
    long long cSniRemoveProvider;
    long long cConnReleaseToZero;
    long long cConnReleaseChainWalk;
    long long cTcpCloseSyncReadHeld;
    long long cTcpReadDoneTotal;
    long long cTcpReadDoneErr;
    long long cTcpReadDoneAbortFix1;
    long long cTcpReadDoneAbortFix2;
    long long cConnCloseRefHeld[16];
};
typedef void (__cdecl *PFN_SNIDumpPacketStats)(SNIPacketStatsSnapshot* pStats);
typedef void (__cdecl *PFN_SNIResetPacketStats)(void);

struct SniFns {
    HMODULE hDll;
    PFN_SNIInitialize             SNIInitialize;
    PFN_SNITerminate              SNITerminate;
    PFN_SNIOpenSyncExWrapper      SNIOpenSyncExWrapper;
    PFN_SNICloseWrapper           SNICloseWrapper;
    PFN_SNIAddProviderWrapper     SNIAddProviderWrapper;
    PFN_SNIRemoveProviderWrapper  SNIRemoveProviderWrapper;
    PFN_SNIGetInfoWrapper         SNIGetInfoWrapper;
    PFN_SNIGetLastError           SNIGetLastError;
    PFN_SNIReadAsyncWrapper       SNIReadAsyncWrapper;
    PFN_SNIReadSyncOverAsync      SNIReadSyncOverAsync;
    PFN_SNIPacketRelease          SNIPacketRelease;
    PFN_SNIDumpPacketStats        SNIDumpPacketStats;   // optional
    PFN_SNIResetPacketStats       SNIResetPacketStats;  // optional
};

static bool LoadSni(SniFns& fns, const wchar_t* dllPath)
{
    fns.hDll = LoadLibraryW(dllPath);
    if (!fns.hDll)
    {
        fwprintf(stderr, L"LoadLibrary(%s) failed with %lu\n", dllPath, GetLastError());
        return false;
    }

#define RESOLVE_REQUIRED(field, exportName) \
    fns.field = reinterpret_cast<decltype(fns.field)>(GetProcAddress(fns.hDll, exportName)); \
    if (!fns.field) { fwprintf(stderr, L"GetProcAddress(%S) failed (LastError=%lu)\n", exportName, GetLastError()); return false; }

#define RESOLVE_OPTIONAL(field, exportName) \
    fns.field = reinterpret_cast<decltype(fns.field)>(GetProcAddress(fns.hDll, exportName));

    RESOLVE_REQUIRED(SNIInitialize,            "SNIInitialize");
    RESOLVE_REQUIRED(SNITerminate,             "SNITerminate");
    RESOLVE_REQUIRED(SNIOpenSyncExWrapper,     "SNIOpenSyncExWrapper");
    RESOLVE_REQUIRED(SNICloseWrapper,          "SNICloseWrapper");
    RESOLVE_REQUIRED(SNIAddProviderWrapper,    "SNIAddProviderWrapper");
    RESOLVE_REQUIRED(SNIRemoveProviderWrapper, "SNIRemoveProviderWrapper");
    RESOLVE_REQUIRED(SNIGetInfoWrapper,        "SNIGetInfoWrapper");
    RESOLVE_REQUIRED(SNIGetLastError,          "SNIGetLastError");
    RESOLVE_REQUIRED(SNIReadAsyncWrapper,      "SNIReadAsyncWrapper");
    RESOLVE_REQUIRED(SNIReadSyncOverAsync,     "SNIReadSyncOverAsync");
    RESOLVE_REQUIRED(SNIPacketRelease,         "SNIPacketRelease");

    RESOLVE_OPTIONAL(SNIDumpPacketStats,       "SNIDumpPacketStats");
    RESOLVE_OPTIONAL(SNIResetPacketStats,      "SNIResetPacketStats");

#undef RESOLVE_REQUIRED
#undef RESOLVE_OPTIONAL
    return true;
}

// --------------------------------------------------------------------------
// Async-read callback. Mirrors the role of TdsParserStateObject.ReadAsyncCallback
// in managed code. The callback is invoked from an SNI worker thread when the
// async read completes. We must release the packet (managed code does NOT release
// it explicitly in ReadAsyncCallback; we suspect that's a managed-side bug, but
// here in the harness we WILL release to verify the native flow is balanced).
//
// To bracket the leak, set g_releaseAsyncReadPacket = false at run time to
// reproduce the managed behavior of NOT releasing.
// --------------------------------------------------------------------------
static SniFns* g_pFns = nullptr;
static volatile LONG g_asyncReadCallbacks = 0;
static bool g_releaseAsyncReadPacket = true;

static void __cdecl AsyncReadCallback(void* /*pKey*/, void* pPacket, UINT32 /*dwError*/)
{
    InterlockedIncrement(&g_asyncReadCallbacks);
    if (g_releaseAsyncReadPacket && pPacket && g_pFns && g_pFns->SNIPacketRelease)
    {
        g_pFns->SNIPacketRelease(pPacket);
    }
}

static void __cdecl AsyncWriteCallback(void* /*pKey*/, void* /*pPacket*/, UINT32 /*dwError*/)
{
    // No async writes in this harness; provided for completeness so SNI doesn't
    // crash if it ever invokes the write callback for some reason.
}

// --------------------------------------------------------------------------
// One connection iteration. Mirrors what SqlClient does on
// SqlConnection.Open() for Encrypt=Strict:
//   1. SNIOpenSyncExWrapper — TCP connect (TDS 8/Strict skips the TDS-7 prelogin
//      negotiation; ALPN happens inside the TLS handshake).
//   2. SNIAddProviderWrapper(SSL_PROV, tlsFirst=TRUE) — drives the TLS handshake.
//      On TLS 1.3 the NewSessionTicket post-handshake is consumed here too,
//      via SEC_I_RENEGOTIATE -> Ssl::Handshake loop. xperf-confirmed this is
//      where the leak originates.
//   3. (Optional) issueAsyncRead: post one SNIReadAsyncWrapper. This mirrors
//      what managed TdsParser does immediately after the TLS handshake to
//      receive the prelogin response / login ack. The async read returns
//      IO_PENDING; the worker thread later invokes our AsyncReadCallback.
//      We give it a brief moment to fire before SNIClose. This is the missing
//      piece that our v1 harness skipped — native data shows the leaked
//      packet comes from this exact code path.
//   4. (Optional) verifyProtocol: query SNI_QUERY_CONN_SSL_PROTOCOL_VERSION to
//      confirm TLS 1.3 (0x2000) was negotiated.
//   5. SNICloseWrapper — should release everything.
// --------------------------------------------------------------------------
static UINT32 RunOneConnection(SniFns& fns, LPCWSTR connStr, LPCWSTR hostInCert,
                                bool issueAsyncRead, bool verifyProtocol, UINT32* outProtocol)
{
    SniClientConsumerInfo info = {};
    info.ConsumerInfo.DefaultUserDataLength = 4096;
    // Install async callbacks if we plan to issue an async read; some SNI code
    // paths require the consumer to have non-null callbacks even for sync conns.
    info.ConsumerInfo.fnReadComp  = reinterpret_cast<void*>(&AsyncReadCallback);
    info.ConsumerInfo.fnWriteComp = reinterpret_cast<void*>(&AsyncWriteCallback);
    // ConsumerKey is the value passed back as `pKey` to callbacks. Use a sentinel.
    info.ConsumerInfo.ConsumerKey = reinterpret_cast<void*>(0xDEADBEEFULL);

    info.wszConnectionString       = connStr;
    info.HostNameInCertificate     = hostInCert;
    info.networkLibrary            = TCP_PREFIX;
    info.szSPN                     = nullptr;
    info.cchSPN                    = 0;
    info.szInstanceName            = nullptr;
    info.cchInstanceName           = 0;
    info.fOverrideLastConnectCache = FALSE;
    // Even though we want async-capable callbacks installed, the connection
    // itself can be opened in sync mode — mirrors SqlClient sync open.
    info.fSynchronousConnection    = TRUE;
    info.timeout                   = 5000;   // 5 sec
    info.fParallel                 = FALSE;
    info.transparentNetworkResolution = DisabledMode;
    info.totalTimeout              = 0;
    info.isAzureSqlServerEndpoint  = FALSE;
    info.ipAddressPreference       = IPv4First;
    info.DNSCacheInfo              = {};

    void* pConn = nullptr;
    UINT32 err = fns.SNIOpenSyncExWrapper(&info, &pConn);
    if (err != ERROR_SUCCESS || !pConn)
    {
        fwprintf(stderr, L"SNIOpenSyncExWrapper failed: 0x%08X\n", err);
        return err;
    }

    // Strict (TDS 8 / TLS first) — tlsFirst = TRUE.
    AuthProviderInfo authInfo = {};
    authInfo.flags    = SNI_SSL_VALIDATE_CERTIFICATE | SNI_SSL_USE_SCHANNEL_CACHE;
    authInfo.tlsFirst = TRUE;
    // All other fields (certContext, certId, certHash, callback*, serverCertFileName)
    // default-init to nullptr/FALSE.

    err = fns.SNIAddProviderWrapper(pConn, SSL_PROV, &authInfo);
    if (err != ERROR_SUCCESS)
    {
        fwprintf(stderr, L"SNIAddProviderWrapper(SSL_PROV) failed: 0x%08X\n", err);
        fns.SNICloseWrapper(pConn);
        return err;
    }

    if (verifyProtocol && outProtocol)
    {
        UINT32 proto = 0;
        UINT32 qerr = fns.SNIGetInfoWrapper(pConn, SNI_QUERY_CONN_SSL_PROTOCOL_VERSION, &proto);
        *outProtocol = (qerr == ERROR_SUCCESS) ? proto : 0;
    }

    // Optional: post one async read to mirror the managed login flow. The
    // server is sitting waiting for our login bytes, so the read will pend
    // (return IO_PENDING) and our AsyncReadCallback will NOT fire before we
    // call SNIClose. SNIClose should still tear down the conn cleanly and
    // any in-flight read packet should be released.
    if (issueAsyncRead)
    {
        void* pPacket = nullptr;
        UINT32 readErr = fns.SNIReadAsyncWrapper(pConn, &pPacket);
        if (readErr != ERROR_SUCCESS && readErr != /*SNI_SUCCESS_IO_PENDING*/ 997 /*ERROR_IO_PENDING*/)
        {
            fwprintf(stderr, L"SNIReadAsyncWrapper failed: 0x%08X\n", readErr);
        }
        else if (readErr == ERROR_SUCCESS && pPacket)
        {
            // Read completed synchronously — release the packet immediately.
            // (Real SqlClient invokes ReadAsyncCallback synchronously in this case.)
            if (g_releaseAsyncReadPacket)
                fns.SNIPacketRelease(pPacket);
        }
        // Else: IO_PENDING (997) — callback will fire later. SNIClose below
        // will trigger cancellation; whether the resulting packet leaks is
        // exactly what we want to find out.
    }

    err = fns.SNICloseWrapper(pConn);
    if (err != ERROR_SUCCESS)
    {
        fwprintf(stderr, L"SNICloseWrapper failed: 0x%08X\n", err);
    }

    return err;
}

// --------------------------------------------------------------------------
// Schannel SP_PROT_* -> human readable.
// --------------------------------------------------------------------------
static const wchar_t* TlsProtocolName(UINT32 schProto)
{
    switch (schProto)
    {
        case 0x00000040: return L"SSL 2.0 client";   // SP_PROT_SSL2_CLIENT
        case 0x00000020: return L"SSL 3.0 client";   // SP_PROT_SSL3_CLIENT
        case 0x00000080: return L"TLS 1.0 client";   // SP_PROT_TLS1_CLIENT
        case 0x00000200: return L"TLS 1.1 client";   // SP_PROT_TLS1_1_CLIENT
        case 0x00000800: return L"TLS 1.2 client";   // SP_PROT_TLS1_2_CLIENT
        case 0x00002000: return L"TLS 1.3 client";   // SP_PROT_TLS1_3_CLIENT
        default:         return L"unknown";
    }
}

// --------------------------------------------------------------------------
// Memory measurement
// --------------------------------------------------------------------------
static SIZE_T GetPrivateBytes()
{
    PROCESS_MEMORY_COUNTERS_EX pmc = { sizeof(pmc) };
    if (!GetProcessMemoryInfo(GetCurrentProcess(),
                              reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&pmc),
                              sizeof(pmc)))
    {
        return 0;
    }
    return pmc.PrivateUsage;
}

// --------------------------------------------------------------------------
// Main
// --------------------------------------------------------------------------
int wmain(int argc, wchar_t** argv)
{
    if (argc < 3)
    {
        wprintf(L"Usage: %s <server> <iterations> [hostNameInCertificate] [pathToSNIDll] [flags]\n", argv[0]);
        wprintf(L"\n");
        wprintf(L"Flags (any subset, in any order):\n");
        wprintf(L"  --async-read              Issue one SNIReadAsyncWrapper per iteration (mirror managed login)\n");
        wprintf(L"  --no-release-async-packet Don't call SNIPacketRelease on the async-read packet\n");
        wprintf(L"                            (simulates the suspected managed-side bug)\n");
        wprintf(L"\n");
        wprintf(L"Examples:\n");
        wprintf(L"  %s localhost,1433 100 localhost\n", argv[0]);
        wprintf(L"  %s localhost,1433 100 localhost Microsoft.Data.SqlClient.SNI.dll --async-read\n", argv[0]);
        wprintf(L"  %s localhost,1433 100 localhost Microsoft.Data.SqlClient.SNI.dll --async-read --no-release-async-packet\n", argv[0]);
        return 1;
    }

    LPCWSTR server      = argv[1];
    int iterations      = _wtoi(argv[2]);
    LPCWSTR hostInCert  = argc >= 4 ? argv[3] : L"";
    const wchar_t* dllPath = argc >= 5 ? argv[4] : L"Microsoft.Data.SqlClient.SNI.dll";

    bool issueAsyncRead = false;
    g_releaseAsyncReadPacket = true;
    for (int i = 5; i < argc; i++)
    {
        if (_wcsicmp(argv[i], L"--async-read") == 0) issueAsyncRead = true;
        else if (_wcsicmp(argv[i], L"--no-release-async-packet") == 0) g_releaseAsyncReadPacket = false;
    }

    wprintf(L"=== SNI Native Repro (ICM 792529661) ===\n");
    wprintf(L"DLL:                %s\n", dllPath);
    wprintf(L"Server:             %s\n", server);
    wprintf(L"hostInCertificate:  %s\n", hostInCert && *hostInCert ? hostInCert : L"(none)");
    wprintf(L"Iterations:         %d\n", iterations);
    wprintf(L"AsyncRead:          %s\n", issueAsyncRead ? L"YES" : L"no");
    wprintf(L"ReleaseAsyncPacket: %s\n", g_releaseAsyncReadPacket ? L"YES" : L"NO (simulate managed bug)");
    wprintf(L"\n");

    SniFns fns = {};
    if (!LoadSni(fns, dllPath))
    {
        return 1;
    }
    g_pFns = &fns;

    UINT32 initErr = fns.SNIInitialize(nullptr);
    if (initErr != ERROR_SUCCESS)
    {
        fwprintf(stderr, L"SNIInitialize failed: 0x%08X\n", initErr);
        return 1;
    }

    // Reset diagnostic counters (if present in this DLL build).
    if (fns.SNIResetPacketStats) fns.SNIResetPacketStats();

    // Warmup + protocol verification.
    UINT32 negotiated = 0;
    if (RunOneConnection(fns, server, hostInCert, issueAsyncRead, /*verifyProtocol=*/true, &negotiated) != ERROR_SUCCESS)
    {
        fwprintf(stderr, L"Warmup connection failed. Aborting.\n");
        fns.SNITerminate();
        return 1;
    }

    wprintf(L"Negotiated TLS version: 0x%08X (%s)\n",
            negotiated, TlsProtocolName(negotiated));
    if (negotiated != 0x00002000)
    {
        wprintf(L"\n");
        wprintf(L"!! WARNING: Negotiated protocol is NOT TLS 1.3 (0x00002000).\n");
        wprintf(L"   The leak reproduces specifically on the TLS 1.3 NewSessionTicket\n");
        wprintf(L"   post-handshake path. If your server doesn't speak TLS 1.3 you may\n");
        wprintf(L"   see no leak here even though the bug still exists.\n");
        wprintf(L"\n");
        wprintf(L"   To force TLS 1.3:\n");
        wprintf(L"   - Use SQL Server 2022 or newer (TLS 1.3 supported by default).\n");
        wprintf(L"   - On the client side TLS 1.3 is enabled by default when SCH_CREDENTIALS\n");
        wprintf(L"     is used (Windows 10 1809+, all Windows 11). Verify the registry:\n");
        wprintf(L"       HKLM\\SYSTEM\\CurrentControlSet\\Control\\SecurityProviders\\SCHANNEL\\\n");
        wprintf(L"       Protocols\\TLS 1.3\\Client\\Enabled=1, DisabledByDefault=0\n");
    }
    wprintf(L"\n");

    SIZE_T baseline = GetPrivateBytes();
    wprintf(L"Baseline private bytes: %.2f MB\n", baseline / (1024.0 * 1024.0));
    wprintf(L"\n");
    wprintf(L"  Iter   PrivateMB  PerConnKB\n");
    wprintf(L"  -----  ---------  ---------\n");

    const int batch = 100;
    SIZE_T lastSnapshot = baseline;
    int errCount = 0;

    for (int i = 1; i <= iterations; i++)
    {
        UINT32 err = RunOneConnection(fns, server, hostInCert, issueAsyncRead, /*verifyProtocol=*/false, nullptr);
        if (err != ERROR_SUCCESS)
        {
            errCount++;
            if (errCount < 5)
                fwprintf(stderr, L"Iteration %d failed: 0x%08X\n", i, err);
            else if (errCount == 5)
                fwprintf(stderr, L"... suppressing further errors ...\n");
        }

        if (i % batch == 0)
        {
            SIZE_T now = GetPrivateBytes();
            double mb = now / (1024.0 * 1024.0);
            double deltaKB = (now > lastSnapshot ? (double)(now - lastSnapshot) : 0.0) / batch / 1024.0;
            wprintf(L"  %5d  %9.2f  %9.2f\n", i, mb, deltaKB);
            lastSnapshot = now;
        }
    }

    // Final tally.
    SIZE_T final_ = GetPrivateBytes();
    double totalDelta = (final_ > baseline ? (double)(final_ - baseline) : 0.0);
    wprintf(L"\nTotal growth: %.2f MB over %d iterations (%.2f KB/iter)\n",
            totalDelta / (1024.0 * 1024.0),
            iterations,
            (totalDelta / iterations) / 1024.0);
    if (errCount > 0)
    {
        wprintf(L"Connection errors:    %d\n", errCount);
    }
    if (issueAsyncRead)
    {
        wprintf(L"Async read callbacks: %ld\n", g_asyncReadCallbacks);
    }

    // Give SNI's IOCP worker threads time to drain any pending async-read
    // abort completions queued by closesocket. Without this drain the
    // harness exits before Tcp::ReadDone gets a chance to run on the
    // aborted reads, and we can't observe whether the LEAK-FIX branches
    // execute. 2 seconds is conservative; even a stress-test of 1000 abort
    // completions should finish in well under that on localhost.
    if (issueAsyncRead)
    {
        wprintf(L"\nDraining IOCP for 2 seconds...\n");
        Sleep(2000);
        wprintf(L"Async read callbacks after drain: %ld\n", g_asyncReadCallbacks);
    }

    // Dump SNI diagnostic counters if the instrumented DLL is present.
    if (fns.SNIDumpPacketStats)
    {
        SNIPacketStatsSnapshot stats = {};
        fns.SNIDumpPacketStats(&stats);
        wprintf(L"\n--- SNI diagnostic counters ---\n");
        wprintf(L"  Ctor=%lld Dtor=%lld AddRef=%lld Release=%lld FinalRel=%lld\n",
                stats.cCtor, stats.cDtor, stats.cAddRef, stats.cRelTotal, stats.cFinalRel);
        wprintf(L"  CachePop=%lld CachePush=%lld CacheReject=%lld\n",
                stats.cCachePop, stats.cCachePush, stats.cCacheReject);
        wprintf(L"  SniConnCtor=%lld SniConnDtor=%lld SniCloseTotal=%lld\n",
                stats.cSniConnCtor, stats.cSniConnDtor, stats.cSniCloseTotal);
        wprintf(L"  CryptoBaseRelease=%lld CryptoBaseDtor=%lld\n",
                stats.cCryptoBaseRelease, stats.cCryptoBaseDtor);
        wprintf(L"  ConnReleaseToZero=%lld ConnReleaseChainWalk=%lld\n",
                stats.cConnReleaseToZero, stats.cConnReleaseChainWalk);
        wprintf(L"  TcpCloseSyncReadHeld=%lld\n", stats.cTcpCloseSyncReadHeld);
        wprintf(L"  TcpReadDoneTotal=%lld TcpReadDoneErr=%lld\n",
                stats.cTcpReadDoneTotal, stats.cTcpReadDoneErr);
        wprintf(L"  TcpReadDoneAbortFix1=%lld TcpReadDoneAbortFix2=%lld\n",
                stats.cTcpReadDoneAbortFix1, stats.cTcpReadDoneAbortFix2);
        if (stats.cConnCloseRefHeld[0] || stats.cConnCloseRefHeld[2])
        {
            wprintf(L"  ConnCloseRefHeld[0:REF_Active]=%lld [2:REF_Packet]=%lld\n",
                    stats.cConnCloseRefHeld[0], stats.cConnCloseRefHeld[2]);
        }
    }

    fns.SNITerminate();
    FreeLibrary(fns.hDll);
    return 0;
}
