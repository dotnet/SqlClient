---
applyTo: "**"
---
# Regression Analysis Instructions

## Purpose

This document provides guidance for analyzing whether a reported issue is a regression — a bug introduced by a recent change that broke previously working functionality. Regression detection is one of the most valuable triage activities because regressions are typically higher priority and easier to fix when the causal change is identified.

## When to Perform Regression Analysis

Perform regression analysis when:
- The reporter explicitly mentions it worked in a previous version
- The reporter mentions upgrading before the issue appeared
- The issue involves core functionality that has historically worked
- The issue was filed shortly after a new release
- The stack trace references code areas touched by recent PRs

## Version and Release Structure

### Release Notes Location
- Release notes are stored in the `release-notes/` directory
- Each major/minor version has its own subdirectory (e.g., `release-notes/5.2/`, `release-notes/6.0/`)
- Preview releases have separate notes (e.g., `6.0-preview1.md`, `6.0-preview2.md`)
- Look for sections: **Added**, **Fixed**, **Changed**, **Breaking Changes**, **Known Issues**

### NuGet Package Versions
- Stable releases: `5.2.2`, `5.2.3`, `6.0.0`
- Preview releases: `6.0.0-preview1.24320.8`, `6.0.0-preview4.XXXXX.X`
- Package page: https://www.nuget.org/packages/Microsoft.Data.SqlClient

### Identifying Version Boundaries
When a reporter says "it broke after upgrading from X to Y":
1. Find release notes for version Y
2. Look for entries mentioning the affected feature/component
3. Search for PRs merged between the release dates of X and Y
4. Check the git diff between the two release tags

## How to Detect Regressions

### Step 1: Extract Version Information
From the issue, identify:
- **Broken version**: The version where the issue occurs
- **Working version**: The last known version where it worked (if stated)
- **Upgrade path**: Did they skip versions? (e.g., 5.1 → 6.0 skipping 5.2)

### Step 2: Review Release Notes
```
# Check release notes for the broken version
read release-notes/<version>/
```
Look for:
- Entries related to the affected component
- Breaking changes that might explain the behavior change
- Bug fixes that might have inadvertently changed behavior (fix one thing, break another)

### Step 3: Search Recent PRs
Search for PRs that modified the affected code area:
```
repo:dotnet/SqlClient is:pr is:merged <component keyword>
repo:dotnet/SqlClient is:pr is:merged path:src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/<affected file>
```

### Step 4: Git History Analysis
Check recent commits to the affected source files:
```
git log --oneline --since="<working version release date>" -- src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/<affected files>
```

Use git blame to identify when specific lines were last changed:
```
git blame src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/<file> -L <start>,<end>
```

### Step 5: Diff Between Versions
If release tags exist, compare the affected files between versions:
```
git diff <working-version-tag>..<broken-version-tag> -- src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/<affected file>
```

## Regression Classification

| Classification | Criteria | Priority Impact |
|---------------|----------|-----------------|
| **Confirmed regression** | Clear evidence: worked in version X, broke in version Y, and a specific PR/commit is identified as the cause | Typically P1 — high priority fix |
| **Likely regression** | Reporter says it worked before, affected area has recent changes, but exact cause not pinpointed | Typically P1-P2 — needs investigation to confirm |
| **Possible regression** | Issue appeared near a release, affected area has some recent changes, but no direct evidence | P2 — investigate further |
| **Not a regression** | Issue is a pre-existing bug, misconfiguration, or expected behavior change documented in release notes | Normal priority based on severity |
| **Inconclusive** | Not enough information to determine | Request version history from reporter |

## Common Regression Patterns in SqlClient

### Pattern 1: Platform-Specific Regression
A change that works on one platform breaks another:
- Fix for .NET 8 breaks .NET Framework 4.6.2
- Windows fix causes Linux failure
- Managed SNI change doesn't apply to Native SNI (or vice versa)

**How to check**: Look for conditional compilation (`#if NETFRAMEWORK`, `#if NET`, `#if _WINDOWS`, `#if _UNIX`) in the changed code. If a change was made inside a `#if NET` block, check if the parallel `#if NETFRAMEWORK` block needs the same fix.

### Pattern 2: Connection Pool Behavior Change
Pool management changes can cause subtle regressions:
- Connections not returned to pool
- Pool size limits behaving differently
- Timeout behavior changes

**How to check**: Review `SqlConnectionPool*`, `DbConnectionPool*` classes for recent modifications.

### Pattern 3: TDS Protocol Handling
Changes to TDS parsing can break specific SQL Server version interactions:
- New token handling breaks older server responses
- Pre-login sequence changes affect specific server configurations

**How to check**: Review `TdsParser`, `TdsParserStateObject`, `TdsParserSession*` for recent changes.

### Pattern 4: Authentication Flow Changes
Auth changes can break specific identity provider configurations:
- Azure AD token acquisition changes
- SSPI/Kerberos handling modifications
- Certificate validation changes

**How to check**: Review `SqlAuthenticationProvider*`, SSPI-related classes, and auth method implementations.

### Pattern 5: Encryption/TLS Changes
Security-related changes can break connectivity:
- TLS version enforcement changes
- Certificate validation behavior changes
- Encryption option default changes

**How to check**: Review SNI layer, `TdsParser` pre-login/login methods, encryption option handling.

## Reporting Regression Findings

When you identify a regression, include in your report:

1. **Regression confirmed between**: Version X → Version Y
2. **Likely causal change**: PR #NNNN or commit SHA
3. **What changed**: Brief description of the change that may have caused the regression
4. **Why it broke**: Explanation of the code path difference
5. **Affected platforms**: Which TFMs / OS / SQL Server versions are affected
6. **Suggested fix direction**: Revert, modify, or new fix
7. **Test gap**: Why existing tests didn't catch this

## Cross-Reference with Issue Templates

The bug report template (`ISSUE_TEMPLATE/bug-report.md`) asks for:
- `Microsoft.Data.SqlClient version` — map to release notes
- `.NET target` — check platform-specific code paths
- `SQL Server version` — check TDS compatibility
- `Operating system` — check OS-specific code paths

If the reporter provides a version but not whether it's a regression, proactively ask:
> "Did this work in a previous version of Microsoft.Data.SqlClient? If so, which version was the last known working one? This helps us determine if this is a regression."
