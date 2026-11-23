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
`CertificateTestWithTdsServer` uses `makepfxcert.ps1 -SkipTrust` and corresponding cleanup to
leave root trust stores unchanged; certificates must remain untrusted for rejection cases.
