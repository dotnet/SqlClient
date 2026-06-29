# Async Performance Research — dotnet/SqlClient

This directory contains research findings from open issues and discussions in the
[dotnet/SqlClient](https://github.com/dotnet/SqlClient) repository related to async performance.
Data gathered on 2026-03-27.

## Files

| File | Description |
| ------ | ------------- |
| [issue-summary.md](issue-summary.md) | Categorized summary of all relevant open issues |
| [root-causes.md](root-causes.md) | Analysis of root causes and architectural problems |
| [connection-pool-redesign.md](connection-pool-redesign.md) | The ChannelDbConnectionPool effort and its progress |
| [recommendations.md](recommendations.md) | Prioritized recommendations for improvement |

## Key Findings

1. **Async APIs are fundamentally slower than sync** — multiple independent reports confirm 10x–250x
   slowdowns for async vs sync code paths.

2. **Connection pool is the #1 bottleneck** — serialized connection creation, blocking WaitHandle
   locks, and fake-async patterns cause thread pool starvation under load.

3. **Large data reads are catastrophically slow in async** — the TDS snapshot/replay mechanism
   causes exponential time growth with data size.

4. **Active redesign underway** — `ChannelDbConnectionPool` (issue #3356) is being implemented to
   replace the legacy pool with an async-first design.

5. **Thread pool starvation is a cross-cutting concern** — it manifests in connection opening, token
   acquisition, MARS on Linux, and managed SNI operations.
