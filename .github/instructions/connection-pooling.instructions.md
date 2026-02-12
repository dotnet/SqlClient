---
applyTo: "**/ConnectionPool/**,**/SqlConnection*.cs,**/*Pool*.cs"
---
# Connection Pooling Implementation Guide

## Overview

Microsoft.Data.SqlClient implements connection pooling to efficiently reuse database connections, reducing connection establishment overhead and improving application performance.

## Pool Implementations

### ChannelDbConnectionPool (Modern)
Located in `ConnectionPool/ChannelDbConnectionPool.cs`:
- Uses `System.Threading.Channels` for async-friendly pooling
- FIFO fairness for connection requests
- Reduced lock contention
- Better performance for async workloads

### WaitHandleDbConnectionPool (Legacy)
Located in `ConnectionPool/WaitHandleDbConnectionPool.cs`:
- Uses traditional wait handles
- Compatible with older .NET Framework patterns
- May block threads while waiting

## Key Components

### Pool Hierarchy
```
DbConnectionPoolGroup
    └── Multiple pools by identity
        └── DbConnectionPool
            ├── Idle connections (channel)
            ├── Busy connections (tracking)
            └── Pending requests (queue)
```

### Core Classes

| Class | Responsibility |
|-------|---------------|
| `ChannelDbConnectionPool` | Pool implementation |
| `ConnectionPoolSlots` | Tracks all connections |
| `DbConnectionPoolGroup` | Groups pools by connection string |
| `DbConnectionPoolKey` | Pool identity key |
| `DbConnectionPoolOptions` | Pool configuration |
| `TransactedConnectionPool` | DTC transaction support |

## Pool Configuration

### Connection String Keywords

| Keyword | Default | Description |
|---------|---------|-------------|
| `Pooling` | `true` | Enable/disable pooling |
| `Min Pool Size` | 0 | Minimum connections |
| `Max Pool Size` | 100 | Maximum connections |
| `Connection Lifetime` | 0 | Max connection age (seconds) |
| `Load Balance Timeout` | 0 | Load balance connection time |
| `Connection Reset` | `true` | Reset on return to pool |

### Pool Sizing
```csharp
// Typical connection string with pool settings
"Server=host;Min Pool Size=5;Max Pool Size=50;Pooling=true;"
```

## Connection Lifecycle

### Getting Connection
```
1. TryGetConnection()
    ├── Check idle channel
    │   └── Found → Return existing
    └── Not found
        ├── Under max limit → Create new
        └── At limit → Wait for release
```

### Returning Connection
```
1. ReturnConnection()
    ├── Connection valid?
    │   ├── No → Remove from pool
    │   └── Yes → Check lifetime
    │       ├── Expired → Remove
    │       └── Valid → Return to idle channel
    └── Signal waiting requests
```

## Transacted Connections

### Distributed Transactions (DTC)
- `TransactedConnectionPool` manages enlisted connections
- Connections bound to `System.Transactions.Transaction`
- Released when transaction completes

### Local Transactions
- Handled within single connection
- Pool reset on close if transaction active

## Pool Health

### Connection Validation
- Connections validated before reuse
- Broken connections removed automatically
- Periodic cleanup of stale connections

### Blocking Period
- Pool enters blocking period after connection failures
- Controlled by `PoolBlockingPeriod` enum:
  - `Auto` - Azure SQL uses shorter timeout
  - `AlwaysBlock` - Standard blocking
  - `NeverBlock` - No blocking

## Implementation Details

### Channel-Based Pool
```csharp
// Idle connections stored in channel
private readonly ChannelReader<DbConnectionInternal?> _idleConnectionReader;
private readonly ChannelWriter<DbConnectionInternal?> _idleConnectionWriter;

// Wait for connection (async-friendly)
await _idleConnectionReader.WaitToReadAsync(token);
```

### Sync Over Async Protection
```csharp
// Prevent thread pool starvation
private static SemaphoreSlim _syncOverAsyncSemaphore = 
    new(Math.Max(1, Environment.ProcessorCount / 2));
```

## Best Practices

### Application Design
```csharp
// DO: Use using pattern
using (var connection = new SqlConnection(connectionString))
{
    await connection.OpenAsync();
    // Use connection
} // Automatically returned to pool

// DON'T: Keep connections open long-term
var connection = new SqlConnection(connectionString);
connection.Open();
// Long-running operation...
connection.Close(); // Bad: Connection held too long
```

### Pool Sizing
- **Min Pool Size**: Set to typical concurrent connections
- **Max Pool Size**: Set based on SQL Server max connections
- Monitor pool exhaustion in production

### Connection Lifetime
- Use for load balancing across cluster nodes
- Avoid for single-server scenarios

## Debugging Pool Issues

### Common Problems

1. **Pool Exhaustion**
   - Symptom: Timeouts waiting for connection
   - Cause: Connections not being closed/disposed
   - Fix: Ensure `using` pattern or explicit `Dispose()`

2. **Connection Leaks**
   - Symptom: Growing connection count
   - Cause: Missing `Dispose()` calls
   - Debug: Enable connection leak detection

3. **Performance Degradation**
   - Symptom: Slow connection acquisition
   - Cause: Pool too small or connection churn
   - Fix: Adjust pool size, check connection lifetime

### Diagnostics
```csharp
// Get pool statistics (if available)
SqlConnection.ClearPool(connection);    // Clear specific pool
SqlConnection.ClearAllPools();          // Clear all pools
```

### Event Tracing
Connection pool events traced via `SqlClientEventSource`:
- Pool creation/destruction
- Connection acquisition/release
- Pool sizing changes

## Related Files

- [SqlConnection.cs](../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnection.cs)
- [SqlConnectionFactory.cs](../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnectionFactory.cs)
- [ChannelDbConnectionPool.cs](../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs)
