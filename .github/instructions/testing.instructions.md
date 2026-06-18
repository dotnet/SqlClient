---
applyTo: "**/tests/**,**/*Test*.cs"
---
# Testing Guide for Microsoft.Data.SqlClient

## Test Project Structure

```
src/Microsoft.Data.SqlClient/tests/
├── FunctionalTests/          # Tests without SQL Server dependency
├── ManualTests/              # Integration tests requiring SQL Server
├── PerformanceTests/         # Benchmark-style perf validation
├── StressTests/              # Long-running stress coverage
├── UnitTests/                # Unit tests with minimal dependencies
└── tools/
    └── Microsoft.Data.SqlClient.TestUtilities/
        ├── config.default.jsonc  # Template configuration
        └── config.jsonc          # Local test configuration (git-ignored)
```

## Test Categories

### Unit Tests (`UnitTests/`)
- Test individual components in isolation
- No external dependencies (database, network)
- Mock external services when needed
- Fast execution for rapid feedback

### Functional Tests (`FunctionalTests/`)
- Test functionality without SQL Server
- May use in-memory constructs
- Verify API behavior and contracts
- Include parser and builder tests

### Manual Tests (`ManualTests/`)
- Full integration tests with SQL Server
- Require `config.jsonc` setup
- Test real database operations
- Include Always Encrypted, Entra ID tests

## Test Configuration

### Setting Up `config.jsonc`
Copy `config.default.jsonc` to `config.jsonc` and configure:

```json
{
  "TCPConnectionString": "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;",
  "NPConnectionString": "Server=np:localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;",
  "EnclaveEnabled": false,
  "TracingEnabled": true,
  "SupportsIntegratedSecurity": true,
  "UseManagedSNIOnWindows": false
}
```

### Key Configuration Properties

| Property | Description |
|----------|-------------|
| `TCPConnectionString` | Primary TCP connection |
| `NPConnectionString` | Named Pipes connection |
| `AADPasswordConnectionString` | Entra ID password auth |
| `AzureKeyVaultURL` | AKV for encryption tests |
| `EnclaveEnabled` | Enable enclave tests |
| `FileStreamDirectory` | FileStream test path |
| `LocalDbAppName` | LocalDB instance name |

## Test Categories and Attributes

### Category Exclusions
Use `[Trait("Category", "...")]` (xUnit, used in both ManualTests and UnitTests) to mark test categories and exclusions:

| Category | Excluded On | Description |
|----------|-------------|-------------|
| `nonnetfxtests` | .NET Framework | Test uses .NET Core/.NET-only APIs |
| `nonnetcoreapptests` | .NET Core/.NET | Test uses .NET Framework-only APIs |
| `nonwindowstests` | Windows | Test targets non-Windows behavior |
| `nonlinuxtests` | Linux | Test targets non-Linux behavior |
| `nonuaptests` | UAP/UWP | Not applicable for UAP |
| `failing` | All platforms | Known permanent failures |
| `flaky` | All platforms (quarantine) | Intermittently failing tests (see Quarantine Zone below) |

### Flaky Test Quarantine Zone
Tests that intermittently fail are quarantined with `[Trait("Category", "flaky")]`. Quarantined tests:
- Are **excluded** from regular test runs by the default filter: `category!=failing&category!=flaky`
- Run in **separate quarantine pipeline steps** to track their status without blocking CI
- Do **not** collect code coverage
- Should be investigated and fixed, then un-quarantined by removing the trait

**When to quarantine a test:**
- The test fails intermittently in CI but passes most of the time
- The failure is not caused by a real product bug (e.g., timing, resource contention, test infrastructure)
- A fix is not immediately available

**How to quarantine:**
```csharp
// For unit tests (xUnit Trait)
[Trait("Category", "flaky")]
public class FlakyConnectionTests { ... }

// For individual test methods
[Trait("Category", "flaky")]
[ConditionalFact(...)]
public async Task OpenAsync_TimingDependent_MayFail() { ... }
```

**How to un-quarantine:** Remove the `[Trait("Category", "flaky")]` attribute once the root cause is fixed and the test passes consistently.

### Test Timeout Enforcement
All test runs use `--blame-hang-timeout 10m` to kill tests that hang for more than 10 minutes. This is configured in `build.proj` and applied to all test targets. If a test is expected to run longer than 10 minutes, it must be restructured or split.

### Test Filter Configuration
The default test filter is defined in `build.proj` via `TestFilters`:
```xml
<TestFilters Condition="'$(TestFilters)' == ''">category!=failing&amp;category!=flaky&amp;category!=interactive</TestFilters>
```
This can be overridden via build property: `dotnet build build.proj -t:TestSqlClientUnit -p:TestFilters="your_filter"`.

### Test Attributes
```csharp
// Platform-specific exclusion
[Trait("Category", "nonlinuxtests")]
public void TestWindowsSpecificFeature() { ... }

// Skip on .NET Framework
[Trait("Category", "nonnetfxtests")]
public void TestNetCoreOnlyFeature() { ... }

// Conditional skip based on test configuration
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public void TestRequiresDatabase() { ... }

// Quarantined flaky test
[Trait("Category", "flaky")]
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public void TestIntermittentlyFails() { ... }
```

## Running Tests

### Using `build.proj` targets (Recommended)
```bash
# Build and run all unit tests
dotnet build build.proj -t:TestSqlClientUnit

# Run functional tests only
dotnet build build.proj -t:TestSqlClientFunctional

# Run manual tests for specific framework
dotnet build build.proj -t:TestSqlClientManual -p:TestFramework=net8.0

# Run specific test set
dotnet build build.proj -t:TestSqlClientManual -p:TestSet=1
```

### Using dotnet CLI
```bash
# Unit tests
dotnet test "src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj" \
  -p:Configuration=Release

# Functional tests with filter (excludes failing, flaky, and interactive tests)
dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.FunctionalTests.csproj" \
  --filter "category!=failing&category!=flaky&category!=interactive"

# Run ONLY quarantined flaky tests (for investigation)
dotnet test ... --filter "category=flaky"

# Single test
dotnet test ... --filter "FullyQualifiedName=Namespace.ClassName.MethodName"
```

## Writing Tests

### Test Documentation Requirements

To keep tests maintainable for contributors and AI agents, test intent must be documented at
class and method level.

#### Required XML Documentation
- Add XML `<summary>` comments to every test class.
- Add XML `<summary>` comments to every test method (`[Fact]`, `[Theory]`, conditional variants).
- For helper methods used by tests, add XML `<summary>` comments and XML `<param>` / `<returns>`
  where applicable.
- For fixture and collection types, add XML `<summary>` comments describing why the fixture exists
  (for example, serialization of console-mutating tests).

#### What the Comments Must Explain
- The behavior/contract being tested (not just restating the method name).
- Why the scenario matters (for example: regression guard, parsing contract, sync/async parity,
  isolation requirement, thread-safety validation, edge case handling).
- For helper methods, what side effects occur (for example console redirection, file system
  copying, process execution) and why they are needed.

#### Implementation Guidance
- Always use the **Arrange-Act-Assert (AAA)** pattern with explicit section comments (see [Test Structure](#test-structure-arrange-act-assert-aaa-pattern)).
- Keep the test body focused on a single logical assertion; avoid testing multiple unrelated behaviors.
- Extract complex setup into helper methods or fixtures rather than embedding it in the test.

#### Style Guidance
- Keep comments concise and factual.
- Prefer behavior-focused wording over implementation trivia.
- Avoid comments that merely repeat obvious code.
- Use inline comments inside test methods only for non-obvious setup/act/assert details.

#### Example
```csharp
/// <summary>
/// Ensures malformed connection strings return a non-zero exit code and emit a parse error
/// without verbose exception details.
/// </summary>
[Fact]
public void AppRunWithMalformedConnectionStringReturnsOneAndWritesParseError()
{
        // Arrange / Act / Assert
}
```

### Test Structure: Arrange-Act-Assert (AAA) Pattern

All test methods **must** follow the **Arrange-Act-Assert (AAA)** pattern with explicit section comments.
This pattern improves readability and maintainability by clearly delineating setup, execution, and validation.

**Pattern:**
```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    // Set up test fixtures, initial state, dependencies, and test data
    var sut = new SystemUnderTest();
    var input = new TestData();

    // Act
    // Execute the code under test
    var result = sut.PerformAction(input);

    // Assert
    // Validate outcomes and expectations
    Assert.Equal(expected, result);
}
```

**Benefits:**
- Clear visual separation of setup, execution, and validation
- Easier to identify test logic flow
- Simpler to debug failing tests (know which section failed)
- Consistent convention across codebase
- Improves AI agent comprehension and test generation

**Guideline:** Each section should be visually distinct. If AAA sections become too large (e.g., Arrange > 20 lines),
consider extracting helper methods or fixtures rather than embedding complexity in the test method itself.

### Naming Conventions
- Test class: `{ClassName}Tests`
- Test method: `{Method}_{Scenario}_{ExpectedBehavior}`
- Descriptive names that document behavior

### Common Patterns

#### Connection Tests
```csharp
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public async Task OpenAsync_ValidConnection_Succeeds()
{
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
    await connection.OpenAsync();
    Assert.Equal(ConnectionState.Open, connection.State);
}
```

#### Error Handling Tests
```csharp
[Fact]
public void Parse_InvalidInput_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => Parser.Parse(null));
}
```

#### Parameterized Tests
```csharp
[Theory]
[InlineData("Server=host", "host")]
[InlineData("Data Source=host", "host")]
public void ServerProperty_VariousSyntax_ExtractsCorrectly(string connStr, string expected)
{
    var builder = new SqlConnectionStringBuilder(connStr);
    Assert.Equal(expected, builder.DataSource);
}
```

## Test Best Practices

### DO
- Write tests before or alongside implementation (test-driven approach)
- Test both sync and async code paths where the API exposes both (e.g., `Open`/`OpenAsync`, `ExecuteReader`/`ExecuteReaderAsync`)
- Test edge cases and error conditions
- Use descriptive test names
- Clean up resources (use `using` statements)
- Make tests independent and isolated

### DON'T
- Depend on test execution order
- Use hardcoded connection strings
- Leave long-running tests without timeouts
- Skip error handling verification
- Write tests that depend on timing
- Test only the sync path when an async equivalent exists

### Sync and Async Test Coverage
Microsoft.Data.SqlClient exposes both synchronous and asynchronous APIs for most operations. **Tests must cover both code paths** because they often have different internal implementations (e.g., different state machine handling, different buffer management, different error propagation).

#### Pattern: Test Both Sync and Async
```csharp
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public void Connection_Open_Succeeds()
{
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
    connection.Open();
    Assert.Equal(ConnectionState.Open, connection.State);
}

[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public async Task Connection_OpenAsync_Succeeds()
{
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
    await connection.OpenAsync();
    Assert.Equal(ConnectionState.Open, connection.State);
}
```

#### Pattern: Shared Helper with Sync/Async Variants
```csharp
[ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
[InlineData(true)]   // async
[InlineData(false)]  // sync
public async Task ExecuteCommand_ReturnsExpectedRows(bool async)
{
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
    if (async)
        await connection.OpenAsync();
    else
        connection.Open();

    using var command = new SqlCommand("SELECT 1", connection);
    object result = async
        ? await command.ExecuteScalarAsync()
        : command.ExecuteScalar();
    Assert.Equal(1, (int)result);
}
```

#### Key Sync/Async API Pairs to Cover
| Sync Method | Async Method |
|-------------|-------------|
| `SqlConnection.Open()` | `SqlConnection.OpenAsync()` |
| `SqlCommand.ExecuteReader()` | `SqlCommand.ExecuteReaderAsync()` |
| `SqlCommand.ExecuteNonQuery()` | `SqlCommand.ExecuteNonQueryAsync()` |
| `SqlCommand.ExecuteScalar()` | `SqlCommand.ExecuteScalarAsync()` |
| `SqlCommand.ExecuteXmlReader()` | `SqlCommand.ExecuteXmlReaderAsync()` |
| `SqlDataReader.Read()` | `SqlDataReader.ReadAsync()` |
| `SqlDataReader.NextResult()` | `SqlDataReader.NextResultAsync()` |
| `SqlDataReader.GetFieldValueAsync<T>()` | (async only — test alongside sync `GetValue()`) |
| `SqlBulkCopy.WriteToServer()` | `SqlBulkCopy.WriteToServerAsync()` |

## Test Utilities

### DataTestUtility
Common test helper class:
```csharp
DataTestUtility.TCPConnectionString  // Get TCP connection
DataTestUtility.AreConnStringsSetup  // Check if config exists
DataTestUtility.IsAADPasswordConnStrSetup  // Check Entra ID config
```

### AssertExtensions
Extended assertions for SqlClient:
```csharp
AssertExtensions.ThrowsContains<SqlException>(() => action(), "expected message");
```

### RAII Database Object Classes
When writing manual integration tests that require transient database objects, use the RAII classes from `Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects` instead of manually writing `try/finally` blocks with DDL `DROP`/`CREATE` statements.

**Available classes:**

| Class | SQL generated | Example definition argument |
|-------|--------------|----------------------------|
| `Table` | `CREATE TABLE {Name} {definition}` | `"(Id INT, Value NVARCHAR(100))"` |
| `StoredProcedure` | `CREATE PROCEDURE {Name} {definition}` | `"AS BEGIN SELECT 1 END"` |
| `UserDefinedType` | `CREATE TYPE [dbo].{Name} AS {definition}` | `"TABLE (f1 INT)"` |

Each class generates a unique object name from the given prefix (incorporating a timestamp-based GUID, username, and machine name), creates the object on construction (requiring the connection to already be open), and drops it when disposed. The generated name is available via the `.Name` property.

**Pattern:**
```csharp
using SqlConnection conn = new(DataTestUtility.TCPConnectionString);
conn.Open();

using Table testTable = new(conn, "MyTable", "(Id INT, Name NVARCHAR(100))");
using StoredProcedure proc = new(conn, "MyProc", $"AS BEGIN SELECT * FROM {testTable.Name} END");

using SqlCommand cmd = conn.CreateCommand();
cmd.CommandText = proc.Name;
cmd.CommandType = CommandType.StoredProcedure;
// ... objects are automatically dropped when the scope ends
```

**Rules:**
- Open the connection **before** constructing any database object (the constructor executes DDL immediately)
- When objects depend on each other (e.g., a stored procedure that references a table), declare the dependent object **last** so it is disposed first — `using` declarations are disposed in reverse order
- Use the `.Name` property directly wherever you need to reference the object in SQL; for `UserDefinedType` this already includes the `[dbo].` schema prefix, making it suitable for use as a TVP `TypeName`

## Code Coverage

### Running with Coverage
```bash
dotnet build build.proj -t:TestSqlClientUnit -p:TestCodeCoverage=true
```

### Coverage Targets
- Focus on public API coverage
- Ensure error paths are covered
- Track coverage trends over time

## Debugging Tests

### IDE
1. Set breakpoints in test code
2. Right-click test → Debug Test (or use CodeLens "Debug Test" link)
3. Use Test Explorer for navigation

### Command Line
```bash
# Enable verbose output
dotnet test --logger "console;verbosity=detailed"
```

## CI/CD Integration

Tests run automatically in Azure DevOps pipelines:
- PR validation runs all test categories (excluding `failing` and `flaky`)
- CI builds run full test matrix across frameworks and OS combinations
- Quarantined (`flaky`) tests run in separate pipeline steps for monitoring
- Test results are published to pipeline artifacts
- Tests that hang beyond 10 minutes are automatically terminated with `--blame-hang-timeout 10m`

### Test Sets for Parallelization
Tests are divided into sets to run in parallel in CI:
- `TestSet=1` — First partition of manual tests
- `TestSet=2` — Second partition
- `TestSet=3` — Third partition
- `TestSet=AE` — Always Encrypted tests (controlled by `runAlwaysEncryptedTests` pipeline parameter)

See [ado-pipelines.instructions.md](ado-pipelines.instructions.md) for pipeline details.
