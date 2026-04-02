# Microsoft.Data.SqlClient — Benchmark Design Overview

> **One-pager**: Architecture, runner catalog, and data flow for the BenchmarkDotNet performance test suite.

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  ENTRY POINT                                                                │
│  Program.cs ──loads──► runnerconfig.json                                    │
│      │                 datatypes.json                                       │
└──────┼──────────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  CONFIGURATION LAYER                                                        │
│                                                                             │
│  ┌──────────────┐  ┌──────────────────┐  ┌─────────────┐  ┌────────────┐  │
│  │  Config.cs    │  │ BenchmarkConfig  │  │ DataTypes.cs│  │ Loader.cs  │  │
│  │  Deserialize  │─►│ BDN ManualConfig │  │ 27 SQL type │  │ JSON load  │  │
│  │  runner config│  │ factory          │  │ metadata    │  │ + env var  │  │
│  └──────────────┘  └──────────────────┘  └─────────────┘  └────────────┘  │
└──────────────────────────────┬──────────────────────────────────────────────┘
                               │
       ┌───────────────────────┼───────────────────────┐
       ▼                       ▼                       ▼
┌──────────────┐  ┌─────────────────────┐  ┌───────────────────────────────┐
│ DB FRAMEWORK │  │  16 BENCHMARK       │  │  RESULTS OUTPUT               │
│              │  │  RUNNERS            │  │                               │
│ Column       │  │                     │  │  BenchmarkDotNet.Artifacts/   │
│ Table        │──│  (see catalog       │──│  ├── results/*.json           │
│ TablePatterns│  │   below)            │  │  │                            │
│ DbUtils      │  │                     │  │  MemoryDiagnoser (alloc)     │
│              │  │                     │  │  ThreadingDiagnoser           │
└──────────────┘  └─────────────────────┘  │  JsonExporter.Full           │
                                           │  EtwProfiler (Windows)       │
                                           └──────────────┬────────────────┘
                                                          │
                                                          ▼
                                           ┌──────────────────────────────┐
                                           │ Compare-BenchmarkResults     │
                                           │ .py / .ps1  (±10% threshold)│
                                           └──────────────────────────────┘
```

---

## Runner Catalog — What Gets Benchmarked

```
MDS BENCHMARKS
│
├── CONNECTIVITY
│   ├── SqlConnectionRunner ·········· Open/OpenAsync, sequential ×100, concurrent ×64
│   │                                  [Params] MARS (T/F) × Pooling (T/F) = 4 combos
│   └── ConnectionPoolRunner ········· Concurrent pooled open, open/close cycle
│                                      [Params] Threads: 10 / 50 / 100
│
├── COMMAND EXECUTION
│   ├── SqlCommandRunner ············· ExecuteReader/Scalar/NonQuery/XmlReader (sync+async)
│   │                                  Concurrent starvation tests ×64
│   ├── CommandBehaviorRunner ········ Default / SingleResult / SingleRow / SequentialAccess
│   ├── StoredProcedureRunner ········ Inline SQL vs StoredProcedure
│   ├── PreparedStatementRunner ······ Prepared vs non-prepared inserts
│   └── ParameterizedQueryRunner ····· Parameterized vs inline literal queries
│
├── DATA READING
│   ├── DataTypeReaderRunner ········· 25 sync benchmark methods (one per SQL type)
│   ├── DataTypeReaderAsyncRunner ···· 24 async benchmark methods (one per SQL type)
│   ├── LargeDataTypeRunner ·········· VARCHAR/NVARCHAR/VARBINARY(MAX), XML
│   │                                  [Params] DataSizeMB: 1 / 2 / 5
│   ├── JsonDataTypeRunner ··········· JSON type sync/async (SQL Server 2025+)
│   │                                  [Params] RowCount: 100/1K/10K/100K × JsonKeyCount: 5/50/200
│   └── VectorDataTypeRunner ········· VECTOR type sync/async (SQL Server 2025+)
│                                      [Params] RowCount: 100/1K/10K/100K × Dimensions: 5/50/200
│
├── BULK & BATCH
│   ├── SqlBulkCopyRunner ··········· IDataReader / SqlDataReader / DataTable
│   │                                 [Params] Cols: 7/25/50 × BatchSize: 100/1K/5K
│   └── BatchApiRunner ·············· SqlBatch vs individual command loop (.NET only)
│                                     [Params] BatchCount: 5 / 10 / 50 / 100
│
├── TRANSACTIONS
│   └── TransactionRunner ··········· Begin/Update/Rollback, Begin/Insert/Commit, Enlist
│                                     [Params] IsolationLevel: RC / RR / S / Snapshot
│
└── SECURITY
    └── AlwaysEncryptedRunner ······· Plaintext vs encrypted INSERT + SELECT
                                      (disabled by default — requires AE setup)
```

---

## Configuration & Data Flow

```
 runnerconfig.json                        datatypes.json
 ┌────────────────────────┐               ┌─────────────────────────┐
 │ ConnectionString       │               │ Numerics (7 types)      │
 │ UseManagedSniOnWindows │               │ Decimals (4 types)      │
 │ UseOptimizedAsync      │               │ DateTimes (6 types)     │
 │                        │               │ Characters (2 types)    │
 │ Benchmarks:            │               │ Binary (1 type)         │
 │  ├─ SqlConnection:     │               │ MaxTypes (3 types)      │
 │  │   Enabled: true     │               │ Others (4: guid,xml,    │
 │  │   LaunchCount: 1    │               │          json,vector)   │
 │  │   IterationCount: 3 │               └────────────┬────────────┘
 │  │   InvocationCount: 5│                            │
 │  │   WarmupCount: 1    │                            │
 │  │   RowCount: 1000    │                   27 SQL types with
 │  ├─ SqlCommand: ...    │                   defaults + bounds
 │  ├─ SqlBulkCopy: ...   │                            │
 │  └─ (16 total)         │                            │
 └───────────┬────────────┘                            │
             │                                         │
             ▼                                         ▼
 ┌─────────────────────────────────────────────────────────────┐
 │  BenchmarkConfig (ManualConfig)                             │
 │                                                             │
 │  Job.LongRun  ─── UnrollFactor=1, Throughput strategy       │
 │  + MemoryDiagnoser.Default  (Gen0/1/2, allocated bytes)     │
 │  + ThreadingDiagnoser.Default  (work items, contentions)    │
 │  + JsonExporter.Full  (CI comparison)                       │
 │  + DisableOptimizationsValidator                            │
 │  + EtwProfiler  (Windows only)                              │
 └─────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
             BenchmarkRunner.Run<TRunner>(config)
                           │
                           ▼
             BenchmarkDotNet.Artifacts/results/*.json
```

---

## Setup / Cleanup Lifecycle

```
 BenchmarkDotNet Engine                Runner                    SQL Server
 ─────────────────────                 ──────                    ──────────

 ┌─ GlobalSetup (once) ──────────────────────────────────────────────────┐
 │                                                                       │
 │  BDN ──► [GlobalSetup] ──► Open connection(s)  ──────────────► DB    │
 │                         ──► CREATE TABLE + INSERT N rows ────► DB    │
 └───────────────────────────────────────────────────────────────────────┘

 ┌─ Iteration Loop (repeated) ──────────────────────────────────────────┐
 │                                                                       │
 │  ┌─ [IterationSetup]  (optional — SqlBulkCopy, BatchApi, etc.) ──┐  │
 │  │  BDN ──► TRUNCATE table / reset DataReader position  ────► DB  │  │
 │  └────────────────────────────────────────────────────────────────┘  │
 │                                                                       │
 │  BDN ──► [Benchmark] method ──► Execute command / Read data ──► DB   │
 │       ◄── Return timing ◄──────                                      │
 │                                                                       │
 │  ┌─ [IterationCleanup]  (optional) ─────────────────────────────┐   │
 │  │  (rarely used)                                                │   │
 │  └───────────────────────────────────────────────────────────────┘   │
 └───────────────────────────────────────────────────────────────────────┘

 ┌─ GlobalCleanup (once) ───────────────────────────────────────────────┐
 │                                                                       │
 │  BDN ──► [GlobalCleanup] ──► DROP TABLE  ────────────────────► DB   │
 │                           ──► Close connection  ─────────────► DB   │
 │                           ──► SqlConnection.ClearAllPools()          │
 └───────────────────────────────────────────────────────────────────────┘
```

---

## Table Patterns Used

| Pattern | Columns | Used By |
|---------|---------|---------|
| `Table7Columns()` | 7 columns (see below) | SqlBulkCopy (small), quick tests |
| `TableAll25Columns()` | All 25 standard SQL types (see below) | SqlCommand, DataTypeReader, CommandBehavior, StoredProc |
| `TableX25Columns(n)` | n × 25 columns (up to 1024) — repeating the 25-col set | SqlBulkCopy wide-table tests |
| Single-column table | 1 column of a specific type | DataTypeReader per-type benchmarks |
| Custom setup | JSON / VECTOR / encrypted (see below) | Json, Vector, AlwaysEncrypted runners |

---

## Exact Column Definitions (from datatypes.json + DDL generation)

The `Column.QueryString` property generates the DDL fragment via each `DataType.ToString()` override.
Sizes shown below are the **exact** DDL emitted per type.

### 25-Column Table (`TableAll25Columns` / `DataTypeReaderRunner` / `DataTypeReaderAsyncRunner`)

| # | Column Name | SQL DDL Type | Default Test Value | Notes |
|---|-------------|-------------|-------------------|-------|
| 1 | `c_bit` | `bit` | `true` | |
| 2 | `c_int` | `int` | `123456` | Index column |
| 3 | `c_tinyint` | `tinyint` | `123` | |
| 4 | `c_smallint` | `smallint` | `1234` | |
| 5 | `c_bigint` | `bigint` | `1234567890` | |
| 6 | `c_money` | `money` | `12334567.89` | |
| 7 | `c_smallmoney` | `smallmoney` | `123345.67` | |
| 8 | `c_decimal` | `decimal(10,4)` | `12345.6789` | Precision 10, Scale 4 |
| 9 | `c_numeric` | `numeric(10,4)` | `12345.6789` | Precision 10, Scale 4 |
| 10 | `c_float` | `float(10)` | `12345.6789` | Precision 10 |
| 11 | `c_real` | `real` | `12345.6789` | No size qualifier |
| 12 | `c_date` | `date` | `1970-01-01` | |
| 13 | `c_datetime` | `datetime` | `1970-01-01 00:00:00` | |
| 14 | `c_datetime2` | `datetime2` | `9999-12-31 23:59:59.9999999` | |
| 15 | `c_time` | `time` | `23:59:59.9999999` | |
| 16 | `c_smalldatetime` | `smalldatetime` | `1970-01-01 00:00:00` | |
| 17 | `c_datetimeoffset` | `datetimeoffset` | `1970-01-01 00:00:00 +00:00` | |
| 18 | `c_char` | `char` | `a` | SQL Server default = `char(1)` |
| 19 | `c_nchar` | `nchar` | `Ş` | SQL Server default = `nchar(1)` |
| 20 | `c_binary` | `binary(20)` | `0x0001e240` | Size = `DefaultValue.Length * 2` = 10 × 2 |
| 21 | `c_varchar` | `varchar(max)` | `abcde12345 .,!@#` | MaxLengthValueLengthType → `(max)` |
| 22 | `c_nvarchar` | `nvarchar(max)` | `abcde12345 .,!@ŞԛȽClient` | MaxLengthValueLengthType → `(max)` |
| 23 | `c_varbinary` | `varbinary(max)` | `0x0001e240` | MaxLengthValueLengthType → `(max)` |
| 24 | `c_uniqueidentifier` | `uniqueidentifier` | `6F9619FF-8B86-D011-B42D-00C04FC964FF` | |
| 25 | `c_xml` | `xml` | `<div><h1>Hello World</h1></div>` | |

### 7-Column Table (`Table7Columns`)

| # | Column Name | SQL DDL Type | Default Test Value |
|---|-------------|-------------|-------------------|
| 1 | `c_int` | `int` | `123456` |
| 2 | `c_char` | `char` | `a` |
| 3 | `c_nvarchar` | `nvarchar(max)` | `abcde12345 .,!@ŞԛȽClient` |
| 4 | `c_decimal` | `decimal(10,4)` | `12345.6789` |
| 5 | `c_uniqueidentifier` | `uniqueidentifier` | `6F9619FF-8B86-D011-B42D-...` |
| 6 | `c_xml` | `xml` | `<div><h1>Hello World</h1></div>` |

### LargeDataTypeRunner (custom CREATE TABLE)

| Column Name | SQL DDL Type | Data Size |
|-------------|-------------|-----------|
| `ID` | `INT PRIMARY KEY` | — |
| `VarcharMaxCol` | `VARCHAR(MAX)` | 1 / 2 / 5 MB (parameterized) |
| `NvarcharMaxCol` | `NVARCHAR(MAX)` | 1 / 2 / 5 MB |
| `VarbinaryMaxCol` | `VARBINARY(MAX)` | 1 / 2 / 5 MB |
| `XmlCol` | `XML` | 1 / 2 / 5 MB |

### JsonDataTypeRunner (custom CREATE TABLE)

| Column Name | SQL DDL Type | Test Value |
|-------------|-------------|------------|
| `Id` | `INT PRIMARY KEY` | 0 – (RowCount−1) |
| `JsonCol` | `JSON` | Generated object with JsonKeyCount key-value pairs |

**`[Params]`**:

| Parameter | Values | Description |
|-----------|--------|-------------|
| `RowCount` | 100, 1,000, 10,000, **100,000** | Number of rows inserted and read back |
| `JsonKeyCount` | 5, 50, 200 | Key-value pairs per JSON object (controls payload size) |

> Total benchmark combinations: 4 × 3 = **12** per method (sync + async = 24 runs).
> Example payload at `JsonKeyCount=5`: `{"key0":"value0","key1":"value1",...,"key4":"value4"}` (~73 bytes)
> Example payload at `JsonKeyCount=200`: ~3.6 KB per row.
> Requires SQL Server 2025+.

### VectorDataTypeRunner (custom CREATE TABLE)

| Column Name | SQL DDL Type | Test Value |
|-------------|-------------|------------|
| `Id` | `INT PRIMARY KEY` | 0 – (RowCount−1) |
| `VectorCol` | `VECTOR({Dimensions})` | Generated float array with Dimensions elements |

**`[Params]`**:

| Parameter | Values | Description |
|-----------|--------|-------------|
| `RowCount` | 100, 1,000, 10,000, **100,000** | Number of rows inserted and read back |
| `Dimensions` | 5, 50, 200 | Number of float dimensions in the vector |

> Total benchmark combinations: 4 × 3 = **12** per method (sync + async = 24 runs).
> Column DDL varies per combo: `VECTOR(5)`, `VECTOR(50)`, or `VECTOR(200)`.
> Example value at `Dimensions=5`: `[0.1, 0.2, 0.3, 0.4, 0.5]`
> Example value at `Dimensions=200`: 200-element float array (~1.2 KB per row).
> Requires SQL Server 2025+.

### PreparedStatementRunner (custom CREATE TABLE)

| Column Name | SQL DDL Type | Notes |
|-------------|-------------|-------|
| `Id` | `INT IDENTITY` | Auto-increment |
| `Name` | `VARCHAR(100)` | Fixed 100-char length |

### ParameterizedQueryRunner (custom CREATE TABLE)

| Column Name | SQL DDL Type | Notes |
|-------------|-------------|-------|
| `Id` | `INT NOT NULL` | |
| `Name` | `VARCHAR(100)` | Fixed 100-char length |

### AlwaysEncryptedRunner (custom CREATE TABLE)

| Column Name | SQL DDL Type | Notes |
|-------------|-------------|-------|
| `Id` | `INT PRIMARY KEY` | |
| `SecretValue` | `NVARCHAR(100)` | Encrypted when AE enabled |

---

## Diagnostics Collected Per Run

| Diagnoser | Metrics | Platform |
|-----------|---------|----------|
| **MemoryDiagnoser** | Gen0, Gen1, Gen2 collections; Allocated bytes | All |
| **ThreadingDiagnoser** | Completed work items; Lock contentions | All |
| **JsonExporter.Full** | Complete BDN result JSON for CI diffing | All |
| **EtwProfiler** | ETW traces (CPU sampling, GC events) | Windows only |

---

## Results Comparison Pipeline

```
  Baseline Run (merge build)          Current Run (PR build)
  ┌──────────────────────┐            ┌──────────────────────┐
  │  results/*.json      │            │  results/*.json      │
  └──────────┬───────────┘            └──────────┬───────────┘
             │                                   │
             └──────────────┬────────────────────┘
                            ▼
              ┌──────────────────────────────┐
              │ Compare-BenchmarkResults     │
              │ .py / .ps1                   │
              │ Threshold: ±10%              │
              └──────────────┬───────────────┘
                             │
                   ┌─────────┴─────────┐
                   ▼                   ▼
           ┌─────────────┐     ┌─────────────┐
           │ Regression  │     │ No change   │
           │ ⚠️ Flag PR  │     │ ✅ Pass     │
           └─────────────┘     └─────────────┘
```

---

## How to Run

```bash
# From repo root
cd src/Microsoft.Data.SqlClient/tests/PerformanceTests

# Edit runnerconfig.json — set ConnectionString, enable desired runners
# Then:
dotnet run -c Release
```

Results appear in `BenchmarkDotNet.Artifacts/results/`.

---

## Quick Stats

| Metric | Count |
|--------|-------|
| Total runner classes | **16** |
| Total benchmark methods | **~100+** |
| SQL data types covered | **27** (including JSON, Vector) |
| Parameterized dimensions | MARS, Pooling, Threads, Columns, BatchSize, DataSize, IsolationLevel, CommandBehavior |
| Diagnosers | 4 (Memory, Threading, JSON export, ETW) |
| Platforms | .NET 8+, .NET Framework 4.6.2 (BatchApi is .NET-only) |
