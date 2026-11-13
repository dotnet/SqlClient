# Automatic OpenAsync health coverage

The existing Linux and Windows perf runners execute this phase automatically before
BenchmarkDotNet. This includes `sqlclient-perf-pr-pipeline.yml` when queued. It does **not**
add an all-PR trigger: perf-pr remains manual/queue-time, nightly remains scheduled, and
the switch experiment remains opt-in.

**The default matrix adds zero network RTT. The 100 ms added-RTT reproduction does not
automatically run.** An explicit Linux CLI run can inject SQL-only loopback delay. Neither
the default run nor a single optional RTT run claims to cover all connection branches.

## Workloads and bounds

Each side runs 20 cases, each repeated three times in fresh processes:

| Connection path | States | Operations |
| --- | --- | --- |
| V1 pool | Cold process, initialized driver with cleared pool, warm available pool, warm oversubscribed pool | OpenAsync/close and OpenAsync/SELECT 1/close |
| V2 pool | Same four states | Same two operations |
| Non-pooled | Cold process and initialized driver | Same two operations |

Defaults are 64 workers, minimum 16 ThreadPool workers, unchanged runtime maximum,
max pool size 100 (16 for oversubscription), an 8-second observation window, 15-second
connect/query timeout, and no connect retries or pool blocking period. Warmup holds all
64 (or 16) connections simultaneously before returning them to the pool. Initializing
a non-pooled driver still creates a new physical SQL connection for every operation.
The small query matters when testing network latency; open-only exposes checkout/return cost.

There are **120 processes** for a fully supported matched default run: 20 x 3 x 2.
Their nominal observation plus probe grace time is 18 minutes, before build/restore,
warmup and drain. The **total phase budget is 30 minutes including its builds/restores**.
Each build is capped at 10 minutes and each child at 3 minutes, further limited by the
remaining total budget. A timeout is invalid evidence, never a skipped/passing case.
The enclosing defaults are increased by 30 minutes: 240 minutes for PR, 210 for nightly
and the switch experiment. SQL authentication/login cost and slow warmup can exhaust
the budget; the runner fails explicitly rather than silently shrinking the matrix.

Candidate and baseline cases run back-to-back on the same host and SQL endpoint, under
the same CPU affinity. Case order is deterministically shuffled each repetition;
candidate runs first in the middle repetition to reduce ordering bias. SNI and optimized
async settings come from the common runner configuration. The suite explicitly selects
both pool versions regardless of the BDN `useConnectionPoolV2` setting. It does not use
or add the experimental `UseLongRunningPoolV2Open` driver switch.

## Matched harness and historical baselines

`PerformanceTests/OpenAsyncHealth/OpenAsyncHealth.csproj` is a small independent executable,
without BDN warmup or configuration loading. The runner compiles the **candidate's same
harness sources** twice, referencing either the candidate driver project or the resolved
baseline driver project/package. It never asks an old baseline's PerformanceTests executable
to understand a new CLI. Build/restore failures remain failures. Source baselines must have
the unified driver project; unsupported source layouts fail explicitly.

The manifest retains harness source hashes, both built harness/driver hashes, baseline
label, complete case settings, affinity, budgets and repetition counts. Raw results also
record actual loaded driver identity/hash (including platform-specific package assets),
runtime, OS, CPU count and ThreadPool maximum. Runtime/CPU
or settings mismatches are invalid evidence.

Some historical packages do not implement V2. The harness checks the actual driver's
pool-switch property before labeling a workload V2. Those baseline cases are reported
**unsupported/uncompared**, never silently measured as V1. The candidate must still run
all cases. Current-only and switch-experiment runs similarly report **uncompared** health
findings; this phase is not a second switch A/B experiment.

## Evidence and gating

The dedicated main/observer threads do not depend on ThreadPool timers to stop a window.
Probe timestamps distinguish intended cadence, posting and execution. Bounded probe
capacity records dropped, missed, rejected and outstanding callbacks, including a
post-window grace period. Raw resource samples include **all process threads**, not just
ThreadPool threads, plus allocation/GC and queue counts.

Work records preserve offer, worker start, open completion and final outcome timestamps.
Started/opened/completed/failed/cancelled/in-flight counters reconcile; failed/cancelled
opens do not disappear into a successful-only latency percentile. Every worker's first
outcome is retained even after the 100,000 normal-record limit; dropped-record counts
remain explicit. Only first-worker, fully observed acquisition latency is gated, so the
retention cap cannot bias a general steady-state percentile into a gate.

There are two separate findings:

* **Absolute health:** investigative dispatch p99 <=100 ms, at least 100 completed probes,
  no missing probes or missing first-worker completions, successful SQL work. This is not
  a product SLA. Censored evidence is marked insufficient (or a known failure when the
  measured lower-bound p99 already exceeds the budget).
* **Matched regression:** the same metric must regress in a strict majority of the three
  paired repetitions. This gate is always active, independently of BDN `failOnRegression`.
  All pairs and unconfirmed/noisy findings remain in the report.

| Metric | Regression threshold |
| --- | --- |
| Dispatch p99 | More than **both** 25% and 25 ms worse |
| First-worker offer-to-open p99 | More than **both** 25% and 50 ms worse |
| Completed work/second | More than 10% lower |
| Peak process threads | More than **both** 25% and 8 threads higher |
| Missing probe fraction | Increase greater than 5 percentage points |
| Missing first-worker completions | Any increase |
| Complete baseline -> censored candidate | Coverage regression |

Latency comparisons require uncensored observations on both sides. Throughput/resource
comparisons remain active when scheduler evidence is censored. Unchanged absolute failures
are **baseline debt**, not a permanently red pipeline. If both sides are censored without a
confirmed degradation, the scheduler conclusion is **inconclusive baseline debt**, not
"healthy" or "no regression proven." Candidate coverage degradation still gates; being
unhealthy on both sides is not a waiver. Candidate improvements are reported separately.

Missing files, nonzero child exits, invalid schema/counters/timestamps, failed or empty SQL
work, observer errors, undrained work and missing first-worker records fail as **invalid
evidence**. There is no blanket `continueOnError`. A confirmed health regression is retained
while BDN runs, then fails the entrypoint. An invalid health phase fails immediately.

## Artifacts and credentials

The existing `perf-results` artifact includes `openasync-health/manifest.json`, raw
per-process case/result JSON and sanitized logs, `rows.json`, `summary.csv`,
`comparison.json`, and optional netem packet/filter/cleanup metadata. A top-level
`openasync-health.md` is available to the template's existing run-summary collection.
Partial raw evidence remains after a failed run. No health data is ingested as BDN timing.

The runner reads the existing **private** runner config and passes its connection string
only through the child environment (`SQLCLIENT_HEALTH_CONNECTION_STRING`); it is never
included in case/result JSON or CLI arguments. The child removes the environment variable
after reading it. Exceptions retain types/SQL error numbers, not server/credential text.
Do not publish the private runner config; the existing redacted config is the artifact.

## Explicit Linux RTT run

On a disposable dedicated perf VM, after its normal SDK, SQL and private runner config
setup, invoke:

```bash
python3 eng/pipelines/perf/scripts/openasync_health.py \
  --results-dir perf-rtt-results \
  --runner-config perf-runnerconfig.json \
  --baseline-source-root /path/to/baseline-source \
  --baseline-label '<ref>@<sha>' \
  --client-cpus "$PERF_CLIENT_CPUS" \
  --server "$SQL_SERVER" --rtt 100
```

Use a new results directory. `--rtt 100` adds 50 ms per direction, not 100 ms per
direction. `tc`, `ip`, root/passwordless sudo, and a local TCP/1433 SQL endpoint are
required. Rules delay only SQL traffic to/from the resolved local addresses. A foreign
loopback queue is never replaced; cleanup removes only the owned queue on normal exit,
failure or SIGTERM. Packet drops, changed rules and zero shaped packets invalidate the
run. Hard termination/VM loss cannot run cleanup; the existing template owns VM teardown.
The same 30-minute limit applies: RTT increases sequential warmup time and may exhaust
the budget. A timed-out RTT sweep is incomplete, not reproduction coverage.

## Deliberate limits

This suite exercises successful OpenAsync paths on modern .NET with the runner's SNI,
authentication, SQL version and endpoint. It does not cover .NET Framework, authentication
mode matrices, transaction enlistment, failover/routing, pool shutdown, every cancellation
race, or timeout/failure distributions. A credential-free stalled-prelogin control tests
harness cancellation/timeout accounting; it is not authenticated SQL or simulated RTT.
Same-host pairing and majority confirmation reduce noise; they do not prove statistical
significance or eliminate SQL/server drift. Dedicated-thread peaks can still fall between
the 25 ms resource samples.
