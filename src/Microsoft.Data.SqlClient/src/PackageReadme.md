# Microsoft.Data.SqlClient

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient)

## Description

**Microsoft.Data.SqlClient** is the official .NET data provider for [Microsoft SQL Server](https://aka.ms/sql) and [Azure SQL](https://aka.ms/azure_sql) databases. It provides access to SQL Server and encapsulates database-specific protocols, including Tabular Data Stream (TDS).

This library grew from a union of the two `System.Data.SqlClient` components which live independently in .NET and .NET Framework. Going forward, support for new SQL Server and Azure SQL features will only be implemented in Microsoft.Data.SqlClient.

## Supportability

This package supports:

- .NET Framework 4.6.2+
- .NET 8.0+

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.Data.SqlClient
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.Data.SqlClient
```

## Getting Started

### Basic Connection

```csharp
using Microsoft.Data.SqlClient;

var connectionString = "Server=myserver;Database=mydb;Integrated Security=true;";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("Connected successfully!");
```

### Execute a Query

```csharp
using Microsoft.Data.SqlClient;

var connectionString = "Server=myserver;Database=mydb;Integrated Security=true;";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

using var command = new SqlCommand("SELECT Id, Name FROM Customers", connection);
using var reader = await command.ExecuteReaderAsync();

while (await reader.ReadAsync())
{
    Console.WriteLine($"Id: {reader.GetInt32(0)}, Name: {reader.GetString(1)}");
}
```

### Parameterized Query

```csharp
using Microsoft.Data.SqlClient;

var connectionString = "Server=myserver;Database=mydb;Integrated Security=true;";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

using var command = new SqlCommand("SELECT * FROM Customers WHERE Id = @id", connection);
command.Parameters.AddWithValue("@id", customerId);

using var reader = await command.ExecuteReaderAsync();
// Process results...
```

### Using Transactions

```csharp
using Microsoft.Data.SqlClient;

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

using var transaction = connection.BeginTransaction();

try
{
    using var command = new SqlCommand("INSERT INTO Orders (CustomerId, Total) VALUES (@customerId, @total)", connection, transaction);
    command.Parameters.AddWithValue("@customerId", customerId);
    command.Parameters.AddWithValue("@total", orderTotal);
    
    await command.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## Key Features

| Feature | Description |
|---------|-------------|
| **Cross-Platform** | Runs on Windows, Linux, and macOS |
| **Azure AD Authentication** | Multiple Azure Active Directory authentication modes |
| **Always Encrypted** | Client-side encryption for sensitive data |
| **Connection Pooling** | Efficient connection management |
| **TLS 1.3 Support** | Enhanced security with strict encryption mode |
| **MARS** | Multiple Active Result Sets on a single connection |
| **Async Programming** | Full async/await support |
| **SqlBatch** | Batch multiple commands for improved performance |
| **JSON/Vector Support** | Native support for JSON and Vector data types (SQL Server 2025+) |

## Commonly Used Types

```
Microsoft.Data.SqlClient.SqlConnection
Microsoft.Data.SqlClient.SqlCommand
Microsoft.Data.SqlClient.SqlDataReader
Microsoft.Data.SqlClient.SqlParameter
Microsoft.Data.SqlClient.SqlTransaction
Microsoft.Data.SqlClient.SqlException
Microsoft.Data.SqlClient.SqlParameterCollection
Microsoft.Data.SqlClient.SqlClientFactory
Microsoft.Data.SqlClient.SqlBulkCopy
Microsoft.Data.SqlClient.SqlConnectionStringBuilder
```

## Authentication Methods

| Method | Connection String |
|--------|-------------------|
| SQL Server Authentication | `User ID=user;Password=pass;` |
| Windows Authentication | `Integrated Security=true;` |
| Azure AD Password | `Authentication=Active Directory Password;User ID=user;Password=pass;` |
| Azure AD Integrated | `Authentication=Active Directory Integrated;` |
| Azure AD Interactive | `Authentication=Active Directory Interactive;` |
| Azure AD Managed Identity | `Authentication=Active Directory Managed Identity;` |
| Azure AD Service Principal | `Authentication=Active Directory Service Principal;User ID=clientId;Password=clientSecret;` |
| Azure AD Default | `Authentication=Active Directory Default;` |

## Encryption Modes

| Mode | Description |
|------|-------------|
| `Encrypt=Optional` | Encryption is used if available |
| `Encrypt=Mandatory` | Encryption is required (default) |
| `Encrypt=Strict` | TDS 8.0 with TLS 1.3 support |

## SNI (SQL Server Network Interface)

Two implementations are available:

- **Native SNI**: Windows-only, provided via [Microsoft.Data.SqlClient.SNI](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI) (.NET Framework) or [Microsoft.Data.SqlClient.SNI.runtime](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI.runtime) (.NET on Windows)
- **Managed SNI**: Cross-platform managed implementation, used by default on Unix platforms

## Documentation

- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [Connection String Syntax](https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax)
- [Connection Pooling](https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling)
- [Always Encrypted](https://learn.microsoft.com/sql/relational-databases/security/encryption/always-encrypted-database-engine)
- [Azure AD Authentication](https://learn.microsoft.com/sql/connect/ado-net/sql/azure-active-directory-authentication)

## Release Notes

Release notes are available at: https://go.microsoft.com/fwlink/?linkid=2090501

## Migrating from System.Data.SqlClient

If you're migrating from `System.Data.SqlClient`, see the [porting cheat sheet](https://github.com/dotnet/SqlClient/blob/main/porting-cheat-sheet.md) for guidance.

Key changes:
1. Change namespace from `System.Data.SqlClient` to `Microsoft.Data.SqlClient`
2. Update package reference to `Microsoft.Data.SqlClient`
3. Review connection string defaults (e.g., `Encrypt=Mandatory` is now the default)

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider](https://www.nuget.org/packages/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider) - Azure Key Vault integration for Always Encrypted
- [Microsoft.SqlServer.Server](https://www.nuget.org/packages/Microsoft.SqlServer.Server) - SQL CLR UDT support
- [Microsoft.Data.SqlClient.SNI](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI) - Native SNI for .NET Framework
- [Microsoft.Data.SqlClient.SNI.runtime](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI.runtime) - Native SNI runtime for .NET on Windows
