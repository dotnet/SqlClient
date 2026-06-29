# 02 — How to measure

The harnesses. The repo already has most of what we need; this document says which tool owns which
metric, what is missing, and how to run each one repeatably. The principle is **simple and
iterative** — a developer working an item should be able to capture a before/after delta in minutes,
not stand up a lab.

---

## Tool ownership

| Tool | Owns | State today |
| --- | --- | --- |
| BenchmarkDotNet `PerformanceTests` | Latency, throughput, allocations, threading per op | Exists; needs large-payload + concurrency runners |
| `ThreadStarvation` app | Starvation onset, thread-pool growth under real concurrency | Exists; used as a pass/fail repro |
| EventCounters / EventSource | Field-observable counters (open latency, allocations) | Partial; extend for the items below |
| `dotnet-counters` / `dotnet-trace` / `dotnet-gcdump` | Ad-hoc heap, GC, and CPU investigation | CLI tools, no setup needed |
| PerfView / ETW (Windows) | Deep CPU + allocation call trees | Windows-only deep dives |

---

## BenchmarkDotNet harness (the primary rig)

The existing project at `src/Microsoft.Data.SqlClient/tests/PerformanceTests` is the backbone. Its
`BenchmarkConfig` already wires the two diagnosers we depend on and a GitHub markdown exporter:

```csharp
DefaultConfig.Instance
    .AddDiagnoser(MemoryDiagnoser.Default)       // allocated bytes/op, Gen0/1/2
    .AddDiagnoser(ThreadingDiagnoser.Default)    // completed work items, contention
    .AddExporter(MarkdownExporter.GitHub)        // checked-in result artifacts
```

Existing runners (`SqlConnectionRunner`, `SqlCommandRunner`, `DataTypeReaderRunner`,
`DataTypeReaderAsyncRunner`, `SqlBulkCopyRunner`) cover the narrow cases. `SqlConnectionRunner`
already parameterizes `MARS` and `Pooling` and benchmarks both `Open()` and `OpenAsync()` — exactly
the sync/async pairing we need for the guard metrics.

### What is missing and must be added before work starts

1. **A large-payload reader runner** — a table with multi-MB `varbinary(max)` / `nvarchar(max)`
   values so a single read spans hundreds of packets. This is the only runner that exercises CMD-1,
   CMD-4, and the zero-copy path. Without it those items are unmeasurable.
2. **A concurrency runner** (or lean on the ThreadStarvation app) — N concurrent `OpenAsync` /
   `ExecuteReaderAsync` with a constrained thread pool, for the CE-* throughput-under-load metrics.
3. **A regime axis** — `[Params]` (or separate config) toggling `UseCompatibilityProcessSni`,
   `MakeReadAsyncBlocking`, and `UseConnectionPoolV2`, so each item is measured in the regime it
   targets (7.1 default Compat-ON vs deferred Compat-OFF).
4. **A managed-vs-native SNI axis** — `UseManagedNetworkingOnWindows` is already honored by
   `Program.cs`; surface it as a configured dimension so Windows managed-SNI items are covered.

### Running it

```bash
cd src/Microsoft.Data.SqlClient/tests/PerformanceTests
# edit runnerconfig.json to enable only the runner under test and point ConnectionString
dotnet run -c Release -f net9.0
```

`runnerconfig.json` controls `LaunchCount` / `IterationCount` / `InvocationCount` / `WarmupCount` /
`RowCount` per runner. For iterative work, enable a **single** runner to keep cycle time short, then
run the full set for the merge baseline.

---

## ThreadStarvation app (the concurrency rig)

The [ThreadStarvation app](../../apps/ThreadStarvation/ANALYSIS.md) is the right tool for the
connection-establishment items because it reproduces the actual failure mode — thread-pool
exhaustion under many parallel queries — that CE-1/CE-4 are meant to fix. It supports:

- `--mode sync|async|both` — the both-mode is the direct sync-vs-async comparison that defines
  issue #1562, and is exactly the guard we need for non-async-isolated items.
- `--mars`, `--pooling`, `--connect-timeout` — regime toggles.
- `--min-threads` / `--max-threads` / `--io-threads` — constrain the pool to force starvation early.
- A background monitor printing thread-pool stats every `--monitor-interval` ms.
- `--log-events` and `--trace` for EventSource and `dotnet-trace` attachment.

This app produces the **time-to-starvation** and **thread-pool growth** metrics that a microbench
cannot, because BenchmarkDotNet isolates one operation while starvation is an emergent,
many-operations property.

---

## EventCounters and EventSource

For field-observable and CI-trendable metrics, lean on the `Microsoft.Data.SqlClient.EventSource`
provider. 04 explicitly recommends adding counters for *"open latency, thread-pool starvation,
allocation"* so regressions are observable in production, not silent. Before the work starts:

1. Confirm or add counters for connection open latency and active connection count.
2. Capture them with `dotnet-counters`:

   ```bash
   dotnet-counters monitor --process-id <pid> Microsoft.Data.SqlClient.EventSource System.Runtime
   ```

3. Use `System.Runtime` counters (`alloc-rate`, `gc-heap-size`, `threadpool-thread-count`,
   `threadpool-queue-length`) as a free, always-available cross-check against the BenchmarkDotNet
   numbers.

---

## Ad-hoc deep dives

When a number moves unexpectedly and the microbench cannot explain it:

- **`dotnet-trace`** — collect a CPU + allocation trace around a single run; open in PerfView or
  Speedscope to find the new hot path or allocation site.
- **`dotnet-gcdump`** — snapshot the managed heap mid-large-read to confirm a pooling change
  actually lowered peak heap (CMD-1).
- **PerfView / ETW** (Windows) — for native-SNI interop and the sync-path call trees where
  cross-platform tools are thinner.

These are investigation tools, not gate tools — the gate is BenchmarkDotNet + ThreadStarvation with
recorded baselines.
