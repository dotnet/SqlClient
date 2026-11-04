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
| `MultiSubnetFailover` | | Enable multi-subnet failover (default `true` for TCP only; an explicit value is used as-is) |

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
- Object-typed `DataTable` columns targeting native vectors can contain a consistent base type
  plus nulls. Bulk copy looks ahead through the selected in-memory rows to find the first non-null
  representation, then validates compatibility as rows are copied. Row-array subsets and row-state
  filters are respected; an all-null column can require examining all selected rows. Native
  float32/float16 values cannot be mixed in one incoming column. Existing float32 JSON coercion
  is preserved. Ordinary text destinations do not use this lookahead.
  Reader setup never consumes values. `SqlDataReader` supplies native metadata; other readers'
  `GetFieldType` is an optional hint used only for native vector destinations.
  `NotImplementedException`/`NotSupportedException` are traced as unavailable metadata; other
  errors propagate. Without a hint, existing float32 input behavior remains available, while
  JSON strings can be used for float16 input.
- Synthetic text input for a destination vector column with a `float16` base type uses an explicit
  UTF-8 encoding/collation pair; SQL Server still performs JSON parsing and conversion.
- Bulk copy does not serialize `SqlVector<T>` values for `varchar`, `nvarchar`, or `json`
  destinations, including vector destinations exposed as text under V1/Off. Supply JSON strings
  or select an explicit SQL cast to `varchar(max)` instead. Ordinary string-valued sources,
  including Framework float16 string projections, keep their existing text-conversion path.
  Native vector reader values bypass the text-stream/null pre-check accessors only when the
  incoming metadata is a native vector.
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

The setting is per connection so components in the same .NET process can retain different
result-type contracts, even against the same database. For example, a legacy reporting component
may cast float16 `GetValue` results to `string`, while a newer component reads `SqlVector<Half>`.
Use `Vector Type Support=v1` for the reporting component and `v2` for the newer component.
A process-wide AppContext switch would force the same choice on both. The requested version is
negotiated at login; connection strings with different settings use separate connection pools.

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
| `Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString` | `true` | .NET Framework only. Set to `false` to restore the legacy TNIR default; MSF must also be disabled for TNIR to take effect |
| `Switch.Microsoft.Data.SqlClient.EnableAppConfig` | `true` | .NET only. Controls whether SqlClient reads app.config: configurable retry logic, authentication providers and switch overrides. See [Trimming](#trimming) |
| `Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault` | `true` | Sets the default for `MultiSubnetFailover`; set to `false` to restore the legacy default. Explicit connection-string values take precedence |
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
| `Switch.Microsoft.Data.SqlClient.UseLegacyUdtAssemblyLoad` | `false` | Restores the pre-policy behavior of loading any assembly named by a server-supplied UDT assembly-qualified name, and of skipping the `[SqlUserDefinedType]` check |

### UDT Assembly Load Policy

A server-supplied UDT assembly-qualified name reaches `Assembly.Load`, so the
driver applies a deny-by-default policy before handing the name to the loader.
There is a single enforcing behavior, which permits:

| Permitted | Notes |
|-----------|-------|
| `Microsoft.SqlServer.Types` | The requested version is normalized to the connection's negotiated type system version; a newer version selected by the application's loader is accepted. The culture must be neutral and the public key token must be Microsoft's |
| Assemblies on the allow list | The application explicitly naming what it is willing to have loaded |
| Assemblies already loaded into the process | Resolved to the instance the process already holds; the server-supplied version, culture and public key token are discarded |

Everything else is refused. In particular, an assembly that is only *statically
referenced* by a loaded assembly is **not** permitted, because loading it is a
genuinely new load — precisely what this policy keeps under the application's
control rather than the server's.

Normalizing the reference is necessary but not sufficient. On .NET the loader
**ignores** the public key token in an `AssemblyName`, and can satisfy a request
with a different version than the one asked for, so pinning the reference does
not by itself determine what arrives. A custom `AssemblyResolve` handler or
`AssemblyLoadContext` resolver can go further still and answer with an assembly
of an entirely different name. The driver therefore verifies the identity of the
assembly the loader actually hands back against every component the decision
relied on, including the simple name that the permission was granted to, and
refuses it on any mismatch. This mirrors what the driver already does for the
Azure authentication extension assembly.

The built-in spatial exception treats the negotiated version as a minimum, not
an exact match. This preserves application binding to newer Microsoft-signed
spatial packages while still verifying their name, token, culture, and minimum
version. Custom-assembly versions specified in the application allow list remain
exact matches.

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

Relatedly, the map of loaded assemblies is snapshotted before the policy can
trigger any load of its own, and loads the policy performs are excluded from it
thereafter. Neither is merely a performance choice. Rebuilding the map on
demand, snapshotting it lazily after a permitted load had already run, or
recording the dependencies that arrive alongside a permitted assembly, would all
let an assembly that was pulled in as a *dependency* of a permitted assembly
silently inherit that permission. Together they keep the tier anchored to what
the application loaded of its own accord.

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

Two less obvious paths also materialize the type and are therefore affected:

- `SqlBulkCopy` **from a `SqlDataReader`** between UDT columns. The copy reads
  each value so it can test it for `INullable`, which materializes the UDT.
  Copying *to* a UDT column from a `DataTable`, or to `varbinary(max)`, does not
  resolve the type and is unaffected.
- Table-valued parameters sourced from a `SqlDataReader`, which build SMI
  metadata and resolve the UDT type with throwing enabled.

The exception is a `TypeLoadException` and is not wrapped in a `SqlException`,
which matches how the driver already reports a UDT type it cannot resolve.

The second row is the harder one to diagnose, because `GetFieldType` does not
normally return `null`; a caller that dereferences the result sees an unrelated
`NullReferenceException`. A denial is always traced through
`SqlClientEventSource` regardless of which path was taken, so enabling event
source tracing will identify the assembly.

The remedy in every case is to name the assembly on the allow list.

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
