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
on operating systems that do not support .NET Framework. As such, it is not necessary to install any version of the
.NET SDK aside from the version specified in [global.json](global.json).

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

```bash
dotnet build build.proj -t:<build_target> [optional_parameters]
```

Since `build.proj` is the only project file in the repo root, it can be omitted when building from
the root:

```bash
dotnet build -t:<build_target> [optional_parameters]
```

The command-line examples below will assume that `build.proj` is selected by default and will omit
it from the `dotnet build` command.

If no target is specified, `build.proj` runs the `BuildAll` target by default, which builds all
projects, tests, samples, and tools for all supported OS combinations. To build only the driver
projects, specify `-t:BuildDriver` explicitly.

The following build targets can be used to build the following projects. All targets will implicitly build any other
projects they depend on.

| `<build_target>`              | Description                                                                     |
|-------------------------------|---------------------------------------------------------------------------------|
| `BuildAbstractions`           | Builds Microsoft.Data.SqlClient.Extensions.Abstractions                         |
| `BuildAkvProvider`            | Builds Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider           |
| `BuildAll`                    | Builds all projects, tests, samples, and tools for all supported OS combinations (default target) |
| `BuildAzure`                  | Builds Microsoft.Data.SqlClient.Extensions.Azure                                |
| `BuildDriver`                 | Builds all driver projects for all platforms                                    |
| `BuildLogging`                | Builds Microsoft.Data.SqlClient.Internal.Logging                                |
| `BuildSamples`                | Builds the sample projects under `doc/samples/`                                 |
| `BuildSqlClient`              | Builds all variants of Microsoft.Data.SqlClient, for all platforms              |
| `BuildSqlClientNotSupported`  | Builds the "unsupported platform" assemblies for Microsoft.Data.SqlClient       |
| `BuildSqlClientRef`           | Builds the reference assemblies for Microsoft.Data.SqlClient                    |
| `BuildSqlClientUnix`          | Builds the Unix-specific implementation binaries of Microsoft.Data.SqlClient    |
| `BuildSqlClientWindows`       | Builds the Windows-specific implementation binaries of Microsoft.Data.SqlClient |
| `BuildSqlServer`              | Builds Microsoft.SqlServer.Server                                               |
| `BuildTests`                  | Builds all test projects for all supported OS combinations                      |
| `BuildTools`                  | Builds auxiliary tool/app projects and their test projects                      |
| `Clean`                       | Removes build and test output directories                                       |

A selection of parameters for build targets in `build.proj` can be found below:

<!-- markdownlint-disable MD060 -->

| `[optional_parameter]`            | Allowed Values                   | Default   | Description                                                                                                                                   |
|-----------------------------------|----------------------------------|-----------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `-p:Configuration=`               | `Debug`, `Release`               | `Debug`   | Build configuration                                                                                                                           |
| `-p:PackageVersion<TargetPackage>=` | `major.minor.patch[-prerelease]` | `[blank]` | Version to assign to the target package, where `<TargetPackage>` can be one of: `['Abstractions', 'Azure', 'AkvProvider', 'Logging', 'SqlClient', 'SqlServer']`. Assembly and file versions are derived from this, if it is provided. See Versioning for more details |

<!-- markdownlint-enable MD060 -->

For most projects, build output is placed in `artifacts/<package_name>/Project-<configuration>/<tfm>`. `<package_name>`
is the full name of the package, `<configuration>` is the build configuration, and `<tfm>` is the target framework
moniker. SqlClient deviates slightly from this convention, since it consists of multiple projects and the
implementation project is OS-specific. Implementation project output is placed in
`artifacts/Microsoft.Data.SqlClient/Project-<configuration>/<os>/<tfm>`. The unsupported platform assemblies are placed
in `artifacts/Microsoft.Data.SqlClient.unsupported/Project-<configuration>/<tfm>`, and the reference assemblies are
placed in `artifacts/Microsoft.Data.SqlClient.ref/Project-<configuration>/<tfm>`.

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

Build v1.2.3 of Microsoft.Data.SqlClient.Extensions.Abstractions:

```bash
dotnet build -t:BuildAbstractions -p:PackageVersionAbstractions=1.2.3
```

### Testing Projects

This section provides a summary and brief example of how to execute tests for projects in this repository. **For more
information about test procedures, including config file setup, see [TESTGUIDE.md](TESTGUIDE.md).**

From the root of your repository, run `dotnet build` against `build.proj` with a test target, following this pattern:

```bash
dotnet build -t:<test_target> [optional_parameters]
```

| `<test_target>`            | Description                                                                                                                                         |
|----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `Test`                     | Runs all tests in the repository for all platforms supported by the host OS. _This will take a considerable amount of time and is not recommended_. |
| `TestAbstractions`         | Runs all tests for Microsoft.Data.SqlClient.Extensions.Abstractions                                                                                 |
| `TestAzure`                | Runs all tests for Microsoft.Data.SqlClient.Extensions.Azure                                                                                        |
| `TestSqlClient`            | Runs all tests for Microsoft.Data.SqlClient.                                                                                                        |
| `TestSqlClientFunctional`  | Runs the "functional" test project for Microsoft.Data.SqlClient. These are a mix of unit and integration tests against live servers.                |
| `TestSqlClientManual`      | Runs the "manual" test project for Microsoft.Data.SqlClient. These are generally integration tests against live servers.                            |
| `TestSqlClientUnit`        | Runs the unit test project for Microsoft.Data.SqlClient. These are a mix of unit tests and integration tests against simulated servers.             |

> [!TIP]
> Test targets will automatically build the projects they depend on. Therefore, it is not necessary to explicitly build
> (eg) SqlClient before executing the (eg) functional tests target.

A selection of parameters for test targets in `build.proj` relevant to common developer workflows can be found below:

<!-- markdownlint-disable MD060 -->

| `[optional_parameter]` | Default Value                                            | Description                                                                                                                                                                                         |
|------------------------|----------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `-p:Configuration=`    | `Debug`                                                  | Build configuration. Can be `Debug` or `Release`.                                                                                                                                                   |
| `-p:DotnetPath=`       | `[blank]`                                                | Path to `dotnet` binary to run the test project. This is useful for running tests against x86 platform on a x86_64 machine. Path must end with `\` or `/`.                                          |
| `-p:TestBlameTimeout=` | `10m`                                                    | How long to wait on a test before timing it out. Use `0` to disable hang timeouts.                                                                                                                  |
| `-p:TestFilters=`      | `category!=failing&category!=flaky&category!=interactive` | Filters to use to select the xUnit tests to execute. Use `none` to run all possible tests.                                                                                                          |
| `-p:TestFramework=`    | `[blank]`                                                | Target framework moniker for the version of .NET to use to execute tests.                                                                                                                           |
| `-p:TestSet=`          | `[blank]`                                                | The `TestSqlClientManual` project is very large and is split into multiple sets that can be executed individually. This parameter allows selecting between test sets: `1`, `2`, `3`, and `AE`. |

<!-- markdownlint-enable MD060 -->

#### Examples

Run Microsoft.Data.SqlClient unit tests:

```bash
dotnet build -t:TestSqlClientUnit
```

Run Microsoft.Data.SqlClient manual test set 2:

```bash
dotnet build -t:TestSqlClientManual -p:TestSet=2
```

Run Microsoft.Data.SqlClient functional tests against x86 dotnet:

```bash
dotnet build -t:TestSqlClientFunctional -p:DotnetPath='C:\path\to\dotnet\x86\'
```

Run all Microsoft.Data.SqlClient.Extensions.Azure unit tests, including interactive, but excluding failing tests:

```bash
dotnet build -t:TestAzure -p:TestFilters=category!=failing
```

Run Microsoft.Data.SqlClient functional tests against net8.0 runtime:

```bash
dotnet build -t:TestSqlClientFunctional -p:TestFramework=net8.0
```

### Packaging Projects

Just like building and testing the various projects in this repository, packaging the projects into NuGet packages is
also handled by `build.proj`. From the root of your repository, run `dotnet build` against `build.proj` with a pack target,
following this pattern:

```bash
dotnet build -t:<pack_target> [optional_parameters]
```

| `<pack_target>`    | Description                                                                         |
|--------------------|-------------------------------------------------------------------------------------|
| `Pack`             | Packages all projects in the repository.                                            |
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
| `-p:Configuration=`                | `Debug`       | `Debug`, `Release`    | Build configuration. Only applies if project and dependencies are being built.                                                                                 |
| `-p:PackBuild=`                    | `true`        | `true`, `false`       | Whether or not to build the project before packing. If `false`, project must be built using the same parameters.                                               |
| `-p:PackageVersion<TargetPackage>=` | `[blank]`     | eg. `1.2.3-dev123`    | Version to assign to the package, where `<TargetPackage>` can be one of: `['Abstractions', 'Azure', 'AkvProvider', 'Logging', 'SqlClient', 'SqlServer']`. If `PackBuild` is `true`, the assembly and file versions will be derived from this version. See Versioning for more details. |

<!-- markdownlint-enable MD060 -->

For `PackSqlClient`, these additional parameters are optional overrides for dependency versions injected into the SqlClient nuspec during pack:

- `-p:PackageVersionAbstractions=<version>`
- `-p:PackageVersionLogging=<version>`

If omitted, `PackSqlClient` computes `AbstractionsPackageVersion` and `LoggingPackageVersion` from sibling projects using the current `BuildNumber` and `BuildSuffix` context.

#### Examples

Package Microsoft.Data.SqlClient.Internal.Logging into a NuGet package:

```bash
dotnet build -t:PackLogging
```

Package Microsoft.Data.SqlClient:

```bash
dotnet build -t:PackSqlClient
```

Package version 1.2.3 of Microsoft.Data.SqlClient.Extensions.Abstractions:

```bash
dotnet build -t:PackAbstractions -p:PackageVersionAbstractions=1.2.3
```

Package Microsoft.Data.SqlClient.Extensions.Azure without building it beforehand:

```bash
dotnet build -t:PackAzure -p:PackBuild=false
```

## Versioning

Versioning can be accomplished by using a mix of different parameters to the `build.proj` targets:
`PackageVersion<TargetProject>`, `BuildNumber`, and `BuildSuffix`. Using these in different combinations, can generate
appropriate package, assembly, and file versions for different scenarios. For most developer workflows, it is not
necessary to specify any of these parameters - appropriate versions based on the latest release will be generated
automatically. This section primarily exists to document the various parameters, their effects, and the scenarios they
can be useful for.

`PackageVersion<TargetProject>` applies to whatever package is being built. For example, if you are building the
Microsoft.Data.SqlClient package, the appropriate parameter is `-p:PackageVersionSqlClient`.

Each package has a `Versions.props` file in its root directory that defines a "default" version. This should be defined
as the latest released version of the package. For the table below, we assume this is "1.2.3".

| `PackageVersion` | `BuildNumber` | `BuildSuffix` | Package Version  | Assembly Version | File Version  | Scenario                                                   |
|------------------|---------------|---------------|------------------|------------------|---------------|------------------------------------------------------------|
| N/A              | N/A           | N/A           | `1.2.3-dev`      | `1.0.0`          | `1.2.3.0`     | Standard developer scenario                                |
| `9.8.7`          | N/A           | N/A           | `9.8.7`          | `9.0.0`          | `9.8.7.0`     | Developer is building a specific version of the package    |
| `9.8.7-preview1` | N/A           | N/A           | `9.8.7-preview1` | `9.0.0`          | `9.8.7.0`     | Developer is building a pre-release version of the package |
| N/A              | `1234`        | N/A           | `1.2.3`          | `1.0.0`          | `1.2.3.1234`  | Automated pipelines building GA releases                   |
| N/A              | `1234`        | `ci`          | `1.2.3-ci1234`   | `1.0.0`          | `1.2.3.1234`  | Automated pipelines building non-prod releases             |

---

## Package Mode Builds

The above documentation is the default mode of operation, and is the recommended mode for most developers. However,
`build.proj` supports "package mode" builds. In this mode, instead of projects depending on other projects, they
depend on NuGet packages. This mode is useful for verifying that packages work with each other, especially in automated
build scenarios. For completeness, and debugging of automated builds, this section documents behavior of "package mode".

To switch to "package mode", set the `ReferenceType` parameter in `build.proj` to `Package`. And, optionally, include
one or more of the following parameters:

- `PackageVersionAbstractions`
- `PackageVersionAkvProvider`
- `PackageVersionAzure`
- `PackageVersionLogging`
- `PackageVersionSqlClient`
- `PackageVersionSqlServer`

These parameters pull double duty. In targets where the package is being built, the parameter sets the version of the
package. In targets where the package is being referenced, the parameter sets the version of the package that is being
referenced.

If these parameters are not specified, the latest version, as defined in the `Versions.props` file, will be used.

The `nuget.config` for this repository defines a local feed that points to the `packages` directory. This allows
developers that need to test against development packages to drop their development packages into this directory, and
run subsequent `build.proj` targets against them.

### Examples

Build Microsoft.Data.SqlClient version 7.1.1 that references Microsoft.Data.SqlClient.Extensions.Abstractions v1.0.1
and Microsoft.Data.SqlClient.Internal.Logging v2.2.2.

Build v2.2.2 of Logging and copy to packages:

```bash
dotnet build -t:PackLogging -p:ReferenceType=Package -p:PackageVersionLogging=2.2.2
cp artifacts/Microsoft.Data.SqlClient.Internal.Logging/Debug/*.*pkg packages/
```

Build v1.0.1 of Abstractions that depends on v2.2.2 of Logging:

```bash
dotnet build -t:PackAbstractions \
  -p:ReferenceType=Package \
  -p:PackageVersionAbstractions=1.0.1 \
  -p:PackageVersionLogging=2.2.2
cp artifacts/Microsoft.Data.SqlClient.Extensions.Abstractions/Package-Debug/*.*pkg packages/
```

Build SqlClient:

```bash
dotnet build -t:PackSqlClient \
  -p:ReferenceType=Package \
  -p:PackageVersionSqlClient=7.1.1 \
  -p:PackageVersionAbstractions=1.0.1 \
  -p:PackageVersionLogging=2.2.2
cp artifacts/Microsoft.Data.SqlClient/Package-Debug/*.*pkg packages/
```

Run Microsoft.Data.SqlClient functional tests against the versions built above:

```bash
dotnet build -t:TestSqlClientFunctional \
  -p:ReferenceType=Package \
  -p:PackageVersionSqlClient=7.1.1 \
  -p:PackageVersionAbstractions=1.0.1 \
  -p:PackageVersionLogging=2.2.2
```

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

### Using VSTest

```bash
dotnet test [test_properties...] --collect:"Code Coverage"
```

### Using Coverlet Collector

```bash
dotnet test [test_properties...] --collect:"XPlat Code Coverage"
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
a local SQL server instance using SQL authentication:

```bash
$ sqlcmd -S localhost -U sa -P password
1> create database [sqlclient-perf-db]
2> go
1> quit
```

The default `runnerconfig.json` expects a database named `sqlclient-perf-db`,
but you may change the config to use any existing database.  All tables in
the database will be dropped when running the benchmarks.

### Configure Runner

Configure the benchmarks by editing the `runnerconfig.json` file directly in the
`PerformanceTests` directory with an appropriate connection string and benchmark
settings:

```json
{
  "ConnectionString": "Server=tcp:localhost; Integrated Security=true; Initial Catalog=sqlclient-perf-db;",
  "UseManagedSniOnWindows": false,
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
    },
    ...
  }
}
```

Individual benchmarks may be enabled or disabled, and each has several
benchmarking options for fine tuning.

After making edits to `runnerconfig.json` you must perform a build which will
copy the file into the `artifacts` directory alongside the benchmark DLL.  By
default, the benchmarks look for `runnerconfig.json` in the same directory as
the DLL.

Optionally, to avoid polluting your git workspace and requiring a build after
each config change, copy `runnerconfig.json` to a new file, make your edits
there, and then specify the new file with the RUNNER_CONFIG environment
variable.

PowerShell:

```pwsh
> copy runnerconfig.json $HOME\.configs\runnerconfig.json

# Make edits to $HOME\.configs\runnerconfig.json

# You must set the RUNNER_CONFIG environment variable for the current shell.
> $env:RUNNER_CONFIG="${HOME}\.configs\runnerconfig.json"
```

Bash:

```bash
$ cp runnerconfig.json ~/.configs/runnerconfig.json

# Make edits to ~/.configs/runnerconfig.json

# Optionally export RUNNER_CONFIG.
$ export RUNNER_CONFIG=~/.configs/runnerconfig.json
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
# copy prepared by the build.
$ dotnet run -c Release -f net9.0

$ RUNNER_CONFIG=~/.configs/runnerconfig.json dotnet run -c Release -f net9.0
```
