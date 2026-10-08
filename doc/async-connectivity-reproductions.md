# Async connectivity reproductions

This focused suite retains five demonstrated mechanisms from
[#3459](https://github.com/dotnet/SqlClient/issues/3459), plus the controls and
test infrastructure needed to distinguish defects from inadmissible setups.
It changes no production behavior or public APIs.

## Retained reproductions

Test classes are under
`src/Microsoft.Data.SqlClient/tests/UnitTests/SimulatedServerTests/`.

| Mechanism | Desired-behavior test | Local baseline and essential controls |
|---|---|---|
| Async authentication starvation | `ConnectivityProcessTests.OpenAsync_TokenBurst_WithCappedWorkers_Completes` | Every V2 worker reaches held Login7. After Login7 release, zero workers are available and the token callback cannot enter. Generous-worker V2 and capped V1/non-pooled controls complete. |
| Additional user-thread OpenAsync behind occupied workers | `ConnectivityProcessTests.OpenAsync_AdditionalOpen_WithCappedWorkers_MakesProgress` | A non-pool caller obtains a Task, but physical login to a separate, unheld endpoint cannot progress until the original Login7 replies release workers. The recovery control completes every open under the unchanged cap. This covers the starvation mechanism in [#3118](https://github.com/dotnet/SqlClient/issues/3118), not its exact TLS/error signature. |
| Synchronous provider-authentication worker starvation | `ConnectivityAuthenticationPressureTests.Open_ProviderAuthentication_WithCappedWorkerCallers_Completes` | Cold and genuinely expired fake managed-identity tokens stall both pools before provider entry with zero workers available. Increasing workers during cleanup completes the same callers. Warm-cache, generous-worker, dedicated-caller, and direct-async controls distinguish worker pressure from provider/cache failures. Relevant to [#2152](https://github.com/dotnet/SqlClient/issues/2152)/[#2470](https://github.com/dotnet/SqlClient/issues/2470), which primarily report synchronous Open. |
| Physical TCP attempts continue after public cancellation | `ConnectivityTransportCancellationTests.OpenAsync_RefusedConnect_AfterPublicCancellation_StartsNoNewAttempt` | Gate the first actual refused TCP connect, confirm public cancellation, then release physical work. Attempts grow from 1 to 13; the desired count remains 1. An uncancelled control proves the internal retry path is reachable with configurable retries disabled. Relevant to [#1619](https://github.com/dotnet/SqlClient/issues/1619). |
| Cancelled-scope rollback/physical-open lock inversion | `ConnectivityTransactionTests.OpenAsync_AmbientTransaction_CancelledAfterBegin_ScopeAndPhysicalWorkComplete` | Real BEGIN completes before a post-BEGIN gate. After public cancellation, rollback owns the connection monitor while physical opening owns the parser monitor. Releasing the gate produces `LOCK_CYCLE_STALLED`. Post-BEGIN, pre-enlistment cancellation, held-BEGIN, and actual sync/async commit/rollback controls establish phase ordering and wire validity. Relevant to [#4696](https://github.com/dotnet/SqlClient/issues/4696). |

**Keep both `ConnectivityProcessTests` anchors.** Their worker caps are accepted
and verified, and an async-only yield workload first proves that the capped
runtime can schedule genuinely asynchronous work. The peer runs in the parent,
outside the capped pool. Caps cover thread-pool workers, not every managed thread.
Worker availability and pending-task state are diagnostics, not requirements that
a future fix must preserve exhaustion.

The fake provider uses the real connection-string authentication-provider path,
but does not contact Azure. Expiry ages physical and shared cached tokens without
substituting pool clearing. The transaction peer supports only the local
BEGIN/COMMIT/ROLLBACK subset needed here, not DTC or a general SQL engine.
Request round-trip/truncation tests protect that new wire support.

## Running

Use the repository's SDK and build instructions in [BUILDGUIDE](../BUILDGUIDE.md).
The unit project builds and copies `tests/tools/ConnectivityTestHost` automatically.
Run healthy controls first:

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  -f net10.0 --filter 'FullyQualifiedName~Connectivity&category!=failing'
```

The desired-behavior reproductions are deliberately `category=failing`, excluded
by the repository's normal filter. Select them explicitly to capture the current
defects; all eight cases are expected to fail on the current macOS baseline:

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  -f net10.0 --filter 'FullyQualifiedName~Connectivity&category=failing' \
  --logger 'console;verbosity=normal'
```

To examine either anchor alone, replace the filter with
`FullyQualifiedName~OpenAsync_TokenBurst_WithCappedWorkers_Completes` or
`FullyQualifiedName~OpenAsync_AdditionalOpen_WithCappedWorkers_MakesProgress`.
Use `--list-tests` with the same filter to check selection. Once a driver fix
passes a desired-behavior test and its controls, remove that test's `failing`
trait and retain it as ordinary regression coverage.

Every protocol read/phase/completion is bounded by an independent parent
watchdog. Missed setup phases, EOF, child faults, or rejected runtime limits are
test failures, not passing reproductions. Teardown releases gates and kills/reaps
only the owned child when it cannot recover. A lifecycle control explicitly
verifies missing-phase timeout and reaping. After a timed-out protocol read,
cleanup must not issue another read on that stream.

## Evidence limits

Local verification uses macOS/.NET 10, ephemeral loopback peers, and fake tokens:
no SQL Server, Azure credentials, certificates, or machine network changes.
Trace gates and reflection probe the current source's physical-open tasks,
authentication caches, transaction promoter, and monitors. Missing members or
trace boundaries fail explicitly; these probes are not a general released-package
comparison harness. Monitor ownership and captured thread states establish the
local lock cycle, but external opposing blocked stacks were not collected.
These mechanisms do not establish every customer's exact environment or cause.

The historical [#2192](https://github.com/dotnet/SqlClient/issues/2192) defect was
addressed by [#2915](https://github.com/dotnet/SqlClient/pull/2915), starting with
6.0.0-preview2; it is not an outstanding reproduction target here. Windows UI,
native-SNI, real-service/network confirmations, broad correctness coverage,
benchmarks, and separate timeout-budget/pool-retirement investigations are
intentionally outside this focused change.
