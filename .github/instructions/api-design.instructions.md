---
applyTo: "**/ref/**,**/Sql*.cs"
---
# API Design Guidelines

## Public API Principles

Microsoft.Data.SqlClient follows strict API design guidelines to ensure:
- Backward compatibility
- Cross-platform consistency
- Industry-standard patterns (ADO.NET)

## Reference Assemblies

### Structure
```
src/Microsoft.Data.SqlClient/
├── netcore/ref/          # .NET Core/.NET public APIs
│   └── Microsoft.Data.SqlClient.cs
└── netfx/ref/            # .NET Framework public APIs
    └── Microsoft.Data.SqlClient.cs
```

### API Surface
Reference assemblies define the public contract:
- Classes, interfaces, enums
- Public and protected members
- Method signatures (no implementation)

### Updating Public APIs
When adding or modifying public APIs:
1. Update reference assembly in BOTH `netcore/ref/` and `netfx/ref/`
2. Ensure signatures match across platforms
3. Add XML documentation
4. Consider backward compatibility

## ADO.NET Interface Compliance

### Required Interfaces
```csharp
// SqlConnection implements:
public class SqlConnection : DbConnection, ICloneable
{
    // DbConnection abstract members
    public override string ConnectionString { get; set; }
    public override string Database { get; }
    public override ConnectionState State { get; }
    public override void Open();
    public override void Close();
    protected override DbCommand CreateDbCommand();
    // ...
}

// SqlCommand implements:
public class SqlCommand : DbCommand, ICloneable
{
    // DbCommand abstract members
    public override string CommandText { get; set; }
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; }
    // ...
}
```

### DbParameter Hierarchy
```
DbParameter (abstract)
    └── SqlParameter
        ├── ParameterName
        ├── SqlDbType
        ├── Value
        └── Size, Direction, etc.
```

## Naming Conventions

### Classes and Interfaces
```csharp
// Prefix Sql for SqlClient types
public class SqlConnection { }
public class SqlCommand { }
public class SqlDataReader { }

// Standard suffixes
public class SqlException : Exception { }
public class SqlClientFactory : DbProviderFactory { }
public interface ISqlVector { }
```

### Methods
```csharp
// Async methods end in Async
public Task<int> ExecuteNonQueryAsync(CancellationToken token);
public ValueTask<bool> ReadAsync(CancellationToken token);

// Try pattern for parsing
public static bool TryParse(string value, out SqlConnectionEncryptOption result);
```

### Properties
```csharp
// Boolean properties use Is/Has prefix when appropriate
public bool IsOpen { get; }
public bool HasRows { get; }

// Collection properties are plural
public SqlParameterCollection Parameters { get; }
```

## Compatibility Considerations

### Breaking Changes
A breaking change is any modification that causes existing code to:
- Fail to compile
- Behave differently at runtime
- Throw new exceptions

### Avoiding Breaking Changes
```csharp
// DO: Add optional parameters with defaults
public void Execute(string sql, int timeout = 30);

// DON'T: Change existing parameter types
public void Execute(string sql); // Existing
public void Execute(ReadOnlySpan<char> sql); // Breaking!

// DO: Add new overloads
public void Execute(ReadOnlySpan<char> sql); // New overload is OK
```

### Deprecation Process
```csharp
// Step 1: Mark obsolete with warning
[Obsolete("Use NewMethod instead. This will be removed in version X.")]
public void OldMethod() { }

// Step 2: In next major version, mark as error
[Obsolete("Use NewMethod instead.", error: true)]
public void OldMethod() { }

// Step 3: Remove in subsequent major version
```

## Connection String Keywords

### Adding New Keywords
1. Define in `SqlConnectionStringBuilder`
2. Add to connection string parser
3. Default to backward-compatible value
4. Document in release notes

### Keyword Naming
```csharp
// Use clear, descriptive names
"Encrypt=Mandatory"
"Trust Server Certificate=True"
"Application Intent=ReadOnly"

// Support common aliases
"Data Source" = "Server" = "Address"
"Initial Catalog" = "Database"
```

## Exception Design

### Exception Hierarchy
```
Exception
└── SystemException
    └── DbException
        └── SqlException
            ├── Errors (SqlErrorCollection)
            ├── Server
            ├── Number
            └── Class (severity)
```

### Throwing Exceptions
```csharp
// Include meaningful information
throw new ArgumentNullException(nameof(connectionString));

// Use specific exception types
throw new InvalidOperationException("Connection is not open.");

// Include inner exception when wrapping
throw new SqlException("Connection failed.", innerException);
```

## Documentation Requirements

### XML Documentation
All public APIs MUST have XML documentation:
```csharp
/// <summary>
/// Opens a database connection.
/// </summary>
/// <exception cref="InvalidOperationException">
/// The connection is already open.
/// </exception>
/// <exception cref="SqlException">
/// A connection-level error occurred.
/// </exception>
public override void Open() { }
```

### Required Elements
- `<summary>`: Brief description
- `<param>`: Parameter descriptions
- `<returns>`: Return value description
- `<exception>`: Possible exceptions
- `<remarks>`: Additional details (optional)

## Event Design

### Event Patterns
```csharp
// Define event arguments
public class SqlInfoMessageEventArgs : EventArgs
{
    public SqlErrorCollection Errors { get; }
    public string Message { get; }
}

// Define delegate (or use EventHandler<T>)
public delegate void SqlInfoMessageEventHandler(
    object sender, SqlInfoMessageEventArgs e);

// Define event
public event SqlInfoMessageEventHandler InfoMessage;
```

### Raising Events
```csharp
// Safe event invocation
protected virtual void OnInfoMessage(SqlInfoMessageEventArgs e)
{
    InfoMessage?.Invoke(this, e);
}
```

## Async API Design

### Async Methods
```csharp
// Provide CancellationToken overload
public Task<int> ExecuteNonQueryAsync();
public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken);

// Use ValueTask for frequently-synchronous operations
public ValueTask<bool> ReadAsync(CancellationToken token);
```

### ConfigureAwait
```csharp
// Use ConfigureAwait(false) in library code
var result = await command.ExecuteReaderAsync(token)
    .ConfigureAwait(false);
```

## Type Design

### Enums
```csharp
// Use explicit values for persistence
public enum SqlDbType
{
    BigInt = 0,
    Binary = 1,
    Bit = 2,
    // ...
}

// Use [Flags] for bitwise combinations
[Flags]
public enum SqlBulkCopyOptions
{
    Default = 0,
    KeepIdentity = 1,
    CheckConstraints = 2,
    // ...
}
```

### Structs vs Classes
- Use `struct` for small, immutable value types
- Use `class` for reference semantics and inheritance

## Code Samples

New public APIs should include samples in `doc/samples/`:
```csharp
// doc/samples/SqlConnection_Open.cs
public static void Main()
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    Console.WriteLine("Connected to: " + connection.Database);
}
```
