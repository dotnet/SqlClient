---
marp: true
theme: default
paginate: true
---

# Transactions × Connection Pooling
**Research Deep-Dive — April 2026**

<!-- This presentation walks through how System.Transactions interacts with SqlClient's connection pool. We'll build from fundamentals to the tricky edge cases, then cover the design decisions we need to make for the new Channel pool. -->

---

## Agenda

1. System.Transactions fundamentals
2. Delegated transactions & PSPE
3. Connection enlistment lifecycle
4. Pool-level binding rules
5. Return paths & stasis
6. Threading & failure modes

<!-- About 30 min total. Each section builds on the previous one. Stop me with questions at any point. -->

---

## Why System.Transactions?

- Single API for local **and** distributed transactions
- `TransactionScope` sets ambient `Transaction.Current`
- Resource managers (SqlClient) auto-detect and enlist

```csharp
using (var scope = new TransactionScope())
{
    conn.Open();   // auto-enlists
    // ...
    scope.Complete();
}
```

<!-- The user writes the same code regardless of whether we use MSDTC or not. SqlClient detects Transaction.Current and enlists automatically. -->

---

## Lightweight vs Distributed

<!-- Diagram source: diagrams/lightweight-vs-distributed.mmd -->
![Lightweight vs Distributed](diagrams/lightweight-vs-distributed.svg)

- Most transactions touch **one database** → lightweight
- Promotion only on 2nd durable resource
- PSPE = the optimization that avoids MSDTC

<!-- Key insight: PSPE is why most of the complexity exists. We avoid MSDTC by delegating the transaction to SqlClient itself, and that creates the "transaction root" problem we'll get to. -->

---

## TransactionScope Options

| Option | `Transaction.Current` | Pool effect |
|--------|----------------------|-------------|
| `Required` | Same or new | Reuse same physical connection |
| `RequiresNew` | New, independent | Different physical connection |
| `Suppress` | `null` | Skip transacted pool entirely |

<!-- Required is the default and is what most people use. RequiresNew creates a totally separate transaction. Suppress is like opting out — Transaction.Current is null so the pool doesn't even check the transacted pool. -->

---

## PSPE: The Delegation Contract

<!-- Diagram source: diagrams/pspe-delegation-sequence.mmd -->
![PSPE Delegation Sequence](diagrams/pspe-delegation-sequence.svg)

<!-- SqlClient says "I'll manage this transaction." System.Transactions says "OK, I'll call you back to commit or rollback." The connection that accepted delegation is now the "transaction root" — it MUST stay alive for those callbacks. -->

---

## The 4 PSPE Callbacks

| Callback | When | What SqlClient does |
|----------|------|-------------------|
| `Initialize()` | Delegation accepted | `BEGIN TRANSACTION` |
| `Promote()` | 2nd resource enlists | Return DTC token |
| `SinglePhaseCommit()` | Commit, never promoted | `COMMIT` |
| `Rollback()` | Abort | `ROLLBACK` |

⚠️ All need the **live connection** to SQL Server

<!-- This is the key constraint. If the connection dies, these callbacks can't do their job. The transaction is doomed. -->

---

## Delegated vs Propagated

<!-- Diagram source: diagrams/delegated-vs-propagated.mmd -->
![Delegated vs Propagated](diagrams/delegated-vs-propagated.svg)

- 1st connection → delegated (owns the transaction)
- Subsequent connections → propagated (DTC cookie)

<!-- First connection wins the delegation. Everyone else gets the propagated path, which requires MSDTC. -->

---

## Connection Enlistment: Two Entry Points

<!-- Diagram source: diagrams/enlistment-entry-points.mmd -->
![Enlistment Entry Points](diagrams/enlistment-entry-points.svg)

- Both check `Enlist` connection string keyword
- Both converge on same `Enlist()` method
- `EnlistedTransaction` stores a **clone** of the transaction

<!-- Fresh connections enlist in CompleteLogin. Reused pooled connections enlist in ActivateConnection. The clone is important — we'll cover that next. -->

---

## Why Clone the Transaction?

- User's `TransactionScope` may **dispose** at any time
- Original `Transaction` → `ObjectDisposedException`
- Clone has **independent lifetime**, shares state

**Two clone sites:**
1. `EnlistedTransaction` setter — survive scope disposal
2. `TransactedConnectionPool` — dictionary key validity

<!-- Clone() creates a ref-counted wrapper around the same InternalTransaction. Each clone has its own disposed flag. Last clone to dispose triggers cleanup. -->

---

## Pool Lookup Order

<!-- Diagram source: diagrams/pool-lookup-order.mmd -->
![Pool Lookup Order](diagrams/pool-lookup-order.svg)

- Same scope + same conn string = **same physical connection**
- Different conn string = different pool → triggers promotion

<!-- The transacted pool is always checked first when there's an ambient transaction. This is how we guarantee the same physical connection is reused. -->

---

## The Transacted Pool

- `Dictionary<Transaction, TransactedConnectionList>`
- `TransactedConnectionList` extends `List<DbConnectionInternal>`
- LIFO retrieval (last parked = first returned)
- **Lazy** health checks on retrieval only

| Connection Type | Dead → | 
|----------------|--------|
| Root | **Throw** (transaction doomed) |
| Non-root | Silently discard |

<!-- The pool doesn't proactively check health of parked connections. Only when someone asks for one. Dead roots are fatal because the delegated transaction can't survive without them. -->

---

## Return Paths: User Close

<!-- Diagram source: diagrams/return-path-user-close.mmd -->
![Return Path: User Close](diagrams/return-path-user-close.svg)

<!-- Two outcomes: if the transaction already finished, go straight to idle pool. If still active, park in the transacted pool and wait for the TransactionCompleted callback. -->

---

## Return Paths: Transaction Completes

<!-- Diagram source: diagrams/return-path-txn-complete.mmd -->
![Return Path: Transaction Completes](diagrams/return-path-txn-complete.svg)

<!-- This fires asynchronously on a System.Transactions thread. The connection moves from the transacted pool back to idle, or gets destroyed if the pool is shutting down. -->

---

## Stasis: Why It Exists

**Problem:** User closes a root connection, but transaction isn't done yet.

- Can't go to idle pool → others would grab it
- Can't destroy it → PSPE callbacks need it alive
- Can't block → pool shutdown is non-blocking

**Solution:** "Stasis" — park with no owner, wait for `TransactionCompleted`

<!-- Stasis is purely for delegated transaction roots that have nowhere to go. It's event-driven — the TransactionCompleted event triggers cleanup. -->

---


## Threading: Two Competing Threads

<!-- Diagram source: diagrams/threading-two-threads.mmd -->
![Threading: Two Competing Threads](diagrams/threading-two-threads.svg)

- Different lock objects: `lock(connection)` vs `lock(transaction)`
- Ordering-safe: either path produces correct result
- No nested locks → no deadlocks

<!-- The two threads never deadlock because they lock different objects. The design ensures that regardless of which thread runs first, the connection ends up in the right place. -->

---

## Lock Hierarchy

| Component | Lock Target |
|-----------|------------|
| Pool return path | `lock(connection)` |
| DetachTransaction | `lock(transaction)` |
| SqlDelegatedTransaction | `lock(connection)` |
| TransactedConnectionPool | `lock(dict)` → `lock(list)` |

**Rule:** Never nest connection lock inside transaction lock

<!-- Deadlock avoidance is by convention, not enforcement. SqlDelegatedTransaction always does pool cleanup OUTSIDE the connection lock. -->

---

## Failure Modes: The Highlights

| Failure | Handling |
|---------|---------|
| Transaction timeout | Normal rollback path |
| Dead parked connection | Lazy detection on retrieval |
| Pool shutdown + root | Stasis: park and wait for TxCompleted |
| Pool clear | Lazy: marks `CanBePooled = false` |
| Root connection dies | Transaction **doomed** — no recovery |

<!-- Most failures funnel through the standard TransactionCompleted path. The interesting ones are root death (unrecoverable) and pool shutdown (stasis-dependent). -->

---

## ⚠️ Known Race: CleanupCallback

```
lock (obj) { check IsTransactionRoot }
// ← TransactionCompleted can fire HERE
obj.SetInStasis();  // stasis counter leak
```

- Source code acknowledges this gap
- "Would require more substantial re-architecture"

<!-- This is a known minor leak in the existing pool. The race window is small and the consequence is a counter leak, not data corruption. -->

---

## Summary

- ✅ Research complete (8 topics, 4 layers)
- 🔜 Next: requirements → design → implementation

<!-- Research phase is done. We have a clear picture of how transactions interact with connection pooling. Next step is defining requirements and making design decisions. -->
