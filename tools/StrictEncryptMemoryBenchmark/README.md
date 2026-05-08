# Strict Encrypt Memory Benchmark

Detects the **SChannel TLS session ticket cache leak** (native memory OOM) when using `Encrypt=Strict` with SQL Authentication. The leak causes ~88–108 KB of native heap per unique TLS 1.3 connection to accumulate unboundedly in SChannel's process-global session ticket cache.

Two modes:
1. **Long-running** (recommended) — simulates production workload: thousands of connections tracking private bytes growth over time
2. **BenchmarkDotNet** — per-operation allocation + native memory growth comparison (Strict vs Mandatory)

## Prerequisites

- .NET 8.0 SDK
- SQL Server 2022+ (TDS 8.0 / Strict mode requires SQL Server 2022 or later)
- SQL Authentication enabled (mixed mode)
- For Strict mode: a valid TLS certificate configured on SQL Server, with the signing CA trusted by the client

## Build

```bash
cd tools/StrictEncryptMemoryBenchmark
dotnet build -c Release
```

## Quick Start

```bash
# Set connection parameters (required)
set BENCHMARK_SERVER=localhost
set BENCHMARK_USER=sa
set BENCHMARK_PASSWORD=YourPassword123!
set BENCHMARK_DATABASE=master

# Optional: for Strict mode certificate validation
set BENCHMARK_HOSTNAME_IN_CERTIFICATE=localhost
set BENCHMARK_SERVER_CERTIFICATE=C:\certs\sqlserver\rootCA.pem

cd tools/StrictEncryptMemoryBenchmark

# Run long-running leak detector (recommended)
dotnet run -c Release -- --long-running --connections 5000 --batch 200 --encrypt Strict --no-pooling --hostname-in-certificate localhost

# Compare with Mandatory (should show no leak)
dotnet run -c Release -- --long-running --connections 5000 --batch 200 --encrypt Mandatory --no-pooling
```

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `BENCHMARK_SERVER` | Yes | SQL Server hostname or IP |
| `BENCHMARK_USER` | Yes | SQL Authentication username |
| `BENCHMARK_PASSWORD` | Yes | SQL Authentication password |
| `BENCHMARK_DATABASE` | No | Database name (default: `master`) |
| `BENCHMARK_SERVER_CERTIFICATE` | No | Path to CA certificate PEM file (for Strict mode validation) |
| `BENCHMARK_HOSTNAME_IN_CERTIFICATE` | No | Expected hostname in server certificate |

## Long-Running Mode (Recommended)

This reproduces the exact production scenario from [ICM 792529661]: open/close thousands of TLS connections, tracking native memory (private bytes) growth.

```bash
# Strict mode, no pooling (forces full TLS handshake each time)
dotnet run -c Release -- --long-running --connections 10000 --batch 200 --encrypt Strict --no-pooling --hostname-in-certificate localhost

# Compare with Mandatory to isolate TDS 8.0 path
dotnet run -c Release -- --long-running --connections 10000 --batch 200 --encrypt Mandatory --no-pooling

# Write CSV for version comparison
dotnet run -c Release -- --long-running --connections 10000 --encrypt Strict --no-pooling --csv results.csv
```

### Long-Running Options

| Option | Description | Default |
|--------|-------------|---------|
| `--connections <n>` | Total connections to open/close | `10000` |
| `--batch <n>` | Connections per measurement interval | `100` |
| `--encrypt <mode>` | `Strict`, `Mandatory`, or `Optional` | `Strict` |
| `--no-pooling` | Disable pooling (forces TLS handshake each time) | pooling enabled |
| `--hostname-in-certificate <host>` | Expected hostname in server cert | from env var |
| `--server-certificate <path>` | Path to CA certificate for Strict validation | from env var |
| `--managed-sni` | Use managed SNI (SslStream) instead of native SChannel | native SNI |
| `--csv <file>` | Append results to CSV for comparison | — |

### Output Columns

| Column | What It Measures | How to Read It |
|--------|-----------------|----------------|
| **Conns** | Total connections opened so far | Counter |
| **Private MB** | Total process private bytes (native + managed) | **Key metric** — if it keeps growing, there's a leak |
| **Δ Priv MB** | Change in private bytes since last batch | Steady positive = leak |
| **WS MB** | Working Set (physical RAM assigned to process) | Grows with Private MB |
| **Managed MB** | .NET GC heap size | Should oscillate (GC working) |
| **Δ Mgd MB** | Change in managed heap since last batch | Fluctuating = normal |
| **Per-Conn KB** | `(Current Private - Baseline) / Total Connections` | Native memory cost per connection |
| **Batch ms** | Time for the batch of connections | Performance reference |

### Interpreting Results

- **Per-Conn KB > 20**: Likely SChannel session ticket cache leak (~32-46 KB/ticket)
- **Per-Conn KB 5-20**: Suspicious, run at higher scale
- **Per-Conn KB < 5**: Normal
- **Managed stays flat but Private grows linearly**: Confirms native leak (SChannel)
- **Both Strict and Mandatory leak**: Issue is in general TLS teardown
- **Only Strict leaks**: Issue is specific to TDS 8.0 code path

### Expected Results (May 2026)

| Driver / Mode | Encrypt | TLS | Pooling | Per-Conn KB | Leak? |
|---------------|---------|-----|---------|-------------|-------|
| SqlClient Native SNI | Strict | 1.3 | Off | **~108 KB** | **YES** |
| SqlClient Native SNI | Strict | 1.3 | On | ~1.17 KB | No |
| SqlClient Managed SNI | Strict | 1.3 | Off | flat | No |
| SqlClient Native SNI | Mandatory | 1.2 | Off | ~4 KB | No |
| ODBC Driver 18 | Strict | 1.3 | Off | **~88.6 KB** | **YES** |
| ODBC Driver 18 | Strict | 1.3 | On | ~0.76 KB | No |

Both ODBC and SqlClient native SNI leak equally without pooling — the issue is in Windows SChannel's TLS 1.3 session ticket cache, not driver-specific. See [ODBC benchmark](../OdbcMemoryBenchmark/) for comparison.

## BenchmarkDotNet Mode

```bash
# All benchmarks
dotnet run -c Release

# Only stress tests (1000/5000 connections)
dotnet run -c Release -- --filter *Stress*

# Only per-connection tests
dotnet run -c Release -- --filter *ConnectionBenchmarks*
```

Reports per-invocation managed allocations AND native memory growth (custom diagnoser).

## Testing Different MDS/SNI Versions

Edit `.csproj` to change the `Microsoft.Data.SqlClient` package version:

```xml
<!-- NuGet version -->
<PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.1" />

<!-- Or test with a locally-built version (project reference) -->
<ProjectReference Include="..\..\src\Microsoft.Data.SqlClient\src\Microsoft.Data.SqlClient.csproj" />
```

Then rebuild and re-run. The tool prints the MDS assembly version at startup.

## Connection String Pattern (from ICM)

The benchmark matches the ICM connection pattern:
```
Data Source=server;Initial Catalog=db;Integrated Security=False;
User ID=cloudsa;Password=***;Connect Timeout=30;
Encrypt=Strict;TrustServerCertificate=False;Authentication=SqlPassword
```

> **Note:** `TrustServerCertificate=True` is NOT supported with `Encrypt=Strict`. The server certificate must be validated against a trusted CA.
