# Priority Rationale: Why P4

## Ranking: Priority 4 of 7

Async SNI connection opening is ranked fourth because it removes the last major blocking point in
`OpenAsync()`, but its impact is partially masked by connection pooling and the implementation
requires changes across multiple layers.

## Justification

### 1. Completes the OpenAsync() Promise

After P1 (pool fix) eliminates the pool-level blocking, `OpenAsync()` still blocks on the SNI layer
during physical connection creation. The full blocking sequence in `OpenAsync()` today:

```text
OpenAsync()
  → Pool.GetAsync()         ← fixed by P1
  → TdsParser.Connect()
    → SNI ctor              ← BLOCKS: TCP connect (5-100ms)
    → SSL handshake         ← BLOCKS: TLS negotiation (10-200ms)
    → PreLogin exchange     ← BLOCKS: sync TDS handshake
    → TDS Login             ← BLOCKS: authentication round trips
```

P4 makes the entire chain async, so `OpenAsync()` truly never blocks a thread.

### 2. Measurable Impact in Cloud Environments

Physical connection creation is dominated by network latency:

- **Local SQL Server:** 5–20ms (low impact)
- **Same-region Azure:** 20–50ms (moderate impact)
- **Cross-region Azure:** 100–500ms (high impact)
- **VPN/Firewall traversal:** 200ms–2s (very high impact)

Each blocked thread during this time is unavailable for processing other async work. In Azure
Functions or containerized workloads with limited thread pools, even a few simultaneous
`OpenAsync()` calls can exhaust available threads.

### 3. Cold-Start Amplification

Issue #601 reports that opening 100 connections in parallel takes minutes because connections are
created serially. P1 enables parallel creation, but if each creation still blocks a thread, the
thread pool becomes the new bottleneck. P4 ensures parallel async opens actually scale with the
number of connections, not the number of available threads.

## Why Not Higher

### vs P1 (Connection Pool)

The pool issue affects every `OpenAsync()` call — even when connections are available in the pool.
P4 only matters when a new physical connection must be created, which is rare in steady-state pooled
workloads. P1 has a much larger blast radius.

### vs P2 (TDS Reads)

The TDS read issue causes per-operation degradation (250x) that recurs on every read for the
connection's lifetime. P4 is a one-time cost at connection creation.

### vs P3 (Async Transactions)

Transactions occur on nearly every request. New physical connections occur only during cold starts,
pool expansion, or reconnection after failures. P3 has higher call frequency.

### Requires Multi-Layer Changes

P4 requires coordinated changes across:

- Managed SNI (`SniTcpHandle` constructor → async factory method)
- SSL handshake (`SslStream.AuthenticateAsClientAsync` → already async, needs plumbing)
- TDS PreLogin (sync request/response → async variant)
- TDS Login (sync authentication → async variant)
- `SqlInternalConnectionTds` (orchestrates the pipeline)

This multi-layer scope increases both effort and risk.

## Why Not Lower

### vs P5 (Allocation Reduction)

P5 reduces constant-factor overhead. P4 eliminates thread blocking — a qualitatively different class
of problem. A blocked thread is worse than a few extra allocations.

### vs P6 (Packet Locking)

P6 primarily affects MARS users, a declining subset. P4 affects all users who create new connections
asynchronously.

### vs P7 (Async TVP)

P7 is niche (TVP streaming). P4 is foundational (every connection must be opened).

## Sequencing Consideration

P4's full value is realized only after P1: without the async pool, there's no async path through
which the async SNI open can be reached. The recommended sequence is:

1. Complete P1 (pool) — unblocks async open path
2. Implement P4 Fix 1 (async TCP connect) — highest standalone impact
3. Layer in Fixes 2–4 (SSL, prelogin, login) incrementally
4. Deliver Fix 5 (end-to-end pipeline) once all pieces are in place

Native SNI is explicitly out of scope — it requires changes to the separate
`Microsoft.Data.SqlClient.SNI` C++ library. The recommendation is to steer async workloads toward
managed SNI, and to consider making managed SNI the default on all platforms once it reaches
performance parity.
