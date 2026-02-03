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

| Property | Description |
|----------|-------------|
| `FullyQualifiedName` | Full namespace + class + method name (e.g., `Namespace.Class.Method`) |
| `Name` | Test method name only |
| `ClassName` | Full namespace + class name (must include namespace) |
| `Priority` | Priority attribute value (integer) |
| `TestCategory` | TestCategory attribute value (string) |

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
| `\|` | OR | `Name~Test1\|Name~Test2` |
| `&` | AND | `ClassName~MyClass&Priority=1` |
| `()` | Grouping | `(Name~Test1\|Name~Test2)&Priority=1` |

## Instructions

1. **Analyze the user's description** to identify:
   - Test names, patterns, or keywords mentioned
   - Class names or namespaces referenced
   - Categories or priorities specified
   - Whether tests should be included or excluded

2. **Choose the appropriate property**:
   - Use `FullyQualifiedName` for namespace + class + method patterns
   - Use `Name` for just the method name
   - Use `ClassName` for filtering by test class (always include namespace)
   - Use `TestCategory` for category-based filtering
   - Use `Priority` for priority-based filtering

3. **Select the correct operator**:
   - Use `~` (contains) for partial matches and patterns
   - Use `=` for exact matches
   - Use `!=` or `!~` for exclusions

4. **Combine conditions** as needed:
   - Use `|` (OR) when any condition should match
   - Use `&` (AND) when all conditions must match
   - Use parentheses `()` for complex logic

5. **Format the output** as a complete `dotnet test` command:
   ```
   dotnet test --filter "<expression>"
   ```

6. **Handle special characters**:
   - Escape `!` with `\!` on Linux/macOS shells
   - Use `%2C` for commas in generic type parameters
   - URL-encode special characters in Name/DisplayName values

## Examples

### Example 1: Run tests containing a keyword
**User says**: "Run all connection tests"
```bash
dotnet test --filter "FullyQualifiedName~Connection"
```

### Example 2: Run tests in a specific class
**User says**: "Run tests in SqlConnectionTest class"
```bash
dotnet test --filter "ClassName=Microsoft.Data.SqlClient.Tests.SqlConnectionTest"
```
Note: If the full namespace is unknown, use contains:
```bash
dotnet test --filter "ClassName~SqlConnectionTest"
```

### Example 3: Run a specific test method
**User says**: "Run the TestOpenConnection test"
```bash
dotnet test --filter "Name=TestOpenConnection"
```

### Example 4: Run tests by category
**User says**: "Run all tests in CategoryA"
```bash
dotnet test --filter "TestCategory=CategoryA"
```

### Example 5: Run high priority tests
**User says**: "Run priority 1 tests"
```bash
dotnet test --filter "Priority=1"
```

### Example 6: Combine multiple conditions (AND)
**User says**: "Run connection tests that are priority 1"
```bash
dotnet test --filter "FullyQualifiedName~Connection&Priority=1"
```

### Example 7: Combine multiple conditions (OR)
**User says**: "Run tests for SqlConnection or SqlCommand"
```bash
dotnet test --filter "FullyQualifiedName~SqlConnection|FullyQualifiedName~SqlCommand"
```

### Example 8: Exclude tests
**User says**: "Run all tests except integration tests"
```bash
dotnet test --filter "FullyQualifiedName!~Integration"
```

### Example 9: Complex filter with grouping
**User says**: "Run connection or command tests that are in CategoryA"
```bash
dotnet test --filter "(FullyQualifiedName~Connection|FullyQualifiedName~Command)&TestCategory=CategoryA"
```

### Example 10: Exclude specific test method
**User says**: "Run all tests except TestSlowOperation"
```bash
dotnet test --filter "Name!=TestSlowOperation"
```

### Example 11: Multiple exclusions
**User says**: "Run tests but skip integration and performance tests"
```bash
dotnet test --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"
```

## Error Handling

- If the user's description is ambiguous, ask for clarification about:
  - Whether they want exact match or contains
  - The full class name or namespace if needed
  - Whether conditions should be AND or OR

- If ClassName filter doesn't work, remind the user that `ClassName` must include the namespace (e.g., `Namespace.ClassName`, not just `ClassName`)

- For complex filters, validate that parentheses are balanced

## Additional Tips

- An expression without any operator is interpreted as `FullyQualifiedName~<value>`
  - Example: `dotnet test --filter xyz` equals `dotnet test --filter "FullyQualifiedName~xyz"`

- All lookups are case-insensitive

- When running on Linux/macOS, escape `!` with backslash: `\!`

- For project-specific tests, add the project path:
  ```bash
  dotnet test path/to/project.csproj --filter "Name~MyTest"
  ```
