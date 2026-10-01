# Microsoft.Data.SqlClient Stress Test

This stress testing application for the `Microsoft.Data.SqlClient` suite is a work in progress.

It exercises the driver under unfavorable conditions to identify failures.

This is a console application targeting the SqlClient test framework matrix (not the shipped driver TFMs):

- .NET 10.0
- .NET 9.0
- .NET 8.0
- .NET Framework 4.6.2 (Windows only; higher .NET Framework versions are supported at runtime via
  compatibility)

## Purpose of application for developers

Define fuzz tests for new driver features and APIs, and run them before each GA release.

## Pre-Requisites

Use the SDK pinned in the repository's [global.json](../../../../global.json) and install the runtime
for the framework you want to run. See [BUILDGUIDE.md](../../../../BUILDGUIDE.md) for prerequisites.
The project declares all four frameworks on every host; select a modern .NET framework when building
or running on Linux/macOS.

Create a local configuration based on
[SqlClient.Stress.Framework/StressTests.config.jsonc](SqlClient.Stress.Framework/StressTests.config.jsonc),
and point `STRESS_CONFIG_FILE` to its absolute path. Do not commit credentials. Without this override,
the runner looks for `StressTests.config.jsonc` in the process working directory; the template is not
automatically copied there.

Configuration fields:

|Field|Values|Description|
|-|-|-|
|`name`||Stress testing source configuration name.|
|`type`|`SqlServer`|Only `SqlServer` is acceptable.|
|`isDefault`|`true`, `false`|Selects the first matching default source, or the first source of the requested type if none is marked default.|
|`dataSource`||SQL Server data source name.|
|`entraIdUser`||Entra ID username; when set, selects Entra ID password authentication instead of SQL authentication.|
|`entraIdPassword`||Password for `entraIdUser`.|
|`user`||SQL Server login used when `entraIdUser` is empty.|
|`password`||Password for the SQL Server login.|
|`supportsWindowsAuthentication`|`true`, `false`|Present in the template, but currently not forwarded by the JSON configuration loader. It does not enable integrated authentication.|
|`isLocal`|`true`, `false`|`true` means database is local.|
|`disableMultiSubnetFailover`|`true`, `false`|Set `true` to disable fake-host setup and randomized MultiSubnetFailover connections.|
|`disableNamedPipes`|`true`, `false`|Set `true` to prevent random selection of the Named Pipes protocol.|
|`encrypt`|`true`, `false`|Assigns the encrypt property of the connection strings.|

Note: The database user must have permission to create and drop databases.  Each execution of the
stress tests will create a database with a name like:

- `StressTests-<GUID>`

The database will be dropped as a best effort once testing is complete.  This allows for multiple
test runs to execute in parallel against the same database server without colliding.

## Adding new Tests

- [ToDo]

## Building the application

Build the application using the top-level project `SqlClient.Stress.Runner`:

```bash
cd src/Microsoft.Data.SqlClient/tests/StressTests
dotnet build SqlClient.Stress.Runner -c Debug -f net9.0
```

Run the `cd` command from the repository root. On Windows, use `-f net462` to build the .NET Framework
executable. Use the same configuration and framework for the build and subsequent `--no-build` run.

## Running tests

After building, locate the chosen framework's output directory. Run its `stresstest.dll` with
`dotnet`, or its `stresstest.exe` for .NET Framework, using the paths shown below.

Use `--console` to keep output in the terminal. Otherwise, log files are written under `../../../logs`,
relative to the process working directory.

You may specify the config file by supplying an environment variable that points to the file:

- `STRESS_CONFIG_FILE=/path/to/my/config.jsonc`

## Command prompt

Run the following commands from the Stress Tests directory (the directory containing this README).
Set `STRESS_CONFIG_FILE` to your prepared configuration first.

```bash
# Linux/macOS
export STRESS_CONFIG_FILE=/path/to/config.jsonc

# Via dotnet run CLI:
dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner -- --assembly SqlClient.Stress.Tests

# Via dotnet CLI:
dotnet SqlClient.Stress.Runner/bin/Debug/net9.0/stresstest.dll --assembly SqlClient.Stress.Tests

# With a specific config file and all output to console:
dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner -e STRESS_CONFIG_FILE=/path/to/config.jsonc -- --assembly SqlClient.Stress.Tests --console
```

```powershell
# Windows
$env:STRESS_CONFIG_FILE = "C:\path\to\config.jsonc"

# Via dotnet run CLI:
dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner -- --assembly SqlClient.Stress.Tests

# Via executable (after building -f net462):
.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests

# With a specific config file and all output to console:
dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner -e STRESS_CONFIG_FILE=C:\path\to\config.jsonc -- --assembly SqlClient.Stress.Tests --console
```

## Supported arguments

|Argument|Values|Description|
|-|-|-|
|--assembly|&lt;assembly-name&gt;|Required. Assembly containing tests, e.g. `SqlClient.Stress.Tests`.|
|--duration|&lt;n&gt;|Duration of the test in seconds. Default value is 1 second.|
|--threads|&lt;n&gt;|Number of threads to use. Default value is 16.|
|--override|&lt;name=value&gt;|Override test property values. Accepts multiple values.|
|--variation|&lt;value&gt;|Add a test variation. Accepts multiple values.|
|--test|&lt;name1;name2&gt;|Run specific test(s). Quote semicolon-separated names.|
|--debug||Print process ID and wait for Enter to attach the debugger.|
|--console||Emit all output to the console instead of a log file.|
|--exception-threshold|&lt;n&gt;|An optional limit on exceptions which will be caught. When reached, test will halt.|
|--monitor-enabled|true, false|Enable monitoring. Default is false [not implemented].|
|--random-seed|&lt;n&gt;|Set the random number generator seed for reproducibility. Default is 0.|
|--filter|&lt;filter&gt;|Run tests whose stress test attributes match the filter. Example: `--filter "TestType=Query,Update;IsServerTest=True"`.|
|--print-method-name||Print test method names in the console.|
|--deadlock-detection||Enable deadlock detection. Disabled by default.|

The former single-dash options (`-a`, `-all`, `-verify`, etc.) are not supported.
Omit `--test` to select all discovered tests, subject to any `--filter`.
The executable examples below assume a Windows `net462` Debug build and the same Stress Tests working directory.

```powershell
# Run all discovered tests.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests
```

```powershell
# Run all discovered tests and print their method names.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --print-method-name
```

```powershell
# Wait for debugger attachment before running all discovered tests.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --debug
```

```powershell
# Run only TestExecuteXmlReaderAsyncCancellation.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --test TestExecuteXmlReaderAsyncCancellation
```

```powershell
# Run all discovered tests for 10 seconds.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --duration 10
```

```powershell
# Run all discovered tests with 5 threads.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --threads 5
```

```powershell
# Run all discovered tests with deadlock detection enabled.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --deadlock-detection
```

```powershell
# Run all discovered tests with Weight set to 15.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --override Weight=15
```

```powershell
# Run all discovered tests with random seed 5.

.\SqlClient.Stress.Runner\bin\Debug\net462\stresstest.exe --assembly SqlClient.Stress.Tests --random-seed 5
```

## Further thoughts

- Implement the uncompleted arguments.
- Add more tests.
- Add support running tests with **System.Data.SqlClient** too.
