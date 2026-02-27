---
name: release-notes
description: Generate release notes for a specific milestone, covering all packages in the repository that have changes.
argument-hint: <milestone>
agent: agent
tools: ['edit/createFile', 'edit/editFiles', 'read/readFile']
---

Generate release notes for the milestone "${input:milestone}".

This repository ships multiple packages. Only generate release notes for packages that have relevant PRs in the milestone. All packages use the same template: [release-notes/template/release-notes-template.md](release-notes/template/release-notes-template.md).

| Package | Release Notes Directory | How to Identify PRs |
|---------|------------------------|---------------------|
| `Microsoft.Data.SqlClient` | `release-notes/<Major.Minor>/` | Default — PRs not assigned to another package |
| `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` | `release-notes/add-ons/AzureKeyVaultProvider/<Major.Minor>/` | Labels containing `AKV`, or PR titles/bodies/files referencing `AzureKeyVaultProvider`, `add-ons/`, or `AlwaysEncrypted.AzureKeyVaultProvider` |
| `Microsoft.SqlServer.Server` | `release-notes/MSqlServerServer/<Major.Minor>/` | PR titles/bodies/files referencing `Microsoft.SqlServer.Server` or `src/Microsoft.SqlServer.Server/` |
| `Microsoft.Data.SqlClient.Extensions.Abstractions` | `release-notes/Extensions/Abstractions/<Major.Minor>/` | PR titles/bodies/files referencing `Extensions.Abstractions` |
| `Microsoft.Data.SqlClient.Extensions.Azure` | `release-notes/Extensions/Azure/<Major.Minor>/` | PR titles/bodies/files referencing `Extensions.Azure` |
| `Microsoft.Data.SqlClient.Extensions.Logging` | `release-notes/Extensions/Logging/<Major.Minor>/` | PR titles/bodies/files referencing `Extensions.Logging` |

## Skills

This prompt uses the following skill:
- [fetch-milestone-prs](.github/skills/fetch-milestone-prs/SKILL.md) — Fetches all merged PR metadata for the milestone

## Steps

### 1. Fetch Milestone Items

- Follow the instructions in the [fetch-milestone-prs](.github/skills/fetch-milestone-prs/SKILL.md) skill to fetch all merged PRs for the milestone "${input:milestone}".
- The output will be saved to `.milestone-prs/${input:milestone}/` with individual JSON files per PR and an `_index.json` summary.

### 2. Analyze and Categorize

- Read the `_index.json` file to get an overview of all PRs. Read individual PR JSON files for full details (title, body, labels).
- For PRs that are porting other PRs to the current branch, read the original PR's JSON file or look up the original PR number mentioned in the body for more context.
- Categorize PRs into: `Added`, `Fixed`, `Changed`, `Removed`.
- Ignore PRs labelled `Area\Engineering` (use the `has_engineering_label` field in the JSON).
- Identify the contributors for the "Contributors" section.
- **Assign each PR to one or more packages** using the identification rules in the table above. A PR may be relevant to multiple packages. PRs not matching any non-core package belong to `Microsoft.Data.SqlClient`.

### 3. Generate Release Notes for Each Package

For each package that has relevant PRs in the milestone:

1. **Determine the package version** from the project files or NuGet specs (check `tools/specs/`, `src/Microsoft.Data.SqlClient.Extensions/`, or `src/Microsoft.SqlServer.Server/` as appropriate).

2. **Create the release notes file** at the path shown in the table above: `<Directory>/<Version>.md`.
   - Use the template from [release-notes/template/release-notes-template.md](release-notes/template/release-notes-template.md).
   - Fill in the template following the instructions in each section.
   - Only include sections (Added, Changed, Fixed, Removed) that have entries.
   - Look up dependencies from the package's own project file or NuGet spec under `tools/specs/`.
   - Omit the Contributors section for packages with no public contributors.

3. **Create or update the version README** at `<Directory>/README.md`. Follow the existing format — see [release-notes/add-ons/AzureKeyVaultProvider/6.1/README.md](release-notes/add-ons/AzureKeyVaultProvider/6.1/README.md) for reference:

   ```markdown
   # <Full Package Name> <Major.Minor> Releases

   The following `<Full Package Name>`
   <Major.Minor> releases have been shipped:

   | Release Date | Description | Notes |
   | :-- | :-- | :--: |
   | <Date> | <Version> | [Release Notes](<Version>.md) |
   ```

4. **Skip packages without changes.** If a package has no relevant PRs in the milestone, do not create release notes for it. Report which packages had changes and which did not.

### 4. Update CHANGELOG.md

- Add a new entry at the top of the list (under the Note) in [CHANGELOG.md](CHANGELOG.md).
- Include all the text from the Added, Fixed, Changed, Removed, etc. sections from the core `Microsoft.Data.SqlClient` release notes.
- If other packages also changed, include a brief summary line for each (e.g., "Released Microsoft.Data.SqlClient.Extensions.Azure 1.0.0-preview1"). The detailed notes live in the per-package release notes files.

### 5. Update Top-Level Release Notes README

- Update [release-notes/README.md](release-notes/README.md):
  - Add the new release to the appropriate package section.
  - If a section for the package doesn't yet exist, add one following the existing pattern (see the `AzureKeyVaultProvider` and `Microsoft.SqlServer.Server` sections for reference).
  - If the section already exists, add the new version link to its Release Information list.

## Notes

- Packages may ship as preview or stable independently. Use the actual version from the project/spec files.
- The directory structure mirrors existing conventions: `add-ons/AzureKeyVaultProvider/` for AKV, `MSqlServerServer/` for SqlServer, and `Extensions/<PackageName>/` for the new extension packages.
