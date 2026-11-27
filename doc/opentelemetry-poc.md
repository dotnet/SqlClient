# SqlClient OpenTelemetry proof of concept

This experimental driver-side slice produces **one client span per public connection open**
and a child span for each started open invocation. Where the open's execution context reaches
physical creation, it also records selected physical and protocol phases. The root covers
pool acquisition and internal login retries. Both `Open` and `OpenAsync` are covered, including
cancellation. It also produces **one client span per `SqlCommand` execute call**, with a child
per configurable-retry attempt, and per-scope operation metrics independent of spans (see
[Phases 3-5](#phases-3-5-statements-statement-errors-and-metrics)). It does not instrument
close, internal reconnects, pool checkout/reset, `SqlBatch`, or row reads after execute.
The experimental `SqlConnectionStringBuilder.OtelEndpoint` property and `OTel Endpoint`
connection-string keyword configure an optional private SDK. The driver has no OpenTelemetry
SDK dependency.

## Run locally: application-owned SDK

Install the .NET SDK required by `global.json`, then run from the repository root:

```powershell
dotnet run --project doc\samples\OpenTelemetry\OpenTelemetry.csproj
```

The app owns a process-lifetime `TracerProvider` and console exporter. It references the
modified driver project, not the published SqlClient package. Dependencies restore from the
repository's governed feed; the OpenTelemetry console exporter and its transitive dependencies
must be available there. No cloud account, collector, OTLP endpoint, or connection string is needed.

The demo reuses the repository's simulated TDS server, bound only to `127.0.0.1` on an ephemeral
port, with protocol logging disabled. It performs two successful opens, then sync and async login
rejections for 18456, 4060, and 40501, then a pre-canceled async open. Expect **nine root
spans and eight attempt spans**, plus physical and phase children where observable; the
pre-canceled open starts no attempt. A second simulated server (error class 16) then runs
two successful `ExecuteNonQuery` calls (sync and async) and one async call failing with
SQL Server 208, producing three `statement.execute` spans, the last with `error.type`
`sqlserver.208`. A console `MeterProvider` prints the `db.client.operation.*` metrics.
The server errors travel through the real driver TDS parser, but the server is a simulation:
this does not validate SQL Server's authentication, database permission, or throttling policies.
The sample uses unencrypted loopback traffic with no credentials, not a production TLS example.

## Run locally: optional private SDK

```powershell
dotnet run --project doc\samples\OpenTelemetryPrivate\OpenTelemetryPrivate.csproj
dotnet run --project doc\samples\OpenTelemetryPrivate\OpenTelemetryPrivate.csproj -- --both
```

The first command configures **no application SDK**. The second additionally enables the
application-owned console SDK, demonstrating that both SDKs may export the same activity.
The example starts two loopback-only OTLP/HTTP test collectors and the simulated TDS server.
It sends a sync 18456 failure to one collector and an async 4060 failure to the other, checks
that neither collector receives the other's error or the simulated message, and prints the
result. Each receives its associated spans. It then runs a successful and a failing (208)
command on a connection routed to collector A, and checks that collector A receives the
statement spans and `db.client.operation.*` metrics on its sibling `/v1/metrics` path, while
collector B receives neither and no payload contains the SQL text. No real SQL Server, network
collector, credentials, or cloud resource is required.

The standalone optional project is
`src\Microsoft.Data.SqlClient.Extensions\OpenTelemetry\src\OpenTelemetry.csproj`; the sample
references it beside the driver project. This is a local PoC project, **not a published NuGet
package**. An app opting into the route supplies the full OTLP/HTTP protobuf trace URL:

```csharp
var builder = new SqlConnectionStringBuilder(connectionString)
{
    OtelEndpoint = "http://127.0.0.1:4318/v1/traces"
};
```

`otelEndpoint` and `OTel Endpoint` are both accepted connection-string spellings. Only absolute
HTTP/HTTPS URLs without user information, query strings, or fragments are accepted, so do not
put bearer tokens in this setting. The exporter overrides environment-provided OTLP headers
with an empty value; the PoC does not implement authentication. Use HTTPS for non-loopback
collectors. The PoC does not establish whether an endpoint is trusted.

At open, the driver dynamically binds the optional module using a BCL-only delegate. Each
distinct endpoint has its own SDK and batch exporter. A private, non-exported activity property
routes each span to its endpoint's processor; other endpoint processors discard it. Existing
application listeners subscribe to the same ActivitySource and may independently export the
same span. If both routes target the same backend, duplicate exports are possible by design.
The private sampler honors an unsampled current parent even if another listener samples it.

#### Route-scoped private sampling

A private pipeline's `ActivityListener` samples only activities created for its own route, and
it decides when the activity is created rather than at export time. The driver passes the route
lease as a creation-time tag (`microsoft.data.sqlclient.internal.route`) on the connection and
command `StartActivity` overloads. The pipeline returns `AllDataAndRecorded` only when that tag
holds a sampled lease for the same pipeline. Otherwise it returns `None`. The SDK
`TracerProvider` registers no sources or sampler. An SDK sampler that drops a root still
allocates a propagation-only activity, so it cannot provide this guarantee. As a result:

- An open or statement without `OTel Endpoint` gets no activity from any private pipeline. When no
  other listener samples it, `StartActivity` returns null and `OpenAsync` falls back to its
  original path.
- An activity routed to endpoint X is never sampled or exported by endpoint Y's pipeline.
- Child spans carry their parent's lease, so they follow the parent's routed decision.
- An application-owned SDK is unaffected. Its own sampler decides for every connection; a routed
  open can export to both.

The driver removes the marker tag immediately after creation, before stop or export, and keeps
the route in a non-exported custom property. Application listeners' `Sample` and
`ActivityStarted` callbacks can briefly observe the marker; its value is an opaque lease without
the endpoint or credentials. While any private pipeline is alive, an unrouted open still pays a
`HasListeners()` check, a route lookup, an `Activity.Current` save/restore and one sampling
callback that receives null creation tags; it allocates no activity. The completion processor
still filters by route, because .NET raises `ActivityStopped` on every subscribed listener.

Pipelines are shared by concurrent opens using the same exact endpoint string. A connection
close does not dispose a shared SDK. This PoC limits the process to 32 live endpoints, reaps
unused pipelines after five idle minutes, and disposes remaining pipelines at process exit. The
synchronous process-exit callback shares a five-second budget across exporter shutdown calls; an
unreachable collector can delay shutdown, and disposal overhead or process termination is not
covered by a delivery guarantee.
Active opens hold a lease so idle cleanup cannot dispose their exporter. When the limit is
reached, private export is skipped with a trace warning. Missing or unloadable optional
extension/setup failures are traced and do not block connection open; correct the issue and
restart after a cached binding failure. Exporter delivery itself is asynchronous and is not
guaranteed. These lifetime settings and failure policies are experimental.
Recoverable failures in the private processor's completion callback are traced without
propagating into connection open. This cannot isolate arbitrary application-owned
`ActivityListener` callbacks from one another; .NET invokes those listeners itself.

Different endpoint values are different connection strings and therefore use different
SqlClient pools. This PoC adds **no** pool checkout, reset, or reconnect spans.

The private provider owns the OTLP export processor, so exported spans carry the SDK default
resource. Set `OTEL_SERVICE_NAME` in the process environment to name the service; otherwise
the SDK reports `unknown_service:<process>`.

## Run locally: dashboard (Grafana, Jaeger, Prometheus)

[samples/OpenTelemetryDashboard](samples/OpenTelemetryDashboard/README.md) is the SqlClient
equivalent of the JDBC user-endpoint demo. It runs SQL Server 2022, an OTel Collector,
Prometheus, Jaeger and Grafana in Docker on `127.0.0.1`, plus a private-route workload that
produces successful opens, a 2-second statement, and failures 18456, 4060 and 102:

```powershell
cd doc\samples\OpenTelemetryDashboard
.\run.ps1                  # up + 180 s workload
.\run.ps1 -Action Down     # cleanup
```

Grafana: <http://127.0.0.1:3000/d/sqlclient-user-endpoint>. Jaeger service: `sqlclient-poc`.

## Telemetry contract

Subscribe to ActivitySource `Microsoft.Data.SqlClient.Connection` (span name `connection.open`).
Success leaves status unset. Failure sets status `Error`, without a description or exception event.
Each root has `db.system.name=microsoft.sql_server` and `db.operation.name=connection.open`.
Failed spans additionally carry:

| Attribute | Meaning |
|---|---|
| `sqlclient.error.source` | `sql_server`, `sni`, `os`, `client`, or `unknown`, based on structured evidence |
| `sqlclient.error.category` | Small experimental category listed below |
| `sqlclient.error.code` | Optional namespaced numeric code, such as `sqlserver:18456` or `os:11001` |
| `error.type` | Source namespace plus invariant numeric code (`sqlserver.18456`, `sni.31`, `os.11001`); code-less client failures use a fixed `client.<category>` value |

The source-specific `error.type` prefixes are centralized in the policy and are provisional;
they are not a proposed stable SqlClient API or a complete semantic-convention mapping.
No raw messages, stacks, SQL, server/database names, credentials, or connection strings are added.
Do not install `OpenTelemetry.Instrumentation.SqlClient` or another DiagnosticListener-to-span
bridge alongside this demo: a second instrumentation owner may create duplicate spans.
The existing DiagnosticListener events are unchanged and can still contain sensitive payloads;
this PoC does not sanitize those events or unrelated application telemetry.

### Open attempts and retry boundaries

The `connection.open.attempt` internal child is created only when the driver begins an
open invocation. Its parent is the public `connection.open` span; timestamps and duration
come from `Activity`. With a configurable retry provider, each invocation of the retry
callback that reaches the open path gets its own child. An async operation canceled before
that point has no child. A failed child records the original exception's safe classification
before the provider can wrap it. A later successful child and root have no failure tags.
When an aggregate contains the same classified failure throughout, the failed root reports
that classification; conflicting classifications remain unknown.

An attempt is **not** a physical-connection attempt. It can obtain a pooled connection,
wait for one, or encompass lower-level login/failover retries. Internal async completion
steps are part of the same attempt, not additional retries. This PoC does not expose a
retry ordinal, pool checkout/reset, or reconnect spans. Failures thrown after root creation
but before the attempt path are recorded on the root.

### Connection lifecycle coverage

The following compares this PoC with the connection-focused JDBC phase-1 concepts. Spans are
created only for work that starts and only when a listener samples the containing activity.
Parent IDs and timing come from `Activity`; a phase failure records a sanitized summary before
rethrowing, and enclosing scopes classify the exception they receive.

| JDBC phase | SqlClient source and PoC status | Validation and limits |
|---|---|---|
| Root / invocation | `SqlConnection.Open` and `OpenAsync`: `connection.open` and `connection.open.attempt`. | Sync/async successes, failures, retries and pre-cancellation use a simulated TDS server. An invocation is not a physical attempt. |
| Physical creation | `SqlConnectionFactory.CreateConnection`: `connection.physical_open` when a matching, active open context reaches the factory. | Simulated sync/async opens and pooled reuse. Background or pool workers without that context are deliberately unattributed. |
| Configuration | `SqlConnectionInternal.PrepareLoginConfiguration`: `connection.configuration` for server-info and failover selection. | Simulated successful open. Connection-string parsing can happen before open and is not included. |
| Endpoint attempts / retries | `AttemptOneLogin`: `connection.endpoint_attempt` per actual attempt. | Simulated sync/async internal transient retries and routing assert counts, ordering, parentage and recovery. One endpoint attempt can create multiple SNI handles; no ordinal is emitted. |
| Transport | `TdsParser.Connect`: `connection.transport` for each SNI handle creation, including the pre-login instance-failure retry. | Simulated sync/async opens; the instance-failure retry is not directly exercised. Native SNI combines internal discovery, resolution and sockets. |
| SQL Browser / instance discovery | Managed `SniProxy.CreateTcpHandle`: `connection.instance_discovery` around the actual SSRP lookup. | An expired lookup is exercised in a direct managed-SNI test. Native SNI is opaque; a successful SQL Browser exchange is not tested. |
| DNS | Managed `SniCommon.ResolveHostAddresses`: `connection.name_resolution` around `Dns.GetHostAddresses`. | Direct managed resolver test with a numeric address; this may not issue a DNS network request. Other lookups and native SNI are not separately traced. |
| Socket | Managed `SniTcpHandle`: `connection.socket_connect` for each sequential address attempt or one parallel batch. | Direct managed loopback tests exercise sequential and parallel success. Parallel individual sockets are not represented separately; native SNI is opaque. |
| TLS | `TdsParser.EnableSsl`: `connection.tls.setup`. | Simulated TLS setup and certificate rejection on .NET; .NET Framework TLS tests could not run with the local certificate fixture. Some handshakes finish later, so this is not a complete handshake span. |
| Pre-login | `TdsParser.SendPreLoginHandshake` and `ConsumePreLoginHandshake`: `connection.prelogin.send` and `.receive`. | Simulated sync/async opens. Instance-failure response marks receive failed but is not directly tested; TLS setup can nest under send or receive. |
| Login | `AttemptOneLogin`: `connection.login` wraps `Login` and `CompleteLogin`. | Simulated success and SQL Server error. Includes token processing and post-login parsing, so it is broader than a login exchange. |
| Token request | `GetFedAuthToken`: `connection.token_request` wraps each provider or access-token callback call, but not cache reuse or retry sleep. | Simulated server exercises callback success and failure; registered provider and provider-retry paths are not independently tested. |
| Redirect | `ApplyRouting`: `connection.redirect` for accepted routing metadata and state preparation. | Simulated sync/async routed connections assert the transition between two endpoint attempts. Validation before `ApplyRouting` and the subsequent login are outside the redirect span. |
| Initialization | `InitializeAfterLogin`: `connection.initialization` for post-response checks, MARS setup and enlistment after an unrouted login. | Simulated successful open and routing verify it is not emitted for the redirected-away endpoint. Login response parsing is outside this scope. |

This is **not full JDBC phase-1 parity**. Native SNI internals and reliable attribution for all
pool-created physical connections need further work. A private endpoint lease is retained for a physical operation
admitted while its matching open attempt is active; such work can finish after cancellation
and after the public parent span ends. Work arriving after the attempt finishes is not attributed.
There is no delivery guarantee for spans that outlive process shutdown.

Activities carry only the safe classification and a non-exported private route lease; no
raw exception is attached. The OpenTelemetry SDK queues `Activity` objects rather than a
driver-owned immutable snapshot, so this PoC does not guarantee snapshot immutability against
mutation by other in-process listeners. .NET's
[listener callbacks](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing-collection-walkthroughs#collect-traces-using-custom-logic)
receive the actual mutable `Activity` and may modify it; replacing it with a private immutable
record would require a separate capture/export pipeline rather than the normal ActivitySource
and SDK processor contract. It would not make the application-owned route immutable.

## Phase 2: connection error classification

Core captures immutable scalar evidence at the failure site, then a classifier maps it to a
category, and a separate attribute policy allowlists output keys and values. The walk follows
`InnerException` and aggregate branches with a depth limit and cycle guard; the deepest structured
cause wins, while conflicting evidence from separate exceptions at that depth becomes `unknown`.
Within one `SqlException`, the first error-severity entry (class 11 or higher) is primary;
informational entries are skipped and later errors are supplemental, not competing causes.
Thus `[4060, 18456]` reports `database_open` / `sqlserver.4060`, while an aggregate of separate
4060 and 18456 exceptions remains `unknown`. This preserves collection order; unlike this
telemetry selection, `SqlException.Number` simply exposes the first entry without filtering.
It retains no exception,
message, stack, SQLSTATE, connection string, or credential. Status and safe attributes are placed
on the owning span, not exception events. Retry membership is not classification evidence.

When classification would otherwise be unknown and evidence is not conflicting, the classifier
also uses failure-site scope fallbacks: `configuration` to `configuration`, `redirect` to
`routing_redirect`, `token_request` to `authentication`, `tls_setup` to `tls_security`, and
`name_resolution` to `name_resolution`. These are **inferences from the instrumented scope**,
not diagnoses established by an error code. The scope is structured context, but does not prove
the underlying cause; these provisional fallbacks should be evaluated separately against the
guide's "structured evidence only" rule.

| Structured evidence | Category | `error.type` |
|---|---|---|
| Parsed TDS ERROR 18456, 18486, 18488 | `authentication` | `sqlserver.<number>` |
| Parsed TDS ERROR 4060 | `database_open` | `sqlserver.4060` |
| Parsed TDS ERROR 40615 | `access_policy` | `sqlserver.40615` |
| Parsed TDS ERROR 40501, 10928, 10929 | `resource_throttling` | `sqlserver.<number>` |
| Selected SQL Server availability/network errors | `server_availability` / `network_connectivity` | `sqlserver.<number>` |
| Other parsed TDS ERROR | `unknown` | `sqlserver.<number>` |
| Recorded SNI code | Selected SNI category, otherwise `unknown` | `sni.<number>` |
| Structured socket/Win32 code | Selected timeout, name-resolution, or network category, otherwise `unknown` | `os.<number>` |
| `TimeoutException` / `OperationCanceledException` | `timeout` / `canceled` | `client.timeout` / `client.canceled` |
| Missing, conflicting, or unrecognized structured evidence | `unknown` | `unknown` |

The TDS parser marks SQL Server provenance only for an ERROR token. Client-created `SqlError`
numbers, including `-2` by itself, are not treated as server evidence; `-2` without structured
timeout evidence remains `unknown`. INFO tokens do not qualify. Windows wait code 258 carried in
`Win32ErrorCode` is emitted as `os.258`; an explicitly captured SNI code uses `sni.<number>`.
`error.type` always uses the source namespace and invariant numeric code when available. This
intentionally differs from JDBC's bare SQL number. Category does not come from localized text,
SQLSTATE alone, or retry/transient lists. Serialized `SqlError` instances without the provenance
field are not treated as confirmed server errors.

### JDBC phase-2 category coverage

This is a connection-open-only comparison. "Equivalent" means SqlClient has an evidence path for
the category, not that every JDBC resource key or runtime error has a SqlClient counterpart.
SqlClient's mappings use parsed SQL errors, SNI/OS codes, safe phase context, or exception types.

| JDBC category | SqlClient status | Current evidence / scope |
|---|---|---|
| `name_resolution` | Equivalent, partial | Socket/OS resolver codes and managed resolver exceptions; native SNI may hide the cause. |
| `network_connectivity` | Equivalent, partial | Selected SQL Server, SNI, and OS/socket codes. |
| `tls_security` | Equivalent, partial | SNI TLS code and TLS phase context; not a full certificate/TLS error inventory. |
| `authentication` | Equivalent, partial | Selected server login codes and token-request phase; no complete identity-provider taxonomy. |
| `access_policy` | Equivalent, partial | SQL Server 40615. |
| `server_availability` | Equivalent, partial | Selected server-availability codes; see the resource-throttling difference below. |
| `configuration` | Equivalent, partial | Selected SNI configuration codes and configuration phase; no localized-message matching. |
| `timeout` | Equivalent, partial | `TimeoutException`, selected OS/SNI codes, and structured timeout fields; `-2` alone is unknown. |
| `canceled` | Equivalent | `OperationCanceledException`; spelling follows JDBC's single-l `canceled`. |
| `protocol_error` | Deferred | No reliable protocol-error code mapping is included in this phase. |
| `connection_lifecycle` | Equivalent, partial | Selected SNI lifecycle code; broader close/reset lifecycle is out of scope. |
| `connection_recovery` | Deferred | Reconnect/recovery tracing and classification are excluded from this connection-open slice. |
| `client_resource_exhaustion` | Deferred | No confirmed client-capacity evidence is currently captured; do not infer from generic failures. |
| `routing_redirect` | Equivalent, partial | Redirect phase fallback; no general redirect-error inventory. |
| `internal_error` | Equivalent, partial | Selected internal SNI code only. |
| `unknown` | Equivalent | Default for missing, conflicting, or unrecognized evidence. |
| JDBC-only resource-key categories | Deferred | SqlClient has no JDBC resource-key equivalent; only explicit SqlClient structured evidence is used. |
| `database_open` | Differs by guide | SQL Server 4060 maps to `database_open`, not `unknown`. |
| `resource_throttling` | Differs by guide | 40501, 10928, 10929 and 49918-49920 map to `resource_throttling`; JDBC classifies the connection-side numbers as server availability. |

Pool checkout/reset, reconnect, complete native SNI internals, and full server/client error
inventories remain out of scope. This is not full JDBC phase-1 or phase-2 parity. Statement
classification is covered separately below.

Without a source listener, no span is allocated and the existing async task path is retained.
When enabled, async wrappers own the root and started attempt spans through task completion
and restore the caller's `Activity.Current`.

## Phases 3-5: statements, statement errors, and metrics

### Statement spans

Subscribe to ActivitySource `Microsoft.Data.SqlClient.Command`. Each public `ExecuteNonQuery`,
`ExecuteReader`, `ExecuteScalar`, and `ExecuteXmlReader` call (sync and async) produces one
`statement.execute` root (kind `Client`). APM `Begin*`/`End*` variants were not separately
validated. When a
`SqlRetryLogicBaseProvider` is configured, each attempt it runs becomes a `statement.attempt`
child (kind `Internal`); attempts are siblings in start order. Without retry logic, no attempt
child is created. Nested internal executions reuse the existing root instead of creating a
second one.

| Attribute | Meaning |
|---|---|
| `db.system.name` | `microsoft.sql_server` |
| `db.operation.name` | Public API family: `ExecuteNonQuery`, `ExecuteReader`, `ExecuteScalar`, `ExecuteXmlReader` |
| `sqlclient.statement.parameter_count` | Number of parameters, never their values |
| `sqlclient.statement.timeout` | `CommandTimeout` in seconds |
| `sqlclient.statement.attempt` | 1-based attempt number (attempt spans only) |
| `sqlclient.operation.outcome` | Root only: `success`, `failure`, `timeout`, or `canceled` |
| `sqlclient.operation.attempt_count` | Root only: attempts started by retry logic |
| `db.query.text` | Off by default; see below |

The root ends when the synchronous call returns or the returned task completes. For
`ExecuteReader`, that is when the reader is returned: **row reads, `NextResult`, and reader
close are not covered**. Errors raised while reading rows are not attributed to the span.

### Statement error classification

Statement failures use the same evidence-classifier-policy pipeline as connection opens, with
a separate statement taxonomy (`StatementErrorEvidence`, `StatementErrorClassifier`,
`StatementErrorAttributePolicy`). The same `error.type`, `sqlclient.error.source`,
`sqlclient.error.category`, and `sqlclient.error.code` keys are used, plus
`sqlclient.error.phase` (`execute`, `attempt`, or `unknown`). Precedence: driver-confirmed
cancellation/timeout, then the SQL Server number, then SNI/OS codes, then the observed client
exception kind, then `unknown`. A confirmed SQL Server number outside the table becomes
`database_error`. Conflicting evidence becomes `unknown`. Messages, stacks, and exception
events are never recorded.

| SQL Server numbers | Category |
|---|---|
| 102, 156, 207, 208, 2812 | `query_syntax_semantics` |
| 515, 547, 2601, 2627 | `constraint_violation` |
| 229, 230 | `authorization` |
| 1205, 1222 | `concurrency_conflict` |
| 40143, 40166, 40197, 40540, 40613, 4221, 42108, 42109 | `server_availability` |
| 40501, 10928, 10929, 49918, 49919, 49920 | `resource_throttling` |
| 64, 10053, 10054 | `network_connectivity` |
| any other confirmed server number | `database_error` |

49918-49920 are `resource_throttling` for both statements and connection opens, following the
cross-driver contract (open item O6).

### Opt-in query text

`db.query.text` is emitted only when the AppContext switch
`Switch.Microsoft.Data.SqlClient.EnableOpenTelemetryQueryText` is `true` (cached on first use)
and `CommandType` is `Text`. Only masked text is emitted: string and Unicode literals, decimal
and hexadecimal numbers become `?`, comments are removed, and whitespace is collapsed, so
`select 'a' where x = 1` becomes `select ? where x = ?`. Bracketed identifiers are kept. Text
starting with `EXEC`/`EXECUTE`, an unterminated literal or comment, or input over 16384
(output over 4096) characters produces no attribute rather than partially masked text.
Stored-procedure and table-direct command text is never emitted. Masking is lexical; it
does not guarantee that no sensitive identifiers remain, so keep the switch off unless the
backend is trusted with SQL shapes.

### Operation metrics

Meter `Microsoft.Data.SqlClient` records **every lifecycle scope** when it completes, whether
or not a span was created or sampled. A meter-only subscriber, with no tracing listener,
receives the full set. Instruments, following JDBC's `PerformanceActivityMetrics`:

| Instrument | Type | Recorded for | Labels |
|---|---|---|---|
| `db.client.operation.count` | Counter | Every completed scope | Base labels + outcome |
| `db.client.operation.duration` | Histogram (s) | Every completed scope | Base labels + outcome |
| `db.client.operation.error.count` | Counter | Non-success scopes | Base labels + `mssql.error.category` |
| `db.client.operation.retry.count` | Counter | Roots with more than one attempt (value = attempts - 1) | Base labels + `mssql.retry.reason` |

Base labels are `db.system.name`, `mssql.performance.activity` (see the mapping below),
`mssql.operation.kind` (`connection` or `statement`) and `mssql.telemetry.schema.version`
(`1.0`). `mssql.operation.outcome` is `success`, `failure`, `timeout` or `canceled`. Every
label has a closed value set; server, database, query text and error numbers are never labels.
An unclassified error is `unknown`.

Metric label names are shared with the JDBC driver (`mssql.*`) so that one dashboard can query
both drivers. They are provisional and apply to **metrics only**: span names and span
attributes keep their SqlClient names (`sqlclient.*`). The meter name stays
`Microsoft.Data.SqlClient`. The names are defined once in `OperationMetrics` and mirrored in
the extension's `EndpointMetrics`.

`mssql.performance.activity` values, by SqlClient span name:

| SqlClient span | `mssql.performance.activity` | JDBC counterpart |
|---|---|---|
| `connection.open` | `connection.open` | Same name |
| `connection.open.attempt` | `connection.attempt` | Same name |
| `connection.configuration` | `connection.configuration` | Same name |
| `connection.instance_discovery` | `connection.instance_discovery` | Same name |
| `connection.name_resolution` | `connection.dns` | Same name; managed SNI only |
| `connection.socket_connect` | `connection.socket_connect` | Same name; managed SNI only |
| `connection.tls.setup` | `connection.tls` | Same name |
| `connection.login` | `connection.login` | Same name |
| `connection.token_request` | `connection.token_request` | Same name |
| `connection.initialization` | `connection.initialize` | Same name |
| `connection.redirect` | `connection.redirect` | Same name |
| `connection.physical_open` | `connection.physical_open` | SqlClient only |
| `connection.endpoint_attempt` | `connection.endpoint_attempt` | SqlClient only |
| `connection.transport` | `connection.transport` | SqlClient only |
| `connection.prelogin.send` | `connection.prelogin_send` | SqlClient only |
| `connection.prelogin.receive` | `connection.prelogin_receive` | SqlClient only |
| `statement.execute` | `statement.execute` | Same name |
| `statement.attempt` | `statement.attempt` | Same name |

JDBC's `connection.prelogin_legacy`, `statement.request_build`, `statement.first_response` and
`statement.server_call*` have no SqlClient scope. Lower-level transport scopes are recorded
only where the span boundaries exist (managed SNI).

#### Cost when nobody listens

`OperationMetrics.Enabled` ORs the instruments' `Enabled` flags. Each scope start checks it
together with `ActivitySource.HasListeners()`. When both are false, `OperationScope.Create`
returns `null`: no scope object, no `Stopwatch` timestamp, no tag list and no `AsyncLocal`
write. The `OpenAsync` fast path also requires `!OperationMetrics.Enabled`, so an unheard open
keeps the original code path. When a span exists, the measurement reuses
`Activity.Duration` rather than taking a second timestamp. Otherwise, one `Stopwatch` timestamp
is taken at start and one at end. Labels are passed as a `TagList`, which does not allocate for
up to eight tags.

#### Private route

Each endpoint pipeline has its own meter (`Microsoft.Data.SqlClient.Endpoint.<n>`) and an OTLP
metric exporter. It records every **routed** scope with the same labels when the scope ends,
whether or not its span was sampled (metrics are never sampled, as in msodbcsql). The scope
carries its route lease and calls the extension through `SqlClientOpenTelemetry.Record`. Once
any routed open has happened in the process, `OperationScope` reads its `AsyncLocal` on every
later open to find the active route; before that, the check is a static bool. The process meter and the endpoint meters are separate meters, so a
measurement is never counted twice in one meter. An app that subscribes to the process meter
and also configures an `OTel Endpoint` sees the routed open in both, intentionally, as for
spans. The metric URL is derived by replacing a trailing `/v1/traces` with `/v1/metrics`; other
endpoint shapes export traces only. The private reader exports every 10 seconds and on shutdown.

#### JDBC metrics comparison

| JDBC | SqlClient | Status |
|---|---|---|
| Recorded on every lifecycle end | Every `OperationScope` dispose | Equivalent |
| Independent of span sampling | Process meter and private meter: yes | Equivalent |
| `mssql.performance.activity` | Same name | Same; JDBC values where a scope matches, see mapping |
| `mssql.operation.kind` | Same name | Same |
| `mssql.telemetry.schema.version` | Same name | Same |
| `mssql.operation.outcome` | Same name | Same |
| Error count per non-success scope, base labels only | Same, plus `mssql.error.category` | Differs: JDBC reads `mssql.error.category` from span attributes only to derive the outcome and does not label the error count with it |
| Retry count on roots only | Same | Equivalent |
| `mssql.retry.reason` | Same name: `transient_server_error`, `timeout`, `other`, or `not_applicable` | Partial; from the last failed attempt's evidence |
| `mssql.connection.auth_method` | Not supplied | Deferred |
| `mssql.statement.type`, `protocol_operation` | Not supplied | Deferred |
| `statement.request_build`, `first_response`, `server_call.*` | No scopes | Deferred; no reliable boundary |
| Lower-level phases (DNS, socket, TLS) on native SNI | No spans; failed step inferred as `sqlclient.error.step` | Partial; see "ODBC-derived improvements" |

### JDBC phase 3-5 comparison

| JDBC element | Status | SqlClient notes |
|---|---|---|
| Statement execute root span | SqlClient equivalent | One root per public execute API family; reader lifetime excluded |
| Per-attempt children for driver retries | SqlClient equivalent | Only for `SqlRetryLogicBaseProvider` attempts; SqlClient has no other internal statement retry |
| Statement error taxonomy and number registry | JDBC parity for the listed numbers | Covered by `StatementErrorClassifierTests` |
| Cancellation and timeout classification | SqlClient equivalent | From `SqlError` cancellation/timeout flags and `OperationCanceledException`/`TimeoutException` types, not messages |
| Inner cause beats wrapper; conflict becomes `unknown` | Equivalent | Same bounded cause walk as connection errors |
| Error status and attributes, no exception events | Guide rule | Follows the cross-driver guide; JDBC exception-event behavior was not re-verified here |
| `sql_state` attribute | Differs by guide | SQLSTATE alone is not classification evidence and is not exported |
| `server_state` / `severity` attributes | Deferred | Not exported in this PoC |
| `request_build` / `first_response` phases | Deferred | SqlClient has no reliable shared boundary in the execute path yet |
| `result_kind`, auth-method label | Deferred | Not implemented; schema version and connection retry reason are recorded |
| Opt-in masked query text | SqlClient equivalent | AppContext switch instead of a JDBC connection property |
| Operation metrics | SqlClient equivalent | Every scope, independent of sampling; see the metrics comparison |
| Batch execution | Deferred | `SqlBatch`/`SqlCommandSet` not instrumented |
| Row fetch / result consumption | Deferred | Not instrumented |

All attribute, instrument, meter, and source names above are provisional PoC names.

## ODBC-derived improvements

These follow the msodbcsql telemetry architecture and the cross-driver guide's SqlClient
"Proposed" items.

| Item | What SqlClient does |
|---|---|
| Native SNI step inference | Native SNI keeps one aggregate `connection.transport` span. On failure, `sqlclient.error.step` is inferred from numbers only: SNI 26 = `instance_discovery`; SNI 31 or `0x8009xxxx` (SEC_E_*) = `tls`; OS 11001-11004 = `name_resolution`; OS 10051, 10060, 10061, 10065 = `socket_connect`; otherwise the classified kind, else `unknown`. Managed SNI uses the same mapping. Message text is never read. |
| Shutdown budget (FF9) | The `ProcessExit` flush has a 500 ms budget in total across all endpoint pipelines; remaining work is abandoned after that. |
| Loss counter (FF5) | `mssqldriver.telemetry.dropped` counts spans on the endpoint meter, label `reason`. `export_failed` is recorded when the OTLP exporter fails or throws. `queue_full` is **not** reported: the batch processor's drop count is internal in OpenTelemetry 1.21.0, and reflection was not used. |
| Metrics never sampled | Endpoint metrics are recorded at scope end, not from the span processor; see "Private route". |
| 49918-49920 | `resource_throttling` for connections and statements (open item O6). |
| Retry reason | The root's `mssql.retry.reason` uses the driver's login transient-error list (4060, 10928, 10929, 40020, 40143, 40166, 40197, 40501, 40540, 40613, 42108, 42109) = `transient_server_error`; a `timeout` category = `timeout`; other failures = `other`; no retry = `not_applicable`. It reflects `SqlRetryLogicBaseProvider` attempts; the driver's internal login transient loop is not counted. |
| ActivityId | The `ActivityCorrelator` ID already sent in PRELOGIN is recorded as `mssqldriver.activity_id` on the root span. The wire format is unchanged. A random ID per root (D12) is a follow-up. |
| Driver identity | The private route's resource carries `mssqldriver.driver.name=sqlclient` and `mssqldriver.driver.version`. They are not added to spans. |

### Coexistence with OpenTelemetry.Instrumentation.SqlClient (O10)

Not tested empirically. That package builds its own statement spans from the SqlClient
`DiagnosticListener` events. An application that also subscribes to the
`Microsoft.Data.SqlClient.Command` source therefore gets two statement spans per execute: one
from each. Applications should choose one. The private route never doubles, because its sampler
only records driver-source spans on its own route.

### Timeout phase vs failing scope

`SqlConnectionTimeoutErrorPhase` is covered by existing scopes:

| SqlConnectionTimeoutErrorPhase | Scope |
|---|---|
| `PreLoginBegin` | `connection.physical_open` / `connection.endpoint_attempt` |
| `InitializeConnection` | `connection.transport` |
| `SendPreLoginHandshake` | `connection.prelogin.send` |
| `ConsumePreLoginHandshake` | `connection.prelogin.receive` / `connection.tls.setup` |
| `LoginBegin`, `ProcessConnectionAuth` | `connection.login` / `connection.token_request` |
| `PostLogin` | `connection.initialization` |

The scope that is open when the timeout fires is the failing scope, so the two normally agree.
This was reasoned from the code, not asserted in a test.

### No background worker

msodbcsql hands telemetry to a background worker over a bounded channel. SqlClient does not:
spans are started inline on the caller's thread, behind the `null`-returning fast path, and the
OpenTelemetry SDK's batch processor already exports asynchronously. A second queue would add
allocation and a thread hop to every recorded scope without reducing export cost.

## Focused checks

```powershell
dotnet test src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj -f net10.0 --filter "FullyQualifiedName~Diagnostics|FullyQualifiedName~SqlConnectionTests|FullyQualifiedName~ManagedSni"
dotnet test src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj -f net462 --filter "FullyQualifiedName~Diagnostics|FullyQualifiedName~SqlConnectionTests|FullyQualifiedName~ManagedSni"
dotnet test src\Microsoft.Data.SqlClient.Extensions\OpenTelemetry\test\OpenTelemetry.Test.csproj -f net10.0
dotnet test src\Microsoft.Data.SqlClient.Extensions\OpenTelemetry\test\OpenTelemetry.Test.csproj -f net462
```

The tests cover conservative categorization, numeric collisions, conflicting errors, privacy,
real TDS-token provenance, sync/async span lifetime, cancellation, and caller-context restoration.
Statement tests use the simulated TDS server for sync/async success, a 208 failure,
retry attempt ordering, masked query text, the no-listener path, and metrics.
`OperationMetricsTests` covers meter-only sync/async opens, per-scope labels, root-only retry
counts, error categories, span-duration reuse and statements. Extension tests cover per-endpoint
statement routing, metric URL derivation, and per-scope metric recording.
