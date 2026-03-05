---
applyTo: "doc/**,**/samples/**"
---
# Documentation and Samples Guide

## Documentation Structure

```
doc/
├── Directory.Packages.props    # Package versions for doc projects
├── samples/                    # Code samples for documentation
│   ├── AADAuthenticationCustomDeviceFlowCallback.cs
│   ├── AzureKeyVaultProviderExample.cs
│   ├── ConnectionStrings_Encrypt.cs
│   └── ...
└── snippets/                   # Documentation snippets
```

## Writing Samples

### Sample File Naming
Use descriptive names following the pattern:
```
{ClassName}_{MethodOrFeature}.cs
{FeatureName}_Example.cs
{ClassName}_{Scenario}.cs
```

Examples:
- `SqlConnection_Open.cs`
- `SqlBulkCopy_ColumnMapping.cs`
- `AlwaysEncrypted_AzureKeyVault.cs`

### Sample Structure
```csharp
// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        // Sample code here
    }
}
// </Snippet1>
```

### Snippet Tags
Use XML comment tags to define reusable snippets:
```csharp
// <Snippet_OpenConnection>
using var connection = new SqlConnection(connectionString);
connection.Open();
Console.WriteLine($"Connected to: {connection.Database}");
// </Snippet_OpenConnection>
```

## Sample Categories

### Connection Samples
Demonstrate connection scenarios:
- Basic connection
- Connection string building
- Authentication methods
- Connection pooling

### Command Samples
Show command execution:
- ExecuteReader
- ExecuteNonQuery
- ExecuteScalar
- Async execution
- Parameterized queries

### Transaction Samples
Transaction management:
- Local transactions
- Distributed transactions (MSDTC)
- SavePoints

### Data Type Samples
Working with SQL data types:
- DateTime handling
- Binary data
- XML data
- JSON (SQL Server 2025+)
- Vector (SQL Server 2025+)

### Security Samples
Authentication and encryption:
- Azure AD authentication
- Always Encrypted
- Azure Key Vault integration
- SSL/TLS configuration

### Performance Samples
Optimization techniques:
- Bulk copy operations
- Async patterns
- Connection pooling
- Batch operations

## Documentation Standards

### XML Documentation
All public APIs must have XML documentation:

```csharp
/// <summary>
/// Opens a database connection with the settings specified by the
/// <see cref="ConnectionString"/> property.
/// </summary>
/// <exception cref="InvalidOperationException">
/// A connection was already open.
/// </exception>
/// <exception cref="SqlException">
/// A connection-level error occurred while opening the connection.
/// </exception>
/// <example>
/// <code>
/// using var connection = new SqlConnection(connectionString);
/// connection.Open();
/// </code>
/// </example>
public override void Open()
```

### Required XML Elements

| Element | Usage |
|---------|-------|
| `<summary>` | Brief description (required) |
| `<param>` | Parameter description |
| `<returns>` | Return value description |
| `<exception>` | Exceptions that may be thrown |
| `<example>` | Usage example |
| `<remarks>` | Additional details |
| `<seealso>` | Related members |

### Writing Style
- Use third person ("Opens a connection" not "Open a connection")
- Be concise but complete
- Include common use cases
- Note any platform-specific behavior

## Sample Best Practices

### DO
```csharp
// Use meaningful variable names
using var connection = new SqlConnection(connectionString);

// Include error handling in samples
try
{
    connection.Open();
}
catch (SqlException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// Show proper resource cleanup
using var reader = command.ExecuteReader();

// Use async when demonstrating async features
await connection.OpenAsync();
```

### DON'T
```csharp
// Don't use hardcoded credentials
var conn = "Server=x;User=sa;Password=secret";  // BAD!

// Don't leave resources unmanaged
var reader = cmd.ExecuteReader();  // Missing using/Dispose

// Don't suppress exceptions silently
try { ... } catch { }  // BAD!
```

## Adding New Samples

1. **Create sample file** in `doc/samples/`
2. **Follow naming convention** for discoverability
3. **Use snippet tags** for documentation inclusion
4. **Test the sample** to ensure it compiles and runs
5. **Link from documentation** where relevant

## Sample Project Testing

Samples should be testable:
```csharp
// Sample helper for testing
public static class SampleRunner
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("SQLCLIENT_TEST_CONNSTR")
        ?? "Server=localhost;Database=master;Trusted_Connection=True;";
}
```

## Microsoft Learn Integration

Samples may be referenced from Microsoft Learn documentation:
- https://learn.microsoft.com/sql/connect/ado-net/

When creating samples for external documentation:
1. Verify snippet tags are correctly formatted
2. Ensure sample compiles standalone
3. Include all necessary using statements
4. Document any prerequisites

## Changelog Documentation

Do not edit `CHANGELOG.md` directly. The changelog is updated as part of the release workflow based on the contents of `release-notes/` and the `release-notes` prompt.

When adding features, fixes, or breaking changes, create or update the appropriate entry under `release-notes/` instead. For example:
```markdown
## [Unreleased]

### Added
- New `SqlCommand.ExecuteJsonAsync()` method for JSON result sets
- Support for SQL Server 2025 JSON data type

### Changed
- Connection encryption now defaults to Mandatory

### Fixed
- Issue with connection pool exhaustion under high load (#1234)

### Deprecated
- `SqlConnection.GetSchema(string)` overload
```

## Release Notes

Release notes in `release-notes/` follow version structure:
```
release-notes/
├── README.md           # Index of all versions
├── 5.0/                # Major version folder
│   └── 5.0.md         # Version release notes
├── 5.1/
│   └── 5.1.md
└── template/           # Release notes template
```

## External Documentation Resources

- [Microsoft Learn - ADO.NET](https://learn.microsoft.com/dotnet/framework/data/adonet/)
- [SQL Server Documentation](https://learn.microsoft.com/sql/sql-server/)
- [Microsoft.Data.SqlClient NuGet](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
