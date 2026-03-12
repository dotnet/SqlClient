---
name: investigate-issue
description: Deep investigation of a GitHub issue — regression detection, code analysis, root cause hypothesis, and initial findings comment.
argument-hint: <issue number>
agent: agent
tools: ['github/search_issues', 'read/readFile', 'codebase/search']
---

Investigate GitHub issue #${input:issue} in `dotnet/SqlClient`.

Perform a thorough initial investigation and post your findings as a comment on the issue. Follow this workflow step-by-step:

## 1. Fetch and Understand the Issue

- Retrieve the full issue details: title, body, labels, author, comments.
- Extract key information:
  - **Error message / exception type** (e.g., `SqlException`, `InvalidOperationException`, `TimeoutException`)
  - **Stack trace** — identify the top frames that reference `Microsoft.Data.SqlClient` code
  - **Reported SqlClient version** — note the exact version (e.g., `6.0.0-preview4`, `5.2.2`)
  - **Target framework** — `.NET 8.0`, `.NET Framework 4.6.2`, etc.
  - **SQL Server version** — `SQL Server 2022`, `Azure SQL Database`, etc.
  - **OS / Platform** — Windows, Linux, macOS, Docker
  - **Repro steps or code** — save these for later reproduction analysis
- If critical environment details are missing, note them but continue investigation with what's available.

## 2. Check for Regression

This is a critical step. Determine whether this issue is a regression introduced in a recent release.

### 2a. Version History Check
- Identify the **reported version** where the issue occurs.
- Check if the reporter mentions it **worked in a previous version**. Look for phrases like:
  - "worked in version X", "broke after upgrading", "regression", "started happening after update"
  - "previously worked", "used to work", "new in version X"
- If a working version is mentioned, note the version range where the regression was introduced.

### 2b. Release Notes Review
- Read the release notes files under `release-notes/` for the reported version and the 1-2 prior versions.
- Look for entries that relate to the affected area/component.
- Search for keywords from the issue (error messages, class names, feature names) in the release notes.
- Check if any "Breaking Changes" or "Known Issues" sections mention related behavior.

### 2c. Recent Commits and PRs
- Search for recently merged PRs that touch the affected code area:
  ```
  repo:dotnet/SqlClient is:pr is:merged <component/keyword>
  ```
- Review the diff of relevant merged PRs to see if they could have introduced the issue.
- Check the git log (`git log --oneline -20 -- <affected file paths>`) for recent changes to the affected source files.
- Pay special attention to PRs merged between the "last known working version" and the "broken version" if the reporter provided this info.

### 2d. Regression Verdict
Based on the above, classify:
- **Confirmed regression**: Clear evidence that this worked in version X and broke in version Y, with identifiable PR/commit.
- **Likely regression**: Reporter says it worked before, but exact version boundary is unclear.
- **Not a regression**: Issue appears to be a pre-existing bug, a misconfiguration, or expected behavior change.
- **Inconclusive**: Not enough information to determine.

## 3. Identify Affected Code Area

- Based on the issue description, stack trace, and error, identify the likely source file(s) in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`.
- Map the reported behavior to specific components:

| Symptom | Likely Component(s) |
|---------|---------------------|
| Connection failures | `SqlConnection`, `SqlInternalConnection`, `TdsParser`, SNI layer |
| Authentication errors | `SqlInternalConnectionTds`, `SqlAuthenticationProvider*`, SSPI classes |
| Timeout issues | `SqlConnection`, `SqlCommand`, `TdsParserStateObject`, connection pool |
| Query execution errors | `SqlCommand`, `SqlDataReader`, `TdsParser` |
| Connection pool problems | `SqlConnectionPool*`, `DbConnectionPool*` |
| Encryption/TLS errors | `TdsParser`, SNI layer, `SqlConnectionEncryptOption` |
| Bulk copy issues | `SqlBulkCopy` |
| Transaction problems | `SqlTransaction`, `SqlDelegatedTransaction`, `SqlInternalTransaction` |
| MARS issues | `SqlDataReaderSmi`, `TdsParserSession*`, `SNIMarsHandle` |
| Data type issues | `SqlBuffer`, `MetaType`, `TdsValueSetter` |

- Check for platform-specific file variants (`.netfx.cs`, `.netcore.cs`, `.windows.cs`, `.unix.cs`) that may contain the affected code path.
- Note any conditional compilation directives (`#if NETFRAMEWORK`, `#if NET`, `#if _WINDOWS`, `#if _UNIX`) in the affected code.

## 4. Code Analysis

- Read the identified source file(s) and focus on:
  - The method(s) referenced in the stack trace
  - Error handling paths that could produce the reported exception
  - Recent changes visible in git blame or recent PRs
  - Thread safety concerns or race conditions if the issue is intermittent
  - Resource management (disposal, connection lifetime) if the issue involves leaks
- Look for:
  - **Null reference potential** — Are there unchecked null dereferences?
  - **State management issues** — Could the object be in an unexpected state?
  - **Platform differences** — Does the code behave differently on the reporter's platform?
  - **Version-specific code paths** — Are there `#if` blocks that differ between frameworks?
  - **Concurrency issues** — Is shared state accessed without proper synchronization?

## 5. Search for Related Issues and Knowledge

- Search for existing issues with similar symptoms:
  ```
  repo:dotnet/SqlClient is:issue <error message keywords>
  repo:dotnet/SqlClient is:issue <exception type>
  repo:dotnet/SqlClient is:issue <affected component>
  ```
- Check if there are:
  - **Duplicate issues** — Same root cause, possibly different symptoms
  - **Previously fixed issues** — Was this fixed before and has re-emerged?
  - **Related PRs** — Open or merged PRs addressing the same area
  - **Workarounds** — Known workarounds mentioned in other issues or discussions
- Search external resources:
  - Microsoft Learn documentation for the affected feature
  - Known issues in SQL Server for the reported version

## 6. Formulate Initial Hypothesis

Based on all gathered evidence, form one or more hypotheses:
- **Root cause hypothesis**: What is most likely causing this behavior?
- **Confidence level**: High / Medium / Low
- **Supporting evidence**: What evidence supports this hypothesis?
- **What would confirm it**: What additional info or tests would prove/disprove it?

## 7. Post Investigation Comment

Post a detailed comment on the issue with the following structure:

```markdown
## 🔍 Initial Investigation

### Environment Summary
| Detail | Value |
|--------|-------|
| SqlClient Version | <version> |
| .NET Target | <framework> |
| SQL Server | <version> |
| OS | <platform> |

### Regression Analysis
<Regression verdict and supporting evidence>

### Affected Component
<Which source files/classes are likely involved>

### Initial Findings
<What the code analysis revealed>

### Hypothesis
<Root cause hypothesis with confidence level>

### Related Issues
<Links to related issues/PRs>

### Recommended Next Steps
<What should happen next — additional info needed, proposed fix approach, etc.>
```

Ensure the comment is:
- Professional and clear
- Factual (based on code evidence, not speculation without basis)
- Actionable (tells the reporter or team what's next)
- Includes `@` mentions for relevant team members if the issue is urgent (P0/P1)

## 8. Update Issue Metadata

- If regression is confirmed or likely, add label: `Regression`
- Update GitHub Project (https://github.com/orgs/dotnet/projects/588/) fields:
  - **Status**: `Investigating`
  - **Priority**: Based on severity assessment
  - **Size**: Based on complexity of likely fix
  - **Comment**: Brief rationale

## 9. Output Summary

Provide a summary:
- **Issue**: #<number> — <title>
- **Type**: Bug / Feature / Task
- **Regression**: Yes (confirmed/likely) / No / Inconclusive
- **Affected component**: <component name>
- **Hypothesis**: <one-line summary>
- **Confidence**: High / Medium / Low
- **Priority recommendation**: P0 / P1 / P2 / P3
- **Next steps**: <action items>
