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
| `Authentication` | See below | Entra ID authentication mode |
| `Attestation Protocol` | None, HGS, AAS | Enclave attestation |

#### Authentication Modes
- `SqlPassword` - SQL Server authentication
- `ActiveDirectoryPassword` - Entra ID with password
- `ActiveDirectoryIntegrated` - Entra ID integrated
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
| `Switch.Microsoft.Data.SqlClient.UseLegacyFailoverAlternationOnLoginSqlErrors` | `false` | Restores legacy `LoginWithFailover` alternation for login-phase SQL errors when parser state is not `Closed` |
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
| `Switch.Microsoft.Data.SqlClient.UseLegacyUdtAssemblyLoad` | `false` | Restores the pre-policy behavior of loading any assembly named by a server-supplied UDT assembly-qualified name, and of skipping the `[SqlUserDefinedType]` check |

### UDT Assembly Load Policy

A server-supplied UDT assembly-qualified name reaches `Assembly.Load`, so the
driver applies a deny-by-default policy before handing the name to the loader.
There is a single enforcing behavior, which permits:

| Permitted | Notes |
|-----------|-------|
| `Microsoft.SqlServer.Types` | Identity pinned: the version is normalized to the connection's negotiated type system version, the culture to neutral, and the public key token to the one Microsoft signs with |
| Assemblies on the allow list | The application explicitly naming what it is willing to have loaded |
| Assemblies already loaded into the process | Resolved to the instance the process already holds; the server-supplied version, culture and public key token are discarded |

Everything else is refused. In particular, an assembly that is only *statically
referenced* by a loaded assembly is **not** permitted, because loading it is a
genuinely new load — precisely what this policy keeps under the application's
control rather than the server's.

Normalizing the reference is necessary but not sufficient. On .NET the loader
**ignores** the public key token in an `AssemblyName`, so pinning it does not by
itself prevent a same-named assembly with a different identity from being
returned. The driver therefore verifies the identity of the assembly the loader
actually hands back, and refuses it if it does not carry the required token.
This mirrors what the driver already does for the Azure authentication extension
assembly.

On .NET, the already-loaded tier is scoped to the `AssemblyLoadContext` that
loaded the driver, since that is the context its `Assembly.Load` calls resolve
into. An application that loads its UDT assembly into a separate (for example
collectible) context must name it on the allow list. The driver holds only weak
references to the assemblies it has observed, so this policy never prevents a
collectible context from unloading.

Setting `UseLegacyUdtAssemblyLoad` disables the policy entirely and restores the
pre-policy behavior. It is a temporary compatibility escape hatch, not a
supported configuration.

Applications that use custom UDTs whose assemblies are loaded on demand must name
them explicitly through the `Microsoft.Data.SqlClient.UdtAssemblyAllowList`
AppContext data element, a semicolon-separated list of assembly names:

```csharp
AppDomain.CurrentDomain.SetData(
    "Microsoft.Data.SqlClient.UdtAssemblyAllowList",
    "Contoso.Udts;Fabrikam.Udts, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
```

Each entry is matched only on the components it specifies, so a simple name
permits any version, culture, and public key token, while a fully-qualified name
must match exactly. An entry that explicitly specifies `PublicKeyToken=null`
requires an unsigned assembly and is not satisfied by a signed one; this is
distinct from omitting the token, which places no constraint on it.

Independently of the assembly policy, a resolved type that is not annotated with
`SqlUserDefinedTypeAttribute` is rejected before any member of it is accessed
(except under `UseLegacyUdtAssemblyLoad`). This is the gate that actually
prevents foreign code execution.

On CoreCLR this has been measured directly: neither `Assembly.Load`, nor
resolving a type from the assembly, nor reading that type's custom attributes
runs anything from it. A module initializer or static constructor runs on first
real member access, which is what `GetUdtValue` would otherwise perform. The
attribute check therefore sits in front of the only step that executes code.

Module initializer timing on .NET Framework has not been measured, and ECMA-335
permits a runtime to run one earlier than CoreCLR does. The portable guarantee
is the one stated above — no member of the type is accessed before the attribute
check — rather than a claim about exactly when the runtime chooses to run
initializers.

Note that the attribute check itself does not execute foreign code.
`SqlUserDefinedTypeAttribute` is `sealed`, so it cannot be subclassed by a
hostile assembly, and the lookup is filtered to that single attribute type, so
the constructors of any other attributes on the type are never invoked.

#### Trust is per process, not per server

The already-loaded tier makes the permitted set a property of the process rather
than of the connection. Once an assembly is loaded by any means, a UDT type
within it can be instantiated on the say-so of any server the process connects
to, whether or not that assembly was loaded for that server's benefit. The
resolved type must still carry `SqlUserDefinedTypeAttribute`, so this is
confined to types that were written to be deserialized from SQL Server, but it
is a genuine widening and is called out here deliberately.

Relatedly, the map of loaded assemblies is built once and then maintained
incrementally. That is not only a performance choice: rebuilding it on demand
would let an assembly that was pulled in as a *dependency* of a permitted
assembly silently inherit that permission. Building it once keeps the tier
anchored to what the application had already loaded.

#### Compatibility impact

This policy is a behavior change for applications that use **custom** UDTs. The
built-in spatial types (`SqlGeography`, `SqlGeometry`, `SqlHierarchyId`) are
unaffected, since `Microsoft.SqlServer.Types` is permitted by identity.

An application is affected when the custom UDT's assembly is not yet loaded at
the moment the value is read. That is common whenever the *driver* materializes
the value and the application never names the type in its own code — generic data
access layers, micro-ORMs, `DataTable.Load`, and schema discovery. In those cases
the driver's own `Assembly.Load` was previously the thing that pulled the
assembly in, and it is now refused.

The symptom depends on the API:

| API | Symptom |
|-----|---------|
| `reader[i]`, `GetValue`, UDT output parameters | `TypeLoadException` naming the assembly and the allow list |
| `GetFieldType`, `GetSchemaTable`, `GetColumnSchema` | Returns `null` for the UDT column's type rather than throwing |

The exception is a `TypeLoadException` and is not wrapped in a `SqlException`,
which matches how the driver already reports a UDT type it cannot resolve.

The second row is the harder one to diagnose, because `GetFieldType` does not
normally return `null`; a caller that dereferences the result sees an unrelated
`NullReferenceException`. A denial is always traced through
`SqlClientEventSource` regardless of which path was taken, so enabling event
source tracing will identify the assembly.

The remedy in every case is to name the assembly on the allow list.

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
