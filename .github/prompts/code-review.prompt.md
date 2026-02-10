---
name: code-review
description: AI-assisted code review for a pull request in Microsoft.Data.SqlClient.
argument-hint: <PR number or branch name>
agent: agent
tools: ['github/search_issues', 'read/readFile', 'codebase/search']
---

Review the pull request "${input:pr}" in `dotnet/SqlClient`.

Follow this structured review process:

## 1. Understand the Change
- Fetch the PR details: title, description, linked issue(s), and diff.
- Read the PR description to understand the intent and scope of the change.
- Check which files are modified and categorize them:
  - **Source code** (`src/Microsoft.Data.SqlClient/src/`) — the main review focus
  - **Tests** (`tests/`) — verify coverage
  - **Reference assemblies** (`netcore/ref/`, `netfx/ref/`) — API surface changes
  - **Documentation** (`doc/`) — doc updates
  - **Pipelines** (`eng/pipelines/`) — CI/CD changes
  - **Legacy directories** (`netfx/src/`, `netcore/src/`) — should generally only have deletions

## 2. Architectural Compliance
Verify the change follows project architecture rules:
- [ ] New source code is in `src/Microsoft.Data.SqlClient/src/` — NOT in legacy `netfx/src/` or `netcore/src/`
- [ ] If public APIs are added/changed, reference assemblies in `netcore/ref/` and `netfx/ref/` are updated
- [ ] Code compiles for all target frameworks: `net462`, `net8.0`, `net9.0`
- [ ] Platform-specific code uses correct file suffixes (`.netfx.cs`, `.netcore.cs`, `.windows.cs`, `.unix.cs`)
- [ ] Conditional compilation uses correct directives (`#if NETFRAMEWORK`, `#if NET`, `#if _WINDOWS`, `#if _UNIX`)
- [ ] Prefer `#if NET` over `#if NETCOREAPP` in this repo

## 3. Code Quality Review
Check against `policy/coding-style.md` and `policy/coding-best-practices.md`:

### Style
- Allman-style braces, 4-space indentation
- `_camelCase` for private fields, `s_camelCase` for static fields
- `PascalCase` for public members
- No unnecessary `this.` qualifiers
- Proper use of `var` (only when type is obvious)

### Best Practices
- Proper `using`/`await using` for disposable resources
- No `async void` methods
- Parameterized queries (no string concatenation for SQL)
- No credential/secret logging in trace or error messages
- Use `ArrayPool<T>` or `Span<T>` for hot-path allocations
- Proper null checks and argument validation
- Exception handling follows existing patterns

## 4. Security Review
- [ ] No credentials, passwords, or secrets in code or comments
- [ ] Certificate validation is not bypassed without proper justification
- [ ] SQL injection prevention via parameterized queries
- [ ] Encryption defaults are secure (Encrypt=Mandatory is default)
- [ ] Sensitive data is not logged via EventSource or DiagnosticListener
- [ ] `SecureString` or credential objects are cleared after use

## 5. Test Coverage Review
- [ ] Bug fixes have a test that would have caught the bug
- [ ] New features have unit, functional, and/or manual tests as appropriate
- [ ] Both sync and async code paths are tested where the API exposes both variants
- [ ] Tests follow naming convention: `{Method}_{Scenario}_{ExpectedBehavior}`
- [ ] Platform-specific tests have correct `[ConditionalFact]` or `[Category]` attributes
- [ ] No flaky patterns (timing-dependent, order-dependent, resource-dependent)
- [ ] Tests do NOT use hardcoded connection strings (use `DataTestUtility`)

## 6. API Design Review (if public API changed)
- [ ] Follows .NET API design guidelines
- [ ] No breaking changes to existing public APIs
- [ ] New APIs have XML documentation comments
- [ ] Async methods return `Task`/`Task<T>` and accept `CancellationToken`
- [ ] Overloads are consistent with existing patterns
- [ ] Default parameter values are backward-compatible
- [ ] Obsolete APIs have `[Obsolete]` attribute with migration guidance

## 7. Backward Compatibility
- [ ] Existing behavior is preserved for current consumers
- [ ] Default values for new parameters/settings maintain old behavior
- [ ] No assembly or namespace changes that break existing references
- [ ] If behavior changes, it's documented and opt-in (e.g., via AppContext switch)

## 8. Documentation and Changelog
- [ ] XML doc comments on new/changed public APIs
- [ ] Code samples added to `doc/samples/` if demonstrating new feature
- [ ] PR description references the issue (`Fixes #...`)
- [ ] PR description includes checklist items

## 9. Summary
Provide a review summary with:
- **Verdict**: Approve / Request Changes / Comment
- **Strengths**: What the PR does well
- **Issues Found**: Categorized as Critical / Major / Minor / Nit
- **Suggestions**: Optional improvements or alternative approaches
- **Questions**: Any clarifications needed from the author

