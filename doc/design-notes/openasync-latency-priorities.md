# OpenAsync latency investigation priorities

## Purpose

This note prioritizes the dependencies in the
[OpenAsync latency dependency catalog](openasync-latency-catalog.md) for
async-performance experiments. Catalog IDs refer to that document's stable
`OA-01` through `OA-39` entries and its source baseline.

Recorded on October 12, 2026. These are investigation priorities, not a ranking of
measured bottlenecks or established performance regressions.

## Prioritization criteria

Highest priority goes to recurring waits that are common, can last a long time,
and multiply across concurrent opens. A dependency becomes especially important
when waiting for it occupies a thread, delays other opens, or extends the time
before `OpenAsync` returns its task.

The initial workload assumption is a warmed-up service. One-time initialization
is relatively lower priority, but it can still delay multiple concurrent first
opens behind shared initialization locks.

## Highest-priority dependencies

| Priority | Dependency | Catalog IDs | Rationale and conditions |
| --- | --- | --- | --- |
| 1 | DNS and name resolution | OA-13, OA-17 | A physical open has multiple resolution opportunities: discovery, transport establishment, and SPN canonicalization. Routing and retries can repeat them. Managed paths use synchronous resolver calls. OS caching can reduce actual DNS traffic, so cached and uncached behavior need separate treatment. |
| 2 | Pool availability and creation admission | OA-04, OA-05, OA-06 | Determines behavior under concurrency even with a healthy server and network. Exhausted pools depend on other callers releasing connections; creation limits can make one slow physical open delay others. Includes the nonpooled creation queue. |
| 3 | Transport establishment | OA-15, OA-16 | Required for every new physical connection. Unreachable addresses, dropped packets, and slow listeners can create long waits. Address attempts, failover, and routing can multiply the work. Managed TCP currently waits through synchronous socket operations. |
| 4 | Prelogin and login response waits | OA-18, OA-19, OA-23, OA-26, OA-27 | Every new physical connection crosses protocol request/response boundaries. Server pressure or delayed responses can hold many opens pending simultaneously. These boundaries isolate waiting after transport establishment succeeds. Include write backpressure as well as response reads. |
| 5 | Authentication acquisition and negotiation | OA-22, OA-24, OA-25 | Can involve additional network services, credential discovery, token refresh, or user interaction. Delays can be substantial and correlated across opens. The driver blocks the login thread while awaiting federated-token acquisition. Top-tier for Entra/integrated-auth workloads; not applicable in the same way to SQL-password authentication. |
| 6 | TLS handshake and certificate validation | OA-20, OA-21 | Recurs on encrypted physical connections and includes network exchanges and platform validation. TLS session resumption and certificate caching change cost without eliminating the dependency boundary. |
| 7 | Pooled activation that performs server I/O | OA-30, OA-31, OA-32, OA-33 | Can occur on a warm pool hit before `OpenAsync` returns its task. Transaction enlistment and isolation-level requests can affect caller responsiveness without creating a connection. High priority for transaction-heavy workloads. |

The ordering is a starting point, not universal. Authentication and transaction
activation should move upward for workloads that use those paths heavily.
Physical-connection dependencies apply on pool misses, pool growth, replacement,
or nonpooled opens, rather than on every successful warm checkout.

## Multipliers and companion measurements

### Retries and routing

Routing and retry behavior (`OA-28`, `OA-34`, `OA-35`, `OA-36`) amplify the
dependencies above. Characterize a single attempt first, then test repeated
attempts and backoff separately. This distinguishes one slow dependency from
repeated external work or blocking between attempts.

### Completion scheduling and cancellation recovery

Completion scheduling and cancellation/resource recovery (`OA-38`, `OA-39`)
belong in every high-priority experiment. Observe whether dependency latency also
delays unrelated work, task completion, cancellation, or the return of usable
capacity. A canceled task alone does not establish that physical work has stopped.

## Lower initial priorities

For a typical warmed-up service, initially defer:

- Cold configuration, provider, and networking/security initialization
  (`OA-01`, `OA-10`).
- Server-alias registry access (`OA-09`).
- LocalDB startup and SQL Express user-instance discovery (`OA-11`, `OA-12`).
- Application diagnostics and event-handler overhead (`OA-02`, `OA-37`).

These remain relevant to their specific workloads. Setup-like work is not
necessarily one-time: configured certificate-file loading, for example, can recur
and remains part of the TLS priority above.

## Recommended first experimental set

1. **DNS resolution:** separate cached and uncached resolution, accounting for
   multiple resolution opportunities within one open.
2. **Saturated-pool waiting:** hold capacity unavailable and observe pending opens,
   thread consumption, and progress after capacity is released.
3. **TCP establishment:** control establishment progress separately from later
   protocol exchanges.
4. **Stalled prelogin/login response:** establish transport, reach the selected
   protocol boundary, and hold its response pending.

Together these cover repeated resolution, shared-capacity waiting, OS/network
waiting, and server waiting without requiring a particular external authentication
provider or transaction configuration.

Experiment design should preserve the dependency behavior under investigation:
replacing blocking I/O with an asynchronous gate can conceal the blocking.
