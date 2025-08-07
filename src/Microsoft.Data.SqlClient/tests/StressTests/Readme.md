# Microsoft.Data.SqlClient Stress Test

This Stress testing application for `Microsoft.Data.SqlClient` is under progress.
This project intends to help finding a certain level of effectiveness under unfavorable conditions, and verifying the mode of failures.
This is a console application with targeting frameworks `.Net Framework 4.8`, `.NET 9.0` under driver's supported operating systems and SQL Servers.

## Purpose of application for developers

Define fuzz tests for all new features/APIs in the driver and to be run before every GA release.

# Pre-Requisites

required in StressTest.config:

"name": stress testing source configuration name.
"type": only `SqlServer` is acceptable.
"isDefault": If there is a source node with `isDefault=true`, this node is returned.
"dataSource": SQL Server data source name.
"database": targeting database name in the SQL Server.
"user": user Id to connect the server.
"password": paired password with the user.
"supportsWindowsAuthentication": tries to use integrated security in connection string mixed with SQL Server authentication if it set to `true` by applying the randomization.
"isLocal": `true` means database is local.
"disableMultiSubnetFailover": tries to add Multi-subnet Failover fake host entries when it equals `true`,
"disableNamedPipes": `true` means the connections will create just using tcp protocol.
"encrypt": assigns the encrypt property of the connection strings.

# Adding new Tests
- [ToDO]

# Building the application

To build the application we need to run the command: 'dotnet build <-f|--framework <FRAMEWORK>> [-c|--configuration <Release|Debug>]'
The path should be pointing to SqlClient.Stress.Runner.csproj file.

```bash
# Default Build Configuration:

> dotnet build
# Builds the application for the Client Os in `Debug` Configuration for `AnyCpu` platform.
# All supported target frameworks, .NET Framework  (NetFx) and .NET Core drivers are built by default (as supported by Client OS).
```

```bash
> dotnet build -f netcoreapp3.1
# Build the application for .Net core 3.1 with `Debug` configuration.

> dotnet build -f net48
# Build the application for .Net framework 4.8 with `Debug` configuration.
```

```bash
> dotnet build -f net5.0 -c Release
# Build the application for .Net 5.0 with `Release` configuration.

> dotnet build -f net48 -c Release
# Build the application for .Net framework 4.8 with `Release` configuration.
```

```bash
> dotnet clean
# Cleans all build directories
```

# Running tests

After building the application, find the built folder with target framework and run the `stresstest.exe` file with required arguments.
Find the result in a log file inside the `logs` folder besides the command prompt.

## Command prompt

```bash
> stresstest.exe [-a <module name>] <arguments>

-a <module name> should specify path to the assembly containing the tests.
```

## Supported arguments

-all                        Run all tests - best for debugging, not perf measurements.

-verify                     Run in functional verification mode. [not implemented]

-duration <n>               Duration of the test in seconds. Default value is 1 second.

-threads <n>                Number of threads to use. Default value is 16.

-override <name> <value>    Override the value of a test property.

-test <name1;name2>         Run specific test(s).

-debug                      Print process ID in the beginning and wait for Enter (to give your time to attach the debugger).

-exceptionThreshold <n>     An optional limit on exceptions which will be caught. When reached, test will halt.

-monitorenabled             True or False to enable monitoring. Default is false [not implemented]

-randomSeed                 Enables setting of the random number generator used internally. This serves both the purpose
                            of helping to improve reproducibility and making it deterministic from Chess's perspective
                            for a given schedule. Default is 0.

-filter                     Run tests whose stress test attributes match the given filter. Filter is not applied if attribute
                            does not implement ITestAttributeFilter. Example: -filter TestType=Query,Update;IsServerTest=True

-printMethodName            Print tests' title in console window

-deadlockdetection          True or False to enable deadlock detection. Default is `false`.

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all
# Run the application for a built target framework and all discovered tests without debugger attached.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -printMethodName 
# Run the application for a built target framework and all discovered tests without debugger attached and shows the test methods' names.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -debug 
# Run the application for a built target framework and all discovered tests and will wait for debugger to be attached.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -test TestExecuteXmlReaderAsyncCancellation
# Run the application for a built target framework and "TestExecuteXmlReaderAsyncCancellation" test without debugger attached.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -test TestExecuteXmlReaderAsyncCancellation
# Run the application for a built target framework and "TestExecuteXmlReaderAsyncCancellation" test without debugger attached.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -duration 10
# Run the application for a built target framework and all discovered tests without debugger attached for 10 seconds.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -threads 5
# Run the application for a built target framework and all discovered tests without debugger attached with 5 threads.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -deadlockdetection true
# Run the application for a built target framework and all discovered tests without debugger attached and dead lock detection process.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -override Weight 15
# Run the application for a built target framework and all discovered tests without debugger attached with overriding the weight property with value 15.
```

```bash
> stresstest.exe -a SqlClient.Stress.Tests -all -randomSeed 5
# Run the application for a built target framework and all discovered tests without debugger attached with injecting random seed of 5.
```

# Further thoughts

- Implement the uncompleted arguments.
- Add support `dotnet run` command.
- Add more tests.
- Add support running tests with **System.Data.SqlClient** too.
