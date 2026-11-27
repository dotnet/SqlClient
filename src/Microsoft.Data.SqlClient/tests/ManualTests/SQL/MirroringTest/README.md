# Failover login regression tests

## Purpose and coverage

These real-server tests guard the fix for [SqlClient issue #5029](https://github.com/dotnet/SqlClient/issues/5029):

- [BasicAvailabilityGroupFailoverTest](./BasicAvailabilityGroupFailoverTest.cs) verifies a listenerless, clusterless Basic AG with a synchronized, non-readable secondary.
- [DatabaseMirroringFailoverTest](./DatabaseMirroringFailoverTest.cs) verifies genuine synchronized, synchronous (`FULL`), witnessless database mirroring using native Windows SNI.

Both belong only to the [ManualTests project](../../Microsoft.Data.SqlClient.ManualTests.csproj). There is no separate failover/comparison project or historical-version override. They build through the existing `BuildSqlClientTestsManual`, `BuildTests`, and `BuildAll` targets without requiring a running lab.

Use the repository SDK and run `dotnet tool restore` from the checkout root before building. The manual suite also builds the driver reference project, whose documentation processing requires the declared PowerShell tool. See the [build prerequisites](../../../../../../BUILDGUIDE.md#prerequisites).

Each theory covers **12 cases per role phase**: unset/false/true compatibility switch, pooling off/on, and synchronous/asynchronous opens. Each case checks direct primary/principal access before opening the secondary/mirror with the primary/principal supplied as `Failover Partner`.

| Switch | Basic AG | Database mirroring |
| --- | --- | --- |
| Unset or true | Reach the primary partner and verify the actual server | Reach the principal and verify the actual server/database |
| Explicit false | Assert login rejection with error 976/class 14 | Assert login rejection with errors 4060/class 11 and 18456/class 14 |

The tests restore the switch cache through the [shared helper](../../../Common/LocalAppContextSwitchesHelper.cs) and disable collection parallelization. Unique application names avoid reusing learned pool-group partner selection across cases.

`Failover Partner` is a database-mirroring feature. The Basic AG scenario investigates compatibility with the reported deployment; it is not an AG deployment recommendation. Use AG listeners for supported AG connectivity. The [simulator unit tests](../../../UnitTests/SimulatedServerTests/ConnectionFailoverTests.cs) separately isolate client retry behavior without real SQL servers.

## Recorded regression analysis

The investigation compared released 7.0.0, released 7.1.1, and the unfixed checkout against the same two-server Windows mirroring lab. Original comparisons used Windows x64/.NET **8.0.34**, native SNI, SQL Server 2019 Developer **15.0.2000.5**, and synchronized `FULL` mirroring without a witness. Each switch setting covered pooling and sync/async combinations before failover, after failover, and after failback.

| Original driver | Explicit false | Explicit true |
| --- | --- | --- |
| Released 7.0.0 | 12/12 passed | 12/12 passed; this release ignores the newer switch |
| Released 7.1.1 | 0/12 passed | 12/12 passed |
| Unfixed 7.1.1-dev checkout | 0/12 passed | 12/12 passed |

**72 cases executed: 48 passed and 24 failed.** Failures were mirror-to-partner opens with false in 7.1.1/the unfixed checkout, returning **4060/class 11 followed by 18456/class 14**. Direct-principal access passed in every case. No prelogin timeouts or skipped enabled cases occurred.

The loaded informational versions were `7.0.0+48b82622d7463aae8e9601bec6b6a20f2e47e2b0`, `7.1.1+29ce2d856cb5a9450f79ea62c0afc744fd337fde`, and `7.1.1-dev+9f12f895991ff53f17d91cde2f7193209fccde13`. Released clients selected Windows/net8.0 runtime assets. These observations apply to these specific versions, runtime, SQL build, and topology.

The parser-state guard introduced by [PR #4140](https://github.com/dotnet/SqlClient/pull/4140) stopped partner alternation for login-phase SQL errors when the compatibility switch was false. Listenerless Linux Basic AGs exposed the same blocked alternation with **976/class 14**.

The fix changes only the default of `Switch.Microsoft.Data.SqlClient.UseLegacyFailoverAlternationOnLoginSqlErrors` from false to true. Unset now restores earlier partner alternation; explicit false retains the guard and its primary-retry behavior. This is a compatibility restoration, not a new error-code classification. Existing terminal authentication-error and timeout stop conditions remain unchanged.

After the fix, all three role phases passed **144/144 checks**: 36 each against SQL 2019/2022/2025 Standard Linux BAGs (`15.0.4510.2`, `16.0.4165.4`, `17.0.5065.9`) and Windows SQL 2019 Developer mirroring (`15.0.2000.5`). These comprised 96 successful unset/true partner connections and 48 asserted false rejections.

A later one-phase check on native Windows/.NET **10.0.15** passed 12/12 checkout cases and 8/8 released 7.0.0 cases; released 7.1.1 again failed its four false cases with 4060/18456 and passed its four true cases. Those historical comparisons are recorded evidence, not a capability retained in the current suite. Current-driver tests remain to prevent future regressions.

The unsupported Windows container deployment and SQL Server RTM build are diagnostic limitations. Exact WSFC-backed customer topology and current-CU Windows VM validation were not established by these results.

## Lab dependency

Infrastructure lives in the separate [SqlClient Samples repository](https://github.com/dotnet/sqlclient-samples), under `FailoverLab/`. Follow its `FailoverLab/README.md` for license acceptance, disposable runtime credentials, setup, synchronization, role changes, and cleanup.

There is no build-time dependency on Samples or inferred sibling checkout location. Both lab runners invoke the manual suite on **.NET 10** and select only the appropriate theory. Windows mirroring must run from native Windows .NET, not WSL. Equivalent real SQL servers can be used if they provide the database names, topology, metadata access, and SQL authentication required by the tests.

Test commands never change roles. After a prepared lab's initial phase, switch the primary/principal through the lab script, rerun the tests, then switch back and rerun. Keep lab credentials out of source files, command-line arguments, logs, and source control.

### Recommended lab commands

After preparing the Linux BAG lab, from the Samples checkout:

```bash
export SQLCLIENT_REPO_ROOT=/path/to/SqlClient
bash FailoverLab/lab.sh test
```

After preparing the Windows mirroring lab, from Windows PowerShell:

```powershell
$SqlClientRoot = 'C:\src\SqlClient'
$Lab = 'C:\src\sqlclient-samples\FailoverLab\windows\lab.ps1'
powershell -NoProfile -ExecutionPolicy Bypass -File $Lab test -SqlClientRoot $SqlClientRoot
```

The scripts supply the corresponding opt-in, credentials/endpoints, and class filter. The Windows runner checks its fresh TRX report for exactly 12 executed/passing cases and rejects missing reports, skipped/partial runs, and failures. Its TRX reports live under `%LOCALAPPDATA%\sqlclient-50263-mirroring-2019\test-artifacts\TestResults\`. Builds use the normal repository output/intermediate layout with configuration `Debug-FailoverWindows`, separate from ordinary Debug binaries. Do not build the same worktree concurrently from Windows and Linux: restore intermediates are shared.

### Direct manual-suite execution

From the SqlClient checkout, with the corresponding lab inputs set:

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTests.csproj --framework net10.0 --filter "FullyQualifiedName~BasicAvailabilityGroupFailoverTest" --logger trx --blame-hang-timeout 2m
```

```powershell
dotnet test src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTests.csproj --framework net10.0 --filter "FullyQualifiedName~DatabaseMirroringFailoverTest" --logger trx --blame-hang-timeout 2m
```

The [build orchestrator](../../../../../../build.proj) also supports `TestSqlClientManual` with `TestFramework=net10.0` and `TestFilters=FullyQualifiedName~BasicAvailabilityGroupFailoverTest` or `FullyQualifiedName~DatabaseMirroringFailoverTest`. Do not select a `TestSet`: these lab theories intentionally have no Set trait.

Both theories have `category=interactive`, excluded by normal orchestrated test filters. Explicitly selecting their class as above includes them, but they still **skip without their opt-in**. A skipped run is not evidence that failover works. Unrelated manual-test discovery may emit configuration diagnostics; judge the selected tests and TRX results rather than treating those messages as successful lab checks.

| Input | Meaning |
| --- | --- |
| `SQLCLIENT_BAG_TESTS=1` | Enable the BAG theory |
| `SQLCLIENT_BAG_PASSWORD` | Disposable runtime SQL `sa` credential |
| `SQLCLIENT_BAG_SERVER1`, `SQLCLIENT_BAG_SERVER2` | BAG endpoints; the script discovers container IPs, while direct tests default to loopback ports 15191/15192 |
| `SQLCLIENT_MIRROR_TESTS=1` | Enable the mirroring theory |
| `SQLCLIENT_MIRROR_PASSWORD` | Disposable runtime SQL `sa` credential |
| `SQLCLIENT_MIRROR_SERVER1`, `SQLCLIENT_MIRROR_SERVER2` | Explicit reachable principal/mirror endpoints |
| `SQLCLIENT_BAG_ACCEPT_EULA`, `SQLCLIENT_MIRROR_ACCEPT_EULA` | Required by the corresponding lab script, not the theory; accept the license before setting them |

Unset-switch coverage requires no existing AppContext override. Run mirroring tests on .NET 10; do not run the opted-in theory across all of the manual project's target frameworks.
