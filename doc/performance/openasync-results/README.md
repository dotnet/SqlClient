# OpenAsync performance results

Captured for team discussion on October 19, 2026. This is a results snapshot,
not a release-performance guarantee or a complete raw-data archive.

## Start here

- [Complete graph atlas and analysis](openasync-v2-all-graphs.md): 21 figures,
  116 panels, all six workloads. The concerning observations remain highlighted
  in bold, with their original caveats and document organization.
- [Focused graphs](openasync-v2-graphs.md): five figures explaining the most
  important responsiveness, throughput, allocation, and queueing tradeoffs.
- [Full narrative scorecard](openasync-v2-results-183246.md): numerical results,
  coverage, interpretation, and limitations.
- [Original preliminary scorecard](perf-results-182320/openasync-summary.md).
- [Existing-workload controls](perf-results-182338/summary.md).
- [Results-volume investigation](openasync-results-volume.md): a **historical
  snapshot before the compact-summary fix**, not the current publication state.

## Main result and measurement context

Run **183246** measured source
`abff493d9d80d8e9a05e14c42054b6592bda815f` on Ubuntu 22.04.5, .NET 10.0.13,
16 processors, with connection pool V2 and optimized async behavior enabled.
Its matrix was six scenarios, two opening APIs, four worker levels, and two
instrumentation modes: 96 configurations, three independent processes each,
three measured and two warmup invocations per process.

Warm contention showed much lower ordinary ThreadPool occupancy with OpenAsync.
Nonpooled async protected unrelated work but incurred higher allocation/CPU costs
and increasing completion latency beyond its physical-creation concurrency limit.
Cold pooled async did **not** show the same unrelated-work isolation. E6 recovered
by the end of its recovery window, but early recovery still had backlog and
elevated end-to-end latency.

These are observed tradeoffs, not proof of a regression against a released driver.
The historical 7.0.2 comparison remains blocked; no dependency exception or
baseline rerun was performed. Sampled and Diagnostic results are separate.
Nine invocation aggregates come from only **three independent processes**;
min/max whiskers are not confidence intervals. E4 has no contained external stack
captures, so its exact runtime attribution is not established.

## Retained runs and supporting evidence

| Evidence | Included | Interpretation |
|---|---|---|
| 183246, current-source V2 | All locally recovered analysis, 26 SVGs across the two graph reports, plotted values, retrieval and validation evidence | Complete measured matrix in the recovered aggregates; original raw archives and the complete compact summary are not included |
| 182320, preliminary E1-E4 | Markdown, BDN JSON, per-invocation JSON, summary and comparison files | Earlier preliminary design/runtime; not a matched comparator for V2 |
| 182338, M1/M2 controls | Markdown, BDN JSON and comparison files | Existing workloads, earlier source/runtime; report-only |
| 182350, interrupted expanded run | All 85 case/variant BDN Markdown/JSON reports; 425 validated compact invocation aggregates, including warmups, in `perf-results-182350/invocation-aggregates.jsonl` | 43 distinct cases, 43 baseline and 42 current case executions; partial run, not a complete matrix or another independent V2 repetition |
| Local SQL and merged-main smoke checks | Original Markdown, BDN JSON/CSV/HTML, and retained invocation JSON | Functional integration evidence, not comparable lab performance |
| Schema-v2 smoke fixtures | Retained invocation JSON | Synthetic serialization/accounting evidence, not benchmark results |
| Packaging validation | Source inventory and checksums; duplicate reports are already present under 182350 | Packaging evidence, not another measurement run |
| 182334, 182342, 182345, 183239 | Inventory of locally retained operational artifacts | Failed setup/restore runs; no successful measurement result is claimed |

The Markdown and graphs copied from the session are byte-for-byte snapshots,
including historical wording. Their stated state is as of their respective
investigation, not necessarily the state at PR creation. The 182350 compact
JSONL is newly derived using the reporter in this PR, one case at a time;
it retains baseline/current and actual/warmup identity, aggregates, coverage, and raw-evidence references.
It does not pretend those raw members are present in this repository.

## Evidence integrity and availability

[source-artifacts.json](source-artifacts.json) records original relative paths,
byte counts, SHA-256 checksums, and whether each inventoried artifact was copied
or excluded. Excluded paths are **not** links to files committed here.
[SHA256SUMS](SHA256SUMS) covers every committed file in this directory except
itself. Local source paths, operational logs, runner configurations and duplicate
packaging trees are not needed to read the reports and are not published as
measurement results.

The main run's recovered analysis is
[perf-183246-analysis.json](perf-183246-analysis.json), SHA-256
`73ea135abd463529f21296fd4d856e694c56906b08f18d68db97d4ff5712f0c8`.
Additional latency aggregates are retained in
[perf-183246-latency-context.json](perf-183246-latency-context.json).
Some small evidence files retain their original retrieval-envelope format.

The main run's measurements finished October 15, but artifact publication failed;
the overall pipeline was canceled October 18 and teardown was abandoned.
The lab VM was still accessible when inspected October 19. **Its survival is
not a durable-storage guarantee.** This PR preserves the recovered reports and
analysis, not that VM or its full evidence.

The following original run-183246 files were verified remotely but have **not**
been downloaded into this repository:

| Archive | Bytes | Members | SHA-256 |
|---|---:|---:|---|
| `raw-measurements-rep1.zip` | 32,716,389 | 978 | `fcb5483f75deaf3db428433c8efcaf7033109358c32cd059020842c398915875` |
| `raw-measurements-rep2.zip` | 32,917,279 | 987 | `7440b327b645460bbc8f15b739aee8f28bfc131303ce4767c8a6a31912719e8a` |
| `raw-measurements-rep3.zip` | 32,909,595 | 984 | `0c56f1190e106f9781222923751f3fce4a775ea9af0515d4f6edc3a8655bbe0e` |

The original summary was 374,515,424 bytes. The separately derived compact summary
was 11,842,902 bytes (96.84% smaller), with SHA-256
`b55522d079c08d8664efad398c7b0471e6e2657315e045ab5e86619853746439`.
Neither full summary is included. The
[retained validation result](perf-183246-compact-validation.json) reports
aggregate equivalence across 1,440 invocations, 1,584 evidence references,
unchanged original archives, and unchanged Markdown result rows. Generation
peaked at 77,700 KiB RSS; validation peaked at 51,976 KiB, both under a 512 MiB
address-space limit.

Large raw diagnostic files from the interrupted 182350 run are also excluded.
Their fingerprints and the locally retained packaging archive's fingerprint are
in the source inventory. Their compact aggregates are included, so the partial
observations are preserved without adding hundreds of megabytes of raw timelines
to Git. Full raw-data archival remains separate follow-up work.

## Verify this snapshot

From the repository root:

```powershell
python doc\performance\openasync-results\verify_results.py
python -B -m unittest discover -s eng\pipelines\perf\scripts\tests
```

The first command checks file hashes, copied-source identity, local Markdown
links, SVG structure, JSON parsing, the 96-case recovered analysis, and partial-run
accounting. It does not revalidate unavailable raw archives or execute a benchmark.
The second covers the perf reporting/packaging regression tests.

## Design and implementation

- [Implementation plan](../../design-notes/openasync-implementation-plan.md)
- [Test inventory](../../design-notes/openasync-test-inventory.md)
- [Test cases](../../design-notes/openasync-test-cases.md)
- [Latency catalog](../../design-notes/openasync-latency-catalog.md)
- [Latency priorities](../../design-notes/openasync-latency-priorities.md)
- [Build guide](../../../BUILDGUIDE.md)
- [Perf pipeline/reporting guide](../../../eng/pipelines/perf/README.md)

The branch also contains the E1-E6 workloads, diagnostics, reporting, regression
tests, and pipeline wiring. It does not change the driver's production behavior.
