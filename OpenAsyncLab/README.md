# OpenAsyncLab

A standalone, live terminal playground for `SqlConnection.OpenAsync`. All tool-specific code
and tests are in this top-level folder. It references the current SqlClient source project,
not a released package, and does not change the BenchmarkDotNet workloads.

**Do not run its SQL workload on the same endpoint as a performance ramp.** Network shaping
affects all clients of that local SQL endpoint; competing CPU/SQL activity also changes results.
The app starts paused. Use the explicitly labeled demo while another experiment is running.

## Run

Requires the repository's .NET 10 SDK. From the repository root:

```powershell
dotnet run --project OpenAsyncLab\src -c Release -- --demo
```

In Linux/WSL, with a local SQL Server on TCP/1433:

```bash
dotnet run --project OpenAsyncLab/src -c Release -- --netem
```

If `dotnet` was installed in `/opt/dotnet`, use `/opt/dotnet/dotnet` instead. Netem requires
`python3`, `ip`, `tc`, kernel netem/flower support, and root or noninteractive `sudo -n tc`.
Without `--netem`, connection and ThreadPool controls work on Windows/Linux, but latency is
unmanaged: zero in the controls does **not** prove the host has no external shaping.

Select the implementation at startup with `--pool-v1` (WaitHandleDbConnectionPool) or
`--pool-v2` (ChannelDbConnectionPool). These flags override the runtime configuration before
SqlClient initializes; without either flag, the existing AppContext setting applies (default V1).
The live header shows the selected version and marks it inactive while pooling is disabled.
Changing versions requires restarting the process; `P` only enables/disables pooling.

For two WSL terminals comparing the implementations against the same local SQL endpoint:

```bash
dotnet OpenAsyncLab/src/bin/Release/net10.0/OpenAsyncLab.dll --pool-v1 --netem
dotnet OpenAsyncLab/src/bin/Release/net10.0/OpenAsyncLab.dll --pool-v2
```

Run each command in its own terminal. Only V1 owns the netem queue and controls delay;
both processes experience that delay. V2 labels delay as unmanaged and cannot change it.
Keep V1 open (it can be paused) to retain shaping; quitting V1 removes delay for both.
Each process has its own connection pools and ThreadPool. Simultaneous workloads share
SQL Server and host resources, so run one workload at a time for less interference.

### Experimental V2 scheduling in this worktree

Press **L** in the V2 window to toggle **ThreadPool** (default) / **LongRunning** dispatch.
The selected mode appears above the controls. The experimental process-wide AppContext switch
`Switch.Microsoft.Data.SqlClient.UseLongRunningPoolV2Open` is intentionally read on every
slow-path dispatch rather than cached. The TUI initializes its selection from that switch.
Changing it does not restart workers or clear connections; already-dispatched work keeps its
original scheduling. V1 and non-pooled opens are unaffected, and demo mode only simulates the toggle.

Dispatch uses `Task.Factory.StartNew` with `DenyChildAttach` and `TaskScheduler.Default`,
unwrapping the async delegate. Enabling `LongRunning` moves initial synchronous physical
connection creation to a dedicated thread. It does not make creation asynchronous: after an
incomplete await, a continuation can still perform creation on the ThreadPool. Every dispatched
slow-path acquisition can create a dedicated thread, even if it then only awaits an available
pooled connection. Watch **process threads** as well as ThreadPool threads; lower ThreadPool
usage may just move the cost.

For a cold-pool comparison, pause and wait for workers to drain, select the dispatch mode with
L, clear with C, then start with Space. Warm inline checkouts bypass dispatch entirely.
History marks mode changes with `|`. This is an experimental driver change, not a production
recommendation.

Set `SQLCLIENT_TUI_CONNECTION_STRING` directly in your terminal, or let the app prompt for the
host, SQL user, and password. **Do not put credentials in command arguments, source, or files.**
The password prompt does not echo. The app removes this environment variable from its child
process environment. The default prompted connection uses `master`, encrypted TCP, and
`TrustServerCertificate=true` for a local development instance; provide a validated connection
string through the environment for a server requiring certificate validation.

## Controls

| Key | Action |
|---|---|
| Up / Down | Select workers, SQL pool maximum, ThreadPool worker minimum/maximum, or delay |
| Enter | Enter a numeric value; Enter again applies it |
| + / - | Adjust the selected value (delay steps are 5 ms) |
| Space | Start / pause the SQL workload |
| P | Switch between pooled and non-pooled `OpenAsync` |
| C | Clear only this TUI's current connection pool |
| L | Toggle V2 slow-path dispatch between ThreadPool and LongRunning |
| W | Switch between Open/close and Open/query/close workloads |
| Q | Shortcut for the same workload switch |
| Tab | Next metric page on shorter terminals |
| Esc | Cancel editing, or quit |
| Ctrl+C | Quit and clean up |

Workers are **concurrent async loops, not dedicated OS threads**. Each loop opens a connection,
executes `SELECT 1` in the query workload, disposes the connection, and yields so synchronously completed
pooled opens cannot monopolize a worker forever. The UI and probe producer run independently
of ThreadPool continuations, so pool starvation does not prevent raising its limits or quitting.
ThreadPool changes affect worker threads only; I/O completion thread limits are preserved.

Pool changes create a new pool key and clear only this app's old pool. Borrowed connections and
old workers drain asynchronously, so the active count can temporarily exceed the new target.
Live changes can affect in-flight operations; history is retained and marked with `|`.
There is no repeated BenchmarkDotNet warmup or synchronous pre-opening of hundreds of connections.
`C` closes idle pooled connections and marks borrowed connections for disposal when returned;
it does not cancel in-flight opens/queries. Running workers can refill the pool immediately.
Pause first if you want to drain the workers before clearing. In non-pooled mode there is no
pool to clear, and each new `OpenAsync` establishes a physical connection. `P` switches modes
live; the header identifies the current mode.

**Warm pooled opens often do no network I/O.** To exercise connection establishment, disable
pooling. To exercise network waits while holding pooled connections, enable `Q`.
The open-latency measurement excludes the optional query; cycle latency includes it and ends
before disposal. Successful opens are counted even when a subsequent query fails.

## Workloads

Press **W** to select the workload; the selection is shown above the controls:

- **Open/close** (default): open, then immediately return/dispose the connection.
- **Open/query/close**: open, await `ExecuteScalarAsync` for **`SELECT 1`**, then return/dispose
  the connection. The connection stays checked out until the query completes.

The query performs a real SQL round trip, not an artificial sleep or SQL `WAITFOR`. Netem latency
therefore extends the time a connection stays checked out even when the open itself is a fast
pooled checkout. Both workloads support pooled and non-pooled connections. Changes apply on the
next iteration; an in-flight query finishes normally. Pause requests cancellation of active
opens/queries and releases connections as they drain.

**Open** timing excludes the query; **cycle** timing includes it. `SQL in flight` includes opens
and queries. Opens/s counts successful acquisitions, not completed queries.
For a cold-pool experiment, pause and wait for active workers to drain, choose Open/query/close,
set workers and latency, clear with C, then start with Space. The query increases pool demand
but is not a barrier: it does not guarantee every worker holds a connection simultaneously.
A pool smaller than the worker count introduces checkout waits.

## Reading the charts

Every displayed metric has a time-based **30-second history**, with approximately one sample
per second. Shorter terminals page the metrics; narrower charts coalesce time positions.
The rightmost position is newest. Each bar chart scales from zero to its displayed maximum;
use a UTF-8 terminal/font with block-character support.
Only changed rows are redrawn; charts advance with the one-second samples while keyboard controls
remain responsive. Full-screen clearing is limited to startup and terminal resizing.

- Open throughput, open p50/p99, cycle p99, errors, SQL in-flight count, active async workers.
- Probe dispatch p99, producer-lateness p99, completions, outstanding/oldest pending probes,
  dropped probes, and missed producer deadlines.
- ThreadPool thread count, busy-worker proxy, queued work, and process thread count.

Quantiles use a fixed-memory logarithmic histogram: reported upper bounds are within 5% or
1 microsecond of the selected rank. `~` marks this approximation. Empty cohorts display **N/A**
and `?`, not a misleading zero. Error retries back off 250 ms and show the SQL error number,
without printing the connection string.
Connection timeouts are counted as errors and retried rather than stopping the workload.
High non-pooled concurrency and injected latency can exhaust the connection timeout while opens
wait for the driver's connection-creation slots. Reduce workers/delay or set a longer
`Connect Timeout` in the input connection string when exploring those conditions.
Unexpected worker failures pause the workload and display their exception type. After the old
workers drain, Space starts fresh workers; the prior error remains visible as the last error.

Probe cadence is 25 ms, with at most 64 outstanding callbacks. **Dispatch delay is the delay
from posting a probe until its ThreadPool callback runs**, not SQL network/operation latency.
Producer lateness measures the observer's own scheduling delay.

These are **one-second completion cohorts**, not BenchmarkDotNet async-health windows. Pending
callbacks are excluded from the percentile until they complete. Always read p99 alongside
outstanding count, oldest pending age, drops, and producer misses. At fewer than 100 completions,
nearest-rank p99 is the maximum. A low observed p99 with stuck pending probes is not evidence
of a healthy pool. Resource values are sampled gauges, not exact interval peaks.

The TUI is exploratory instrumentation, not a statistically controlled benchmark. Its
histograms, scheduler yield, observer, and UI add overhead. Use the perf harness for controlled
comparisons; do not compare these numbers directly to its ExtraIteration reports.

## Netem ownership

`--netem` supports explicitly configured `Server=tcp:<local-host>,1433` only. Every resolved
address must route through Linux loopback. IPv4/IPv6 SQL packets are filtered in both directions;
other ports use the undelayed band. A delay of **100 ms per direction adds approximately
200 ms RTT**, not a fixed 200 ms to every OpenAsync call.

The bridge reuses `eng/pipelines/perf/scripts/netem.py` for exclusive creation and cleanup.
It reserves an owned queue even at zero delay before starting SQL work, changes only that queue,
and refuses existing or externally changed rules. Packet drops fail the next update/cleanup
rather than being silently presented as pure latency. Pause leaves the owned queue installed;
**quit removes it**. ThreadPool settings are also restored before shutdown drains SQL work.

Normal exit, Ctrl+C, SIGTERM, and SIGHUP attempt cleanup. SIGKILL, host failure, and forcibly
closing WSL cannot guarantee cleanup. State and recovery handles remain in the app's unique
`/tmp/openasync-lab-*` directory. Only use the matching state file with the copied
`netem_control.py stop --state <exact-state-file>`; never delete an unrelated root queue.

## Validation

```powershell
dotnet test OpenAsyncLab\tests -c Release
dotnet run --project OpenAsyncLab\src -c Release -- --demo --frames 2
```

Privileged Linux checks use disposable network namespaces, not the host interface or SQL Server.
Build the Release tool first and put `dotnet` on PATH. The terminal check drives the real app
through a pseudo-terminal, observes expected SQL errors against a nonexistent isolated server,
changes workers and netem live, and verifies SIGTERM removes its queue. This does not substitute
for an authenticated SQL throughput run.

```bash
PERF_NETEM_TESTS=1 python3 -B -m unittest discover \
  -s OpenAsyncLab/tests -p 'test_*.py' -v
```

Demo data is visibly labeled **SIMULATED** and is never exported as measurement evidence.
Demo mode does not connect to SQL, change ThreadPool limits, or invoke netem.
