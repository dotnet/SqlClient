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
| ConfigurationTest | Current and legacy real app.config handlers still compile through the public `ConfigurationSection` base type and resolve through driver type forwards; initializer/configuration beat discovered Azure and application registration; application registration replaces Azure; invalid provider/initializer/unsupported provider failures remain cached and traced after tracing is enabled; disabled switches leave explicit registration usable. |
| RuntimeVersionTest | Replacement driver retains `$(SqlClientAssemblyVersion)` but reports informational version 7.0.1. Both enabled and disabled bootstrap switches reject it; tracing after the first failure reports exact versions and upgrade/restart guidance. The runner restores the real driver and configuration in `finally` blocks. |
| PublishTest | References Abstractions and Azure but never uses Azure types in source. linux-x64 trim/AOT publishes treat C#, ILLink and ILC warnings as errors. Execution proves discovery absent and explicit registration succeeds; the runner verifies no Azure DLL or Azure runtime dependency remains. XML documentation is not a runtime assembly. |

The focused `TestAuthenticationConfiguration` target runs just current/legacy
configuration; `TestAuthenticationRuntimeVersions` adds the replacement checks.
`TestAuthentication` runs six configuration and two runtime scenarios. Optional
`AuthenticationPublish=true` adds two publishes and executions.

On Windows use `TestFramework=net462` and omit publishing. Linux may cross-build
net462 using `BuildAuthenticationTests`, but **cannot execute its Windows runtime
checks**. `BuildTests` includes `BuildAuthenticationTests`; the solution includes
all three fixtures. Direct runner invocation requires packages prepared in advance.

The CI stage runs both reference modes on Linux and Windows, self-building its
packages with per-job caches and unique versions rather than assuming artifacts.
