---
applyTo: "**/tests/**,**/*Test*.cs"
---
# Testing Guide for Microsoft.Data.SqlClient

## Test Project Structure

```
src/Microsoft.Data.SqlClient/tests/
├── Common/                   # TestCommon utilities shared across test projects
├── FunctionalTests/          # Tests without SQL Server dependency
├── ManualTests/              # Integration tests requiring SQL Server
├── PerformanceTests/         # Benchmark-style perf validation
├── StressTests/              # Long-running stress coverage
├── UnitTests/                # Unit tests mirroring the tested source folders
│   └── Utilities/            # Utilities used only by UnitTests
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
| `TCPConnectionString` | Primary TCP connection to SQL Server or Azure SQL — used for all tests. Set to an Azure SQL endpoint for Entra ID auth tests. |
| `NPConnectionString` | Named Pipes connection to on-premises SQL Server |
| `AzureKeyVaultURL` | AKV for encryption tests |
| `EnclaveEnabled` | Enable enclave tests |
| `FileStreamDirectory` | FileStream test path |
| `LocalDbAppName` | LocalDB instance name |

## Test Categories and Attributes

### Category Exclusions
Use `[Trait("category", "...")]` (xUnit, used in both ManualTests and UnitTests) to mark test categories and exclusions:

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
Tests that intermittently fail are quarantined with `[Trait("category", "flaky")]`. Quarantined tests:
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
[Trait("category", "flaky")]
public class FlakyConnectionTests { ... }

// For individual test methods
[Trait("category", "flaky")]
[ConditionalFact(...)]
public async Task OpenAsync_TimingDependent_MayFail() { ... }
```

**How to un-quarantine:** Remove the `[Trait("category", "flaky")]` attribute once the root cause is fixed and the test passes consistently.

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
[Trait("category", "nonlinuxtests")]
public void TestWindowsSpecificFeature() { ... }

// Skip on .NET Framework
[Trait("category", "nonnetfxtests")]
public void TestNetCoreOnlyFeature() { ... }

// Conditional skip based on test configuration
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public void TestRequiresDatabase() { ... }

// Quarantined flaky test
[Trait("category", "flaky")]
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
dotnet build build.proj -t:TestSqlClientManual -p:TestFramework=net10.0

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

### Required Test Authoring Rules

These rules are mandatory for unit tests and should be strongly considered for manual and
functional tests. Apply them when adding or changing tests, even if neighboring older tests use
different conventions.

1. **Structure:** Write sections in Arrange, Act, Assert, Cleanup order and mark each included
   section with its own comment (`// Arrange`, `// Act`, `// Assert`, `// Cleanup`). Every test must
   have Act and Assert sections. Include Arrange only when setup is needed, and Cleanup only when
   a special construct needs teardown. If a section has multiple groups of steps, give each group
   its own comment explaining what happens and, when unclear, why. Use `using` for normal resource
   disposal; put special teardown in `finally` or a fixture so it also runs after a failed assertion.
2. **Theory data:** Use `[InlineData]` for compile-time constant data. When it cannot represent the
   parameters, always use `[MemberData]`, never `[ClassData]`. Declare the member's return type as
   `TheoryData<...>` with the theory parameter types, not `IEnumerable<object[]>` or `object[][]`.
3. **Class ownership:** A unit test file tests only the class it is named for. For example,
   `TdsParserTests.cs` contains tests against `TdsParser`, not unrelated classes.
4. **Method names:** Use `{MethodName}_{Input}_{ExpectedOutput}` without the tested class name.
   Examples: `ExecuteReader_NullParameter_Throws` and
   `ExecuteReader_ValidParameters_ReturnsReader`.
5. **File placement:** Mirror the tested class's source folder structure under `UnitTests/`.
   For example, within `src/Microsoft.Data.SqlClient/`,
   `src/Microsoft/Data/SqlClient/Parser/TdsParser.cs` maps to
   `tests/UnitTests/Microsoft/Data/SqlClient/Parser/TdsParserTests.cs`.
6. **Unit-only utilities:** Place helpers used only by UnitTests in
   `src/Microsoft.Data.SqlClient/tests/UnitTests/Utilities/`.
7. **Shared utilities:** Place helpers shared across test projects in the TestCommon project at
   `src/Microsoft.Data.SqlClient/tests/Common/`. Fuzz, stress, and performance test projects are
   excluded from this sharing rule.
8. **Focused scenarios:** Split separate scenarios into separate tests. Do not use switches,
   boolean flags, or mode parameters to change the behavior a test exercises, including selecting
   sync versus async calls. Theories may supply different inputs and expected results for the
   same behavior; they must not dispatch to unrelated scenarios.
9. **Beneficial tests:** Each test must exercise the intended product behavior and assert an
   observable result that would detect a regression. For bug fixes, confirm the test fails for the
   reported bug before the fix and passes afterward. Avoid tautologies, assertions only about
   setup or mocks, and duplicates that add no distinct behavioral protection. Confirm the selected
   tests actually ran: a successful command with zero matches or only skipped tests is not evidence
   of coverage. Report any tests that could not run and why.

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
  isolation requirement).
- For helper methods, what side effects occur (for example console redirection, file system
  copying, process execution) and why they are needed.

#### Style Guidance
- Keep comments concise and factual.
- Prefer behavior-focused wording over implementation trivia.
- Avoid comments that merely repeat obvious code.
- Always include the section and step-group comments required above. Keep additional inline
  comments focused on non-obvious details.

#### Example
```csharp
/// <summary>
/// Ensures the server alias is recognized, preserving the supported keyword contract.
/// </summary>
[Fact]
public void ContainsKey_ServerAlias_ReturnsTrue()
{
    // Arrange
    var builder = new SqlConnectionStringBuilder();

    // Act
    bool result = builder.ContainsKey("Server");

    // Assert
    Assert.True(result);
}
```

### Test Structure
```csharp
/// <summary>Verifies connection string keyword contracts.</summary>
public class SqlConnectionStringBuilderTests
{
    /// <summary>Ensures the server alias remains a supported keyword.</summary>
    [Fact]
    public void ContainsKey_ServerAlias_ReturnsTrue()
    {
        // Arrange
        var builder = new SqlConnectionStringBuilder();

        // Act
        bool result = builder.ContainsKey("Server");

        // Assert
        Assert.True(result);
    }
}
```

### Naming Conventions
- Test class: `{ClassName}Tests`
- Test method: `{MethodName}_{Input}_{ExpectedOutput}`
- Do not include the tested class name in the method name.
- Keep each unit test file focused on its named class and mirror the source folder structure.

### Common Patterns

#### Connection Tests
```csharp
/// <summary>Verifies an asynchronous open establishes a usable connection.</summary>
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public async Task OpenAsync_ValidConnection_Succeeds()
{
    // Arrange
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);

    // Act
    await connection.OpenAsync();

    // Assert
    Assert.Equal(ConnectionState.Open, connection.State);
}
```

#### Error Handling Tests
```csharp
/// <summary>Verifies null keywords fail with the documented argument exception.</summary>
[Fact]
public void ContainsKey_NullKeyword_ThrowsArgumentNullException()
{
    // Arrange
    var builder = new SqlConnectionStringBuilder();

    // Act
    Action action = () => builder.ContainsKey(null);

    // Assert
    Assert.Throws<ArgumentNullException>(action);
}
```

#### Parameterized Tests
```csharp
/// <summary>Ensures both server keyword aliases remain supported.</summary>
[Theory]
[InlineData("Server")]
[InlineData("Data Source")]
public void ContainsKey_ServerAliases_ReturnsTrue(string keyword)
{
    // Arrange
    var builder = new SqlConnectionStringBuilder();

    // Act
    bool result = builder.ContainsKey(keyword);

    // Assert
    Assert.True(result);
}
```

For non-constant parameters, use a member with a typed `TheoryData<...>` return type:

```csharp
/// <summary>Provides builders with and without a server to test removal results.</summary>
public static TheoryData<SqlConnectionStringBuilder, bool> RemoveServerData =>
    new TheoryData<SqlConnectionStringBuilder, bool>
    {
        { new SqlConnectionStringBuilder("Server=host"), true },
        { new SqlConnectionStringBuilder(), false }
    };

/// <summary>Ensures removal reports whether the keyword was explicitly configured.</summary>
[Theory]
[MemberData(nameof(RemoveServerData))]
public void Remove_KeywordPresence_ReturnsExpectedResult(
    SqlConnectionStringBuilder builder, bool expected)
{
    // Act
    bool result = builder.Remove("Data Source");

    // Assert
    Assert.Equal(expected, result);
    Assert.False(builder.ShouldSerialize("Data Source"));
}
```

Each row exercises the same operation; the boolean is an expected result, not a behavior switch.
For exception tests, the Act section may declare the delegate invoked by `Assert.Throws` (or
`Assert.ThrowsAsync`) in Assert; do not invoke the throwing operation before the assertion.

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
/// <summary>Verifies a synchronous open establishes a usable connection.</summary>
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public void Open_ValidConnection_Succeeds()
{
    // Arrange
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);

    // Act
    connection.Open();

    // Assert
    Assert.Equal(ConnectionState.Open, connection.State);
}

/// <summary>Verifies an asynchronous open establishes a usable connection.</summary>
[ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
public async Task OpenAsync_ValidConnection_Succeeds()
{
    // Arrange
    using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);

    // Act
    await connection.OpenAsync();

    // Assert
    Assert.Equal(ConnectionState.Open, connection.State);
}
```

Keep sync and async cases in separate tests as shown above. Shared helpers may prepare common
state, but must not hide behavior selection behind flags or switches.

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
DataTestUtility.TCPConnectionString    // Get TCP connection string (on-prem or Azure SQL)
DataTestUtility.AreConnStringsSetup    // Check if TCP/NP connection strings are configured
DataTestUtility.IsAzureConnStringSetup // Check if TCPConnectionString points to Azure SQL
DataTestUtility.IsAzureSqlConnectionString(connStr) // Detect whether any connection string targets Azure SQL
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
