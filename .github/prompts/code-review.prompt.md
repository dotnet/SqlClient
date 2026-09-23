---
name: code-review
description: AI-assisted code review for a pull request or branch in Microsoft.Data.SqlClient.
argument-hint: <PR number, PR URL, branch name, or local changes>
agent: agent
tools: ['github/search_issues', 'github/issue_read', 'github/pull_request_read', 'github/get_file_contents', 'read/readFile', 'search/changes', 'search/codebase', 'search/fileSearch', 'search/listDirectory', 'search/textSearch', 'search/usages', 'web/fetch', 'vscode/askQuestions']
---

Review the changes in "${input:target}" for `dotnet/SqlClient`.

Load and follow the [SqlClient code-review skill](../skills/sqlclient-code-review/SKILL.md).
It is the shared review procedure for this prompt and automated reviewers.

This prompt produces draft findings using only the read-only tools listed above.
Publishing, running tests, or applying requested fixes requires a separate workflow
with explicitly authorized tools; do not expand this prompt's permissions.
If the available tools cannot retrieve the requested branch comparison or pinned
revision, request that context or report the review as partial rather than using
a terminal fallback.

- Establish the comparison base and reviewed revision before inspecting changes.
- Follow affected call paths and applicable driver checks; report only
  substantiated, actionable findings.
- Keep coverage gaps and unresolved questions separate from demonstrated defects.
- Return draft findings. Automated publishers can use the shared skill in their
  own explicitly scoped workflow.
- Do not modify code, approve, merge, or resolve human review threads.
