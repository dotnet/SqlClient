# Database Context Preservation Across Internal Reconnections

## Issue

[dotnet/SqlClient#4108](https://github.com/dotnet/SqlClient/issues/4108) — `SqlConnection` doesn't
restore database in the new session if connection is lost.

When a user changes the active database via `USE [db]` through `SqlCommand.ExecuteNonQuery()`, and
the physical connection subsequently breaks and is transparently reconnected, the recovered session
may land on the **initial catalog** from the connection string instead of the database the user
switched to.

## Status

**Fix implemented and validated.** The root cause (Issue G) has been identified and fixed. See
[03-issues.md](03-issues.md) for the full issue list and [04-recommendations.md](04-recommendations.md)
for the fix details. Server-side analysis of the SQL Server engine's session recovery code confirms
the fix is correct — see [06-server-side-analysis.md](06-server-side-analysis.md).

### Root Cause

`CompleteLogin()` in `SqlConnectionInternal.cs` trusted the server's `ENV_CHANGE` response
unconditionally after session recovery. If the server did not properly restore the database context,
the client silently ended up on the wrong database.

### Server-Side Confirmation

Analysis of the SQL Server source code (`featureext.cpp`, `login.cpp`, `session.cpp`) confirms that
the server correctly implements session recovery for database context — the recovery database from
the ToBe chunk is treated as mandatory (`Source #0`) with no silent fallback. The root cause is
entirely **client-side**: `CompleteLogin()` did not verify the server's response matched the recovery
target.

### Fix

After session recovery completes in `CompleteLogin()`, the fix compares `CurrentDatabase` (set by
the server's `ENV_CHANGE`) against the database from `_recoverySessionData`. If they differ, a
`USE [database]` command is sent to the server to force alignment. This guarantees both client and
server are on the correct database after recovery, regardless of server behavior.

The fix is gated behind the `Switch.Microsoft.Data.SqlClient.VerifyRecoveredDatabaseContext`
AppContext switch (default: `true`). Manual tests set it to `false` to confirm the server-only path
fails without the fix.

### Key Properties of the Fix

- **Correct**: Both client and server are guaranteed to be on the same database after recovery
- **Safe**: Only executes during reconnection (`_recoverySessionData != null`), never on first login
- **Efficient**: No overhead when the server properly restores the database (the common case)
- **Defensive**: Handles both wrong-database and missing-ENV_CHANGE server behaviors

## Scope

This analysis covers every code path where an internal reconnection can occur and evaluates whether
the current database context (`CurrentDatabase`) is correctly maintained. The assumption is:

> Any internal reconnection within `SqlConnection` must maintain the current database context.

## Documents

| File | Contents |
| ---- | -------- |
| [01-architecture.md](01-architecture.md) | How database context is tracked and how session recovery works |
| [02-flows.md](02-flows.md) | Every reconnection flow, annotated with database context behaviour |
| [03-issues.md](03-issues.md) | Identified bugs and gaps, ranked by severity |
| [04-recommendations.md](04-recommendations.md) | Proposed fixes |
| [05-reconnection-and-retry-mechanisms.md](05-reconnection-and-retry-mechanisms.md) | All retry/reconnection mechanisms with public doc cross-references |
| [06-server-side-analysis.md](06-server-side-analysis.md) | SQL Server engine session recovery internals, including `ParseFeatureData`, `ParseSessionDataChunk`, `FDetermineSessionDb`, test coverage gaps |
