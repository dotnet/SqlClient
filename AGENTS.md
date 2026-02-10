# AI Agent Guidelines for Microsoft.Data.SqlClient

This document provides guidance for AI coding agents working with the Microsoft.Data.SqlClient repository.

## Quick Start

### Essential Context Files
Before making changes, agents should be aware of:

| File | Purpose |
|------|---------|
| [README.md](README.md) | Project overview |
| [BUILDGUIDE.md](BUILDGUIDE.md) | Build and test instructions |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contribution guidelines |
| [.github/copilot-instructions.md](.github/copilot-instructions.md) | Copilot-specific instructions |

### Detailed Technical Instructions
The `.github/instructions/` directory contains comprehensive guides:

| Guide | Coverage |
|-------|----------|
| [architecture.instructions.md](.github/instructions/architecture.instructions.md) | Project structure, unified project model |
| [tds-protocol.instructions.md](.github/instructions/tds-protocol.instructions.md) | TDS protocol, packet handling |
| [ado-pipelines.instructions.md](.github/instructions/ado-pipelines.instructions.md) | Azure DevOps CI/CD pipelines |
| [testing.instructions.md](.github/instructions/testing.instructions.md) | Test framework, running tests |
| [connection-pooling.instructions.md](.github/instructions/connection-pooling.instructions.md) | Connection pool internals |
| [api-design.instructions.md](.github/instructions/api-design.instructions.md) | Public API design principles |
| [features.instructions.md](.github/instructions/features.instructions.md) | Feature reference, keywords |
| [documentation.instructions.md](.github/instructions/documentation.instructions.md) | Documentation and samples |
| [external-resources.instructions.md](.github/instructions/external-resources.instructions.md) | MCP tools, docs links, version matrix |

## Workflow Prompts

This repository provides reusable prompts in `.github/prompts/` for common maintainer workflows. Use these to guide agents through multi-step operations.

| Prompt | Purpose |
|--------|---------|
| [fix-bug.prompt.md](.github/prompts/fix-bug.prompt.md) | Diagnose and fix a bug with tests and documentation |
| [implement-feature.prompt.md](.github/prompts/implement-feature.prompt.md) | Plan and implement a new feature end-to-end |
| [triage-issue.prompt.md](.github/prompts/triage-issue.prompt.md) | Triage a new GitHub issue with labeling and categorization |
| [code-review.prompt.md](.github/prompts/code-review.prompt.md) | AI-assisted code review for a pull request |
| [perf-optimization.prompt.md](.github/prompts/perf-optimization.prompt.md) | Investigate and implement performance improvements |
| [release-notes.prompt.md](.github/prompts/release-notes.prompt.md) | Generate release notes for a specific milestone |
| [update-build-pipelines.prompt.md](.github/prompts/update-build-pipelines.prompt.md) | Modify Azure DevOps CI/CD pipeline configuration |

## Core Principles

1. **Cross-Platform Compatibility**: Code must work on .NET Framework 4.6.2+ and .NET 8.0+
2. **Backward Compatibility**: No breaking changes without proper deprecation
3. **Test-First Development**: All changes require tests
4. **Security by Default**: Secure defaults, no credential logging
5. **Protocol Compliance**: Follow MS-TDS specifications
6. **Performance Optimization**: Use pooling, async, efficient allocations
7. **Observability**: EventSource tracing, meaningful errors

## Common Tasks

### Bug Fix Workflow
1. Understand the issue from the bug report
2. Locate relevant code in `src/Microsoft.Data.SqlClient/src/` (do NOT modify legacy `netcore/src/` or `netfx/src/`)
3. Write a failing test that reproduces the issue
4. Implement the fix
5. Ensure all tests pass
6. Update documentation if behavior changes

### Feature Implementation
1. Review the feature specification
2. Plan the implementation (see `implement-feature` prompt)
3. Update reference assemblies if adding public APIs
4. Implement with tests
5. Add samples to `doc/samples/`
6. Update CHANGELOG.md

### Adding Connection String Keywords
1. Add to `SqlConnectionStringBuilder`
2. Update connection string parser
3. Default to backward-compatible value
4. Add tests for new keyword
5. Document in feature reference

### Protocol Changes
1. Reference MS-TDS specification
2. Update `TdsEnums.cs` for new constants
3. Implement in `TdsParser.cs` and related files
4. Test against multiple SQL Server versions
5. Consider backward compatibility

### Performance Optimization
1. Profile the issue using benchmarks or traces
2. Identify allocation hotspots (see `perf-optimization` prompt)
3. Apply patterns: `ArrayPool<T>`, `Span<T>`, static/cached instances, source generation
4. Verify no regressions with existing tests

## External Resources

### MCP Tool Integration
MCP servers are configured in [.vscode/mcp.json](.vscode/mcp.json) for shared team use:

| Server | Purpose |
|--------|---------|
| **github** | GitHub repository operations — issues, PRs, code search |
| **ado** | Azure DevOps — pipelines, work items, builds (org: `sqlclientdrivers`) |
| **bluebird-sqlclient** | Engineering Copilot for the SqlClient ADO repo — code discovery, architecture search |
| **bluebird-sni** | Engineering Copilot for the SNI repo — native SNI code search |

Additional MCP tools provided by VS Code extensions (no mcp.json entry needed):
- **Microsoft Learn Docs** — search and fetch official Microsoft documentation (from GitHub Copilot for Azure extension)

### Key Documentation Links
- [Microsoft.Data.SqlClient on Microsoft Learn](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [MS-TDS Protocol Specification](https://learn.microsoft.com/openspecs/windows_protocols/ms-tds)
- [SQL Server Documentation](https://learn.microsoft.com/sql/sql-server/)

## Repository Policies

See the `policy/` directory for:
- [coding-best-practices.md](policy/coding-best-practices.md) - Programming standards
- [coding-style.md](policy/coding-style.md) - Code formatting guidelines
- [review-process.md](policy/review-process.md) - PR review requirements

## Getting Help

- Check existing tests for usage patterns
- Reference similar implementations in the codebase
- Consult the Microsoft Docs for API behavior specifications
- For protocol questions, refer to MS-TDS open specifications

---

*This document is automatically loaded as context for AI agents working in this repository.*
