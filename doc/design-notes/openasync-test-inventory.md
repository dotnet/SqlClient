# OpenAsync async-performance test inventory

## Purpose and status

This inventory defines the proposed tests for evaluating how well `OpenAsync`
behaves asynchronously and how its observable performance compares with `Open`.
It accompanies the [latency dependency catalog](openasync-latency-catalog.md)
and [investigation priorities](openasync-latency-priorities.md).

Recorded on October 12, 2026, using the catalog's source baseline
`c7ab087c26b8c3c29275cca671799138b9c31987`.
This is a design inventory, not an implementation checklist claiming completed
work or measured results. The subsequent implementation-plan comparison
inventories all repository C# projects and maps relevant existing test assertions
and workloads across the test/benchmark projects. It is a source-level comparison,
not evidence that tests passed or performance was measured.

The [implementation plan](openasync-implementation-plan.md) maps these cases to
existing projects, shared infrastructure, delivery increments, and CI.

Coverage labels:

- **Covered:** an existing test substantially measures the stated objective.
- **Partial:** a related workload or behavior test exists, but requires additional
  controls or measurements.
- **New:** no dedicated coverage was identified in the investigation.

Test IDs are stable references. There are 32 active test definitions across four
layers: 5 mechanism benchmarks, 16 controlled dependency experiments, 5
cross-cutting behavior/recovery families, and 6 end-to-end scenarios. R4 and R5 are
retained as excluded references, bringing the historical inventory to 34. Parameter combinations
and applying recovery families to multiple fixtures will produce more individual
test cases.

The suite defines correct async behavior even when current code fails. Pass/fail
is structural; performance numbers are reported without numerical timing gates.
The detailed specification's [agreed requirements](openasync-test-cases.md#agreed-requirements)
record cancellation, compatibility-sensitive timeout behavior, platform focus,
and maintainer approval of unavoidable exceptions.

## 1. Fast-path and mechanism benchmarks

These measure implementation overhead without intentionally introducing external
latency. Existing runner names refer to files under
`src\Microsoft.Data.SqlClient\tests\PerformanceTests\BenchmarkRunners`.

| ID | Test | Existing coverage |
| --- | --- | --- |
| M1 | Warm pooled open/close, sync versus async, one caller, pool depths 1/10/100 | **Covered:** `ConnectionPoolChurnRunner`. Measures a complete 1,000-operation loop including construction and disposal, not isolated `OpenAsync`. |
| M2 | Basic connection lifecycle matrix: pooling on/off and MARS on/off, sync versus async | **Covered:** `SqlConnectionRunner`. Nonpooled cases are end-to-end connection measurements rather than pure microbenchmarks. |
| M3 | Warm `OpenAsync` synchronous prefix and completion rate: task-return duration and proportion completed at return | **Partial:** M1/M2 exercise the path but do not report these measurements separately. |
| M4 | Pool waiter registration and cancellation bookkeeping: allocation and CPU cost excluding the deliberate wait | **New:** requires a controlled component-level fixture. |
| M5 | Pool handoff: resource release to waiting-opener completion, including scheduling cost | **Partial:** contention/churn workloads exercise handoff but do not isolate it. |

Narrow DNS-dispatch, authentication-dispatch, or parser microbenchmarks should be
added when needed to explain a measured regression, rather than automatically
benchmarking every helper.

## 2. Controlled dependency experiments

Each experiment must observe arrival at the intended boundary, hold the dependency
pending, and release it. Shared scenarios serve structural tests in the normal
test projects and BenchmarkDotNet measurement runs in the existing
`PerformanceTests` project. Measurement runs include task-return latency, occupied threads,
unrelated-work responsiveness, and release-to-completion delay; behavioral runs
need the evidence for their structural assertions, not the full measurement stack.
See [test placement and execution modes](openasync-test-cases.md#test-placement-and-execution-modes).

| ID | Test | Catalog IDs | Existing coverage |
| --- | --- | --- | --- |
| D1 | DNS pending during endpoint resolution, with controlled cache state and lookup counts | OA-13 | **New.** |
| D2 | SPN canonical-name resolution pending, distinct from transport DNS; explicit-SPN control | OA-17 | **New.** |
| D3 | Pool exhausted: retain all connections, start additional opens, then release capacity deliberately | OA-04 | **Partial:** `ConnectionPoolContentionRunner`, `ConnectionPoolStressRunner`, and manual pool-exhaustion tests. Deterministic hold/release and async-quality measurements are needed. |
| D4 | Pooled creation admission: hold physical creation pending and observe other opens under the creation limit | OA-05 | **Partial:** `ConnectionPoolRampRunner` exercises growth but does not isolate admission from physical establishment. |
| D5 | Nonpooled creation queue: hold preceding creations pending and observe queued opens | OA-06 | **Partial:** nonpooled `ParallelAsyncConnectionRunner` exercises concurrent creation but does not isolate the factory queue. |
| D6 | TCP establishment pending, including failed-address and multi-address attempts | OA-15 | **Partial:** existing nonpooled/ramp benchmarks perform real connects. Controlled establishment and blocked-thread attribution are new. |
| D7 | Named-pipe establishment pending, where supported | OA-16 | **New:** transport-specific counterpart to D6. |
| D8 | Prelogin response withheld after receiving the request | OA-19 | **New** for the proposed performance measurements. |
| D9 | Login/authentication response withheld, including final login completion and fragmented delivery | OA-23, OA-26, OA-27 | **New** for the proposed performance measurements. |
| D10 | Write backpressure during opening, with evidence that the write actually became pending | OA-18, OA-23, OA-26 | **New:** requires transport-level control; merely stopping server reads may not fill buffers. |
| D11 | Token provider or `AccessTokenCallback` held pending, including cached completion and refresh | OA-24, OA-25 | **Partial:** ManualTests covers callback success and pooled expiry/replacement for both pools and APIs; Azure.Test owns real-provider controls. Held acquisition, concurrent-refresh cancellation, and measurements remain new. |
| D12 | Integrated-authentication token generation and challenge handling, controlled provider plus real-platform confirmation | OA-22, OA-26 | **Partial:** simulated SSPI sync/async login and real integrated-auth/SPN controls exist. Held generation/challenge and async-quality evidence remain new. |
| D13 | TLS handshake held pending, separately for Strict and prelogin-negotiated encryption | OA-20 | **Partial:** UnitTests has held-handshake cancellation settlement/no-deadlock coverage; ManualTests has real encryption/certificate controls. Exact cancellation/cleanup, verified resumption, and performance evidence need extensions. |
| D14 | Certificate validation/file-access cost, separating warm/cold state and platform behavior | OA-21 | **Partial:** managed certificate-helper matching/missing-file failures and integration validation matrices exist. Warm/cold cost, missing async variants, and recovery observations remain; validation must stay enabled. |
| D15 | Warm pooled activation requiring server I/O: enlistment, unenlistment, isolation reset/reassertion | OA-30, OA-32, OA-33 | **Partial:** UnitTests and ManualTests cover reset/reassertion, ownership, and budget/state controls; `TransactionScopeIsolationRunner` measures related workloads. Add held activation and cancellation/measurement evidence, not duplicate baseline tests. |
| D16 | Distributed enlistment/promotion, including coordinator and SQL Server waits | OA-31 | **Partial:** Windows distributed-transaction regressions exist, including a quarantined pooled-preservation test. Controlled coordinator/SQL waits, cancellation, and performance evidence remain new. |

For D1/D2, D6/D7, D12, D13/D14, and D16, substitutes alone are insufficient.
Real OS/native/provider integration runs must confirm that a substitute did not
remove the blocking behavior under investigation.

Fixture constraints:

- A dedicated invoking thread and independent observer allow the experiment to
  detect a blocked invocation without preventing release of the dependency.
- Delaying server `Accept` is not sufficient to stall TCP establishment: the OS
  can complete the handshake before the application accepts.
- An asynchronous resolver substitute cannot establish that production DNS is
  nonblocking.
- Protocol-aware response gates must distinguish prelogin, authentication
  challenges, and final login completion.
- Application authentication callbacks establish driver composition behavior,
  not the behavior of Azure Identity, MSAL, or OS authentication.
- Posted background receives are not automatically waits on the open's critical
  path.

## 3. Cross-cutting behavior, cancellation, and recovery

These are reusable families applied to relevant dependency fixtures, not
necessarily separate harnesses.

| ID | Test | Existing coverage |
| --- | --- | --- |
| R1 | Cancellation before invocation and while pending: no acquisition for an already-canceled token; underlying-work termination and resource recovery | **Partial:** component cancellation/retired-pool draining and held-TLS settlement exist. Public precancellation/no-acquisition and underlying-work cleanup remain gaps. Manual command-cancellation tests are not OpenAsync coverage. |
| R2 | Timeout behavior: budget consumption, terminal outcome, and post-timeout recovery | **Partial:** existing virtual-time pool budget, login-switch selection, timer, and activation-budget tests provide structural coverage. Add missing phase-specific timeout/recovery evidence and measurements, not another generic budget suite. |
| R3 | Failure/release/cancellation races: no false success, leaked connection, stale waiter, or lost capacity | **Partial:** stress tests exercise races broadly; deterministic boundary-specific cases are needed. |
| R4 | Caller-context independence on genuinely pending paths | **Excluded by scope decision:** no known defect motivating this test. Neither caller `SynchronizationContext` nor custom `TaskScheduler` independence is tested by this suite. |
| R5 | Continuation isolation: a waiting consumer must not unexpectedly occupy a sensitive releasing/completion path | **Excluded by scope decision:** no known defect motivates dedicated expensive/reentrant consumer-continuation tests. Prioritize natural `async`/`await` composition and finish required bookkeeping before exposing completion; this does not guarantee queued continuations. |
| R6 | Retries, failover, and routing: repeated dependency visits, backoff behavior, cancellation, and attempt counts | **Partial:** routing success/limits and sync/async retry exhaustion/counts/`Retrying`-event cancellation exist. Add held attempt/backoff, caller-token cancellation, cleanup and composition evidence; event cancellation is not caller-token cancellation. |
| R7 | Recovery after a cancellation/failure burst: subsequent healthy opens regain normal throughput, latency, and capacity | **Partial:** `ConnectionPoolStressRunner.PoolExhaustionRecovery` is related but does not establish recovery from cancellation/failure storms. |

These families primarily cover catalog entries OA-28 and OA-34 through OA-39,
plus the dependency boundary at which they are exercised.

Assertions must distinguish current supported behavior, desired async behavior,
and known limitations. A characterization result must not silently become a
passing assertion that blocking is acceptable.

## 4. End-to-end concurrency scoreboard

These scenarios extend the existing `PerformanceTests` BenchmarkDotNet workloads,
alongside D-case measurement classes. They are explicit perf runs, not default
unit tests. Long experiments use scenario-sized invocations rather than treating
every case as a microbenchmark; per-open metrics supplement BDN operation timing.
Reuse the current perf configuration/scripts, not another runner setup.

Each scenario compares baseline and candidate `OpenAsync`, with baseline and
candidate `Open` controls, in fresh processes across a concurrency sweep.
This prevents a slower sync implementation from being mistaken for async progress.

| ID | Test | Existing coverage |
| --- | --- | --- |
| E1 | Warm pooled reuse, sufficient capacity, immediate return | **Partial:** churn and rapid-fire benchmarks provide the workload. A consistent sweep and per-open/resource measurements are needed. |
| E2 | Saturated pool, fixed capacity, controlled connection hold time | **Partial:** `ConnectionPoolContentionRunner` is close, but includes `SELECT 1` and lacks the complete scoreboard. |
| E3 | Sustained nonpooled physical establishment | **Partial:** `SqlConnectionRunner` and `ParallelAsyncConnectionRunner`. Sustained paired runs, achieved concurrency, per-open distributions, and resource tracking are needed. |
| E4 | Cold pooled ramp, retaining connections until the burst finishes | **Partial:** `ConnectionPoolRampRunner` implements the key workload. Extend reporting and historical comparisons. |
| E5 | Open, first query, close; sync/async opening variants with otherwise matched work | **Partial:** contention/stress runners include queries. Keep separate from open-only results because opening can defer work until the first command. |
| E6 | Fixed-arrival-rate overload and recovery, including bursts | **Partial:** bursty stress coverage exists, but independently scheduled arrivals, offered-load accounting, and recovery measurements are new. |

### Workload controls

For the primary application-oriented comparison, both sync and async workers run
on the normal .NET ThreadPool. Sync workers call `Open` directly; async workers
await `OpenAsync`. Dedicated-thread sync runs are optional diagnostic controls,
not a replacement for the primary comparison.

Each worker has at most one outstanding open. A common start gate, identical
connection settings, identical work outside opening, and known pool state keep
variants comparable. Where a hold is required, both variants use the same
asynchronous hold mechanism so only the opening API differs.

An initial concurrency sweep can use 1, 8, 32, 64, 128, and 256 workers, adjusted
to lab capacity and pool-size boundaries. Measure achieved concurrency and
outstanding opens rather than assuming the configured worker count was attained.

Use fresh processes, repeat runs, alternate or randomize comparison order, and
monitor SQL Server capacity. ThreadPool growth, pool state, credential caches,
and server saturation can otherwise confound the comparison.

### Required scoreboard measurements

| Measurement | Purpose |
| --- | --- |
| Successful opens per second | Delivered throughput |
| Individual-open p50/p95/p99 latency | Typical and tail caller experience |
| `OpenAsync` invocation-to-task-return latency | Blocking in the public async entry point |
| Actual outstanding opens and startup/admission delay | Detect runs that never achieve requested concurrency |
| Thread counts and blocked-stack evidence | Distinguish useful async waiting from occupied threads |
| CPU, memory, allocations, and GC behavior | Resource cost at the achieved throughput |
| Failure, timeout, and cancellation counts | Avoid success-only bias |
| Unrelated-work latency | Shared-process responsiveness |
| Offered, started, completed, and dropped/rejected work for E6 | Explain overload and generator capacity limits |

Open latency starts immediately before the API invocation and ends at successful
or unsuccessful completion. Task becoming terminal and consumer resumption are
distinct observation boundaries. Batch time divided by operation count is not
individual-open latency.

The primary plots are throughput, p99 open latency, thread count, and unrelated-work
p99 latency against concurrency, showing baseline/candidate async and sync controls.

Performance progress means competitive warm-path cost and improved behavior when waiting:
comparable or better throughput/tails with fewer occupied threads, better
responsiveness, or a higher concurrency level before degradation. Matching
elapsed time with materially lower resource consumption is a useful improvement.
These are reported comparisons, not numerical pass/fail gates. Structural
correctness determines test outcomes.

## Execution matrix

Use targeted combinations, not the full Cartesian product:

- Legacy and V2 connection pools.
- Linux managed SNI as the primary focus; Windows managed and native SNI remain
  tracked configurations.
- Authentication, encryption, transaction, cache-state, and retry variants only
  where they affect the boundary being tested.

The existing performance project targets .NET 10 only. A .NET Framework
performance commitment requires a separate execution/build surface; modern-runtime
results do not substitute for it.

## Recommended initial delivery

1. E1-E4 establish the end-to-end progress scoreboard.
2. D1, D3, D6, and D8/D9 isolate its most important waits.
3. R1-R3 accompany those fixtures.
4. M1/M2 remain fast-path regression controls.
5. Authentication, TLS, and transaction-specific coverage follow.

## Existing test locations

- Performance runners:
  `src\Microsoft.Data.SqlClient\tests\PerformanceTests\BenchmarkRunners`.
- Manual pool stress/exhaustion:
  `src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\ConnectionPoolTest\ConnectionPoolStressTest.cs`.
- Manual retry timing:
  `src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\ConnectivityTests\ConnectivityTest.cs`,
  including `ConnectionOpenAsyncDisableRetry`.
- General stress framework and runner:
  `src\Microsoft.Data.SqlClient\tests\StressTests`.
- Real Azure-provider tests:
  `src\Microsoft.Data.SqlClient.Extensions\Azure\test`.
- Callback expiry/replacement:
  `src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\AADFedAuthTokenRefreshTest\AADFedAuthTokenRefreshTest.cs`.

Existing pipeline baseline comparison and reporting infrastructure lives under
`eng\pipelines\perf`. It is reusable infrastructure, not evidence that the new
per-open or async-quality measurements already exist.
