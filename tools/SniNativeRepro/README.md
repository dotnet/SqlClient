# SniNativeRepro — Native C++ harness for ICM 792529661

Loads `Microsoft.Data.SqlClient.SNI.dll` and replays the same API sequence
SqlClient uses for an `Encrypt=Strict` connection. Use this with native
memory profilers to pinpoint the leak without managed-runtime noise.

## Why it exists

The managed bench (`StrictEncryptMemoryBenchmark`) shows a stable
~52 KB/conn native leak on Strict + TLS 1.3 + `--no-pooling`. xperf heap
stacks point to `SNI_Packet` allocations from `Tcp::ReadSync` that never
get released. Refcount instrumentation in the SNI DLL plus managed
`Dispose()` counters tell us:

- `Stuck refs = 1001` per 1000 connections (exactly 1 packet per conn
  alive with refcount > 0)
- `_writePacketCache.Count = 1` per conn at `Dispose()` entry — but that
  packet IS disposed cleanly via `SNIPacket.ReleaseHandle()`
- All instrumented native field locations (Tcp::m_pSyncReadPacket,
  CryptoBase::m_pPartial/m_pLeftOver/queues) are NULL at SNIClose entry

Net: the leak is in SNI's internal state somewhere we haven't named yet,
and the managed-side instrumentation can't reach it. A native repro with
**Application Verifier / Visual Studio Memory Diagnostics / DrMemory**
running against this process will be able to.

## Build

From a Visual Studio Developer Command Prompt:

```cmd
cd C:\Users\apdeshmukh\git\SqlClient\dev-ad-strict-oom\tools\SniNativeRepro
msbuild SniNativeRepro.vcxproj /p:Configuration=Release /p:Platform=x64
```

Output: `x64\Release\SniNativeRepro.exe`.

For a debug-friendly build (preferred for memory profiling, since CRT debug
heap and PageHeap behave better with full PDBs):

```cmd
msbuild SniNativeRepro.vcxproj /p:Configuration=Debug /p:Platform=x64
```

Output: `x64\Debug\SniNativeRepro.exe`.

## Deploy

Copy your instrumented SNI DLL next to the EXE (or set `PATH`):

```cmd
copy /Y "C:\Users\apdeshmukh\git\ado\Microsoft.Data.SqlClient.sni\bin\Windows_NT\Release.x64\SNI\Microsoft.Data.SqlClient.SNI.dll" .
```

Also need the SNI dependency `Microsoft.Data.SqlClient.SNI.runtime.x64.dll`
or whatever it is in your build tree — copy the full `bin\Windows_NT\Release.x64\SNI\`
folder side-by-side. On Windows 11 the only runtime dep that matters is
the SNI DLL itself; everything else (SSPI, SChannel, ncrypt) is in System32.

## Run (smoke test)

```cmd
SniNativeRepro.exe localhost,1433 100 localhost
```

Expected output:

```
=== SNI Native Repro (ICM 792529661) ===
DLL:                Microsoft.Data.SqlClient.SNI.dll
Server:             localhost,1433
hostInCertificate:  localhost
Iterations:         100

Negotiated TLS version: 0x00002000 (TLS 1.3 client)

Baseline private bytes: 5.42 MB

  Iter   PrivateMB  PerConnKB
  -----  ---------  ---------
    100      55.71       50.30

Total growth: 50.29 MB over 100 iterations (515.10 KB/iter)
```

(Numbers are illustrative. The harness prints the negotiated TLS version
via `SNIGetInfoWrapper(SNI_QUERY_CONN_SSL_PROTOCOL_VERSION)` — `0x2000` means
TLS 1.3, `0x800` means TLS 1.2. The leak reproduces specifically on the
TLS 1.3 NewSessionTicket post-handshake path; if you see TLS 1.2 the bug
may not manifest.)

## Forcing / verifying TLS 1.3

Client side:
- Schannel picks the highest mutually-supported protocol. SNI uses
  `SCH_CREDENTIALS` (version 5, no protocol restriction), so on Windows 10
  1809+ TLS 1.3 is selected automatically against any TLS 1.3-capable server.
- Verify client TLS 1.3 is enabled (default on Windows 11):
  ```cmd
  reg query "HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Client"
  ```
  Expected: `Enabled = 1`, `DisabledByDefault = 0`. If those keys don't exist,
  Schannel uses safe defaults (TLS 1.3 ON).

Server side:
- **SQL Server 2022+** ships with TLS 1.3 support out of the box.
- Older SQL Server versions need a recent CU (e.g. SQL 2019 CU22+) and the
  server's `[HKLM\...\TLS 1.3\Server\Enabled]` flag.
- To confirm what the server offers, run **Wireshark** on `tcp port 1433`
  during the handshake — the ServerHello's `selected_version` extension
  identifies the negotiated TLS version.

The harness itself prints the negotiated version after the warmup connection,
so the simplest way to verify TLS 1.3 is just to read the first output line:
look for `Negotiated TLS version: 0x00002000 (TLS 1.3 client)`.

## Profile under Application Verifier

Catches double-free / use-after-free / heap corruption inside SNI.dll:

```cmd
appverif /verify SniNativeRepro.exe /faults
SniNativeRepro.exe localhost,1433 100 localhost
:: To turn off after profiling:
appverif /n SniNativeRepro.exe
```

When AppVerif fires, the debugger breaks at the offending call site with
full native stacks. Look for breaks inside ssl.cpp / tcp.cpp / sni.cpp.

## Profile under Visual Studio Memory Diagnostics

The most direct way to see the leak.

1. Open `SniNativeRepro.sln` in Visual Studio.
2. Set the EXE's working directory to where you copied the SNI DLL.
3. **Debug → Performance Profiler → Memory Usage → Start**.
4. Use the command-line args: `localhost,1433 1000 localhost`.
5. Click **Take Snapshot** at iteration 100 (watch the console output).
6. Click **Take Snapshot** again at iteration 1000.
7. Click the **diff (→)** view on the second snapshot.
8. Look for native types that grew by ~1000 instances: this is your leak.

## Profile under DrMemory (open-source Valgrind-equivalent)

```cmd
:: Install: https://drmemory.org/page_download.html
drmemory.exe -- SniNativeRepro.exe localhost,1433 100 localhost
```

DrMemory writes a `results.txt` with every unfreed allocation grouped by
stack. On the leak path, you'll see entries like:

```
LEAK 100 direct bytes ... + 0 indirect bytes
  malloc
  operator new
  SNI_Packet::SNI_Packet
  SNIPacketNew
  SNIPacketAllocateEx2
  Tcp::ReadSync
  ...
```

## Caveats

1. **TDS Login is not exercised here.** This harness only triggers TCP open
   and the TLS handshake (via `SNIAddProvider(SSL_PROV)`). The xperf
   evidence shows the leak is in the SSL provider's `Ssl::Decrypt`
   SEC_I_RENEGOTIATE branch, which fires during the initial TLS 1.3
   handshake on TDS 8. If the leak reproduces here, the fix is in SNI;
   if it does NOT reproduce, the leak only manifests after a full TDS
   exchange and we'd need to add Login7 / batch packet handling.

2. **`SNIOpenSyncEx` signature is intricate.** The struct layout for
   `SNI_CLIENT_CONSUMER_INFO` differs between netfx and netcore builds
   in the managed wrapper. If `SNIOpenSyncEx` fails with `0x80004005` or
   AV's at the call site, the struct layout may be off. Compare against
   `SniNativeMethods.cs` in dev-ad-strict-oom and adjust.

3. **`SNIAuthProviderInfo` layout** must match `sni.hpp::SNIAuthProviderInfo`
   in the SNI repo. We use a minimal shape that omits enclave / cert hash
   fields — if you need those, copy the full struct from
   `c:\Users\apdeshmukh\git\ado\Microsoft.Data.SqlClient.sni\src\Microsoft.Data.SqlClient\netfx\src\SNI\include\ssl.hpp`.

## Next steps after the leak is pinpointed

Once VS Memory Diagnostics / DrMemory shows the exact alloc-without-free
pair (stack with file:line in our SNI source), the fix is local: find the
function that holds the orphan reference and add the corresponding release.
Then re-run this harness AND the managed bench to confirm both go to zero.
