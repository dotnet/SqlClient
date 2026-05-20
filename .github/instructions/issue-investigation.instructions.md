---
applyTo: "**"
---
# Issue Investigation Instructions

## Purpose

This document provides Copilot with SqlClient-specific knowledge for diagnosing reported issues. It is the reference companion to the `investigate-issue` prompt and is also consulted whenever Copilot is working in an issue-investigation context (e.g., when assigned a bug issue by a maintainer).

It sits between triage and fix:

- **Triage** (`triage-issue.prompt.md` and the `issue-triage.md` agentic workflow) — classifies the issue, validates required environment fields, applies area labels, and posts a triage summary.
- **Investigation** (this file + `investigate-issue.prompt.md`) — diagnoses *what is broken and where*, producing a hypothesis with code references.
- **Fix** (`fix-bug.prompt.md`) — reproduces, writes a failing test, implements the fix, and prepares a PR.

This file focuses on the middle step. It does **not** restate project structure, build conventions, or platform-targeting rules — those live in `architecture.instructions.md`. Refer to that file for the canonical project layout and conditional-compilation patterns.

## Environment Detail Requirements

### Why Environment Details Matter

Microsoft.Data.SqlClient has significantly different code paths depending on the reporter's environment. Without these details, investigation often goes down the wrong code path.

| Dimension | Impact |
|-----------|--------|
| **.NET version** | `net462` vs `net8.0` vs `net9.0` — different compilation targets, different runtime behaviors, different APIs available. |
| **OS** | Windows vs Linux vs macOS — different SNI implementations (Native vs Managed), different auth providers (SSPI vs GSSAPI), different cert stores. |
| **SQL Server version** | TDS protocol version differences, feature availability (e.g., JSON type requires SQL Server 2025+, Always Encrypted requires 2016+). |
| **SqlClient version** | Feature availability, bug fixes, behavior changes across versions. |
| **Authentication method** | Completely different code paths for SQL Auth vs Windows Auth vs Azure AD variants. |
| **Encryption mode** | Optional vs Mandatory vs Strict — different TLS handshake flows, different SNI behavior. |

### Minimum Required Details for Bug Investigation

Without these, meaningful investigation is not possible:
1. Microsoft.Data.SqlClient NuGet package version
2. .NET target framework
3. SQL Server version or Azure SQL service tier
4. Operating system and version
5. Reproduction steps or minimal code sample
6. Expected vs actual behavior

The `issue-triage.md` agentic workflow already checks for these on every new issue. When invoking `investigate-issue`, treat any flagged gaps from triage as inputs — note them in the investigation comment rather than re-asking the reporter.

### Details That Accelerate Investigation

These are not strictly required, but if present they should be used:
- Full exception message and stack trace
- Connection string (with password/secrets redacted)
- Authentication method being used
- Whether the issue is intermittent or consistent
- Whether it worked in a previous SqlClient version (regression indicator)
- Network topology (direct connection, VPN, load balancer, proxy)

## Code Investigation Guide

All active source code lives in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/` (see `architecture.instructions.md` for the unified project layout). The tables below group files by **diagnostic area** — i.e., "I see this symptom, here is where to look" — which is the orthogonal view to the implementer-focused layout in `architecture.instructions.md`.

### Connection Management
| File | Responsibility |
|------|----------------|
| `SqlConnection.cs` | Public connection API, open/close lifecycle |
| `SqlInternalConnection.cs` | Internal connection state management |
| `SqlInternalConnectionTds.cs` | TDS-specific connection implementation |
| `SqlConnectionString.cs` | Connection string parsing and validation |

### Connection Pooling
| File | Responsibility |
|------|----------------|
| `SqlConnectionPool*.cs` | SqlClient-specific pool logic |
| `DbConnectionPool*.cs` | Base pool implementation |

For deeper context on pool behavior and edge cases, see `connection-pooling.instructions.md`.

### Command Execution
| File | Responsibility |
|------|----------------|
| `SqlCommand.cs` | Public command API |
| `SqlDataReader.cs` | Result set reading |
| `SqlBulkCopy.cs` | Bulk insert operations |

### TDS Protocol
| File | Responsibility |
|------|----------------|
| `TdsParser.cs` | Core TDS protocol parser |
| `TdsParserStateObject.cs` | TDS session state management |
| `TdsParserSession*.cs` | TDS session handling |
| `TdsEnums.cs` | TDS protocol constants and enums |

For deeper context on TDS semantics, see `tds-protocol.instructions.md`.

### Authentication
| File | Responsibility |
|------|----------------|
| `SqlAuthenticationProvider*.cs` | Authentication provider framework |
| `ActiveDirectory*.cs` | Azure AD authentication implementations |
| SSPI-related files | Windows Integrated / Kerberos auth |

### Encryption
| File | Responsibility |
|------|----------------|
| SNI layer files | TLS handshake, network encryption |
| `SqlConnectionEncryptOption.cs` | Encryption mode configuration |
| `SqlColumnEncryption*.cs` | Always Encrypted column encryption |

## Stack Trace to Source Mapping

When analyzing stack traces from issue reports:

1. **Extract the top Microsoft.Data.SqlClient frames** — ignore runtime/BCL frames at the top.
2. **Map method names to source files** — use the tables above as a first pass; fall back to codebase search by method name.
3. **Check the execution context**:
   - Sync vs async path (methods ending in `Async` or containing `Task`)
   - Which TFM was compiled for (sometimes visible in the stack)
   - Was the method called from a pool callback, timer, or user thread?
4. **Look for try/catch boundaries** — where is the exception thrown vs where is it caught and re-thrown?
5. **Cross-check platform variants** — if the reporter is on Linux but the top frames are in `*.windows.cs` files, you are reading the wrong variant.

## Integration with Existing Copilot Assets

This file is referenced by, and complements, several existing assets in `.github/`:

- **`prompts/triage-issue.prompt.md`** — runs first. Investigation begins after triage has classified the issue and confirmed it is actionable.
- **`prompts/investigate-issue.prompt.md`** — the primary consumer of this file. Provides the step-by-step investigation flow.
- **`prompts/fix-bug.prompt.md`** — runs after investigation. The hypothesis and affected-file list produced by investigation become the starting point for the fix.
- **`workflows/issue-triage.md`** — the agentic workflow that auto-runs on new issues and produces the triage summary that points subsequent investigators here.
- **`instructions/architecture.instructions.md`** — canonical source for project structure, platform targeting, and conditional-compilation rules. Always consult before assuming a file path.
- **`instructions/connection-pooling.instructions.md`** and **`instructions/tds-protocol.instructions.md`** — deeper implementer-focused context for the corresponding diagnostic areas above.
