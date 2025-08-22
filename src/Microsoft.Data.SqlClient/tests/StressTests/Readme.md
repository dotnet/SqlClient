# Microsoft.Data.SqlClient Stress Test

This Stress testing application for `Microsoft.Data.SqlClient` is under progress.

This project intends to help finding a certain level of effectiveness under
unfavorable conditions, and verifying the mode of failures.

This is a console application targeting all frameworks supported by MDS,
currently:

- .NET 8.0
- .NET 9.0
- .NET Framework 4.6.2
- .NET Framework 4.7
- .NET Framework 4.7.1
- .NET Framework 4.7.2
- .NET Framework 4.8
- .NET Framework 4.8.1

## Purpose of application for developers

Define fuzz tests for all new features/APIs in the driver and to be run before
every GA release.

## Pre-Requisites

Required in the config file:

|Field|Values|Description|
|-|-|-|
|`name`||Stress testing source configuration name.|
|`type`|`SqlServer`|Only `SqlServer` is acceptable.|
|`isDefault`|`true`, `false`|If there is a source node with `isDefault=true`, this node is returned.|
|`dataSource`||SQL Server data source name.|
|`user`||User Id to connect the server.|
|`password`||Paired password with the user.|
|`supportsWindowsAuthentication`|`true`, `false`|Tries to use integrated security in connection string mixed with SQL Server authentication if it set to `true` by applying the randomization.|
|`isLocal`|`true`, `false`|`true` means database is local.|
|`disableMultiSubnetFailover`|`true`, `false`|Tries to add Multi-subnet Failover fake host entries when it equals `true`.|
|`disableNamedPipes`|`true`, `false`|`true` means the connections will create just using tcp protocol.|
|`encrypt`|`true`, `false`|Assigns the encrypt property of the connection strings.|

Note: The database user must have permission to create and drop databases.
Each execution of the stress tests will create a database with a name like:

- `StressTests-<GUID>`

The database will be dropped as a best effort once testing is complete.  This
allows for multiple test runs to execute in parallel against the same database
server without colliding.

## Adding new Tests

- [ToDo]

## Building the application

To build the application using the `StressTests.slnx` solution:

```bash
dotnet build [-c|--configuration <Release|Debug>]
```

```bash
# Builds the application for the Client Os in `Debug` Configuration for `AnyCpu`
# platform.
#
# All supported target frameworks are built by default.

$ dotnet build
```

```bash
# Build the application for .Net framework 4.8.1 with `Debug` configuration.

$ dotnet build -f net481
```

```bash
# Build the application for .Net 9.0 with `Release` configuration.

$ dotnet build -f net9.0 -c Release
```

```bash
# Cleans all build directories

$ dotnet clean
```

## Running tests

After building the application, find the built folder with target framework and
run the `stresstest.exe` file with required arguments.

Find the result in a log file inside the `logs` folder besides the command
prompt.

You may specify the config file by supplying an environment variable that
points to the file:

- `STRESS_CONFIG_FILE=/path/to/my/config.jsonc`

## Command prompt

You must run the stress tests from the root of the Stress Tests project
directory (i.e. the same directory this readme file is in).

```bash
# Linux
$ cd /home/paul/dev/SqlClient/src/Microsoft.Data.SqlClient/tests/StressTests

# Via dotnet run CLI:
$ dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner/SqlClient.Stress.Runner.csproj -- -a SqlClient.Stress.Tests

# Via dotnet CLI:
$ dotnet SqlClient.Stress.Runner/bin/Debug/net9.0/stresstest.dll -a SqlClient.Stress.Tests

# With a specific config file and all output to console:
$ dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner/SqlClient.Stress.Runner.csproj -e STRESS_CONFIG_FILE=/path/to/config.jsonc -- -a SqlClient.Stress.Tests -console
```

```powershell
# Windows
> cd \dev\SqlClient\src\Microsoft.Data.SqlClient\tests\StressTests

# Via dotnet run CLI:
> dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner\SqlClient.Stress.Runner.csproj -- -a SqlClient.Stress.Tests

# Via executable:
> .\SqlClient.Stress.Runner\bin\Debug\net481\stresstest.exe -a SqlClient.Stress.Tests

# With a specific config file and all output to console:
> dotnet run --no-build -f net9.0 --project SqlClient.Stress.Runner\SqlClient.Stress.Runner.csproj -e STRESS_CONFIG_FILE=c:\path\to\config.jsonc -- -a SqlClient.Stress.Tests -console
```

## Supported arguments

|Argument|Values|Description|
|-|-|-|
|-all||Run all tests - best for debugging, not perf measurements.|
|-verify||Run in functional verification mode. [not implemented]|
|-duration|&lt;n&gt;|Duration of the test in seconds. Default value is 1 second.|
|-threads|&lt;n&gt;|Number of threads to use. Default value is 16.|
|-override|&lt;name&gt; &lt;value&gt;|Override the value of a test property.|
|-test|&lt;name1;name2&gt;|Run specific test(s).|
|-debug||Print process ID in the beginning and wait for Enter (to give your time to attach the debugger).|
|-console||Emit all output to the console instead of a log file.|
|-exceptionThreshold|&lt;n&gt;|An optional limit on exceptions which will be caught. When reached, test will halt.|
|-monitorenabled|true, false|True or False to enable monitoring. Default is false [not implemented]|
|-randomSeed||Enables setting of the random number generator used internally. This serves both the purpose of helping to improve reproducibility and making it deterministic from Chess's perspective for a given schedule. Default is 0.|
|-filter|&lt;filter&gt;|Run tests whose stress test attributes match the given filter. Filter is not applied if attribute does not implement ITestAttributeFilter. Example: -filter TestType=Query,Update;IsServerTest=True|
|-printMethodName||Print tests' title in console window|
|-deadlockdetection|true, false|True or False to enable deadlock detection. Default is `false`.|

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached.

> .\stresstest.exe -a SqlClient.Stress.Tests -all
```

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached and shows the test methods' names.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -printMethodName 
```

```powershell
# Run the application for a built target framework and all discovered tests and
# will wait for debugger to be attached.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -debug 
```

```powershell
# Run the application for a built target framework and
# "TestExecuteXmlReaderAsyncCancellation" test without debugger attached.

> .\stresstest.exe -a SqlClient.Stress.Tests -test TestExecuteXmlReaderAsyncCancellation
```

```powershell
# Run the application for a built target framework and
# "TestExecuteXmlReaderAsyncCancellation" test without debugger attached.

> .\stresstest.exe -a SqlClient.Stress.Tests -test TestExecuteXmlReaderAsyncCancellation
```

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached for 10 seconds.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -duration 10
```

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached with 5 threads.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -threads 5
```

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached and dead lock detection process.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -deadlockdetection true
```

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached with overriding the weight property with value 15.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -override Weight 15
```

```powershell
# Run the application for a built target framework and all discovered tests
# without debugger attached with injecting random seed of 5.

> .\stresstest.exe -a SqlClient.Stress.Tests -all -randomSeed 5
```

## Further thoughts

- Implement the uncompleted arguments.
- Add more tests.
- Add support running tests with **System.Data.SqlClient** too.
