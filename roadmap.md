# Microsoft.Data.SqlClient Roadmap

The Microsoft.Data.SqlClient roadmap communicates project priorities for evolving and extending the scope of the product. We encourage the community to work with us to improve the SqlClient driver for these scenarios and extend it for others.

> **Last updated:** May 2026
>
> This roadmap is a living document. Priorities and timelines may shift based on community feedback, engineering constraints, and business needs. We update this page regularly to reflect the current state of development.

---

## Release Milestones

For active release milestones, their target dates, and the changes included, see [SqlClient milestones](https://github.com/dotnet/SqlClient/milestones).

---

## Current Focus Areas

Our team is actively working on the following high-level themes. Features are tracked via [GitHub issues](https://github.com/dotnet/SqlClient/issues) where applicable — see the linked milestones for associated issue details.

- **Active** — Currently in development
- **Planned** — Committed for a future milestone with estimated delivery
- **Backlog** — On our radar for future months, not yet scheduled

### Performance & Reliability

| Feature | Status | ETA |
|---------|--------|-----|
| Connection pool performance improvements | Active | June 2026 |
| Performance benchmarking suite | Active | June 2026 |
| Phase 1 - Unix async performance — thread starvation in parallel `ExecuteReaderAsync` | Active | September 2026 |
| Phase 2 - Async usage analysis and optimization | Planned | — |

### New Data Type Support

| Feature | Status | ETA |
|---------|--------|-----|
| Vector subtype support — `float16` (`Half`) | Active | August 2026 |

### Observability & Diagnostics

| Feature | Status | ETA |
|---------|--------|-----|
| OpenTelemetry support | Planned | — |
| Logging improvements | Planned | — |
| Integrate with / expose MSAL logging | Planned | — |

### API Improvements

| Feature | Status | ETA |
|---------|--------|-----|
| Expose connection encryption information to clients | Planned | September 2026 |
| Throw `TaskCanceledException` instead of `SqlException` for cancellations | Planned | September 2026 |
| `BeginTransactionAsync` API on `SqlConnection` | Planned | — |

### Security & Architecture

| Feature | Status | ETA |
|---------|--------|-----|
| Security hardening activities | Active | Ongoing internally |

### AI & Developer Tooling

| Feature | Status | ETA |
|---------|--------|-----|
| `System.Data.SqlClient` → `Microsoft.Data.SqlClient` migration via Modernize with Copilot | Active | September 2026 |
| Modernize SqlClient repository with AI | Active | Ongoing |

### Engineering & Infrastructure

| Feature | Status | ETA |
|---------|--------|-----|
| CI/CD pipeline redesign | Active | August 2026 |
| Add SQL Server 2025 to test matrix | Planned | August 2026 |
| Add .NET 10 to test matrix | Planned | August 2026 |
| Converting existing traditional pipelines to Yaml | Active | August 2026 |
| Performance benchmarking pipeline (Internal) | Planned | September 2026 |
---

## Released Versions

- [Release Notes](release-notes/README.md) — Detailed release notes summarizing all changes and features released.
- [GitHub Releases](https://github.com/dotnet/sqlclient/releases) — NuGet packages and changelog notes for each release.

---

## Community Contributions & Feedback

For information on how to contribute, see [CONTRIBUTING.md](CONTRIBUTING.md). For details on the PR tracking workflow, see [contributing-workflow.md](contributing-workflow.md).

The best way to give feedback is to create issues in the [dotnet/SqlClient](https://github.com/dotnet/SqlClient) repo.
