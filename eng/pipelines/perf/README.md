# SqlClient Performance Test Pipeline

This directory contains the Azure DevOps pipeline and supporting scripts that run the
Microsoft.Data.SqlClient [BenchmarkDotNet](https://benchmarkdotnet.org/) performance tests on a
dedicated performance test lab (Azure Dedicated Hosts), compare the branch under test against a
released NuGet baseline, and optionally ingest the results into an Azure Data Explorer (Kusto)
database.

## Contents

| Path | Purpose |
| ---- | ------- |
| `sqlclient-perf-pipeline.yml` | The main (manual/nightly) pipeline. Extends `v1/Perf.Test.Job.yml@PerfTemplates`. Baseline = released NuGet package; ingests into Kusto. |
| `sqlclient-perf-pr-pipeline.yml` | PR pipeline. Same template, same scripts, same options; baseline = **`main` branch source**; **no Kusto ingestion**. |
| `sqlclient-perf-experiment.yml` | Experiment pipeline. Same template, same scripts; both passes build the **same source** and differ only in one runner-config switch; **no Kusto ingestion**. |
| `scripts/run-perf-tests.sh` | Linux on-VM entry point: install SDK, create DB, run benchmarks (interleaved or sequential), compare. Baseline is a released package (`--baseline-version`), another git ref's source (`--baseline-source-ref`), or the same source with one runner-config switch flipped off (`--switch-under-test`). |
| `scripts/run-perf-tests.ps1` | Windows equivalent (ProcessorAffinity instead of `taskset`). |
| `scripts/interleave_perf.py` | Interleaved + best-of-N orchestrator: runs each unit baseline↔candidate back-to-back and confirms regressions across N passes. |
| `scripts/compare_perf.py` | Compares baseline vs current BenchmarkDotNet JSON → delta (md + json). Reused by the orchestrator. |
| `scripts/perf_to_kusto.py` | Translates BenchmarkDotNet "full" JSON → Kusto `PerfRun` + `PerfBenchmarkResult` NDJSON. |
| `scripts/ingest_kusto.py` | Queued Kusto ingestion (az CLI auth, runs on the agent). |
| `scripts/async_health_report.py` | Validated, versioned async-health JSON/Markdown; no timing regression gates or Kusto ingestion. |

## Async-health diagnoser

Add `[AsyncHealthDiagnoser]` to benchmark methods or classes to select them for diagnosis.
Unmarked cases run without async-health observation. Existing `PERF_BENCHMARK`, BDN `--filter`,
parameters, invocation counts, and setup/cleanup still select and execute the same workloads.
`AsyncHealthDiagnoser` requests one extra diagnostic iteration per marked case: normal iterations
collect timing results without probes or a grace-period delay, and only the extra iteration
collects an async-health window. Memory and threading diagnosers remain enabled.
Diagnostic-iteration durations are not performance measurements. No measurement-mode flag,
environment variable or pipeline parameter is required.

This diagnoser answers "how well can unrelated ThreadPool work run while this workload executes?"
It does **not** prove that an API never blocks, that async code is correct, or that synchronous
completion is bad. CPU saturation, GC, locks, OS scheduling and the observer itself also affect
results. Existing sync/async variants retain their original schedulers: for example, cold-ramp
sync uses dedicated threads while async uses ThreadPool tasks. They are not scheduler-matched.

### Execution boundary and coverage

The suite uses BDN **0.16.0-preview.1 InProcessEmitToolchain** in the `PerformanceTests` process spawned by
`interleave_perf.py`, not in the Python orchestrator. `AsyncHealthDiagnoser` implements
`IInProcessDiagnoser` with `RunMode.ExtraIteration`, following `ThreadingDiagnoser`.
Its in-process handler starts observation at `BeforeExtraIteration` and completes it at
`AfterExtraIteration`. No custom engine factory or workload-delegate wrapper is used.
Global/iteration setup and cleanup, JIT/pilot/warmup stages, overhead iterations, and normal
timing iterations are excluded. BDN performs forced GC and a possible background-JIT delay
after the start hook, so this diagnostic window includes that preparation as well as workload
execution. BDN awaits Task/ValueTask completion on its dedicated benchmark thread.
No arbitrary workload is fanned out, and ThreadPool limits/minima are unchanged.

Verified upstream contracts:
[IDiagnoser](https://github.com/dotnet/BenchmarkDotNet/blob/dde87a16370686adf5b33e63eafae0fb5d32374c/src/BenchmarkDotNet/Diagnosers/IDiagnoser.cs),
[Engine](https://github.com/dotnet/BenchmarkDotNet/blob/dde87a16370686adf5b33e63eafae0fb5d32374c/src/BenchmarkDotNet/Engines/Engine.cs),
[ThreadingDiagnoser](https://github.com/dotnet/BenchmarkDotNet/blob/dde87a16370686adf5b33e63eafae0fb5d32374c/src/BenchmarkDotNet/Diagnosers/ThreadingDiagnoser.cs).
Revalidate this contract and the real-process smoke test when upgrading BDN. An out-of-process
BDN toolchain is not supported by this integration.

The diagnostic iteration is one **configured invocation batch** (`UnrollFactor=1`).
There is no automatic duration extension, invocation multiplier, or workload retry to
manufacture coverage. BDN retains the configured normal warmup and iteration counts, then runs
one extra diagnostic iteration with its own setup and cleanup. Stateful/cold tests keep their
existing per-iteration reset boundaries. `TimeoutMinutes` bounds the complete case execution;
a timeout stops the process's benchmark sequence, including non-cooperative invocation batches.
For a repeatable steady-state microbenchmark, choose an explicit `InvocationCount` *before*
running; it must be the same in baseline and candidate. Very short tests will legitimately
produce `Insufficient` results. Do not increase cold/stateful invocation counts to get a p99.

A dedicated observer thread posts tiny timestamped callbacks, default every **25 ms**, with at
most **64 outstanding**. Missed producer deadlines are counted as producer-missed ticks rather than
being replayed as a burst. At workload completion the observer is stopped and joined; an
in-flight observation may finish during shutdown, but no recovery sampling phase is run.
The existing **500 ms** grace period (`RecoveryMilliseconds`) lets accepted callbacks finish
before iteration cleanup. Producer shutdown counts toward this deadline, and no new probes
or resource samples are produced after the observer has joined. Outstanding callbacks at its
end remain explicitly outstanding in a frozen snapshot, even if they execute later. An undrained
window fails the process rather than allowing those probes to contaminate another window. Retained
probe/resource samples are capped at 100,000 each; exceeding the probe budget counts as skipped probes.
No SQL diagnostics, stacks, external collectors, or ThreadPool timers are started.

Raw JSON includes total/posted/executed/dropped/rejected/skipped/producer-missed/outstanding counts, all
retained timestamps, outstanding work at workload end, and a resource timeline (pool thread
count, OS process-thread count, busy-worker proxy, queue depth, completed items, allocated
bytes and Gen2 collections). Pool `Threads` excludes dedicated `LongRunning` and other
non-pool threads. Each window records `ProcessThreadsAtStart` after the observer starts but
before the workload. Summaries show `PeakProcessThreads` during the workload and
`ProcessThreadIncrease`, the nonnegative increase over that baseline, alongside peak pool
threads and busy workers. Absolute totals matter: workload warmup can leave many pool
threads alive before the baseline, so a zero increase does not mean low thread usage.

The probe count `Total` includes all cadence slots, including missed ones:
`Total = ProducerMissed + Dropped + Rejected + Skipped + Outstanding + Executed`, where
`Posted = Outstanding + Executed`. `ProducerMissed` counts skipped cadence slots after a
delayed producer wake; `Dropped` counts probes not dispatched because outstanding capacity is full.
`Rejected` counts dispatch exceptions: the sample and posted count are rolled back, and the
exception still propagates to fail the observer rather than being swallowed.
`Skipped` counts probes not dispatched because sample retention is full, even if outstanding
capacity is also full. Window exports and summaries use schema version 4.
The reporter converts version-1 combined drops into separate missed/drop counts and accepts
the former `Scheduled` count. Version-1/2 drops still include sample-limit rejections:
their skipped counts are unavailable (`N/A`), not inferred as zero. Version-1/2/3 drops still
include dispatch exceptions, so their rejected counts are also unavailable (`N/A`). Comparisons require matching
accounting versions; legacy windows in summaries retain version 2 after missed-count conversion.
Per-probe `ScheduledMilliseconds` timestamps are unchanged.

Process-thread counts include managed, native, runtime, benchmark and observer threads,
including idle threads. They measure existing OS threads, not runnable threads, CPU usage,
thread creation totals, or exact user-thread attribution. Snapshot peaks can miss brief
threads between samples. Enumerating process threads adds observer cost and allocations;
compare matching instrumentation rather than treating older pool-only runs as equivalent.
Older artifacts have no process-thread counts and display `N/A`, never zero, for these fields.

The summary reports posted-to-executed dispatch, scheduled-to-executed delay and producer
lateness distributions separately. Work-posted callbacks that execute during the grace period
remain **late work**, not workload samples. The reporter retains a separate recovery-posted
cohort for older artifacts that collected recovery samples.
Counts cover the complete observation; raw timestamps identify posted/executed cohorts.
An offered probe retains the original due time before missed intervals are skipped. For
example, a 100 ms deadline offered at 160 ms with a 25 ms cadence records 60 ms producer
lateness, two missed intervals, and a next deadline of 175 ms; no catch-up burst is queued.
Scheduled-to-executed delay includes that full producer lateness; posted-to-executed dispatch
delay is unchanged. Older artifacts used the adjusted slot's due time, so their producer
lateness and scheduled-to-executed metrics are not directly comparable after missed intervals.

`Supported` requires the declared minimum duration (default 1 second), the configured
`MinimumSamples` callbacks (default 1) both posted and executed inside the workload window,
no outstanding/late-work probes or dropped resource samples, and no failure. Dropped probes
and missed or skipped probes do not disqualify a window: `DroppedProbes`, `SkippedProbes`, and `ProducerMissedProbes`
(missed posting deadlines) are reported separately. `AdmissionDroppedProbes` remains a
compatibility alias for `DroppedProbes`.
Dispatch exceptions remain failures; their `RejectedProbes` count is reported separately.
The Markdown report shows drops, rejections, skips, producer-missed ticks and outstanding probes;
comparison rows include separate dropped, rejected, skipped, and producer-missed count deltas. Percentiles describe only executed
probes and can be biased when observations are dropped, skipped, or missed. `Supported` does not mean loss-free.
Any positive sample threshold up to
100,000 is accepted; there is no fixed 100-probe floor. Existing explicit thresholds still apply.
`Insufficient`, `Incomplete` (outstanding/late work or lost resource samples), and `Failed` have **null percentiles**, never
zero. Coverage is an accounting check, not a statistical confidence guarantee. Percentiles use
nearest rank: below 100 samples, p99 equals the observed maximum. Use the reported counts and
independent process repetitions to assess variability before interpreting small differences.
Legacy recovery cohorts retain their own duration and sample-support checks. No recovery
sampling phase is performed in current runs. The existing JSON `Recovery` field remains false,
and `RecoveryEndMilliseconds` records the end of the grace period for format compatibility.
Raw data and counts remain available when percentiles are suppressed.

### Configuration and commands

Use the `AsyncHealth` section in `runnerconfig.jsonc` to configure the observer. Select cases
with the attribute from `Microsoft.Data.SqlClient.PerformanceTests`:

```csharp
[Benchmark]
[AsyncHealthDiagnoser]
public Task ReadAsync() => ReadWorkloadAsync();
```

Marked cases require one launch, positive explicit invocation/iteration counts and nonnegative
warmup. Profiler attachment is rejected for marked cases. Sequential/interleaved orchestration,
normal timing comparisons and Kusto ingestion remain available; health sidecars are not timing
rows and are not ingested. There is no dependency on experimental OpenAsync diagnostics or profiles.

For a selected existing unit, from the repository root:

```powershell
dotnet build .\src\Microsoft.Data.SqlClient\tests\PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj -c Release -f net10.0
$env:RUNNER_CONFIG = (Resolve-Path .\my-runner.json).Path
$env:DATATYPES_CONFIG = (Resolve-Path .\src\Microsoft.Data.SqlClient\tests\PerformanceTests\datatypes.json).Path
$env:PERF_BENCHMARK = 'SqlConnection'
dotnet .\src\Microsoft.Data.SqlClient\tests\PerformanceTests\bin\Release\net10.0\PerformanceTests.dll --filter '*OpenAsyncConnection*'
```

Direct execution writes schema-v4 `BenchmarkDotNet.Artifacts\async-health\async-health-*.json`.
BDN's full JSON and Markdown timing reports describe the normal execution without observation.
The extra-iteration measurement is diagnostic, not a workload performance measurement.
Async-health observations
remain separate from performance ingestion in the pipeline. Their configuration identifies
`AsyncHealthDiagnoser` and `DiagnosticRunMode=ExtraIteration` so comparisons do not mix
the former single-run or extra-run harnesses with the extra-iteration lifecycle.

To run enabled units in separate processes and collect timing reports plus diagnostic sidecars:

```powershell
python -B .\eng\pipelines\perf\scripts\interleave_perf.py --current-exe-dir .\src\Microsoft.Data.SqlClient\tests\PerformanceTests\bin\Release\net10.0 --current-runner-config C:\perf\runner.json --results-dir C:\perf\health-results --work-dir C:\perf\health-work
```

Use fresh result/work paths, a dedicated idle host, and absolute config paths.
The orchestrator supplies the built `datatypes.json` unless `DATATYPES_CONFIG` overrides it.
Health sidecars are preserved under each timing pass's `async-health` directory. A current-only
run executes each enabled unit once and makes no baseline comparison. Add `--baseline-exe-dir`
and optionally `--baseline-runner-config` for ordinary timing comparisons. Their confirmation
runs are selected by timing regressions, not an independent health sampling plan. Iterations
within a process are not independent replicates. Use separate declared process runs when
assessing health variability; do not pool window percentiles. Older baseline binaries may
produce only timing reports. Matching workload, invocation count, observer options and runtime
configuration are required before interpreting health differences.

`TimeoutMinutes` retains the per-case watchdog. The orchestrator's optional
`--unit-timeout-minutes` adds a process watchdog. Nonzero process exit codes and timeouts fail
the run; available sidecars from nonzero exits are retained. Durations/completed work/error
types are context, not an async-health score.

For a bounded PR-pipeline run, select `platform=windows`, `currentOnly=true`,
`runnerConfigFile=runnerconfig.connection-smoke.jsonc` and `failOnRegression=false`.
Current-only suppresses source-baseline checkout and tagging without claiming a candidate-vs-main
verdict. Only cases carrying `[AsyncHealthDiagnoser]` emit health sidecars; choosing a workload config
does not attach the diagnoser to additional cases.
It keeps the existing `SqlConnection` parameter matrix with short one-invocation windows and the
existing `ConnectionPoolContention` matrix with a predeclared 100-invocation steady-state batch.
Both use two actual iterations and one warmup. No cold/stateful benchmark is repeated to manufacture
coverage. The shell entry points accept `-RunnerConfigFile` / `--runner-config-file` as a filename
under `PerformanceTests`; the default remains the existing `runnerconfig.jsonc`.

### SQL-free calibration and validation

Enable only the opt-in `AsyncHealthCalibrationRunnerConfig` in a separate config:

```json
{
  "Benchmarks": {
    "AsyncHealthCalibrationRunnerConfig": {
      "Enabled": true, "LaunchCount": 1, "IterationCount": 2,
      "InvocationCount": 1, "WarmupCount": 1, "RunStrategy": "Monitoring"
    }
  }
}
```

Run it with the orchestrator command above. The calibration runner carries `[AsyncHealthDiagnoser]`
at class level, so every calibration benchmark is diagnosed. `ProbeOnly` sleeps the dedicated benchmark thread
for 3 seconds; `AsyncWait` starts 32 three-second async waits; `Blocking` queues 32 pool work
items that each sleep for 1.5 seconds. `AsyncWaitAwaited` explicitly awaits each delay;
`BlockingLongRunning` uses 32 dedicated long-running tasks instead of pool tasks.

Additional opt-in controls retain the same lifecycle and do not change ThreadPool settings:

| Control | Workload |
|---|---|
| `SyncOverAsync` | 32 pool tasks synchronously waiting on 1.5-second `Task.Delay` tasks. |
| `BoundedBlockingFour` / `BoundedBlockingEight` | The same 32 1.5-second sleeps, with async semaphore admission limiting occupied workers to 4 or 8. Expected minimum durations are 12 or 6 seconds. |
| `LockContention` | 32 pool tasks each holding the same lock while sleeping for 100 ms. |
| `MixedOneBlocking` / `MixedFourBlocking` | 32 concurrent three-second waits, with 1 or 4 implemented as pool-thread sleeps and the remainder as async delays. |

Use a separate process and an exact BDN filter such as `--filter '*Runner.SyncOverAsync'`
for each case when studying isolation. To compare fresh-process and workload-warmed pool
behavior, run `Blocking` in separate processes with `WarmupCount=0` versus `WarmupCount=1`.
Even the no-warmup case includes runtime/harness initialization; it is not a guaranteed empty
ThreadPool. Later iterations retain state from earlier ones. No setting forces the pool size.

Expect sensitivity, not a universal latency threshold: hardware, existing pool state and
background activity vary. The controls' lifecycle counters verify that setup/cleanup do not
overlap measurement. A SQL-free real-process test also verifies that normal mode creates no
health probes/artifacts and that warmup is excluded:

```powershell
python -B -m unittest discover -s eng\pipelines\perf\scripts\tests -p 'test_*.py' -q
dotnet test src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj -c Release -f net10.0 --filter 'FullyQualifiedName~AsyncHealthTests' --blame-hang-timeout 10m
$env:PERF_HEALTH_TEST_BINARY = (Resolve-Path src\Microsoft.Data.SqlClient\tests\PerformanceTests\bin\Release\net10.0\PerformanceTests.dll).Path
python -B -m unittest discover -s eng\pipelines\perf\scripts\tests -p 'test_async_health.py' -v
```

For read-only local smoke testing, use `Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true`
and select only existing connection, contention (`SELECT 1`) or cold-ramp benchmarks with bounded
parameters. Do not run the lab provisioning entry points against a developer SQL installation.
Other enabled suite workloads may create fixtures; the mode does not change their permissions
or SQL behavior.

## Architecture

```
Queue pipeline (ADO)
      │
      ▼
extends: v1/Perf.Test.Job.yml@PerfTemplates
      │  (provisions a dedicated-host VM, SCPs the repo, runs the script over SSH,
      │   SCPs <testResultsSubDir> back, publishes it, tears the VM down)
      ▼
ON THE VM  ── run-perf-tests.{sh,ps1}
      1. Install the .NET SDK pinned by global.json (+ the .NET 10.0 runtime), then run
         dotnet tool restore from the repository root before building.
      2. Create the perf database on the VM's SQL Server.
      3. Inject the VM SQL connection string into runnerconfig.
      4. Baseline pass  → MDS <baselineVersion> from the governed feed (Package mode) → results/baseline/
         (PR pipeline: MDS built from the <baselineSourceRef> source tree)
      5. Current  pass  → MDS built from source (ProjectReference)              → results/current/
         (both pinned to PERF_CLIENT_CPUS; interleaved per-unit by default, or two full
          sequential passes when benchmarkRunMode=sequential)
      6. interleave_perf.py / compare_perf.py → results/comparison/ + results/summary.md
      ▼
ON THE AGENT  ── pipeline post-test steps
      • Show the BenchmarkDotNet markdown reports in the log.
      • perf_to_kusto.py → NDJSON for both passes (published as 'perf-kusto-payloads').
      • (optional) AzureCLI@2 + ingest_kusto.py → Kusto database.
      (both Kusto steps are omitted entirely in the PR pipeline)
```

The extends template only exposes **post-test** steps to consumers (no pre-build hook), so **both
benchmark passes run inside the on-VM script**. Translation and ingestion run on the **agent**
because that is where the pipeline's AAD identity / service connection and the native pipeline
context variables are available (the VM is behind NAT and lacks the pipeline identity).

## Parameters

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `platform` | `linux` | `linux` or `windows` VM + client. |
| `dotnetFramework` | `net10.0` | TFM the benchmarks run against (`net10.0` only). |
| `testTimeoutMinutes` | `180` | Template timeout waiting for the VM run. |
| `baselineVersion` | `latest` | Highest stable MDS version in the governed feed, resolved at run time. Pin an explicit version to override. Empty = current-only (no baseline pass / comparison). |
| `regressionThreshold` | `10` | Percent slowdown (current vs baseline mean) flagged as a regression. |
| `failOnRegression` | `false` | When `true`, a candidate-slower regression **fails** the run (gate). In interleaved mode only **confirmed** regressions (best-of-N majority) fail. Default off. |
| `benchmarkRunMode` | `interleaved` | `interleaved` (per-unit baseline↔candidate + best-of-N confirmation) or `sequential` (legacy two full passes). |
| `confirmationRuns` | `3` | Best-of-N: interleaved passes for a flagged unit before a regression is confirmed. `1` disables confirmation. Interleaved mode only. |
| `useManagedSniOnWindows` | `false` | SqlClient `UseManagedSniOnWindows` flag. Applied to the runner config for **both** passes on the VM and recorded in `PerfRun.Config`. Default matches the checked-in `runnerconfig.default.jsonc`. |
| `useOptimizedAsyncBehaviour` | `true` | SqlClient `UseOptimizedAsyncBehaviour` flag. Applied to both passes and recorded in `PerfRun.Config`. Default matches `runnerconfig.default.jsonc`. |
| `useConnectionPoolV2` | `false` | SqlClient `UseConnectionPoolV2` flag. Applied to both passes and recorded in `PerfRun.Config`. Default matches `runnerconfig.default.jsonc`. |
| `enableKustoIngestion` | `true` | **Ingest results into Kusto** — when `false`, the run still benchmarks + compares but skips ingesting into the perf database. When `true`, ingestion additionally requires the `ADX Cluster Variables` group to be populated. |

The following values are **fixed constants** in the pipeline (not parameters or variables), since they
are invariant for this pipeline: `buildConfiguration = Release` and
`driverName = Microsoft.Data.SqlClient` (recorded on every row as `DriverName` / `DerivedRunId`).

All three perf pipelines derive `PerfSourceRoot` from `Build.Repository.Name` to match the
template's multi-repo checkout: `SqlClient` for GitHub's `dotnet/SqlClient`, or `dotnet-sqlclient`
for Azure Repos. The VM source upload and agent-side post-test scripts use this same root.

### Kusto (Azure Data Explorer) ingestion variables

The ADX ingestion coordinates are **not** pipeline parameters — they come from a pipeline library
variable group named **`ADX Cluster Variables`** so no infrastructure identifiers are hard-coded in
the pipeline. The group must define:

| Variable | Description |
| -------- | ----------- |
| `KustoClusterUri` | ADX cluster URI, e.g. `https://<cluster>.<region>.kusto.windows.net`. Empty ⇒ ingestion skipped. |
| `KustoDatabase` | Target Kusto database. Empty ⇒ ingestion skipped. |
| `KustoServiceConnection` | Azure DevOps ARM service connection whose SP has ingest rights. Empty ⇒ ingestion skipped. |

Ingestion is gated at runtime: it only runs when `KustoClusterUri`, `KustoDatabase` and
`KustoServiceConnection` are all non-empty, so the pipeline still runs + compares before the
cluster/service connection exist.

### Managing the baseline version

The **Nightly performance run** schedule runs daily at **06:00 UTC on `main`**,
even without source changes.
Manual runs remain available; PRs and commits do not trigger it.

The default `baselineVersion=latest` reads the `governed` source from the repository's `NuGet.config`,
discovers its NuGet V3 package endpoint, and resolves the highest available stable version once per
run, excluding prereleases. Baseline restores use the same config and its package source mappings;
packages must be approved and available in the governed feed. Lookup failures fail the run rather
than selecting a stale version or skipping comparison. The resolved version is used for the build, comparison,
baseline build tag, and Kusto metadata, and is saved in `baseline-label.txt`.
An explicit version can still be supplied at queue time; an empty value runs current-only.

## PR pipeline (`sqlclient-perf-pr-pipeline.yml`)

`sqlclient-perf-pr-pipeline.yml` answers the question a PR author actually has — *does my branch
regress the branch I am merging into?* — by running the **same** benchmarks, on the **same** extends
template, through the **same** on-VM scripts, with the **same** configuration options as the main
pipeline. It differs in exactly two ways:

| | `sqlclient-perf-pipeline.yml` | `sqlclient-perf-pr-pipeline.yml` |
| --- | --- | --- |
| Candidate | branch the run is queued on | branch the run is queued on (unchanged) |
| Baseline | released NuGet package (`baselineVersion`, default `latest`) | **source of another git ref** (`baselineSourceRef`, default `main`) |
| Kusto | translates + (optionally) ingests | **never** — no ADX variable group, no translate/ingest steps |

The main pipeline is scheduled daily and can also be queued manually. The PR pipeline is
**manual / queue-time only**: a run occupies a dedicated host for hours, so a PR opts into it
explicitly. Neither pipeline triggers on PRs or commits (`pr: none`, `trigger: none`).

PR-only parameters (everything else is identical to the table above):

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `baselineSourceRef` | `main` | Git ref of this repo whose **source** is the baseline. Empty = current-only (no baseline pass / comparison). |
| `baselineRepoUrl` | `https://github.com/dotnet/SqlClient.git` | Fallback remote used to obtain the baseline ref when the VM copy of the checkout cannot fetch it from its own `origin`. |
| `testTimeoutMinutes` | `210` | Higher than the main pipeline's `180` because **both** sides are built from source. |

### Source baseline (`--baseline-source-ref` / `-BaselineSourceRef`)

The run scripts accept the source baseline as an alternative to `--baseline-version` (the two are
mutually exclusive and the script fails fast if both are supplied). On the VM the script:

1. Materialises the baseline ref next to the checkout, in `../sqlclient-perf-baseline-src` — outside
   the checkout so it can never be picked up by the candidate build or the results copy. It first
   tries the copied checkout's own `origin` (`git fetch --depth 1` into a private
   `refs/remotes/perfbaseline/*` namespace, then `git worktree add --detach`), and falls back to a
   shallow `git clone` of `baselineRepoUrl` when the tree arrived without `.git` or `origin` needs
   credentials. On ADO the origin fetch is *expected* to fail — the checkout is copied to the VM
   without credentials — so the clone fallback is the normal path there. Every network git call runs
   non-interactively (`GIT_TERMINAL_PROMPT=0`, no credential helper, stdin closed) under a
   `GIT_NET_TIMEOUT_SECS` timeout (default 300s), and its output is kept in
   `<results>/diagnostics/git-*.log`, so a missing credential fails in under a second instead of
   blocking the job on a prompt.
2. Builds **that ref's own `PerformanceTests` project** as the `baseline` variant, so the driver
   under measurement is the baseline ref's source via `ProjectReference` — mirroring how the
   `current` variant is built from this branch. No NuGet baseline config is involved.
3. Labels the comparison `"<ref>@<short-sha>"` (e.g. `main@a1b2c3d`) so a run states exactly which
   baseline commit it was measured against, and writes that label to
   `<results>/baseline-label.txt`. The PR pipeline reads that file after the results are copied
   back and tags the build `Baseline <ref>@<short-sha>`, so the exact baseline commit is visible in
   the ADO build list. (The pipeline cannot produce this tag on its own — at compile time it only
   knows the ref name, which would tag every run identically.)

Because each side builds its own harness, a benchmark added by the PR simply shows up as `new` in
the comparison (and one removed by the PR as `removed`) instead of failing the run. Both passes
share the single generated runner config (`RUNNER_CONFIG` / `DATATYPES_CONFIG` env vars), so
connection string and behaviour flags are identical on both sides.

## Experiment pipeline (`sqlclient-perf-experiment.yml`)

The three perf pipelines are the same benchmarks, template and scripts pointed at three different
questions. Two of them vary the **source** under measurement; the third varies the **config**:

| Question | Pipeline | Baseline | Current |
| --- | --- | --- | --- |
| Has this branch regressed against a released package? | `sqlclient-perf-pipeline.yml` | released NuGet package | queued branch |
| Does my PR regress the branch it merges into? | `sqlclient-perf-pr-pipeline.yml` | `main` source | queued branch |
| What does this switch cost or buy? | `sqlclient-perf-experiment.yml` | queued branch, switch **off** | queued branch, switch **on** |

`sqlclient-perf-experiment.yml` picks one runner-config switch via the `switchUnderTest`
queue-time parameter (`UseConnectionPoolV2`, `UseOptimizedAsyncBehaviour` or
`UseManagedSniOnWindows`) and runs the baseline pass with it `false` and the current pass with it
`true`. Both passes measure the **same commit** — the branch the run is queued on — so queue it on a
PR branch to ask "what does this switch do to my change?", or on `main` to ask "what does it do to
`main`?".

Two passes are required because these are `AppContext` switches latched process-wide (for example
`UseConnectionPoolV2` is read and cached the first time a connection pool is created), so they cannot
be toggled between benchmarks within a single process. The pipeline wires that into the existing
comparison machinery — interleaved best-of-N or sequential, via `benchmarkRunMode` — instead of two
ad hoc manual runs.

Switch-pipeline parameters that differ from the tables above:

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `switchUnderTest` | `UseConnectionPoolV2` | The single switch to A/B. Baseline forces it `false`, current forces it `true`. |
| `failIfSwitchSlower` | `false` | Maps onto the scripts' `--fail-on-regression` gate, but means something different here: "switch on is slower" is usually the *result* you queued the run to measure, not a defect. Enable it only when asserting the switch must not be a slowdown (e.g. before flipping its default). |
| `testTimeoutMinutes` | `180` | Same as the main pipeline, not the PR pipeline's `210`: both sides are the same source, so only **one** driver build is needed. |

The pipeline deliberately does **not** expose `baselineVersion` / `baselineSourceRef` (the run
scripts reject combining those with `--switch-under-test`, since a simultaneous source change would
make the delta unattributable), nor the `useManagedSniOnWindows` / `useOptimizedAsyncBehaviour` /
`useConnectionPoolV2` flags. Every switch except the one under test stays at its checked-in
`runnerconfig.jsonc` value, so the measured difference is attributable to exactly one variable.

### Why these runs are never ingested into Kusto

This mode has its own pipeline file, rather than being a flag on the other two, specifically so that
"never ingested" is structural rather than a conditional someone can flip. Ingesting a switch
experiment would corrupt the perf database three ways:

* **`DerivedRunId` collision.** The ID is `driver|commit|pipelineRunId`. The other two pipelines keep
  their two rows distinct because the baseline row carries a *different* commit (`v7.0.2`, or the
  baseline ref's sha). Here both passes are the same commit in the same pipeline run, so both rows
  would derive the same ID.
* **`PerfRun.Config` is stamped once per run.** `translate_results_to_kusto.sh` builds one
  `--config-override` set from the queue-time `CFG_*` values and reuses it for both the baseline and
  current rows. That is correct when the config genuinely is shared, but it means the two rows could
  not record the differing switch values that are the entire point of the experiment.
* **Trend pollution.** No field marks a row as an experiment — `RunType` is already
  `Sequential`/`Interweaved` — so the switch-on pass would be indistinguishable from an ordinary
  measurement of the branch and would distort the very trends the other two pipelines exist to
  protect.

The comparison report and the raw BenchmarkDotNet artifacts are published as usual, and the build is
tagged `Switch <name>` so experiments are identifiable in the ADO build list.

### Designing benchmarks for switch experiments

A switch experiment flips behaviour on purpose, so a benchmark that measures that behaviour will
report a regression even when the change is working. `UseConnectionPoolV2` is the motivating example:
`ChannelDbConnectionPool` opens physical connections concurrently, where `WaitHandleDbConnectionPool`
serialises growth behind a `Semaphore(1, 1)`. `ConnectionPoolStressRunner` used to call
`ClearAllPools()` in `[IterationCleanup]`, so every iteration was a cold-start burst in which the
extra parallel opens had nothing to amortise against, and the runner reported the trade-off as a loss
because that was the only thing it could measure.

That is a benchmark defect rather than something to explain away, so the runner now pre-warms the
pool to full capacity in `[GlobalSetup]` and no longer clears it between iterations. Establishing a
connection costs milliseconds while a pooled checkout costs microseconds, so any creation left in the
measured body swamps the pool cost the runner exists to measure. Keep that separation in mind when
adding a pool benchmark: warm the pool first unless connection establishment is precisely the thing
under test.

The pipeline has no way to mark a result as acceptable, and deliberately so: a mute is only as good
as the reasoning behind it, and that reasoning belongs in the pull request where a reviewer can
challenge it. Prefer instead to fix the benchmark, or add one that measures the intended behaviour
directly. `ConnectionPoolRampRunner` was added for exactly this reason: it keeps the cold pool but
makes every caller *hold* its connection until all of them have connected, so the pool genuinely needs
N physical connections and the only variable left is how fast it can open them. That rewards
concurrent creation instead of penalising it, and it isolates cold start now that the stress runner no
longer conflates it with checkout cost.

The same principle applies to how a benchmark schedules its workers. A sync `Open()` that has to
wait blocks whichever thread it runs on, so a pool whose waiter wake-up needs a queued continuation
stalls when every threadpool thread is already blocked; the wake-up waits on thread injection. On
the TFM the perf project builds (net10.0) the runtime is told about cooperative blocking and
compensates quickly, so that stall is tens to a few hundred milliseconds, and that is the only
expectation these benchmarks validate. On net462 the `Task` wait never notifies the pool, so the
wake-up falls to starvation detection and hill climbing and is materially slower; the pool carries no
framework guards and the driver still ships net462, so that path is live but unmeasured here.
Threadpool threads are the realistic case, because sync database
calls in ASP.NET run on them, and they are the only configuration in which that stall is visible.
Benchmarks therefore keep threadpool threads as the default and add dedicated-thread variants
alongside rather than instead:

- `ConnectionPoolContentionRunner.SteadyStateOpenQueryCloseDedicatedThreads` runs the existing
  workload on dedicated threads. A regression in both variants points at the pool; a regression in
  only the threadpool variant points at the waiter wake path.
- `ConnectionPoolThreadPoolPressureRunner` pins the threadpool floor via `[Params]`, below the worker
  count (starved) and above it (control), so the effect is reproducible instead of depending on
  hill-climbing timing.

This failure mode is tail latency, not a shifted median, so compare distributions. Aggregating with
a per-configuration minimum hides it completely.

Note what those benchmarks are for. Saturating the thread pool with blocked synchronous calls is an
application configuration problem, not a pool defect: an application should keep its parallelism
below the thread pool's worker count so newly queued work still runs promptly, and pre-warming the
thread pool is the application's responsibility rather than the driver's. These benchmarks exist to
characterise where that boundary sits and to catch it moving, so a delta here is a prompt to check
the boundary has not shifted rather than a bug to fix.

## Two-pass build model

The `PerformanceTests` project references Microsoft.Data.SqlClient two ways, selected by MSBuild:

- **Current** (default): `ProjectReference` to the in-repo source — the branch under test.
- **Baseline (package)**: `ReferenceType=Package` turns the reference into a `PackageReference`. Because the
  repo uses **Central Package Management (CPM)**, the version is pinned with `VersionOverride` via
  `-p:MdsPackageVersion=<version>` (a plain `Version` is ignored under CPM).
- **Baseline (source)**: no reference switching at all — the baseline ref's own copy of the perf
  project is built from `../sqlclient-perf-baseline-src`, keeping its default `ProjectReference` to
  that ref's driver source. Used by the PR pipeline.
- **Baseline (switch experiment)**: no second build at all — `--switch-under-test` measures one
  source tree twice, so the scripts build the `current` variant once and point both passes at it,
  differing only in the runner config each pass is handed. Used by the experiment pipeline.

The baseline pass restores through the repository's **`NuGet.config`**, passed explicitly with
`-p:RestoreConfigFile`. This preserves its governed feed and package source mappings.
No separate baseline config or direct NuGet.org source is generated.

### Benchmarks must compile against the oldest baseline

The baseline pass compiles the **same** `PerformanceTests` sources against the *released* MDS
package, so a benchmark that calls an API introduced after `baselineVersion` fails that pass with
`CS1061`. The project defines cumulative `MDS_GE_<major>` constants from `MdsPackageVersion` (see
`Microsoft.Data.SqlClient.PerformanceTests.csproj`); guard such calls and provide an older
fallback:

```csharp
#if MDS_GE_6
    _ = reader.GetSqlJson(0).Value;   // GetSqlJson was added in MDS 6.0
#else
    _ = reader.GetString(0);
#endif
```

Project mode, Package mode with no pinned version, and unparsable version strings define every
constant, since those builds reference a current MDS. Note that a fallback makes the baseline
measure a **different code path** than the current pass — the affected benchmark's delta is not
meaningful, so the runner should say so loudly at setup (see `JsonVsVarcharReadRunner`).

## Comparison output

`compare_perf.py` matches benchmarks by `(Type, Method, Parameters)` and reports, per benchmark, the
baseline/current mean (ms), mean %Δ, allocation %Δ, and a status (`regression` / `improvement` /
`unchanged` / `new` / `removed`). Outputs:

- `results/comparison/comparison.md` (also copied to `results/summary.md`, which the template
  attaches as the run summary),
- `results/comparison/comparison.json` (structured, for tooling).

## Reducing noise

The harness applies a set of harness-owned controls to reduce measurement noise. The lab already
supplies the isolated dedicated host, the tuned SQL instance, and the disjoint client CPU set
(`PERF_CLIENT_CPUS`); the run scripts add:

| Control | What the harness does |
| ------- | --------------------- |
| Client CPU pin | Pins the benchmark process to `PERF_CLIENT_CPUS` (`taskset` on Linux, `ProcessorAffinity` on Windows). |
| Fail loud | Preflight `SELECT 1` before any pass, **and** a post-pass guard that fails the run if a pass produced **zero** benchmark results — so an empty comparison can never be reported green. |
| Warm-up | Touches the target DB in the preflight to warm the buffer pool / plan cache before the first measured benchmark. |
| Allocator tuning (Linux) | Exports `MALLOC_MMAP_THRESHOLD_=128MiB` and `MALLOC_TRIM_THRESHOLD_=-1` so large-buffer benches (`LargeDataRead`, `SqlBulkCopy`) stop re-`mmap`ing per iteration. |
| Network tuning (Linux) | Best-effort `sysctl` to widen the ephemeral port range and enable `tcp_tw_reuse` for churn benches (`ConnectionPoolStress`, `ConnectionPoolRamp`, `ConnectionPoolThreadPoolPressure`, `ParallelAsyncConnection`). Never fails the run. |
| Diagnostics | Writes `results/diagnostics/`: SQL instance config (MAXDOP, memory, affinity, tempdb files, `@@VERSION`), host CPU topology, and per-pass CPU-clock/thermal telemetry (before/after each pass). |
| Regression gate | `failOnRegression` threads `--fail-on-regression`; only a **candidate-slower** delta past the threshold fails, and in interleaved mode only after best-of-N confirmation. Default off. |
| Interleaving | In `interleaved` mode the harness runs **one benchmark unit at a time, baseline then candidate back-to-back**, so both sides see the same host state (see below). |
| Best-of-N confirmation | A unit flagged in the first interleaved pass is re-run `confirmationRuns` times; a regression is **confirmed** only on a strict majority. Unconfirmed flags are reported but never fail the gate. |

### Windows physical opens

The Windows connection probe measured raw TCP at about 0.06 ms but a physical
`SqlConnection.Open()` at about 158 ms when the runner used the `sa` SQL login, with the same result
from native and managed SNI. Changing TLS and TCP acknowledgment settings did not change that
latency. The Windows image runs SQL Server 2025, which verifies SQL-login passwords with 100,000
PBKDF2 iterations; Microsoft documents the resulting login-performance impact.

The benchmark process runs through a public-key SSH token, which cannot delegate Windows credentials
to the VM's private network address. When the injected SQL endpoint resolves to a local interface,
the Windows harness connects to the same SQL Server 2025 instance over local TCP loopback with
integrated authentication. It grants the ephemeral VM identity `db_owner` in the perf database; `sa`
remains unchanged and is used only for one-time setup. External SQL endpoints continue to use the
injected address and SQL authentication. This stays on SQL Server 2025's supported authentication
path while preventing password hashing from dominating local tests that intentionally create
physical connections.

### Interleaving + best-of-N (run model)

`benchmarkRunMode` selects how the two variants are measured:

- **`interleaved`** (default) — `interleave_perf.py` orchestrates the run. Both variants are built
  **once** into separate output dirs (`perf-build-baseline`, `perf-build-current`), then for each
  benchmark unit the baseline and candidate builds run **back-to-back** before moving to the next
  unit. Because the same benchmark is measured on both sides within seconds, slow host drift affects
  both roughly equally and cancels out of the delta. This relies on the `PerformanceTests` runner
  supporting `PERF_LIST_BENCHMARKS` (enumerate enabled units) and `PERF_BENCHMARK=<unit>` (run a
  single unit) — see `Program.cs`.

  After the first interleaved pass, only the units containing a flagged regression are re-run
  `confirmationRuns` times (best-of-N). A regression is **confirmed** only when a strict majority of
  the N passes agree `(count * 2 > N)`; otherwise it is reported as `regression (unconfirmed)` and
  does **not** fail the `failOnRegression` gate. `confirmationRuns = 1` disables confirmation.

- **`sequential`** — legacy model: the whole baseline suite runs, then the whole candidate suite,
  then `compare_perf.py` diffs them. Kept as a fallback; produces the same `results/baseline`,
  `results/current`, and `results/comparison/` layout so Kusto ingestion is identical.

Both modes emit `results/comparison/comparison.md` + `comparison.json` and copy the markdown to
`results/summary.md`; interleaved mode adds a **Confirm** column and a confirmed/unconfirmed summary.

### Further tuning (not yet implemented)

- **Release-grade sampling / relaxed thresholds** — tune BenchmarkDotNet job counts and
  significance thresholds in `runnerconfig.jsonc` / `BenchmarkConfig.cs` now that interleaving and
  best-of-N are in place.

## Kusto schema & ingestion

Two tables:

- **`PerfRun`** — one row per run (baseline OR current): `DerivedRunId` (PK =
  `DriverName|CommitHash|PipelineRunId`), driver/machine/agent, `OperatingSystem` (`Windows`/`Linux`),
  `Architecture` (`x64`/`x86`), `RunType` (`Sequential`/`Interweaved`), pipeline id + build URL,
  branch + `BranchCategory`, `VersionString`, commit hash/date, `IsComparableBase`, `IngestedAt`.
  `BranchCategory` buckets the ref as `main`, `release`, `dev`, `feature`, `pull_request` or
  `other`. The internal ADO mirror's `internal/` prefix is stripped first, so `internal/main` and
  `internal/release/*` land in the same buckets as their public counterparts.
- **`PerfBenchmarkResult`** — one row per benchmark: `BenchmarkId` (PK =
  `DerivedRunId|BenchmarkName|MethodName|ParameterSignature`), timings in **milliseconds**
  (BenchmarkDotNet reports nanoseconds; values are divided by 1,000,000), percentiles, throughput,
  allocation, runtime/platform, and `DriverSpecificMetrics` (GC collections, lock contentions, …).

The baseline and current passes share the pipeline run id and (for the current pass) the commit, so
to keep their `DerivedRunId`s distinct the baseline row uses `CommitHash = v<baselineVersion>` and
`IsComparableBase = true`; the current row uses the real commit and `IsComparableBase = false` with
the triggering branch name.

Source = BenchmarkDotNet **JSON "full"** exporter files (`*-report-full.json`). The exporter is
enabled in `Config/BenchmarkConfig.cs` (`JsonExporter.Full`).

### One-time database setup

Before ingestion can run, create the two tables (`PerfRun`, `PerfBenchmarkResult`) in the target
database, with columns matching the schema summarized above. No server-side ingestion mappings need
to be created: `ingest_kusto.py` sends a **self-contained inline JSON column mapping** built from
each payload's own property names (which are identical to the table's column names), so ingestion
does not depend on any pre-created named mapping existing on the cluster.

### Authentication

Ingestion runs in an `AzureCLI@2` task using the ADO **ARM service connection**
(`KustoServiceConnection` from the `ADX Cluster Variables` group). That connection's **service
principal** must be granted, on the target database:

- **Database Ingestor** — required to queue the ingestion, and
- **Database Viewer** — required for the post-ingestion verification queries. With Ingestor-only
  rights the data still lands, but the verify step cannot read it back and logs a warning naming
  this missing role.

`ingest_kusto.py` authenticates to Kusto with `with_az_cli_authentication` (the service connection
is already `az login`'d inside the task) and performs a **queued** ingestion against the
data-management (`ingest-`) endpoint.

### Running before the cluster exists

Ingestion is **conditional**: it only runs when the `enableKustoIngestion` parameter is `true` (the
default) **and** `KustoClusterUri`, `KustoDatabase` and `KustoServiceConnection` (from the `ADX
Cluster Variables` group) are all non-empty. Set `enableKustoIngestion` to `false` to opt a run out
of ingestion explicitly. Until a cluster and service connection are
configured, the pipeline still runs both passes, produces the comparison, and publishes the
translated NDJSON as the `perf-kusto-payloads` artifact for manual/backfill ingestion.

## Running the pipeline

1. Open the performance test pipeline in Azure DevOps and select **Run pipeline**.
2. Choose the branch to benchmark; override `baselineVersion` only if needed. Ingestion is on by
   default (`enableKustoIngestion`) and uses the `ADX Cluster Variables` group — populate
   `KustoClusterUri` / `KustoDatabase` / `KustoServiceConnection` there to enable it, leave them empty
   to skip ingestion, or untick **Ingest results into Kusto** to skip it for a single run.
3. After the run, review the **run summary** (comparison) and the `perf-results` /
   `perf-kusto-payloads` artifacts. When a baseline pass ran, the build is tagged
   **`Baseline <version>`** so the baseline used is visible at a glance in the ADO build list.

### Running the PR pipeline

1. Open the **PR** performance test pipeline (`sqlclient-perf-pr-pipeline.yml`) in Azure DevOps and
   select **Run pipeline**.
2. Choose the PR's branch to benchmark. Leave `baselineSourceRef` at `main` unless the PR targets a
   different branch (e.g. a release branch). No Kusto configuration is involved — PR results are
   never ingested.
3. After the run, review the **run summary** (comparison, labelled `<ref>@<short-sha>`) and the
   `perf-results` artifact. The build is tagged **`Baseline <ref>`**.

### Running the experiment pipeline

1. Open the **experiment** performance test pipeline (`sqlclient-perf-experiment.yml`) in Azure
   DevOps and select **Run pipeline**.
2. Choose the branch whose behaviour you want to measure — `main` to characterise the switch on its
   own, or a PR branch to characterise it against that change — and pick `switchUnderTest`. There is
   no baseline selector: the baseline *is* this branch with the switch off. No Kusto configuration is
   involved; these results are never ingested (see [Why these runs are never ingested into
   Kusto](#why-these-runs-are-never-ingested-into-kusto)).
3. After the run, review the **run summary** (comparison, labelled `<Switch>=false`) and the
   `perf-results` artifact. The build is tagged **`Switch <name>`**.

## Troubleshooting

| Symptom | Likely cause / fix |
| ------- | ------------------ |
| `NU1507` during the baseline pass | Multiple NuGet sources under CPM without package source mappings. Ensure the repository's `NuGet.config` is passed via `-p:RestoreConfigFile` and its mappings are intact. |
| Baseline restore fails to find MDS | The requested version or one of its dependencies isn't approved and available in the governed feed, or the VM cannot access that feed. |
| Baseline pass fails to compile: `CS1061 ... does not contain a definition for <member>` | A benchmark calls an MDS API newer than `baselineVersion`. Guard it with the `MDS_GE_<major>` constants and add an older fallback — see [Benchmarks must compile against the oldest baseline](#benchmarks-must-compile-against-the-oldest-baseline). |
| No comparison / summary | The baseline pass was skipped (empty `baselineVersion` / `baselineSourceRef`) or one pass produced no `*-report-full.json`. |
| `--switch-under-test is mutually exclusive with --baseline-version and --baseline-source-ref` | A switch experiment was combined with a source baseline. The experiment pipeline never does this; if you are invoking the scripts directly, clear the baseline selector — varying source and config at once makes the delta unattributable. |
| Switch experiment shows a ~0% delta everywhere | Expected for benchmarks the switch does not touch. If *every* benchmark is flat, check the run log's `Switch A/B` line actually names the switch, and that the switch is one the driver reads at startup via the runner config. |
| Baseline source ref not found (PR pipeline) | `baselineSourceRef` is not a branch on `origin`, and the fallback `git clone --branch <ref>` of `baselineRepoUrl` also failed (ref does not exist there, or the VM has no outbound access to the remote). The reason git gave is echoed into the build log and saved to `<results>/diagnostics/git-*.log`. |
| `Fetching baseline ref ... from the checkout's origin` is the last line, then the job stalls | Should no longer happen. The checkout is copied to the VM without credentials (ADO's checkout task defaults to `persistCredentials: false`), so a fetch from an authenticated `origin` used to sit on a `Username for ...` prompt forever. All network git calls now run with `GIT_TERMINAL_PROMPT=0`, no credential helper, stdin closed, and a `GIT_NET_TIMEOUT_SECS` (default 300s) hard timeout, so this fails in under a second and falls back to cloning `baselineRepoUrl`. A fetch failure here is expected and harmless on ADO. |
| Ingestion step skipped | `enableKustoIngestion` is `false`, or `KustoClusterUri`, `KustoDatabase` or `KustoServiceConnection` (from `ADX Cluster Variables`) is empty (expected until the cluster is provisioned). |
| Ingestion auth error | The service connection's SP lacks **Database Ingestor** on the target database. |
| "Kusto ingestion was queued, but the ingestion principal is not authorized to query the database" | The SP has **Database Ingestor** but not **Database Viewer**. Ingestion succeeded; grant **Database Viewer** so the verify step can confirm the rows landed. |
| `Kusto ingestion not yet queryable after Ns ... no ingestion failures were reported` (warning, step passes) | Expected, harmless: queued ingestion is asynchronous and small perf payloads can take longer than the verify window to become queryable. The step **warns and passes** because `.show ingestion failures` is clean, so the rows will land shortly. The step only **fails** when `.show ingestion failures` actually reports failures — in that case confirm the `PerfRun` / `PerfBenchmarkResult` tables exist with columns matching the schema above (a schema/column-name mismatch is the usual cause; the self-contained inline JSON mapping rules out a missing server-side named mapping). |
| Benchmarks not CPU-pinned | `PERF_CLIENT_CPUS` was not injected, or `taskset` is unavailable on the VM. For interleaved Windows runs, check stderr for `SetProcessAffinityMask` warnings with the child PID, requested mask, and Win32 error code/message. Pinning remains best effort; masks are limited to the Python process's pointer width (32 or 64 bits) and cannot span processor groups. |

### Affinity regression tests

Run the standard-library tests from the repository root:

```text
python -m unittest discover -s eng/pipelines/perf/scripts/tests -p test_affinity.py -v
```

On Windows, `py -3` can be used instead of `python`. The tests cover the Windows ctypes
signature, error diagnostics, mask bounds, and unchanged Linux warning-only behavior.
Windows-only tests also pin a disposable Python child to an available CPU and read its
affinity back, and check the real error from an invalid handle. No SQL Server is needed,
and the test runner's affinity is never changed.
