# SqlClient Dotnet Pack Flow

This document describes how the SqlClient package is produced with dotnet pack in this branch.

## Scope

- Package: `Microsoft.Data.SqlClient`
- Entry target: `PackSqlClient` in `build.proj`
- Pack engine: `dotnet pack` (no `nuget.exe`)

## How to run

Run from repo root:

```bash
dotnet build -t:PackSqlClient -p:Configuration=Release -p:ReferenceType=Project -p:PackBuild=false
```

Optional version override inputs:

- `PackageVersionAbstractions`
- `PackageVersionLogging`

These can still be passed to override sibling package versions, but they are no longer required. If
omitted, `PrepareSqlClientPackNuspec` evaluates the computed `AbstractionsPackageVersion` and
`LoggingPackageVersion` from the sibling projects using the current `BuildNumber` and `BuildSuffix`
context.

## Why a generated nuspec is used

SqlClient packaging still relies on `tools/specs/Microsoft.Data.SqlClient.nuspec` for file mapping
and dependency groups (`lib/*`, `ref/*`, `runtimes/*`, resources, metadata).

In this flow, SDK pack token substitution is reliable for general nuspec properties, but dependency
version token replacement in the nuspec dependency section can fail. To avoid that, the project
generates an intermediate nuspec before `GenerateNuspec`:

- Template nuspec: `tools/specs/Microsoft.Data.SqlClient.nuspec`
- Generated nuspec: `obj/Microsoft.Data.SqlClient.pack.nuspec`
- Replacements applied:
  - `$AbstractionsPackageVersion$` -> `$(AbstractionsPackageVersion)`
  - `$LoggingPackageVersion$` -> `$(LoggingPackageVersion)`
  - `$NuspecVersion$` -> `$(NuspecVersion)`

This keeps layout parity with the existing nuspec while using `dotnet pack` end-to-end.

## Known SDK behavior and repro

Passing all tokens through `NuspecProperties` looks correct, but this command fails on SDK
`10.0.107`:

```bash
dotnet pack src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj --no-build -p:Configuration=Debug -p:ReferenceType=Project -p:NuspecFile=<repo>/tools/specs/Microsoft.Data.SqlClient.nuspec -p:NuspecBasePath=<repo>/tools/specs -p:NuspecProperties="COMMITID=abc;Configuration=Debug;ReferenceType=Project;AbstractionsPackageVersion=1.0.0-dev;LoggingPackageVersion=1.0.0-dev" -p:NuspecVersion=7.1.0-preview1-dev -p:PackageOutputPath=<repo>/artifacts/tmp -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
```

To reproduce this failure in the current branch and exercise the workaround, you can still pass the
direct project properties explicitly:

```bash
dotnet pack src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj --no-build -p:Configuration=Debug -p:ReferenceType=Project -p:AbstractionsPackageVersion=1.0.0-dev -p:LoggingPackageVersion=1.0.0-dev -p:NuspecVersion=7.1.0-preview1-dev -p:PackageOutputPath=<repo>/artifacts/tmp -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
```

Note: `PackageVersionAbstractions` and `PackageVersionLogging` are `build.proj` entrypoint
properties. Direct project pack can instead pass `AbstractionsPackageVersion` and
`LoggingPackageVersion`, or omit them and let the project evaluate sibling defaults.

The `PrepareSqlClientPackNuspec` target will materialize the tokens into an intermediate nuspec,
bypassing the SDK's substitution bug.

Observed error (without workaround):

- `An error occurred while trying to parse the value '' of property 'dependencies' in the manifest file.`
- `'' is not a valid version string.`

The same flow succeeds when dependency tokens are pre-materialized into the intermediate nuspec by
`PrepareSqlClientPackNuspec`.

## Related upstream issues

The behavior appears related to long-standing SDK/NuGet pack substitution issues when using nuspec
files with `dotnet pack`:

- [dotnet/sdk#15482](https://github.com/dotnet/sdk/issues/15482) (open): multiple `NuspecProperties`
  values, only first substituted and others become empty.
- [dotnet/sdk#29661](https://github.com/dotnet/sdk/issues/29661) (closed as duplicate): same symptom
  on SDK 6.0.404.
- [dotnet/sdk#10516](https://github.com/dotnet/sdk/issues/10516) (closed): `dotnet pack` with nuspec
  not filling tokens like `$version$`.
- [dotnet/sdk#15407](https://github.com/dotnet/sdk/issues/15407) (closed): `$configuration$` empty
  when packing with nuspec.
- [dotnet/sdk#16816](https://github.com/dotnet/sdk/issues/16816) (closed): nuspec token/path
  behavior inconsistencies with `dotnet pack`.

These links do not prove this exact dependency-token timing path is identical, but they strongly
indicate related substitution behavior in the same pack surface area.

## Where pack properties are defined

SqlClient-specific pack defaults are set in:

- `src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj`

The `build.proj` target passes dynamic values only:

- `CommitId`
- `PackageOutputPath`
- plus standard version/reference-type arguments

Note: `NuspecVersion` is derived from `$(Version)` by SDK defaults and does not need to be
explicitly passed.

## Outputs

Expected artifacts:

- `artifacts/Microsoft.Data.SqlClient/<ReferenceType>-<Configuration>/Microsoft.Data.SqlClient.<version>.nupkg`
- `artifacts/Microsoft.Data.SqlClient/<ReferenceType>-<Configuration>/Microsoft.Data.SqlClient.<version>.snupkg`
