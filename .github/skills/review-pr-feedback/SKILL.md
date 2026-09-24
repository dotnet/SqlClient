---
name: review-pr-feedback
description: Collects unresolved PR review feedback, optionally includes discussion comments, applies fixes, and reports status. Works through whichever GitHub access path is available (gh CLI, GitHub MCP server, or other). Invoke explicitly with /review-pr-feedback and a PR number or URL.
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
| **Tooling** | A preferred access path (for example "use the GitHub MCP server" or "use gh"). See Tool selection. |

Work in the current workspace folder. If the user has selected text or attached files,
treat that as additional context about which feedback matters most.

## Skills

When you need a `dotnet test` filter expression, use the `generate-mstest-filter` skill
and follow its instructions rather than hand-writing a filter.

## Tool selection

Nothing in this skill is tied to a specific tool. Use whatever is available to reach
GitHub and the repository — the `gh` CLI, a GitHub MCP server, a REST/GraphQL call with a
token, a git CLI, or editor-provided tools. Choose per capability, not once for the whole
run: it is normal and correct to read through one path and write through another.

Select a path for each capability in this order, stopping at the first that applies:

1. **What the user asked for in this request.** An explicit instruction always wins. If
   the requested path turns out not to work, say so and ask before substituting another.
2. **What is already working in this session.** If a path has already succeeded for a
   capability during this conversation, keep using it. Do not re-probe and do not switch
   mid-run without reason.
3. **What previous runs used.** Check any persistent memory or notes you have access to,
   and the visible conversation history, for a path this skill recorded earlier. Because
   the report from every run records the paths used (see Output Format), that record is
   the primary carrier of this preference across sessions. Treat a recorded path as a
   default, not as proof it still works — always re-run pre-flight against it.
4. **Whatever pre-flight proves capable**, preferring the path that needs the fewest
   permissions for the job.

If more than one path is viable after pre-flight, say which you chose and why. If the
user names a path that pre-flight shows cannot do part of the job, report the specific
gap rather than silently falling back.

## Pre-flight validation

Before gathering any feedback, confirm the paths you intend to use actually work. Probe
only with cheap, read-only, side-effect-free calls — never validate a write path by
performing a real write.

Validate these capabilities independently, since they need different permissions and are
often served by different paths:

| Capability | Needed for | Validate by |
| --- | --- | --- |
| Identify repo and PR | All steps | Resolve the repo and confirm the PR exists and is readable. |
| Read review threads with resolved state | Steps 2, 6 | Fetch one page of review threads and confirm an `isResolved` (or equivalent) field is present. A path that returns review comments but not their resolved state cannot drive this skill on its own. |
| Read discussion comments | Step 3 | Only if the user asked for them. Fetch one page of PR issue comments. |
| Read and edit local files | Step 5 | Confirm the workspace is the right repository and is writable. |
| Run tests/builds | Step 5 | Confirm the needed runner exists, for example that `dotnet` is on PATH. |
| Commit and push | Step 8 | Confirm the git remote name (do not assume `origin`) and check push permission without pushing. |
| Post replies and resolve threads | Step 8 | Confirm the path exposes these operations and the credential carries write scope. Resolving a review thread needs a GraphQL mutation that some paths, including some MCP servers, do not expose. |

Rules for pre-flight:

- Report the outcome as a short capability table before doing any work.
- A failure is only fatal if it blocks the capability it gates. Missing write access is
  not a reason to abort: continue read-only, and hand the user exact instructions for the
  steps you could not perform.
- If no path can read review threads with their resolved state, stop and report that —
  everything else in this skill depends on it.
- Report the precise failure, including which path and which operation failed, and the
  minimum action needed to fix it. Do not retry silently in a loop.
- If a path fails partway through the run, report it, re-validate, and confirm the
  substitute with the user before continuing.

## Task
1. Establish scope
- Complete Tool selection and Pre-flight validation above before continuing.
- Resolve the repository from the request, or infer it from the git remote.
- Resolve the PR number from the request (accept a number or a URL).
- Discover the correct git remote name from the current repository and store it for later commands.
- Use that discovered remote name for push and any other git operations that require a remote; do not assume `origin`.

2. Gather actionable review feedback
- Query the PR's review threads through the validated read path.
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
- For any write pre-flight showed is unavailable, give the user a ready-to-run command or a step-by-step alternative instead of attempting it.

## Output Format
1. PR Scope
- Repo
- PR number
- Access paths used, one line per capability (read, edit, test, commit/push, reply/resolve)
- Capabilities pre-flight found unavailable, and the resulting limits on this run
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
- Steps left to the user because a path was unavailable
- Recommended next step

## Rules
- Do not invent comments; only act on data actually fetched from GitHub.
- Review-thread resolution tracking is authoritative for unresolved state.
- Keep behavior-compatible edits unless feedback explicitly requires change.
- If no unresolved review threads exist, report that explicitly.
- If auth or permission fails, report the exact failure, the path it failed on, and the minimum required user action.
- Never substitute a different access path for one the user explicitly requested without telling them and getting agreement.
- Record the access paths used in the final report so later runs can prefer them.
- Do not use `set -e` in bash commands or scripts.
- After each terminal step, verify the bash session is still alive; if it died, report it immediately, start a new session, and continue from the last confirmed checkpoint.
- Use the discovered git remote name consistently anywhere a remote is required.
- Do not post generic batch replies; each reply must be tailored to the specific comment content and its exact resolution status.
