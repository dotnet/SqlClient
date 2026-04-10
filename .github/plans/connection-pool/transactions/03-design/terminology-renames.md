# Terminology Renames

Track naming inconsistencies and proposed renames to improve clarity.

## Enlistment Path Naming

The two enlistment paths in `EnlistNonNull` use confusing terminology — "enlisted" is both a generic term (any transaction association) and a specific term (the non-delegated DTC path).

| Current Name | Where Used | Proposed Name | Rationale |
|-------------|-----------|---------------|-----------|
| `IsEnlistedInTransaction` | `SqlConnectionInternal` | `IsPropagatedTransaction` or `IsDistributedEnlistment` | Only set on the non-delegated (DTC cookie) path. "Propagated" matches TDS layer naming (`PropagateTransactionCookie`, `TransactionManagerRequestType.Propagate`). |

## Terminology Reference

How each layer currently names the two enlistment paths:

| Layer | Delegated (PSPE) Path | Non-Delegated (DTC) Path |
|-------|----------------------|--------------------------|
| System.Transactions | `EnlistPromotableSinglePhase` | (promotion triggers DTC) |
| SqlConnectionInternal logs | `"delegated to transaction"` | `"delegation not possible, enlisting"` |
| SqlConnectionInternal properties | `DelegatedTransaction`, `IsTransactionRoot` | `IsEnlistedInTransaction` |
| SqlInternalTransaction | `TransactionType.Delegated` | `TransactionType.Distributed` |
| TDS protocol | `Begin/Promote/Commit/Rollback` | `Propagate` |
| Base class (DbConnectionInternal) | `EnlistedTransaction` (both paths) | `EnlistedTransaction` (both paths) |

## Other Renames to Track

| Current Name | Location | Issue | Proposed |
|-------------|----------|-------|----------|
| | | | |
