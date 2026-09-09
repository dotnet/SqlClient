# Connection pool ramp stress

Standalone, sustained connection-opening experiments for legacy pooling, V2
pooling, and a non-pooled control. This does not change `SqlClient.Stress` or the
existing BenchmarkDotNet `ConnectionPoolRampRunner`. Those exercise mixed
correctness workloads and repeatable fixed-size microbenchmarks, respectively.

## Build and configure

From the repository root:

```sh
dotnet build src/Microsoft.Data.SqlClient/tests/ConnectionPoolRampStress/ConnectionPoolRampStress.csproj -c Release
dotnet test src/Microsoft.Data.SqlClient/tests/ConnectionPoolRampStress.Tests/ConnectionPoolRampStress.Tests.csproj -c Release --blame-hang-timeout 10m
```

The projects target net8.0, net9.0, and net10.0. net10.0 tests use the driver's
compatible net9.0 target. Project references are the default. To use a packaged
driver, build with `-p:ReferenceType=Package`. Package restore uses the repository's
`NuGet.config`, including its local and governed SqlClient feeds and source mapping.
Versions come from central package management. To select a family version, use
the standard `-p:SqlClientPackageVersion=<version>` property.
A V2 sample rejects a driver without the V2 pool implementation.

Provide a connection string through `SQLCLIENT_RAMP_CONNECTION`, using your
terminal's secret-input facility or an existing secret manager. Do not put it on
the command line, in this README, or in a result file. The harness supports a
different variable name through `--connection-env`. It performs a separate,
non-pooled connectivity preflight before every sample. It does not create a
database, execute measured queries, provision SQL Server, or start containers.

The following shell variable contains only the executable path:

```sh
harness=src/Microsoft.Data.SqlClient/tests/ConnectionPoolRampStress/bin/Release/net8.0/ConnectionPoolRampStress.dll
dotnet "$harness" --help
dotnet "$harness" --list
```

## Run comparisons

Full matrix, with paired legacy/V2 ordering and the non-pooled control:

```sh
dotnet "$harness" --output ramp-full.jsonl
```

Defaults: concurrency 1, 2, 4, ..., 512, three repetitions, both workloads, all
three callers, both ThreadPool profiles, 30-second measurement windows, and
one-second reporting. This is **1,080 samples and at least nine hours** before
startup, drain, and cooldown. Inspect with `--list` first.

One configuration at 512 workers for 30 seconds:

```sh
dotnet "$harness" --start 512 --max 512 --repetitions 1 \
  --pool legacy --caller sync-threadpool --profile default \
  --workload open-close --duration 30 --output ramp-single.jsonl
```

Paired comparison at the same level, including the non-pooled control:

```sh
dotnet "$harness" --start 512 --max 512 --repetitions 3 \
  --pool all --caller sync-threadpool --profile both \
  --workload both --duration 30 --output ramp-paired.jsonl
```

Use a new output path per invocation. Existing files are never overwritten.

| Option | Values / default |
|---|---|
| `--pool` | `legacy`, `v2`, `disabled`, `all` (default) |
| `--caller` | `async`, `sync-threadpool`, `sync-dedicated`, `all` (default) |
| `--workload` | `open-close`, `cold-ramps`, `both` (default) |
| `--profile` | `default`, `provisioned`, `both` (default) |
| `--worker-minimum` | Explicit provisioned minimum, default 1024 |
| `--start`, `--max`, `--growth` | 1, 512, 2. Includes a non-geometric maximum once |
| `--repetitions` | 3 |
| `--duration`, `--interval` | 30 and 1 seconds |
| `--connect-timeout` | 15 seconds, finite and positive |
| `--drain` | 20 seconds after admission stops |
| `--startup-timeout` | 60 seconds to prepare workers and finish preflight |
| `--deadline` | 120 seconds from child launch, including startup and drain |
| `--cleanup-grace` | 5 seconds for cooperative shutdown before forced termination |
| `--cooldown` | 2 seconds after child exit |
| `--list`, `--dry-run` | No credentials, connections, or child processes |

The deadline must cover startup + measurement + drain. Cleanup grace follows an
external deadline. Sweep concurrency is bounded at 4096 and retained intervals
at 3600. Small windows are useful for smoke tests, not capacity conclusions.

## Workloads and callers

**Open/close**: N persistent workers repeatedly create their own connection,
open it, and close/dispose it. There are no queries, holds, retries, per-open
`Task.Run` calls, delays, pool clears, or per-iteration barriers. A cycle counts
only after successful opening and disposal. Pooling warms naturally after the
initial ramp. These are **pooled reuse cycles**, not physical connections/second.

**Cold ramps**: N persistent workers each attempt one open per round. Successful
connections stay open until every attempt settles. The coordinator closes them
all and clears only this sample's pool before releasing the next round.
Asynchronous completion signals, rather than blocking per-worker barriers, let
synchronous ThreadPool callers release their threads while retaining connections.
Between-round disposal and clearing belong to sustained round throughput and
are also reported separately. No pool is cleared while an attempt remains active.

Async callers await `OpenAsync`. Sync ThreadPool callers deliberately block
ThreadPool threads in `Open`. Dedicated callers use one persistent background
thread each. Every worker has at most one outstanding open. Worker readiness
never waits for all ThreadPool workers to be injected. Dedicated-thread readiness
is bounded. Worker-start delay measures gate-to-worker scheduling separately from
Open latency. In cold ramps it includes each round's worker dispatch.

The main child thread coordinates rounds, samples counters, and enforces
admission/drain deadlines. A separate thread receives supervisor stop requests.
Neither depends on ThreadPool timer callbacks to notice saturation. `OpenAsync`
can still do blocking physical connection work on ThreadPool threads internally.

The default profile leaves runtime ThreadPool settings unchanged. Provisioned
sets exactly the requested worker minimum, validates the return value, and reads
the settings back. It never raises the maximum. Profile settings are recorded,
not treated as interchangeable workloads.

## Isolation, failure, and safety

Every complete timed sample is a new process. The V2 AppContext switch is set
before the first connection, including preflight. This avoids cached switch/pool
state leaking between modes. Legacy/V2 ordering alternates by repetition and
the disabled control follows them. Within a sample, workers and runtime state
persist. Pooling is normalized to max size N and min size zero. Connection
retries and ambient enlistment are disabled. Pool blocking policy, encryption,
certificate trust, and SNI choice are recorded without server names or credentials.

The measurement window controls admission, not cancellation. Already admitted
operations and a cold round can finish during bounded drain. Their completions
are recorded in drain metrics, not measurement throughput. A failed operation
stops new admission immediately, drains admitted work, and records safe failure
groups. Ordinary window expiry is success. Failed attempts, setup failures, and
incomplete drain/deadline outcomes are distinct.

At drain expiry, cancellation runs on a dedicated thread because callbacks can
block. Settled cold connections are disposed without clearing an active pool.
Late-settling workers also dispose held connections. Uninterruptible calls are
contained by the sample process. The supervisor drains both output streams,
requests shutdown, kills only that child process tree if necessary, and reaps it
before continuing. No next sample starts if reaping cannot be confirmed.

A failed/incomplete sample stops escalation and further repetitions for its
workload/pool/caller/profile combination. Earlier repetitions remain recorded.
Other combinations continue. Setup failures stop the run because they are not
evidence of saturation. Exit codes: 0 success, 2 setup, 3 failed operations,
4 incomplete. Timeouts remain `timeout-unclassified` unless stronger evidence
exists. A timeout alone does not establish pool exhaustion or server saturation.

Credentials are inherited only through the environment. Arbitrary exceptions,
stderr, unrecognized stdout, oversized lines, and parser diagnostics are never
forwarded. Errors contain only allowlisted types, categories, and primary SQL
error numbers. Connection strings, arbitrary exception messages, environment
names/values, and output paths are excluded from the result schema.

## Read the results

Output is **JSON Lines**: one valid JSON object per line, flushed incrementally.
Record kinds are `run`, `sample-start`, `progress`, `sample-end`, `boundary`, and
`run-end`. A progress packet carries `ready` metadata, an interval, a result, or
a setup failure. Each record is associated with its exact sample. UTC startup
timestamps and monotonic interval offsets allow alignment with external traces.
Abrupt termination retains already flushed intervals. A missing final result
does not mean that a sample succeeded.
The supervisor's `sample-end` outcome takes precedence over a late child result.
Intervals also carry cumulative sanitized failure groups. Do not sum those
cumulative counts across intervals.

Metrics separate:

* Attempt admissions, successful opens, failed-open latencies, and completed
  open/close cycles.
* Gate-to-completion, worker scheduling delay, and Open-call latency.
* The initial ramp's settled attempts, successes, completion flag, duration,
  and successes divided by gate-to-last-completion duration.
* Cold-ramp Open duration (`Ramps`), full round duration through close/clear
  (`Rounds`), successful opens per summed ramp duration, sustained rounds/second,
  and separate close/clear distributions and total time.
* Measurement and drain counts and distributions. Completions are assigned to
  intervals when recorded under the worker's aggregation lock. This adds a small
  bookkeeping delay, rather than backdating late records into published intervals.
* Started workers, active workers, peak outstanding API calls, and sampled
  physical connection counters. **Outstanding API calls are not physical logins.**

Histograms use 320 logarithmic buckets, a 1-microsecond floor, and 8% bucket
spacing. Percentiles use nearest rank and bucket upper bounds capped by observed
maximum. Max is exact. Empty percentiles are null. Low-sample p95/p99 values are
descriptive only. Each worker retains measured/drain totals and one current
interval, not per-attempt samples. Interval merging occurs at rollover/reporting,
not through one shared hot-loop lock.

Zero-completion intervals remain visible. The first interval is marked startup.
Shortened intervals and drain intervals are explicit. Early/late trends compare
up to three complete non-startup, non-drain intervals at each end. They show mean
throughput and **mean interval p95**, not a pooled percentile across the group.
Either group having no opens reports `insufficient-data`. A slow cold round may
provide too little information for a trend. A trend is not a regression verdict.

Resource observations include cumulative process CPU seconds, working set,
managed bytes, GC counts, available ThreadPool workers/I/O threads, ThreadPool
thread count, and queued work. SqlClient EventCounters report active physical
connections and the last hard-connect increment with its sampling interval.
These counters may lag or be unavailable during ThreadPool starvation. Sampling
cannot establish peak physical login concurrency. Preflight can appear in the
first physical-counter sample. Instrumentation overhead can affect small/fast
workloads.

Driver version/commit (when embedded), runtime, OS/version/architecture,
processor count, ThreadPool settings, and numeric server version are recorded.
Server resource observations and container limits are explicitly unavailable
inside this client-only harness. Record them externally alongside the JSONL,
including emulation, CPU/memory limits, concurrent server activity, and host
contention. Do not mix runs with different environments.

Boundaries report the highest level with all requested repetitions successful
and the first non-successful level. Reaching 512 reports **“no failure observed
through 512”**, not “capacity is 512.” A single failure may be transient.
Non-pooled results help identify limits independent of pool coordination but
cannot alone prove that SQL Server is the bottleneck. Clearing a pool does not
guarantee immediate server-side teardown. A local container and client share
host resources, so their results are not a universal product ceiling.

## Validation scope

The sibling xUnit project uses fake connections and the executable's private
fixture-child modes to test success, failure, hanging children, partial output,
bounded metrics, round lifetimes, admission/drain, and output redaction.
It does not run SQL saturation workloads. Before interpreting a full comparison,
validate against a real server that pooled cycles reuse sessions and cold rounds
create distinct held sessions, and check failure/deadline cleanup externally.

### Opt-in live contract test

`LiveDatabaseTests.WorkloadRunnerPreservesPhysicalSessionContract` skips unless
`SQLCLIENT_RAMP_CONNECTION` is supplied through the environment. It drives the
actual runner with all three caller modes, one continuous worker and three cold
workers. It observes `ClientConnectionId` without queries, checks held sessions
and between-round clears, and exercises late async disposal after incomplete drain.
This is a small correctness check, not a saturation or server-teardown test.

Run each pool mode in a separate test invocation because SqlClient caches the
AppContext switch. Select exactly `legacy`, `v2`, or `disabled`:

```sh
SQLCLIENT_RAMP_POOL=legacy dotnet test src/Microsoft.Data.SqlClient/tests/ConnectionPoolRampStress.Tests/ConnectionPoolRampStress.Tests.csproj \
  -c Release -f net8.0 --filter FullyQualifiedName~LiveDatabaseTests --blame-hang-timeout 30s
```

Repeat with `SQLCLIENT_RAMP_POOL=v2` and `SQLCLIENT_RAMP_POOL=disabled`. Keep this
filter so unrelated tests cannot initialize SqlClient before the selected switch.
The test has a 30-second timeout and reports sanitized failure descriptors.
