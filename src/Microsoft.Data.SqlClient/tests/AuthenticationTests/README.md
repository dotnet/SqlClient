# Authentication scenarios

These executable fixtures are intentionally separate from ordinary xUnit tests.
They require neither SQL Server nor identity-service access. Each scenario runs
in a fresh process so bootstrap state, switches and app.config cannot leak.

Use `build.proj` from the repository root:

```bash
dotnet build build.proj -t:TestAuthentication -p:Configuration=Release -p:TestFramework=net10.0 -p:ReferenceType=Project -p:AuthenticationPublish=true -p:PackageVersionSqlClient=8.0.0-stack.6
dotnet build build.proj -t:TestAuthentication -p:Configuration=Release -p:TestFramework=net10.0 -p:ReferenceType=Package -p:AuthenticationPublish=true -p:PackageVersionSqlClient=8.0.0-stack.6
```

Set `NUGET_PACKAGES` to an isolated cache and choose a new family version when
rebuilding a different package image. Package mode prepares real local Logging,
Abstractions, SqlServer, SqlClient and Azure packages using standard orchestration;
fixtures reject missing local family packages and verify their restored package assets.
Only use `SkipDependencyPack=true` with already prepared packages of that exact version.
The runner forwards configuration, reference mode, build number/suffix and family/
SqlServer version inputs and freezes the effective family version for all builds.

| Fixture | Checks |
| --- | --- |
| ConfigurationTest | Current and legacy real app.config handlers still compile through the public `ConfigurationSection` base type and resolve through driver type forwards; initializer/configuration beat discovered Azure and application registration; application registration replaces Azure; invalid provider/initializer/unsupported provider entries are traced once without blocking discovery or explicit registration; disabled switches leave explicit registration usable. |
| PublishTest | References Abstractions and Azure but never uses Azure types in source. linux-x64 trim/AOT publishes treat C#, ILLink and ILC warnings as errors. Execution proves discovery absent and explicit registration succeeds; the runner verifies no Azure DLL or Azure runtime dependency remains. XML documentation is not a runtime assembly. |

The focused `TestAuthenticationConfiguration` target runs just current/legacy
configuration. `TestAuthentication` runs six configuration scenarios. Optional
`AuthenticationPublish=true` adds two publishes and executions.

On Windows use `TestFramework=net462` and omit publishing. Linux may cross-build
net462 using `BuildAuthenticationTests`, but **cannot execute its Windows runtime
checks**. Without `TestFramework`, `BuildAuthenticationTests` builds both net462
and net10.0; an explicit value builds only that framework. `BuildTests` includes
`BuildAuthenticationTests`; the solution includes both fixtures. Direct runner
invocation requires packages prepared in advance.

Scenario configuration retains the generated `runtime` settings, including
.NET Framework assembly-binding redirects. The runner restores the original
configuration bytes after execution, including when a scenario fails.

The CI stage runs both reference modes on Linux and Windows, self-building its
packages with per-job caches and unique versions rather than assuming artifacts.

## Registry publishing regression fixture

[RegistryPublishTest](RegistryPublishTest/AuthenticationPublishTest.csproj) preserves
the registry-specific publish checks developed during PR #4821. It references only
the Abstractions project and supplies a synthetic Azure discovery contract; it does
not validate the real Azure package or Package reference mode.

From the repository root, publish each case to a separate, empty output directory:

```bash
dotnet publish src/Microsoft.Data.SqlClient/tests/AuthenticationTests/RegistryPublishTest/AuthenticationPublishTest.csproj -c Release -r linux-x64 -p:ReferenceType=Project -p:EnableReflection=true -o /tmp/registry-reflection
dotnet publish src/Microsoft.Data.SqlClient/tests/AuthenticationTests/RegistryPublishTest/AuthenticationPublishTest.csproj -c Release -r linux-x64 -p:ReferenceType=Project -p:EnableReflection=false -o /tmp/registry-explicit
dotnet publish src/Microsoft.Data.SqlClient/tests/AuthenticationTests/RegistryPublishTest/AuthenticationPublishTest.csproj -c Release -r linux-x64 -p:ReferenceType=Project -p:EnableReflection=false -p:NativeAot=true -p:CppCompilerAndLinker=/usr/bin/gcc -o /tmp/registry-aot
```

Run `Microsoft.Data.SqlClient.Extensions.Azure` from each output directory. Enabled
reflection checks configuration activation, initializer execution and discovery in
trimmed JIT, explicitly rooting the contracts and accepting linker diagnostics.
Disabled reflection checks explicit registration in trimmed JIT and Native AOT.
Publish settings are local project properties to avoid propagating trimming into
the multi-targeted dependency restore.

The enabled-reflection case is expected to fail until the pending PR #4821 change
stops capability guards from unconditionally removing those paths during trimming.
This fixture is deliberately not yet wired into the default runner, solution or CI;
reconcile that integration as the parent PRs merge.
