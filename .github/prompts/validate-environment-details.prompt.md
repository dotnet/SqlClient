---
name: validate-environment-details
description: Validate that a bug report has all required environment details and request missing information.
argument-hint: <issue number>
agent: agent
tools: ['github/search_issues']
---

Check GitHub issue #${input:issue} in `dotnet/SqlClient` for completeness of environment details.

## 1. Fetch Issue

- Retrieve the full issue: title, body, labels, template used.
- Determine if this is a bug report (uses `ISSUE_TEMPLATE/bug-report.md` template, has `:bug: Bug!` label, or contains bug-report sections).
- If not a bug report, skip environment validation and note the issue type.

## 2. Check Required Environment Fields

For bug reports, verify each of the following fields is present and meaningful (not just the template placeholder text):

### Critical Fields (must have)

| # | Field | How to Detect | Example Values |
|---|-------|---------------|----------------|
| 1 | **Microsoft.Data.SqlClient version** | Version number like `5.2.2`, `6.0.0-preview4`, NuGet package reference | `5.2.2`, `6.0.0-preview4.24320.8` |
| 2 | **.NET target framework** | Framework moniker or version | `.NET 8.0`, `.NET Framework 4.8.1`, `net462` |
| 3 | **SQL Server version** | SQL Server year, Azure SQL, or edition | `SQL Server 2022`, `Azure SQL Database` |
| 4 | **Operating system** | OS name and version | `Windows Server 2022`, `Ubuntu 24.04`, `macOS 14.7.1` |
| 5 | **Reproduction steps or code** | Code block, step-by-step instructions | Complete runnable code sample |
| 6 | **Expected vs actual behavior** | Description of what should happen vs what does happen | Clear contrast statement |

### Recommended Fields (very helpful)

| # | Field | How to Detect | Why It Helps |
|---|-------|---------------|--------------|
| 7 | **Exception message and stack trace** | Exception text, `at ...` stack frames | Pinpoints the exact failure location in code |
| 8 | **Authentication method** | Connection string keywords, auth mode mention | Different auth flows have different code paths |
| 9 | **Connection string** (sanitized) | Connection string with password removed | Reveals encryption, pooling, timeout settings |
| 10 | **Frequency / consistency** | "always", "intermittent", "only under load" | Distinguishes deterministic bugs from race conditions |
| 11 | **Worked in previous version?** | "worked in 5.1", "broke after upgrading" | Critical for regression detection |

### Version Validation

If the SqlClient version IS provided, cross-check it:
- Is it a currently supported version? Check against `release-notes/` folder.
- Is it the latest stable or preview? If not, note whether upgrading might help.
- Is the combination of SqlClient version + .NET version valid? Refer to `.github/instructions/external-resources.instructions.md` for the compatibility matrix:
  - `net462` — .NET Framework 4.6.2+
  - `net8.0` — .NET 8.0 (LTS)
  - `net9.0` — .NET 9.0 (STS)

## 3. Assess Completeness

Categorize the issue:

- **✅ Complete**: All 6 critical fields present with meaningful values → Ready for investigation
- **⚠️ Partially complete**: 4-5 critical fields present → Can start investigating, but request missing items
- **❌ Incomplete**: 3 or fewer critical fields present → Cannot investigate effectively, must request info first

## 4. Post Comment

### If fields are missing:

Post a comment structured as:

```markdown
👋 Thanks for reporting this issue!

To help us investigate, we need a few more details:

<list each missing field with a specific question>

**Quick template** — you can copy-paste and fill in:
~~~
- **SqlClient version**: 
- **.NET target**: 
- **SQL Server version**: 
- **OS**: 
- **Repro code**: (paste below)
~~~

You can edit your original issue or reply here. Once we have these details, we'll start investigating! 🔍
```

### If all fields are present:

Post a confirmation:

```markdown
✅ All required environment details are present. This issue is ready for investigation.

**Environment snapshot:**
| Detail | Value |
|--------|-------|
| SqlClient | <version> |
| .NET | <framework> |
| SQL Server | <version> |
| OS | <platform> |
```

## 5. Update Labels

- If missing critical fields → Add `Needs More Info :information_source:` label
- If complete → Ensure `Triage Needed :new:` is present and remove `Needs More Info :information_source:` if it was there
- If reporter mentioned a previous working version → Add note for regression check

## 6. Output

Return:
- **Completeness**: Complete / Partially complete / Incomplete
- **Missing fields**: List of missing items
- **Version validation**: Any version compatibility concerns
- **Regression hint**: Whether the reporter indicated this might be a regression
