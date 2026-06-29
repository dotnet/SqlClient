# Fix 10: Integration Test Suite

**Priority:** Medium — validates correctness under real-world conditions
**Risk:** Low

## Problem

Existing V2 pool tests are unit tests with mocks. Before making the V2 pool the default,
comprehensive integration tests against a real SQL Server are needed.

## Location

**Directory:** `src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ConnectionPoolTest/`

## Changes Required

### Adapt Existing V1 Tests

The following existing test files should be parameterized to run against both V1 and V2 pools:

- `ConnectionPoolTest.cs` — basic pool operations
- `ConnectionPoolStressTest.cs` — concurrent load testing
- `TransactionPoolTest.cs` — transaction enlistment

### New V2-Specific Tests

1. **Parallel cold start** — Open 100 connections concurrently on an empty pool, measure total time,
   verify all connections valid.

2. **Async correctness** — Verify `OpenAsync()` returns without blocking the calling thread pool
   thread (use a constrained thread pool to detect blocking).

3. **Failover recovery** — Kill server connection, verify pool recovers and error backoff works
   correctly.

4. **Azure SQL serverless resume** — If testable, verify pool handles the paused → resumed SQL
   server transition.

5. **Token refresh under load** — With Entra ID auth, concurrent operations during token expiry.
   This was the scenario in #2152.

6. **Clear/Reset** — `ClearPool()` under active connections, verify graceful cleanup.

7. **Mixed sync/async** — Verify pool handles interleaved `Open()` and `OpenAsync()` calls.

### Benchmark Suite

Create a benchmark project that compares V1 vs V2 for:

- Cold start (empty pool → N connections)
- Warm pool throughput (sustained acquire/release)
- High contention (100 concurrent openers, max pool = 50)
- Memory allocation profile
