---
name: release-notes
description: Generate release notes for a specific milestone, covering all packages in the repository that have changes.
argument-hint: <milestone> <branch>
agent: agent
tools: ['edit/createFile', 'edit/editFiles', 'read/readFile', 'execute/runInTerminal']
---

Generate release notes for the milestone "${input:milestone}" on the branch "${input:branch}".

This repository ships multiple packages. Only generate release notes for packages that have relevant PRs in the milestone. All packages use the same template: [release-notes/template/release-notes-template.md](release-notes/template/release-notes-template.md).

## Branch Model

The release notes content and the source code it describes live on different branches:

- **Release notes files are maintained on `main`.** Every release's notes (for all branches/versions) are committed under `release-notes/` on `main`. Create and edit the release notes Markdown files on `main` (or a PR targeting `main`), not on the release branch.
- **The source code for the release lives only on the target branch `${input:branch}`.** Version sources (`Versions.props`), project files (`*.csproj`), and dependency files (`Directory.Packages.props`) reflect the released bits *as they exist on `${input:branch}`*, which can differ from `main`. When you look up versions, dependencies, TFM/OS scope, or verify API names (Steps 2.1, 2.2, 4, and the Version and Dependency Lookup table), read those source files from `${input:branch}` — not from your current `main` checkout.
- **Practical implication:** Do not assume a `...VersionDefault` or dependency version read from `main` matches what shipped on `${input:branch}`. Confirm against `${input:branch}` (e.g., `git show ${input:branch}:<path>`), or against the milestone/release artifacts.

## Package Registry

| Package | Release Notes Directory | How to Identify PRs |
| ------- | ----------------------- | ------------------- |
| `Microsoft.Data.SqlClient` | `release-notes/<Major.Minor>/` | Default — PRs not assigned to another package |
| `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` | `release-notes/add-ons/AzureKeyVaultProvider/<Major.Minor>/` | Labels containing `AKV`, or PR titles/bodies/files referencing `AzureKeyVaultProvider`, `add-ons/`, or `AlwaysEncrypted.AzureKeyVaultProvider` |
| `Microsoft.SqlServer.Server` | `release-notes/MSqlServerServer/<Major.Minor>/` | PR titles/bodies/files referencing `Microsoft.SqlServer.Server` or `src/Microsoft.SqlServer.Server/` |
| `Microsoft.Data.SqlClient.Extensions.Abstractions` | `release-notes/Extensions/Abstractions/<Major.Minor>/` | PR titles/bodies/files referencing `Extensions.Abstractions` |
| `Microsoft.Data.SqlClient.Extensions.Azure` | `release-notes/Extensions/Azure/<Major.Minor>/` | PR titles/bodies/files referencing `Extensions.Azure` |
| `Microsoft.Data.SqlClient.Internal.Logging` | `release-notes/Internal/Logging/<Major.Minor>/` | PR titles/bodies/files referencing `Internal.Logging` |

> **Not all packages exist on every branch.** This table is the full, current package set. Older release branches ship a subset — for example, `release/6.1` and earlier have no extension packages (`Extensions.Abstractions`, `Extensions.Azure`) and no `Internal.Logging`; the companion-package set and even the `AzureKeyVaultProvider` source location vary by branch. Before generating notes for a package, confirm it actually exists on the target branch `${input:branch}` (e.g., `git ls-tree -r --name-only ${input:branch} | grep -i "<package path fragment>"`). Skip any package that does not exist on `${input:branch}`, even if the table lists it.

## Version and Dependency Lookup

Each package's version and dependency information comes from MSBuild props/project files **on the target branch `${input:branch}`** (see Branch Model). The exact file paths, file names, and property names that hold versions **differ by branch**, because the versioning layout was refactored over time. Do not assume the layout of your current checkout — discover the version source on `${input:branch}`.

Two known layouts:

| Layout | Branches | MDS version source | Companion/extension version sources |
| ------ | -------- | ------------------ | ----------------------------------- |
| **Centralized** | `release/7.0` (and earlier 7.0.x) | `tools/props/Versions.props` (`MdsVersionDefault`) | `tools/props/Versions.props` imports per-package props with the older names: `…/Extensions/Abstractions/src/AbstractionsVersions.props`, `…/Extensions/Azure/src/AzureVersions.props`, `…/Internal/Logging/src/LoggingVersions.props`, `…/Microsoft.Data.SqlClient/add-ons/AzureKeyVaultProvider/AkvProviderVersions.props` |
| **Per-package** | `main`, `7.1+` | `src/Microsoft.Data.SqlClient/Versions.props` (`SqlClientVersionDefault`) | Each package has its own `Versions.props`: `…/Extensions/Abstractions/src/Versions.props` (`AbstractionsVersionDefault`), `…/Extensions/Azure/src/Versions.props` (`AzureVersionDefault`), `…/Internal/Logging/src/Versions.props` (`LoggingVersionDefault`), `…/AlwaysEncrypted.AzureKeyVaultProvider/src/Versions.props` (`AkvProviderVersionDefault`), `…/Microsoft.SqlServer.Server/Versions.props` (`SqlServerVersionDefault`) |

Discovery approach (works regardless of layout):

1. List the version props on the target branch, e.g. `git ls-tree -r --name-only ${input:branch} | grep -i "Versions.props$"`.
2. Read the relevant file from the target branch, e.g. `git show ${input:branch}:<path>`, and find the package's default/`PackageVersion` property.
3. Prefer the explicit shipped version: on a release branch the actual version may be supplied by the pipeline (`...PackageVersion`) rather than the `...VersionDefault` fallback, so confirm against the milestone/release artifacts rather than assuming the default.

Dependency sources (read from `${input:branch}`): the per-package project file (`src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj`, the AKV `.csproj`, `Abstractions.csproj`, `Azure.csproj`, `Logging.csproj`, `Microsoft.SqlServer.Server.csproj`) plus the centrally-managed concrete versions in `Directory.Packages.props`. Framework-conditional versions (e.g., `net9.0` vs everything else) are handled by `Condition` attributes there.

> **Companion package version alignment (7.0.2 and later):** Starting with 7.0.2, the companion packages (`AzureKeyVaultProvider`, `Extensions.Azure`, `Extensions.Abstractions`, `Internal.Logging`) ship version-aligned with the core `Microsoft.Data.SqlClient` driver. When generating notes for an aligned release, use the core MDS version (read from the target branch) for these companion packages — their per-package default version on `main` may point at a different next version and must not be assumed to be the shipped version. `Microsoft.SqlServer.Server` continues to version independently.

## Skills

This prompt uses the following skill:
- [fetch-milestone-prs](.github/skills/fetch-milestone-prs/SKILL.md) — Fetches all merged PR metadata for the milestone

## Steps

### 1. Fetch Milestone Items

- Follow the instructions in the [fetch-milestone-prs](.github/skills/fetch-milestone-prs/SKILL.md) skill to fetch all merged PRs for the milestone "${input:milestone}".
- The output will be saved to `.milestone-prs/${input:milestone}/${input:branch}` with individual JSON files per PR and an `_index.json` summary.
- Identify any milestone items that don't have corresponding commits on the release branch "${input:branch}", and vice versa.

### 2. Analyze and Categorize

- Read the `_index.json` file to get an overview of all PRs. Read individual PR JSON files for full details (title, body, labels).
- For PRs that are porting other PRs to the current branch, read the original PR's JSON file or look up the original PR number mentioned in the body for more context.
- Categorize PRs into: `Added`, `Fixed`, `Changed`, `Removed`.
- Ignore PRs labelled `Area\Engineering` (use the `has_engineering_label` field in the JSON).
- Identify the contributors for the "Contributors" section.
- **Assign each PR to one or more packages** using the identification rules in the Package Registry table. A PR may be relevant to multiple packages. PRs not matching any non-core package belong to `Microsoft.Data.SqlClient`.

### 2.1. Determine Target Framework (TFM) Scope Per Change

For each PR included in release notes, determine whether it applies to all supported TFMs for the package or only a subset.

Use source-level evidence (not assumptions) to classify scope:

- **TFM-specific files** indicate scoped impact (for example, `.netfx.cs`, `.netcore.cs`).
- **Conditional compilation** indicates scoped impact (for example, `#if NETFRAMEWORK`, `#if NET`).
- **Project or build conditions** indicate scoped impact (for example, `Condition` expressions on `TargetFramework` or `TargetFrameworks`).
- **Tests-only TFM changes** should not be called out as customer-facing unless the behavior change is also present in product code.

When writing notes:

- If the change affects **all supported TFMs** for that package, do not add a TFM qualifier.
- If the change affects **only some TFMs**, include an explicit qualifier in the relevant bullet or section title.
- Use concise qualifiers like:
  - `(net462 only)`
  - `(net8.0/net9.0 only)`

Do not infer TFM scope from labels alone; verify from changed files and code paths.

### 2.2. Determine Operating System (OS) Scope Per Change

For each PR included in release notes, determine whether it applies to all supported OS targets for the package or only a subset.

Use source-level evidence (not assumptions) to classify scope:

- **OS-specific files** indicate scoped impact (for example, `.windows.cs`, `.unix.cs`).
- **OS preprocessor symbols** indicate scoped impact (for example, `#if _WINDOWS`, `#if _UNIX`).
- **Project/build conditions** indicate scoped impact (for example, `TargetOs`, `NormalizedTargetOs`, or OS-conditional `ItemGroup`/`PropertyGroup` entries).
- **SNI implementation or native dependency gates** can imply OS scope when behavior changes only apply to native Windows SNI vs managed cross-platform paths.
- **Tests-only OS changes** should not be called out as customer-facing unless the behavior change is also present in product code.

When writing notes:

- If the change affects **all supported OS targets**, do not add an OS qualifier.
- If the change affects **only some OS targets**, include an explicit qualifier in the relevant bullet or section title.
- Use concise qualifiers like:
  - `(Windows only)`
  - `(Unix only)`
  - `(Linux only)`
  - `(macOS only)`
- If both TFM and OS are scoped, combine them in one qualifier, for example: `(net8.0/net9.0 on Windows only)`.

Do not infer OS scope from labels alone; verify from changed files and code paths.

### 3. Enrich Feature Sections with Issue Context

For significant features or bug fixes that reference a GitHub issue:

1. Read the referenced issue to understand the original request, use cases, and community context.
2. Use this information to write richer *Who Benefits* and *Impact* sections — don't just restate the PR description.
3. Include the issue link alongside PR links (e.g., `([#1108](https://github.com/dotnet/SqlClient/issues/1108), [#3680](https://github.com/dotnet/SqlClient/pull/3680))`).

### 4. Verify API Names from Source Code

When release notes reference a public API (property, method, class):

1. Search the source code (`src/Microsoft.Data.SqlClient/src/`) to confirm the exact name, type, and signature.
2. Check XML doc comments for usage warnings or caveats that should be included in the *Impact* section.
3. Never guess API names from PR titles — always verify against the actual code.

### 5. Generate Release Notes for Each Package

For each package that ships in this milestone — i.e., it has relevant PRs, **or** it is a version-aligned companion package (7.0.2+) bumping to match the core `Microsoft.Data.SqlClient` release even without its own changes (see item 2):

1. **Determine the package version** using the Version Source from the lookup table above. Read the actual props/project file to find the version.

2. **Create the release notes file** at the path shown in the Package Registry table: `<Directory>/<Version>.md`.
   - Use the template from [release-notes/template/release-notes-template.md](release-notes/template/release-notes-template.md).
   - Fill in the template following the instructions in each section.
   - Only include sections (Added, Changed, Fixed, Removed) that have entries.
   - For each Added/Changed/Fixed/Removed item, include TFM and OS scope qualifiers when Step 2.1 or Step 2.2 determines the change is not universal across the package's supported targets.
   - Look up dependencies using the Dependency Sources from the lookup table above. Resolve concrete versions from [Directory.Packages.props](Directory.Packages.props).
   - List dependencies per target framework. Use the project file's `<TargetFrameworks>` to determine which frameworks to list.
   - Omit the Contributors section for packages with no public contributors.
   - **GA releases (all packages):** When the release is a stable (non-preview) version, structure the notes with two sections:
     1. **"Changes Since [last preview]"** — only the delta since the most recent preview of this package.
     2. **"Cumulative Changes Since [last stable]"** — all changes since the last stable release of this package, synthesized from all preview release notes plus the GA milestone. This applies to every package (MDS, AKV, Extensions.Azure, Abstractions, Internal.Logging, etc.), not just the core driver. Apply the cross-referencing from Step 3 to eliminate items already shipped in prior stable patch releases.
   - **Preview releases:** Only include the delta since the previous release (preview or stable). No cumulative section is needed.
   - **Version-alignment-only releases (7.0.2+ companion packages):** Starting with 7.0.2, the companion packages (`AzureKeyVaultProvider`, `Extensions.Azure`, `Extensions.Abstractions`, `Internal.Logging`) ship a new version aligned with the core driver on every core release — **even when they have no functional or API changes**. In that case, still create the package's release notes file using a version-alignment-only style: state that there are no functional or API changes, note the version alignment with the core driver, link to the core `Microsoft.Data.SqlClient <Version>` notes, and (for .NET Framework) call out any `AssemblyVersion` strong-name change. Use the shipped 7.0.2 companion notes as the reference pattern (e.g., [release-notes/Extensions/Abstractions/7.0/7.0.2.md](release-notes/Extensions/Abstractions/7.0/7.0.2.md), [release-notes/Internal/Logging/7.0/7.0.2.md](release-notes/Internal/Logging/7.0/7.0.2.md), [release-notes/add-ons/AzureKeyVaultProvider/7.0/7.0.2.md](release-notes/add-ons/AzureKeyVaultProvider/7.0/7.0.2.md)). For the `Internal.Logging` package, retain its internal-use note. `Microsoft.SqlServer.Server` is **not** version-aligned and follows the normal skip rule.

3. **Create or update the version README** at `<Directory>/README.md`. Follow the existing format — see [release-notes/add-ons/AzureKeyVaultProvider/6.1/README.md](release-notes/add-ons/AzureKeyVaultProvider/6.1/README.md) for reference:

   ```markdown
   # <Full Package Name> <Major.Minor> Releases

   The following `<Full Package Name>`
   <Major.Minor> releases have been shipped:

   | Release Date | Description | Notes |
   | :-- | :-- | :--: |
   | <Date> | <Version> | [Release Notes](<Version>.md) |
   ```

4. **Skip packages without changes (or that don't exist on the branch).** If a package has no relevant PRs in the milestone, or the package does not exist on the target branch `${input:branch}` (see the Package Registry note — older branches like `release/6.1` have no extension or `Internal.Logging` packages), do not create release notes for it. **Exception (7.0.2+):** version-aligned companion packages (`AzureKeyVaultProvider`, `Extensions.Azure`, `Extensions.Abstractions`, `Internal.Logging`) still get a release notes file when they bump to the aligned core version, even with no functional changes — use the version-alignment-only style from item 2. Report which packages had changes, which shipped alignment-only notes, which did not, and which are not present on the branch.

5. **Cross-link companion packages from the core release notes.** When one or more companion packages (`AzureKeyVaultProvider`, `Extensions.Azure`, `Extensions.Abstractions`, `Internal.Logging`, `Microsoft.SqlServer.Server`) also ship in this milestone, add a `### Companion package release notes` section to the core `Microsoft.Data.SqlClient` release notes file that links to each companion package's release notes for the same version. This preserves context for the companion packages when the core release notes are used as the published GitHub release body. Use relative links (e.g., `../Extensions/Azure/<Major.Minor>/<Version>.md`). Only list packages that actually shipped release notes in this milestone.

### 6. Update CHANGELOG.md

- Add a new entry at the top of the list (under the Note) in [CHANGELOG.md](CHANGELOG.md).
- Include all the text from the Added, Fixed, Changed, Removed, etc. sections from the core `Microsoft.Data.SqlClient` release notes.
- If other packages also changed, include a brief summary line for each (e.g., "Released Microsoft.Data.SqlClient.Extensions.Azure 1.0.0-preview1. See [release notes](...)."). The detailed notes live in the per-package release notes files.

### 7. Update Top-Level Release Notes README

- Update [release-notes/README.md](release-notes/README.md):
  - Add the new release to the appropriate package section.
  - If a section for the package doesn't yet exist, add one following the existing pattern (see the `AzureKeyVaultProvider` and `Microsoft.SqlServer.Server` sections for reference).
  - If the section already exists, add the new version link to its Release Information list.

### 8. Markdown for GitHub Release

- Use the contents of the new release notes markdown file to produce markdown suitable for pasting into a GitHub UI Release textbox.
  - GitHub renders newlines within paragraphs and lists as hard breaks, so remove those.
  - Omit the main heading and first sub-heading.
  - Update any relative links to use absolute URLs pointing to the file in the repository.
  - Provide this new markdown in a code block that can easily be copied and pasted directly into the GitHub UI.

## Notes

- Release notes are maintained on `main` for all branches/releases, while the corresponding source code lives only on the target branch `${input:branch}` (see Branch Model above). Read version/dependency/source files from `${input:branch}`; write release notes files on `main`.
- Packages may ship as preview or stable independently. Use the actual version from the project/spec files on the target branch.
- The directory structure mirrors existing conventions: `add-ons/AzureKeyVaultProvider/` for AKV, `MSqlServerServer/` for SqlServer, `Extensions/<PackageName>/` for the extension packages (e.g., `Extensions/Abstractions/`, `Extensions/Azure/`), and `Internal/Logging/` for the internal logging package.
- When referencing code samples, link to files in the `doc/samples/` directory if a relevant sample exists.
