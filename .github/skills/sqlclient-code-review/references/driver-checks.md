# Driver checks

Use this as a risk index, not a checklist to paste into reviews. Load the relevant
repository instruction guide for each touched area. Paths below are repository-relative;
confirm them on the reviewed revision before searching or claiming a missing update.

## Repository map

| Surface | Starting point |
| --- | --- |
| Driver implementation | `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/` |
| Public API and build selection | `src/Microsoft.Data.SqlClient/ref/`, implementation/ref `.csproj` files and imported build files |
| Pool implementations | `ConnectionPool/ChannelDbConnectionPool.cs`, `WaitHandleDbConnectionPool.cs`, and their shared helpers under the driver directory |
| Wire and transport | `TdsParser*`, `ManagedSni/`, native interop, and platform-specific counterparts |
| Behavior switches | `LocalAppContextSwitches.cs` and `.github/instructions/features.instructions.md` |
| Tests | `src/Microsoft.Data.SqlClient/tests/{UnitTests,FunctionalTests,ManualTests,PerformanceTests,StressTests}/` |
| Related products | `src/Microsoft.Data.SqlClient.Extensions/`, `src/Microsoft.Data.SqlClient/add-ons/`, `src/Microsoft.SqlServer.Server/` |
| Build and distribution | `build.proj`, `src/Directory.Build.*`, `Directory.Packages.props`, `Versions.props`, `.nuspec`, `eng/pipelines/` |

Some instructions describe legacy `netcore/ref/` and `netfx/ref/` layouts. Inspect
the active ref project and compile inputs: the reviewed checkout may instead use
the unified `ref/` directory. Do not demand edits to nonexistent/inactive files.
Likewise, distinguish implementation TFMs, test-only runtimes, reference/stub
assemblies, and frameworks that can compile but cannot execute on the host.
New driver implementation belongs in the unified `src/` tree; do not revive
legacy `netcore/src/` or `netfx/src/` paths.

## API and behavioral compatibility

Read `.github/instructions/api-design.instructions.md`.

- Compare shipped public/protected signatures and their active reference surface,
  including attributes, optional defaults, overload resolution, type forwarding,
  and interface/base-class contracts. A `public` member on an internal type is not
  automatically a public API addition. Respect the ref nullable-annotation convention.
- Check return values, conversion rules, exception type/metadata and validation
  timing, events, connection-string parsing/aliases, defaults, and post-failure
  state. Adding an optional parameter to an existing signature can still break
  binary compatibility; adding an overload can affect source binding.
- For a parity claim, identify the intended SqlClient baseline and relevant source
  path. When measured, record driver package/build, framework, OS/SNI, switches,
  and server configuration. Source inspection alone is not a measured comparison.
  Other drivers can suggest cases, but their API contracts do not override ADO.NET
  or SqlClient compatibility. Trace wrapper/ORM behavior separately from the driver.
- When adding a setting, check its setter/getter, builder/parser aliases, serialized
  connection string, and effective runtime behavior as applicable. Follow entry-point
  normalization before claiming a validator branch is reachable. Respect intentional
  canonicalization and hidden-secret behavior rather than requiring literal symmetry.
- Separate an intentional documented change from an accidental regression.
  For non-security breaking changes, check the repository's compatibility opt-out
  policy. Inspect existing switches and their initialization/caching before
  suggesting a new one; verify default and compatibility paths.
- Check directly affected XML docs and `doc/` examples. Do not propose unrelated
  API redesign, blanket obsoletion, or new APIs to tidy an internal implementation.
  Verify worked examples' byte counts, encoding expansion, units, and boundary
  arithmetic; an incorrect example can conceal the very case the change must handle.

## Async, cancellation, timeouts, and retries

- Compare `Open`/`OpenAsync`, command execution, reader operations, and bulk copy
  variants when touched. Compare observable outcomes and cleanup, not identical
  implementations. Preserve documented differences in exception timing and legacy
  APM behavior.
- Follow cancellation registration, completion, disposal, and callback lifetime.
  Identify a concrete interleaving for double completion, lost wakeup, use-after-return,
  deadlock, or a callback acting on a recycled connection. Do not demand general
  thread safety for unsupported concurrent use of public objects.
- Distinguish pool wait/login timeout, command network-read timeout, caller
  cancellation, and retry budgets. Check units, zero/infinite semantics, overflow,
  and remaining time at retry/redirect boundaries against the actual contract.
  `CommandTimeout` is not a universal wall-clock budget for consuming all rows.
- Verify ATTENTION/acknowledgment and pending-result cleanup before reuse when the
  changed path can interrupt a command. Cancellation does not prove server rollback.
- Keep initial connection retries, idle recovery, failover, and command replay
  distinct. Check retry eligibility, bounded attempts, partial results, transaction
  state, and duplicate writes. Do not introduce command retries on the assumption
  that a transport failure means nothing executed.

## Pooling, transactions, and resource ownership

Read `.github/instructions/connection-pooling.instructions.md`.

- Trace acquisition, ownership transfer, return, clear, prune, and disposal.
  Track connection counts/leases and waiter notifications on all affected exits.
  Verify both pool implementations only when shared contracts or selection are affected.
- Check pool-key equality/hash consistency and separation by relevant connection
  configuration, identity, credentials, and token/provider callbacks. Trace the
  implementation rather than assuming equivalent strings share a pool.
- Verify that reset and transaction/session cleanup take effect before the next
  borrower's command uses the connection. Reset may be deferred until that command
  is sent; its absence in `Close` or checkout alone is not a bug. A canceled or
  errored connection is not necessarily broken: determine whether this path
  restores usability or must discard it.
- Follow local/ambient/distributed transaction enlistment, completion, stasis,
  and disposal. Ensure clearing a pool does not make checked-out connections
  reusable contrary to the clear operation's contract.
- Track every rented buffer, socket, native handle, cancellation registration,
  and semaphore lease through exceptional paths. An asynchronous consumer must
  finish before its memory/handle is released; returning memory once is not enough
  if it is returned too early.
- For native SNI/interop changes, check allocator/free pairing, pinning and callback
  lifetime, pointer widths, struct layout, and alignment against the actual native
  contract. Use existing safe-handle/marshalling helpers; a Rust or ODBC ownership
  convention is not evidence of this ABI's requirements.

## TDS, transport, and data fidelity

Read `.github/instructions/tds-protocol.instructions.md`; use MS-TDS for the
specific token, negotiated feature, and protocol revision.

- Check packet/token lengths, offsets, endianness, overflow, partial reads/writes,
  EOF, and token ordering. A socket read is not a complete TDS packet or value.
  Determine what state is retained if parsing pends and resumes.
- Check remaining-byte accounting, buffer boundaries, large/PLP values, and
  sequential access. Rejecting malformed input must not leave a connection
  incorrectly available for the next operation.
- For serialization failures, distinguish bytes buffered locally from bytes already
  sent. Follow the actual flush boundary and cancellation/drain/discard path to
  determine the effect on the command, transaction, and connection. A short payload
  may cover only pre-send failure; exercise relevant boundaries after partial sends.
  Do not use payload size alone to infer PLP routing: inspect type metadata and
  the selected serializer.
- Preserve metadata/value agreement: null versus empty, `DBNull`/SQL null types,
  precision/scale, rounding/truncation, collation/encoding, and type-specific
  representations. Review reader, parameter, bulk-copy, and TVP paths as affected.
- Verify feature negotiation and older-server behavior for JSON, vector, Always
  Encrypted, session recovery, or other versioned features. Do not infer support
  merely from the SQL Server product version or an enum value.
- Trace MARS logical-session isolation and physical-connection state where shared
  code changes. Follow managed/native SNI error, cancellation, and ownership
  contracts; do not assume a managed-path fix reaches the native path.

## Authentication, encryption, and diagnostics

- Check that identity/token caches preserve their intended scope and refresh
  semantics. Confirm TLS negotiation and certificate/hostname validation across
  affected encryption modes; do not weaken defaults to make a test pass.
- Check Always Encrypted key/plaintext lifetimes and provider/cache isolation
  when touched. Distinguish column encryption from transport encryption.
- Trace logging arguments, not just message templates: no passwords, tokens,
  credential-bearing connection strings, or sensitive packet/value dumps.
  Preserve diagnostic correlation and balanced operation events on error paths
  without introducing material allocations when tracing is disabled.
- Trace user-controlled values used in SQL metadata queries and identifiers;
  do not label all string construction as injection without following trust,
  parameterization, and escaping. Route suspected vulnerabilities through the
  private process in `CONTRIBUTING.md`, not public review details.

## Performance, build, and test evidence

- For changed hot paths, inspect allocations/copies, lock duration/contention,
  blocking I/O, round trips, and cache growth/eviction. Use measurements or a
  demonstrated workload-dependent regression, not aesthetic claims that one
  abstraction is faster. Preserve ownership correctness in pooling optimizations.
  A latency/throughput claim needs timing under a representative workload;
  allocation or state-machine size alone is not a timing result. Read helper/API
  implementations before alleging allocations: a borrowed view, scan, copy, and
  allocation are different costs.
- Verify the actual OS/framework constants and file inclusion conditions; do not
  derive compiled coverage from filename suffixes alone. For packaging changes,
  examine `ReferenceType=Project` versus `Package`, reference/runtime assets,
  native SNI dependencies, and affected sibling products. Compilation alone
  does not prove that a shipped package contains the right assets.
- Prefer a regression test that fails under the old behavior and covers the
  promised fix. Select relevant sync/async, boundary, cancellation/failure, and
  subsequent-reuse cases rather than requiring every combination for every diff.
  For pool reuse, verify physical connection identity or use deterministic pool
  instrumentation; the same pool key does not establish that the same connection
  was reused. Test discard instead when the affected path must reject a broken
  connection.
- Check that test setup reaches the intended branch and cannot pass on an unrelated
  exception or cleanup performed by setup. Expected values should not reuse the
  faulty conversion/units being tested. Exercise the public entry point when
  normalization or routing matters. A successful SQL round trip alone does not
  prove a specific RPC/token sequence; use existing simulated-TDS infrastructure
  for wire assertions where supported, and name any remaining observation gap.
- Inspect actual fixtures and skip conditions. Unit/functional tests can use
  simulated servers or local infrastructure; directory names do not establish
  that a test is offline. Use manual SQL Server tests for server-dependent behavior,
  with the required feature/version/authentication configuration.
- Use existing `DataTestUtility` configuration and test helpers, unique database
  object names, and reliable cleanup. Avoid global switch/pool/console state
  leaking across parallel tests; use deterministic synchronization rather than
  sleeps to prove race conditions. Preserve repository test-documentation rules.
- Name the exact missing assertion or environment in a coverage request.
  A zero-match filter or skipped test is not a pass. Do not invent credentials,
  change shared servers, or run a PR's build/scripts with publishing credentials.
