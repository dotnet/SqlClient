# OpenAsync results breakdown — run 183246

**The measurement count is expected. The oversized summary is mostly avoidable duplication.**

## Why there are so many records

**6 scenarios × 2 opening APIs × 4 concurrency levels × 2 instrumentation modes = 96 configurations.**

Three independent repetitions, each with **3 measured invocations + 2 warmups**, produce **1,440 invocation records**: 864 measured and 576 warmup. This run is current-source only; there are no baseline comparisons.

| Archive contents | Rep 1 | Rep 2 | Rep 3 |
|---|---:|---:|---:|
| Measured invocation JSON | 288 | 288 | 288 |
| Warmup JSON | 192 | 192 | 192 |
| External-observation indexes | 48 | 48 | 48 |
| SQL snapshots | 225 | 229 | 228 |
| Stack captures | 225 | 230 | 228 |
| **Total files** | **978** | **987** | **984** |

Snapshot counts depend on observation-window duration and timing. The collectors independently check invocation boundaries, so SQL and stack counts need not match.

## Evidence volume

Decimal MB; compressed category sizes exclude ZIP headers.

| Category across all repetitions | Files | Uncompressed MB | Compressed MB |
|---|---:|---:|---:|
| Measured invocation JSON | 864 | 746.5 | 42.1 |
| Warmup JSON | 576 | 498.1 | 28.4 |
| Parsed external observations | 144 | 222.5 | 17.0 |
| Raw SQL snapshots | 682 | 64.1 | 8.8 |
| Raw stacks | 683 | 30.6 | 1.4 |

The three ZIP archives total **98,543,263 bytes (98.5 MB)**, including headers. The remaining publication tree contains **592 files / 475,287,701 bytes (475.3 MB)**, dominated by the uncompressed summary.

## Why the summary is 374.5 MB

The exact file size is **374,515,424 bytes**. Bounded streaming of the serialized document identified:

| Summary field | Serialized bytes | Share |
|---|---:|---:|
| `ExternalDiagnostics` | 221,399,529 | 59.1% |
| `Diagnostics` | 138,073,515 | 36.9% |
| `DiagnosticSummary` | 6,142,155 | 1.6% |
| `LoadSummary` | 2,225,158 | 0.6% |
| Everything else | 6,675,067 | 1.8% |

**The first two fields account for 96.0% of the summary.**

- `Diagnostics` retains raw resource timelines, unrelated-work probe samples and GC observations. The timeline alone occupies **112.0 MB**, and unrelated-work data occupies **25.6 MB**.
- `ExternalDiagnostics` embeds parsed server snapshots and wait-delta arrays already retained in the external-observation evidence. Additional JSON indentation increases their serialized size.
- `DiagnosticSummary` also retains per-probe and per-GC stage-label arrays, so it is not strictly aggregate-only.
- Per-open `Samples`, `QuerySamples` and the raw E6 offer list are **excluded**. Latency distributions retain counts and quantiles, not full arrays of latency values.

### Generator behavior

In `eng\\pipelines\\perf\\scripts\\scoreboard_report.py`:

- `summarize()` copies raw fields except `Samples`, `QuerySamples` and `Load`, inadvertently retaining the complete `Diagnostics` object.
- `load_case()` embeds external server snapshots and wait deltas alongside raw-file references.
- `write_summary()` writes every invocation row with `indent=2`.
- `package_raw_measurements()` archives per-repetition invocation JSON and external diagnostics, but intentionally leaves top-level reports readable. It therefore overlooked this raw-heavy summary.

## Does 375 MB explain the out-of-memory failure?

**Not by itself.** A file this size is inappropriate for model context or full tool output, but can be processed with bounded streaming. Whole-document JSON parsing creates many objects, and formatting or copying the result can multiply memory usage.

For run 183246, available telemetry shows **96.88% agent-host memory used while publication remains in progress**. It does not provide a process-specific OOM termination record, memory limit, RSS or peak usage. It therefore does **not** establish which process ran out of memory or prove that the summary alone caused it. No available evidence identifies the Copilot/tool-host process referenced in the user's report.

This investigation used a **bounded line reader (maximum 64 KiB)** and ZIP directory metadata, not whole-document parsing or model ingestion.

## Minimal reporting change proposed

Keep the detailed evidence once, in the archives. Make the summary aggregate-only:

- Identity, provenance and archive-member references.
- Outcome counts, throughput and latency quantiles.
- E6 phase and recovery aggregates.
- Diagnostic peaks, counter deltas, probe distributions and stage counts.
- Coverage/quality indicators and selected server aggregates.

Keep full timelines, probe/GC records, SQL snapshots and wait arrays, stacks, and open/query/offer samples in the raw archives only.

Removing just the two embedded diagnostic fields would eliminate **359,473,044 bytes**, leaving approximately **15.0 MB** before replacing the remaining stage-label arrays with counts.

**No source artifacts were deleted or regenerated, and no pipeline/reporting implementation was changed.** Detailed count and byte evidence is retained in the session artifact `perf-183246-volume-breakdown.json`.
