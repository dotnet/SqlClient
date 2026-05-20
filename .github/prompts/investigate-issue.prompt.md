---
name: investigate-issue
description: Deep investigation of a GitHub issue — regression detection, code analysis, root cause hypothesis, and a structured findings comment.
argument-hint: <issue number>
agent: agent
tools: ['github/search_issues', 'read/readFile', 'codebase/search']
---

Investigate GitHub issue #${input:issue} in `dotnet/SqlClient`.

This prompt is the diagnostic step that sits between triage (`triage-issue` prompt and the `issue-triage.md` agentic workflow) and fixing (`fix-bug` prompt). Triage decides *what* an issue is and *who* should own it; this prompt decides *what is actually broken and where*. It produces a hypothesis and code references, not a fix.

Perform a thorough initial investigation and post your findings as a comment on the issue. Follow this workflow step-by-step.

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
- If critical environment details are missing, note them but continue investigation with what's available. (The `issue-triage.md` workflow already flags missing env fields; do not re-litigate.)

## 2. Check for Regression

Determine whether this issue is a regression introduced in a recent release.

### 2a. Version History Check
- Identify the **reported version** where the issue occurs.
- Check if the reporter mentions it **worked in a previous version**. Look for phrases like "worked in version X", "broke after upgrading", "regression", "started happening after update", "previously worked", "used to work", "new in version X".
- If a working version is mentioned, note the version range where the regression was introduced.

### 2b. Release Notes Review
- Read the release-notes files for the reported version and the 1-2 prior versions.
- Look for entries that relate to the affected area/component.
- Search for keywords from the issue (error messages, class names, feature names) in the release notes.
- Check if any "Breaking Changes" or "Known Issues" sections mention related behavior.

### 2c. Recent Commits and PRs
- Search for recently merged PRs that touch the affected code area:
  ```
  repo:dotnet/SqlClient is:pr is:merged <component/keyword>
  ```
- Review the diff of relevant merged PRs to see if they could have introduced the issue.
- Check the git log for recent changes to the affected source files.
- Pay special attention to PRs merged between the "last known working version" and the "broken version" if the reporter provided this info.

### 2d. Regression Verdict
Classify as one of:
- **Confirmed regression**: Clear evidence that this worked in version X and broke in version Y, with identifiable PR/commit.
- **Likely regression**: Reporter says it worked before, but exact version boundary is unclear.
- **Not a regression**: Issue appears to be a pre-existing bug, a misconfiguration, or expected behavior change.
- **Inconclusive**: Not enough information to determine.

## 3. Identify Affected Code Area

Use the stack-trace-to-source mapping and source-code-layout tables in `.github/instructions/issue-investigation.instructions.md` to narrow down the likely files.

Quick symptom map:

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

Check for platform-specific file variants (`.netfx.cs`, `.netcore.cs`, `.windows.cs`, `.unix.cs`) and conditional compilation directives (`#if NETFRAMEWORK`, `#if NET`, `#if _WINDOWS`, `#if _UNIX`) — see `architecture.instructions.md` for the full layout.

## 4. Code Analysis

Read the identified source file(s) and focus on:
- The method(s) referenced in the stack trace
- Error handling paths that could produce the reported exception
- Recent changes visible in git blame or recent PRs
- Thread safety concerns or race conditions if the issue is intermittent
- Resource management (disposal, connection lifetime) if the issue involves leaks

Look for:
- **Null reference potential** — Are there unchecked null dereferences?
- **State management issues** — Could the object be in an unexpected state?
- **Platform differences** — Does the code behave differently on the reporter's platform?
- **Version-specific code paths** — Are there `#if` blocks that differ between frameworks?
- **Concurrency issues** — Is shared state accessed without proper synchronization?

## 5. Search for Related Issues and Knowledge

Search for existing issues with similar symptoms:
```
repo:dotnet/SqlClient is:issue <error message keywords>
repo:dotnet/SqlClient is:issue <exception type>
repo:dotnet/SqlClient is:issue <affected component>
```

Check for:
- **Duplicate issues** — Same root cause, possibly different symptoms
- **Previously fixed issues** — Was this fixed before and has it re-emerged?
- **Related PRs** — Open or merged PRs addressing the same area
- **Workarounds** — Known workarounds mentioned in other issues or discussions

## 6. Formulate Initial Hypothesis

Based on all gathered evidence, form one or more hypotheses:
- **Root cause hypothesis**: What is most likely causing this behavior?
- **Confidence level**: High / Medium / Low
- **Supporting evidence**: What evidence supports this hypothesis?
- **What would confirm it**: What additional info or tests would prove/disprove it?

## 7. Post Investigation Comment

Post a single comment on the issue with this structure:

```markdown
## Initial Investigation

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
<Which source files/classes are likely involved, with file paths>

### Initial Findings
<What the code analysis revealed>

### Hypothesis
<Root cause hypothesis with confidence level>

### Related Issues
<Links to related issues/PRs>

### Recommended Next Steps
<What should happen next — additional info needed, suggested fix approach,
or handoff to the `fix-bug` prompt>
```

Keep the comment:
- Professional and clear
- Factual (based on code evidence, not speculation without basis)
- Actionable (tells the reporter or team what's next)

## 8. Update Issue Metadata (only if investigation produced new signal)

- If a regression is confirmed, add the `Regression` label.
- If investigation reveals the issue is in a different area than the triage label suggests, update the area label.
- Do **not** re-do triage classification or change Status/Priority/Size — that is the `triage-issue` prompt's job.

## 9. Output Summary

Provide a short summary back to the invoker:
- **Issue**: #${input:issue} — <title>
- **Regression**: Yes (confirmed/likely) / No / Inconclusive
- **Affected component**: <component name>
- **Hypothesis**: <one-line summary>
- **Confidence**: High / Medium / Low
- **Next step**: investigate further / request more info / handoff to `fix-bug`
