# Sequential Unicode Decoding Regression

See [BUILDGUIDE.md](BUILDGUIDE.md) for this release's SDK, build, and SQL Server test setup.

`SqlUnicodeEncodingTests` checks raw UTF-16LE code-unit preservation, partial consumption,
offsets, zero-capacity conversions, and unchanged input bytes. Run it without a database:

```powershell
dotnet test src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj --framework net8.0 --filter "FullyQualifiedName~SqlUnicodeEncodingTests" --blame-hang-timeout 10m
```

Repeat with `--framework net9.0` and, on Windows, `--framework net462` to cover this
release's test targets. The .NET and .NET Framework projects link the same decoder source.

With SQL Server test configuration in place, run the Unicode round-trip regression:

```powershell
dotnet test src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj --framework net8.0 -p:TestSet=3 --filter "FullyQualifiedName~UnicodeParameterAndReaderRoundTrip_PreservesWorldwideText" --blame-hang-timeout 10m
```

The integration test covers buffered and streamed parameters, synchronous and asynchronous
readers, single-character and mixed-size reads, destination offsets, zero-length reads, and
EOF over packet-spanning `nvarchar(max)` text. It uses a connection-scoped temporary table
and skips when connection strings are not configured. Confirm the selected cases actually
execute; skipped tests do not validate the round trip.

SQL Server may run remotely; verifying the big-endian decoder path requires running the test
client on a real big-endian runtime (for example, s390x). Passing on x64 verifies only the
little-endian path.

## Simulated server tests

The unit project's `SimulatedServerTests` use local TDS endpoints, not an installed SQL Server.
Run this suite with `TestSqlClientUnit` and a `FullyQualifiedName` filter containing
`SimulatedServerTests`. Validate discovery and the resulting TRX case counts when narrowing a run;
the default category filter excludes quarantined tests.

For new scenarios, use explicit protocol gates rather than sleeps to decide which login phase
fails. `GatedTdsServer` can withhold pre-login responses while keeping its port and existing sessions
alive. This separates failover from post-login failures and allows pool clearing to be verified
with physical connection identity. Use a numeric loopback address when DNS is not under test.

Declare `SimulatedServerPoolScope` after the servers and before the connections in pooling scenarios.
It assigns a unique pool identity and clears that pool before the ephemeral servers are disposed.
Keep retry/error/partner assertions explicit; transport attempt counts are not proof of pool reuse.

The simulator runs blocking handlers on dedicated workers. Fault budgets are reserved before
response processing, and disposal must finish owned workers and report unexpected handler failures.
Transport initialization and socket closure share a lock so shutdown cannot invalidate a
`TcpClient` while its worker acquires the stream or initializes the parser. The worker-drain
wait stays outside that lock, allowing a stopped worker to finish its cleanup.
Negative TLS scenarios can set `TdsServerArguments.ExpectTlsHandshakeFailure` to explicitly expect
authentication failure during TLS negotiation. Missing certificates/private keys and exceptions from
protocol handlers still fail disposal; the default remains strict. I/O exceptions are tolerated only
when identified at the physical transport boundary, not merely by their exception type. Handler and
log-writer I/O failures (including failures while logging another error) are reported during disposal.
Derived simulators can call `AbortConnection(session)` from a pre-login handler and return an empty
response to reset only that physical socket. This does not join the worker or stop the listener;
normal session cleanup still runs, and unexpected handler failures after the abort remain errors.
Retry regressions intentionally abort the first pre-login session and gate the second distinct
`SessionID` before replying; assert the phase-specific pre-login/login counts and pending open
before releasing the gate. Use an explicit configurable network-error retry policy and assert
its retry event; `ConnectRetryCount` does not retry a pre-login reset. On 6.1, retry regressions
abort the first transport after observing ClientHello instead of before the pre-login response,
avoiding the older Debug provider-EOF formatter without suppressing driver assertions.
Pool-wait regressions
explicitly select the legacy wait-budget policy, hold the occupied slot beyond that budget,
and verify actual pool-wait admission before release; elapsed time or transport counts alone are
not evidence of successful pool waiting.
Assert the expected client failure
as well, and keep success controls so simulator setup errors cannot masquerade as certificate rejection.
`ServerLifecycleTests` covers these contracts. Validate changes on both supported frameworks and
on native and managed SNI, including broader simulator consumers before removing a flaky trait.

The manual `CertificateTestWithTdsServer` matrix requires an untrusted self-signed certificate.
Its setup and cleanup scripts use `-SkipTrust` to leave root trust stores unchanged, and the test
asserts `UntrustedRoot` before connecting. Installing that certificate as trusted invalidates
the negative cases; explicit certificate pinning and `TrustServerCertificate` provide the success controls.
