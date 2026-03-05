# Microsoft.Data.SqlClient.Extensions.Logging

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.Extensions.Logging.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Logging)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient.Extensions.Logging?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Logging)

## Description

This package provides **ETW EventSource tracing and diagnostics** for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient). It enables comprehensive logging and telemetry capabilities for SQL Server database operations.

## Key Features

- **ETW EventSource Integration**: Structured logging using Event Tracing for Windows (ETW)
- **Diagnostic Listeners**: Hook into SqlClient diagnostic events
- **Performance Counters**: Track connection pool and query performance metrics
- **Correlation Support**: Integrate with distributed tracing systems

## Supportability

This package supports:

- .NET Standard 2.0 (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+)

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.Data.SqlClient.Extensions.Logging
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.Data.SqlClient.Extensions.Logging
```

## Getting Started

### Enable ETW Tracing

Use tools like PerfView or Windows Performance Recorder to capture ETW events:

```bash
# Using PerfView
PerfView.exe collect /Providers=*Microsoft.Data.SqlClient.EventSource
```

### Subscribe to Diagnostic Events

```csharp
using System.Diagnostics;

// Subscribe to SqlClient diagnostic events
DiagnosticListener.AllListeners.Subscribe(new SqlClientObserver());

public class SqlClientObserver : IObserver<DiagnosticListener>
{
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "SqlClientDiagnosticListener")
        {
            listener.Subscribe(new SqlClientEventObserver());
        }
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}

public class SqlClientEventObserver : IObserver<KeyValuePair<string, object>>
{
    public void OnNext(KeyValuePair<string, object> value)
    {
        Console.WriteLine($"Event: {value.Key}");
        // Process event data...
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
```

## Event Categories

The logging extension emits events in these categories:

| Category | Events |
|----------|--------|
| **Connection** | Open, Close, Pool operations |
| **Commands** | Before/After execution, Errors |
| **Transactions** | Begin, Commit, Rollback |
| **Pool** | Connection acquired/released, Pool sizing |
| **Errors** | Exceptions, Retries, Timeouts |

## Documentation

- [SqlClient Diagnostic Tracing](https://learn.microsoft.com/sql/connect/ado-net/tracing)
- [ETW Tracing in .NET](https://learn.microsoft.com/dotnet/core/diagnostics/eventsource)
- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) - The main SqlClient driver
- [Microsoft.Data.SqlClient.Extensions.Abstractions](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Abstractions) - Core abstractions
- [Microsoft.Data.SqlClient.Extensions.Azure](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure) - Azure integration extensions
