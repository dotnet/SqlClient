# OpenAsync health boundary experiments

This diagnostic companion to BenchmarkDotNet measures **fresh-process cold starts**, which
normal benchmark warmup and the existing prewarmed `ConnectionPoolContentionRunner` conceal.
It does not replace BDN timing baselines or pretend its instrumented throughput is a microbenchmark.

## Run a case

Build `src/Microsoft.Data.SqlClient/tests/PerformanceTests/Microsoft.Data.SqlClient.PerformanceTests.csproj`
in Release. Run the built `PerformanceTests.dll` with:

```text
dotnet PerformanceTests.dll --openasync-health case.json result.json
```

Set `SQLCLIENT_HEALTH_CONNECTION_STRING` privately in the launching environment. Never put a
password in `case.json`, shell arguments, reports, or source. The program removes that variable
from its environment after reading it. Results contain no connection string or exception messages.

Example `case.json`:

```json
{
  "Workers": 64,
  "PoolSize": 100,
  "MinimumThreads": 16,
  "MaximumThreads": 32767,
  "DurationSeconds": 8,
  "ConnectTimeoutSeconds": 15,
  "PoolV2": true,
  "LongRunning": false,
  "Pooling": true,
  "WarmPool": false,
  "Query": true,
  "Burst": false,
  "StalledPrelogin": false
}
```

One process runs exactly one case. V2 and managed SNI switches are set before driver use.
LongRunning is the experimental dispatch switch from this branch, **not an async-networking fix**.
ThreadPool minimum/maximum apply to workers, not I/O-completion threads. Settings are validated
against the runtime-visible CPU count. All outcomes, including SQL errors and cancellation, are
counted. Pool blocking-period caching is disabled so repeated failures are not suppressed; this
differs from the playground default and is recorded in the protocol.

`WarmPool=true` synchronously pre-opens and holds `min(Workers, PoolSize)` connections outside
measurement, then returns them. It must not be used with the stalled endpoint or non-pooled mode.
It warms driver initialization as well as connection state, so compare it with cold-process
results as a composite warm/cold contrast, not an isolated measure of one lock.

`StalledPrelogin=true` needs no credentials. It opens an ephemeral loopback TCP listener on a
dedicated thread and deliberately never responds to TDS prelogin. This drives real driver open
and timeout paths without SQL or netem. It is a **negative control, not an RTT simulation**.
The sweep uses one burst and a three-second connect timeout for this control; SQL cases use
continuous loops, `SELECT 1`, and a fifteen-second timeout. Do not compare their throughput.

## Repeated sweep

`scripts/openasync_health_sweep.py` launches each case/repetition in a **fresh process** and
randomizes case order with seed 5014. No prior BDN warmup or previous case can leave a grown
ThreadPool. Existing `netem.py` provides optional exclusive ownership and cleanup.

```bash
# SQL-free blocking controls; never touches shared traffic-control rules.
python3 scripts/openasync_health_sweep.py --mode stalled \
  --binary /path/to/PerformanceTests.dll --output /path/to/new-results --repetitions 3

# Linux SQL matrix: enter sa password privately, once. Stop other netem owners first.
python3 scripts/openasync_health_sweep.py --mode sql --prompt-sql --netem \
  --binary /path/to/PerformanceTests.dll --output /path/to/new-results \
  --rtt 0 20 50 100 200 --repetitions 3
```

These commands assume the working directory is `eng/pipelines/perf`. Use `--dotnet` if the
runtime is not on PATH. `--only w16 w64 w256 w64-long w64-min64 w64-max16 w64-v1 w64-warm`
selects a smaller matrix. `--revision` records the tested source revision; binary and driver
SHA-256 hashes are also retained. Do not rebuild into the binary directory during a sweep.

RTT arguments are **added round-trip delay**. For 100 ms RTT, the queue applies 50 ms in
each direction. Netem is Linux-loopback SQL TCP/1433 only; it refuses a foreign root queue,
including on a requested zero-delay run. Do not overlap another workload on the same SQL server.
Warmup may take considerable time under latency. A case has a process watchdog; failures remain
explicit rather than silently retried.

## OpenAsync path regression suite

`--suite open-paths` is the balanced success-path suite, rather than the V2-heavy exploratory
`--suite study` ladder. It defines **20 cases per worker count** greater than one:

| Mode | Initialization / pool state | Workloads | Purpose |
|---|---|---|---|
| Pooled V1 and V2 | Fresh process, empty pool | Open/close and Open/query/close | Cold physical creation and startup scheduling |
| Pooled V1 and V2 | Initialized driver, cleared pool | Both | Physical creation without the first driver/login initialization |
| Pooled V1 and V2 | Prewarmed pool, enough connections for every worker | Both | Available pooled checkout/return and async query continuations |
| Pooled V1 and V2 | Prewarmed pool, capacity one quarter of worker count | Both | Oversubscribed acquisition/return and pool waiters |
| Non-pooled | Fresh process | Both | Repeated physical opens, with initial startup costs |
| Non-pooled | Initialized driver | Both | Repeated physical opens without the first initialization; **not a warm connection pool** |

The driver-initialized setup performs one synchronous connection open/close outside measurement.
For pooled cases it then clears that pool. This initializes common login code but does not
promise every async-only JIT or runtime path is warmed. `WarmPool` and `WarmDriver` are mutually
exclusive, recorded setup choices.

Use **Open/close** to isolate acquisition/return throughput and **Open/query/close** to keep
connections borrowed during real I/O. Warm Open/close can finish inline and may not actually
exhaust a pool even when worker count exceeds capacity. The raw export now records
`Final.InlineOpens` and `Final.PendingOpens` (task completed/not completed when OpenAsync returned).
Check those and retained timings; do not equate pending opens exclusively with pool waits or
claim every oversubscribed case exercised contention without evidence.

```bash
python3 scripts/openasync_health_sweep.py --mode sql --suite open-paths \
  --workers 1 16 64 256 --prompt-sql --netem --rtt 0 100 \
  --binary /path/to/PerformanceTests.dll --output /path/to/baseline --repetitions 3
```

At one worker the impossible oversubscribed cases are omitted. The baseline defaults to managed
SNI. Repeat on Windows with `--native-sni` (and without Linux-only netem) for native SNI.
Native SNI on Linux is explicitly rejected rather than silently measuring managed networking.
Keep SNI, OS, runtime, query, timeout, and worker count identical across baseline/candidate.
Connection strings can select the authentication/protocol configuration appropriate to the lab;
do not change them between paired runs.

After building a candidate, use the same suite/settings with
`--baseline-summary /path/to/baseline/summary.csv`. This additionally gates:

- Every candidate repetition must pass the scheduler coverage and successful-work checks.
- Per-case median completed-work throughput must not fall more than 10% below baseline.
- Per-case peak process threads must not exceed the larger of baseline plus eight threads or
  baseline times 1.25. This catches shifting the cost into dedicated threads.
- The case settings, RTTs, repetition identities/count, runtime, OS, and CPU counts must match.
  Missing/duplicated repetitions or an invalid-work baseline are rejected.

These are **initial explicit regression tolerances**, not statistically calibrated universal
limits. Review repeated paired measurements on the performance host before changing them.
The baseline itself may have the known scheduler failure, but must perform valid SQL work.
An absolute failure is not waived because the candidate is less bad. The comparison cannot
verify from these files that operators used the same database or equally quiet host; that
remains part of controlled execution.

### What this protects, and what it does not yet protect

This suite covers the main **success-path families** of OpenAsync, not every possible internal
branch. The query workload is meaningful in each family, but one query variant cannot substitute
for all the rows above. Existing functional tests remain necessary for correctness.

| Additional dimension | Current coverage / remaining requirement |
|---|---|
| Cold vs warm, V1 vs V2, non-pooled | Explicit symmetric success-path cases |
| Managed vs native SNI | Selectable; needs runs on each supported platform/runtime |
| Failed prelogin / blocked establishment | Separate `--mode stalled` negative control for V1/V2/non-pooled; not a successful-SQL gate |
| Cancellation of in-flight work | Deadline cancellation and bounded drain in every run; not exhaustive cancellation-phase assertions |
| Natural pool-exhaustion/login timeouts | **Not protected by the default eight-second success suite**; use dedicated longer, expected-failure cases |
| Reconnect, broken pooled connections, ClearPool during live work | Not isolated by this suite |
| Transactions, access-token callbacks, authentication variants, failover, MARS | Not exhaustively parameterized; require focused scenarios / existing manual tests |
| Framework and platform matrix | This performance executable targets modern .NET; it does not certify .NET Framework behavior |

The scripts are executable gates available to the performance harness. They are **not yet an
automatically scheduled required PR CI job**: adding a lab job with managed credentials and
exclusive network ownership is a separate integration step. Do not describe the added cases
alone as complete CI protection for every OpenAsync path.

Use `--duration-seconds 30 --connect-timeout-seconds 15` to observe beyond the connection timeout.
These settings are persisted in every case and must match between baseline and candidate.
This extends observation; it does not itself create or assert an expected timeout/cancellation
scenario. Failure-path correctness needs dedicated expected-outcome tests, not the successful-SQL gate.

## What is measured

- Every 25 ms, a dedicated observer posts a bounded ThreadPool callback and samples ThreadPool
  threads, total process threads, queue depth, allocation, and GC counters.
- The raw observer export retains posting and execution timestamps, producer lateness/misses,
  dropped and outstanding callbacks. Its 64-probe capacity intentionally matches the playground.
- Whole-window dispatch p99 uses posting cohorts, including execution during the one-second
  recovery. `worst_1s_completion_p99_ms` approximates the playground's one-second completion
  cohorts; it is not the same statistic.
- First work is offered before `Task.Run`, so worker startup delay is visible separately from
  OpenAsync duration. Operations record open-end and cycle-end (including disposal) separately.
  `first_acquisition_from_offer_p99_ms` includes initial worker queueing through open completion;
  it must not be confused with `first_open_success_p99_ms`, which starts inside the worker.
- Successful and failed-open durations are never mixed. The first open per worker is reported
  separately; thousands of warm opens can otherwise hide a cold-start stall in overall p99.
- A fixed observation deadline is enforced by the main thread, not a ThreadPool timer.
  At the deadline, unstarted workers, in-flight operations, counters, and missing probes remain
  visible. Cancellation is followed by bounded drain. Incomplete runs remain incomplete.
- Operation samples retain the first 100,000 completions plus each worker's first completion,
  even if a fast worker filled the cap first. Counts remain uncapped and dropped sample
  counts are exported. Open percentiles from a truncated sample are **not full-run percentiles**;
  the independently collected scheduler probes are unaffected by this retention limit.
  Older artifacts without every worker's first record report their cold percentile as N/A.

The observer and outcome recording add overhead. Process-cold JIT, authentication, and TLS
initialization are part of cold results; they are not all connection-pool costs.

## Proving an improvement

Use the same runtime, host, SQL server, network condition, query, connection timeout, worker count,
pool capacity, and ThreadPool settings for baseline and candidate. Alternate build order and run
at least three independent process repetitions (use more for a release decision). Retain raw JSON,
CSV, manifests, workload failures, and censored work, not just averages or screenshots.

The exploratory scheduler budget is **100 ms dispatch p99**, not a driver SLA. A result over the
budget is a known failure even if probes were lost. A low result is only provisionally passing
with at least 100 completed probes, no lost/missed/outstanding probes, no resource-sample loss,
and a completed observer/run. Otherwise it is **Insufficient**, not healthy.

Add `--require-healthy-sql` to make the sweep return a failing exit code if **any** repetition
is over budget or insufficient, has workload failures/unstarted workers, completes no SQL cycles,
or exits unsuccessfully. Negative controls cannot pass this gate. Throughput and resource
non-regression must still be compared against the matched baseline; this absolute gate alone
is not evidence that a candidate is better.

A candidate should:

1. Meet the declared scheduler budget in every repetition of the critical cold SQL case, not
   merely improve a median. Report maximum and worst one-second p99 as well.
2. Maintain successful completed-work throughput and eliminate unexpected SQL/open failures.
   Record cancellation at the fixed deadline separately; it is not a connect timeout.
3. Not move the problem to unbounded dedicated threads, memory, or queueing. Compare process
   thread peaks as well as ThreadPool peaks.
4. Preserve warm, low-concurrency, V1, and non-pooled controls. LongRunning should be treated as
   a scheduling comparison, not automatically selected as the winning implementation.
5. Reproduce the baseline failure before crediting a candidate. The stalled-prelogin control
   proves blocking sensitivity, but **cannot prove healthy authenticated SQL under real RTT**.

The existing BDN warm-pool benchmark remains useful for steady-state throughput. This separate
entry point deliberately avoids its warmup lifecycle and exports failed/pending operations
that successful-only latency summaries omit. No production driver behavior is changed here.

## Initial authenticated reproduction

On the 16-logical-processor WSL developer host, three fresh cold V2 runs with 64 workers,
pool max 100, TP min/max 16/32767 and 100 ms added RTT measured dispatch p99 of
2,690-2,791 ms. The prewarmed control was 5.5-17.0 ms. This confirms that prewarming can
hide the cold-start regression. These are observations, not portable timing assertions.

The 64-worker LongRunning comparison measured 8.4-21.2 ms dispatch p99 at that RTT, but
increased median process-thread peak from 29 to 91. V1 and non-pooled controls kept scheduler
tails low while completing substantially less cold-window SQL work. Do not credit a fix solely
for moving work to dedicated threads or reducing the work actually performed.

Focused baseline/candidate reproduction:

```bash
python3 scripts/openasync_health_sweep.py --mode sql --prompt-sql --netem \
  --binary /path/to/PerformanceTests.dll --output /path/to/new-results \
  --only w64 --rtt 100 --repetitions 3 --require-healthy-sql
```

The current baseline is expected to fail this scheduler-health gate. Preserve warm V2, V1,
non-pooled, and high-concurrency comparisons when evaluating a candidate. An eight-second
window cannot establish natural incidence of fifteen-second connection timeouts.

Render a completed homogeneous Linux RTT sweep with all repetitions and coverage-aware graphs:

```bash
python3 scripts/openasync_rtt_report.py /path/to/results/summary.csv \
  --output /path/to/authenticated-report.html
```
