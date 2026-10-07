# Command and reader performance tests

The command/parser coverage lives in the existing benchmark units. Select a unit with
`PERF_BENCHMARK`; the performance pipeline continues to discover enabled units through
`PERF_LIST_BENCHMARKS`. There are no separate command-execution or smoke-test runners.

| Unit | Coverage |
| --- | --- |
| `SqlCommand` | ExecuteNonQuery, ExecuteScalar, ExecuteReader, and ExecuteXmlReader, sync and async; stable one-row UPDATE/SELECT/XML workloads; SQL text, parameterized SQL, and stored-procedure RPCs |
| `DataTypeReader` | Every individual type from `datatypes.json`, plus mixed 16/64-column rows; drain baseline, typed getters, and GetValues, sync and async; Default and SequentialAccess |
| `LargeDataRead` | Binary and Unicode text; full-value materialization, GetBytes/GetChars, and sync/async GetStream/GetTextReader; Default and SequentialAccess; 8 KiB and 1 MiB streaming buffers |
| `AlwaysEncryptedDataTypeReader` | Encryptable individual types; drain baseline, typed getters, and GetValues, sync and async; Default behavior |
| `AlwaysEncryptedLargeDataRead` | Binary materialization, sync and async; Default behavior |

**Every unit above automatically runs with MARS off and on.** The shared base declares
`[Params(false, true)]` and overrides the connection string accordingly. No MARS flag or second
manual run is needed. These cases measure MARS overhead with one active command; they do not
measure concurrent readers. Unsupported sequential/streaming modes are excluded from encrypted
variants, and mixed-row fixtures are plaintext only.

The mixed-row fixture repeats 16 types: bit, tinyint, smallint, int, bigint, decimal, real, float,
datetime2, datetimeoffset, time, uniqueidentifier, varchar, nvarchar, binary, and varbinary.
Every fourth row contains NULLs. Typed reads check for NULL and consume fields in ordinal order.
Async typed reads use synchronous getters after ReadAsync. Large-value async materialization uses
GetFieldValueAsync; async streaming uses ReadAsync on the stream or text reader.
Row counts, row shapes, and large-value byte sizes are parameters in the exported reports.
Typed accessors are resolved from actual CLR metadata during setup, including `float(n)` precision.
Chunked getters (`GetBytes`/`GetChars`) are synchronous APIs, not simulated async reads.

SequentialAccess is a command behavior; streaming is an accessor choice. They are independent
dimensions: streaming with Default can still buffer inside the driver. Text sizes and streaming
buffer sizes are expressed in UTF-16 bytes, so a text buffer has half as many characters as bytes.
The existing GetBytes-named method uses GetChars for text, and GetStream-named methods use
GetTextReader for text; the Kind parameter identifies which path was measured.

## Measurement boundary

Global setup opens a dedicated physical connection, populates fixtures, creates reusable commands,
and allocates reusable buffers. Plaintext large values are generated and stored server-side, with
DATALENGTH checked before timing. Encrypted values are generated client-side during setup.
Global cleanup drops tables, disposes encryption fixtures and commands, and closes the connection.
Setup failures also clean up acquired resources. Mixed-row and command fixtures use session-local
temporary tables/procedures; individual-type and large-data fixtures use unique table names.
The command fixture checks all eight API paths outside timing. Reader cleanup checks observed
row/value or byte counts, also outside timing, so a truncated transfer is not a successful case.

Timed invocations execute an existing command and fully consume its result. Per-execution readers
and streams are disposed within the invocation. Full-value allocations and accessor costs are
intentionally measured. NonQuery updates the same row to the same value, avoiding growing fixtures
or per-invocation reset work. No connection open/close, command construction, buffer allocation,
or fixture population is timed.

These are end-to-end SQL benchmarks: server execution, network transfer, XML parsing, and accessor
costs contribute to timings. Use the same server, transport, runtime, and configuration for
comparisons. Warmup warms fixtures and code; these are not cold-connection benchmarks.

**Baseline boundary:** the existing runner names are retained, but their measurements changed.
Commands/buffers formerly allocated in benchmark bodies now live in setup; DataTypeReader adds
materialization alongside its drain baseline; SqlCommand now uses focused one-row workloads.
Compare baseline and candidate drivers using the same updated benchmark sources. Historical
results from the old implementations are not directly comparable. No parser or driver API code
is changed by this suite consolidation. A source baseline must already contain the suite update
before it is compared with subsequent parser/API changes; the source-baseline pipeline otherwise
builds that older ref's own benchmark sources.

## Running and configuration

Build in Release and run from the output directory so `datatypes.json` can be found:

```powershell
dotnet build .\src\Microsoft.Data.SqlClient\tests\PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj -c Release -f net10.0
Set-Location .\src\Microsoft.Data.SqlClient\tests\PerformanceTests\bin\Release\net10.0
$env:RUNNER_CONFIG = (Resolve-Path .\runnerconfig.jsonc).Path
$env:PERF_BENCHMARK = 'DataTypeReader'
dotnet .\PerformanceTests.dll
```

Set ConnectionString to an existing test database. The account needs permission to create/drop
test tables and, for encrypted variants, column encryption/master keys. Encrypted fixtures also
use the existing certificate-store infrastructure. All five runners disable pooling so connection
and fixture lifetime match; other connection settings and the harness's AppContext switches remain
in effect.

The existing `SqlCommandRunnerConfig`, `DataTypeReaderRunnerConfig`, `LargeDataReadRunnerConfig`,
and corresponding AlwaysEncrypted entries remain the configuration points. Their shared defaults
are one launch, 20 iterations, one invocation, one warmup, 10,000 rows, and a 1,800-second SQL
command timeout. Only Enabled and desired overrides are needed, for example:

```json
"DataTypeReaderRunnerConfig": { "Enabled": true, "RowCount": 1000 }
```

Plaintext large-data defaults use five iterations, Monitoring, and a 120-minute case timeout,
including when a private configuration specifies only Enabled. RowCount does not apply to LOB
or command-entry-point fixtures.

Repetitions are controlled by LaunchCount/WarmupCount/IterationCount/InvocationCount. The fast
SqlCommand benchmarks additionally batch 512 sequential command executions per invocation to
reach BenchmarkDotNet's recommended iteration duration. OperationsPerInvoke normalizes reported
timings and allocations to one command execution. The checked-in large-data entries use
Monitoring and a longer TimeoutMinutes
for slow transfers. CommandTimeoutSeconds controls workload/setup commands and row bulk-copy
population; existing encryption-key fixture helpers retain their own timeouts. TimeoutMinutes
controls the entire BenchmarkDotNet case. Invalid configurations and failed benchmark cases
produce failures rather than silently changing the workload or reporting a successful empty run.

**Plaintext large-data defaults include 1/5/10/20 MiB and 128 MiB (134,217,728 bytes).**
The 128 MiB value runs by default across materialization, chunked getters, and streaming, for
binary and Unicode text under both command behaviors and both MARS settings. Encrypted defaults
retain 1/5/10/20 MiB. To select sizes, override `PayloadSizesBytes` in the existing large-data job:

```json
"LargeDataReadRunnerConfig": {
    "Enabled": true,
    "PayloadSizesBytes": [1048576, 134217728],
    "IterationCount": 5,
    "RunStrategy": "Monitoring",
    "TimeoutMinutes": 120
}
```

Sizes must be distinct, positive, even byte counts up to 1 GiB. An explicit `[1073741824]` override
can still exercise a 1 GiB column. Materialization and Default buffering need memory proportional
to the payload, with additional copies possible; generating/storing fixtures also needs server
memory and database space. Streaming with Default can still buffer the full value inside SqlClient.
Each plaintext size expands to 64 cases, plus warmup and diagnostic executions. These diagnostic
executions also transfer data. Use the same size list for baseline and candidate runs.

Default-mode binary chunk/stream access can be particularly expensive: the current buffered
`GetBytes` path obtains `GetSqlBinary(...).Value` for each chunk, which copies the complete value.
Small buffers therefore expose repeated full-value copying, not just network throughput. These
cases intentionally remain in the suite to make such regressions/improvements visible.

The harness forwards standard BenchmarkDotNet arguments, including `--filter`. For a focused
run of all materialization methods and the 1 MiB chunk/stream arguments:

```powershell
$env:PERF_BENCHMARK = 'LargeDataRead'
dotnet .\PerformanceTests.dll --filter '*GetFieldValue*' '*1048576*'
```

Filters match full benchmark names, including parameters. Select payload sizes in RUNNER_CONFIG;
the example's numeric filter matches 1 MiB payloads too if they are present. Omitting `--filter`
continues to run the entire enabled unit. Filtering a subset does not remove any default cases.

## Quick execution checks

Use the ordinary harness with a private RUNNER_CONFIG copy: set IterationCount and InvocationCount
to 1, WarmupCount to 0, and RunStrategy to Monitoring. For small data use RowCount 17 and
PayloadSizesBytes [65538, 131074], which exercise partial chunks. Select a unit using the usual
PERF_BENCHMARK selector and verify that each exported case has measurements. This checks setup,
execution, cleanup, and export on the actual performance-run path. Follow with a real 128 MiB
configuration to exercise the large-payload boundary. These short-run timings are not suitable
for performance comparisons.
