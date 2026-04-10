# Quickstart: Pool Pruning

**Feature**: Pool Pruning for ChannelDbConnectionPool  
**Date**: 2026-04-10

## What This Feature Does

Adds periodic idle connection pruning to `ChannelDbConnectionPool`. When demand drops, the pool automatically closes excess idle connections to reduce resource consumption, using a sampling-based approach that avoids over-pruning during transient lulls.

## User-Facing Changes

### New Connection String Keyword

```
Connection Pruning Interval=10
```

Controls how often (in seconds) the pool samples idle connection counts and evaluates for pruning. Default: `10`. Set to `0` to disable pruning.

### Observable Behavior

1. **Under steady high load**: No pruning occurs — all connections are busy
2. **After load drops**: Pool samples idle counts over `ConnectionLifetime / PruningInterval` intervals, then prunes median-idle connections
3. **After pruning**: If demand returns, new connections are created on-demand (subject to rate limiting and MaxPoolSize)
4. **MinPoolSize floor**: Pool never prunes below MinPoolSize

## How to Enable (Channel Pool V2)

Pruning is automatically active when using the channel-based connection pool (V2):

```csharp
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", true);

var builder = new SqlConnectionStringBuilder
{
    DataSource = "myserver",
    InitialCatalog = "mydb",
    IntegratedSecurity = true,
    // Pruning configuration (optional — defaults shown)
    // ConnectionPruningInterval = 10,  // Check every 10 seconds
    // MinPoolSize = 0,                 // Allow full pruning
    // MaxPoolSize = 100,               // Upper bound
};

using var conn = new SqlConnection(builder.ToString());
conn.Open();
// ... use connection ...
```

## Contracts

This feature is purely internal to the pool implementation. There are no new public API types or interfaces.

The only external contract is the connection string keyword, which flows through the existing `SqlConnectionStringBuilder` → `SqlConnectionString` → `DbConnectionPoolGroupOptions` pipeline.

## Architecture Overview

```
SqlConnectionStringBuilder
  └── Connection Pruning Interval → SqlConnectionString → DbConnectionPoolGroupOptions

ChannelDbConnectionPool
  ├── Constructor: create Timer + sample buffer
  ├── UpdatePruningTimer(): enable/disable based on connection count vs MinPoolSize
  ├── PruneIdleConnections(): static timer callback
  │     ├── Sample _idleCount into buffer
  │     ├── When buffer full: sort, median, prune
  │     └── Re-arm timer
  ├── ReturnInternalConnection: increment _idleCount
  ├── GetIdleConnection: decrement _idleCount
  ├── Startup(): no-op for timer (lazy enable via UpdatePruningTimer)
  └── Shutdown(): dispose timer
```
