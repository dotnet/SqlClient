# Issue 3148 Investigation App

This folder contains a small standalone diagnostic app for investigating the
`0x8007007E` `Microsoft.Data.SqlClient.SNI.dll` load failure discussed in
issue `#3148`.

The app is aimed at the path-ordering hypothesis:

- show the current process architecture
- show the runtime architecture
- show the `PATH` entries that contain `dotnet`
- resolve `dotnet` from `PATH` the same way process launch does
- optionally relaunch itself with x86 `dotnet` ordered before x64 `dotnet` on Windows
- optionally try `SqlConnection.Open()` if a connection string is provided

## Build

```bash
dotnet build investigations/issue-3148/Issue3148PathProbe.csproj
```

## Run diagnostics only

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj
```

## Run with a connection attempt

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj -- \
  --connection-string "Server=...;Database=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True"
```

## Force Windows PATH ordering

This mode is intended to actively test the hypothesis behind issue `#3148`.
On Windows, the probe starts a fresh child process with `Program Files (x86)\\dotnet`
placed before `Program Files\\dotnet` in `PATH`, then runs the same diagnostics and
optional connection test in that child.

This mode must be run as a framework-dependent app through `dotnet`, for example via
`dotnet run` or `dotnet Issue3148PathProbe.dll`. It does not validate host selection for
published executables directly.

If both `Program Files (x86)\\dotnet` and `Program Files\\dotnet` are not present in
`PATH`, the probe prints a diagnostic message and exits with code `2`.

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj -- \
  --force-x86-dotnet-first
```

You can combine it with a connection attempt:

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj -- \
  --force-x86-dotnet-first \
  --connection-string "Server=...;Database=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True"
```

## Test with x86 runtime only (no x86 SDK required)

For validating runtime behavior, run the built DLL directly with x86 `dotnet`.
This avoids SDK resolution and therefore avoids `global.json` SDK pinning.

### 1) Verify x86 runtime installation

```powershell
$x86Dotnet = 'C:\Program Files (x86)\dotnet\dotnet.exe'
if (Test-Path $x86Dotnet) {
    & $x86Dotnet --list-runtimes
}
```

Look for `Microsoft.NETCore.App 8.0.26` in the output.

### 2) Ensure the probe DLL exists

```powershell
$dll = Join-Path $PWD 'investigations\issue-3148\bin\Debug\net8.0\Issue3148PathProbe.dll'
if (-not (Test-Path $dll)) {
    dotnet build investigations/issue-3148/Issue3148PathProbe.csproj -v minimal
}
```

### 3) Run with x86 host and x86-first PATH ordering

```powershell
$dll = Join-Path $PWD 'investigations\issue-3148\bin\Debug\net8.0\Issue3148PathProbe.dll'
$env:PATH = 'C:\Program Files (x86)\dotnet;C:\Program Files\dotnet;' + $env:PATH
& 'C:\Program Files (x86)\dotnet\dotnet.exe' $dll
Write-Output "EXIT:$LASTEXITCODE"
```

Expected signals in output:

- `Process architecture: X86`
- `Framework: .NET 8.0.26`
- `Current process path: C:\Program Files (x86)\dotnet\dotnet.exe`
- `PATH ordering assessment: warning: x86 dotnet appears before x64 in PATH ...`
- `EXIT:0`

## Scenario probes

The four probes below each target one of the root-cause scenarios (A–D) identified
in the runtime analysis. They require no SQL Server connection and run in seconds.

### Probe A — SNI path survey (`--probe-sni-paths`)

Enumerates every location the runtime will probe for `Microsoft.Data.SqlClient.SNI.dll`
and reports existence and file size. Covers scenario A (DLL not deployed).

```powershell
$dll = Join-Path $PWD 'investigations\issue-3148\bin\Debug\net8.0\Issue3148PathProbe.dll'
& 'C:\Program Files\dotnet\dotnet.exe' $dll --probe-sni-paths
```

Expected output on a healthy machine:

```
Probe: SNI path survey
======================
Process architecture: X64

[FOUND] C:\Users\...\AppData\Local\Temp\.net\Issue3148PathProbe\<hash>\Microsoft.Data.SqlClient.SNI.dll
        size=566832 bytes
```

Exit code 1 with `[ABSENT]` means no SNI DLL was found anywhere — a deployment
problem.

### Probe B — Architecture match (`--probe-arch-match`)

Reads the PE `IMAGE_FILE_MACHINE` field of every SNI DLL found on disk and compares
it to `RuntimeInformation.ProcessArchitecture`. Covers scenario B (arch mismatch).

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' $dll --probe-arch-match
```

Expected output:

```
Probe: SNI architecture match
=============================
Process architecture : X64

[MATCH    ] ...\Microsoft.Data.SqlClient.SNI.dll
             DLL arch=X64  process arch=X64
```

A `[MISMATCH]` line means the DLL on disk is the wrong architecture for this
process. The .NET runtime will report `ERROR_BAD_EXE_FORMAT` (193), not
`ERROR_MOD_NOT_FOUND` (126). If an arch mismatch appears *together* with no
matching-arch copy at all, the runtime will instead report `0x8007007E`.

### Probe C — Native load (`--probe-native-load`)

Calls `NativeLibrary.TryLoad()` against each SNI DLL found on disk. If the file
exists and the arch matches but load still fails, a VC++ or other import-table
dependency is missing. Covers scenario C (missing VC++ runtime).

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' $dll --probe-native-load
```

Expected output:

```
Probe: NativeLibrary.TryLoad
===========================
Process architecture: X64

[OK    ] ...\Microsoft.Data.SqlClient.SNI.dll
```

A `[FAIL]` line with "Architecture mismatch" confirms scenario B. A `[FAIL]` line
with "VC++ or other import-table dependency is missing" points to scenario C —
install the Visual C++ redistributable that matches the SNI DLL version.

### Probe D — Single-file extraction race (`--probe-extraction-race`)

Deletes extracted SNI DLL copies under `%TEMP%` then calls `SqlConnection.Open()`.
Simulates what happens when a temp-cleanup tool removes the extracted copy between
process startup and the first database connection. Covers scenario D (single-file
extraction race).

This probe is only meaningful when run from a **single-file published exe** (see
`Publish a Windows single-file probe` below). When run as a framework-dependent
app, the runtime finds SNI via the packs cache and succeeds regardless.

```powershell
# Publish first:
dotnet publish investigations/issue-3148/Issue3148PathProbe.csproj `
  -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

# Run the published exe once so SNI is extracted to %TEMP%:
.\investigations\issue-3148\bin\Release\net8.0\win-x64\publish\Issue3148PathProbe.exe

# Now run the probe — it deletes the extracted copy then calls Open():
$cs = 'Server=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True;'
.\investigations\issue-3148\bin\Release\net8.0\win-x64\publish\Issue3148PathProbe.exe `
  --probe-extraction-race --connection-string $cs
```

Expected output when the race is reproduced (no fallback copy exists):

```
[DELETED] C:\Users\...\AppData\Local\Temp\.net\Issue3148PathProbe\<hash>\Microsoft.Data.SqlClient.SNI.dll
...
SqlConnection.Open() failed — SNI absent at LoadLibrary time.
[0] System.DllNotFoundException: Unable to load DLL 'Microsoft.Data.SqlClient.SNI.dll'...
```

### Probe E — Delayed first load (`--probe-lazy-load`)

This mode simulates a long-running process that does not touch SqlClient until
later. The probe waits for a configurable delay and only then performs the first
`SqlConnection.Open()`, which is when SNI is actually loaded.

Use this mode when you need to reproduce "service ran for hours, then first DB
call failed with `0x8007007E`" behavior.

```powershell
$dll = Join-Path $PWD 'investigations\issue-3148\bin\Debug\net8.0\Issue3148PathProbe.dll'
$cs = 'Server=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True;'
& 'C:\Program Files\dotnet\dotnet.exe' $dll --probe-lazy-load --lazy-load-delay 600 --connection-string $cs
```

To intentionally disturb the SNI load environment during the delay window:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' $dll --probe-lazy-load --lazy-load-delay 30 --lazy-load-disturb-sni --connection-string $cs
```

Options for this probe:

- `--probe-lazy-load` — Enable delayed first-load probe.
- `--lazy-load-delay <seconds>` — Delay before first `SqlConnection.Open()` (default: 60).
- `--lazy-load-disturb-sni` — Rename discovered `Microsoft.Data.SqlClient.SNI.dll` files (app output, native search directories, and relevant NuGet package roots) just before first `Open()`, then restore them after the attempt.
- `--connection-string` — Required.

Expected output:

```
Probe: delayed first SNI load
==============================
PID: 12345
Delay before first SqlConnection.Open(): 600 seconds
Waiting... 600s remaining
...
Delay complete. Attempting first SqlConnection.Open() now...
```

With `--lazy-load-disturb-sni`, output will also show how many SNI DLL files were
renamed before first `Open()`, and confirm they were restored afterward.

If the environment has drifted during the wait (for example temp cleanup,
deployment changes, or AV timing), this first-open call is where `0x8007007E`
will surface.

## Load-time absent probe (`--stress-load-absent`)

This mode proves that error `0x8007007E` is elicited when SNI is absent at
`LoadLibrary` time. Because proof #1 shows that a process which has already loaded
SNI cannot encounter this error again, this mode tests only the **load-time path**
— it does not reproduce the long-running service scenario.

### Why in-process connection loops cannot trigger the issue

Once `Microsoft.Data.SqlClient.SNI.dll` is loaded via `LoadLibrary`, Windows holds
a lock on the file for the **lifetime of the process**. All subsequent
`SqlConnection.Open()` calls reuse the already-loaded native library without
touching the disk. An in-process loop therefore exercises the *post-load* path,
which is bulletproof by design. Rename attempts against an already-loaded DLL
always fail with a sharing-violation error.

### How the child-process approach works

The stress test spawns a **fresh child process** for each attempt. Each child goes
through the full `LoadLibrary` path from scratch. The parent process — which never
loads `SqlClient` or `SNI` itself — can rename the SNI DLL freely before the child
starts, creating a genuine load-time interference window.

| Aspect | In-process loop (old) | Child-process spawn (new) |
|--------|----------------------|--------------------------|
| Connection attempts | Same process (SNI already loaded) | Fresh child process each time (SNI not yet loaded) |
| DLL rename window | After SNI loaded → always blocked (file in use) | Before child starts → succeeds every iteration |
| What is tested | `Open()`/`Close()` throughput | Actual `LoadLibrary` call path |
| Can trigger issue #3148? | No | Yes — if SNI is absent or wrong-arch when child calls `LoadLibrary` |

The parent holds the rename for 2 seconds (long enough for the child CLR to start
and reach `LoadLibrary`), then restores the DLL so the child can still pick it up
if it hasn't loaded yet. This creates a race window matching the original failure
scenario.

### Run the stress test

```powershell
$dll = Join-Path $PWD 'investigations\issue-3148\bin\Debug\net8.0\Issue3148PathProbe.dll'
$cs = 'Server=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True;'
& 'C:\Program Files\dotnet\dotnet.exe' $dll --stress-load-absent --connection-string $cs --stress-duration 60
```

Options:

- `--stress-load-absent` — Enable load-time absent probe. Windows-only.
- `--connection-string` — Required. SQL Server connection string passed to each child process.
- `--stress-duration <seconds>` — How long to keep spawning children (default: 30 seconds).

Expected output:

```
Starting SNI stress test (child-process mode)...
Duration: 60 seconds
Each iteration spawns a fresh child process so SNI loads from scratch.
Parent briefly renames SNI DLLs before each child starts to inject load-time interference.

[OK]   iteration=1
[OK]   iteration=2
...

Stress test complete.
Child process launches: 20
  Successful: 20
  Failed: 0
Iterations with rename window: 20 of 20
```

Interpreting results:

- `Iterations with rename window: N of N` — confirms the parent found the SNI DLL on
  disk and renamed it before each child. If this is `0 of N`, the DLL was not found
  in the expected locations (check the packs cache or temp directory).
- `[OK]` — child loaded SNI and connected successfully.
- `[FAIL] exitCode=1` — child failed. Capturing the child's output (remove
  `RedirectStandardOutput` temporarily) will show the full exception chain.

A failing environment (wrong SNI architecture, AV interference, or a single-file
extraction race) would produce `[FAIL]` entries here. A clean lab environment with
the correct x64 SNI installed will typically show all `[OK]`.

## Troubleshooting tips

- `dotnet run` requires an SDK and is affected by `global.json`.
- `dotnet <app>.dll` uses runtime resolution and is the preferred path for runtime-only testing.
- If `--force-x86-dotnet-first` prints that both x86 and x64 entries are required, add both folders to `PATH` for that session before running.
- If you need to test SDK commands under x86 host selection, install an x86 SDK version compatible with the repository `global.json`.
- The `--stress-test-sni` mode is Windows-only and requires a valid SQL connection string.
- If `Iterations with rename window: 0 of N`, the SNI DLL was not found in the packs cache
  or temp directories. Run the probe once without `--stress-test-sni` to do a connection test
  first; the NuGet package extraction will place the DLL in a temp folder that the stress
  test can then locate.


## Publish a Windows single-file probe

This is useful if you want to mirror the original report more closely.

```bash
dotnet publish investigations/issue-3148/Issue3148PathProbe.csproj \
  -c Release \
  -r win-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## What to look for

- If the first `dotnet` resolved from `PATH` is under `Program Files (x86)`,
  that supports the path-ordering suspicion.
- If `--force-x86-dotnet-first` reproduces the failure in the child process,
  that is strong evidence that launcher/runtime selection is part of the issue.
- If the process architecture and runtime architecture do not match the intended
  deployment, that is another strong signal.
- If `SqlConnection.Open()` fails, the app prints the full exception chain so
  the loader error code can be captured.

---

## Background: Runtime source analysis

This section documents findings from reading the .NET 8 runtime source code
(`v8.0.0` tag on `dotnet/runtime`). The evidence below informs both the design
of the stress test and the list of root causes to investigate on affected systems.

### Proof: SNI.dll is loaded exactly once per process, never reloaded

#### Entry point: `NDirectImportThunk` → `NDirectLink` (dllimport.cpp)

The first time a P/Invoke method is called, control passes through
`NDirectImportThunk`, which calls `NDirectLink`. That function calls
`NativeLibrary::LoadLibraryFromMethodDesc`, obtains the `HMODULE`, resolves the
entry point, and then stores both permanently in the `NDirectMethodDesc`:

```cpp
// dllimport.cpp — NDirectLink()
NATIVE_LIBRARY_HANDLE hmod = NativeLibrary::LoadLibraryFromMethodDesc(pMD);
// ...
LPVOID pvTarget = NDirectGetEntryPoint(pMD, hmod);
if (pvTarget)
{
    pMD->SetNDirectTarget(pvTarget);   // ← written into NDirectMethodDesc once
    fSuccess = TRUE;
}
```

`SetNDirectTarget` writes directly into `NDirectMethodDesc::m_pWriteableData->m_pNDirectTarget`.
Every subsequent call to that P/Invoke method jumps directly to that pointer.
`NDirectImportThunk` — and therefore `LoadLibrary` — is **never invoked again**
for that method.

#### The `UnmanagedImageCache`: add-only, process-lifetime (nativelibrary.cpp + appdomain.cpp)

Inside `LoadLibraryFromMethodDesc`, the call chain reaches `LoadNativeLibrary()`:

```cpp
// nativelibrary.cpp — LoadNativeLibrary()
hmod = pDomain->FindUnmanagedImageInCache(wszLibName);  // check cache first
if (hmod != NULL)
    return hmod.Extract();   // ← return immediately, no LoadLibrary call

hmod = LoadNativeLibraryBySearch(pMD, pErrorTracker, wszLibName);
if (hmod != NULL)
{
    pDomain->AddUnmanagedImageToCache(wszLibName, hmod); // ← store permanently
    return hmod.Extract();
}
```

The cache is implemented in `AppDomain::AddUnmanagedImageToCache()` (appdomain.cpp).
It allocates the key on the loader heap (which lives for the AppDomain lifetime)
and inserts into a hash table. There is **no `RemoveUnmanagedImageFromCache`**
anywhere in the codebase. The cache is insert-only.

In .NET 8, there is a single AppDomain that lasts for the entire process lifetime.
Therefore:

- `LoadLibrary` is called for `SNI.dll` **at most once per process**.
- After the first successful load the HMODULE is cached; all future P/Invoke
  calls use the cached function pointer and never touch the disk.
- `FreeLibrary` is never called for P/Invoke-loaded libraries during normal
  process execution. The OS keeps the DLL mapped until process exit.

#### Why this matters for a long-running service

Because loading is **lazy** (triggered by the first `SqlConnection.Open()`, not at
process startup), the `LoadLibrary` call can happen at any point during the service's
lifetime — potentially hours after startup. Whether the load succeeds depends on
filesystem state, AV scanner activity, and environment **at that moment**.
Subsequent connections reuse the cached pointer and never touch the disk, which is
why affected services often recover after a process restart that re-loads SNI.dll
under favourable conditions.

---

### All scenarios where error `0x8007007E` can occur

`0x8007007E = HRESULT_FROM_WIN32(ERROR_MOD_NOT_FOUND)` — Win32 error 126,
"The specified module could not be found."

The runtime converts this to `DllNotFoundException` inside
`LoadLibErrorTracker::Throw()` (nativelibrary.cpp):

```cpp
HRESULT theHRESULT = GetHR();
if (theHRESULT == HRESULT_FROM_WIN32(ERROR_BAD_EXE_FORMAT))
    COMPlusThrow(kBadImageFormatException);
else
{
    SString hrString;
    GetHRMsg(theHRESULT, hrString);
    COMPlusThrow(kDllNotFoundException, IDS_EE_NDIRECT_LOADLIB_WIN,
                 libraryNameOrPath.GetUnicode(), hrString);
}
```

The error tracker assigns priorities when multiple probes are attempted:

```cpp
case ERROR_FILE_NOT_FOUND:
case ERROR_PATH_NOT_FOUND:
case ERROR_MOD_NOT_FOUND:    // priority 10 — lowest
case ERROR_DLL_NOT_FOUND:
    priority = const_priorityNotFound;
    break;
case ERROR_ACCESS_DENIED:
    priority = const_priorityAccessDenied;  // priority 20
default:
    priority = const_priorityCouldNotLoad;  // priority 99999 — highest
```

The tracker keeps only the highest-priority error. Seeing `0x8007007E` in the
exception means **every** search strategy returned "not found" or lower — no probe
hit a higher-priority error such as access denied or a corrupt image.

#### Search order that must exhaust before the exception is thrown

`LoadNativeLibraryBySearch()` probes these locations in order:

1. Windows API sets (skipped — SNI is not an API set name)
2. Directories in `NATIVE_DLL_SEARCH_DIRECTORIES` (set by the host)
3. The P/Invoke assembly's own directory (the `.dll`/`.exe` folder)
4. `LoadLibraryEx` with `dllImportSearchPathFlags` (assembly-level or default)

All of these must fail for `0x8007007E` to be reported.

#### Scenario A — SNI DLL not deployed

The file is simply absent from all probed paths. This is the most common root
cause when a deployment is incomplete (missing the
`runtimes/win-x64/native/` or `runtimes/win-x86/native/` subtree from the NuGet
layout). Results in a consistent, non-intermittent failure on every
`SqlConnection.Open()`.

#### Scenario B — Architecture mismatch: no matching-arch DLL present

A 32-bit service process on a machine that has only the x64 NuGet layout (or vice
versa). The runtime probes for a DLL whose bitness matches the process, finds
none, and reports `0x8007007E`. If the wrong-arch DLL is found and Windows tries
to load it into the mismatched process, `ERROR_BAD_EXE_FORMAT` (193) is returned
instead, producing `BadImageFormatException` rather than `DllNotFoundException`.
Getting `0x8007007E` with an arch mismatch therefore means there is no DLL at all
for that architecture — not even a wrong-arch one.

This is the scenario most consistent with issue #3148's description: a Windows
service that runs as 32-bit (`x86`) on a machine whose deployment only includes
the `win-x64` NuGet runtime assets.

#### Scenario C — A dependency of SNI.dll is missing

`Microsoft.Data.SqlClient.SNI.dll` is a native C++ DLL with its own import table.
It depends on system DLLs and the Visual C++ runtime (`VCRUNTIME140.dll`,
`MSVCP140.dll`, etc.). When `LoadLibrary` maps SNI.dll, the Windows loader
recursively resolves its imports. If any **direct or transitive** dependency is
missing, the Windows loader returns `ERROR_MOD_NOT_FOUND` for the outer call to
`SNI.dll` — even though SNI.dll itself was found on disk. The missing dependency
is not identified in the exception message.

Common in stripped server images, minimal Docker base images, or servers that
lack the Visual C++ redistributable.

#### Scenario D — Single-file app extraction race

When published with `PublishSingleFile=true` and
`IncludeNativeLibrariesForSelfExtract=true`, native DLLs are extracted to a
temp directory at startup (e.g.,
`%TEMP%\.net\<appname>\<contenthash>\`). Extraction and `LoadLibrary` are two
distinct operations. If a cleanup tool, AV scanner, or another process removes
the extracted file in the window between extraction and the first
`SqlConnection.Open()`, the file is gone when `LoadLibrary` is called →
`ERROR_MOD_NOT_FOUND`. This is intermittent and can occur hours after startup
if the service's first database connection is delayed.

#### Scenario E — AV/EDR scan-on-access interception

Some endpoint security products intercept `LoadLibrary` calls by briefly renaming
or locking the target file during an on-access scan. From the calling process's
perspective the file disappears between the search pass and the map pass, and the
OS loader reports `ERROR_MOD_NOT_FOUND`. This failure is intermittent and
timing-dependent — it occurs only when the AV scanner's scan window overlaps with
the `LoadLibrary` call, which can happen at any point during the process lifetime
(including long after startup).

This is the scenario the `--stress-load-absent` mode's rename-window mechanism is designed to
simulate.

#### Scenario F — Directory-level ACL change after deployment

If the runtime probes the assembly directory but the **directory** itself (not just
the DLL file) loses read/traverse permission after deployment, the probe silently
finds nothing and reports `ERROR_MOD_NOT_FOUND`. If the DLL file itself loses read
permission, `ERROR_ACCESS_DENIED` is reported instead (and would override
`0x8007007E` in the error tracker). The directory case therefore produces
`0x8007007E` even though the file physically exists.

#### Scenario G — SxS/manifest activation failure (rare)

`Microsoft.Data.SqlClient.SNI.dll` ships with an embedded side-by-side manifest
specifying a VC++ runtime version. If the activation context creation fails (e.g.,
the required VC++ merge module is not installed on the system), Windows returns
`ERROR_MOD_NOT_FOUND` for the outer `LoadLibrary` call rather than a more specific
SxS error. This is uncommon in modern deployments but can occur on images built
with minimal Windows component selection.

#### Summary

| Scenario | Error consistent? | Intermittent? | Most relevant to issue #3148? |
|---|---|---|---|
| **A** — SNI DLL not deployed | Consistent | No | Unlikely (service ran before) |
| **B** — Arch mismatch, no matching-arch DLL | Consistent | No | **Yes — 32-bit service, x64-only deploy** |
| **C** — VC++ runtime dependency missing | Consistent | No | Possible on minimal images |
| **D** — Single-file extraction race | Intermittent | Yes | Yes — if using single-file publish |
| **E** — AV/EDR scan-on-access | Intermittent | Yes | **Yes — matches intermittent pattern** |
| **F** — Directory ACL change | Intermittent | Yes | Possible in hardened environments |
| **G** — SxS/manifest version mismatch | Consistent | No | Unlikely |

Scenarios B, D, and E are the primary candidates for the "works after restart,
fails intermittently in a long-running service" pattern described in issue #3148.

---

### Interpreting the long-running-process symptom

Issue #3148 describes a process that runs for a long time, performs database work,
and only later reports `0x8007007E` for `Microsoft.Data.SqlClient.SNI.dll`.

Given the runtime behavior above (native DLL load is cached per process and never
reloaded), a true "loaded successfully and then failed later in the same process"
sequence is not expected in normal operation.

In practice, the symptom usually means one of the following:

1. The first SNI load was delayed (lazy load), so the process had been running but
  had not yet reached the first `SqlConnection.Open()` that triggers `LoadLibrary`.
2. The reported failure came from another process instance, worker, or recycle event,
  while logs were interpreted as one continuous process.
3. A non-standard event occurred (for example process/host recycle, unusual loading
  context transitions, or environmental interference around first load).

#### What "something extraordinary happens" means

In this context, "extraordinary" means an event that breaks the normal assumption
of one stable OS process with one successful first native load that remains cached.

Concrete examples:

1. **Silent process replacement (most common)**
  - Service restart, app pool recycle, container restart, watchdog restart, crash-restart.
  - Operationally appears as one continuous service, but it is a *new process* doing
    a fresh first `LoadLibrary`.
  - Evidence to collect: PID changes, startup timestamps, service control events,
    container instance IDs.

2. **"Successful earlier work" did not include SNI load yet**
  - Service was active for hours, but first SqlClient path was delayed.
  - Failure appears "late" only because first `SqlConnection.Open()` happened late.
  - Evidence to collect: application timeline showing first DB call aligns with failure.

3. **Failure occurred in a different worker process**
  - Parent process appears healthy; child worker/sidecar process is the one failing
    its first load.
  - Evidence to collect: executable path + PID in logs for each failure record.

4. **Host environment changed before a new first-load event**
  - During recycle/deploy window, path/deployment/temp/AV state changed.
  - New process then fails first load with `0x8007007E`.
  - Evidence to collect: deployment history, temp cleanup tasks, AV quarantine logs,
    file inventory before/after restart.

5. **In-process native tampering (rare)**
  - Injected tools/hooks alter loader behavior or call `FreeLibrary` unexpectedly.
  - This can violate normal runtime invariants.
  - Evidence to collect: unexpected native modules, endpoint instrumentation,
    debugger/injector presence.

6. **Severe memory corruption (very rare)**
  - Unsafe/native corruption damages runtime or loader state.
  - Not specific to SNI; usually accompanied by broader instability.
  - Evidence to collect: access violations, Watson dumps, random unrelated faults.

7. **Later code path loads an additional native dependency (edge case)**
  - SNI may be present, but a dependency touched by a later feature path is missing.
  - Can look like "worked earlier, failed later" if paths differ.
  - Evidence to collect: feature-specific repro matrix; compare auth/query paths.

#### Most likely root-cause pattern

The strongest fit for "works for a while, then fails" is delayed first load plus
environment drift before the first database call:

- Service starts and does non-database work.
- SNI is not loaded yet (lazy native load path).
- Temp cleanup, deployment drift, architecture mismatch exposure, or AV timing
  changes the load environment.
- First connection attempt happens later and fails with `0x8007007E`.

This pattern maps directly to scenarios B, D, and E:

- **B**: no matching-architecture SNI DLL is available for the process bitness.
- **D**: single-file extracted native payload under `%TEMP%` is removed before first load.
- **E**: AV/EDR transiently interferes with the file at load time.

#### How to reason about scenario C in this symptom pattern

Scenario C (missing transitive dependency such as VC++ runtime) is still possible,
but it is usually consistent rather than intermittent. It appears "delayed" only
when the first connection is delayed. Once the process has loaded SNI successfully,
scenario C should not reappear inside that same process lifetime.

#### Why this matters for reproductions

Because SNI load is one-time per process, reproductions should focus on the first
load window, not steady-state `Open()/Close()` loops after successful load.

That is why the tooling in this folder separates:

- load-window probes (`--probe-*` modes), and
- load-time interference stress (`--stress-load-absent`).

These modes are designed to diagnose first-load failures that surface as
`0x8007007E`, including cases that appear as "long-running" in operational logs.
