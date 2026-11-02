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
