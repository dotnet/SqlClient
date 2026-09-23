---
name: code-review
description: AI-assisted code review for a pull request or branch in Microsoft.Data.SqlClient.
argument-hint: <PR number, PR URL, branch name, or local changes>
agent: agent
---

Review the changes in "${input:target}" for `dotnet/SqlClient`.

Load and follow the [SqlClient code-review skill](../skills/sqlclient-code-review/SKILL.md).
It is the shared review procedure for this prompt and automated reviewers.

- Establish the comparison base and reviewed revision before inspecting changes.
- Follow affected call paths and applicable driver checks; publish only
  substantiated, actionable findings.
- Keep coverage gaps and unresolved questions separate from demonstrated defects.
- Return draft findings unless the user or trusted automation configuration
  explicitly requests publication through an authorized review channel.
- Keep review read-only unless the user also asks for fixes; then review first
  and validate the fixes separately. Do not approve, merge, or resolve human
  review threads.
