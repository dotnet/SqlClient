---
name: implement-feature
description: Guided workflow for implementing a new feature in Microsoft.Data.SqlClient.
argument-hint: <feature name or issue number>
agent: agent
tools: ['github/search_issues', 'edit/createFile', 'edit/editFiles', 'read/readFile', 'codebase/search']
---

Implement the feature described in "${input:feature}".

Follow this workflow step-by-step:

## 1. Understand the Feature
- If a GitHub issue number is provided, fetch the full issue details from `dotnet/SqlClient`.
- Identify the feature scope, requirements, and acceptance criteria.
- Determine which platforms must be supported (.NET Framework 4.6.2, .NET 8.0, .NET 9.0).
- Check for related issues or prior discussions.
- If the feature involves a new connection string keyword, new data type, or TDS protocol change, note the additional areas impacted.

## 2. Plan the Implementation
Before writing code, produce a brief implementation plan covering:
- **Files to modify or create** — all in `src/Microsoft.Data.SqlClient/src/`. Never add to legacy `netcore/src/` or `netfx/src/`.
- **Public API surface changes** — any new classes, methods, properties, enums, or connection string keywords.
- **Platform-specific considerations** — will the feature need `.netfx.cs`/`.netcore.cs` or `.windows.cs`/`.unix.cs` variants?
- **Dependencies** — any new NuGet packages or framework references needed?
- **Test strategy** — which test projects will cover this feature?
- **Documentation plan** — XML docs, samples, release notes entries.

## 3. Update Reference Assemblies (if public API changes)
- If adding new public APIs, update reference assemblies FIRST:
  - `netcore/ref/Microsoft.Data.SqlClient.cs` and/or `Microsoft.Data.SqlClient.Manual.cs` for .NET Core/.NET APIs
  - `netfx/ref/Microsoft.Data.SqlClient.cs` for .NET Framework APIs
  - `ref/` shared files if the API applies to batch or cross-framework features
- Include only the method/property signatures with no implementation.
- Add XML documentation comments on all public members.

## 4. Implement the Feature
- Add source files to `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`.
- Use appropriate file suffixes for platform-specific code:
  - `.netfx.cs` for .NET Framework only
  - `.netcore.cs` for .NET Core/.NET 8+ only
  - `.windows.cs` for Windows-only code
  - `.unix.cs` for Unix/Linux/macOS-only code
- Use conditional compilation:
  - `#if NETFRAMEWORK` for net462 code paths
  - `#if NET` for net8.0+ code paths (NOT `#if NETCOREAPP`)
  - `#if _WINDOWS` or `#if _UNIX` for OS-specific code
- Ensure the code compiles for ALL target frameworks: `net462`, `net8.0`, `net9.0`.
- Follow the coding standards in `policy/coding-style.md` and `policy/coding-best-practices.md`.

### Connection String Keywords (if applicable)
1. Add the keyword to `SqlConnectionStringBuilder` with getter/setter.
2. Update `DbConnectionStringKeywords` and `DbConnectionStringDefaults`.
3. Add parsing in the connection string parser.
4. Default to a backward-compatible value.
5. Add the keyword to the features reference in `.github/instructions/features.instructions.md`.

### TDS Protocol Changes (if applicable)
1. Reference the MS-TDS specification for the protocol extension.
2. Add new token/flag constants to `TdsEnums.cs`.
3. Implement parsing/writing in `TdsParser.cs` and related files.
4. Test against multiple SQL Server versions.

## 5. Write Tests
- **Unit tests** in `tests/UnitTests/` for isolated logic.
- **Functional tests** in `tests/FunctionalTests/` for API behavior without SQL Server.
- **Manual tests** in `tests/ManualTests/` for full integration with SQL Server.
- Cover:
  - Happy path and edge cases
  - **Both sync and async code paths** where the feature exposes both variants
  - Cross-platform behavior differences
  - Error handling and invalid inputs
  - Backward compatibility (existing behavior unchanged)

## 6. Add Documentation and Samples
- Add XML doc comments on all new public APIs.
- Create a code sample in `doc/samples/` demonstrating usage.
- Update relevant `.github/instructions/` files if the feature adds new architectural patterns.

## 7. Prepare for PR
- Summarize the feature and link to the issue.
- Provide a checklist:
  - [ ] Reference assemblies updated (if public API changed)
  - [ ] Tests added (unit, functional, manual as appropriate)
  - [ ] Both sync and async code paths tested
  - [ ] XML documentation on all public members
  - [ ] Code sample added to `doc/samples/`
  - [ ] Compiles on all target frameworks (`net462`, `net8.0`, `net9.0`)
  - [ ] No breaking changes to existing APIs
  - [ ] Follows coding style (`policy/coding-style.md`)
