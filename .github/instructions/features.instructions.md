---
applyTo: "**"
---
# Microsoft.Data.SqlClient Feature Reference

## Connection String Keywords

This is a comprehensive reference of supported connection string keywords.

### Server/Data Source

| Keyword | Aliases | Description |
|---------|---------|-------------|
| `Data Source` | Server, Address, Addr, Network Address | SQL Server instance |
| `Initial Catalog` | Database | Database name |
| `Failover Partner` | | Mirroring failover partner |
| `ApplicationIntent` | | ReadWrite (default) or ReadOnly |
| `MultiSubnetFailover` | | Enable multi-subnet failover |

### Authentication

| Keyword | Values | Description |
|---------|--------|-------------|
| `Integrated Security` | True/False, SSPI | Windows Authentication |
| `User ID` | | SQL Server username |
| `Password` | PWD | SQL Server password |
| `Authentication` | See below | Azure AD authentication mode |
| `Attestation Protocol` | None, HGS, AAS | Enclave attestation |

#### Authentication Modes
- `SqlPassword` - SQL Server authentication
- `ActiveDirectoryPassword` - Azure AD with password
- `ActiveDirectoryIntegrated` - Azure AD integrated
- `ActiveDirectoryInteractive` - Interactive browser auth
- `ActiveDirectoryServicePrincipal` - Service principal
- `ActiveDirectoryManagedIdentity` - Managed identity
- `ActiveDirectoryDefault` - DefaultAzureCredential

### Security/Encryption

| Keyword | Values | Default | Description |
|---------|--------|---------|-------------|
| `Encrypt` | Optional, Mandatory, Strict, True, False | Mandatory | Connection encryption |
| `Trust Server Certificate` | True/False | False | Skip certificate validation |
| `Host Name In Certificate` | | | Expected certificate hostname |
| `Server Certificate` | | | Server CA certificate (Strict mode) |

### Connection Pool

| Keyword | Default | Description |
|---------|---------|-------------|
| `Pooling` | True | Enable connection pooling |
| `Min Pool Size` | 0 | Minimum pool connections |
| `Max Pool Size` | 100 | Maximum pool connections |
| `Connection Lifetime` | 0 | Max connection age (seconds) |
| `Load Balance Timeout` | 0 | Load balancing time |
| `Pool Blocking Period` | Auto | Pool blocking behavior |

### Connection Behavior

| Keyword | Default | Description |
|---------|---------|-------------|
| `Connect Timeout` | 15 | Connection timeout (seconds) |
| `Command Timeout` | 30 | Command timeout (seconds) |
| `Packet Size` | 8000 | Network packet size |
| `Workstation ID` | | Client workstation name |
| `Application Name` | .NET SqlClient | Application identifier |
| `Multiple Active Result Sets` | False | Enable MARS |
| `MultipleActiveResultSets` | False | MARS (alternate keyword) |

### Advanced

| Keyword | Default | Description |
|---------|---------|-------------|
| `Column Encryption Setting` | Disabled | Always Encrypted mode |
| `Enclave Attestation Url` | | Enclave attestation URL |
| `Type System Version` | Latest | Type system version |
| `Replication` | False | Replication support |
| `User Instance` | False | SQL Express user instance |
| `ConnectRetryCount` | 1 | Connection retry count |
| `ConnectRetryInterval` | 10 | Retry interval (seconds) |

## Data Types

### Standard Types

| SqlDbType | CLR Type | Description |
|-----------|----------|-------------|
| `BigInt` | `Int64` | 64-bit integer |
| `Binary` | `Byte[]` | Fixed-length binary |
| `Bit` | `Boolean` | Boolean |
| `Char` | `String` | Fixed-length string |
| `DateTime` | `DateTime` | Date and time |
| `Decimal` | `Decimal` | Numeric |
| `Float` | `Double` | 64-bit float |
| `Image` | `Byte[]` | Variable binary (deprecated) |
| `Int` | `Int32` | 32-bit integer |
| `Money` | `Decimal` | Currency |
| `NChar` | `String` | Unicode fixed-length |
| `NText` | `String` | Unicode text (deprecated) |
| `NVarChar` | `String` | Unicode variable-length |
| `Real` | `Single` | 32-bit float |
| `SmallDateTime` | `DateTime` | Date/time (less precision) |
| `SmallInt` | `Int16` | 16-bit integer |
| `SmallMoney` | `Decimal` | Small currency |
| `Text` | `String` | Variable text (deprecated) |
| `Timestamp` | `Byte[]` | Row version |
| `TinyInt` | `Byte` | 8-bit integer |
| `UniqueIdentifier` | `Guid` | GUID |
| `VarBinary` | `Byte[]` | Variable-length binary |
| `VarChar` | `String` | Variable-length string |
| `Variant` | `Object` | SQL_Variant |
| `Xml` | `SqlXml` | XML data |

### Modern Types

| SqlDbType | CLR Type | SQL Server Version |
|-----------|----------|-------------------|
| `Date` | `DateTime` | SQL Server 2008+ |
| `Time` | `TimeSpan` | SQL Server 2008+ |
| `DateTime2` | `DateTime` | SQL Server 2008+ |
| `DateTimeOffset` | `DateTimeOffset` | SQL Server 2008+ |
| `Json` | `String` | SQL Server 2025+ |
| `Vector` | `ISqlVector` | SQL Server 2025+ |

## SqlCommand Execution Modes

### ExecuteNonQuery
Returns number of rows affected:
```csharp
int rows = command.ExecuteNonQuery();
```

### ExecuteReader
Returns SqlDataReader for row enumeration:
```csharp
using var reader = command.ExecuteReader();
while (reader.Read()) { ... }
```

### ExecuteScalar
Returns first column of first row:
```csharp
object result = command.ExecuteScalar();
```

### ExecuteXmlReader
Returns XmlReader for FOR XML queries:
```csharp
using var reader = command.ExecuteXmlReader();
```

## SqlBulkCopy Options

| Option | Description |
|--------|-------------|
| `Default` | No special options |
| `KeepIdentity` | Preserve source identity values |
| `CheckConstraints` | Check constraints during insert |
| `TableLock` | Hold bulk update table lock |
| `KeepNulls` | Preserve null values |
| `FireTriggers` | Fire insert triggers |
| `UseInternalTransaction` | Use internal transaction |
| `AllowEncryptedValueModifications` | Allow encrypted value modifications |

## Diagnostics

### EventSource Tracing
Provider name: `Microsoft.Data.SqlClient.EventSource`

Event categories:
- Trace
- Enter/Leave scope
- Connection open/close
- Command execution
- Transaction operations
- Pool operations
- Error events

### Activity Tracing
DiagnosticListener: `SqlClientDiagnosticListener`

Activities:
- `Microsoft.Data.SqlClient.WriteCommandBefore`
- `Microsoft.Data.SqlClient.WriteCommandAfter`
- `Microsoft.Data.SqlClient.WriteCommandError`
- `Microsoft.Data.SqlClient.WriteConnectionOpenBefore`
- `Microsoft.Data.SqlClient.WriteConnectionOpenAfter`
- `Microsoft.Data.SqlClient.WriteConnectionCloseAfter`

## Common Patterns

### Retry Logic
```csharp
public static async Task<T> ExecuteWithRetry<T>(
    Func<Task<T>> operation, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try { return await operation(); }
        catch (SqlException ex) when (IsTransient(ex))
        {
            if (i == maxRetries - 1) throw;
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
        }
    }
    throw new InvalidOperationException();
}
```

### Connection String Building
```csharp
var builder = new SqlConnectionStringBuilder
{
    DataSource = "server",
    InitialCatalog = "database",
    IntegratedSecurity = true,
    Encrypt = SqlConnectionEncryptOption.Mandatory
};
var connectionString = builder.ToString();
```

### Parameterized Query
```csharp
using var cmd = new SqlCommand("SELECT * FROM Users WHERE Id = @id", conn);
cmd.Parameters.AddWithValue("@id", userId);
// Or explicit typing:
cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId;
```

## External References

- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [Connection String Reference](https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax)
- [Always Encrypted](https://learn.microsoft.com/sql/relational-databases/security/encryption/always-encrypted-database-engine)

## AppContext Switches

AppContext switches allow runtime behavior changes without modifying connection strings. They are defined in `LocalAppContextSwitches.cs` and can be set via `AppContext.SetSwitch()` or `runtimeconfig.json`.

### Available Switches

| Switch Name | Default | Description |
|-------------|---------|-------------|
| `Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString` | `false` | Disables Transparent Network IP Resolution by default |
| `Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault` | `false` | Sets `MultiSubnetFailover=true` as the default for all connections |
| `Switch.Microsoft.Data.SqlClient.EnableUserAgent` | varies | Controls sending user agent information to SQL Server |
| `Switch.Microsoft.Data.SqlClient.IgnoreServerProvidedFailoverPartner` | `false` | Ignores failover partner information sent by the server |
| `Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior` | `false` | Restores legacy null handling for rowversion columns |
| `Switch.Microsoft.Data.SqlClient.LegacyVarTimeZeroScaleBehaviour` | `false` | Restores legacy zero-scale behavior for time/datetime2/datetimeoffset |
| `Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking` | `false` | Makes ReadAsync behave synchronously (legacy compat) |
| `Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning` | `false` | Suppresses warnings about insecure TLS versions |
| `Switch.Microsoft.Data.SqlClient.TruncateScaledDecimal` | `false` | Truncates scaled decimal values instead of rounding |
| `Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour` | `false` | Uses legacy async behavior for compatibility |
| `Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni` | `false` | Uses legacy SNI processing path |
| `Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2` | `false` | Enables the new `ChannelDbConnectionPool` implementation |
| `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows` | `false` | Forces managed SNI on Windows (instead of native SNI) |
| `Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin` | `false` | Sets 1-second minimum in login timeout calculations |

### Usage Example
```csharp
// Set via AppContext before opening any connection
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault", true);

// Or via runtimeconfig.json
// {
//   "runtimeOptions": {
//     "configProperties": {
//       "Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault": true
//     }
//   }
// }
```

### Guidelines for Adding New Switches
1. Define the switch name constant in `LocalAppContextSwitches.cs`
2. Add a cached property with lazy evaluation pattern (see existing switches)
3. Default to `false` — the switch should opt-in to the new behavior
4. Add a test in `LocalAppContextSwitchesTest.cs`
5. Document the switch in this file
