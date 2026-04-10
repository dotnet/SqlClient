# Transaction Cloning

How and why SqlClient clones `Transaction` objects for lifetime management.

This document builds on the `EnlistedTransaction` property covered in [03-connection-enlistment-lifecycle.md](03-connection-enlistment-lifecycle.md).

## What Clone() Does

`Transaction.Clone()` creates a **ref-counted wrapper** around a shared `InternalTransaction` object:

```csharp
public Transaction Clone()
{
    return new Transaction(_isoLevel, _internalTransaction);
}
```

The cloned `Transaction` is an independent object with its own `_disposed` flag, but shares all transactional state (phase, status, enlistments) via the shared `InternalTransaction`. Each clone increments `_internalTransaction._cloneCount`; on `Dispose()`, it decrements the count. The **last clone to dispose** triggers cleanup of the `InternalTransaction`.

**Key point:** Cloning does not create a new transaction or a dependent relationship. It is purely a lifetime management tool.

## Why Clone Instead of Holding the Original?

The caller who created the original `Transaction` (or `TransactionScope`) may dispose it at any time. If a component holds a reference to the original and it gets disposed, subsequent operations on that reference throw `ObjectDisposedException`. Cloning gives an independent lifetime — a clone stays valid even if the original is disposed, as long as the underlying `InternalTransaction` hasn't been cleaned up.

## Where SqlClient Clones

### 1. The EnlistedTransaction Setter

The connection needs to query transaction state (e.g., `TransactionInformation.Status`) even after the user's `TransactionScope` has been disposed. If it held the original, those queries would throw. The clone keeps the metadata accessible for the full enlistment lifetime.

### 2. TransactedConnectionPool Dictionary Keys

The transacted pool uses a `Dictionary<Transaction, TransactedConnectionList>`. The clone is used as the dictionary key. This ensures the key remains valid for lookup even after the user-facing transaction object is disposed.

## Safe Disposal Pattern

Both clone sites use the same ownership-transfer pattern:

```csharp
Transaction clone = null;
try
{
    clone = transaction.Clone();
    StoreClone(clone);   // transfer ownership to the store
    clone = null;         // prevent finally from disposing
}
finally
{
    clone?.Dispose();     // only runs if store failed
}
```

When the clone is no longer needed, the owner disposes it explicitly:
- The `EnlistedTransaction` setter disposes the old clone when a new one is swapped in
- `TransactedConnectionList.Dispose()` disposes the key clone when the transaction ends

## DependentClone (Not Used by SqlClient)

For completeness, `Transaction.DependentClone(DependentCloneOption)` creates a `DependentTransaction` with a causal relationship to the parent:

| Option | Behavior |
|--------|----------|
| `BlockCommitUntilComplete` | Parent cannot commit until `DependentTransaction.Complete()` is called |
| `RollbackIfNotComplete` | Parent is rolled back if the dependent is disposed without `Complete()` |

SqlClient does not use `DependentClone` — `Clone()` is sufficient for its lifetime management needs.

## Safety Rules

- **Always dispose clones** — they hold ref-counted references to `InternalTransaction`
- **Don't clone after completion** — the transaction is already resolved; cloning serves no purpose
- **Thread-safe** — `Clone()` and `Dispose()` use interlocked operations on the clone count
- **Idempotent dispose** — safe to call `Dispose()` multiple times on a clone
