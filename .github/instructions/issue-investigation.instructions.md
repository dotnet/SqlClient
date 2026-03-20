---
applyTo: "**"
---
# Issue Investigation Instructions

## Purpose

This document provides Copilot with the knowledge and process to perform automated initial investigation of new issues. It complements the existing triage process by adding deeper technical analysis before a human engineer reviews the issue.

## Investigation Workflow

When a new issue arrives, the fully automated pipeline runs end-to-end:

1. **Environment Validation** → Check that all required details are provided (GitHub Actions regex check + Copilot `validate-environment-details` prompt)
2. **Triage Classification** → Auto-label by area, check for duplicates (GitHub Actions)
3. **Deep Investigation** → Regression check, code analysis, initial findings (Copilot agent runs `investigate-issue` prompt automatically)

All three stages trigger automatically when an issue is opened — no human intervention required. The workflow `issue-auto-triage.yml` orchestrates the full pipeline: Jobs 1-3 (env check, labeling, duplicate search) run in parallel, then Job 4 (Copilot investigation) runs after they complete.

## Environment Detail Requirements

### Why Environment Details Matter

Microsoft.Data.SqlClient has significantly different code paths depending on:

| Dimension | Impact |
|-----------|--------|
| **.NET version** | `net462` vs `net8.0` vs `net9.0` — different compilation targets, different runtime behaviors, different APIs available |
| **OS** | Windows vs Linux vs macOS — different SNI implementations (Native vs Managed), different auth providers (SSPI vs GSSAPI), different cert stores |
| **SQL Server version** | TDS protocol version differences, feature availability (JSON type requires 2025+, Always Encrypted requires 2016+) |
| **SqlClient version** | Feature availability, bug fixes, behavior changes across versions |
| **Authentication method** | Completely different code paths for SQL Auth vs Windows Auth vs Azure AD variants |
| **Encryption mode** | Optional vs Mandatory vs Strict — different TLS handshake flows, different SNI behavior |

### Minimum Required Details for Bug Reports

Without these, meaningful investigation is not possible:
1. Microsoft.Data.SqlClient NuGet package version
2. .NET target framework
3. SQL Server version or Azure SQL service tier
4. Operating system and version
5. Reproduction steps or minimal code sample
6. Expected vs actual behavior

### Helpful Additional Details

These accelerate investigation significantly:
- Full exception message and stack trace
- Connection string (with password/secrets redacted)
- Authentication method being used
- Whether the issue is intermittent or consistent
- Whether it worked in a previous SqlClient version (regression indicator)
- Network topology (direct connection, VPN, load balancer, proxy)

## Code Investigation Guide

### Source Code Layout

All active source code is in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`. Key files by component:

#### Connection Management
| File | Responsibility |
|------|---------------|
| `SqlConnection.cs` | Public connection API, open/close lifecycle |
| `SqlInternalConnection.cs` | Internal connection state management |
| `SqlInternalConnectionTds.cs` | TDS-specific connection implementation |
| `SqlConnectionString.cs` | Connection string parsing and validation |

#### Connection Pooling
| File | Responsibility |
|------|---------------|
| `SqlConnectionPool*.cs` | SqlClient-specific pool logic |
| `DbConnectionPool*.cs` | Base pool implementation |

#### Command Execution
| File | Responsibility |
|------|---------------|
| `SqlCommand.cs` | Public command API |
| `SqlDataReader.cs` | Result set reading |
| `SqlBulkCopy.cs` | Bulk insert operations |

#### TDS Protocol
| File | Responsibility |
|------|---------------|
| `TdsParser.cs` | Core TDS protocol parser |
| `TdsParserStateObject.cs` | TDS session state management |
| `TdsParserSessio*.cs` | TDS session handling |
| `TdsEnums.cs` | TDS protocol constants and enums |

#### Authentication
| File | Responsibility |
|------|---------------|
| `SqlAuthenticationProvider*.cs` | Authentication provider framework |
| `ActiveDirectory*.cs` | Azure AD authentication implementations |
| SSPI-related files | Windows Integrated / Kerberos auth |

#### Encryption
| File | Responsibility |
|------|---------------|
| SNI layer files | TLS handshake, network encryption |
| `SqlConnectionEncryptOption.cs` | Encryption mode configuration |
| `SqlColumnEncryption*.cs` | Always Encrypted column encryption |

### Platform-Specific Code Patterns

When investigating, always check for platform variants:
- `<FileName>.netfx.cs` — .NET Framework specific
- `<FileName>.netcore.cs` — .NET Core / .NET 5+ specific
- `<FileName>.windows.cs` — Windows specific
- `<FileName>.unix.cs` — Linux/macOS specific

Conditional compilation directives:
- `#if NETFRAMEWORK` — .NET Framework 4.6.2
- `#if NET` — .NET 8.0+
- `#if NET8_0_OR_GREATER` — .NET 8.0 and above
- `#if _WINDOWS` — Windows OS
- `#if _UNIX` — Linux/macOS

### Stack Trace to Source Mapping

When analyzing stack traces from issue reports:

1. **Extract the top Microsoft.Data.SqlClient frames** — ignore runtime/BCL frames
2. **Map method names to source files** — Use codebase search for the method name
3. **Check the execution context**:
   - Sync vs async path (methods ending in `Async` or containing `Task`)
   - Which TFM was it compiled for (sometimes visible in the stack)
   - Was it called from a pool callback, timer, or user thread?
4. **Look for try/catch boundaries** — Where is the exception thrown vs where is it caught and re-thrown?

## Integration with Existing Prompts

This investigation process integrates with:

- **`triage-issue` prompt**: Runs first to classify and label. Investigation adds deeper technical analysis.
- **`fix-bug` prompt**: Investigation findings feed directly into bug fixing. The hypothesis and affected files become the starting point.
- **`code-review` prompt**: If a fix PR is submitted, the investigation context helps reviewers understand the change.

## PR and Commit Conventions

When creating a PR from an investigation:

- **PR title**: Use the format `Fix <short description> (Fixes #<issue_number>)` where `<issue_number>` is the actual GitHub issue number being investigated (e.g., `Fix MARS deadlock under concurrent load (Fixes #3)`)
- **Do NOT** use placeholder issue numbers like `ISSUE-123` — always reference the real issue number from the trigger
- **Branch name**: Use `copilot/fix-<short-kebab-description>`
- **PR body**: Include a summary of investigation findings, root cause analysis, and what the fix changes

## Automated Workflow Trigger

The GitHub Actions workflow `issue-auto-triage.yml` runs on every new issue with 4 jobs:

1. **validate-environment-details** (parallel) — Regex-based check for required fields, posts summary comment with table
2. **initial-area-labeling** (parallel) — Keyword-based area label assignment, posts area classification comment
3. **duplicate-check** (parallel) — Searches for similar existing issues, posts results table
4. **copilot-investigate** (after 1-3 complete) — Posts triage summary and prompts maintainer to assign Copilot for deep investigation

After a maintainer assigns Copilot, the Coding Agent:
- Runs the `investigate-issue` prompt
- Performs regression analysis and code investigation
- Opens a draft PR with fixes and findings linked to the original issue

The prompts (`investigate-issue`, `validate-environment-details`, `triage-issue`) can also be invoked manually via `@copilot /prompt-name <issue number>` for re-investigation or follow-up analysis.
