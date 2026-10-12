# OpenAsync latency dependency catalog

## Purpose and scope

This catalog identifies operations that can introduce externally determined latency
or internal contention during `SqlConnection.OpenAsync`. It is a reference for
designing async-performance experiments, not a benchmark result or an API guarantee.

Source baseline: `c7ab087c26b8c3c29275cca671799138b9c31987`.
Cataloged on October 12, 2026. Implementation observations below describe that
revision; method names are included because source line numbers can change.

The scope starts at the public `OpenAsync` invocation and ends at task completion,
with cancellation/failure cleanup and consumer scheduling called out separately.
Connection construction, connection-string assignment, and token acquisition
performed by the application before invocation are outside this boundary.
Initialization costs belong inside the measurement only when the open actually
triggers them.

Dependencies are classified as:

- **External:** OS services, files, network, SQL Server, and authentication or
  transaction infrastructure.
- **Application:** callbacks, event handlers, other connection owners, and custom
  providers.
- **Internal:** locks, queues, admission limits, retry delays, and scheduling.

These categories can overlap. "Uncontrolled latency" does not imply an unlimited
wait: a timeout may bound an operation without making its duration predictable.
Timeout coverage and cancellation propagation must be established independently.

Native SNI, OS security/TLS services, authentication SDKs, and arbitrary callbacks
are opaque dependency trees from this repository's perspective. Their boundaries
are cataloged here, but their internal operations are not exhaustively enumerated.

## Flow

```text
OpenAsync
  -> retry-provider/configuration setup and diagnostics
  -> identity, pool selection, and acquisition
       -> reusable connection: validation -> activation
       -> new physical connection:
            endpoint discovery
            transport establishment
            TLS / TDS prelogin
            authentication and login response
            optional routing: repeat against another endpoint
            MARS initialization
            activation / transaction enlistment
  -> connection state notification
  -> task completion
  -> consumer continuation (a separate observation boundary)
```

This is a branch-aware outline, not a list of steps executed by every open.
Retries can repeat portions of the flow. Strict encryption performs TLS before
TDS prelogin; ordinary TDS encryption negotiates through prelogin before enabling
TLS.

Catalog IDs are stable reference names for future experiments. New entries should
receive new IDs rather than renumbering existing entries.

## Entry and connection acquisition

Source groups: [entry](#source-entry), [factory](#source-factory),
[pools](#source-pools), and [configuration](#source-configuration).

| ID | Operation | Dependency and latency source | Conditions / implementation notes |
| --- | --- | --- | --- |
| OA-01 | Lazy configuration/provider initialization | External files and assembly loading; application provider constructors; internal initialization locks | Retry-provider access precedes `InternalOpenAsync`. Other components initialize when first needed. Distinguish cold-process from warmed-up behavior. |
| OA-02 | Open-before diagnostics | Application diagnostic subscribers and logging/listener implementations | Synchronous callbacks before acquisition can extend invocation-to-task-return time. |
| OA-03 | Identity and pool lookup | External Windows identity/token APIs; internal pool-group locks | Integrated authentication can require identity lookup. Pool creation, replacement, and clearing add contention. |
| OA-04 | Wait for an available pooled connection | Application owners returning connections; transactions retaining capacity; internal reclamation | V2 awaits the idle channel when capacity is unavailable. The legacy pending-open path uses a dedicated thread and blocking waits. |
| OA-05 | Wait for permission to create a connection | Internal legacy creation semaphore or configured V2 creation rate limiter; other in-flight creations | Distinct from waiting for an existing connection. V2 falls back to the idle-channel wait when a creation permit is unavailable. |
| OA-06 | Nonpooled creation queue | Internal bounded task slots, earlier opens, and scheduling | `Pooling=false` does not eliminate admission delay. The factory chains creation tasks and uses `LongRunning` continuations. |
| OA-07 | Validate or reject a pooled connection | External OS socket/native SNI health check; internal eviction and disposal | Dead, expired, or unusable connections can convert a pool hit into physical creation. Managed TCP liveness uses a short `Socket.Poll`, not a SQL ping. |
| OA-08 | Recover from a pool shutdown/replacement race | Internal concurrent clearing/retirement and backoff | The factory can use incremental `Thread.Sleep` before trying another pool, potentially on the invoking thread. |

Physical creation is generally dispatched away from the caller, but remains
synchronous work. V2 calls the synchronous connection factory from a worker task.
The legacy pool also has a dead-connection replacement branch that can create a
connection during its initial acquisition attempt. Returning a task promptly
therefore does not establish that the underlying work is nonblocking.

## Endpoint discovery and transport establishment

These operations apply when a new physical connection is required.
Source groups: [factory](#source-factory), [login](#source-login),
[transport](#source-transport), and [native SNI](#source-native).

| ID | Operation | Dependency and latency source | Conditions / implementation notes |
| --- | --- | --- | --- |
| OA-09 | Resolve a configured server alias | External Windows registry | May change endpoint and protocol. Cached alias information avoids some lookups. |
| OA-10 | Initialize networking/security components | External DLL loading, native SNI initialization, OS security packages; internal initialization serialization | Primarily first-use costs, located at the first use of each component rather than one universal initialization step. |
| OA-11 | Resolve or start LocalDB | External registry, LocalDB runtime DLL, and database process startup | Starting an instance is distinct from connecting to its resulting pipe. |
| OA-12 | Discover a SQL Express user instance | External preliminary SQL Express connection and login response | `User Instance=true` can require a discovery connection followed by a connection to the returned instance. |
| OA-13 | Resolve host addresses | External OS resolver, DNS, local configuration/cache | Managed SNI calls synchronous `Dns.GetHostAddresses`. Discovery and final transport setup can each perform resolution. |
| OA-14 | Discover a named-instance or DAC port | External SQL Server Browser/SSRP over UDP | Conditional on port discovery. Includes sending, receiving, and address-selection behavior. SQL Browser normally uses UDP port 1434. |
| OA-15 | Establish TCP | External OS sockets, network, firewall, server listener, and connection backlog | Managed SNI uses `Socket.Connect` and blocking `Socket.Select` for finite-timeout establishment. |
| OA-16 | Establish named-pipe or other native transport | External OS IPC, remote pipe services, or native transport provider | Managed named pipes call synchronous `Connect` despite asynchronous stream options. Native SNI owns additional transport-specific behavior, including shared memory. |
| OA-17 | Resolve the authentication SPN | External DNS canonical-name lookup | Managed integrated authentication can call `Dns.GetHostEntry` after transport creation; an explicit SPN avoids this lookup. |

Sequential/parallel address attempts, multi-subnet behavior, .NET Framework TNIR,
and cached-address fallback affect attempt count and duration. These should be
treated as variants of discovery/establishment, not assumed to be identical
between managed/native SNI or frameworks.

## Prelogin, TLS, and authentication

Source groups: [protocol](#source-protocol), [login](#source-login),
[transport](#source-transport), [native SNI](#source-native), and
[authentication](#source-authentication).

| ID | Operation | Dependency and latency source | Conditions / implementation notes |
| --- | --- | --- | --- |
| OA-18 | Send prelogin request | External socket/pipe write capacity and network backpressure | Writes can wait; latency is not confined to response reads. |
| OA-19 | Receive prelogin response | External SQL Server prelogin processing and transport delivery | Synchronous parser consumption. An instance-failure response can trigger reconnection and another prelogin exchange. |
| OA-20 | TLS handshake | External server TLS implementation, network exchanges, and OS cryptography | Managed SNI uses synchronous authentication or blocks on an async helper. Native SNI exposes a handshake-completion wait. Strict encryption moves this before TDS prelogin. |
| OA-21 | Certificate validation | External OS trust/chain services and certificate material; configured certificate file | `Server Certificate` can cause synchronous file loading. Additional certificate-service activity depends on platform/policy; a revocation-network request is not assumed for every open. |
| OA-22 | Generate integrated-authentication tokens | External SSPI/Negotiate, credentials, and potentially Kerberos/domain infrastructure; application custom SSPI provider | Security-provider calls are synchronous. Initial token generation and later challenge responses can both cross this boundary. |
| OA-23 | Send LOGIN7 and receive authentication challenges | External SQL Server and transport writes/reads | SQL, integrated, and federated authentication follow different exchanges. |
| OA-24 | Acquire or refresh a federated token | Application authentication provider or `AccessTokenCallback`; external token service on cache miss/refresh | Driver code uses `Task.Run(...).GetAwaiter().GetResult()` around async acquisition, occupying the login thread while waiting. A token supplied before open does not imply acquisition during open. |
| OA-25 | Provider-internal authentication work | External Entra/managed-identity endpoints, credential sources, broker/browser; application callbacks/user interaction; provider cache synchronization | Depends on mode and cache state. Azure Identity/MSAL own further dependencies. Interactive/device-code authentication can depend on a person. |
| OA-26 | Continue authentication exchanges | External SQL Server and selected authentication system | Additional SSPI challenges can generate tokens and writes. Federated authentication includes receiving server auth information, acquiring/sending a token, and processing the response. |
| OA-27 | Consume the complete login response | External SQL Server login processing, database availability/startup, login triggers, resource pressure, and transport delivery | `CompleteLogin` runs the parser through completion, not merely until a login acknowledgment arrives. Server-internal causes are possibilities, not separate client calls. |

## Routing and activation

Source groups: [login](#source-login), [protocol](#source-protocol),
[pools](#source-pools), and [transport](#source-transport).

| ID | Operation | Dependency and latency source | Conditions / implementation notes |
| --- | --- | --- | --- |
| OA-28 | Follow server routing/redirection | External destination endpoint and its discovery, transport, TLS, and login dependencies | Repeats physical setup against another endpoint; not merely a local address substitution. |
| OA-29 | Enable MARS and create the default session | External SNI/transport control-packet writes; internal session setup and synchronization | Conditional on MARS. Posting its background receive is not itself a wait for application data. |
| OA-30 | Automatically enlist in an ambient transaction | External `System.Transactions` and SQL Server transaction-manager requests | Applies to pooled activation and eligible nonpooled opens. Delegated transaction initialization can require a server round trip. |
| OA-31 | Promote or join a distributed transaction | External transaction coordinator/DTC and SQL Server | May obtain DTC whereabouts, export/promote a transaction, and propagate its cookie/token. Platform and transaction type determine the path. |
| OA-32 | Unenlist from an old transaction | External SQL Server transaction state | Activation can propagate a null transaction cookie instead of only updating local state. |
| OA-33 | Restore or reassert session isolation level | External SQL batch execution and response | This baseline has an opt-in isolation reset and a same-transaction isolation reassertion path. Both can execute synchronously during pooled activation. |

A warm pooled open can perform server I/O before returning its task: both pools'
immediate-acquisition paths activate the connection inline.

Ordinary connection reset is generally prepared locally and carried on a subsequent
request. It should not automatically be counted as a separate
`sp_reset_connection` round trip on every open. An activation batch can cause that
pending reset to be sent during open.

## Retries, completion, and cleanup

Source groups: [entry](#source-entry), [factory](#source-factory),
[pools](#source-pools), [login](#source-login), and [retry](#source-retry).

| ID | Operation | Dependency and latency source | Conditions / implementation notes |
| --- | --- | --- | --- |
| OA-34 | Connection/login/failover retry delays | Internal backoff and scheduling; repeated external connection work | Several physical-login retry paths use `Thread.Sleep`. |
| OA-35 | Authentication retry or timeout recovery | Application/provider retry policy; internal backoff; repeated external authentication/login | Token acquisition and login recovery can introduce additional attempts. |
| OA-36 | Configurable retry provider | Application retry predicates, `Retrying` handlers, custom provider; internal delay | The built-in async provider uses `Task.Delay`; callback work is separately uncontrolled. |
| OA-37 | Connection state and informational notifications | Application `StateChange`/`InfoMessage` handlers | Delay the path that invokes them; informational events can also occur while processing server responses. |
| OA-38 | Complete the open and resume consumers | Internal scheduler availability, locks, cancellation-registration disposal; application diagnostics/continuations | Pending opens use completion dispatch. Distinguish task becoming terminal from consumer resumption; inline continuations can occupy the completing thread after task state changes. |
| OA-39 | Failure/cancellation cleanup | Internal locks and resource ownership; external handle disposal; still-running acquisition | Includes returning or destroying a connection acquired after cancellation. A canceled public task does not prove physical work or resource recovery has finished. |

## Cross-cutting effects and experiment boundaries

GC, JIT, thread preemption, CPU quotas, runtime thread injection, and machine
contention can affect every stage. They are runtime effects rather than additional
protocol operations. Background pool warmup/pruning and credential-cache activity
can likewise change the state seen by a measured open.

The principal experimental boundaries are pool admission, discovery, transport
establishment, TLS, authentication exchanges, login completion, transaction
activation, and final task handoff.

For each experiment, the catalog ID should be accompanied by the active
framework/SNI/pool/authentication configuration, cache state, timing boundary, and
evidence that the intended operation was reached. Where possible, a fixture should
observe arrival, hold readiness, then release/cancel/fail the dependency. A fixed
delay alone does not establish that callers waited at the intended boundary.

Replacing a blocking dependency with an async gate can remove the behavior under
investigation. Component isolation and controlled real-dependency experiments
therefore answer different questions.

## Source references

Repository-relative paths below are rooted at the repository root. The line numbers
refer to the source baseline above; method names are the primary navigation aids.

### Source: entry

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnection.cs`:
  `RetryLogicProvider` (150), `InternalOpenAsync` (2020), `OpenAsyncRetry` (2188),
  `TryOpenInner` (2366), and `SetInnerConnectionEvent` (2590).

### Source: factory

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnectionFactory.cs`:
  `TryGetConnection` (384), `CreateConnection` (646), and
  `CreateReplaceConnectionContinuation` (838).

### Source: pools

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\ConnectionPool\ChannelDbConnectionPool.cs`:
  `TryGetConnection` (915), `OpenNewInternalConnection`,
  `TryGetPooledConnectionInline` (1469), `GetInternalConnection` (1528),
  and `PrepareConnection`.
- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\ConnectionPool\WaitHandleDbConnectionPool.cs`:
  `WaitForPendingOpen`, `TryGetConnection` (874, 952), and `PrepareConnection`.
- The same directory: `DbConnectionPoolGroup.cs`, `DbConnectionPoolIdentity.cs`,
  and `TransactedConnectionPool.cs`.

### Source: login

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs`:
  constructor login/retry loop (452), `Activate` (2119), `AttemptOneLogin` (2287),
  `CompleteLogin` (2369), `Enlist` (2510), `ReassertSessionIsolationLevel`,
  `EnlistNonNull` (2663), `GetFedAuthToken` (3016), `LoginNoFailover` (3435),
  `LoginWithFailover` (3764), `ResetConnection` (4203),
  `ResetSessionIsolationLevel`, and `ResolveExtendedServerName` (4368).

### Source: protocol

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParser.cs`:
  `Connect` (359), `EnableMars` (670), `SendPreLoginHandshake`,
  `EnableSsl` (936), `ConsumePreLoginHandshake` (996), and `ProcessSSPI`.
- The same directory: `TdsParserStateObject.cs` (`ReadSyncOverAsync` call sites,
  `IsConnectionAlive`) and `TdsParserStaticMethods.cs` (`AliasRegistryLookup`).

### Source: transport

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\ManagedSni\SniProxy.netcore.cs`:
  `CreateConnectionHandle`, `GetSqlServerSPNs`, and `CreateTcpHandle`.
- The same directory: `SniTcpHandle.netcore.cs` (`Connect`, `ParallelConnect`,
  `EnableSsl`, `CheckConnection`), `SniNpHandle.netcore.cs` (constructor),
  `SsrpClient.netcore.cs` (`GetPortByInstanceName`, `GetDacPortByInstanceName`),
  `LocalDB.netcore.cs` (`GetLocalDBConnectionString`),
  `SniHandle.netcore.cs` (`AuthenticateAsClient`),
  `SniCommon.netcore.cs` (`LoadValidationCertificate`, `GetDnsIpAddresses`),
  and `SniMarsHandle.netcore.cs` (session control packets).

### Source: native

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObjectNative.cs`:
  `CreatePhysicalSNIHandle` (138), `ReadSyncOverAsync`, and
  `WaitForSSLHandShakeToComplete`.
- The same directory: `TdsParserSafeHandles.cs` (SNI initialization/open).
- `src\Microsoft.Data.SqlClient\src\Interop\Windows\Sni\SniNativeWrapper.cs`:
  `SniOpenSyncEx`, `SniReadSyncOverAsync`, and `SniWaitForSslHandshakeToComplete`.

### Source: authentication

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SSPI\NativeSspiContextProvider.cs`:
  `LoadSSPILibrary` and `GenerateContext`.
- The same directory: `NegotiateSspiContextProvider.cs` (`GenerateContext`).
- `src\Microsoft.Data.SqlClient.Extensions\Azure\src\ActiveDirectoryAuthenticationProvider.cs`:
  `AcquireTokenAsync` (201), `TryAcquireTokenSilent`,
  `AcquireTokenInteractiveDeviceFlowAsync`, and `GetTokenAsync`.

### Source: configuration

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlAuthenticationProviderManager.cs`:
  static initialization, configuration reading, and Azure provider loading.
- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Reliability\AppConfigManager.cs`:
  configuration-section access.

### Source: retry

- `src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Reliability\Common\SqlRetryLogicProvider.cs`:
  `ExecuteAsync` (149) and `ApplyRetryingEvent`.
