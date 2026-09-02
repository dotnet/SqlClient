# Root cause analysis: #4001 — pooled connection broken after `TransactionScope` rollback

| | |
|---|---|
| **Issue** | [#4001](https://github.com/dotnet/SqlClient/issues/4001) |
| **Regressed by** | [#3019](https://github.com/dotnet/SqlClient/pull/3019) (`0322d44c7`), shipped in 6.1.0 |
| **Affected** | 6.1.0 → 6.1.6, `main` |
| **Last good** | 6.0.5 |
| **Code** | `SqlConnectionInternal.ResetConnection()` |

This note records why the bug happens, why the previous fix caused it, why the new
condition cannot reintroduce the issue that fix was addressing, and — importantly —
what the existing test suite does and does not actually verify.

---

## 1. Background: two ways a connection can be "in" a transaction

This distinction is the crux of the entire bug.

When a connection participates in a `TransactionScope`, it ends up in one of **two
distinct** states. They overlap, but neither implies the other.

### Delegated root — "I *own* this transaction"

Only one connection is involved, so `System.Transactions` delegates the transaction
down to SQL Server rather than paying for a distributed coordinator. The transaction
lives **on** the connection.

- `IsTransactionRoot` → `true`
- `EnlistedTransaction` → set at first, then **cleared** later (see below)

`IsTransactionRoot` is not a stored flag. It is derived:

```csharp
internal bool IsTransactionRoot => DelegatedTransaction?.IsActive == true;
```

Enlistment sets `EnlistedTransaction` unconditionally, so a *freshly* delegated root
has both. But once the transaction is no longer `Active`,
`DbConnectionInternal.DetachCurrentTransactionIfEnded` clears `EnlistedTransaction`:

```csharp
transactionIsDead = enlistedTransaction.TransactionInformation.Status != TransactionStatus.Active;
if (transactionIsDead) { DetachTransaction(enlistedTransaction, true); }
```

The delegated transaction, meanwhile, can still report `IsActive == true`. That
transient **half-state** — root, but no `EnlistedTransaction` — is precisely the state
issue #4001 reproduces in.

### Enlisted participant — "I *joined* someone else's transaction"

Multiple resources are involved, so a coordinator (MSDTC) owns the transaction and
each connection enlists in it.

- `IsTransactionRoot` → `false`
- `EnlistedTransaction` → set

### The trap

Neither field subsumes the other: a delegated root can have a `null`
`EnlistedTransaction`, and an enlisted participant is never a root. Any check that
tests only one of these fields silently misses the other case — and does so with no
exception at the point of the mistake.

---

## 2. Where the damage occurs

When a connection is closed it returns to the pool and is **reset** — wiped clean for
the next consumer. If a transaction is still in flight, that reset must *preserve* it.
The entire decision is one boolean:

```csharp
_parser.PrepareResetConnection(preserveTransaction);
```

Pass `false` while a transaction is genuinely live, and the TDS reset destroys the
server-side transaction **while `System.Transactions` still believes it exists**.

The failure then surfaces later, some distance from the cause:

1. `TransactionScope` disposes and rolls back.
2. `SqlDelegatedTransaction.Rollback` asks the server to roll back a transaction the
   server no longer has.
3. The rollback fails, and SqlClient calls `DoomThisConnection()`.
4. The physical connection is now permanently marked broken.
5. With a small pool (the report used `MaxPoolSize=1`) that same doomed connection is
   immediately handed back out.
6. The next caller gets:

> `InvalidOperationException: The requested operation cannot be completed because the connection has been broken.`

The exception names the connection, not the reset that ruined it. That distance
between cause and symptom is what makes this class of bug hard to trace.

---

## 3. What PR #3019 actually changed

The relevant diff from `0322d44c7`:

```diff
- _parser.PrepareResetConnection(IsTransactionRoot && !IsNonPoolableTransactionRoot);
+ _parser.PrepareResetConnection(EnlistedTransaction is not null && Pool is not null);
```

The old helper was:

```csharp
internal protected override bool IsNonPoolableTransactionRoot
    => IsTransactionRoot && (!Is2008OrNewer || Pool == null);
```

Substituting, the pre-#3019 condition was:

```csharp
IsTransactionRoot && Is2008OrNewer && Pool != null
```

The `Is2008OrNewer` term is not vestigial. `AdapterUtil.ValidateTdsVersion` still accepts
`TdsEnums.SQL2005_VERSION`, and `ConnectionCapabilities.Is2008R2OrNewer` returns `false`
for it. A pre-2008 server cannot carry a delegated transaction across a reset, so such a
connection must not be recycled with one attached.

So the two conditions were:

| | Question it asked | Covered | Missed |
|---|---|---|---|
| **Pre-#3019** | "Am I the *owner*?" | delegated root | enlisted → **#2970** |
| **#3019** | "Am I *enlisted*?" | enlisted | delegated root → **#4001** |

**#3019 swapped one case for the other rather than covering both.** It genuinely fixed
#2970, and it traded it for #4001. Both conditions were half-right; neither was wrong
about the case it did cover.

This reframes the fix: the goal is not to undo #3019, it is to finish it.

---

## 4. The fix

The predicate is extracted into a helper so it can be tested directly:

```csharp
internal static bool ShouldPreserveTransactionOnReset(
    bool isPooled,
    bool isTransactionRoot,
    bool is2008OrNewer,
    bool hasEnlistedTransaction)
{
    if (!isPooled)
    {
        return false;
    }

    return (isTransactionRoot && is2008OrNewer) || hasEnlistedTransaction;
}
```

This is exactly `OLD || NEW`, with both prior conditions preserved verbatim — including
the `Is2008OrNewer` guard that only ever applied to the delegated-root arm.

---

## 5. Why this cannot reintroduce #2970

This is the question that matters most, and it is answerable by inspection rather than
by testing. Every reachable state, for a pooled connection:

| `IsTransactionRoot` | `Is2008OrNewer` | `EnlistedTransaction` | Pre-#3019 | #3019 | **This fix** |
|:---:|:---:|:---:|:---:|:---:|:---:|
| `false` | `true` | `null` | `false` | `false` | `false` |
| **`true`** | **`true`** | **`null`** | ✅ `true` | ❌ `false` ← **#4001** | ✅ **`true`** |
| `true` | `false` | `null` | `false` | `false` | `false` |
| **`false`** | any | **set** | ❌ `false` ← **#2970** | ✅ `true` | ✅ **`true`** |
| `true` | any | set | see above | `true` | `true` |

Read the **#2970 row**. That is the row PR #3019 was created to fix, and this fix still
evaluates `true` there. It is untouched.

Reintroducing #2970 would require that cell to flip to `false`, and `A || B` cannot
evaluate `false` while `B` is `true`. The guarantee is structural, not empirical.

Because each arm reproduces its original predecessor exactly, the condition returns
`true` wherever either predecessor did, and never returns `false` where one of them
returned `true`.

---

## 6. How the root cause was established

The cause was **proven at runtime, not inferred**. The driver was temporarily
instrumented at the reset site and at `DoomThisConnection()`. The captured state at the
critical reset:

```
[RESET] obj=4  preserve=False  root=False  deleg=null         enlisted=null  pool=set
[RESET] obj=7  preserve=False  root=False  deleg=null         enlisted=null  pool=set
[RESET] obj=7  preserve=False  root=True   deleg=active=True  enlisted=null  pool=set   <-- old: true, new: false
[DOOM]  obj=7
   at Microsoft.Data.SqlClient.SqlDelegatedTransaction.Rollback(...)
   at System.Transactions.Transaction.Rollback()
   at System.Transactions.TransactionScope.InternalDispose()
   at System.Transactions.TransactionScope.Dispose()
[FAIL] Bug reproduced
```

The third reset is the bug caught in the act: `root=True`, `deleg.IsActive=True`,
`enlisted=null`, and `preserve=False`. A live delegated transaction being discarded.

This mattered, because **the initial hypothesis was wrong.** The first theory was that
#3019 had made the condition *too broad*, and a narrowing fix was written on that
basis. It did not work. The instrumentation showed the opposite — #3019 had *narrowed*
the condition, not widened it — and the fix was rewritten accordingly. Without runtime
evidence this would have been fixed in the wrong direction.

All instrumentation was removed before commit.

---

## 7. What the existing tests actually verify

This section is deliberately blunt, because the intuitive answer is wrong.

### Bisection

Against the reporter's reproduction (NHibernate 5.5.2, `MaxPoolSize=1`,
`TransactionScope` with a failed DTC promotion):

**6.0.5 ✅ · 6.1.0 ❌ · 6.1.1 ❌ · 6.1.4 ❌ · `main` ❌**

This places the regression in the 6.1.0 window, consistent with #3019.

### Both pool implementations, both directions

| | without fix | with fix |
|---|---|---|
| `WaitHandleDbConnectionPool` (default) | ❌ reproduces | ✅ passes |
| `ChannelDbConnectionPool` (`UseConnectionPoolV2`) | ❌ reproduces | ✅ passes |

The **left-hand column is the load-bearing one.** It was produced by stashing the fix
and rebuilding. Without it, a pass on the V2 pool could simply mean V2 never reaches
this code path, which would prove nothing.

(V2 in released 6.1.4 throws `NotImplementedException`, so only `main` was testable.)

### Mutation testing of the manual suite

`--filter "FullyQualifiedName~TransactionTest"` reports **9/9 passing** with the fix.
That number is easy to over-read, so the suite was mutation-tested: the condition was
replaced with each known-buggy variant and the suite re-run.

| Condition compiled in | Bug it contains | Suite result |
|---|---|---|
| Pre-#3019 (`IsTransactionRoot && Pool is not null`) | **#2970** | **9/9 passed** |
| #3019 (`EnlistedTransaction is not null && Pool is not null`) | **#4001** | **9/9 passed** |
| This fix (union) | none | 9/9 passed |

**The suite passes on all three.** It does not detect either bug, and therefore does
not guard this line at all in this environment.

`Test_EnlistedTransactionPreservedWhilePooled` — the test added by #3019 specifically
to cover #2970 — is tagged `[Trait("Category", "flaky")]` and passes against code that
carries the #2970 bug.

The practical conclusion: **the 9/9 result is evidence of no collateral damage, not
evidence that the fix works.**

### The regression test that was added

An end-to-end reproduction was attempted extensively and abandoned. The `#4001` state
requires a narrow simultaneity — the transaction's status already non-`Active` (so
`DetachCurrentTransactionIfEnded` has cleared `EnlistedTransaction`) while
`DelegatedTransaction.IsActive` is still `true` — and which of two teardown paths in
`SqlDelegatedTransaction` wins is a race:

- `TransactionEnded` sets `_active = false` and *immediately* calls
  `DoomThisConnection()`. If this path runs first, the delegate is already inactive
  before any reset, so the state is never observed.
- `Rollback`, driven from `TransactionScope.Dispose`, sets `_active = false` *after* the
  reset. This is the ordering the reporter hit.

Roughly twenty harness variants — varying pool size, pool implementation, promotion
success, explicit rollback, and parking the delegate in the transacted pool ahead of the
enlistment — consistently drove the first path. A test built on that race would be
flaky, which is the same defect `Test_EnlistedTransactionPreservedWhilePooled` already
demonstrates.

Instead the predicate was extracted into
`SqlConnectionInternal.ShouldPreserveTransactionOnReset` and pinned directly by
`SqlConnectionInternalResetTransactionTests`, following the existing precedent of
`ResolveLoginTimeout` / `SqlConnectionInternalTimeoutTests`. The tests were themselves
mutation-tested:

| Condition compiled into the helper | Bug it contains | New tests |
|---|---|---|
| `hasEnlistedTransaction` (#3019) | **#4001** | ❌ 2 failed |
| `isTransactionRoot && is2008OrNewer` (pre-#3019) | **#2970** | ❌ 4 failed |
| Union without the 2008 guard | pre-2008 regression | ❌ 2 failed |
| The shipped condition | none | ✅ 19 passed |

This satisfies "fails before the change, passes after" for **both** regressions, and
does so deterministically and without a server.

---

## 8. Related

- **#2970** — the issue #3019 was fixing. Fully preserved by this change (section 5).
- **#2285** — reports the same exception with no reproduction. Plausibly the same root
  cause, though unconfirmed.
