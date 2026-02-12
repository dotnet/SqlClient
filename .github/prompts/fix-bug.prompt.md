---
name: fix-bug
description: Guided workflow for fixing a bug in Microsoft.Data.SqlClient.
argument-hint: <issue number or description>
agent: agent
tools: ['github/search_issues', 'edit/createFile', 'edit/editFiles', 'read/readFile', 'codebase/search']
---

Fix the bug described in "${input:issue}".

Follow this workflow step-by-step:

## 1. Understand the Bug
- If a GitHub issue number is provided, fetch the issue details from `dotnet/SqlClient`.
- Identify the repro steps, expected behavior, and actual behavior.
- Determine which platforms are affected (.NET Framework, .NET 8+, Windows, Unix).
- If the issue lacks sufficient detail, note what information is missing.

## 2. Locate the Relevant Code
- All source code lives in `src/Microsoft.Data.SqlClient/src/`. Do NOT modify files in the legacy `netcore/src/` or `netfx/src/` directories.
- Search for related classes, methods, or keywords in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`.
- Check for platform-specific files (`.netfx.cs`, `.netcore.cs`, `.windows.cs`, `.unix.cs`) that may contain the affected code path.
- Understand conditional compilation: use `#if NETFRAMEWORK` for net462, `#if NET` for net8.0+, `#if _WINDOWS` / `#if _UNIX` for OS-specific code.

## 3. Write a Failing Test
- Create a test that reproduces the bug BEFORE implementing the fix.
- Choose the correct test project:
  - `tests/UnitTests/` — for isolated logic tests (no SQL Server needed)
  - `tests/FunctionalTests/` — for API behavior tests (no SQL Server needed)
  - `tests/ManualTests/` — for integration tests (requires SQL Server)
- Follow existing naming conventions: `{ClassName}Tests.cs` with methods named `{MethodName}_{Scenario}_{ExpectedResult}`.
- If the bug is platform-specific, add appropriate `[ConditionalFact]` or `[ConditionalTheory]` attributes with `[PlatformSpecific]`.
- **Cover both sync and async code paths** if the affected API has both variants (e.g., `Open`/`OpenAsync`, `ExecuteReader`/`ExecuteReaderAsync`). Sync and async paths often have different internal implementations and a bug may manifest in only one.

## 4. Implement the Fix
- Make the minimal change needed to fix the bug.
- Ensure the fix compiles for ALL target frameworks: `net462`, `net8.0`, `net9.0`.
- If the fix requires platform-specific code, use the appropriate conditional compilation directives.
- Do NOT introduce breaking changes to public APIs.
- If a public API must change, update the reference assemblies in `netcore/ref/` and `netfx/ref/`.

## 5. Validate
- Verify the failing test now passes with the fix applied.
- Check that existing tests in the affected area still pass.
- Review that no new compiler warnings or errors are introduced.

## 6. Document
- Add XML doc comments if the fix changes behavior of documented APIs.
- Add a code sample in `doc/samples/` if the fix demonstrates important usage.

## 7. Prepare for PR
- Summarize the root cause and the fix.
- Reference the issue with `Fixes #<issue_number>`.
- Provide a checklist:
  - [ ] Tests added or updated
  - [ ] Both sync and async code paths tested (where applicable)
  - [ ] Public API changes documented (if applicable)
  - [ ] Verified against customer repro (if applicable)
  - [ ] No breaking changes introduced
