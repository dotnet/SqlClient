# OpenAsync priority coverage: detailed cases

## Scope and status

This specification closes the coverage gaps identified in the
[test inventory](openasync-test-inventory.md) against the
[priority list](openasync-latency-priorities.md). Dependency IDs refer to the
[latency catalog](openasync-latency-catalog.md).

Recorded on October 12, 2026. Implementation observations inherit source baseline
`c7ab087c26b8c3c29275cca671799138b9c31987`. Cases below are proposed, not implemented
or verified. Existing D/M/R/E IDs retain their meaning; suffixes identify concrete
cases or variants rather than renumbering the inventory.

This is a supplement, not a replacement for D1-D16. It makes native transport,
real authentication, concurrent refresh, TLS resumption, retry mechanisms, and
cross-cutting recovery coverage explicit.

See the [implementation plan](openasync-implementation-plan.md) for project-level
test ownership, shared infrastructure, and the delivery sequence.

## Agreed requirements

The suite defines correct async behavior, not merely parity with today's
implementation. Passing cases become regression checks; failures identify work
needed to meet the requirements.

- Pass/fail is structural. Timing, throughput, allocation, and thread-count
  measurements are reported for trends, not subjected to numerical performance
  gates.
- Pending external work must not prevent task return or occupy a thread for the
  duration of the wait. Moving blocking work to another thread does not satisfy
  this requirement.
- Cancellation must terminate the public operation and promptly stop its
  underlying acquisition work and release resources owned by it. Required
  protocol cleanup and resources legitimately retained by other callers or
  active transactions must be distinguished from abandoned acquisition work.
  Responsiveness timings are reported, not gated.
- With otherwise valid inputs and an already-canceled token, no pool acquisition,
  connection/authentication work, or authentication callback may begin. Validation
  and diagnostics are separate from these acquisition operations.
- A single open-attempt timeout budget is required subject to
  `Switch.Microsoft.Data.SqlClient.UseOverallConnectTimeoutForPoolWait`.
  Test the selected compatibility behavior explicitly; do not silently change
  outer retry-policy semantics or assume one budget spans all retries.
- Exceptions to asynchronous waiting or underlying cancellation/cleanup require
  evidence that no equivalent asynchronous approach can achieve the result and
  explicit maintainer approval. Approved exceptions are reported separately,
  never as async-compliance passes.
- Linux managed SNI is the primary focus. Windows managed and native SNI remain
  tracked configurations.
- R4 and R5 are excluded as recorded below. Prefer natural `async`/`await`
  composition without claiming it guarantees asynchronous continuation dispatch.

## Test placement and execution modes

The agreed design separates reusable dependency scenarios from behavioral and
measurement execution, while reusing existing test projects and the existing
PerformanceTests/BenchmarkDotNet setup. No new project or independent runner is
planned. The extensions below are proposed, not implemented.

| Responsibility | Location and execution |
| --- | --- |
| Shared scenarios | Reusable test infrastructure under `src\Microsoft.Data.SqlClient\tests\Common` and `tests\tools\TDS` as appropriate. Own dependency gates, lifecycle events, and cleanup; keep runner-specific assertions/reporting outside the shared helpers. |
| Structural pass/fail | Extend existing coverage in its owning project: component cases in `UnitTests`, protocol-server cases following `UnitTests\SimulatedServerTests`, and other no-SQL-Server API gaps in `FunctionalTests`. Real SQL Server/OS-authentication/coordinator gaps belong in `ManualTests`; Azure-provider integration extends the existing `src\Microsoft.Data.SqlClient.Extensions\Azure\test\Azure.Test.csproj`. Provision these integration cases explicitly. DNS/network manipulation requires an isolated environment. |
| D-family measurement runs | Benchmark classes in existing `src\Microsoft.Data.SqlClient\tests\PerformanceTests`, reusing shared scenarios without referencing behavioral test assemblies. Use BDN Monitoring jobs for long experiments with one bounded scenario invocation per iteration. |
| E-family scoreboard | Extend/reuse concurrency workloads in the same PerformanceTests project, adding matched controls and supplementary per-open/resource measurements. |
| M-family benchmarks | Existing `src\Microsoft.Data.SqlClient\tests\PerformanceTests` BenchmarkDotNet project. Held-dependency experiments are not automatically microbenchmarks. |

Reuse existing runner configuration, benchmark selection, BDN execution and
`eng\pipelines\perf` scripts, primarily on Linux and with Windows tracked
separately. Extend existing process selection where necessary to isolate method/
parameter configurations; BDN runs in-process, so iterations alone do not reset
cached switches or prior ThreadPool growth.

Publish supplementary per-open/dependency metrics alongside the existing BDN
artifacts and extend current reporting scripts to summarize them. Whole-invocation
BDN timing is not individual-open latency, and intentional hold time is not
driver overhead. Separate warmup/diagnostic samples from measured iterations.
Reuse provisioning, baseline builds and artifact publication; no separate
controller or reporting platform is planned. These runs remain outside the
default unit-test command and do not use numerical performance gates.

Behavioral runs collect phase/order/ownership evidence and any instrumentation
needed for their structural assertions. They may emit diagnostic timings but do
not require the entire measurement stack on every CI run. Controlled measurement
runs provide authoritative performance trends without numerical timing gates.
Both modes preserve scenario behavior and case IDs. Infrastructure failures and
invalid scenarios are explicit failures, not successful measurements.

## Common experiment contract

This contract applies to all controlled dependency experiments **D1-D16** in the
[test inventory](openasync-test-inventory.md#2-controlled-dependency-experiments),
including their detailed subcases in this document. It also applies when an
R-family case exercises one of those fixtures, according to the
[recovery and scheduling applicability matrix](#7-recovery-and-scheduling-applicability-matrix).
The observation table below is the full contract for their **measurement runs**.
Behavioral runs retain the structural evidence described under
[test placement and execution modes](#test-placement-and-execution-modes), not
necessarily every performance counter or trace.

The M-family microbenchmarks and E-family end-to-end scenarios have their own
measurement requirements; this contract does not require them to implement the
full dependency-gating instrumentation.

Each D-case measurement run must record the applicable observations below. An immediate-ready
control has no held interval; a failure or cancellation case records its terminal
outcome rather than successful completion. Mark inapplicable observations with a
reason. Deferred or unavailable fixtures represent missing coverage, not evidence
that these observations were collected.

| Boundary | Required evidence |
| --- | --- |
| Before invocation | Effective settings, source revision, runtime, SNI/pool selection, dependency/cache state, and unique operation ID |
| Invocation to task return | Start/return timestamps and whether the returned task was already complete |
| Arrival at dependency | Positive evidence identifying the specific blocked phase; a delay alone is insufficient |
| While dependency is held | Actual pending operations, process/worker thread counts, blocked-stack samples, CPU/memory, and unrelated-work latency |
| Release to completion | Dependency-release timestamp, task-terminal observation, and caller continuation resumption |
| After completion/cancellation | Outstanding dependency work, usable capacity, and subsequent successful opens |

Use independent fixture coordination and, when necessary, a dedicated invocation thread
so a blocked `OpenAsync` call cannot prevent releasing the fixture. A bounded
external watchdog must produce a failure artifact and release owned resources
when the driver fails to make progress. Watchdog termination is not a successful
driver timeout or cancellation.

Task-terminal observations have scheduling/observation error. Record the method
used to observe completion; do not label a delayed consumer continuation as the
exact instant the task became terminal.

Run immediate-ready, single-pending, and concurrent-pending controls. Each case
declares its concurrency levels, sample duration, configured operation timeout,
and watchdog before execution. Do not introduce calibrated latency or resource
thresholds as pass/fail gates. Use observable ordering, ownership, and
dependency relationships for structural assertions; report measured numbers.
Configured timeout semantics are functional requirements, not benchmark latency
targets. Where possible, test budget propagation with a controlled clock or
instrumented boundary rather than tight wall-clock tolerances.

Two kinds of result must remain separate:

- **Behavioral assertions:** no success before required work completes, correct
  terminal result, no double completion/ownership, no stranded capacity, and valid
  connection/transaction state after success. Prove independence from a held
  dependency and absence of blocking with appropriate instrumentation; a
  low-latency run or an absence of sampled blocked stacks alone is insufficient.
- **Performance characterization:** task-return and cancellation responsiveness,
  occupied threads, scaling, and recovery time. Known synchronous/noncancelable
  paths remain structural failures unless explicitly approved as exceptions,
  rather than being skipped or relabeled as passing async behavior. A valid
  experiment may expose a failing implementation.

Exact cache lookup counts or authentication attempt counts are asserted only
when the scenario and implementation contract define them. Otherwise record
counts and detect unexpected amplification relative to the baseline.

## 1. Name resolution and repeated lookup sites

These cases refine D1/D2 and connect them to R6.

| Case | Setup and controlled boundary | Required observations |
| --- | --- | --- |
| D1-a | Explicit TCP port and SQL authentication; controlled uncached hostname; hold the transport address lookup | Resolver arrival, task-return latency, blocked threads, timeout/cancellation behavior, and transport attempts after release |
| D1-b | Same target with verified resolver-cache state | Compare cache-hit cost with D1-a; distinguish resolver API calls from actual DNS network queries |
| D1-c | Named-instance discovery with DNS and SSRP controlled independently | Identify resolution for discovery versus transport; stall each occurrence separately and record lookup/request counts |
| D2-a | Integrated authentication, implicit SPN; allow transport resolution/connect, hold canonical-name resolution | Demonstrate arrival at the SPN lookup rather than inferring it from elapsed time |
| D2-b | Same endpoint with an explicit valid SPN | Confirm bypass of driver SPN canonicalization; do not assume transport DNS or security-package lookups disappear |
| D1-d | Server routing or a selected retry sends the open to a separately identifiable endpoint | Attribute lookup counts and delay to each destination/attempt; use R6 route/retry controls below |

DNS control must be isolated to the test environment, not machine-wide settings
on a shared developer host. Use unique names or verified cache management, not an
assumption that a new client process clears OS DNS caches.

For cancellation during resolver work, observe both public-task cancellation and
resolver completion. If the resolver cannot be interrupted, report residual work
and time until it stops; do not assert that canceling the task cancels OS resolution.

## 2. Native transport coverage

OA-16 includes more than named pipes. D7 therefore has explicit transport
subcases rather than treating a named-pipe run as all-native-transport coverage.

| Case | Configuration and fixture | Required observations |
| --- | --- | --- |
| D7-a | Windows managed SNI, explicit named-pipe endpoint; control pipe availability | Pending establishment, release/failure, invocation responsiveness, and handle recovery |
| D7-b | Windows native SNI, same supported named-pipe scenario | Same measurements at the native boundary; confirm selected transport rather than assuming fallback behavior |
| D7-c | Windows native SNI, explicit shared-memory protocol and local SQL Server | Immediate-success and unavailable-endpoint cases; protocol evidence, setup cost, errors, and handle recovery |
| D7-d | Shared-memory establishment held pending inside native SNI/OS IPC | **Required for complete controlled OA-16 coverage, but deferred until an observable native test seam or dedicated controllable environment exists.** Never substitute a TCP or named-pipe stall and claim this case passed. |

Managed SNI shared memory and unsupported OS/transport combinations are
not-applicable, with a capability reason, rather than silent skips. D7-c does not
replace D7-d: endpoint absence may fail immediately and never exercise a wait.
The deferred case must remain visible in coverage reports.

D6 separately exercises TCP success, refused connection, establishment that
receives no response, and multi-address attempts. Delayed application `Accept`
alone does not control the OS TCP handshake.

## 3. Authentication provider integration and refresh

D11's controlled provider establishes driver composition behavior. It cannot
establish the behavior of a real authentication SDK or service.

### Driver/provider boundary

| Case | Setup and gate | Required observations |
| --- | --- | --- |
| D11-a | Provider/callback returns an already-completed valid token result | Invocation overhead, completion path, and successful login |
| D11-b | Provider/callback confirms invocation, then awaits an externally released gate | Occupied login/worker threads, task-return time, release-to-login completion, and cancellation-token delivery where the API exposes a token |
| D11-c | Single acquisition/refresh with controlled validity and cache state | Cache hit versus acquisition/refresh counts, expiry handling, and successful use of the resulting token |
| D11-d | Concurrent physical opens sharing the relevant authentication context/cache key at refresh time | Acquisition fan-out, refresh coordination, blocked threads, tail latency, and resulting pool capacity |
| D11-e | Same concurrency as D11-d with independent authentication contexts/cache keys | Control for cache serialization versus general provider/ThreadPool limits |
| D11-f | During concurrent refresh, cancel one waiter, then a cohort; separately fail refresh | Surviving callers progress; terminal results and residual work are counted; a later valid acquisition/open succeeds |

Do not assume single-flight refresh is an API guarantee. Record duplicate
acquisitions and coordination behavior; assert a maximum only when that mechanism
has an established contract. Use a supported provider seam or controlled token
validity for deterministic expiry, not changes to the shared machine clock.
For real tokens, never log token contents or manufacture invalid credentials.

### Real-provider integration

| Case | Actual dependency to exercise | Required variants and evidence |
| --- | --- | --- |
| D11-g | Service-principal authentication using the shipped Azure provider and SDK | Initial acquisition, warm cache, refresh, endpoint failure/retry; record SDK/service calls and driver-thread occupancy |
| D11-h | Managed identity in a dedicated supported host environment | Cold/warm acquisition, identity-endpoint delay/failure where controllable, concurrency, and recovery |
| D11-i | Workload identity | Credential-file access and token exchange; missing/unavailable file and exchange failure; cache/refresh behavior |
| D11-j | Default credential chain | Pin the SDK version and allowed credential sources; record which source succeeds and attempted fallbacks; compare prompt success with slow/unavailable earlier sources |
| D11-k | Interactive, device-code, or brokered authentication | Controlled/manual user-completion and abandonment; record human/broker wait separately. Not a deterministic throughput gate. |
| D12-a | Controlled integrated-authentication provider with initial token and subsequent challenge gates | Exercise each invocation independently; capture caller/login-thread occupancy and cancellation/failure cleanup |
| D12-b | Real OS integrated authentication in a dedicated domain/Kerberos environment | Warm versus cold credential/ticket state where controllable, successful negotiation, unavailable security infrastructure, and challenge handling |

Apply delay injection only in an isolated endpoint/network fixture that preserves
real provider execution and TLS validation. If a service or SDK offers no safe
way to control a phase, record the case as an environment-dependent
characterization, not deterministic coverage of that phase.

Secrets remain in environment/secret-store configuration and must never be
captured in result artifacts. Pin credential-chain selection to prevent
accidentally contacting developer-local tools or identities.

Legacy password-based Entra authentication, if included in the support commitment,
requires its own real-provider case; it is explicitly outside the initial matrix,
not implied covered by service-principal authentication.

## 4. TLS full handshake versus resumption

Each applicable row runs separately for Strict and prelogin-negotiated TLS and
for the selected managed/native SNI implementations. Protocol-version cells are
selected according to actual platform/server support.

| Case | Setup and controlled phase | Required observations |
| --- | --- | --- |
| D13-a | New physical connection with verified full handshake | Disable resumption on the dedicated peer or isolate session state; prove a full handshake using TLS/server telemetry |
| D13-b | Establish session state, close transport, then create a new physical connection eligible for resumption | Confirm resumption actually occurred. A reused pooled connection is not a resumed handshake. Compare latency/resource cost with D13-a. |
| D13-c | Attempt resumption but have the peer reject it and perform a full handshake | Verify fallback, no false success, latency, and cleanup |
| D13-d | Hold a full handshake at an identified server-flight boundary | Task-return, occupied threads, release-to-completion, cancellation, timeout, and handshake-resource recovery |
| D13-e | Hold a resumed handshake at an observable boundary | Same measurements; report unsupported/unobservable resumption explicitly rather than recording an assumed resumed result |
| D14-a | Valid certificate with warm versus cold trust/chain state where controllable | Validation duration, platform activity, and correct server identity validation |
| D14-b | Configured server-certificate file with warm versus cold file access; missing/unreadable file | File-access cost and explicit validation failure; no fallback that silently bypasses the requested check |
| D14-c | Invalid identity/untrusted certificate and interrupted handshake | Correct failure, no usable connection returned, resource recovery, then a successful valid connection |

A new process does not necessarily clear OS TLS or certificate caches. Record
server resumption policy and actual negotiated behavior. Certificate-service
network access is platform/policy-dependent; do not claim every validation
performs revocation I/O.

## 5. Retry, failover, and routing cases

Disable unrelated retry layers while isolating one mechanism. Then run a bounded
composition case. Retry exhaustion means the configured attempts/budget are
exhausted, not that the external watchdog terminated the test.

| Case | Mechanism and trigger | Required observations |
| --- | --- | --- |
| R6-a | Physical connect retry after a retry-eligible transport failure | Destination/attempt counts, per-attempt phase timings, backoff duration and occupied threads, success and exhaustion |
| R6-b | Login transient-fault handling under `ConnectRetryCount`/`ConnectRetryInterval` | Inject an eligible login failure; compare retry enabled with the applicable disabled/override control; measure budget and backoff separately |
| R6-c | Authentication-provider retry | Controlled provider returns a retryable exception with/without a retry period, then succeeds or continues failing; capture provider calls, backoff, budget, and cancellation |
| R6-d | Authentication timeout recovery during login | Delay acquisition/server progress to reach the driver's recovery path; verify the path using events/counters and record token reuse versus reacquisition |
| R6-e | Built-in configurable connection retry provider | Eligible failure then success, exhausted attempts, cancellation during `Task.Delay`, and cancellation requested by a `Retrying` handler |
| R6-f | Custom retry callbacks/provider | Gate predicate/event/provider work separately from its delay; attribute application-caused latency rather than assuming the built-in policy's behavior |
| R6-g | Failover partner selection | Primary unavailable/partner healthy, opposite availability, and both unavailable; record destination order, per-attempt waits, applicable compatibility settings, and terminal outcome |
| R6-h | Server routing | Initial server returns routing information; destination succeeds, stalls, or fails. Attribute repeated DNS/transport/TLS/authentication and cleanup of the original connection. |
| R6-i | Excessive/invalid routing | Exercise supported redirect-limit or invalid-destination failure behavior; ensure bounded attempts and cleanup |
| R6-j | Bounded composition of configurable retry with one internal retry/routing mechanism | Record actual total attempts, total elapsed time, per-layer budgets, cancellation, and residual work; detect multiplicative amplification |

Cancellation cases must cover waiting on an attempt and waiting between attempts.
Record whether caller cancellation reaches the underlying mechanism. Pool
blocking-period cached errors must be separately identified so a fast cached
failure is not mistaken for a fresh connection attempt.

## 6. Activation cases and transaction recovery

Each case starts from a verified warm physical connection when testing pooled
activation. Record physical connection creation counts to exclude accidental
cold opens. A protocol gate must identify the activation request being withheld.

| Case | Trigger and gate | Required state/recovery checks |
| --- | --- | --- |
| D15-a | Automatic enlistment in a new ambient transaction; hold its server response | Successful open has the expected enlistment; cancellation/failure does not leak or incorrectly reuse transaction-owned capacity |
| D15-b | Checkout requiring unenlistment; hold cookie propagation response | No connection is handed out with incorrect transaction ownership; later acquisition succeeds |
| D15-c | Dirty isolation level with reset switch enabled; hold the reset batch response | Correct isolation after success; failed reset cannot return an unsafe connection. Include switch-disabled and clean-session controls. |
| D15-d | Same-transaction checkout requiring isolation reassertion | Exercise non-ReadCommitted reassertion and ReadCommitted no-extra-batch control; verify effective session isolation after success |
| D16-a | Distributed enlistment requiring coordinator/token work | Gate/observe the coordinator boundary where supported; track transaction outcome, callbacks, connections, and recovery |
| D16-b | Distributed enlistment requiring SQL Server requests | Hold whereabouts/cookie propagation at the applicable boundary separately; verify outcome and resource ownership |

For every case, request cancellation while activation is pending and race
cancellation with response release. Activation can execute before `OpenAsync`
returns its task; independent fixture coordination must still be able to request
cancellation and eventually release the dependency.

Use a finite connection/transaction timeout plus an independent watchdog. Record
whether the particular operation honors the connection budget, transaction
timeout, caller token, or only completes after dependency release. Do not assume
all three budgets propagate uniformly.

A connection legitimately retained by an active transaction is not a leak.
Measure recovery both before and after explicit transaction completion/disposal.
Do not require zero retained connections until the fixture has released legitimate
owners. Follow failure with a healthy transaction/open to verify usable capacity.

## 7. Recovery and scheduling applicability matrix

Every D-case measurement run inherits the common performance measurements,
including release-to-completion and unrelated-work latency. Behavioral runs use
the same scenarios with the evidence needed for structural assertions. This matrix specifies companion
behavior families; it does not imply that each combination requires a new harness.

Legend:

- **Y:** required companion cases.
- **C:** conditional on a supported/observable mechanism; record the selected
  cases or a specific non-applicability reason.
- **O:** observe/characterize even if cancellation or interruption is unsupported.

R1 = cancellation and resource recovery; R2 = timeout; R3 = races;
R6 = retry/routing; R7 = burst recovery.
R4 is excluded by scope decision: no known defect motivates caller
`SynchronizationContext` or custom `TaskScheduler` independence testing.
R5 is also excluded: no known defect motivates dedicated expensive/reentrant
consumer-continuation tests. Implementation work should prioritize natural
`async`/`await` composition, completing required bookkeeping before exposing
completion. This improves completion ordering but does not guarantee that
consumer continuations are queued rather than executed inline.

| Fixture | R1 | R2 | R3 | R6 | R7 |
| --- | --- | --- | --- | --- | --- |
| D1 endpoint DNS | O | O | Y | Y | Y |
| D2 SPN DNS | O | O | Y | C | Y |
| D3 pool availability | Y | Y | Y | C | Y |
| D4 pooled creation admission | Y | Y | Y | Y | Y |
| D5 nonpooled queue | Y | Y | Y | Y | Y |
| D6 TCP establishment | O | Y | Y | Y | Y |
| D7 named pipes | O | Y | Y | C | Y |
| D7 shared memory | O | O | C | C | C |
| D8 prelogin response | O | Y | Y | C | Y |
| D9 login/auth response | O | Y | Y | Y | Y |
| D10 write backpressure | O | Y | Y | C | Y |
| D11 token provider | O | O | Y | Y | Y |
| D12 integrated auth | O | O | Y | C | Y |
| D13 TLS | O | Y | Y | C | Y |
| D14 certificate validation | O | O | C | C | C |
| D15 pooled activation | O | O | Y | C | Y |
| D16 distributed activation | O | O | Y | C | Y |

The D7 shared-memory row is subject to the explicit D7-d deferral. Immediate
endpoint failures do not satisfy pending-path matrix cells.

For R3, cover cancellation before arrival, while held, and racing release/failure
where the boundary is controllable. Assert state/ownership integrity irrespective
of which permitted terminal outcome wins.

R1 also includes the already-canceled-token case: prepare valid inputs, cancel
before invocation, and instrument pool acquisition, physical creation, and
authentication entry points. Assert cancellation and zero acquisition work or
authentication callback invocations, without a tight elapsed-time assertion.
This refines R1 rather than adding another top-level inventory definition.

For R7, cancel/fail a cohort, release all fixture gates, wait for or account for
residual work, then run healthy opens. Compare recovered capacity and latency
with the pre-burst control. ThreadPool threads can remain alive after load; thread
count alone is not proof of leaked work.

## 8. Scoreboard links and completion criteria

E1-E6 remain the overall progress measurements. Add targeted profiles rather than
crossing every variant:

| Profile | End-to-end scenarios | Controlled cases explaining changes |
| --- | --- | --- |
| Basic SQL authentication, explicit TCP endpoint | E1-E4 | D1, D3-D6, D8-D10 |
| Encrypted physical connections, full/resumed handshake | E3/E4 | D13/D14 |
| Entra acquisition and refresh | E3/E4 plus a suitable burst profile in E6 | D11 and R6-c/R6-d |
| Integrated authentication | E3/E4 | D2/D12 |
| Transactional pooled reuse | E1/E2/E5 with explicit transaction lifecycle | D15/D16 |

All profiles use the inventory's baseline/candidate async and sync controls,
fresh-process comparison discipline, per-open distributions, failure counts,
achieved concurrency, and resource/responsiveness measurements.

A priority is fully accounted for only when its selected cases and matrix cells
have explicit status: implemented/passing, failed with characterization,
maintainer-approved exception, unsupported with evidence, or deferred with a
named blocker. Approved exceptions include supporting evidence and the approval
reference and remain distinct from passes.
Environment unavailability is not a pass. This specification closes ambiguity in
the planned coverage; it does not claim the native, SDK, or laboratory controls
already exist.
