# ODBC Memory Benchmark

Measures native memory growth per connection using ODBC Driver 17/18 for SQL Server. Used to compare SChannel TLS 1.3 session ticket cache behavior against Microsoft.Data.SqlClient's `StrictEncryptMemoryBenchmark`.

## Prerequisites

- **ODBC Driver 18 for SQL Server** installed ([download](https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server))
- **SQL Server 2022+** with TDS 8.0 support (required for `Encrypt=Strict`)
- .NET 8.0 SDK

## Setup

Set environment variables:

```cmd
set BENCHMARK_SERVER=localhost
set BENCHMARK_DATABASE=master
set BENCHMARK_USER=sa
set BENCHMARK_PASSWORD=YourPassword
```

## Usage

```cmd
# Basic run — Strict encryption, 1000 connections, pooling enabled
dotnet run -c Release -- --encrypt Strict --connections 1000

# Without connection pooling (forces new TLS handshake per connection)
dotnet run -c Release -- --encrypt Strict --connections 1000 --no-pooling

# Mandatory encryption for comparison
dotnet run -c Release -- --encrypt Mandatory --connections 1000 --no-pooling

# Use ODBC Driver 17
dotnet run -c Release -- --encrypt Strict --driver "ODBC Driver 17 for SQL Server"

# Export results to CSV
dotnet run -c Release -- --encrypt Strict --connections 2000 --csv results.csv
```

## Command-Line Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `--connections N` | 5000 | Total connections to open/close |
| `--batch N` | 100 | Connections per measurement batch |
| `--encrypt MODE` | Strict | `Strict`, `Mandatory`, or `Optional` |
| `--driver NAME` | ODBC Driver 18 for SQL Server | ODBC driver name |
| `--no-pooling` | (pooling on) | Disable connection pooling |
| `--csv PATH` | (none) | Append results to CSV file |

## Interpreting Results

The key metric is **Per-Conn KB** — cumulative native memory growth per connection:

| Per-Conn KB | Interpretation |
|-------------|----------------|
| < 5 KB | Normal — no leak |
| 5–20 KB | Moderate — monitor with more connections |
| > 20 KB | Leak detected — consistent with SChannel TLS 1.3 session ticket caching |

## How Pooling Affects Results

With `--no-pooling`, each connection string is made unique by appending `APP=bench_N`, forcing the ODBC Driver Manager to create a new physical connection (and TLS handshake) every time.

Without `--no-pooling`, the Driver Manager reuses pooled connections, so only a few TLS handshakes occur regardless of connection count.

## Benchmark Results (May 2026)

| Driver | Encrypt | TLS | Pooling | Per-Conn KB |
|--------|---------|-----|---------|-------------|
| ODBC 18 | Strict | 1.3 | On | ~0.76 KB |
| ODBC 18 | Strict | 1.3 | Off | **~88.6 KB** |
| SqlClient Native SNI | Strict | 1.3 | On | ~1.17 KB |
| SqlClient Native SNI | Strict | 1.3 | Off | **~108 KB** |
| SqlClient Managed SNI | Strict | 1.3 | Off | flat |

Both ODBC and SqlClient native SNI leak equally without pooling — the issue is in Windows SChannel's TLS 1.3 session ticket cache, not driver-specific.

## Related

- [StrictEncryptMemoryBenchmark](../StrictEncryptMemoryBenchmark/) — SqlClient equivalent
- [ICM 792529661](https://portal.microsofticm.com/imp/v5/incidents/details/792529661/summary) — OOM incident
- [Investigation Summary](../../docs/icm-792529661-investigation-summary.md)
