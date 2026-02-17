---
name: release-notes
description: Generate release notes for a specific milestone of the Microsoft.Data.SqlClient project.
argument-hint: <milestone>
agent: agent
tools: ['github/search_issues', 'edit/createFile', 'edit/editFiles', 'read/readFile']
---

Generate release notes for the milestone "${input:milestone}".

Steps:
1. Fetch Milestone Items
    - Search for all **merged** Pull Requests associated with the milestone "${input:milestone}" in the `dotnet/SqlClient` repository.
    - Use `github/search_issues` with query `is:pr is:merged milestone:"${input:milestone}" repo:dotnet/SqlClient`.
2. Analyze and Categorize
    - Review the title and body of each PR. For PRs that are porting other PRs to current branch use `github/search_issues` with query `is:pr is:merged repo:dotnet/SqlClient <original PR number>` to get more context.
    - Categorize them into: `Added`, `Fixed`, `Changed`, `Removed`.
    - Ignore PRs that are labelled as `Area\Engineering`
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
