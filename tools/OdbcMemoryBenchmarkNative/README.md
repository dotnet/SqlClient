# ODBC Memory Benchmark (Native C++)

Native C++ reproduction app for the SChannel TLS 1.3 session ticket cache memory leak (ICM 792529661). This is the native equivalent of the .NET `OdbcMemoryBenchmark` tool, created per ODBC team's request to reproduce the issue without any .NET runtime overhead.

## Build

### Option 1: MSVC Developer Command Prompt

```cmd
cl /EHsc /O2 OdbcMemoryBenchmark.cpp /link odbc32.lib psapi.lib
```

### Option 2: CMake

```cmd
cmake -B build
cmake --build build --config Release
```

The output binary will be at `build\Release\OdbcMemoryBenchmark.exe`.

## Usage

Set required environment variables:

```cmd
set BENCHMARK_SERVER=your-server.database.windows.net
set BENCHMARK_USER=your-username
set BENCHMARK_PASSWORD=your-password
set BENCHMARK_DATABASE=master
```

Run with default settings (5000 connections, Strict encryption, pooling enabled):

```cmd
OdbcMemoryBenchmark.exe
```

### Command-Line Options

| Option | Default | Description |
|--------|---------|-------------|
| `--connections N` | 5000 | Total number of connections to open |
| `--batch N` | 100 | Connections per measurement batch |
| `--encrypt MODE` | Strict | Encryption mode: Strict, Mandatory, Optional |
| `--driver NAME` | "ODBC Driver 18 for SQL Server" | ODBC driver name |
| `--no-pooling` | (pooling on) | `SQL_CP_OFF` + unique `APP=` per iter — genuine no-pool (fresh handshake every time) |
| `--mimic-net-odbc` | (off) | `SQL_CP_ONE_PER_HENV` + unique `APP=` per iter — mirrors `System.Data.Odbc`'s behavior |
| `--unique-connstr` | (off) | `SQL_CP_ONE_PER_DRIVER` + unique `APP=` per iter — default pool mode with unique conn strings (isolation test) |
| `--csv PATH` | (none) | Append measurements to a CSV file |

### Examples

```cmd
REM Strict encryption (TDS 8.0) - reproduces the leak
OdbcMemoryBenchmark.exe --connections 10000 --encrypt Strict

REM Mandatory encryption (TDS 7.x + STARTTLS) - baseline comparison
OdbcMemoryBenchmark.exe --connections 10000 --encrypt Mandatory

REM Disable pooling to ensure fresh TLS handshake each time
OdbcMemoryBenchmark.exe --connections 5000 --encrypt Strict --no-pooling

REM Mirror System.Data.Odbc's pooling behavior to see if its growth
REM reproduces in native code (proves the growth is DM pool accumulation,
REM not a driver leak).
OdbcMemoryBenchmark.exe --connections 5000 --encrypt Strict --mimic-net-odbc

REM Use ODBC Driver 17 for comparison
OdbcMemoryBenchmark.exe --encrypt Mandatory --driver "ODBC Driver 17 for SQL Server"

REM Export results to CSV
OdbcMemoryBenchmark.exe --encrypt Strict --csv results.csv
```

## What It Does

1. Connects to the specified SQL Server using the ODBC driver
2. Opens and closes connections in batches, executing `SELECT 1` on each
3. Measures private bytes (native memory) after each batch
4. Reports per-connection memory growth to identify the SChannel leak

## Interpreting Results

- **>20 KB/connection**: Leak detected — consistent with SChannel TLS 1.3 session ticket caching
- **5-20 KB/connection**: Moderate growth — may be caching, test with more connections
- **<5 KB/connection**: Minimal growth — no significant native memory leak

## Expected Behavior

With `Encrypt=Strict` (TDS 8.0, TLS 1.3), you should observe unbounded native memory growth because SChannel caches TLS 1.3 session tickets without eviction. With `Encrypt=Mandatory` (TDS 7.x, TLS 1.2), memory should remain stable.

## Pooling-mode comparison

| Mode | DM pooling setting | Conn string | Expected behaviour |
|---|---|---|---|
| default | `SQL_CP_ONE_PER_DRIVER` | same | DM pool hits after first — minimal native growth |
| `--unique-connstr` | `SQL_CP_ONE_PER_DRIVER` | unique per iter | Same DM pool mode as default but forces a new pool bucket per iter — isolation test for the "DM pool + unique conn str = growth" hypothesis |
| `--no-pooling` | `SQL_CP_OFF` | unique per iter | Fresh handshake every iter — driver-bug leaks visible |
| `--mimic-net-odbc` | `SQL_CP_ONE_PER_HENV` | unique per iter | DM pool accumulates one entry per unique conn string — mirrors `System.Data.Odbc`'s benchmark behaviour |

### Why `--mimic-net-odbc` exists

`System.Data.Odbc`'s [`OdbcEnvironmentHandle` constructor](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Data.Odbc/src/System/Data/Odbc/OdbcEnvironmentHandle.cs) **always** calls `SQLSetEnvAttr(SQL_ATTR_CONNECTION_POOLING, SQL_CP_ONE_PER_HENV)` — there is no off switch. When the .NET ODBC benchmark uses `Pooling=false` + unique `APP=` per iteration, the ADO.NET-level pool is bypassed but the underlying DM pool **still pools** each unique conn string into its own permanent entry. Each entry retains its own SChannel context, certificate chain, and key material.

The `--mimic-net-odbc` flag replicates this exact setup in native code so you can confirm whether the .NET ODBC benchmark's per-iter growth is:
- a real driver leak (native repro should also leak with this flag), OR
- benign DM pool accumulation (native repro grows with this flag, but at the same rate as .NET — not a bug).
