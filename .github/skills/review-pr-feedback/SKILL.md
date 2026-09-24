---
name: review-pr-feedback
description: Uses gh CLI to collect unresolved PR review feedback, optionally includes discussion comments, applies fixes, and reports status. Invoke explicitly with /review-pr-feedback and a PR number or URL.
disable-model-invocation: true
argument-hint: <pr-number-or-url> [repo owner/name] [include discussion comments] [author filter] [test scope]
---
You are an expert software maintenance agent focused on resolving pull request feedback quickly, safely, and with clear traceability.

## Inputs

Read the following from the text the user supplies after the slash command, for example
`/review-pr-feedback 3412 include discussion comments from dnfadmin`. Only the PR is
required.

| Input | How to resolve it |
| --- | --- |
| **PR** (required) | A PR number or URL in the request. If absent, ask for it and stop. |
| **Repository** | An explicit `owner/name` in the request; otherwise infer from the git remote of the current workspace. |
| **Include discussion comments** | Default no. Treat as yes only if the request asks for discussion, issue, or non-review comments. |
| **Author filter** | A name, regex, or comma-separated list in the request. Default no filtering. |
| **Test scope** | A hint about which tests to run. Default is to choose targeted tests yourself. |

Work in the current workspace folder. If the user has selected text or attached files,
treat that as additional context about which feedback matters most.

## Skills

When you need a `dotnet test` filter expression, use the `generate-mstest-filter` skill
and follow its instructions rather than hand-writing a filter.

## Task
1. Validate prerequisites
- Confirm gh CLI is installed and authenticated.
- Resolve the repository from the request, or infer it from the git remote.
- Resolve the PR number from the request (accept a number or a URL).
- Discover the correct git remote name from the current repository and store it for later commands.
- Use that discovered remote name for push and any other git operations that require a remote; do not assume `origin`.

2. Gather actionable review feedback
- Query PR review threads with gh api GraphQL.
- Keep only unresolved threads where isResolved is false.
- Extract thread id, file path, line/startLine, comment url, author login, and body.
- If an author filter was given, apply it case-insensitively.

3. Optionally gather non-review discussion comments
- Only if the user asked for discussion comments, fetch PR issue comments.
- Mark these as Informational because they do not have open/resolved state.
- Apply the author filter if one was given.

4. Build an implementation plan
- Group unresolved review feedback by file and risk.
- Determine minimal safe edits needed.
- Identify comments that are non-actionable or ambiguous.
- Ask the user to confirm the plan before proceeding, showing a concise summary of proposed changes and rationale.

5. Implement and verify
- Apply required code or test updates with smallest safe change set.
- Run targeted checks first.
- If a test scope was given, use the `generate-mstest-filter` skill to build a focused filter for it.
- Collect diagnostics when tests cannot run.

6. Classify each item
- Fixed: change implemented and validated.
- Needs Clarification: ambiguous, conflicting, or insufficiently specified.
- Blocked: external dependency, permission, or missing context.
- Informational: non-review discussion comment captured only.

7. Produce a final report
- Keep review-thread outcomes and discussion outcomes in separate sections.
- Include evidence for each item: file location, change summary, validation result.
- Draft a distinct reply for each comment item that addresses that exact comment's request, context, and outcome.

8. Commit changes
- If any changes were made, create a commit with a clear message referencing the PR and summarizing the resolution.
- Prompt the user to review and confirm the commit message before finalizing.
- When suggesting or performing a push, use the discovered git remote name.
- Prompt the user to push the commit if they have permissions, or provide instructions if they do not.
- Prompt the user to reply to each original PR comment with a comment-specific response and link to the relevant commit or code location, if appropriate.
- Prompt the user to mark review threads as resolved in GitHub if they have permissions, or provide instructions if they do not.

## Output Format
1. PR Scope
- Repo
- PR number
- Unresolved review threads found
- Discussion comments found (if enabled)

2. Unresolved Review Feedback (Actionable)
- Item: <comment url>
- Location: <file>:<line>
- Author: <login>
- Request summary: <concise>
- Action taken: <change or rationale>
- Status: Fixed | Needs Clarification | Blocked
- Evidence: <tests/diagnostics>
- Suggested reply: <specific response for this exact comment>

3. Discussion Comments (Informational, optional)
- Item: <comment url>
- Author: <login>
- Summary: <concise>
- Notes: <if converted to actionable task, explain>
- Suggested reply: <specific response for this exact comment>

4. Validation
- Commands run
- Filters used
- Pass/fail summary
- Remaining warnings/errors

5. Final Summary
- Files changed
- Number fixed
- Number needing clarification
- Number blocked
- Number informational
- Recommended next step

## Rules
- Do not invent comments; only act on data fetched from gh.
- Review-thread resolution tracking is authoritative for unresolved state.
- Keep behavior-compatible edits unless feedback explicitly requires change.
- If no unresolved review threads exist, report that explicitly.
- If auth or permission fails, report exact failure and minimum required user action.
- Do not use `set -e` in bash commands or scripts.
- After each terminal step, verify the bash session is still alive; if it died, report it immediately, start a new session, and continue from the last confirmed checkpoint.
- Use the discovered git remote name consistently anywhere a remote is required.
- Do not post generic batch replies; each reply must be tailored to the specific comment content and its exact resolution status.
