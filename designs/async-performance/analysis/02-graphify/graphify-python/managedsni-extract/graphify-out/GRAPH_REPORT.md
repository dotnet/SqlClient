# Graph Report - designs/async-performance/plans/02-graphify/managedsni-extract  (2026-06-29)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 355 nodes · 535 edges · 21 communities (17 shown, 4 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `dc86cadf`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]

## God Nodes (most connected - your core abstractions)
1. `SniPacket` - 58 edges
2. `SniMarsHandle` - 36 edges
3. `SniTcpHandle` - 33 edges
4. `SniNpHandle` - 26 edges
5. `SniMarsConnection` - 24 edges
6. `SniHandle` - 22 edges
7. `SslOverTdsStream` - 16 edges
8. `LocalDB` - 15 edges
9. `DataSource` - 14 edges
10. `SniProxy` - 11 edges

## Surprising Connections (you probably didn't know these)
- `SniSslStream` --references--> `ConcurrentQueueSemaphore`  [EXTRACTED]
  SniSslStream.netcore.cs → ConcurrentQueueSemaphore.netcore.cs
- `SniMarsHandle` --references--> `SniError`  [EXTRACTED]
  SniMarsHandle.netcore.cs → SniError.netcore.cs
- `SniMarsConnection` --references--> `SniHandle`  [EXTRACTED]
  SniMarsConnection.netcore.cs → SniHandle.netcore.cs
- `SniMarsHandle` --inherits--> `SniHandle`  [EXTRACTED]
  SniMarsHandle.netcore.cs → SniHandle.netcore.cs
- `SniPacket` --references--> `SniHandle`  [EXTRACTED]
  SniPacket.netcore.cs → SniHandle.netcore.cs

## Import Cycles
- None detected.

## Communities (21 total, 4 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.08
Nodes (22): bool, Exception, Guid, int, IPAddress, NetworkStream, object, SniAsyncCallback (+14 more)

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (12): Guid, int, object, SniAsyncCallback, uint, ushort, Microsoft.Data.SqlClient.ManagedSni, SniMarsHandle (+4 more)

### Community 2 - "Community 2"
Cohesion: 0.07
Nodes (22): EncryptionOptions, Exception, Guid, int, IPAddress, SslPolicyErrors, TimeoutTimer, X509Certificate (+14 more)

### Community 3 - "Community 3"
Cohesion: 0.08
Nodes (17): bool, Exception, Guid, int, object, SniAsyncCallback, SslPolicyErrors, SslStream (+9 more)

### Community 4 - "Community 4"
Cohesion: 0.15
Nodes (12): Microsoft.Data.SqlClient.ManagedSni, ResolvedServerSpn, char, int, SqlConnectionIPAddressPreference, SQLDNSInfo, string, TimeoutTimer (+4 more)

### Community 5 - "Community 5"
Cohesion: 0.11
Nodes (14): bool, CancellationToken, Guid, int, Memory, ReadOnlyMemory, Span, Task (+6 more)

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (7): Action, byte, SniAsyncCallback, Span, Stream, Task, SniPacket

### Community 7 - "Community 7"
Cohesion: 0.12
Nodes (14): ConcurrentQueue, ConcurrentQueueSemaphore, CancellationToken, Task, Microsoft.Data.SqlClient.ManagedSni, CancellationToken, Memory, ReadOnlyMemory (+6 more)

### Community 8 - "Community 8"
Cohesion: 0.14
Nodes (7): Dictionary, byte, Guid, int, object, ushort, SniMarsConnection

### Community 9 - "Community 9"
Cohesion: 0.17
Nodes (11): byte, char, Exception, int, IPAddress, SqlConnectionIPAddressPreference, TimeoutTimer, Microsoft.Data.SqlClient.ManagedSni (+3 more)

### Community 10 - "Community 10"
Cohesion: 0.22
Nodes (8): int, string, LocalDB, LocalDBErrorState, Microsoft.Data.SqlClient.ManagedSni, IntPtr, LocalDBStartInstance, SafeLibraryHandle

### Community 11 - "Community 11"
Cohesion: 0.12
Nodes (4): SniAsyncCallback, SniHandle, List, SslProtocols

### Community 12 - "Community 12"
Cohesion: 0.24
Nodes (8): CancellationToken, Memory, ReadOnlyMemory, Task, ValueTask, Microsoft.Data.SqlClient.ManagedSni, SniSslStream, SslStream

### Community 13 - "Community 13"
Cohesion: 0.20
Nodes (7): byte, int, Span, uint, ushort, Microsoft.Data.SqlClient.ManagedSni, SniSmuxHeader

### Community 14 - "Community 14"
Cohesion: 0.32
Nodes (4): CancellationToken, SslStream, Task, X509CertificateCollection

### Community 15 - "Community 15"
Cohesion: 0.32
Nodes (5): int, GetStackParts(), Microsoft.Data.SqlClient.ManagedSni, SniPhysicalHandle, ObjectPool

### Community 16 - "Community 16"
Cohesion: 0.40
Nodes (5): int, string, Direction, History, Microsoft.Data.SqlClient.ManagedSni

## Knowledge Gaps
- **23 isolated node(s):** `Microsoft.Data.SqlClient.ManagedSni`, `Microsoft.Data.SqlClient.ManagedSni`, `Microsoft.Data.SqlClient.ManagedSni`, `Microsoft.Data.SqlClient.ManagedSni`, `Microsoft.Data.SqlClient.ManagedSni` (+18 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SniPacket` connect `Community 6` to `Community 0`, `Community 1`, `Community 3`, `Community 8`, `Community 11`, `Community 15`, `Community 16`?**
  _High betweenness centrality (0.239) - this node is a cross-community bridge._
- **Why does `SniTcpHandle` connect `Community 0` to `Community 4`, `Community 5`, `Community 14`, `Community 15`?**
  _High betweenness centrality (0.181) - this node is a cross-community bridge._
- **Why does `SniHandle` connect `Community 11` to `Community 1`, `Community 4`, `Community 6`, `Community 8`, `Community 14`, `Community 15`, `Community 18`?**
  _High betweenness centrality (0.167) - this node is a cross-community bridge._
- **What connects `Microsoft.Data.SqlClient.ManagedSni`, `Microsoft.Data.SqlClient.ManagedSni`, `Microsoft.Data.SqlClient.ManagedSni` to the rest of the system?**
  _23 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.08170731707317073 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08558558558558559 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.06896551724137931 - nodes in this community are weakly interconnected._