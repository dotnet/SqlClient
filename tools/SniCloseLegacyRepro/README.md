# SniCloseLegacyRepro

Diagnostic harness that runs **SNIClose / MARS close-during-I/O deadlock tests** against the
**legacy `System.Data.SqlClient`** driver, to see whether the deadlock from **ICM 775308542 /
ADO.Net #43847** ("SNIClose can deadlock with in-flight async I/O during connection close")
reproduces there.

---

## Background

ICM 775308542 / ADO.Net #43847 concern a potential SNIClose deadlock with
in-flight async I/O during connection close in the **retired, in-box
`System.Data.SqlClient`**. The environment reported in the ICM:

| | Reported environment (ICM) |
|---|---|
| OS | Windows Server 2016 (10.0.14393) |
| Runtime | .NET Framework 4.7 (CLR 4.0.30319) |
| Driver | in-box `System.Data.dll` **4.7.4081.0** |
| CPUs | 4 |

`System.Data.SqlClient` is retired, so the fix (if any) is being investigated in
`Microsoft.Data.SqlClient` via #43847. The branch adds regression tests on the
MDS side; this harness points those same tests at the legacy driver.

---

## How it works

Rather than re-implementing the tests, the project **links the real test source
files** and swaps only the driver reference:

- **Linked tests** (compiled verbatim, not copied):
  - `SNICloseDeadlockTest.cs` (from `tests/UnitTests/SimulatedServerTests`) —
    3 in-process tests: pending async read (via the in-repo TDS test server),
    pre-login handshake, and TLS handshake (both via raw `TcpListener`).
  - `SNICloseRaceDeadlockTest.cs` (same folder) — releases the server response
    at the same instant as close so the read completion races `SNIClose`
    (×100 iterations).
  - `MarsCloseDeadlockTest.cs` (from `tests/ManualTests/SQL/AsyncTest`) —
    live-server MARS pending-read test.
  - `MarsCloseStressTest.cs` (same folder) — many MARS connections with
    in-flight reads, all closed simultaneously over several iterations
    (live server).
- **Compat shim** makes the legacy driver satisfy the linked files' bare
  `Microsoft.Data.SqlClient` type names:
  - `GlobalUsings.cs` — global `using` aliases mapping `SqlConnection`,
    `SqlCommand`, `SqlDataReader` to `System.Data.SqlClient`, and
    `SqlConnectionStringBuilder` / `SqlConnectionEncryptOption` to the shims.
  - `Compat.cs` — a `SqlConnectionEncryptOption` enum stand-in, a
    `SqlConnectionStringBuilder` facade over the (sealed) legacy builder that
    accepts the enum-typed `Encrypt`, and a `DataTestUtility` stand-in that
    reads the connection string from `SNICLOSE_CONNSTR`.

All four test files are the **real driver tests**; this project only links them
and swaps the driver reference. (The race/stress tests were originally prototyped
here and have since been promoted into the driver test projects.)

The bare-name binding trick: the linked files sit in `Microsoft.Data.SqlClient.*`
namespaces, and because this project does **not** reference MDS, unqualified
names like `SqlConnection` fall through to the global aliases in `GlobalUsings.cs`.

### Why `xunit.runner.json` (shadowCopy: false) matters

`Microsoft.DotNet.XUnitExtensions` (which provides `[ConditionalTheory]`) ships a
**delay-signed** beta assembly. On .NET Framework, xUnit's default shadow copy
re-verifies the strong name from the shadow-copy directory and fails with
`FileLoadException: strong name could not be verified`. The real test projects
ship `xunit.runner.json` with `shadowCopy: false`; this harness **links that same
file** so the delay-signed assembly loads from its build-output location (where
.NET Framework's strong-name bypass applies).

---

## Target frameworks

`net462;net47;net472;net481;net10.0`

- **.NET Framework targets** use the **in-box** `System.Data.dll`.
- **.NET (Core) target** (`net10.0`) uses the **`System.Data.SqlClient` NuGet
  package** (version parametrized — see below).

> **Important:** the in-box `System.Data.dll` is *machine-global* for the whole
> 4.x line — only one physical DLL exists per machine. On this dev box that is
> 4.8.1, so every netfx target binds 4.8.1 at runtime; the older TFMs only change
> compile-time references and framework "quirk" behaviors, **not** the DLL bits.
> To run the exact reported 4.7.4081.0 bits you need an environment isolated to
> .NET 4.7.2 (see [Testing older in-box DLLs](#testing-older-in-box-dlls-containers)).

---

## Running

From the repo root.

### In-process tests only (no SQL Server)

```powershell
dotnet test .\tools\SniCloseLegacyRepro
# or a single framework:
dotnet test .\tools\SniCloseLegacyRepro -f net481
```

The live MARS test and the MARS stress test are skipped when no connection
string is set.

### With a live SQL Server (enables MARS + stress tests)

Set `SNICLOSE_CONNSTR` yourself so the password is not routed through tooling:

```powershell
$env:SNICLOSE_CONNSTR = "Server=127.0.0.1,1434;Database=master;User ID=<user>;Password=<pwd>;TrustServerCertificate=true;Connect Timeout=15"
dotnet test .\tools\SniCloseLegacyRepro -f net481
Remove-Item Env:\SNICLOSE_CONNSTR   # clear when done
```

### Sweeping legacy NuGet versions (net10.0 only)

The `.NET (Core)` target's package version is parametrized via
`$(SqlClientLegacyVersion)` (default `4.9.1`). The versions must exist on a
restore feed.

```powershell
foreach ($v in '4.5.3','4.6.1','4.7.0','4.8.6','4.9.1') {
  dotnet test .\tools\SniCloseLegacyRepro -f net10.0 -p:SqlClientLegacyVersion=$v
}
```

### Emulating the reported 4-CPU box

Pin the test process (and its child `testhost`) to 4 cores:

```powershell
$p = Start-Process dotnet -ArgumentList 'test','.\tools\SniCloseLegacyRepro','-f','net47','--no-build' -PassThru -NoNewWindow
$p.ProcessorAffinity = [IntPtr]0xF   # cores 0-3
$p.WaitForExit()
```

---

## Testing older in-box DLLs (containers)

Because the in-box `System.Data.dll` is machine-global, the faithful way to test
older bits is an isolated environment. Use the official .NET Framework images —
the **framework version tag controls `System.Data.dll`**; the **Windows base tag
controls the OS + native `sni.dll`**.

```
mcr.microsoft.com/dotnet/framework/runtime:<fwver>-windowsservercore-<base>
```

| Image tag | `System.Data.dll` ≈ | Role |
|---|---|---|
| `4.6.2-windowsservercore-ltsc2016` | 4.7.4081.0 (see note) | — |
| `4.7-windowsservercore-ltsc2016` | **4.7.4081.0** | **ICM match** |
| `4.7.2-windowsservercore-ltsc2016` | 4.7.4081.0 | ICM match |
| `4.8-windowsservercore-ltsc2019` | 4.8.4690.0 | newer |
| `4.8.1-windowsservercore-ltsc2022` | 4.8.9xxx | this dev box |

> **.NET Framework 4.x is a single, in-place, monotonic runtime.** The `ltsc2016`
> base OS is already serviced to 4.7.2, so its in-box `System.Data.dll` is
> **4.7.4081.0** — and a lower framework tag (e.g. `4.6.2`) cannot downgrade it.
> On this base, every tag at or below 4.7.2 resolves to the same 4.7.4081.0 DLL,
> which happens to be the reported version (the practical floor). Getting
> a genuinely older DLL (e.g. 4.6.x) would require an *unserviced* older base OS
> image, which MCR does not publish.

Verify the DLL version inside a container:

```powershell
(Get-Item C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll).VersionInfo.FileVersion
```

### `run-in-container.ps1`

The framework `runtime` images have **no `dotnet` CLI**, so `dotnet test` will not
work there. The `run-in-container.ps1` helper handles this: it builds `-f net47`
on the host, stages `xunit.console.exe` (from `xunit.runner.console`) next to the
build output, and runs the tests in the container so they bind the container's
old in-box `System.Data.dll`.

```powershell
# in-process tests only (no SQL Server):
.\run-in-container.ps1

# faithful ICM match: 4.7.4081.0 + 4 processors, live MARS + stress:
$env:SNICLOSE_CONNSTR = "Server=127.0.0.1,1434;Database=master;User ID=sa;Password=***;TrustServerCertificate=true"
.\run-in-container.ps1 -Cpus 4

# a different framework image:
.\run-in-container.ps1 -Image mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
```

**Networking:** a Windows container cannot reach the host's loopback, and
`host.docker.internal` is not injected for hyperv-isolated Windows containers.
The script targets the Docker NAT gateway (the host's `vEthernet (nat)` IP, e.g.
`172.17.224.1`) and rewrites the connection string to it. Because the SQL Server
(a Linux container published to host loopback) does not listen on that IP, add a
one-time **elevated** host bridge:

```powershell
netsh interface portproxy add v4tov4 listenaddress=172.17.224.1 listenport=1434 connectaddress=127.0.0.1 connectport=1434
New-NetFirewallRule -DisplayName "SQL 1434 from Windows containers" -Direction Inbound -LocalAddress 172.17.224.1 -LocalPort 1434 -Protocol TCP -Action Allow
# teardown:
#   netsh interface portproxy delete v4tov4 listenaddress=172.17.224.1 listenport=1434
#   Remove-NetFirewallRule -DisplayName "SQL 1434 from Windows containers"
```

---

## Findings

Across every configuration tested, the deadlock **does not reproduce** (all
tests pass), including:

- in-box `System.Data.dll` 4.8.1 on `net462` / `net47` / `net472` / `net481`
  (host dev box)
- 4-CPU-pinned runs
- the completion-races-close and MARS concurrency stress variants
- the full `System.Data.SqlClient` NuGet lineage on `net10.0`
  (4.5.3, 4.6.1, 4.7.0, 4.8.6, 4.9.1)
- **the reported (ICM) environment**, reproduced faithfully in a container:
  Windows Server 2016 base (`ltsc2016`) · .NET Framework 4.7 · in-box
  `System.Data.dll` **4.7.4081.0** · **4 processors** (`--cpu-count 4`) · live
  MARS + concurrency stress against SQL Server 2025 → **12/12 pass**.
- container `ltsc2019` · .NET Framework 4.8 · `System.Data.dll` 4.8.4690.0 ·
  4 processors · live MARS + stress → **12/12 pass**.

So the SNIClose close-during-I/O deadlock does not reproduce even in a
bit-for-bit reconstruction of the reported ICM environment. Because that
*exact* `System.Data.dll` (4.7.4081.0) also passes, there is no
version boundary / "fixed in a later servicing build" explanation: the synthetic
tests simply do not trigger whatever the original incident hit. That points away
from a plain client-side ordering bug reachable from managed timing alone, and
toward something more environment-specific in the original incident (e.g. exact
network/timing conditions, or a native-layer interaction not exercised here).

---

## Isolation notes

- Uses Central Package Management via its own `Directory.Build.props`
  (`ManagePackageVersionsCentrally=true`) and `Directory.Packages.props`, which
  imports the shared SqlClient tests package versions. The legacy
  `System.Data.SqlClient` package is pinned there; the net10.0 target overrides
  it per-run via `VersionOverride` / `-p:SqlClientLegacyVersion=…`.
- Legacy `System.Data.SqlClient` versions are served from the repo's governed
  feed (no extra `NuGet.config`).
- References the driver-agnostic in-repo TDS test-server projects
  (`tests/tools/TDS/*`).
