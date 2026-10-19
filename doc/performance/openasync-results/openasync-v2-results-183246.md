# OpenAsync V2 performance scorecard

**Recovered analysis of run 183246 — measurements from October 15, 2026; analysis recovered October 19.**

**Bottom line:** warm reuse and matched-query paths show little reason to expect a throughput win from async opening. Under contention/nonpooled load, async preserves worker availability, but it does not remove capacity limits, latency or allocation costs. Cold pooled ramp remains an important exception: both APIs show substantial scheduling delay.

## Scope and evidence

- Current source only: `abff493d9d80d8e9a05e14c42054b6592bda815f`, `8.0.0-preview1-dev`, **V2 pooling enabled**, optimized async behavior enabled.
- Ubuntu 22.04.5, .NET **10.0.13**, 16 reported processors. These are not the older legacy-pool/M1/M2 runs and must not be treated as a paired comparison with them.
- **96 configurations × 3 process repetitions × (3 actual + 2 warmup invocations).** 864 actual and 576 warmup rows; warmups are excluded below.
- **1,088,667 successful measured opens** across Sampled and Diagnostic modes; **544,178** in Sampled mode. **241,920 successful queries** across both modes. No recorded open, query or lifecycle failure/cancellation.
- Timing/throughput tables use **Sampled** mode only; diagnostic measurements are separate.
- Each displayed number is the median of **nine per-invocation aggregates**, spanning **three independent processes**. Those nine observations are not nine independent replicates. A median of invocation p95s is **not** a pooled-operation p95. No significance claim or numerical gate.

## Sampled throughput across the complete matrix

Completed successful opens per second; E5 includes the matched async query/lifecycle, and E6 is a whole-run accepted/completed rate, not its offered rate.

| Scenario | Workers | Open /s | OpenAsync /s | Async / sync |
|---|---:|---:|---:|---:|
| E1 | 1 | 129,476.0 | 122,347.5 | 0.945 |
| E1 | 8 | 528,434.3 | 463,306.5 | 0.877 |
| E1 | 32 | 897,104.6 | 838,966.0 | 0.935 |
| E1 | 64 | 701,694.3 | 680,862.4 | 0.970 |
| E2 | 1 | 97.1 | 97.0 | 0.999 |
| E2 | 8 | 381.5 | 384.1 | 1.007 |
| E2 | 32 | 381.4 | 385.5 | 1.011 |
| E2 | 64 | 381.9 | 384.0 | 1.006 |
| E3 | 1 | 147.1 | 140.7 | 0.957 |
| E3 | 8 | 928.9 | 880.2 | 0.948 |
| E3 | 32 | 1,404.4 | 1,297.0 | 0.924 |
| E3 | 64 | 1,384.8 | 1,318.2 | 0.952 |
| E4 | 1 | 123.6 | 123.5 | 0.999 |
| E4 | 8 | 582.2 | 613.9 | 1.054 |
| E4 | 32 | 1,150.0 | 1,115.9 | 0.970 |
| E4 | 64 | 1,208.3 | 1,220.9 | 1.010 |
| E5 | 1 | 3,129.6 | 3,182.8 | 1.017 |
| E5 | 8 | 21,194.9 | 20,547.1 | 0.969 |
| E5 | 32 | 45,407.4 | 46,273.7 | 1.019 |
| E5 | 64 | 46,693.1 | 47,041.5 | 1.007 |
| E6 | 1 | 59.5 | 59.5 | 1.000 |
| E6 | 8 | 155.6 | 156.5 | 1.005 |
| E6 | 32 | 159.5 | 160.6 | 1.007 |
| E6 | 64 | 165.1 | 165.9 | 1.005 |

### E1 — warm pooled reuse

All measured async opens completed before return in every Sampled invocation. This exercises the synchronous-completion fast path, not asynchronous waiting. At 64 workers, median throughput was 701,694/s sync versus 680,862/s async; allocations were approximately 606 versus 798 bytes per successful open. Broad invocation ranges overlap: 578,441–1,097,858/s sync and 531,479–1,033,978/s async. These short instrumented runs do not establish a stable small regression.

### E2 — contention for four pooled connections

The matched 10 ms holds impose an approximate capacity ceiling of 400/s. Both APIs reach about 382–384/s at 64 workers. The difference is waiting cost, not removal of that bottleneck:

- Open p95: **160.21 ms sync / 158.54 ms async**.
- Async pending task-return p95: **0.0064 ms**, versus approximately 158.54 ms to finish the open.
- Separate Diagnostic runs: median peak busy-worker proxy **65 sync / 3 async**; contained stacks observed up to **61** blocking-open threads for sync, versus **0 observed** for async.
- Unrelated-work probe p95 remains about **0.12 ms for both** here. Fewer occupied workers does not automatically imply a measured responsiveness benefit in every workload.
- Process CPU normalized per successful open: **0.477 ms sync / 0.270 ms async**. Allocation tradeoff: **1,766 bytes sync / 2,638 bytes async**.

### E3 — repeated nonpooled opens

This is the strongest observed responsiveness benefit, with a real cost tradeoff at 64 workers:

| Metric | Open | OpenAsync |
|---|---:|---:|
| Completed opens/s | 1,384.84 | 1,318.23 |
| Invocation open p95, ms | 21.34 | 84.15 |
| Initial worker admission p95, ms | 39.61 | 4.47 |
| Median peak outstanding opens | 23 | 64 |
| Allocated bytes / successful open | 46,275 | 67,209 |
| Process CPU ms / successful open | 2.874 | 3.863 |
| Diagnostic unrelated-work p95, ms | 41.341 | 0.046 |
| Diagnostic median peak busy-worker proxy | 25 | 4 |
| Diagnostic median peak pending work items | 54 | 5 |

Async is about **4.8% lower in median throughput** and **45.2% higher in allocation** here, while allowing much more work to be outstanding and preserving unrelated-work responsiveness. The sync open timer begins only after worker admission; its lower per-open p95 does not mean the whole scheduled workload had less latency. Admission occurs once per worker and cannot simply be added to every operation. Contained stacks observed up to 26 blocking-open threads in sync captures and none in the sampled async captures; that is sparse evidence, not proof of zero blocking.

### E4 — retained cold pooled ramp

**Prompt task return does not imply prompt cohort progress.** At 64 workers, both opening APIs behave similarly at the whole-cohort level:

| Metric | Open | OpenAsync |
|---|---:|---:|
| Completed opens/s | 1,208.29 | 1,220.93 |
| Invocation open p95, ms | 17.75 | 18.05 |
| Initial worker admission p95, ms | 40.45 | 37.45 |
| Common-epoch completion p95, ms | 49.71 | 50.71 |
| Median peak outstanding opens | 17 | 16 |
| Diagnostic unrelated-work p95, ms | 40.957 | 40.949 |

Async pending task-return p95 is only **0.0159 ms**, but unrelated work and later worker admission still wait tens of milliseconds. No contained external captures were obtained for these short E4 invocations. These measurements identify a cold-ramp scheduling concern; they do not isolate its implementation-level cause. It needs controlled attribution, not a guessed fix.

### E5 — identical async query after either opening API

At 64 workers, whole-workload throughput is **46,693/s sync-open / 47,042/s async-open**, with overlapping ranges. Query p95 is **2.857 / 3.102 ms** respectively, versus open p95 of **0.0111 / 0.0130 ms**. All measured async opens completed at return; the query/lifecycle dominates this configuration. Allocation is **12,490 / 12,678 bytes per successful open**. This does not demonstrate an end-to-end benefit from choosing OpenAsync for already warm opens.

### E6 — independent offered load, bounded overload, and recovery

Each invocation schedules 50 baseline, 1,600 overload and 50 recovery offers (25/800/25 per second for two seconds each). The waiting channel holds 64; active workers can hold additional admitted work. **Rejections are intentional load accounting, not driver errors.**

| Workers | Sync/async overload accepted (of 1,600) | Sync/async overload rejected | Sync/async overloaded offered-to-close p95, ms |
|---:|---:|---:|---:|
| 1 | 258 / 258 | 1342 / 1342 | 675.10 / 673.93 |
| 8 | 834 / 840 | 766 / 760 | 190.45 / 187.57 |
| 32 | 858 / 864 | 742 / 736 | 253.34 / 250.24 |
| 64 | 891 / 896 | 709 / 704 | 337.85 / 334.14 |

All accepted work drained. Every Sampled invocation had zero backlog by the end of Recovery; the last recovery second completed 25 requests and the last half-second 12, with zero final backlog. Some configurations rejected one early recovery offer while the backlog was still full. At 64 workers, Recovery offer-cohort p95 remains **209.51 ms sync / 204.64 ms async**, so end-of-phase recovery must not be confused with immediate recovery.

Overload-end median backlog grows from 65 at one worker to 128 at 64 workers. The increase in accepted offers across worker counts partly reflects extra in-flight buffering, not increased database service capacity. Async does not eliminate overload or queueing. At 64 workers its Diagnostic median peak busy proxy is 3 versus 63 for sync, but both probe p95s stay around 0.12 ms.

## Diagnostic evidence and limits

**177 of 432 Diagnostic actual invocations had at least one contained stack and SQL capture; 255 were insufficiently sampled.** Covered means some usable observations, not complete temporal coverage or proof of nonblocking. Sampled mode intentionally has no external captures.

| Scenario | Covered / 72 Diagnostic invocations | Contained stack captures |
|---|---:|---:|
| E1 | 0 / 72 | 0 |
| E2 | 69 / 72 | 195 |
| E3 | 36 / 72 | 54 |
| E4 | 0 / 72 | 0 |
| E5 | 0 / 72 | 0 |
| E6 | 72 / 72 | 288 |

- No measured Diagnostic invocation reported dropped probes, skipped-GC observations or negative heap estimates. Last-GC polling is nevertheless not a complete GC trace.
- Short E1/E4/E5 windows are undersampled; a zero busy-worker proxy from polling is not proof that no workers were used. Probe distributions can be unavailable.
- Diagnostic observers perturb the process; stack capture can suspend it. Do not combine Diagnostic timing with Sampled timing or assign sparse stack counts to every open.
- SQL observations are instance-wide, include observers, and do not establish client-only attribution.
- Allocations and CPU include process/instrumentation effects. Normalizing by successful opens does not turn them into precise per-operation allocation/CPU attribution.

## Full Sampled timing and resource table

Open and pending-return columns are medians of invocation p95s, in milliseconds. N/A pending-return means there were no pending-return observations, not zero return time.

| Scenario | API | Workers | Open p95 ms | Pending return p95 ms | Completed at return | Bytes / successful open |
|---|---|---:|---:|---:|---:|---:|
| E1 | Sync | 1 | 0.0019 | N/A | N/A | 618 |
| E1 | Async | 1 | 0.0028 | N/A | 1.0000 | 810 |
| E1 | Sync | 8 | 0.0046 | N/A | N/A | 608 |
| E1 | Async | 8 | 0.0062 | N/A | 1.0000 | 800 |
| E1 | Sync | 32 | 0.0059 | N/A | N/A | 607 |
| E1 | Async | 32 | 0.0065 | N/A | 1.0000 | 799 |
| E1 | Sync | 64 | 0.0103 | N/A | N/A | 606 |
| E1 | Async | 64 | 0.0109 | N/A | 1.0000 | 798 |
| E2 | Sync | 1 | 0.0081 | N/A | N/A | 948 |
| E2 | Async | 1 | 0.0086 | N/A | 1.0000 | 1,140 |
| E2 | Sync | 8 | 11.1227 | N/A | N/A | 1,762 |
| E2 | Async | 8 | 11.2816 | 0.0063 | 0.0078 | 2,630 |
| E2 | Sync | 32 | 75.6114 | N/A | N/A | 1,765 |
| E2 | Async | 32 | 73.8708 | 0.0065 | 0.0020 | 2,637 |
| E2 | Sync | 64 | 160.2100 | N/A | N/A | 1,766 |
| E2 | Async | 64 | 158.5352 | 0.0064 | 0.0010 | 2,638 |
| E3 | Sync | 1 | 7.5526 | N/A | N/A | 46,533 |
| E3 | Async | 1 | 7.9203 | 0.3273 | 0.0000 | 67,396 |
| E3 | Sync | 8 | 11.8107 | N/A | N/A | 46,475 |
| E3 | Async | 8 | 12.6397 | 0.5840 | 0.0000 | 67,328 |
| E3 | Sync | 32 | 24.3319 | N/A | N/A | 46,282 |
| E3 | Async | 32 | 40.8450 | 0.2860 | 0.0000 | 67,215 |
| E3 | Sync | 64 | 21.3442 | N/A | N/A | 46,275 |
| E3 | Async | 64 | 84.1546 | 0.0092 | 0.0000 | 67,209 |
| E4 | Sync | 1 | 7.9582 | N/A | N/A | 55,824 |
| E4 | Async | 1 | 7.9768 | 0.0329 | 0.0000 | 56,928 |
| E4 | Sync | 8 | 12.8063 | N/A | N/A | 48,760 |
| E4 | Async | 8 | 12.4819 | 0.0277 | 0.0000 | 49,760 |
| E4 | Sync | 32 | 16.8651 | N/A | N/A | 47,976 |
| E4 | Async | 32 | 16.6793 | 0.0196 | 0.0000 | 48,798 |
| E4 | Sync | 64 | 17.7452 | N/A | N/A | 47,872 |
| E4 | Async | 64 | 18.0501 | 0.0159 | 0.0000 | 48,645 |
| E5 | Sync | 1 | 0.0113 | N/A | N/A | 12,501 |
| E5 | Async | 1 | 0.0131 | N/A | 1.0000 | 12,666 |
| E5 | Sync | 8 | 0.0104 | N/A | N/A | 12,489 |
| E5 | Async | 8 | 0.0128 | N/A | 1.0000 | 12,681 |
| E5 | Sync | 32 | 0.0110 | N/A | N/A | 12,486 |
| E5 | Async | 32 | 0.0127 | N/A | 1.0000 | 12,678 |
| E5 | Sync | 64 | 0.0111 | N/A | N/A | 12,490 |
| E5 | Async | 64 | 0.0130 | N/A | 1.0000 | 12,678 |
| E6 | Sync | 1 | 0.0131 | N/A | N/A | 1,346 |
| E6 | Async | 1 | 0.0142 | N/A | 1.0000 | 1,538 |
| E6 | Sync | 8 | 11.2033 | N/A | N/A | 1,942 |
| E6 | Async | 8 | 10.7771 | 0.0068 | 0.1054 | 2,732 |
| E6 | Sync | 32 | 75.3648 | N/A | N/A | 2,259 |
| E6 | Async | 32 | 73.6821 | 0.0071 | 0.1006 | 3,055 |
| E6 | Sync | 64 | 160.1795 | N/A | N/A | 2,823 |
| E6 | Async | 64 | 157.3437 | 0.0068 | 0.0954 | 3,645 |

## Variability at 64 workers

Median [minimum–maximum] across nine invocations; ranges are descriptive, not confidence intervals.

| Scenario | API | Throughput /s | Invocation open p95 ms |
|---|---|---:|---:|
| E1 | Sync | 701,694.3 [578,441.2–1,097,858.4] | 0.0103 [0.0054–0.0134] |
| E1 | Async | 680,862.4 [531,478.7–1,033,977.9] | 0.0109 [0.0055–0.0139] |
| E2 | Sync | 381.9 [378.8–385.8] | 160.2100 [158.9085–161.5986] |
| E2 | Async | 384.0 [381.4–386.2] | 158.5352 [157.1885–159.4332] |
| E3 | Sync | 1,384.8 [1,297.4–1,439.7] | 21.3442 [19.2065–24.4412] |
| E3 | Async | 1,318.2 [1,236.7–1,350.1] | 84.1546 [79.3430–102.4112] |
| E4 | Sync | 1,208.3 [1,024.0–1,351.1] | 17.7452 [16.2990–23.0309] |
| E4 | Async | 1,220.9 [1,066.6–1,287.1] | 18.0501 [15.5694–19.6578] |
| E5 | Sync | 46,693.1 [45,946.5–49,180.6] | 0.0111 [0.0102–0.0140] |
| E5 | Async | 47,041.5 [44,289.4–51,109.1] | 0.0130 [0.0124–0.0179] |
| E6 | Sync | 165.1 [164.5–165.3] | 160.1795 [159.1601–161.2398] |
| E6 | Async | 165.9 [165.4–166.1] | 157.3437 [156.4463–158.9292] |

## Delivery state and remaining work

[Run 183246](https://dev.azure.com/sqlclientdrivers/ADO.Net/_build/results?buildId=183246) completed measurements successfully on October 15. Artifact publication timed out, and the overall run was canceled on October 18. Teardown was abandoned; the existing VM was still accessible on October 19. No artifact is registered in Azure DevOps.

The compact summary fix was validated against all 1,440 rows without changing original evidence: **374,515,424 → 11,842,902 bytes (96.84% smaller)**. Generation/repackaging peaked at **75.9 MiB RSS**; independent bounded equivalence validation peaked at **50.8 MiB**. The 83 perf-script tests pass. The code changes are still uncommitted and unpushed.

This scorecard and its 94,531-byte structured analysis have been recovered locally with a verified transport SHA-256. The raw 98.5 MB archives and full compact summary have **not** been recovered locally; successful publication is not proven. No rerun, cancellation, teardown, new infrastructure/credentials, or audit exception was performed.

Next decisions/work remain: preserve/recover the full evidence via authorized lab access; publish the compact reporting change when authorized and validate the publisher; resolve the historical 7.0.2 baseline risk-policy decision; then complete a genuine baseline comparison. Controlled dependency fixtures D1–D16 remain later planned work.

## Provenance

- Derived compact summary SHA-256: `b55522d079c08d8664efad398c7b0471e6e2657315e045ab5e86619853746439`.
- Recovered analysis SHA-256: `73ea135abd463529f21296fd4d856e694c56906b08f18d68db97d4ff5712f0c8`.
- Local structured analysis: `perf-183246-analysis.json`; supplemental admission/recovery statistics: `perf-183246-latency-context.json`.
- Existing remote evidence root: `/home/perfadmin/perf-tests/perf-results`; isolated derived root: `/home/perfadmin/compact-summary-validation-1bddc36b64bb/derived`.
- Report created from retained observations only; no new benchmark data or inferred baseline results.
