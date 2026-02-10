---
name: publish-release
description: End-to-end workflow for publishing a new release of Microsoft.Data.SqlClient.
argument-hint: <version, e.g. 7.0.0-preview1>
agent: agent
tools: ['github/search_issues', 'edit/createFile', 'edit/editFiles', 'read/readFile', 'codebase/search']
---

Prepare and publish release "${input:version}" of Microsoft.Data.SqlClient.

Follow this workflow step-by-step:

## 1. Gather Merged PRs for the Milestone
- Search for all **merged** Pull Requests associated with the milestone "${input:version}" in `dotnet/SqlClient`.
- Also look into recent PRs merged to the release or main branch (respective to the release) that may not be linked to the milestone but should be included in the release notes.
- For PRs that port changes from another PR, fetch the original PR for full context.
- Categorize PRs into: **Added**, **Fixed**, **Changed**, **Removed**.
- Ignore PRs labelled `Area\Engineering` from user-facing release notes (include them in changelog only if significant).
- Identify external contributors for the Contributors section.

## 2. Create the Release Notes File
- Determine the path: `release-notes/<Major.Minor>/${input:version}.md`.
- Use the template at `release-notes/template/release-notes-template.md`.
- Fill in every section following the template instructions:
  - **Changed**: Breaking changes with migration guidance, other changes, dependency updates.
  - **Fixed**: Bugs fixed with PR links.
  - **Added**: New features with descriptions, who benefits, impact, and code examples.
  - **Contributors**: List only public/external contributors (not core team or bots).
  - **Target Platform Support**: List supported target frameworks and their OS support.
  - **Dependencies**: Pull from `tools/specs/Microsoft.Data.SqlClient.nuspec` for each target framework.

## 3. Update CHANGELOG.md
- Add a new entry at the **top** of the list (under any existing header note).
- Include all items from Added, Fixed, Changed, Removed sections with PR links.
- Follow the existing format in CHANGELOG.md.

## 4. Update Release Directory README
- Update `release-notes/<Major.Minor>/README.md`.
- Add a new row to the table: `| <Date> | ${input:version} | [Release Notes](${input:version}.md) |`.

## 5. Version Bumps
- Update version numbers in:
  - `tools/props/Versions.props` (or equivalent version properties file)
  - NuSpec files under `tools/specs/` if needed
  - `Directory.Packages.props` if package versions changed

## 6. Final Checklist
- [ ] Release notes file created at correct path
- [ ] CHANGELOG.md updated with all changes
- [ ] Release directory README updated
- [ ] Version numbers bumped
- [ ] Dependency versions accurate (cross-check with nuspec)
- [ ] Contributors section includes only external contributors
- [ ] No sensitive information in release notes
