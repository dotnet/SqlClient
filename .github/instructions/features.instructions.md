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
| `Vector Type Support` | v2 | Vector base types exchanged in binary form: `off`, `v1`, `v2` |
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

### Vector Base Types

A `vector` column has a base type, which determines how its elements are stored and
transported. The base type is selected by the type parameter of `SqlVector<T>`.

| SQL Server base type | `SqlVector<T>` | Element size | Max dimensions | Availability |
|----------------------|----------------|--------------|----------------|--------------|
| `float32` (default)  | `SqlVector<float>` | 4 bytes | 1998 | SQL Server 2025+ |
| `float16`            | `SqlVector<Half>`  | 2 bytes | 3996 | SQL Server 2025+ (preview), .NET only |

Notes:

- A vector column with a `float16` base type is returned in binary form when the connection
  negotiates `v2` through the `Vector Type Support` keyword. See below.
- `System.Half` does not exist on .NET Framework, so `SqlVector<Half>` cannot be used there.
  A vector column with a `float16` base type is instead reported as a string, and can be read
  as a JSON array through `GetString`/`GetSqlString`/`GetFieldValue<string>`, or as a
  `SqlVector<float>` via `GetSqlVector<float>`, which widens the elements. Widening from `float16` is exact.
- `float16` requires `ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON` while
  it is in preview.
- SQL Server converts between base types for parameters and native vector bulk-copy sources.
  Bulk copy describes the source vector's base type in both its `INSERT BULK` declaration
  and binary payload, so the server can convert it to the destination column. JSON text
  copied into a vector column with a `float16` base type is sent as `varchar(max)`, without
  client-side narrowing.
  The existing JSON-to-`float32` bulk-copy path retains its native float32 conversion.
- Object-typed `DataTable` columns can contain a consistent native vector base type plus nulls.
  Bulk copy looks ahead only through the selected in-memory rows to find the first non-null
  representation, then validates compatibility as rows are copied. Row-array subsets and row-state
  filters are respected; an all-null column can require examining all selected rows. Native
  float32/float16 values cannot be mixed in one incoming column. Existing float32 JSON coercion
  is preserved. Reader setup uses schema metadata instead of consuming values.
- Synthetic text input for a destination vector column with a `float16` base type uses an explicit
  UTF-8 encoding/collation pair; SQL Server still performs JSON parsing and conversion.
- Typed non-SqlClient readers, including `DataTableReader`, render native vector values as JSON
  during row copying when incoming metadata is unencrypted `varchar`, `nvarchar`, or `json`.
  This also covers float16 destinations exposed as text under V1/Off. No metadata-time value
  reads occur; nulls remain SQL NULL, and vector fields bypass text-stream/null pre-check accessors.
  Native vector metadata still selects the binary path. Ordinary parameter coercion and
  in-memory source policies are unchanged.
- A column's base type and number of dimensions are available from the column schema:
  `reader.GetColumnSchema()[i]["VectorBaseType"]` and `["VectorDimensions"]`. Both are `null`
  for columns which are not reported as vectors, including float16 under `v1`. These
  properties distinguish the base types
  when a vector column with a `float16` base type is surfaced as a JSON string, because
  `GetFieldType` reports `string` for such a column just as it does for a `varchar` one:

  ```csharp
  DbColumn column = reader.GetColumnSchema()[0];

  string baseType   = (string)column["VectorBaseType"];    // "float16" | "float32"
  int    dimensions = (int)   column["VectorDimensions"];  // element count

  // The dimension count is known before any row is read, so a buffer can be sized once.
  float[] buffer = new float[dimensions];

  while (reader.Read())
  {
      // Widening from float16 is exact, so float32 reads either base type
      // losslessly, and is the only strongly typed option on .NET Framework.
      reader.GetSqlVector<float>(0).Memory.Span.CopyTo(buffer);
  }
  ```

  For table or view columns, the public
  [sys.columns catalog view](https://learn.microsoft.com/sql/relational-databases/system-catalog-views/sys-columns-transact-sql)
  provides `vector_dimensions`, `vector_base_type`, and `vector_base_type_desc`, subject to
  metadata visibility permissions. It is not a substitute for arbitrary query result metadata.

> [!NOTE]
> On .NET Framework, `float16` workloads do more work per value than they do on .NET.
> Because `System.Half` isn't available, the driver widens each element to float32
> when it reads a vector column with a `float16` base type, and renders the result as a JSON
> array for the string read paths. On .NET, a `SqlVector<Half>` wraps the payload the server sent,
> so no per-element conversion takes place. Writes differ in the same way: .NET can send a
> `SqlVector<Half>` unchanged, while .NET Framework sends a JSON string or a
> `SqlVector<float>`, which SQL Server converts to the column's base type.
>
> If you read vector columns with a `float16` base type in bulk on .NET Framework, prefer
> `GetSqlVector<float>` over the string read paths. Both widen the elements, but only the
> string paths also serialize the result.

#### Vector Feature Extension Versions

The vector base types available on a connection are negotiated through the `VECTORSUPPORT`
feature extension (`0x0E`):

| Version | Meaning |
|---------|---------|
| `0` | Native vector support was not negotiated. Vector columns are returned as `varchar(max)`. |
| `1` | `float32` is supported. Columns with any other base type are returned as `varchar(max)`. |
| `2` | `float16` is supported in addition to `float32`. |

The version requested at login is chosen by the `Vector Type Support` connection string
keyword, which may also be written `VectorTypeSupport`, and the server acknowledges the
highest version they have in common:

| Keyword value | Requested version |
|---------------|-------------------|
| `off` | The feature extension is not requested at all. |
| `v1` | `1` |
| `v2` | `2` — this is the **default** starting with Microsoft.Data.SqlClient 8.0. |

The default is `v2`, so a connection requests native support for both base types. When the
server negotiates V2, modern .NET returns a `SqlVector<Half>` instead of a JSON string for a
vector column with a `float16` base type. Applications which need the earlier JSON string
representation can set `Vector Type Support=v1`; `off` keeps all vector results as text.
The equivalent property is
`SqlConnectionStringBuilder.VectorTypeSupport`, of type `SqlVectorTypeSupport`.

The vector row in `GetSchema("DataTypes")` also follows the negotiated version. V1 reports
`CreateFormat = "vector({0})"` and `CreateParameters = "dimensions"`; V2 reports
`CreateFormat = "vector({0}, {1})"` and `CreateParameters = "dimensions,base_type"`.
No vector row is reported without vector negotiation. The driver's own parameter and bulk
declarations retain `vector(N)` for float32 and use `vector(N, float16)` for float16.

```csharp
var builder = new SqlConnectionStringBuilder(connectionString)
{
    // Preserve JSON string results for vector columns with a float16 base type.
    VectorTypeSupport = SqlVectorTypeSupport.V1
};
```

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

AppContext switches allow runtime behavior changes without modifying connection strings. Driver switches are defined in `LocalAppContextSwitches.cs` and can be set via `AppContext.SetSwitch()` or `runtimeconfig.json`.

### Available Switches

| Switch Name | Default | Description |
|-------------|---------|-------------|
| `Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString` | `false` | Disables Transparent Network IP Resolution by default |
| `Switch.Microsoft.Data.SqlClient.EnableAppConfig` | `true` | .NET only. Controls whether SqlClient reads app.config: configurable retry logic, authentication providers and switch overrides. See [Trimming](#trimming) |
| `Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault` | `false` | Sets `MultiSubnetFailover=true` as the default for all connections |
| `Switch.Microsoft.Data.SqlClient.EnableTransactionIsolationLevelReset` | `false` | Resets a changed session transaction isolation level to `READ COMMITTED` before a pooled connection is reused |
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

### Authentication configuration in app.config

The `SqlClientAuthenticationProviders` (modern) and `SqlAuthenticationProviders`
(legacy) sections are optional. Applications may provide full, partial, or no
configuration, and may register providers in code instead.

- **No config file or section:** this is normal. The driver uses its default providers and settings.
- **Partial configuration:** the driver applies the valid settings and uses defaults for the rest.
- **Invalid entries:** an invalid `useWamBroker` value is traced and ignored, keeping the default
  and other valid settings. Configuration-reading errors (`ConfigurationErrorsException`) are
  traced and produce empty settings without trying legacy. Other exception types propagate;
  initializer and provider validation or activation can still fail.
- **Internal handler metadata mismatches:** assert in Debug. In Release, treat the affected
  string setting or provider list as empty while retaining other valid settings.
  Omitting an optional XML attribute uses the handler's default and is not a metadata mismatch.
- **Precedence:** if the modern section is declared, it wins over the legacy section, even when
  its element is absent. The legacy section is read only when the modern section is missing or
  its handler type doesn't match.
- **Config providers take precedence:** a provider listed in app.config for an authentication
  method can't be replaced by `SqlAuthenticationProvider.SetProvider`.

To diagnose unexpected provider selection, turn on `SqlClientEventSource` tracing.
Skipped or invalid settings are reported there.

These loading policies apply to the new Abstractions loader; the driver continues using its
existing reader until the registry migration in #4821.

On .NET, `Switch.Microsoft.Data.SqlClient.EnableAppConfig` controls whether configuration
is read at all. See [Trimming](#trimming).

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

### Trimming

Setting `EnableAppConfig` to `false` at publish time removes the configuration reading from a trimmed or Native AOT application:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="Switch.Microsoft.Data.SqlClient.EnableAppConfig" Value="false" Trim="true" />
</ItemGroup>
```

Setting it only at run time, with `AppContext.SetSwitch` or `runtimeconfig.json`, stops the reading but leaves the trim warnings. On .NET Framework the switch has no effect, since trimming does not apply there.

### Guidelines for Adding New Switches
1. Define the switch name constant in `LocalAppContextSwitches.cs`
2. Add a cached property with lazy evaluation pattern (see existing switches)
3. Default to `false` — the switch should opt-in to the new behavior
4. Add a test in `LocalAppContextSwitchesTest.cs`
5. Document the switch in this file
