---
name: triage-issue
description: Triage a new GitHub issue — categorize, label, request info, and link related items.
argument-hint: <issue number>
agent: agent
tools: ['github/search_issues', 'read/readFile', 'codebase/search']
---

Triage GitHub issue #${input:issue} in `dotnet/SqlClient`.

**GitHub Project**: https://github.com/orgs/dotnet/projects/588/ — All issues are tracked here. After triaging, update the issue's project fields.

Follow this workflow step-by-step:

## 1. Fetch and Understand the Issue
- Retrieve the full issue details (title, body, labels, author, comments).
- Determine which issue template was used:
  - `ISSUE_TEMPLATE/bug-report.md` → Bug
  - `ISSUE_TEMPLATE/feature_request.md` → Feature
  - No template / sub-issue → Task
  - High-level work with sub-issues → Epic

## 2. Set Issue Type
Update the issue type if not already set:
- **Bug** — Unexpected behavior, crash, regression
- **Feature** — New capability or enhancement proposal
- **Task** — Sub-issue or discrete work item
- **Epic** — High-level work linking multiple sub-issues

## 3. Evaluate Completeness
Check whether the issue provides enough information to act on:

**For Bugs**, verify:
- [ ] .NET version and target framework
- [ ] Microsoft.Data.SqlClient version
- [ ] SQL Server version (if relevant)
- [ ] OS and platform
- [ ] Repro steps or minimal repro code
- [ ] Expected vs actual behavior
- [ ] Stack trace or error message

**For Features**, verify:
- [ ] Clear problem statement / motivation
- [ ] Proposed solution or API shape
- [ ] Use case / scenario description

If any required details are missing, add the label `Needs more info :information_source:` and post a comment requesting the specific missing items.

## 4. Apply Labels
Select appropriate labels based on the issue content:

**Area labels** (pick the most relevant):
- `Area\Engineering` — Build system, CI/CD, project infrastructure
- `Area\Connection Pooling` — Pool behavior, timeouts, pool size
- `Area\AKV Provider` — Always Encrypted Azure Key Vault provider
- `Area\Json` — JSON data type support
- `Area\SqlClient` — General driver behavior
- `Area\SNI` — Network layer (managed or native)
- `Area\TDS` — Protocol-level issues

**Status labels**:
- `Triage Needed :new:` — For new issues needing initial review
- `Needs more info :information_source:` — Missing required details
- `Performance :chart_with_upwards_trend:` — Performance-related concern

## 5. Update GitHub Project Fields
Ensure the issue is added to the GitHub Project (https://github.com/orgs/dotnet/projects/588/) and set the following fields to the most appropriate values:

| Field | Values | Guidance |
|-------|--------|----------|
| **Status** | `To Triage`, `Needs Response`, `Investigating`, `Waiting for customer`, `Backlog`, `In progress`, `In review`, `Done` | Set to `Investigating` if actionable, `Needs Response` if info is missing from reporter, `Waiting for customer` if awaiting external input, `Backlog` for valid lower-priority items |
| **Priority** | `P0`, `P1`, `P2`, `P3` | **P0**: Critical — data loss, security vulnerability, widespread breakage. **P1**: High — significant regression, blocking customer scenario. **P2**: Medium — important but has workaround, affects subset of users. **P3**: Low — minor issue, enhancement, nice-to-have |
| **Size** | `XS`, `S`, `M`, `L`, `XL` | Estimate effort: **XS**: trivial fix (<1h). **S**: small, well-scoped (<1 day). **M**: moderate, may touch multiple files (1-3 days). **L**: significant, cross-cutting (1-2 weeks). **XL**: large feature or architectural change (2+ weeks) |
| **Comment** | Free text | Add a brief note explaining priority/size rationale or any context for the team |

### Field Assignment Examples
- **Bug with repro + clear root cause** → Status: `Investigating`, Priority: `P2`, Size: `S`
- **Bug with missing info** → Status: `Needs Response`, Priority: unset until understood
- **Feature request, well-specified** → Status: `Backlog`, Priority: `P3`, Size: `M`
- **Security vulnerability** → Status: `Investigating`, Priority: `P0`, Size: varies
- **Performance regression** → Status: `Investigating`, Priority: `P1`, Size: `M`

## 6. Search for Related Issues
- Search for existing issues with similar keywords: `repo:dotnet/SqlClient <key terms>`
- If duplicates or related issues exist:
  - Link them in a comment
  - If it's an exact duplicate, label accordingly and reference the original
- Check if there are related PRs (open or merged) that address the same area.

## 7. Identify Affected Code Area
- Based on the issue description, identify the likely source file(s) in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`.
- Note the component (e.g., SqlConnection, TdsParser, ConnectionPool, SqlCommand).
- Check if the issue is platform-specific (Windows-only, Unix-only, .NET Framework-only).

## 8. Post Triage Comment
Add a comment that includes:
- Confirmation that the issue has been triaged
- Issue type classification
- Which area/component is likely affected
- Any questions or requests for missing information
- Links to related issues, documentation, or code references
- GitHub Project fields that were set (Status, Priority, Size)
- `@` mentions for relevant team members if the issue is urgent or high-impact

## 9. Summary
Output a brief triage summary:
- **Type**: Bug / Feature / Task / Epic
- **Area**: Which component(s) affected
- **Labels applied**: List of labels
- **Project fields**: Status, Priority, Size values set
- **Missing info**: What additional info is needed (if any)
- **Related issues**: Links to related issues or PRs
- **Severity assessment**: Low / Medium / High / Critical
