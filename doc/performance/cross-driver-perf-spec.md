# Cross-Driver Performance Test Specification

## 1. Introduction

This document defines a standardized set of performance benchmark scenarios applicable to all Microsoft SQL Server client drivers. The specification is designed to enable cross-driver performance comparison and ensure each driver team measures performance consistently against a common baseline.

### 1.1 Scope

This specification covers the following SQL Server drivers:

| Driver | Language/Runtime | Package/Library |
|--------|-----------------|-----------------|
| **Microsoft.Data.SqlClient** | C# / .NET | NuGet: Microsoft.Data.SqlClient |
| **ODBC Driver for SQL Server** | C/C++ (native) | msodbcsql18 |
| **JDBC Driver for SQL Server** | Java | Maven: mssql-jdbc |
| **PHP Driver for SQL Server** | PHP | PECL: sqlsrv / pdo_sqlsrv |
| **Python Driver (pyodbc)** | Python | PyPI: pyodbc |

### 1.2 Terminology

| Term | Definition |
|------|-----------|
| **Wall-clock latency** | Elapsed real time from operation start to completion |
| **Mean** | Arithmetic mean of all measured iterations |
| **P50 / P95 / P99** | 50th / 95th / 99th percentile of measured latency distribution |
| **Throughput** | Number of operations completed per second (ops/sec) |
| **Memory allocation** | Bytes allocated per operation (where measurable by runtime) |
| **Warm cache** | Operation measured after initial execution has populated any internal caches |
| **Cold cache** | Operation measured on first execution with empty caches |

### 1.3 General Guidelines

- All benchmarks SHALL use a dedicated SQL Server instance (not shared with other workloads)
- The SQL Server version SHOULD be SQL Server 2022 or later
- Connection strings SHOULD use `TrustServerCertificate=true` for test environments
- Each scenario SHOULD be executed with sufficient iterations to produce statistically meaningful results (minimum 10 iterations after warmup)
- Both sync and async variants SHALL be measured where the driver supports async APIs

---

## 2. Measurement Dimensions

Every benchmark scenario in this specification SHALL report the following measurement dimensions where applicable:

### 2.1 Required Dimensions

| Dimension | Unit | Description |
|-----------|------|-------------|
| **Mean Latency** | milliseconds (ms) | Arithmetic mean of all iterations |
| **P50 Latency** | ms | Median latency |
| **P95 Latency** | ms | 95th percentile |
| **P99 Latency** | ms | 99th percentile |
| **Throughput** | ops/sec | Operations completed per second |

### 2.2 Optional Dimensions (runtime-dependent)

| Dimension | Unit | Applicable Drivers |
|-----------|------|--------------------|
| **Memory Allocation** | bytes/op | SqlClient (.NET), JDBC (Java) |
| **GC Collections** | count/op | SqlClient (.NET) |
| **Thread Count** | count | All (for concurrency benchmarks) |
| **CPU Time** | ms | ODBC (via OS counters), SqlClient (ETW) |

### 2.3 Driver-Specific Measurement Tools

| Driver | Recommended Tool |
|--------|-----------------|
| SqlClient | BenchmarkDotNet with MemoryDiagnoser |
| JDBC | JMH (Java Microbenchmark Harness) |
| ODBC | Custom C/C++ timer + `QueryPerformanceCounter` / `clock_gettime` |
| PHP | Custom `microtime()` harness |
| Python | `timeit` module or `pytest-benchmark` |

---

## 3. Benchmark Categories

### 3.1 Connection Benchmarks

#### Scenario C1: Single Connection Open/Close (Pooled)

**SQL**: N/A (connection lifecycle only)

**Table Schema**: None required

**Parameters**:
- Iterations: 1000
- `Pooling=true` (or driver equivalent)

**Measurement**: Latency per open+close cycle

**Driver Adaptations**:
- SqlClient: `SqlConnection.Open()` / `.Close()` with `Pooling=true`
- JDBC: `DriverManager.getConnection()` / `.close()` (pooling via HikariCP or driver pool)
- ODBC: `SQLDriverConnect()` / `SQLDisconnect()` with connection pooling enabled
- PHP: `sqlsrv_connect()` / `sqlsrv_close()` with `ConnectionPooling=1`
- Python: `pyodbc.connect()` / `.close()` with pooling enabled

#### Scenario C2: Single Connection Open/Close (Non-Pooled)

Same as C1, but with `Pooling=false` (or driver equivalent).

#### Scenario C3: Concurrent Connection Open (N Threads)

**Parameters**:
- Thread counts: 10, 50, 100
- `Pooling=true`, `MaxPoolSize=200`

**Measurement**: Total wall-clock time for N threads to each open one connection. Per-connection mean latency.

**Driver Adaptations**:
- Each driver uses its native threading/async model
- Java: `ExecutorService` with `Callable` tasks
- Python: `concurrent.futures.ThreadPoolExecutor`
- PHP: Not natively multi-threaded; use `pcntl_fork` or skip

#### Scenario C4: Encryption Mode Impact

**Parameters**:
- `Encrypt=Optional` (or `false`)
- `Encrypt=Mandatory` (or `true`)
- `Encrypt=Strict` (TDS 8.0, where supported)

**Measurement**: Connection open latency per encryption mode

---

### 3.2 Query Execution Benchmarks

#### Scenario Q1: Simple SELECT (Single Row)

**SQL**:
```sql
SELECT Id, Name FROM BenchmarkTable WHERE Id = 1
```

**Table Schema**:
```sql
CREATE TABLE BenchmarkTable (
    Id INT PRIMARY KEY,
    Name VARCHAR(100)
)
-- Insert 1 row: (1, 'TestData')
```

**Measurement**: Latency for execute + read of single row

#### Scenario Q2: SELECT N Rows

**SQL**:
```sql
SELECT * FROM BenchmarkLargeTable
```

**Table Schema**:
```sql
CREATE TABLE BenchmarkLargeTable (
    Id INT PRIMARY KEY,
    Col_Int INT,
    Col_Varchar VARCHAR(100),
    Col_Decimal DECIMAL(10,4),
    Col_DateTime DATETIME2
)
-- Insert N rows (100, 1000, 10000)
```

**Parameters**: Row counts: 100, 1,000, 10,000

**Measurement**: Latency for execute + full row consumption

#### Scenario Q3: Single INSERT

**SQL**:
```sql
INSERT INTO BenchmarkInsertTable (Name, Value) VALUES (@name, @value)
```

**Table Schema**:
```sql
CREATE TABLE BenchmarkInsertTable (
    Id INT IDENTITY PRIMARY KEY,
    Name VARCHAR(100),
    Value INT
)
```

**Measurement**: Latency per INSERT operation

#### Scenario Q4: Parameterized SELECT

**SQL**:
```sql
SELECT Name FROM BenchmarkTable WHERE Id = @id
```

**Measurement**: Latency comparison vs non-parameterized equivalent (`WHERE Id = 1`)

#### Scenario Q5: Stored Procedure Execution

**SQL**:
```sql
EXEC dbo.GetBenchmarkData
```

**Setup**:
```sql
CREATE PROCEDURE dbo.GetBenchmarkData AS SELECT * FROM BenchmarkTable
```

**Measurement**: Latency comparison vs inline `SELECT * FROM BenchmarkTable`

#### Scenario Q6: Batch Execution

**SQL** (in a single batch):
```sql
INSERT INTO BenchmarkInsertTable (Name, Value) VALUES ('Item_1', 1)
INSERT INTO BenchmarkInsertTable (Name, Value) VALUES ('Item_2', 2)
-- ... N commands
```

**Parameters**: Batch sizes: 5, 10, 50, 100

**Measurement**: Latency per batch. Compare batched vs individual execution.

**Driver Adaptations**:
- SqlClient: `SqlBatch` API (.NET only) or semicolon-separated commands
- JDBC: `Statement.addBatch()` / `executeBatch()`
- ODBC: `SQLExecDirect` with concatenated statements
- PHP: `sqlsrv_query` with multi-statement SQL
- Python: `cursor.execute` with multi-statement or `executemany`

---

### 3.3 Data Type Read Benchmarks

All data type benchmarks use the same structure:
1. Create a single-column table of the specified type
2. Insert N rows with representative values
3. SELECT all rows and read each value through the driver's data reader
4. Measure total read latency

**Parameters**: Row count: 1,000

#### Scenario D1: Integer Types

| Type | SQL Column Def | Sample Value |
|------|---------------|--------------|
| `INT` | `INT` | 123456 |
| `BIGINT` | `BIGINT` | 1234567890 |
| `SMALLINT` | `SMALLINT` | 1234 |
| `TINYINT` | `TINYINT` | 123 |
| `BIT` | `BIT` | 1 |

#### Scenario D2: Decimal Types

| Type | SQL Column Def | Sample Value |
|------|---------------|--------------|
| `DECIMAL(10,4)` | `DECIMAL(10,4)` | 12345.6789 |
| `FLOAT` | `FLOAT` | 12345.6789 |
| `REAL` | `REAL` | 12345.6789 |

#### Scenario D3: String Types

| Type | SQL Column Def | Sample Value |
|------|---------------|--------------|
| `VARCHAR(100)` | `VARCHAR(100)` | 'abcde12345...' (16 chars) |
| `NVARCHAR(100)` | `NVARCHAR(100)` | N'abcde12345ŞȽ...' (16 chars) |

#### Scenario D4: Date/Time Types

| Type | SQL Column Def | Sample Value |
|------|---------------|--------------|
| `DATETIME2` | `DATETIME2` | '2024-01-15 10:30:45.1234567' |
| `DATETIMEOFFSET` | `DATETIMEOFFSET` | '2024-01-15 10:30:45.1234567 +05:30' |
| `DATE` | `DATE` | '2024-01-15' |
| `TIME` | `TIME` | '10:30:45.1234567' |

#### Scenario D5: Binary Types

| Type | SQL Column Def | Sample Value |
|------|---------------|--------------|
| `VARBINARY(100)` | `VARBINARY(100)` | 0x0001E240... (100 bytes) |

#### Scenario D6: Large Object (LOB) Types

| Type | SQL Column Def | Data Sizes |
|------|---------------|-----------|
| `VARCHAR(MAX)` | `VARCHAR(MAX)` | 1 MB, 2 MB, 5 MB |
| `NVARCHAR(MAX)` | `NVARCHAR(MAX)` | 1 MB, 2 MB, 5 MB |
| `VARBINARY(MAX)` | `VARBINARY(MAX)` | 1 MB, 2 MB, 5 MB |

**Parameters**: Data size: 1 MB, 2 MB, 5 MB (single row per size)

---

### 3.4 Bulk Insert Benchmarks

#### Scenario B1: Bulk Insert N Rows

**Table Schema**: See Q2 (5-column table) or scaled:
- 7 columns (mixed types)
- 25 columns (all common types)
- 50 columns (extended)

**Parameters**:
- Row counts: 1,000 / 10,000 / 100,000
- Column counts: 7 / 25 / 50

**Driver Adaptations**:
- SqlClient: `SqlBulkCopy.WriteToServer()`
- JDBC: `SQLServerBulkCopy.writeToServer()`
- ODBC: `bcp_sendrow` / `bcp_batch` or parameterized INSERT loops
- PHP: `sqlsrv_query` with bulk insert syntax or TVP
- Python: `cursor.executemany` or `fast_executemany=True`

**Measurement**: Total latency for bulk insert. Throughput in rows/second.

---

### 3.5 Transaction Benchmarks

#### Scenario T1: Begin-Commit Cycle

**SQL**:
```sql
BEGIN TRANSACTION
INSERT INTO BenchmarkInsertTable (Name, Value) VALUES ('TxnTest', 1)
COMMIT
```

**Measurement**: Latency for full begin-insert-commit cycle

#### Scenario T2: Begin-Rollback Cycle

Same as T1, but with `ROLLBACK` instead of `COMMIT`.

#### Scenario T3: Isolation Level Variants

**Isolation Levels**:
- READ COMMITTED (default)
- REPEATABLE READ
- SERIALIZABLE
- SNAPSHOT (requires database-level enablement)

**SQL**:
```sql
SET TRANSACTION ISOLATION LEVEL <level>
BEGIN TRANSACTION
UPDATE BenchmarkTable SET Name = 'Updated' WHERE Id = 1
ROLLBACK
```

**Measurement**: Latency per isolation level for the begin-update-rollback cycle

---

## 4. Driver Adaptations Appendix

### 4.1 SqlClient (.NET)

- **Framework**: BenchmarkDotNet with `[Benchmark]` attributes
- **Async**: `OpenAsync()`, `ExecuteReaderAsync()`, `ReadAsync()` — always test both sync and async
- **Batch**: Use `SqlBatch` API on .NET 8+ for scenario Q6
- **Bulk**: `SqlBulkCopy` supports `IDataReader`, `SqlDataReader`, `DataTable` sources
- **Memory**: `MemoryDiagnoser` reports bytes/op and GC info

### 4.2 ODBC Driver

- **Framework**: Custom C/C++ timing harness or system benchmarking tools
- **Async**: ODBC async via `SQL_ATTR_ASYNC_ENABLE` + polling; often excluded from benchmarks
- **Batch**: Concatenate SQL statements; limited batch API
- **Bulk**: Use `bcp` utility functions or parameterized multi-row INSERT
- **Memory**: OS-level memory counters (`GetProcessMemoryInfo`, `getrusage`)

### 4.3 JDBC Driver

- **Framework**: JMH (Java Microbenchmark Harness) with `@Benchmark` annotations
- **Async**: JDBC is synchronous; use thread pools for concurrency benchmarks
- **Batch**: `Statement.addBatch()` / `Statement.executeBatch()`
- **Bulk**: `SQLServerBulkCopy` API (Microsoft JDBC driver specific)
- **Memory**: JMH `@State(Scope.Thread)` + GC profiling

### 4.4 PHP Driver

- **Framework**: Custom `microtime(true)` timing or phpbench
- **Async**: Not natively supported; skip or use process-level concurrency
- **Batch**: Multi-statement SQL via `sqlsrv_query` or PDO `exec`
- **Bulk**: Parameterized multi-row INSERT or TVP via `sqlsrv_send_stream_data`
- **Memory**: `memory_get_peak_usage()` for allocation tracking

### 4.5 Python (pyodbc)

- **Framework**: `timeit`, `pytest-benchmark`, or custom timing
- **Async**: Use `asyncio` with `aioodbc` if available; otherwise skip
- **Batch**: `cursor.executemany()` or `fast_executemany=True`
- **Bulk**: `fast_executemany` + batch size tuning
- **Memory**: `tracemalloc` for allocation tracking

---

## 5. Reporting Requirements

### 5.1 Output Format

Each driver team SHALL produce results in a structured format (JSON or CSV) containing:

```json
{
  "driver": "SqlClient",
  "version": "6.0.0",
  "sqlServerVersion": "2022",
  "targetFramework": "net8.0",
  "timestamp": "2026-04-01T12:00:00Z",
  "benchmarks": [
    {
      "scenario": "C1",
      "name": "SingleConnectionOpenClose_Pooled",
      "meanLatencyMs": 0.42,
      "p50LatencyMs": 0.38,
      "p95LatencyMs": 0.65,
      "p99LatencyMs": 1.2,
      "throughputOpsPerSec": 2380,
      "memoryBytesPerOp": 1024
    }
  ]
}
```

### 5.2 Comparison

When comparing across drivers, normalize by:
- Same SQL Server instance and version
- Same network topology (local vs. remote)
- Same data volumes and table schemas
- Focus on relative trends, not absolute numbers (different runtimes have inherently different overhead)
