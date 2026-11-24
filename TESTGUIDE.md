# Simulated server test harness

The unit project's `SimulatedServerTests` use local TDS endpoints, not an installed SQL Server.
Select a Linux-supported framework from the unit project and filter by
`FullyQualifiedName~SimulatedServerTests` when validating the harness. Include quarantined cases
explicitly; skipped tests are not passes.

Use `GatedTdsServer` to withhold pre-login responses instead of using sleeps to choose the
failure phase. Prefer numeric loopback addresses unless DNS is under test.
Declare `SimulatedServerPoolScope` after servers and before connections: it isolates and clears
only the scenario's pool before server teardown. Verify pool reuse with physical connection IDs.

Blocking protocol handlers use dedicated workers. Fault quotas are reserved before processing
responses. Disposal drains workers and reports unexpected failures. Transport startup and socket
closure share a lock; the worker-drain wait is outside it so cleanup can finish.
`ServerLifecycleTests` and `ServerEndPointConnectionTests` cover these contracts. The repeated
lifecycle disposal regression retains its flaky annotation and probation metadata.

Negative TLS scenarios opt into `TdsServerArguments.ExpectTlsHandshakeFailure`. Missing
certificates, missing keys, and protocol-handler failures still fail disposal.
I/O exceptions are tolerated only when identified at the physical transport boundary, not merely
by their exception type. Handler and log-writer I/O failures, including failures while logging
another error, are reported during disposal.
Derived simulators can call `AbortConnection(session)` from a pre-login handler and return an empty
response to reset only that physical socket. This does not join the worker or stop the listener;
normal session cleanup still runs, and unexpected handler failures after the abort remain errors.
Retry regressions intentionally abort the first pre-login session and gate the second distinct
`SessionID` before replying; assert the phase-specific pre-login/login counts and pending open
before releasing the gate. Use an explicit configurable network-error retry policy and assert
its retry event; `ConnectRetryCount` does not retry a pre-login reset. Pool-wait regressions
explicitly select the legacy wait-budget policy, hold the occupied slot beyond that budget,
and verify actual pool-wait admission before release; elapsed time or transport counts alone are
not evidence of successful pool waiting.
`CertificateTestWithTdsServer` uses `makepfxcert.ps1 -SkipTrust` and corresponding cleanup to
leave root trust stores unchanged; certificates must remain untrusted for rejection cases.
