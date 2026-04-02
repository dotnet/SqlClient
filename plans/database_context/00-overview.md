# Database Context Preservation Across Internal Reconnections

## Issue

[dotnet/SqlClient#4108](https://github.com/dotnet/SqlClient/issues/4108) — `SqlConnection` doesn't
restore database in the new session if connection is lost.

When a user changes the active database via `USE [db]` through `SqlCommand.ExecuteNonQuery()`, and
the physical connection subsequently breaks and is transparently reconnected, the recovered session
may land on the **initial catalog** from the connection string instead of the database the user
switched to.

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
