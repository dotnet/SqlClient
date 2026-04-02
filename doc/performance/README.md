# Performance Testing Infrastructure

## Overview

This directory contains the performance testing specification and documentation for the Microsoft.Data.SqlClient driver and the broader SQL Server driver family.

## Contents

| File | Description |
|------|-------------|
| [cross-driver-perf-spec.md](cross-driver-perf-spec.md) | Cross-driver performance test specification defining common benchmark scenarios for SqlClient, ODBC, JDBC, PHP, and Python drivers |
| [README.md](README.md) | This file |

## SqlClient Benchmark Suite

The SqlClient performance benchmarks are located at:

```
src/Microsoft.Data.SqlClient/tests/PerformanceTests/
```

### Running Benchmarks Locally

1. Create a SQL Server database for benchmarks (see [BUILDGUIDE.md](../../BUILDGUIDE.md#run-performance-tests))
2. Configure `runnerconfig.json` with your connection string
3. Run: `dotnet run -c Release -f net9.0`

### Benchmark Categories

- **Connection**: Open/close (pooled/non-pooled), concurrent pool access, MARS
- **Command Execution**: ExecuteReader, ExecuteScalar, ExecuteNonQuery, ExecuteXmlReader
- **Data Type Reads**: All SQL Server types (sync + async), large objects, JSON, Vector
- **Bulk Copy**: SqlBulkCopy with varying sources, batch sizes, column counts
- **Transactions**: Isolation levels, commit vs rollback, enlist overhead
- **Batch API**: SqlBatch vs individual commands
- **Prepared Statements**: Prepared vs non-prepared execution
- **Always Encrypted**: Encryption overhead (opt-in)

### CI Integration

- **PR builds**: Run connection + command benchmarks, compare against baseline
- **Merge builds**: Run full suite, publish baseline artifacts
- Pipeline definitions: `eng/pipelines/benchmarks/`

### Comparison Scripts

- `tools/scripts/Compare-BenchmarkResults.ps1` (PowerShell)
- `tools/scripts/Compare-BenchmarkResults.py` (Python)

Both scripts compare two BenchmarkDotNet JSON exports and produce a Markdown regression report.
