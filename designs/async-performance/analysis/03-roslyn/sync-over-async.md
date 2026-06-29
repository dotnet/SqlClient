# Sync-Over-Async Inventory

The `sync-over-async` analyzer flags `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, and
`.RunSynchronously()` — the patterns that block a thread pool thread on an in-flight `Task`. 35
deduplicated occurrences were found across the async hot path. The full list is in
[results/analyzer-output.md](results/analyzer-output.md); the notable clusters are below.

## Clusters that matter for the quick-wins

### Connection establishment (managed SNI)

| Location | Pattern | Member | Relevance |
| --- | --- | --- | --- |
| `SslOverTdsStream.netcore.cs:191,212,240,267` | `.Result` | `ReadAsync` (async) | CE-2 — TLS read path blocks even inside an `async` method |
| `SsrpClient.netcore.cs:313–424` | `.Result` / `.Wait()` (9x) | `SendUDPRequest`, `SendBroadcastUDPRequest` | SSRP UDP lookup blocks the open path (see 05-fundamental SSRP note) |
| `SniHandle.netcore.cs:39` | `.GetAwaiter().GetResult()` | `AuthenticateAsClient` | CE-2 — sync TLS wrapper over the async handshake |
| `SniMarsHandle.netcore.cs:175,522` | `.Wait()` | `Send`, `Receive` | MARS send/receive block; ties to CE-5 / MARS multiplexing |

The four `SslOverTdsStream.ReadAsync` hits are the most striking: the **containing method is itself
`async`** (flagged `Y`), yet it blocks on `.Result` internally — a textbook async-over-sync-over-
async sandwich that CE-2 should unwind.

### Command execution (`SqlCommand.*Async` / reader / TDS write)

| Location | Pattern | Member |
| --- | --- | --- |
| `SqlCommand.Scalar.cs:110,136,260,292` | `.Result` | `ExecuteScalarBatchAsync`, `ExecuteScalarAsyncInternal` |
| `SqlCommand.Reader.cs:237,608` | `.Result` | `ExecuteDbDataReaderAsync`, `CleanupExecuteReaderAsync` |
| `SqlCommand.NonQuery.cs:337` | `.Result` | `CleanupAfterExecuteNonQueryAsync` |
| `SqlCommand.Xml.cs:371` | `.Result` | `CleanupAfterExecuteXmlReaderAsync` |
| `SqlCommand.cs:2460,2542` | `.Result` | `CreateLocalCompletionTask` |
| `SqlDataReader.cs:5630` | `.Result` | `CompleteAsyncCall` |
| `TdsParser.cs:1708,9767,10009,10134` | `.Wait()` | `ThrowExceptionAndWarning`, `TdsExecuteTransactionManagerRequest`, `TdsExecuteSQLBatch`, `TdsExecuteRPC` |
| `TdsParserStateObject.cs:3082,3358` | `.Wait()` | `SNIWritePacket`, `SendAttention` |

The `SqlCommand.*Async` `.Result` reads sit in the **completion/cleanup tails** of the async
execute methods — they unblock the continuation but run on the captured context, so they are
candidates for the broader async-cleanup hardening rather than any single 7.1 quick-win.

The `TdsParser.TdsExecute*` `.Wait()` calls are on the **write** side (`SNIWritePacket` →
`.Wait()`), confirming that write-path blocking is a separate, larger workstream from the read-path
quick-wins and is correctly out of the 7.1 shortlist.

## Reading the config column

Every managed-SNI row above is active under the four `.NET` configurations only
(`net8.0-unix net8.0-windows net9.0-unix net9.0-windows`) because the files carry the `.netcore.cs`
suffix; the `SqlCommand.*` / `TdsParser.cs` rows are `all` (shared across `net462` too). This is the
conditional-compilation precision the graphify pass could not provide.
