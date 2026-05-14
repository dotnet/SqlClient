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

Our team is actively working on the following high-level themes:

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

## Community Contributions

We welcome and value community contributions to Microsoft.Data.SqlClient! To ensure we can maintain quality and alignment with our roadmap, please follow these guidelines:

### Submitting Pull Requests

- **New features from community PRs must be driven by creating a GitHub issue first.** Discuss the proposal in the issue before starting implementation. This helps avoid wasted effort and ensures alignment with project goals.
- **Community contributions must not derail the project roadmap.** We prioritize features and fixes according to our published milestones. PRs that conflict with or distract from active roadmap items may be deferred.
- **Our maintainers reserve the right to reject PRs** that do not meet the required criteria to qualify for review. This includes PRs that:
  - Lack a corresponding approved issue
  - Introduce breaking changes without prior discussion
  - Do not follow the project's coding standards and conventions
  - Are missing adequate test coverage
  - Conflict with work already in progress by the team
- **Bug fixes and small improvements** are generally welcome without a prior issue, but a linked issue helps us triage and prioritize your contribution.

### What Makes a Great Contribution

1. **Start with an issue** — File a [feature request](https://github.com/dotnet/SqlClient/issues/new?template=feature_request.md) or [bug report](https://github.com/dotnet/SqlClient/issues/new?template=bug-report.md) and wait for maintainer feedback
2. **Follow the conventions** — See [CONTRIBUTING.md](CONTRIBUTING.md) and [coding guidelines](policy/coding-style.md)
3. **Include tests** — Both unit tests and integration tests where applicable
4. **Keep scope focused** — One feature or fix per PR
5. **Update documentation** — For any public API changes

### Contribution Workflow

See [contributing-workflow.md](contributing-workflow.md) for details on how PRs are tracked through our review process.

---

## Feedback

The best way to give feedback is to create issues in the [dotnet/SqlClient](https://github.com/dotnet/SqlClient) repo.

Please give us feedback that will provide insight on the following:

- Existing features that are missing some capability or otherwise don't work well enough.
- Missing features that should be added to the product.
- Design choices for a feature that is currently in-progress.

Some important caveats:

- It is best to give design feedback quickly for improvements that are in development. We're unlikely to hold a feature from a release on late feedback.
- We are most likely to include improvements that either have a positive impact on a broad scenario or have very significant positive impact on a niche scenario. This means that we are unlikely to prioritize modest improvements to niche scenarios.
- Compatibility will almost always be given a higher priority than improvements.
