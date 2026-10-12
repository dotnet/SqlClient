# OpenAsync test suite implementation plan

## Status and scope

This plan implements the [test inventory](openasync-test-inventory.md) and
[detailed cases](openasync-test-cases.md). Except for the initial implementation
status below, it records proposed changes, not completed coverage. The inventory's 32 active definitions and 47 detailed
subcases retain their IDs; subcases and recovery combinations are not additional
top-level definitions.

The suite will use existing projects for structural correctness and
**the existing PerformanceTests project and perf pipeline for all measurements**,
including dependency characterization and the end-to-end scoreboard. No new
project, executable, independently configured runner, or controller is planned.
This supersedes the earlier separate-executable proposal.

Pass/fail remains structural. Timing and resource numbers are reported without
numerical performance gates. Linux managed SNI is primary; Windows managed and
native SNI are tracked separately. R4/R5 remain excluded. Driver improvements
identified by these tests are follow-up work, not silently included in harness
implementation.

Paths below are relative to `src\Microsoft.Data.SqlClient\tests` unless stated
otherwise. Suggested new class/file names may change to match nearby conventions;
case IDs, responsibilities, and project ownership are the stable contract.

### Initial performance implementation

The initial E1-E4 adapter is `PerformanceTests\BenchmarkRunners\OpenAsyncScoreboardRunner.cs`,
selected through the existing registry/configuration and the opt-in
`openasync-preliminary` perf profile. It adapts the existing warm/contended/
nonpooled/retained-ramp workload shapes into matched ThreadPool Open/OpenAsync
cohorts; it does not replace the existing uninstrumented benchmarks.
`UnitTests\OpenAsyncScoreboardTests.cs` tests the linked pure configuration and
workload helpers without referencing BenchmarkDotNet or adding a test project.

The first matrix has 24 configurations: E1-E4, Sync/Async, concurrency 1/8/32.
Each process has three actual Monitoring iterations, no warmup and no
regression-triggered reruns or numerical gates. E1/E2/E3 use 16 opens per worker;
E4 uses one. E2 uses a fixed pool of four with identical 10 ms asynchronous holds,
including an intentionally unsaturated concurrency-one control. E4 retains the
whole cohort; E1/E2 prewarm by retaining all required connections before release.
Pool preparation is repeated outside each measured invocation. Pools are unique
to a case and explicitly cleared; the process, ThreadPool and OS caches are not
reset between that case's iterations.

The supplementary schema records per-open/task-return observations, admission
delay, actual peak outstanding/owned counts, failures and process-wide resource
deltas. It excludes BDN warmup and diagnostic invocations through workload-stage
signals. Export and pool setup/cleanup occur outside the timed benchmark.
Infrastructure, acquisition or accounting failures invalidate the run.
Source/package baseline selection remains the existing pipeline's responsibility.

This is an **instrumented preliminary subset**, not completion of the foundation,
scoreboard or async-compliance milestones. Blocked-stack attribution,
unrelated-work latency, server saturation telemetry, controlled D-fixtures and
their R variants are still unimplemented. Throughput includes matched yields,
holds and close costs; it is not a pure driver-overhead measurement. Initial lab
results must retain each iteration and state these limitations. Commands and
artifact interpretation are documented in `BUILDGUIDE.md` and the perf README.

### Repository-wide comparison scope

The comparison now inventories all 41 C# projects in this checkout and compares
relevant assertions/workloads across the 11 test/benchmark projects below.
Unrelated tests are not credited as OpenAsync coverage merely because they use
async APIs. This is source-level coverage analysis, not an execution result.

| Existing project / group | Disposition |
| --- | --- |
| SqlClient UnitTests | Reuse the component and simulated-server regressions cataloged below; extend only missing async-boundary evidence. |
| SqlClient FunctionalTests | Mock base-provider cancellation and retry argument validation are not the proposed SqlConnection precancellation contract. Packet encapsulation is not TLS-handshake coverage. |
| SqlClient ManualTests | Reuse real pool, retry, token refresh, certificate, isolation, and distributed-transaction baselines; add only missing async variants, controlled waits, and cleanup evidence. |
| SqlClient PerformanceTests | M1/M2 and substantial E-family workload building blocks already exist. New adapters/measurements must not be described as entirely new workloads. |
| `StressTests\SqlClient.Stress.Tests` | Reuse relevant stress patterns as supporting evidence, not deterministic phase coverage. No new stress project or duplicate broad soak tests. |
| `src\Microsoft.Data.SqlClient.Extensions\Azure\test\Azure.Test.csproj` (repo-relative) | Existing owner of real Azure-provider integration. Extend here rather than recreating those tests in ManualTests. |
| `src\Microsoft.Data.SqlClient.Extensions\Abstractions\test\Abstractions.Test.csproj` (repo-relative) | Provider lookup without the driver, auth parameter/token constructors, and exception metadata. No OpenAsync acquisition tests; no additions planned. |
| `src\Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider\test\Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.Test.csproj` (repo-relative) | `KeyedAsyncLockTest` already tests held-key waits, precancellation, canceled cohorts, surviving waiters, and gate reclamation for AKV. This is not the connection pool or login-token refresh mechanism; no duplicate AKV tests or OpenAsync credit. |
| `tools\PackageCompatibility\test\PackageCompatibility.Test.csproj` (repo-relative) | CLI/build-version/error/logging checks, including unreachable-endpoint failure. Not pending OpenAsync or acquisition cleanup; no additions planned. |
| `tools\PackageValidator\test\PackageValidator.Test.csproj` (repo-relative) | Package metadata, binary, symbols, source-map, and version checks; not runtime acquisition coverage. No additions planned. |
| `tools\SniCloseLegacyRepro\SniCloseLegacyRepro.csproj` (repo-relative) | Links existing SNI/MARS regressions against legacy System.Data.SqlClient. Do not count the linked files as additional Microsoft.Data.SqlClient coverage or expand its historical framework matrix. |

Common, TDS, TestUtilities/ExtUtilities, CustomConfigurableRetryLogic, the four
stress support/runner projects, and TestUdts are supporting infrastructure, not
additional independent OpenAsync suites. Other product/sample/application
projects are not test coverage. The existing perf-script `test_affinity.py`
tests process-affinity handling, not the new latency/result schema.
When extending SNI test files linked by SniCloseLegacyRepro, preserve that build
surface or put Microsoft.Data.SqlClient-specific additions in an unlinked file.

## 1. Project changes at a glance

| Project | Planned additions or extensions |
| --- | --- |
| `UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj` | Reuse existing pool, budget, ownership, routing, TLS, and reset regressions. Add only the missing held-dependency/cancellation assertions described below, plus tests for the new measurement harness. |
| `FunctionalTests\Microsoft.Data.SqlClient.FunctionalTests.csproj` | Serverless public-API precancellation and missing controlled retry-delay/callback contracts. Reuse retry exhaustion/event-cancellation evidence already in ManualTests. |
| `ManualTests\Microsoft.Data.SqlClient.ManualTests.csproj` | Extend existing pool, callback/refresh, TLS, and transaction tests with missing async/held-phase/cleanup evidence; add provisioned DNS/transport controls. No duplicate ready-path integration suites. |
| Existing Azure extension `Azure.Test.csproj` (repo-relative path above) | Extend shipped-provider integration with missing async/held-phase/refresh evidence; preserve existing service-principal, managed-identity, default, Entra-integrated, and interactive controls. |
| `PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj` | Retain M1/M2; add M3-M5 measurements, D1-D16 characterization with applicable R variants, and E1-E6 scoreboard support through existing/new BenchmarkDotNet classes. Reuse configuration, selection, execution, and reporting infrastructure. |
| `Common\Microsoft.Data.SqlClient.TestCommon.csproj` | Reusable scenario controls, lifecycle records, capability declarations, and cleanup under `OpenAsync`. No runner-specific assertions or reporting in the new helpers. |
| `tools\TDS\TDS.csproj`, `TDS.EndPoint\TDS.EndPoint.csproj`, `TDS.Servers\TDS.Servers.csproj` | Extend existing protocol infrastructure with phase-aware response gates, attempt/connection identities, scripted failures/routing, and TLS controls where feasible. |
| `StressTests` and unrelated test projects | No additions in this plan. R7 is a bounded reproducible recovery scenario, not a new unbounded stress workload. |

Existing UnitTests, FunctionalTests, ManualTests, and Common retain
`net462;net10.0`; TDS helpers retain `netstandard2.0`. New behavioral tests run on
each applicable existing target. Platform/API exclusions must be explicit.
PerformanceTests retains `net10.0`; this plan does not
promise .NET Framework performance measurements.

## 2. Shared infrastructure before scenario duplication

Add runner-neutral helpers under `Common\OpenAsync` for:

- Scenario identity/configuration, required capabilities, effective pool/SNI and
  compatibility settings, and sanitized environment metadata.
- Explicit prepare/arrive/hold/release/fail/cancel/cleanup lifecycle controls.
  Gates identify the actual dependency visit, connection, and attempt.
- Event records for invocation, task return, dependency arrival/release, observed
  task termination, consumer resumption, underlying-work termination, and resource
  ownership. Preserve the distinction between event time and observation time.
- Cleanup that releases owned gates, drains or accounts for outstanding work,
  disposes connections/transactions, and verifies that fixtures leave no owned
  background work behind.

Keep TDS-specific parsing and peer behavior in the TDS projects. Do not add a TDS
dependency to Common merely to make it an umbrella library; runners can reference
both. Existing test projects already reference Common and TDS. PerformanceTests
already references Common; add TDS references only for the selected scenarios
that need a controlled protocol peer.

Use real driver paths wherever possible. Fake pool components or instrumented
dependencies can prove local ownership and propagation contracts, but cannot
prove production DNS, native SNI, OS security, or provider execution is
nonblocking. Such cases have separate integration obligations below.

Fixture coordination must still operate if invocation or a driver worker
blocks; this is scenario control, not a separate runner/controller. A watchdog
produces diagnostics and terminates a stuck experiment; it
must not report successful cancellation or timeout. Do not use sleeps to establish
phase arrival or tight elapsed-time assertions to infer asynchronous behavior.
Task return while a dependency remains held is necessary evidence, not proof that
another thread was not blocked instead.

### Instrumentation and seam delivery

For each boundary, first reuse existing events, counters, callbacks, and test
helpers. If these cannot establish arrival, work ownership, or cancellation
propagation, deliver a narrowly scoped internal seam with that fixture. Do not
introduce public driver APIs or replace a synchronous production dependency with
an async fake and call the production path compliant.

Every seam-dependent milestone starts with a feasibility check: demonstrate the
intended boundary with both a ready and a pending control. If evidence cannot be
obtained, keep that case blocked with the missing seam named. Missing blocked-stack
samples or favorable thread counts alone cannot turn it into a structural pass.

## 3. Tests in existing behavioral projects

### UnitTests

The coverage audit below compares existing test bodies and assertions with the
proposed work. **Existing behavior is removed from the new-test backlog**, not
deleted from the repository. D/R IDs still describe overall suite requirements;
an existing test can satisfy a structural requirement without already providing
the new performance measurements. This is a source-coverage assessment, not a
claim that those tests were executed during planning.

#### Existing coverage: no new duplicate tests

All paths in this table are relative to `UnitTests`.

| Requirement removed from new work | Existing test evidence | Coverage boundary |
| --- | --- | --- |
| D3 basic saturation, release, and waiter progress | `ConnectionPool\WaitHandleDbConnectionPoolTransactionTest.cs`: `PoolSaturation_BlocksUntilConnectionAvailable`; `ChannelDbConnectionPoolTest.cs`: `SyncCheckout_WhenSaturatedOnThreadPoolThreads_AllWaitersMakeProgress`, `ConcurrentCallers_ShouldTimeoutIndependently`. | Pool-component behavior; does not establish nonblocking public OpenAsync or caller-token cleanup. |
| D4 V2 creation permit acquisition, reuse, release, and failure cleanup | `ConnectionPool\ChannelDbConnectionPoolTest.cs`: `RateLimiter_PermitAvailable_CreatesPhysicalConnection`, `RateLimiter_SuccessfulCreate_ReleasesLeaseForNextCreate`, `RateLimiter_LeaseReleaseAllowsRateLimitedWaiterToCreatePhysicalConnection(bool async)`, `RateLimiter_PermitDenied_ReusesReturnedConnection`, `RateLimiter_LeaseDisposedOnFailure_DoesNotStarvePool`. | Covers the pooled V2 limiter, not the legacy creation semaphore or D5 nonpooled factory queue. |
| R2 consumed pool-wait budget and login compatibility selection | `ConnectionPool\WaitHandleDbConnectionPoolBudgetTest.cs`: `GetConnection_TimeoutTimerReflectsTimeAlreadyConsumed(bool async)`, `ResolvePoolWaitTimeoutMs_ReturnsExpected`; `ChannelDbConnectionPoolTest.cs`: `GetConnection_TimeoutTimerReflectsPoolWaitTime`; `Microsoft\Data\SqlClient\SqlConnectionInternalTimeoutTests.cs`: `ResolveLoginTimeout_HonorsSwitch`. | Existing virtual-time propagation and timer-identity/fresh-budget assertions replace the proposed new component budget tests. Phase-specific cancellation and retry composition remain separate. |
| R2 timer expiration, child budgets, and timer-driven cancellation | `ProviderBase\TimeoutTimerTest.cs`: `CreateCancellationTokenSource_FiresWhenTimerExpires`, `CreateCancellationTokenSource_AlreadyExpired_ReturnsCanceledSource`, `StartChild_PropagatesParentTimeProvider`, `StartChild_RequestedDurationLongerThanParent_IsCappedAtParentRemaining`, and related expired/infinite cases. | No duplicate timeout utility suite or new clock abstraction is needed for these contracts. |
| R1 component channel cancellation and legacy retired-pool waiter draining | `ConnectionPool\IdleConnectionChannelTest.cs`: `ReadAsync_Cancelled_ThrowsOperationCanceledException`; `WaitHandleDbConnectionPoolShutdownTest.cs`: `Shutdown_InFlightRequest_CompletesOnRetiredPool(bool async, bool cancel)` cancels the completion and checks the waiter count drains. | Not proof that a public caller token terminates physical acquisition. Reuse these assertions instead of duplicating channel/retirement tests. |
| R7 reclamation restores capacity; repeated creation failures do not strand V2 permits | `ConnectionPool\DbConnectionPoolReclamationTest.cs`: `SaturatedRequest_IsServedByReclaimingLeakedConnection`, `WaitingCaller_IsServed_WhenConnectionIsLeakedAfterTheRequestBegins(bool async)`; V2 `RateLimiter_LeaseDisposedOnFailure_DoesNotStarvePool`. | Removes generic reclamation/failure-recovery component additions, not cancellation/failure-cohort recovery through OpenAsync. |
| D15 transaction parking, reuse, release, and ambient-transaction ownership | `ConnectionPool\WaitHandleDbConnectionPoolTransactionTest.cs`: `GetConnectionAsync_UnderTransaction_ReturnsSameConnectionFromTransactedPool`, `TransactionCommit_ClearsTransactedPool`, `TransactionRollback_ClearsTransactedPool`; `ChannelDbConnectionPoolTransactionTest.cs`: `ReturnConnection_TransactionRootToShutDownPool_EntersStasisRatherThanBeingDestroyed`, `ReplaceConnection_CarriesEnlistedTransactionToNewConnection`, `GetConnectionAsync_UsesAmbientTransactionCapturedOnCallersThread`, and related async-flow cases. | Reuse pool ownership tests; add only missing cancellation while actual activation work is held. |
| D15 transaction-preserving reset predicate | `Microsoft\Data\SqlClient\SqlConnectionInternalResetTransactionTests.cs`: `ShouldPreserveTransactionOnReset_CoversBothTransactionOwnershipModes` asserts all eight input combinations. | No duplicate ownership-predicate tests. |
| D15-c/D15-d reset/reassertion controls, failure safety, and activation budget | `SimulatedServerTests\IsolationLevelResetTests.cs`: `ReassertSessionIsolationLevel_TracksChangesForOptInReset`, `ResetSessionIsolationLevel_Success_KeepsConnectionUsable`, `ResetSessionIsolationLevel_ServerRejects_DoomsConnection`, `ResetSessionIsolationLevel_ParserNotUsable_ThrowsAndDooms`, `ResetSessionIsolationLevel_ConnectionAlreadyDoomed_SkipsReset`, `PooledReset_UsesRemainingOpenBudget`, `ResetSessionIsolationLevel_UsesOpenBudget`. | Already checks batch counts, dirty/doom state, switch controls, and remaining budget. Missing work is held activation and cancellation/release behavior, not these baseline checks. |
| R6-h routed success and destination/database/visit accounting | `SimulatedServerTests\ConnectionEnhancedRoutingTests.cs`: `RoutedConnection(bool useAsync)` checks open state, routing destination, database and one prelogin at each peer; `ServerDoesNotRoute` checks the non-routed control. | Remove new generic route-success tests. Extend only for stalled destinations, cancellation, residual work, or new attempt composition. |
| R6-i redirect-limit failure | `SimulatedServerTests\ConnectionReadOnlyRoutingTests.cs`: `ConnectionRoutingLimit`, `AsyncConnectionRoutingLimit` assert the too-many-redirections exception. | No new redirect-limit regression just for this suite; cleanup and cancellation evidence may still need extensions. |
| D12 successful custom SSPI multi-step login | `SimulatedServerTests\SspiTests.cs`: `CustomSspiProviderCompletesLogin`, `CustomSspiProviderCompletesLoginAsync` assert open state and at least two provider invocations. | Ready-path controls already exist; held token generation/challenge cancellation remains new. |
| D13 cancellation during a held TLS handshake does not deadlock | `SimulatedServerTests\SNICloseHandshakeCancellationTest.cs`: `CancelOpenAsyncDuringTlsHandshake_DoesNotDeadlock` observes the in-flight handshake and requires settlement after cancellation. | It accepts fault or cancellation and does not assert resource cleanup. Extend this test/fixture; do not add a second equivalent deadlock test. |
| D14 configured certificate matching/mismatch and missing-file failure | `Microsoft\Data\SqlClient\ManagedSni\SniCommonValidateSslServerCertificateTest.cs`: matching/mismatched-pin cases, null/unavailable-certificate cases, and `ValidateSslServerCertificate_UnreadablePinFile_PolicyNone_Throws`. | Managed helper coverage, including fail-closed missing-file behavior; does not replace a real handshake/ownership test or cold-cache measurement. |
| R3 connection-transition race guards | `Microsoft\Data\SqlClient\SqlConnectionStateTransitionTests.cs`: `TryOpenInner_WhenInnerConnectionChangesAfterTryOpen_ThrowsInvalidOperation`, corresponding replacement case, and transition/event tests. | Keep existing state-machine guards; these do not cover all cancellation-versus-release orderings. |

Existing pool-clear coverage also remains in
`SimulatedServerTests\PoolClearDuringOpenTests.ClearDuringOpen_InFlightConnectionsAreDestroyedOnClose`:
pending opens, retirement, destroyed-on-close/drained counts, and a replacement
pool. It is not transaction activation coverage. Likewise, existing
`SniProxyGetSqlServerSPNsTest` selection/explicit-SPN tests remain the baseline
for D2; they do not establish behavior while real DNS is pending.

#### Remaining additions and targeted extensions

The proposed blanket `OpenAsyncPoolWaitTests` and
`OpenAsyncCreationAdmissionTests` component suites are removed. Extend existing
pool tests only for a specifically missing assertion; keep the new public-path
dependency scenarios separate from already-covered pool algorithms.

| Owner / proposed location | New work only |
| --- | --- |
| Existing pool tests plus `SimulatedServerTests\OpenAsyncProtocolWaitTests` | D3/D4: public OpenAsync task return with a held dependency, independent evidence of worker waiting, caller-token propagation and underlying acquisition cleanup, and cancellation-versus-release/failure orderings. Do not recreate generic saturation, V2 permit, budget, or reclamation tests. Legacy admission assertions are not assumed covered by V2 tests. |
| Nonpooled factory fixture in `OpenAsyncProtocolWaitTests` | D5: actual nonpooled queue/admission, queued cancellation and physical-work ownership. V2 pooled limiter coverage does not satisfy this case. |
| `SimulatedServerTests\OpenAsyncProtocolWaitTests` | D8/D9: identified prelogin/login/challenge/final-completion gates, fragmented responses, task-return/worker-wait evidence, and missing R1-R3/R7 companions. Reuse existing connection/failure controls rather than adding ordinary login-success tests. |
| `SimulatedServerTests\OpenAsyncWriteBackpressureTests` | D10: demonstrated pending opening write, followed by cancellation, release/failure or timeout and cleanup. Requires a reliable pending-write seam. |
| `SimulatedServerTests\OpenAsyncAuthenticationTests` and existing `SspiTests` | D11-a through D11-f controlled pending/concurrent token-provider/callback behavior; D12-a held token/challenge behavior and cancellation cleanup. Reuse existing ManualTests token-expiry/replacement and SSPI ready-path controls instead of adding duplicate baseline refresh or basic custom-provider login tests. |
| Existing SNI handshake-cancellation tests and certificate tests | D13/D14: add exact terminal-outcome and underlying-resource evidence to the held-handshake fixture, then missing Strict/negotiated phase variants and failed-handshake recovery. Do not duplicate certificate-helper decisions or the existing no-deadlock check. |
| Existing routing/failover/connection tests | R6: add held-attempt/backoff evidence, cancellation propagation, original/destination cleanup, controlled authentication retry, and bounded retry-layer composition. Reuse routed/non-routed success and redirect-limit assertions; introduce a separate helper/class only for a genuinely new mechanism. |
| Existing `IsolationLevelResetTests` plus activation fixture extensions | D15: held enlist/unenlist/reset/reassertion requests, task-return and worker-wait evidence, cancellation/release races and post-cancellation recovery. Reuse existing reset controls, doom-state/budget assertions, and transaction ownership tests. |
| Public precancellation instrumentation in UnitTests | R1: prove zero acquisition/physical-creation/authentication entry for an already-canceled caller token. Component channel cancellation is not equivalent. Pair with the FunctionalTests contract cases below. |
| `OpenAsyncHarnessTests` | Gate lifecycle, event ordering/correlation, cancellation isolation, cleanup on failure, capability/status validation, and watchdog-result classification. Test fixtures themselves before trusting driver results. |
| Measurement-helper tests (`net10.0` where required) | Test supplementary result aggregation, latency boundaries, iteration selection, failed-operation accounting and E6 offered-load accounting. Reuse Common helpers or reference the existing PerformanceTests project for this target; PerformanceTests must never reference UnitTests. Test script selection/process-failure/reporting extensions alongside existing perf-script tests. |

These extensions do not require rewriting existing regression assertions to
match the new fixture API. Extract shared setup only where a measurement runner
actually needs it. Keep existing tests independently runnable. Measurement
adapters remain new work even when their behavioral controls already exist.

Tests involving process-wide switches, registered providers, pool registries, or
other global state need isolated processes or appropriate existing serialized
collections with restoration. Loopback tests must not change host DNS/firewall
configuration. A simulated peer's own blocked thread is not driver blocking:
attribute evidence by thread, stack, phase, and operation.

### FunctionalTests

Existing `BaseProviderAsyncTest.TestDbConnection` exercises the base-provider
mock, not the SqlConnection acquisition path. `SqlConfigurableRetryLogicTest`
currently covers invalid arguments/options and loader behavior, not the retry
execution matrix. `SslOverTdsStreamTest.SyncTest`/`AsyncTest` and their core
helpers verify packet encapsulation, not held TLS negotiation. Preserve these
tests, but do not use them to mark the missing OpenAsync contracts covered.

| Group / proposed location | Cases and assertions |
| --- | --- |
| `OpenAsyncCancellationContractTest` | R1 already-canceled token with otherwise valid inputs: canceled result, unchanged closed state, no authentication callback invocation. Cover relevant overloads, pooling modes, and supported retry settings without contacting an external server. |
| Existing `SqlConfigurableRetryLogicTest` | R6-e/R6-f missing controlled-operation cases: caller-token cancellation during a held retry delay and attribution of gated custom predicate/event/provider work. Remove new generic exhaustion/counting and `Retrying`-handler cancellation cases: ManualTests already asserts those for Open and OpenAsync. |

The public precancellation test alone cannot prove zero internal acquisition.
Pair it with UnitTests instrumentation at pool/factory/authentication entry points.
Cold/nonpooled and prepared warm-pool integration variants use the shared fixtures
in UnitTests or ManualTests rather than adding an external dependency here.
Retry-provider component tests likewise do not replace actual OpenAsync integration.
Conversely, an existing retry execution test in UnitTests or ManualTests is not
missing coverage merely because it lives outside FunctionalTests; do not port or
duplicate it solely to populate this project.

### Azure extension tests

Real Azure-provider tests already live in
`src\Microsoft.Data.SqlClient.Extensions\Azure\test` (repo-relative), targeting
`net462;net10.0`. This existing project, rather than a new ManualTests group,
owns D11-g through D11-k provider-specific behavioral extensions.

| Existing test | Reuse; do not recreate | Remaining difference |
| --- | --- | --- |
| `AADConnectionTest.NoCredentialsActiveDirectoryServicePrincipal` | Real service-principal successful connection and missing-credential rejection. | Existing helper opens synchronously; add missing async acquisition/refresh/failure evidence, not another sync happy path. |
| `AADConnectionTest.SystemAssigned_ManagedIdentityTest`, `UserAssigned_ManagedIdentityTest`, and `Azure_*ManagedIdentityTest` | Provisioned system/user identity connection controls. | Async successful acquisition, verified cache/refresh and held identity-endpoint behavior remain to be established. |
| `AADConnectionTest.ActiveDirectoryManagedIdentityWithInvalidUserIdMustFail` | Both Open and OpenAsync reject invalid identity IDs with the expected authentication failure. | No duplicate invalid-ID test; it does not establish pending cancellation or resource recovery. |
| `AADConnectionTest.ActiveDirectoryDefaultMustPass` | Existing default-auth connection control. | Pinned source selection/fallback and pending acquisition evidence, including async coverage. |
| `AADConnectionTest.ADIntegratedUsingSSPI` | Entra Integrated successful connection in its provisioned environment. | Not the SQL Server OS-integrated D12-b scenario; track the mechanisms separately. |
| `ActiveDirectoryInteractiveTests.TestConnection` | Interactive, nonpooled success for all four Open/OpenAsync overloads, with and without retry override. | Human/broker wait attribution, controlled abandonment and cleanup remain new; do not infer an actual prompt from connection success or duplicate this smoke test. |
| `WorkloadIdentityFederationTests.GetCredential` | Azure Pipelines credential obtains a nonempty token. | Does not exercise driver OpenAsync with workload-identity credential-file/token-exchange behavior; D11-i remains distinct. |
| `AADAuthenticationTests`, `DefaultAuthProviderTests`, `WamBrokerTests`, `AuthorityParsingTests` | Registration, provider selection, broker options, and malformed-authority rejection. | Not pending acquisition, concurrent refresh, or a real broker-abandonment test. |

Keep driver callback/cache behavior in UnitTests/ManualTests where already owned.
Measurement scenarios reuse runner-neutral helpers and provisioning, not the
Azure.Test assembly. Do not assume a particular credential implementation from
the authentication keyword: record the actual shipped extension/dependency
versions and credential source used.

### ManualTests

Add deterministic structural counterparts where real dependencies can be safely
controlled, and integration confirmations elsewhere. Full performance collection
belongs to PerformanceTests, even when both use the same provisioned lab.

#### Existing coverage: no new duplicate tests

Paths in this table are relative to `ManualTests`.

| Existing tests | Existing coverage credited | Limit / remaining work |
| --- | --- | --- |
| `SQL\ConnectionPoolTest\ConnectionPoolStressTest.ConnectionPoolStress_Sync` / `ConnectionPoolStress_Async`, through `TestConnectionPoolExhaustion` | After stress, can open and retain `maxPoolSize` connections, then dispose them; both APIs. | No new generic post-stress capacity test. Still need controlled OpenAsync cancellation/failure cohorts and underlying-work accounting. |
| `SQL\ConnectionPoolTest\TransactionPoolTest.BasicTransactionPoolTest`, `TransactionCleanupTest` | Transaction-pool reuse, stasis and count checks. | Reuse rather than duplicating general transaction-pool cleanup; held activation/cancellation is separate. |
| `SQL\RetryLogic\SqlConnectionReliabilityTest.ConnectionRetryOpenInvalidCatalogFailed` and async counterpart | Configured retry exhaustion, retry/event and aggregate exception counts. | No new generic exhaustion/counting tests in FunctionalTests or another integration class. Not cancellation during a held attempt/delay. |
| Same file: `ConnectionCancelRetryOpenInvalidCatalog`, `ConnectionCancelRetryOpenAsyncInvalidCatalog`, and `CreateConnectionWithInvalidCatalog` | `Retrying` sets `e.Cancel`; verifies early-stop counts, exception aggregation, and event retry-count/exception-count consistency for both APIs. | Remove duplicate handler-cancellation cases. This is event cancellation, not cancellation by the caller's token. |
| `SQL\ConnectivityTests\ConnectivityTest.ConnectionOpenAsyncDisableRetry` | Existing OpenAsync retry-disable timing control. | Reuse the configuration; do not adopt its timing as a new structural gate or call it held-backoff evidence. |
| `SQL\ConnectivityTests\AADConnectionTest.AccessTokenTest`, `AccessTokenCallbackMustOpenPassAndChangePropertyFail`, and related credential-conflict tests | Raw-token OpenAsync success, callback-based opening, immutable-while-open callback property, and incompatible credential settings. | No duplicate raw-token/callback happy-path or conflict matrix. Controlled pending callbacks and concurrent refresh remain distinct. |
| `SQL\AADFedAuthTokenRefreshTest\AADFedAuthTokenRefreshTest.AccessTokenCallback_PooledConnectionIsReplacedOnExpiry` | Both pool implementations and sync/async: reuses a physical connection before expiry; close does not reacquire; expiry at checkout replaces it and invokes the callback; replacement is reused and a query succeeds. | Removes baseline D11-c expiry/replacement work. It artificially ages token/context state; does not hold acquisition pending or establish concurrent-refresh cancellation/single-flight behavior. |
| `SQL\ConnectivityTests\IntegratedAuthenticationTest` pooled/nonpooled, explicit/invalid ServerSPN, and custom SSPI generator cases | OS-integrated and SPN ready/error controls. | Preserve these controls; add missing pending generation/canonicalization and domain-service evidence, not a new basic integrated-auth matrix. |
| `SQL\ConnectionTestWithSSLCert\CertificateTest.OpeningConnectionWithGoodCertificateTest`, `OpeningConnectionWithNicTest`, `RemoteCertificateNameMismatchErrorTest` | Windows certificate success for Optional/Mandatory/Strict when supported; hostname-in-certificate controls; managed-SNI mismatch error details. | Existing opens are synchronous and require their configured Windows/admin environment. Missing async/held-handshake/recovery evidence remains; no duplicate sync matrix. |
| `SQL\ConnectionTestWithSSLCert\CertificateTestWithTdsServer.BeginWindowsConnectionTest`, `BeginLinuxConnectionTest`, through `ConnectionTest` | Parameterized encryption/trust/hostname/server-certificate success/failure controls against a test peer. | Failure cases accept a broad failure outcome; not proof of exact phase, cancellation, resource recovery, or TLS resumption. Extend only those missing observations/variants. |
| `SQL\TransactionTest\IsolationLevelLeakTest` and `TransactionScopeIsolationReassertTest` | Existing isolation reset opt-in/default behavior, pool reuse, same-transaction reassertion and session-override controls, including sync/async coverage. | Remove baseline D15 isolation-state/reuse additions; retain held request, cancellation-race and recovery extensions. |
| `SQL\TransactionTest\DistributedTransactionTest.Test_EnlistedTransactionPreservedWhilePooled` | Windows transaction-owned pooled reuse and failure after killing the root connection; checks transaction/command failure and no committed rows. | Existing test is marked flaky. Preserve this status; do not count it as a reliable gate or a controlled coordinator-wait test. D16-a/D16-b wait attribution and cancellation remain new. |
| Same file: `Delegated_transaction_deadlock_in_SinglePhaseCommit` | Existing modern-Windows commit deadlock regression. | Completion-based regression, not coordinator-phase instrumentation. Do not duplicate it as an OpenAsync test. |
| `SQL\DNSCachingTest\DNSCachingTest.DNSCachingIsSupportedFlag`, `DNSCachingGetDNSInfo`; `SQL\SqlNamedPipesTest\SqlNamedPipesTest` | DNS support/cache-information and ordinary named-pipe connection controls. | Not pending production DNS or named-pipe establishment; D1/D2/D7 hold/cancel evidence remains missing. |

Do not count `AsyncCancelledConnectionsTest.CancelAsyncConnections` as
OpenAsync/pool-wait cancellation: its cancellation coverage is command-oriented.
Likewise, `AsyncTimeoutTest.TestDelayedAsyncTimeout` exercises async command
timeouts, not the opening budget. Reuse general infrastructure where useful,
but do not mark R1/R2 acquisition requirements satisfied by those tests.

#### Remaining additions and targeted extensions

| Group / suggested existing area | New work only |
| --- | --- |
| `SQL\ConnectivityTests\OpenAsyncDnsTest` | D1-a through D1-d, D2-a/D2-b: isolated resolver/SSRP environment, verified cache states, explicit versus implicit SPN, route/retry destination attribution. |
| Existing connectivity/named-pipe tests plus transport fixture | D6 controlled pending/multi-address establishment with cancellation/cleanup; D7-a/D7-b missing async/held named-pipe evidence; D7-c native shared-memory protocol confirmation and recovery. Reuse ordinary connection controls. |
| `SQL\ConnectionPoolTest` extensions | D3-D5 and R1-R3/R7 held admission/nonpooled queue and cancellation/failure-cohort recovery; distinguish task cancellation from underlying work. No new generic saturation/post-stress capacity suite. |
| Existing callback/token tests and `SQL\ConnectivityTests\IntegratedAuthenticationTest` | Pending/concurrent D11 callback/refresh and D12-b generation/challenge cleanup only; reuse sequential expiry/replacement and integrated-auth controls. Real Azure-provider extensions belong in Azure.Test. |
| `SQL\ConnectionTestWithSSLCert` extensions | D13 verified full/resumed/rejected-resumption and held flights; D14 controlled warm/cold file/validation behavior, missing async paths and post-failure resource recovery. Preserve existing validation matrices. |
| `SQL\TransactionTest` extensions | D15/D16 held activation/coordinator/SQL requests, cancellation races, legitimate retained ownership, and post-cancellation recovery. Reuse isolation/reset/reassertion and transaction-failure baselines. |
| `SQL\RetryLogic` / `SQL\ConnectivityTests` extensions | R6 held-attempt/backoff and caller-token cancellation, residual-work/route attribution, and bounded retry-layer composition. No duplicate retry-exhaustion or `Retrying` event-cancel tests. |

D7-d remains explicitly deferred until a native shared-memory pending-boundary
seam or controllable environment exists. D7-c cannot satisfy it. Other lab cases
must advertise exact prerequisites, including privileges, OS/provider support,
certificate setup, domain/cloud identity, and coordinator availability.
Configuration is opt-in; secrets stay out of source and artifacts.

### Recovery families are parameters, not duplicate harnesses

R1, R2, R3, and R7 accompany each fixture according to the existing
[applicability matrix](openasync-test-cases.md#7-recovery-and-scheduling-applicability-matrix).
Keep the matrix's required, conditional, and observe-only distinctions.
R6's ten detailed subcases use the retry/routing owners above. Behavioral wrappers
assert structural outcomes; measurement variants add responsiveness and resource
reports. Record each selected combination by D-case, R-family, and configuration.

R3 covers controlled arrival/release/failure/cancellation orderings and permitted
race outcomes, not a requirement that one winner always prevail. R7 checks
recovered usable capacity with bounded healthy opens; throughput/tail recovery is
reported by the measurement runner, not asserted against a numerical threshold.

Use synchronous Open controls for shared state, error, and cleanup semantics,
but do not apply async task-return or cancellation-token requirements to Open.

## 4. Existing PerformanceTests additions

M1/M2 already exist. E-family work must reuse the following workload building
blocks within PerformanceTests, extracting shared setup where needed rather than
copying workloads into a second runner.

| Existing runner/method | Existing workload credited | Remaining scoreboard work |
| --- | --- | --- |
| `ConnectionPoolChurnRunner.RapidOpenCloseSingleThread` / `RapidOpenCloseSingleThreadAsync`; `ConnectionPoolStressRunner.RapidFireOpenCloseSync` / `RapidFireOpenCloseAsync` | M1 and E1 warm checkout/return, including prewarmed concurrent churn. | Common concurrency sweep, matched configurations, per-open/task-return distributions and resource/responsiveness reporting. |
| `SqlConnectionRunner.OpenConnection` / `OpenAsyncConnection` | M2 pooling/MARS lifecycle matrix. | Keep the existing control; do not add a duplicate lifecycle benchmark. |
| `ConnectionPoolContentionRunner.SteadyStateOpenQueryClose`, `SteadyStateOpenQueryCloseAsync`, `SteadyStateOpenQueryCloseDedicatedThreads` | Existing saturated open/query/close workload and ThreadPool/dedicated-thread controls, useful for E2/E5. | E2 needs open-only work with matched holds. Existing sync variant also queries synchronously and async variant queries asynchronously; E5 must hold query behavior constant to isolate opening. Add scoreboard measurements without relabeling query-inclusive numbers as open-only latency. |
| `ParallelAsyncConnectionRunner.OpenConnectionsConcurrently` | E3 building block: concurrent pooled/nonpooled async opens, pool clearing and immediate disposal. | Sustained paired sync/async workload, achieved concurrency and per-open observations. It does not prove all configured callers hold simultaneous physical connections. |
| `ConnectionPoolRampRunner.ColdStartRampAsync` / `ColdStartRamp` | E4 already has cold-pool clearing and a retention barrier: connections remain held until the cohort arrives. | Reuse that workload. Existing sync control uses dedicated threads; add normal-ThreadPool sync workers for the primary comparison, plus the new distributions/admission/resource reports. |
| `ConnectionPoolThreadPoolPressureRunner.SaturatedSyncOpenOnThreadPool` | Existing sync scheduling-pressure diagnostic with ThreadPool minimum controls and warm-pool setup. | Not an async comparison or proof of nonblocking OpenAsync. Reuse as a diagnostic, not as the E scoreboard itself. |
| `TransactionScopeIsolationRunner.OpensOutsideScope`, `OpensInsideScope_ReadCommitted`, `OpensInsideScope_Serializable`, `OpensInsideScope_SerializableAsync` | Existing warm transactional/reassertion cost controls for D15-related E profiles. | Held activation, cancellation/cleanup evidence, and concurrency reporting remain new; do not recreate basic transactional performance controls. |
| `ConnectionPoolStressRunner.PoolExhaustionRecovery` | Opens `MaxPoolSize + Parallelism` operations, runs queries and random async holds, exercising oversubscription/backpressure. | Not an R7 cancellation/failure storm and no residual-work proof. Reuse load controls; add controlled cohort cancellation/failure and explicit recovery accounting. |
| `ConnectionPoolStressRunner.BurstyTrafficPattern` | Five completion-coupled bursts with a pause between them; each open performs several queries. | E6 independently scheduled arrivals, bounded generator capacity and offered/started/dropped accounting remain new. Do not relabel these bursts as fixed-arrival load. |
| `ConnectionPoolStressRunner.MixedSyncAsyncContention`, `RandomizedHoldAndQuery`, `MultiCommandReuse` | Existing mixed sync/async contention, randomized hold/query and repeated-command workloads. | Supporting stress controls, not matched open-only experiments or acquisition-cancellation tests. No duplicate stress workload planned. |

StressTests also exercises connection opening through
`SqlClient.Stress.Framework\DataStressConnection.Open`, `OpenAsync`, and
`OpenSync`, with sync/async selection and SPID retrieval. Command cancellation
and database-context reconnection stress are not OpenAsync acquisition
cancellation or phase-gated R7 coverage. Preserve those tests without treating
them as a replacement for the controlled fixture.

| ID | Implementation work |
| --- | --- |
| M1/M2 | Keep `ConnectionPoolChurnRunner` and `SqlConnectionRunner` as fast-path controls. Preserve workload meaning, pool depths, pooling/MARS variants, and setup/disposal accounting. |
| M3 | Add a warm-path runner/measurement support for invocation-to-task-return and completed-at-return fraction. Separate these samples from full lifecycle BDN operation time; do not label batch-normalized output as per-open latency. |
| M4 | Add a pool-waiter bookkeeping runner once the controlled pool fixture exists. Exclude deliberate wait time from the measured operation and attribute allocation/CPU to registration/cancellation work, not setup. |
| M5 | Add a handoff runner once D3 release/arrival controls exist. Measure release to waiting-opener observation, documenting observer/scheduling cost rather than claiming exact task-terminal timing. |

Keep BDN setup/iteration semantics and existing diagnostics. Where BDN cannot
express a custom per-event metric faithfully, export it as a clearly identified
companion measurement rather than changing the definition of its operation time.
Implement the E scoreboard through these same BDN classes/helpers, not a parallel
execution system.

## 5. Fit all measurements into PerformanceTests

Extend the existing layout rather than creating another executable:

```text
PerformanceTests\
  BenchmarkRunners\    existing workloads and new dependency benchmark classes
  Config\              existing RunnerJob and BenchmarkConfig extensions
  Program.cs           existing benchmark registry and selectors
  runnerconfig.jsonc   same configuration surface
```

Place supplementary measurement/export helpers within this project as needed.
Reference Common and required TDS projects, never UnitTests, FunctionalTests,
ManualTests, or Azure.Test assemblies. Preserve repository-standard
`ReferenceType=Project` and `ReferenceType=Package` consistently through the
dependency graph, including the configured driver version in Common.

Use the existing `RunnerJob.RunStrategy` support for `Monitoring` on long
dependency/concurrency experiments, with one complete scenario invocation per
iteration, unroll factor one, explicit repetitions, and deliberate warm/cold
setup. Keep suitable microbenchmarks on the existing throughput strategy.
BenchmarkDotNet drives both kinds of measurement; there is no custom worker mode.

Each dependency invocation prepares the fixture, observes the held phase,
coordinates release/failure/cancellation, and drains or accounts for remaining
work. Warmup and diagnoser executions can also invoke a benchmark: reset scenario
state appropriately and identify which executions contribute reported samples.
Do not mix warmup, diagnostic, and measured samples or perform cleanup while
unaccounted work is still running.

| Area | Required behavior |
| --- | --- |
| Existing registry/configuration | Register added benchmark classes through `Program.cs` and the existing configuration. Preserve current selectors and defaults; extend selection only where finer isolation is needed. |
| Dependency benchmarks | D1-D16 and applicable R variants reuse shared controls. Run ready, single-pending, and concurrent-pending configurations. Unavailable fixtures remain visible with their blocker. |
| Concurrency benchmarks | E1 warm reuse; E2 saturated pool with matched holds; E3 sustained nonpooled opens; E4 retained cold ramp; E5 matched open/query/close; E6 independently scheduled arrival/overload/recovery. |
| Matched comparisons | Baseline/candidate OpenAsync plus baseline/candidate Open, normal ThreadPool workers, at most one outstanding open per worker, matched non-open work, repeated/interleaved configurations. Dedicated sync threads are diagnostic only. |
| Diagnostics | Task-return and per-open distributions, achieved concurrency, admission delay, resource counters, blocked-stack attribution, unrelated-work latency, cancellation/cleanup observations, and failure counts. Record unavailable diagnostics, never invent zeros. |
| Supplementary results | Publish per-open/dependency metrics alongside existing BDN Markdown/full JSON artifacts. Include a versioned supplementary format, case/configuration and iteration identity, source/package versions, structural verdicts, capability state, and instrumentation mode. Extend existing comparison/publication scripts rather than creating another reporting platform. |

BDN elapsed time measures the whole invocation. Do not label that distribution
as individual-open latency or treat an intentionally held dependency's duration
as driver overhead. Use the supplementary samples for those questions; keep the
existing BDN schema and its meaning intact.

The current in-process toolchain preserves switches set by `Program.cs`.
Reuse the existing perf scripts' executable launches and interleaving for process
isolation. They currently select benchmark groups, not necessarily individual
method/parameter configurations: extend selection for fresh-process comparisons
where needed. In-process BDN launch/iteration counts do not by themselves reset
ThreadPool, static, or OS cache state. Do not switch to a generated out-of-process
BDN host without also preserving driver configuration there.

For E6, schedule arrivals independently of completion and bound generator
capacity. Count offered, started, completed, canceled/failed, and dropped/rejected
work separately; report queuing delay rather than hiding it inside a success-only
latency distribution. Validate the accounting under overload using synthetic
operations before involving SQL Server.

Use separate low-overhead throughput runs and instrumented diagnostic runs where
tracing perturbs results; label them and never combine their numbers as though
they came from one equivalent sample. Record server saturation and failed starts.
Retain all unsuccessful operations in outcome accounting.

Result dimensions must distinguish experiment validity, structural outcome, and
performance samples. A valid measurement can expose a structural failure.
Unavailable/deferred/unsupported and maintainer-approved exceptions remain
distinct from passes; exceptions include evidence and an approval reference.
An explicitly requested case with missing prerequisites cannot exit as a
successful measurement. Structural or infrastructure failures must remain visible
in process/job outcomes even when artifacts were successfully written.

## 6. Build, CI, and reporting changes

| Surface | Planned integration |
| --- | --- |
| Solution and repository-root `build.proj` | Reuse existing PerformanceTests registration/build targets. No new project path or parallel build/run target set; measurements remain outside default behavioral test execution. |
| Existing behavioral CI | Run deterministic structural additions in their owning test project, including Azure.Test for extension-owned authentication. Use existing framework/platform conventions and process isolation for global configuration. |
| Provisioned integration jobs | Explicitly select lab-dependent ManualTests and publish prerequisites and unsupported/deferred cells. Do not allow shared-host DNS/firewall mutations. |
| `eng\pipelines\perf` | Extend existing run-perf-tests/interleave scripts only as needed for configuration-level selection/isolation, watchdogs and supplemental artifact collection. Reuse the same provisioning, configuration, baseline selection and entry points; Linux primary, Windows separately selected. |
| Baseline build/staging | Reuse existing source/package baseline builds and isolated outputs. Source baselines currently build their own perf project, so verify scenario revision/configuration equivalence; stage compatible shared benchmark sources if required, or report the comparison unavailable. Never silently compare different workload definitions. |
| Reporting | Extend existing comparison/reporting scripts to validate and summarize supplementary metrics alongside BDN results. Preserve case/configuration comparability and separate diagnostic runs. For these scenarios, bypass numerical regression gating and regression-triggered confirmation policy; use predeclared repetitions and report all outcomes. Leave unrelated benchmark policies unchanged. |
| Documentation | Extend `BUILDGUIDE.md` and the perf README with actual implemented commands, prerequisites, schema, case selection, and artifact interpretation as each entry point lands. |

Internal-seam tests may require source builds and need not work against an old
package baseline. Public-API E workloads should remain package-comparable;
unsupported D measurements must be labeled rather than simulated with different
semantics. Verify the loaded driver assembly/version in each measurement process.
Ensure BDN case failures, invalid experiments, missing required artifacts, and
watchdog termination propagate into script/job failure even if the executable
produces partial reports or otherwise returns success.

New desired-behavior failures must not be hidden as skipped/flaky tests or
weakened to match existing blocking behavior. Land a failing structural regression
with its driver fix when practical. Otherwise track it in an explicitly visible
async-compliance run until the fix lands; that run still reports failure.
Moving a case into ordinary gating requires demonstrated correctness, not a
calibrated timing threshold.

## 7. Delivery sequence and exit criteria

Each increment includes its shared fixture, existing behavioral coverage or
targeted missing assertions, measurement adapter, selected R companions, and
coverage metadata where applicable. Do not add a new behavioral wrapper when
an existing test already establishes the same contract. A measurement-only
implementation does not complete a dependency milestone with structural gaps.

| Increment | Deliverables | Depends on | Exit evidence |
| --- | --- | --- | --- |
| 1. Foundation | Common lifecycle/events; register scenario benchmarks in existing PerformanceTests configuration; supplementary measurements/export and helper tests; extend existing scripts only for missing selection/isolation/failure handling. | None | Synthetic ready/pending/failure/cancellation cases validate gates, watchdog cleanup, iteration accounting, result statuses, and loaded-driver identity through the existing perf setup. |
| 2. Initial scoreboard | Adapt existing warm/churn/contention/parallel-open/retained-ramp BDN workload building blocks for E1-E4; add matched controls and diagnostics, use existing Linux perf jobs and extend existing reports. Keep M1/M2 unchanged. | 1 | Matched source/package configurations produce individual-open distributions, achieved concurrency and failure accounting; no timing gates. |
| 3. Pool and protocol | D3-D5, D8/D9 held-phase/cancellation gaps; R1-R3/R7; FunctionalTests precancellation and UnitTests entry instrumentation. Reuse existing pool/limiter/budget regressions rather than creating parallel component suites. | 1 | Phase arrival/release proven, public versus underlying cancellation distinguished, no lost capacity after controlled races; current defects reported. |
| 4. Resolver and transport | D1-a through D1-d, D2-a/D2-b, D6, D7-a through D7-c; initial R6-a/R6-b/R6-g/R6-h/R6-i controls. | 3; isolated DNS/network lab; Windows transport lab | Actual production lookup/transport visits identified; timeout/cleanup and repeated destinations accounted for. D7-d stays named as deferred. |
| 5. Writes and mechanism metrics | D10, M3-M5, E5/E6. | 2 and 3; observable pending-write seam | Pending write demonstrated; benchmark setup excluded as specified; offered-load totals reconcile under overload. |
| 6. Authentication | D11-a through D11-k and D12-a/D12-b gaps; R6-c through R6-f and bounded R6-j composition. Extend existing Azure.Test provider and ManualTests callback/OS-integrated coverage rather than duplicating ready paths. | 3; controlled provider/security seams; identity/domain environments | Cache/refresh and callback/provider distinctions proven; surviving waiters progress; real-provider coverage distinguished from substitutes and manual interaction. |
| 7. TLS/certificates | D13-a through D13-e, D14-a through D14-c measurement/integration gaps; extend existing held-handshake settlement coverage with missing cancellation/cleanup evidence. No duplicate pin-validation unit tests. | 3; controllable TLS peer and lab telemetry | Full/resumed/fallback handshakes verified; validation remains enabled; failures never yield usable connections; unsupported controls explicitly recorded. |
| 8. Activation | D15-a through D15-d held-activation/cancellation gaps, D16-a/D16-b, transactional scoreboard profiles. Reuse existing isolation reset/budget and transaction-ownership unit coverage. | 3; transaction/coordinator environments | Warm activation verified; transaction/isolation state confirmed on SQL Server; recovery accounts for legitimate retained owners before and after transaction completion. |

After foundation, scoreboard and structural-fixture work can proceed
independently. TLS work need not wait for authentication if its prerequisites are
ready. Apply Windows configuration tracking with each supported increment rather
than treating it as covered by Linux results.

## 8. Completion checklist and remaining dependencies

An implemented case is complete only when it has a project owner, scenario ID,
capability manifest, applicable recovery variants, structural evidence, cleanup
verification, and the specified measurement output. The registry/report must
reconcile all 32 active definitions and 47 named subcases without adding the two
counts or presenting excluded R4/R5 as missing work.

Implementation still needs to establish the feasibility of controlled DNS/SPN,
TCP/pipe pending states, real write backpressure, integrated-authentication
generation, TLS resumption flights, and coordinator boundaries. These are
engineering/environment dependencies, not questions to resolve by selecting
arbitrary timing thresholds. Assign each unsupported or deferred cell a concrete
reason and follow-up owner when the implementation work is scheduled.

Initial numerical runs will establish distributions, instrumentation overhead,
useful concurrency ranges, and lab capacity. They may refine workload parameters
and diagnostic collection, but do not change the structural pass/fail policy.
