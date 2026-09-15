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
| `--max-concurrent-opens` | 0 (no external cap), or 1-4096 for `--pool disabled --workload open-close` only |
| `--drain` | 20 seconds after admission stops |
| `--startup-timeout` | 60 seconds to prepare workers and finish preflight |
| `--deadline` | 120 seconds from child launch, including startup and drain |
| `--cleanup-grace` | 5 seconds for cooperative shutdown before forced termination |
| `--cooldown` | 2 seconds after child exit |
| `--xevents` | Opt into server-side `sqlserver.process_login_finish` timing; disabled by default |
| `--xevent-event` | `process_login_finish` (default), or `login` for counts without durations |
| `--xevent-connection-env` | Optional observer credential variable; defaults to `--connection-env` |
| `--xevent-timeout` | Observer connect/command timeout, 5 seconds, range 1-60 |
| `--cache-authentication` | Opt-in process-local token cache for Active Directory Default |
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

### Controlled non-pooled open admission

Use `--max-concurrent-opens` to compare callers with the same external limit on
outstanding `Open` / `OpenAsync` invocations. A single per-sample semaphore guards
only connection establishment, not creation or disposal. Synchronous callers
wait synchronously on their existing threads. Async callers await the same gate.
No per-open `Task.Run`, driver changes, reflection into driver state, or processor
count overrides are used.

Zero preserves the uncapped path. Nonzero caps require exactly `--pool disabled`
and `--workload open-close`; pooled modes, `--pool all`, cold ramps, and
`--workload both` are rejected. A cap above worker count N is allowed and has
effective capacity N.

For example, if the child reports `ProcessorCount = 10`, run these sequentially
with separate output files, then repeat with `--max-concurrent-opens 0` for both
uncapped controls:

```sh
dotnet "$harness" --pool disabled --workload open-close --profile default \
  --caller sync-dedicated --start 32 --max 32 --repetitions 1 \
  --max-concurrent-opens 10 --output ramp-capped-sync.jsonl
dotnet "$harness" --pool disabled --workload open-close --profile default \
  --caller async --start 32 --max 32 --repetitions 1 \
  --max-concurrent-opens 10 --output ramp-capped-async.jsonl
```

Keep N, profile, authentication, connection settings, and duration identical.
Repeat at other N values, such as 128. Matching the external cap to the recorded
`Environment.ProcessorCount` tests whether equalizing admission reduces the
sync/async throughput difference. It does not directly observe the driver's
internal non-pooled pending-open slots or establish them as the only cause.

Each sample result adds:

- `ProcessorCount`: the child's actual `Environment.ProcessorCount`.
- `ConfiguredMaxConcurrentOpens` and `EffectiveMaxConcurrentOpens`: zero means
  no external cap, otherwise the effective value is `min(configured, N)`.
- `ActualPeakActiveOpens`: peak outstanding driver invocations inside the gate,
  including admitted calls that finish during drain. This measures API calls,
  not internal sockets or server logins.
- `OpenGateWait`: bounded histogram across measurement and drain, including
  successful, timed-out, and canceled gate waits. It is empty when uncapped.
- `DriverOpenLatency` and `FailedDriverOpenLatency`: separate histograms of
  successful and failed driver invocations across measurement and drain. These
  exclude gate wait, creation, and disposal and are recorded even when uncapped.
  Gate timeouts and canceled gate waits are not driver invocations.
- `CanceledOpenGateWaits`: queued admissions stopped without invoking the driver.

Existing successful and failed Open latency includes gate wait. Outstanding-call
counts include gate waiters; `ActualPeakActiveOpens` excludes them. Attempts that
stop at the gate count as attempts and canceled waits, not failed driver opens.

Each gate wait is bounded by `--connect-timeout` and emits `TimeoutException` on
expiry while measurement is still admitting. The driver retains its own existing
connect timeout after admission: these are **sequential wait + open budgets**,
not one shared deadline. Measurement stop cancels queued gate waits without
creating failures. Already admitted driver calls can finish during bounded drain
and are canceled only under the existing drain/shutdown policy. Driver failures,
including driver cancellation exceptions, remain failures and release permits.

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

## Server-side login timing with Extended Events

Add `--xevents` to capture server login time alongside client `Open()` latency.
For the focused eight-thread experiment, use **dedicated synchronous workers**,
not the ThreadPool caller. Each worker keeps the same real thread for its entire
open/close loop. The observer runs in the parent process on a separate thread.

```sh
dotnet "$harness" --pool disabled --caller sync-dedicated --profile default --workload open-close --start 8 --max 8 --repetitions 1 --duration 30 --xevents --output ramp-eight-xevents.jsonl
```

On Windows PowerShell, after supplying `SQLCLIENT_RAMP_CONNECTION` securely:

```powershell
$harness = "src/Microsoft.Data.SqlClient/tests/ConnectionPoolRampStress/bin/Release/net8.0/ConnectionPoolRampStress.dll"
dotnet $harness --pool disabled --caller sync-dedicated --profile default --workload open-close --start 8 --max 8 --repetitions 1 --duration 30 --xevents --output ramp-eight-xevents.jsonl
```

The observer needs permission to create, start, read, and drop an Extended Events
session, including the relevant XE DMVs. The collector selects **database scope**
for Azure SQL Database and **server scope** for SQL Server and Azure SQL Managed
Instance. `XEvents.Scope` records this choice. Permission names vary by platform
and version; SQL Server 2022+ has granular event-session permissions and
`VIEW SERVER PERFORMANCE STATE`. Use an authorized test instance, not production.
The selected event and fields are discovered before admitting workload. Duration
mode requires `sqlserver.process_login_finish`, `total_time_ms`, and `is_success`.
Count mode requires `sqlserver.login` and `is_cached`. Missing capabilities fail
setup explicitly. No other event is silently substituted.
Database-scoped session support does not imply that Azure SQL Database exposes
`process_login_finish`. If the event is unavailable, omit `--xevents` for a
client-only run and report server-side login duration as unavailable, not zero.

Normally the observer uses the workload connection credentials. To use a separate
authorized account on the **same SQL Server instance**, supply its connection
string securely in another environment variable and select it with
`--xevent-connection-env SQLCLIENT_RAMP_OBSERVER`. This named observer variable is
not forwarded to workload children. Neither connection string is written to
results. Authentication, transport, and encryption of the workload remain
unchanged.
For Azure SQL Database, the observer must also connect to the workload database.

### Azure SQL and cached authentication

The harness references the repository's Azure authentication extension. For
`Authentication=Active Directory Default`, sign in with `az login` before starting
the harness. DefaultAzureCredential does not open an interactive browser itself.
CLI credential retrieval can still invoke a process for every token request,
even after sign-in.

Use `--cache-authentication` to isolate that retrieval overhead from login
measurements. Each process wraps the default provider in a single-flight,
memory-only token cache, keyed by authentication identity and destination. The
existing non-pooled preflight warms authentication, not the measured connection
pool. Failures are not cached. Workload and observer connections must both use
Active Directory Default when this option is enabled.

Tokens refresh within five minutes of expiry, so long samples can still acquire
tokens during measurement. Each sample records
`AuthenticationAcquisitionsBeforeWorkload` and
`AuthenticationAcquisitionsDuringWorkload`. The latter includes drain and should
be zero when interpreting a sample as excluding token retrieval.

For databases that expose `sqlserver.login` but not `process_login_finish`,
explicitly select counts rather than durations:

```sh
dotnet "$harness" --pool v2 --caller sync-dedicated --profile default \
  --workload open-close --start 1 --max 512 --repetitions 1 --duration 30 \
  --connect-timeout 30 --drain 35 --deadline 140 --cache-authentication \
  --xevents --xevent-event login --xevent-timeout 30 --output azure-v2.jsonl
```

`login` exposes `is_cached`, distinguishing new connections from cached
connection logins. It does not expose login duration. These event counts are not
pooled client open/close cycle counts: a pooled open/close loop without commands
may not send a reset request to the server. Client Open latency remains available.
Count results have `DurationAvailable=false` and populate `Counts` rather than
duration observations. Do not interpret the empty duration histograms as zeros.
Resume a paused serverless database before measuring and record its service tier.

The harness creates one uniquely named session per sample with `STARTUP_STATE=OFF`
and a bounded, in-memory `ring_buffer` target retaining at most 128 full events.
Polling every second consumes and deduplicates events before they are evicted.
This keeps large login payloads from accumulating past the target's XML limit.
Bursts that exceed this capacity between polls are reported as incomplete capture.
A generated application name filters
the sample's logins. Observer and preflight connections use different names.
Polling aggregates event timestamps and `total_time_ms`, then discards raw XML.
No event files, query text, usernames, or server names are written to results.
The session uses lossy event retention rather than blocking SQL Server if capture
cannot keep up. Instrumentation still adds overhead, so compare instrumented runs
with other instrumented runs.

Each `sample-end.Result.XEvents` contains:

* `Status`, event counts, and successful/failed server-login distributions with
  count, mean, p50, p95, p99, and maximum in **milliseconds**.
* Per-second `Intervals` using **server event timestamps**, not client stopwatch
  time. Client/server clock synchronization is needed for cross-machine alignment.
* Capture loss, truncation, invalid-event indicators, and cleanup status.

These aggregates cover the sample's complete captured lifetime, **including
drain**, unlike the client's measurement-only throughput. They are not per-Open
correlations. Pooled reuse can complete many client opens without new physical
login events, and some client failures occur before the server emits this event.
Server event timing is not end-to-end client latency. Percentiles use the same
bounded logarithmic histograms as other harness metrics, not exact sorted samples.

Empty, lossy, malformed, or failed capture is reported explicitly rather than
as zero-millisecond latency. A capture problem stops the run and produces a
nonzero exit code (2 for setup, otherwise 4), while retaining the workload's own
outcome and any collected data. It is **not a workload saturation boundary**.

The parent reads the target before stopping it, then drops the session even when
a workload child times out or is killed. Observer setup/collection/cleanup are
outside the child's measurement/deadline and have bounded individual SQL calls
controlled by `--xevent-timeout`. Under high load, observer queries can also time out. Increasing
`--xevent-timeout` (for example, to 30 seconds) leaves the workload's
`--connect-timeout` unchanged. If the parent itself is killed, the server
disconnects, or permissions prevent cleanup, a session can remain. The result
includes the generated `SessionName` and `SessionDropped`; the `xevent-start`
record also saves the session name before creation, in case the parent exits
without a final result. Remove only that session on the same instance if cleanup
is incomplete.

### Live capture checks

Offline capture tests run with the harness test project. For the opt-in live
checks, securely supply `SQLCLIENT_RAMP_CONNECTION`, set
`SQLCLIENT_RAMP_XEVENT_TESTS=1`, and run:

```sh
dotnet test src/Microsoft.Data.SqlClient/tests/ConnectionPoolRampStress.Tests/ConnectionPoolRampStress.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~LiveXEventTests" --blame-hang-timeout 3m
```

These short samples exercise dedicated synchronous and asynchronous callers,
both pools and the non-pooled control, isolation from preflight/observer logins,
and session cleanup after forced child termination. They require the event and
permissions described above and fail rather than skip if capture is unavailable.

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
