# Generate Release Notes

This prompt guides the AI to generate release notes for a specific milestone of the Microsoft.Data.SqlClient project.

## Prerequisites

- The user must provide the **Milestone Name** (e.g., `7.0.0-preview3`).
- The user should specify if this is a preview or stable release if it's not obvious from the milestone name.

## Workflow Steps

1.  **Fetch Milestone Items**:
    - Search for all **merged** Pull Requests associated with the specified milestone in the `dotnet/SqlClient` repository.
    - Use `mcp_github_search_issues` with query `is:pr is:merged milestone:"<Milestone Name>" repo:dotnet/SqlClient`.

2.  **Analyze and Categorize**:
    - Review the title and body of each PR.
    - Categorize them into: `Added`, `Fixed`, `Changed`, `Removed`, etc.
    - Identify the contributors for the "Contributors" section.

3.  **Draft Release Notes Content**:
    - **Header**: `# Release Notes` followed by `## <Release Type> Release <Version> - <Date>`.
    - **Changes Link**: `## Changes Since [<Previous Version>](<Previous Version>.md)`.
    - **Items**: For each categorized item, generate a detailed entry:
        - **Title**: The PR title or a summarized version.
        - **Link**: Link to the PR `([#<PR Number>](<PR URL>))`.
        - **Details**:
            - `*What Changed:*`: A brief description of the technical change.
            - `*Who Benefits:*`: The target audience (e.g., Developers using feature X).
            - `*Impact:*`: The effect on the application (e.g., Performance improvement, Bug fix).
    - **Contributors**: List public contributors (excluding core team if possible, or list all non-bot contributors).
    - **Target Platform Support**:
        - Check `README.md` or project files for current support.
        - List supported .NET Framework and .NET Core/.NET versions and OSs.
    - **Dependencies**:
        - Analyze `Directory.Packages.props` and `src/**/*.csproj` files to list dependencies for each target framework (.NET Framework, .NET Core/Standard).
        - Group them by target framework (e.g., .NET Framework 4.6.2+, .NET 8.0, .NET 9.0).

4.  **Create Release Notes File**:
    - Determine the correct path: `release-notes/<Major.Minor>/<Version>.md`.
    - Create the file with the drafted content.

5.  **Update CHANGELOG.md**:
    - Add a new entry at the top of the list (under the Note).
    - Format: `## [<Release Type> Release <Version>] - <Date>`.
    - Include the "Changes Since..." text and the categorized items (Added, Fixed, Changed).
    - *Note*: You can omit the detailed "What Changed/Who Benefits/Impact" sections in the Changelog to keep it concise, or include them if requested. The previous pattern suggests including the full details or a summary. *Follow the existing pattern in CHANGELOG.md*.

6.  **Update Release Directory README**:
    - Update `release-notes/<Major.Minor>/README.md`.
    - Add the new release to the table: `| <Date> | <Version> | [Release Notes](<Version>.md) |`.

## Example Usage

"Generate release notes for milestone 7.0.0-preview3."
