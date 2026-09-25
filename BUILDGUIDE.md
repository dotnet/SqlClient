<!-- markdownlint-disable MD024 -->

# Build Guide for Microsoft.Data.SqlClient and Related Packages

This document provides details on how to build the Microsoft.Data.SqlClient package and the other related packages
contained within this repository.

## Prerequisites

### .NET SDK

Projects in this repository require the .NET SDK to be installed in order to build. For the exact version required for
building the current version, see [global.json](global.json). Downloads for .NET SDK can be found at
[.NET Downloads](https://dotnet.microsoft.com/en-us/download/dotnet).

The .NET SDK contains support for building for previous versions of .NET, including support for building .NET Framework
on operating systems that do not support .NET Framework. As such, it is not necessary to install an older
.NET SDK to compile the projects. Running tests requires their target runtimes: install
.NET 8 and .NET 9 alongside the .NET 10 SDK. Running .NET Framework tests requires Windows and a compatible
.NET Framework runtime. On Linux and macOS, select a supported .NET test framework with `-p:TestFramework=net8.0`,
`net9.0`, or `net10.0` rather than running every declared test framework.

### Miscellaneous

**PowerShell** is included as a .NET local tool in this repository. Running `dotnet tool restore`
(see below) will make it available via `dotnet tool run pwsh -- <args>`. Note that `pwsh` is not
added to PATH — it must be invoked through `dotnet tool run`. Build targets handle this
automatically; manual invocation is only needed for ad-hoc scripting.

The **NuGet** binary is optional for inspection and feed-management workflows, but build and packaging flows in this
repository are run through `dotnet build` against `build.proj`.

### .NET Tools

This repository uses .NET local tools (e.g. PowerShell) that must be restored before building. Run the following from the repository root:

```bash
dotnet tool restore
```

## Developer Workflow

Once you've cloned the repository and made your changes to the codebase, it is time to build, test, and optionally
package the project. The `build.proj` file provides convenient targets to accomplish these tasks.

> [!NOTE]
> Although every effort has been made to make building and testing work in your IDE of choice, some quirks in behavior
> may be noticed, possibly severe. All official build and test infrastructure uses the `build.proj` entrypoint, and it
> is recommended that `build.proj` is used for local development, as well.

<!-- Avoid MD028 (blank line inside a blockquote - these are 2 separate blockquotes. -->

> [!TIP]
> This section is not exhaustive of all targets or parameters to `build.proj`. Complete documentation is available in
> [`build.proj`](build.proj).

### Building Projects

From the root of your repository, run `dotnet build` against `build.proj` with a build target, following this pattern:

```text
dotnet build build.proj -t:<build_target> [optional_parameters]
```

Since `build.proj` is the only project file in the repo root, it can be omitted when building from
the root:

```text
dotnet build -t:<build_target> [optional_parameters]
```

The command-line examples below will assume that `build.proj` is selected by default and will omit
it from the `dotnet build` command.

If no target is specified, `build.proj` runs the `BuildAll` target by default, which builds all
driver projects, tests, samples, and tools for their declared target frameworks. To build only the driver
projects, specify `-t:BuildDriver` explicitly.

The implementation is built from `src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj`,
which targets `net462`, `net8.0`, and `net9.0`. The modern .NET implementation is shared across Windows,
Linux, and macOS; there are no separate Windows and Unix build targets. Reference assemblies are built
from `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.csproj`.

The following build targets can be used to build the following projects. All targets will implicitly build any other
projects they depend on.

| `<build_target>`              | Description                                                                     |
|-------------------------------|---------------------------------------------------------------------------------|
| `BuildAbstractions`           | Builds Microsoft.Data.SqlClient.Extensions.Abstractions                         |
| `BuildAkvProvider`            | Builds Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider           |
| `BuildAll`                    | Builds driver projects, tests, samples, and tools for their declared target frameworks (default target) |
| `BuildAzure`                  | Builds Microsoft.Data.SqlClient.Extensions.Azure                                |
| `BuildDriver`                 | Builds all driver projects for all platforms                                    |
| `BuildLogging`                | Builds Microsoft.Data.SqlClient.Internal.Logging                                |
| `BuildSamples`                | Builds the sample projects under `doc/samples/`                                 |
| `BuildSqlClient`              | Builds the implementation, reference, and unsupported-platform assemblies       |
| `BuildSqlClientImpl`          | Builds the shared implementation assemblies of Microsoft.Data.SqlClient         |
| `BuildSqlClientNotSupported`  | Builds the "unsupported platform" assemblies for Microsoft.Data.SqlClient       |
| `BuildSqlClientRef`           | Builds the reference assemblies for Microsoft.Data.SqlClient                    |
| `BuildSqlServer`              | Builds Microsoft.SqlServer.Server                                               |
| `BuildTests`                  | Builds the driver test projects for their declared target frameworks            |
| `BuildTools`                  | Builds auxiliary tool/app projects and their test projects                      |
| `Clean`                       | Removes build and test output directories                                       |

A selection of parameters for build targets in `build.proj` can be found below:

<!-- markdownlint-disable MD060 -->

| `[optional_parameter]`            | Allowed Values                   | Default   | Description                                                                                                                                   |
|-----------------------------------|----------------------------------|-----------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `-p:Configuration=`               | `Debug`, `Release`               | `Debug`   | Build configuration                                                                                                                           |
| `-p:PackageVersionSqlClient=`     | `major.minor.patch[-prerelease]` | `[blank]` | Version to assign to the SqlClient family (`Microsoft.Data.SqlClient`, `Internal.Logging`, `Extensions.Abstractions`, `Extensions.Azure`, and the AKV Provider all share it). Assembly and file versions are derived from this, if it is provided. See Versioning for more details |
| `-p:PackageVersionSqlServer=`     | `major.minor.patch[-prerelease]` | `[blank]` | Version to assign to `Microsoft.SqlServer.Server`, which is versioned separately from the SqlClient family. |

<!-- markdownlint-enable MD060 -->

For driver dependencies and extensions, build output is placed in `artifacts/<package_name>/<configuration>/<tfm>`. `<package_name>`
is the full name of the package, `<configuration>` is the build configuration, and `<tfm>` is the target framework
moniker. SqlClient's implementation output is placed in
`artifacts/Microsoft.Data.SqlClient/<reference_type>-<configuration>/<tfm>`. The unsupported-platform assemblies are placed
in `artifacts/Microsoft.Data.SqlClient.notsupported/<reference_type>-<configuration>/<tfm>`, and the reference assemblies are
placed in `artifacts/Microsoft.Data.SqlClient.ref/<reference_type>-<configuration>/<tfm>`.
`<reference_type>` is `Project` by default, or `Package` for package-mode builds. There is no OS subdirectory.

#### Examples

Build everything (all projects, tests, samples, and tools) using the default target:

```bash
dotnet build
```

Build only the driver projects:

```bash
dotnet build -t:BuildDriver
```

Build Microsoft.Data.SqlClient in Release configuration:

```bash
dotnet build -t:BuildSqlClient -p:Configuration=Release
```

Build a specific version of Microsoft.Data.SqlClient.Extensions.Abstractions (Abstractions is part of the
SqlClient family, so its version is set via the family parameter `PackageVersionSqlClient`):

```bash
dotnet build -t:BuildAbstractions -p:PackageVersionSqlClient=7.1.0
```

### Testing Projects

This section provides a summary and brief example of how to execute tests for projects in this repository. **For more
information about test procedures, including config file setup, see [TESTGUIDE.md](TESTGUIDE.md).**

From the root of your repository, run `dotnet build` against `build.proj` with a test target, following this pattern:

```text
dotnet build -t:<test_target> [optional_parameters]
```

| `<test_target>`            | Description                                                                                                                                         |
|----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `Test`                     | Runs the driver and extension test targets. Without `TestFramework`, all declared frameworks are attempted, including .NET Framework. _This takes considerable time and requires configured servers_. |
| `TestAbstractions`         | Runs all tests for Microsoft.Data.SqlClient.Extensions.Abstractions                                                                                 |
| `TestAkvProvider`          | Runs the unit test project for Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.                                                      |
| `TestAzure`                | Runs all tests for Microsoft.Data.SqlClient.Extensions.Azure                                                                                        |
| `TestSqlClient`            | Runs the unit, functional, and manual test projects for Microsoft.Data.SqlClient.                                                                    |
| `TestSqlClientFunctional`  | Runs the "functional" test project for Microsoft.Data.SqlClient. These do not require a live SQL Server.                                            |
| `TestSqlClientManual`      | Runs the "manual" test project for Microsoft.Data.SqlClient. These are generally integration tests against live servers.                            |
| `TestSqlClientUnit`        | Runs the unit test project for Microsoft.Data.SqlClient. These are a mix of unit tests and integration tests against simulated servers.             |
| `TestPackageCompatibility` | Runs the PackageCompatibility tool tests using Microsoft.Testing.Platform.                                                                         |
| `TestPackageValidator`     | Runs the PackageValidator tool tests using Microsoft.Testing.Platform.                                                                             |

The `Test` aggregate does not include the tool tests, performance benchmarks, stress runner, or
PowerShell script tests. The tool test targets use their own runner options rather than the driver
test parameters below; see the [PackageCompatibility](tools/PackageCompatibility/README.md) and
[PackageValidator](tools/PackageValidator/README.md) guides.

> [!TIP]
> In project-reference mode, test targets automatically build their dependencies. In package mode,
> prepare the referenced packages first; see [Package Mode Builds](#package-mode-builds).

A selection of parameters for test targets in `build.proj` relevant to common developer workflows can be found below:

<!-- markdownlint-disable MD060 -->

| `[optional_parameter]` | Default Value                                            | Description                                                                                                                                                                                         |
|------------------------|----------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `-p:Configuration=`    | `Debug`                                                  | Build configuration. Can be `Debug` or `Release`.                                                                                                                                                   |
| `-p:DotnetPath=`       | `[blank]`                                                | Path to `dotnet` binary to run the test project. This is useful for running tests against x86 platform on a x86_64 machine. Path must end with `\` or `/`.                                          |
| `-p:TestBlameTimeout=` | `10m`                                                    | How long to wait on a test before timing it out. Use `0` to disable hang timeouts.                                                                                                                  |
| `-p:TestFilters=`      | `category!=failing&category!=flaky&category!=interactive` | Filters to select the xUnit tests to execute. Use `none` to disable this filter; test-set selection and conditional skips still apply.                                                              |
| `-p:TestFramework=`    | `[blank]`                                                | Target framework to execute; blank attempts every framework declared by the project. Select an installed runtime on the current host.                                                              |
| `-p:TestSet=`          | `[blank]`                                                | The `TestSqlClientManual` project is very large and is split into multiple sets that can be executed individually. This parameter allows selecting between test sets: `1`, `2`, `3`, and `AE`. |

<!-- markdownlint-enable MD060 -->

Unless `TestFilters=none` is specified, unsigned runs also exclude the `signed` category.
`TestSet` still limits manual tests when `TestFilters=none` is used. Quote filters containing shell
metacharacters such as `&`, `|`, or parentheses.

#### Examples

Run Microsoft.Data.SqlClient unit tests:

```bash
dotnet build -t:TestSqlClientUnit -p:TestFramework=net8.0
```

Run Microsoft.Data.SqlClient manual test set 2:

```bash
dotnet build -t:TestSqlClientManual -p:TestFramework=net8.0 -p:TestSet=2
```

Run Microsoft.Data.SqlClient functional tests against x86 dotnet:

```powershell
dotnet build -t:TestSqlClientFunctional -p:TestFramework=net8.0 -p:DotnetPath='C:\path\to\dotnet\x86\'
```

Run all Microsoft.Data.SqlClient.Extensions.Azure unit tests, including interactive, but excluding failing tests:

```bash
dotnet build -t:TestAzure -p:TestFramework=net8.0 -p:TestFilters="category!=failing"
```

Run Microsoft.Data.SqlClient functional tests against net8.0 runtime:

```bash
dotnet build -t:TestSqlClientFunctional -p:TestFramework=net8.0
```

### Packaging Projects

Just like building and testing the various projects in this repository, packaging the projects into NuGet packages is
also handled by `build.proj`. From the root of your repository, run `dotnet build` against `build.proj` with a pack target,
following this pattern:

```text
dotnet build -t:<pack_target> [optional_parameters]
```

| `<pack_target>`    | Description                                                                         |
|--------------------|-------------------------------------------------------------------------------------|
| `Pack`             | Packages all driver, dependency, and extension packages listed below.                |
| `PackAbstractions` | Packages the Microsoft.Data.SqlClient.Extensions.Abstractions package               |
| `PackAkvProvider`  | Packages the Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider package |
| `PackAzure`        | Packages the Microsoft.Data.SqlClient.Extensions.Azure package                      |
| `PackLogging`      | Packages the Microsoft.Data.SqlClient.Internal.Logging package                      |
| `PackSqlClient`    | Packages the Microsoft.Data.SqlClient package                                       |
| `PackSqlServer`    | Packages the Microsoft.SqlServer.Server package                                     |

> [!TIP]
> For convenience, the Pack targets will automatically build the target project and any dependencies.

A selection of parameters for pack targets in `build.proj` relevant to common developer workflows can be found below:

<!-- markdownlint-disable MD060 -->

| `[optional_parameter]`             | Default Value | Allowed Values        | Description                                                                                                                                                    |
|------------------------------------|---------------|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `-p:Configuration=`                | `Debug`       | `Debug`, `Release`    | Configuration to build or, with `PackBuild=false`, to package from existing output.                                                                             |
| `-p:PackBuild=`                    | `true`        | `true`, `false`       | Whether or not to build the project before packing. If `false`, project must be built using the same parameters.                                               |
| `-p:PackageVersionSqlClient=`       | `[blank]`     | eg. `7.1.0-dev123`    | Version to assign to the entire SqlClient family (`Microsoft.Data.SqlClient`, `Internal.Logging`, `Extensions.Abstractions`, `Extensions.Azure`, and the AKV Provider — they all share the SqlClient version). If `PackBuild` is `true`, the assembly and file versions are derived from this version. See Versioning for more details. |
| `-p:PackageVersionSqlServer=`       | `[blank]`     | eg. `1.1.0-dev123`    | Version to assign to `Microsoft.SqlServer.Server`, which is versioned separately from the SqlClient family. |

<!-- markdownlint-enable MD060 -->

For `PackSqlClient`, the SqlClient nuspec uses the same `SqlClientPackageVersion` as the lower bound
for its family dependencies (Abstractions and Logging), with the next major version as the exclusive
upper bound. A single `-p:PackageVersionSqlClient=<version>` controls the package version and those
dependency ranges. `Microsoft.SqlServer.Server` uses a separate range based on `-p:PackageVersionSqlServer=<version>`.

If omitted, `PackSqlClient` computes these versions from `Versions.props` using the current `BuildNumber` and `BuildSuffix` context.

#### Examples

Package Microsoft.Data.SqlClient.Internal.Logging into a NuGet package:

```bash
dotnet build -t:PackLogging
```

Package Microsoft.Data.SqlClient:

```bash
dotnet build -t:PackSqlClient
```

Package a specific version of Microsoft.Data.SqlClient.Extensions.Abstractions (set via the family parameter
`PackageVersionSqlClient`):

```bash
dotnet build -t:PackAbstractions -p:PackageVersionSqlClient=7.1.0
```

Package Microsoft.Data.SqlClient.Extensions.Azure from existing build output, without rebuilding.
First build it with the same configuration, reference mode, version, and signing parameters:

```bash
dotnet build -t:BuildAzure
dotnet build -t:PackAzure -p:PackBuild=false
```

### Release Source Link Symbols

To reproduce release symbol generation locally, set `BuildForRelease` in the environment before
packing. `build.proj` launches child `dotnet` processes, so passing only
`-p:BuildForRelease=true` to the orchestrator does not enable it in those processes.

```powershell
$env:BuildForRelease = 'true'
dotnet build build.proj -t:PackSqlClient -p:Configuration=Release
Remove-Item Env:\BuildForRelease
```

The `.nupkg` and matching `.snupkg` are written to
`artifacts/Microsoft.Data.SqlClient/Project-Release/`. Inspect them together with
[PackageValidator](tools/PackageValidator/README.md) or NuGet Package Explorer.

To fail validation on missing source coverage or non-portable source paths:

```powershell
dotnet run --project tools\PackageValidator\src\PackageValidator.csproj -- `
  artifacts\Microsoft.Data.SqlClient\Project-Release `
  --fail-on missing-source-link --fail-on untracked-source --fail-on non-deterministic-source-path
```

Tracked source paths in the PDBs must match the Source Link document map; generated sources must
be embedded. Keep the repository root configured in `RepositoryInfo.targets` as the source root:
adding a separate `src/` root without source-control metadata can remap tracked files to `/_1/`
while Source Link only maps `/_/`. NuGet Package Explorer reports this as
**Contains untracked sources (obj)** for both Source Link and Deterministic, even when all `obj`
sources are embedded and the compiler's deterministic flag is enabled.

## Versioning

Versioning can be accomplished by using a mix of different parameters to the `build.proj` targets:
`PackageVersionSqlClient` (or `PackageVersionSqlServer`), `BuildNumber`, and `BuildSuffix`. Using these in different
combinations can generate appropriate package, assembly, and file versions for different scenarios. For most developer
workflows, it is not necessary to specify any of these parameters - appropriate versions based on the source-declared next release
will be generated automatically. This section primarily exists to document the various parameters, their effects, and
the scenarios they can be useful for.

All packages in the **SqlClient family** (`Microsoft.Data.SqlClient`, `Internal.Logging`, `Extensions.Abstractions`,
`Extensions.Azure`, and the AKV Provider) share a single version, set via `-p:PackageVersionSqlClient`.
`Microsoft.SqlServer.Server` is versioned separately via `-p:PackageVersionSqlServer`.

The SqlClient family version is defined in `src/Microsoft.Data.SqlClient/Versions.props` (and SqlServer's in its own
`Versions.props`), which declares a "default" version — the next version to release. For the table below, we assume this
is "1.2.3".

| `PackageVersion` | `BuildNumber` | `BuildSuffix` | Package Version  | Assembly Version | File Version  | Scenario                                                   |
|------------------|---------------|---------------|------------------|------------------|---------------|------------------------------------------------------------|
| N/A              | N/A           | N/A           | `1.2.3-dev`      | `1.0.0.0`        | `1.2.3.0`     | Standard developer scenario                                |
| `9.8.7`          | N/A           | N/A           | `9.8.7`          | `9.0.0.0`        | `9.8.7.0`     | Developer is building a specific version of the package    |
| `9.8.7-preview1` | N/A           | N/A           | `9.8.7-preview1` | `9.0.0.0`        | `9.8.7.0`     | Developer is building a pre-release version of the package |
| N/A              | `1234`        | N/A           | `1.2.3`          | `1.0.0.0`        | `1.2.3.1234`  | Automated pipelines building GA releases                   |
| N/A              | `1234`        | `ci`          | `1.2.3-ci.1234`  | `1.0.0.0`        | `1.2.3.1234`  | Automated pipelines building non-prod releases             |

When the source version already contains a prerelease suffix, it is preserved: `1.2.3-preview1`
becomes `1.2.3-preview1-dev` locally or `1.2.3-preview1-ci.1234` with the CI parameters above.

---

## Package Mode Builds

The above documentation is the default mode of operation, and is the recommended mode for most developers. However,
`build.proj` supports "package mode" builds. In this mode, instead of projects depending on other projects, they
depend on NuGet packages. This mode is useful for verifying that packages work with each other, especially in automated
build scenarios. For completeness, and debugging of automated builds, this section documents behavior of "package mode".

To switch to "package mode", set the `ReferenceType` parameter in `build.proj` to `Package`. And, optionally, include
one or both of the following parameters:

- `PackageVersionSqlClient` — the version for the entire SqlClient family.
- `PackageVersionSqlServer` — the version for `Microsoft.SqlServer.Server`.

These parameters pull double duty. In targets where a package is being built, the parameter sets the version of the
package. In targets where a package is being referenced, the parameter sets the version of the referenced package.
Because the SqlClient family shares one version, `PackageVersionSqlClient` covers every family package, whether it is
being built or referenced.

If these parameters are not specified, versions are calculated from `Versions.props` using
`BuildNumber` and `BuildSuffix`, as described above.

The [NuGet.config](NuGet.config) defines a local feed at `packages/` and a governed feed for external
dependencies and published packages. In package mode, product build and pack targets prepare required sibling
packages automatically, and pack targets copy their output to the local feed. This orders dependency
packing; it does not invalidate previously restored packages. Manual copying is not needed.
Before running tests in package mode, pack the packages they reference; test targets do not prepare that feed.
Set `-p:SkipDependencyPack=true` when supplying prebuilt dependencies, as CI does. For pack targets,
`-p:PackBuild=false` also skips dependency packing so previously built or signed binaries are not rebuilt.

### Repeated local builds

NuGet treats each package ID/version as immutable. Repacking changed source under an existing
version, including the default `-dev` version, can leave downstream projects using old binaries from
the global-packages cache or an up-to-date assets file. This also applies to the automatic dependency
packs for `PackAbstractions`, `PackAzure`, and `PackAkvProvider`.

Use new versions for each changed build, and reuse those exact versions for subsequent test and
no-build pack commands. When rebuilding SqlServer too, give it a new version as well. Prefer the
default project-reference mode for routine source iteration. The orchestrator does not clear shared
NuGet caches or silently change version overrides.

### Examples

From a PowerShell shell, create a unique local version pair and pack SqlClient with its dependencies.
The base versions below are examples; choose the bases appropriate for the branch:

```powershell
$buildId = [guid]::NewGuid().ToString('N')
$sqlClientVersion = "8.0.0-preview1-local-$buildId"
$sqlServerVersion = "1.1.0-preview1-local-$buildId"

dotnet build -t:PackSqlClient `
  -p:ReferenceType=Package `
  -p:PackageVersionSqlClient=$sqlClientVersion `
  -p:PackageVersionSqlServer=$sqlServerVersion
```

In the same shell, run functional tests against the versions built above:

```powershell
dotnet build -t:TestSqlClientFunctional `
  -p:ReferenceType=Package `
  -p:PackageVersionSqlClient=$sqlClientVersion `
  -p:PackageVersionSqlServer=$sqlServerVersion `
  -p:TestFramework=net8.0
```

The same version arguments apply to `PackAbstractions`, `PackAzure`, and `PackAkvProvider`.
Generate a new `$buildId` and recompute both versions after editing source; do not overwrite the
previously restored versions.

Manual test prerequisites and configuration are covered in [TESTGUIDE.md](TESTGUIDE.md#manual-test-prerequisites).

## Using Managed SNI on Windows

Managed SNI can be enabled on Windows by enabling the below AppContext switch:

`Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows`

## Set truncation on for scaled decimal parameters

Scaled decimal parameter truncation can be enabled by enabling the below AppContext switch:

`Switch.Microsoft.Data.SqlClient.TruncateScaledDecimal`

## Enabling row version null behavior

`SqlDataReader` returns a `DBNull` value instead of an empty `byte[]`. To enable the legacy behavior, you must enable the following AppContext switch on application startup:

`Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior`

## Suppressing TLS security warning

When connecting to a server, if a protocol lower than TLS 1.2 is negotiated, a security warning is output to the console. This warning can be suppressed on SQL connections with `Encrypt = false` by enabling the following AppContext switch on application startup:

`Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning`

## Collecting Code Coverage

Driver test targets in `build.proj` collect coverage by default using the repository's
runsettings. Set `-p:TestCodeCoverage=false` to disable collection.

### Using VSTest

Select a test project and a target framework with an installed runtime:

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  -f net8.0 --collect:"Code Coverage"
```

### Using Coverlet Collector

This optional collector is not configured in the repository. The test project must reference
`coverlet.collector` before using `--collect:"XPlat Code Coverage"`; otherwise VSTest cannot find it.

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  -f net8.0 --collect:"XPlat Code Coverage"
```

## Run Performance Tests

The performance tests live here: `src\Microsoft.Data.SqlClient\tests\PerformanceTests\`

They can be run from the command line by following the instructions below.

Launch a shell and change into the project directory:

PowerShell:

```pwsh
> cd src\Microsoft.Data.SqlClient\tests\PerformanceTests
```

Bash:

<!-- markdownlint-disable MD014 -->

```bash
$ cd src/Microsoft.Data.SqlClient/tests/PerformanceTests
```

<!-- markdownlint-enable MD014 -->

### Create Database

Create an empty database for the benchmarks to use.  This example assumes
a local SQL Server instance using SQL authentication:

```text
$ sqlcmd -S localhost -U "<user>"
1> create database [sqlclient-perf-db]
2> go
1> quit
```

Omit `-P` so `sqlcmd` prompts for the password instead of placing it in shell history.

The default `runnerconfig.jsonc` expects a database named `sqlclient-perf-db`,
but you may change the config to use any existing database.  The benchmarks
create and drop their own tables (typically prefixed with `perf_`) in this
database; other existing tables are left untouched.

### Configure Runner

Configure the benchmarks by editing the `runnerconfig.jsonc` file directly in the
`PerformanceTests` directory with an appropriate connection string and benchmark
settings:

```jsonc
{
  "ConnectionString": "Server=tcp:localhost; Integrated Security=true; Initial Catalog=sqlclient-perf-db;",
  "UseManagedSniOnWindows": false,
  "UseOptimizedAsyncBehaviour": true,
  "WaitForProfiler": false,
  "UseNativeMemoryAndETWProfiler": false,
  "Benchmarks":
  {
    "SqlConnectionRunnerConfig":
    {
      "Enabled": true,
      "LaunchCount": 1,
      "IterationCount": 50,
      "InvocationCount":30,
      "WarmupCount": 5,
      "RowCount": 0
    }
    // Other benchmark configurations omitted.
  }
}
```

Individual benchmarks may be enabled or disabled, and each has several
benchmarking options for fine tuning.

The top-level flags control global runner behavior:

| Flag | Description |
| --- | --- |
| `UseManagedSniOnWindows` | Enables the managed SNI implementation on Windows instead of native SNI. |
| `UseOptimizedAsyncBehaviour` | Enables packet multiplexing and other async optimizations in SqlClient. |
| `WaitForProfiler` | Pauses at startup and prints the process ID so you can attach an external profiler (e.g. `dotnet-trace`) before benchmarks run. |
| `UseNativeMemoryAndETWProfiler` | Attaches the `NativeMemoryProfiler` and `EtwProfiler` BenchmarkDotNet diagnosers. Windows only; has no effect on other OSes. |

Some benchmarks (e.g. `DataTypeReaderRunner`) also
read per-type test values from `datatypes.json` in the `PerformanceTests`
directory. Like `runnerconfig.jsonc`, this file's location can be overridden
with the `DATATYPES_CONFIG` environment variable.

By default, the benchmarks load `runnerconfig.jsonc` and `datatypes.json` from
the current working directory. Run the commands below from the `PerformanceTests`
directory, or set the configuration environment variables to absolute file paths.
Configuration-only edits do not require rebuilding the benchmark binaries.

Optionally, to keep local configuration out of your git workspace,
copy `runnerconfig.jsonc` to a new file, make your edits
there, and then specify the new file with the RUNNER_CONFIG environment
variable. The same approach works for `datatypes.json` via the
`DATATYPES_CONFIG` environment variable.

PowerShell:

```pwsh
> New-Item -ItemType Directory -Force $HOME\.configs | Out-Null
> copy runnerconfig.jsonc $HOME\.configs\runnerconfig.jsonc

# Make edits to $HOME\.configs\runnerconfig.jsonc

# You must set the RUNNER_CONFIG environment variable for the current shell.
> $env:RUNNER_CONFIG="${HOME}\.configs\runnerconfig.jsonc"
```

Bash:

```bash
$ mkdir -p ~/.configs
$ cp runnerconfig.jsonc ~/.configs/runnerconfig.jsonc

# Make edits to ~/.configs/runnerconfig.jsonc

# Set RUNNER_CONFIG to use the external configuration.
$ export RUNNER_CONFIG=~/.configs/runnerconfig.jsonc
```

### Run Benchmarks

All benchmarks must be compiled and run in **Release** configuration.

PowerShell:

```pwsh
> dotnet run -c Release -f net9.0
```

Bash:

```bash
# Omit RUNNER_CONFIG if you exported it earlier, or if you're using the
# configuration in the current PerformanceTests directory.
$ dotnet run -c Release -f net9.0

$ RUNNER_CONFIG=~/.configs/runnerconfig.jsonc dotnet run -c Release -f net9.0
```
