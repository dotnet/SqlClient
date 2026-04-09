# System.Transactions from the Outside

How the `System.Transactions` framework works from the perspective of a consumer (SqlClient).

## Topics to Research

- [x] Lightweight (local) vs distributed transactions — when does each apply?
- [x] `TransactionScope` vs `CommittableTransaction` — usage patterns, nesting behavior
- [x] Transaction promotion — what triggers it, what's the sequence
- [x] `TransactionCompleted` event — which thread fires it, ordering guarantees, reentrancy
- [x] Two-phase commit mechanics — Prepare/Commit/Rollback, `IEnlistmentNotification`
- [ ] `EnlistVolatile` vs `EnlistDurable` — what SqlClient uses, when
- [ ] Transaction isolation levels — how they interact with connection pooling
- [x] Transaction timeout — what happens to enlisted connections when a transaction times out
- [x] `TransactionScopeOption` — `Required`, `RequiresNew`, `Suppress` semantics
- [x] `TransactionScopeAsyncFlowOption` — what it changes about `Transaction.Current` propagation
- [x] Transaction cloning — `Clone()` vs `DependentClone()`, lifecycle, disposal safety

## Findings

### The Problem System.Transactions Solves

`System.Transactions` provides a single programming model for both local and distributed transactions. You write code the same way regardless of whether the transaction ends up being managed by one database or coordinated across multiple resources by MSDTC.

### Two Key APIs

- **`TransactionScope`** — sets `Transaction.Current` (ambient, thread-local) so resource managers like SqlClient auto-detect and auto-enlist. Most common pattern.
- **`CommittableTransaction`** — explicit control, manual enlistment via `conn.EnlistTransaction(tx)`.

### Lightweight vs Distributed

System.Transactions starts every transaction as **lightweight** (Local Promotable Single Phase Enlistment — LPSPE). Only one durable resource, no MSDTC, fast. Promotes to **distributed** when a second durable resource enlists (e.g., second `SqlConnection` to a different server in the same scope). Distributed transactions require MSDTC and are slower.

### TransactionScope Options

| Option | Behavior | Pool implication |
|--------|----------|-----------------|
| `Required` (default) | Joins ambient transaction or creates new | Nested scopes share same `Transaction`; pool can reuse same physical connection |
| `RequiresNew` | Creates independent transaction, suspends outer | Different `Transaction.Current`; pool must give separate physical connections |
| `Suppress` | `Transaction.Current` = null inside | Pool skips transacted pool lookup entirely |

### TransactionCompleted Event

- Fires once, after outcome is determined (commit or rollback).
- **Thread is non-deterministic**: local transactions may fire on user thread during `Dispose()`; distributed transactions may fire on a thread pool thread from MSDTC. Pool event handler code must be thread-safe.
- This is the signal for the pool to move connections out of the transacted pool.

### Transaction Timeout

Default ~60 seconds (configurable). If scope isn't completed before timeout, transaction is aborted. `TransactionCompleted` fires with `TransactionStatus.Aborted`. From the pool's perspective, timeout is just another rollback — same cleanup path.

### Two-Phase Commit (2PC)

Only applies to distributed transactions. Phase 1: coordinator asks resources to prepare. Phase 2: commit or rollback. SqlClient implements this via `IEnlistmentNotification`. The pool doesn't interact with 2PC directly — it only sees `TransactionCompleted` after everything is resolved.

### TransactionScopeAsyncFlowOption

By default, `Transaction.Current` is **thread-local** and does not flow across `await`. Post-`await`, a different thread may run and `Transaction.Current` is null.

`TransactionScopeAsyncFlowOption.Enabled` stores the transaction in `AsyncLocal<T>` instead, so it survives `await`. Directly relevant to the pool: `GetFromTransactedPool()` reads `Transaction.Current`, so if async flow isn't enabled post-`await`, the pool won't see the transaction.

### Transaction.Clone() and DependentClone()

#### Clone()

`Transaction.Clone()` creates a **ref-counted wrapper** around a shared `InternalTransaction` object. From the .NET runtime source:

```csharp
public Transaction Clone()
{
    // creates a new Transaction wrapper sharing the same _internalTransaction
    return new Transaction(_isoLevel, _internalTransaction);
}
```

Key characteristics:
- The cloned `Transaction` is an **independent object** with its own `_disposed` flag, but shares all transactional state (phase, status, enlistments) via the shared `InternalTransaction`.
- Each clone increments `_internalTransaction._cloneCount`. On `Dispose()`, it decrements the count. The **last clone to dispose** triggers cleanup of the `InternalTransaction`.
- Cloning does **not** create a new transaction or a dependent relationship — it's purely a lifetime/reference management tool.
- `Dispose()` is idempotent — calling it multiple times on the same clone is safe (guarded by `_disposed` flag).

**Why clone instead of holding the original?** The caller who created the original `Transaction` (or `TransactionScope`) may dispose it at any time. If a component holds a reference to the original and it gets disposed, subsequent operations on that reference will throw `ObjectDisposedException`. Cloning gives you an independent lifetime — your clone stays valid even if the original is disposed, as long as the underlying transaction hasn't completed.

#### DependentClone()

`Transaction.DependentClone(DependentCloneOption)` creates a `DependentTransaction` that has a **causal relationship** with the parent:

- `DependentCloneOption.BlockCommitUntilComplete` — The parent transaction **cannot commit** until `DependentTransaction.Complete()` is called. Used when the dependent work must finish before the transaction commits.
- `DependentCloneOption.RollbackIfNotComplete` — If the `DependentTransaction` is disposed without calling `Complete()`, the parent transaction is **rolled back**. Used for fire-and-forget work that should abort the transaction on failure.

#### Clone vs DependentClone comparison

| Aspect | Clone() | DependentClone() |
|--------|---------|-------------------|
| Return type | `Transaction` | `DependentTransaction` |
| Blocks parent commit? | No | Yes (BlockCommitUntilComplete) or rolls back (RollbackIfNotComplete) |
| Needs `Complete()` call? | No | Yes |
| Use case | Independent lifetime management | Coordinating dependent async/parallel work |
| Used by SqlClient? | Yes — pool stores clones as dictionary keys in `TransactedConnectionPool` | No |

#### Safety rules
- **Always dispose** clones — they hold ref-counted references to `InternalTransaction`.
- **Don't clone after completion** — the transaction is already resolved; cloning serves no purpose.
- **Thread-safe** — `Clone()` and `Dispose()` use interlocked operations on the clone count.
- **Idempotent dispose** — safe to call `Dispose()` multiple times on a clone.
