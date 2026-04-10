# Transaction Support Requirements for ChannelDbConnectionPool

These requirements describe the **observable behavior** a user should experience when using `SqlConnection` with `System.Transactions` on pool v2 (`UseConnectionPoolV2`). They are framed as user-facing expectations, independent of how the pool implements them internally.

The reference for correct behavior is the existing `WaitHandleDbConnectionPool` — pool v2 must produce the same observable outcomes.

---

## 1. Connection Reuse Within a Transaction

**REQ-1.1:** When a user opens multiple `SqlConnection` instances with the **same connection string** inside the same `TransactionScope`, they must receive the **same physical connection** to SQL Server.

**REQ-1.2:** The physical connection must remain enlisted in the transaction for its entire lifetime — from first `Open()` until `TransactionCompleted` fires.

**REQ-1.3:** Calling `Close()` on a connection and re-opening another `SqlConnection` with the same connection string within the same scope must return the same physical connection, not a new one.

**REQ-1.4:** Connection reuse must work regardless of how many times the user opens and closes connections within the scope, as long as the connection string matches.

---

## 2. Transaction Isolation Between Callers

**REQ-2.1:** Two concurrent `TransactionScope` instances (e.g., `Required` and `RequiresNew`, or two independent `Required` scopes on different threads) must **never** share a physical connection.

**REQ-2.2:** A connection enlisted in transaction A must not be visible to callers operating under transaction B, even if the connection string matches.

**REQ-2.3:** A connection retrieved from the transacted pool for one transaction must not be returned to a different transaction's pool entry. The transaction-to-connection binding is strict.

---

## 3. Commit / Rollback Cleanup

**REQ-3.1:** After a transaction commits or rolls back, all connections that were enlisted in that transaction must be returned to the general idle pool (assuming they are still healthy and poolable).

**REQ-3.2:** Connections returned to the general pool after transaction completion must have their `EnlistedTransaction` cleared (set to `null`).

**REQ-3.3:** If a connection is not healthy (`CanBePooled == false` or connection is broken) at transaction completion, it must be destroyed rather than returned to the general pool.

**REQ-3.4:** Cleanup must happen regardless of whether the user explicitly closed the connection before the transaction completed or left it open.

---

## 4. Nested Transaction Scopes

**REQ-4.1: `TransactionScopeOption.Required` (default):** A nested scope with `Required` inherits the ambient transaction. Connections opened inside the nested scope must use the same physical connection as the outer scope (same connection string assumed).

**REQ-4.2: `TransactionScopeOption.RequiresNew`:** A nested scope with `RequiresNew` creates an independent transaction. Connections opened inside it must use a **different** physical connection than the outer scope, even with the same connection string.

**REQ-4.3: `TransactionScopeOption.Suppress`:** A nested scope with `Suppress` sets `Transaction.Current` to `null`. Connections opened inside it must come from the general idle pool, completely bypassing the transacted pool.

---

## 5. Connection Lifetime and Transactions

**REQ-5.1:** A connection that is enlisted in an active transaction must **not** be destroyed due to idle timeout, connection lifetime expiry (`LoadBalanceTimeout`), or pool cleanup timers. The transaction takes priority over lifetime policies.

**REQ-5.2:** After the transaction completes, normal lifetime policies resume. If the connection has exceeded its lifetime during the transaction, it should be destroyed at that point rather than returned to the idle pool.

**REQ-5.3:** The pool must not proactively health-check transacted connections while they are parked. Health checks happen lazily when a connection is retrieved from the transacted pool.

---

## 6. Pool Shutdown and Clear With Active Transactions

**REQ-6.1:** If the pool is shutting down while a delegated transaction root connection is still active (transaction not yet completed), the connection must **not** be destroyed. It must remain alive until the `TransactionCompleted` callback fires, because the PSPE callbacks (`SinglePhaseCommit`, `Rollback`) require it.

**REQ-6.2:** After the transaction completes on a shutting-down pool, the connection should be destroyed (not returned to the idle pool).

**REQ-6.3:** `SqlConnection.ClearPool()` must not directly destroy transacted connections. It should mark them as non-poolable (`CanBePooled = false`), and they will be destroyed lazily when retrieved or when the transaction completes.

**REQ-6.4:** Non-root transacted connections during pool shutdown follow the same destroy path as non-transacted connections. The existing pool destroys them immediately. This is safe because: (a) the 2PC protocol between MSDTC and SQL Server uses a separate communication channel (OleTx/DTC protocol), not the client's TDS connection — so destroying the client connection doesn't break commit/rollback, and (b) when a connection is destroyed, the TDS session closes, which causes SQL Server to clean up its server-side transaction state for that session. The unenlistment message (`PropagateTransactionCookie(null)`) that would normally be sent when the connection is reused becomes unnecessary since the session no longer exists.

---

## 7. Delegated Transaction Promotion

**REQ-7.1:** When a second durable resource enlists in the same transaction, `System.Transactions` calls the `Promote()` callback on the PSPE resource manager (`SqlDelegatedTransaction`). The pool must not interfere with this process.

**REQ-7.2:** After promotion, subsequent connections opened with a **different** connection string enlist via the propagated (DTC) path. The pool must correctly handle both delegated and propagated connections coexisting for the same transaction.

**REQ-7.3:** The delegated transaction root connection must remain alive through promotion — promotion does not change the root's lifecycle requirements. It still needs to be alive for the `SinglePhaseCommit`/`Rollback` fallback if using the promoted path.

---

## 8. Error Cases

**REQ-8.1: Transaction timeout.** A transaction timeout triggers normal rollback. The pool must handle this through the standard `TransactionCompleted` path — no special error handling required.

**REQ-8.2: Root connection dies mid-transaction.** If the physical connection that is the delegated transaction root breaks (network failure, server kill, etc.), the transaction is **doomed**. There is no recovery. The pool must not attempt to silently replace the root connection or suppress the error.

**REQ-8.3: Non-root connection dies while parked.** When a non-root connection is retrieved from the transacted pool and found dead, it must be silently discarded and a new connection created. This is not a fatal error.

**REQ-8.4: Root connection dies while parked.** When a root connection is retrieved from the transacted pool and found dead, this is a fatal error for the transaction. The pool must throw (or propagate the existing exception) — it must not silently substitute a new connection.

**REQ-8.5: Pool exhaustion during a transaction.** If `MaxPoolSize` is reached and a new connection is needed for a different transaction, the request must wait/block normally (same behavior as non-transacted pool exhaustion). The transacted pool does not count against `MaxPoolSize` — connections parked in the transacted pool are already counted from when they were originally created.

**REQ-8.6: Connection open fails inside a TransactionScope.** If connection creation fails (authentication error, server unreachable, etc.), the failure must propagate normally. The transaction state in the pool should not be corrupted by a failed connection attempt.

---

## 9. Async Scenarios

**REQ-9.1:** When `TransactionScopeAsyncFlowOption.Enabled` is set, the ambient transaction must flow correctly across `await` points. `SqlConnection.OpenAsync()` inside such a scope must enlist in the flowed transaction and reuse the same physical connection as synchronous opens would.

**REQ-9.2:** The `TransactionCompleted` callback fires on a `System.Transactions` thread, not the user's async context. The pool's cleanup path must be thread-safe and must not deadlock with the user's async operations.

**REQ-9.3:** Multiple concurrent `OpenAsync()` calls within the same `TransactionScope` (with async flow enabled) may each create a separate physical connection if they race past the transacted pool lookup before the first connection is parked. This triggers promotion to a distributed transaction. The pool must handle this correctly — multiple connections associated with the same transaction, each parked in the transacted pool's list for that transaction.
