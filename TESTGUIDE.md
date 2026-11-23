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
protocol handlers still fail disposal; the default remains strict. Assert the expected client failure
as well, and keep success controls so simulator setup errors cannot masquerade as certificate rejection.
`ServerLifecycleTests` covers these contracts. Validate changes on both supported frameworks and
on native and managed SNI, including broader simulator consumers before removing a flaky trait.

The manual `CertificateTestWithTdsServer` matrix requires an untrusted self-signed certificate.
Its setup and cleanup scripts use `-SkipTrust` to leave root trust stores unchanged, and the test
asserts `UntrustedRoot` before connecting. Installing that certificate as trusted invalidates
the negative cases; explicit certificate pinning and `TrustServerCertificate` provide the success controls.
