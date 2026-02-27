---
name: release-notes
description: Generate release notes for a specific milestone of the Microsoft.Data.SqlClient project.
argument-hint: <milestone>
agent: agent
tools: ['edit/createFile', 'edit/editFiles', 'read/readFile']
---

Generate release notes for the milestone "${input:milestone}".

## Skills

This prompt uses the following skill:
- [fetch-milestone-prs](.github/skills/fetch-milestone-prs/SKILL.md) — Fetches all merged PR metadata for the milestone

## Steps

1. Fetch Milestone Items
    - Follow the instructions in the [fetch-milestone-prs](.github/skills/fetch-milestone-prs/SKILL.md) skill to fetch all merged PRs for the milestone "${input:milestone}".
    - The output will be saved to `.milestone-prs/${input:milestone}/` with individual JSON files per PR and an `_index.json` summary.
2. Analyze and Categorize
    - Read the `_index.json` file to get an overview of all PRs. Read individual PR JSON files for full details (title, body, labels).
    - For PRs that are porting other PRs to the current branch, read the original PR's JSON file or look up the original PR number mentioned in the body for more context.
    - Categorize them into: `Added`, `Fixed`, `Changed`, `Removed`.
    - Ignore PRs that are labelled as `Area\Engineering` (use the `has_engineering_label` field in the JSON).
    - Identify the contributors for the "Contributors" section.
3. Create Release Notes File
    - Determine the correct path: `release-notes/<Major.Minor>/<Version>.md`.
    - Create the file with the template contents from `release-notes/template/release-notes-template.md`.
    - Fill in the template, following the instructions present in each section.
4. Update CHANGELOG.md
    - Add a new entry at the top of the list (under the Note).
    - Include all the text from the Added, Fixed, Changed, Removed, etc. sections from the release notes.
5. Update Release Directory README
    - Update `release-notes/<Major.Minor>/README.md`.
    - Add the new release to the table: `| <Date> | <Version> | [Release Notes](<Version>.md) |`.
