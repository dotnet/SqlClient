---
name: generate-mstest-filter
description: Generates well-formed MSTest filter expressions for dotnet test. Use this skill when asked to create a test filter, run specific tests, filter tests by name, class, category, or priority, or when the user describes tests they want to run selectively.
---

This skill generates MSTest filter expressions for use with `dotnet test --filter`. Use this when users describe tests they want to run and need a properly formatted filter expression.

## When to Use This Skill

- User asks to run specific tests by name or pattern
- User wants to filter tests by class name, namespace, or method
- User wants to run tests with specific categories or priorities
- User describes a set of tests to include or exclude
- User needs help with test filter syntax for MSTest

## MSTest Filter Syntax Reference

### Supported Properties

| Property | Description | Supported By |
|----------|-------------|--------------|
| `FullyQualifiedName` | Full namespace + class + method name (e.g., `Namespace.Class.Method`) | MSTest, xUnit, NUnit |
| `DisplayName` | The display name of the test (often same as FullyQualifiedName for xUnit) | MSTest, xUnit, NUnit |
| `Name` | Test method name only | MSTest only (not xUnit) |
| `ClassName` | Full namespace + class name (must include namespace) | MSTest only (not xUnit) |
| `Priority` | Priority attribute value (integer) | MSTest (with `[Priority]` attribute) |
| `TestCategory` | TestCategory attribute value (string) | MSTest (with `[TestCategory]` attribute) |

> **Important**: For xUnit tests (common in .NET Core projects), **always use `FullyQualifiedName` or `DisplayName`**. The `Name` and `ClassName` properties are not populated by xUnit and will result in no matches.

### Operators

| Operator | Meaning | Example |
|----------|---------|---------|
| `=` | Exact match | `Name=TestMethod1` |
| `!=` | Not exact match | `Name!=TestMethod1` |
| `~` | Contains | `FullyQualifiedName~Connection` |
| `!~` | Does not contain | `FullyQualifiedName!~Integration` |

### Boolean Operators

| Operator | Meaning | Example |
|----------|---------|---------|
| `\|` | OR | `FullyQualifiedName~Test1\|FullyQualifiedName~Test2` |
| `&` | AND | `FullyQualifiedName~MyClass&Priority=1` |
| `()` | Grouping | `(FullyQualifiedName~Test1\|FullyQualifiedName~Test2)&Priority=1` |

## Instructions

1. **Analyze the user's description** to identify:
   - Test names, patterns, or keywords mentioned
   - Class names or namespaces referenced  
   - Categories or priorities specified
   - Whether tests should be included or excluded
   - Whether the user referenced a **file name** (look for `.cs` extension or file path patterns)

2. **Handle file name inputs**:
   - If the user provides a file name (e.g., `ChannelDbConnectionPoolTest.cs`), extract the class name by removing the `.cs` extension
   - File names typically correspond to the test class name (e.g., `SqlConnectionTest.cs` → class `SqlConnectionTest`)
   - Use `FullyQualifiedName~ClassName` pattern for file-based inputs

3. **Choose the appropriate property**:
   - **For xUnit tests (most .NET Core projects)**: Always use `FullyQualifiedName` or `DisplayName`
   - **For MSTest only**: `Name` and `ClassName` properties are also available
   - Use `FullyQualifiedName~` with contains operator for maximum compatibility
   - Use `TestCategory` for category-based filtering (MSTest only)
   - Use `Priority` for priority-based filtering (MSTest only)

4. **Select the correct operator**:
   - Use `~` (contains) for partial matches and patterns - **this is the safest default**
   - Use `=` for exact matches only when you know the full value
   - Use `!=` or `!~` for exclusions

5. **Combine conditions** as needed:
   - Use `|` (OR) when any condition should match
   - Use `&` (AND) when all conditions must match
   - Use parentheses `()` for complex logic

6. **Format the output** as a complete `dotnet test` command:
   ```
   dotnet test --filter "<expression>"
   ```

7. **Handle special characters**:
   - Escape `!` with `\!` on Linux/macOS shells
   - Use `%2C` for commas in generic type parameters
   - URL-encode special characters in Name/DisplayName values

## Examples

### Example 1: Run tests containing a keyword
**User says**: "Run all connection tests"
```bash
dotnet test --filter "FullyQualifiedName~Connection"
```

### Example 2: Run tests in a specific class (xUnit compatible)
**User says**: "Run tests in SqlConnectionTest class"
```bash
dotnet test --filter "FullyQualifiedName~SqlConnectionTest"
```
> **Note**: Avoid using `ClassName=` for xUnit tests - it won't work. Always use `FullyQualifiedName~` for cross-framework compatibility.

### Example 3: Run tests from a specific file
**User says**: "Run tests in ChannelDbConnectionPoolTest.cs"
```bash
dotnet test --filter "FullyQualifiedName~ChannelDbConnectionPoolTest"
```
> Strip the `.cs` extension and use `FullyQualifiedName~` with the class name.

### Example 4: Run a specific test method
**User says**: "Run the TestOpenConnection test"
```bash
dotnet test --filter "FullyQualifiedName~TestOpenConnection"
```
> Use `FullyQualifiedName~` instead of `Name=` for xUnit compatibility.

### Example 5: Run tests by category (MSTest only)
**User says**: "Run all tests in CategoryA"
```bash
dotnet test --filter "TestCategory=CategoryA"
```

### Example 6: Run high priority tests (MSTest only)
**User says**: "Run priority 1 tests"
```bash
dotnet test --filter "Priority=1"
```

### Example 7: Combine multiple conditions (AND)
**User says**: "Run connection tests that are priority 1"
```bash
dotnet test --filter "FullyQualifiedName~Connection&Priority=1"
```

### Example 8: Combine multiple conditions (OR)
**User says**: "Run tests for SqlConnection or SqlCommand"
```bash
dotnet test --filter "FullyQualifiedName~SqlConnection|FullyQualifiedName~SqlCommand"
```

### Example 9: Exclude tests
**User says**: "Run all tests except integration tests"
```bash
dotnet test --filter "FullyQualifiedName!~Integration"
```

### Example 10: Complex filter with grouping
**User says**: "Run connection or command tests that are in CategoryA"
```bash
dotnet test --filter "(FullyQualifiedName~Connection|FullyQualifiedName~Command)&TestCategory=CategoryA"
```

### Example 11: Exclude specific test method
**User says**: "Run all tests except TestSlowOperation"
```bash
dotnet test --filter "FullyQualifiedName!~TestSlowOperation"
```

### Example 12: Multiple exclusions
**User says**: "Run tests but skip integration and performance tests"
```bash
dotnet test --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"
```

## Verification (Required)

**Always verify the generated filter before presenting it to the user.** Use the `--list-tests` flag to confirm the filter matches the expected tests:

```bash
dotnet test <project.csproj> --list-tests --filter "<your-filter>" --framework <target-framework>
```

### Verification Steps

1. **Run the list-tests command** with the generated filter
2. **Check the output**:
   - If tests are listed → filter is valid
   - If "No test matches the given testcase filter" → filter is invalid, needs adjustment
3. **If no matches**, try these fixes in order:
   - Switch from `ClassName=` or `Name=` to `FullyQualifiedName~`
   - Remove the namespace prefix and use just the class/method name with `~`
   - Try `DisplayName~` as an alternative
4. **Re-run verification** after any changes

### Example Verification

```bash
# Generate filter for "ChannelDbConnectionPoolTest" class
dotnet test tests/UnitTests/UnitTests.csproj --list-tests --filter "FullyQualifiedName~ChannelDbConnectionPoolTest" --framework net9.0

# Expected output shows matching tests:
# The following Tests are available:
#     Microsoft.Data.SqlClient.UnitTests.ConnectionPool.ChannelDbConnectionPoolTest.GetConnectionEmptyPool_ShouldCreateNewConnection(...)
#     ...
```

## Error Handling

- If the user's description is ambiguous, ask for clarification about:
  - Whether they want exact match or contains
  - The full class name or namespace if needed
  - Whether conditions should be AND or OR

- **If a filter returns no matches**:
  - First, verify the test class/method exists in the project
  - Switch to `FullyQualifiedName~` with contains operator
  - Check if the project uses xUnit (common in .NET Core) - if so, avoid `Name` and `ClassName` properties

- For complex filters, validate that parentheses are balanced

## Additional Tips

- An expression without any operator is interpreted as `FullyQualifiedName~<value>`
  - Example: `dotnet test --filter xyz` equals `dotnet test --filter "FullyQualifiedName~xyz"`

- All lookups are case-insensitive

- When running on Linux/macOS, escape `!` with backslash: `\!`

- For project-specific tests, add the project path:
  ```bash
  dotnet test path/to/project.csproj --filter "FullyQualifiedName~MyTest"
  ```

## Common Pitfalls

| Problem | Cause | Solution |
|---------|-------|----------|
| No test matches filter | Using `Name=` or `ClassName=` with xUnit | Use `FullyQualifiedName~` instead |
| No test matches filter | Using just class name without namespace in `ClassName=` | Use `FullyQualifiedName~ClassName` |
| Filter matches too many tests | Using overly broad `~` pattern | Add more specific qualifiers or use `&` with additional conditions |
| TestCategory filter doesn't work | Project uses xUnit, which doesn't support TestCategory | Use `[Trait]` attributes with xUnit and filter by trait name |
