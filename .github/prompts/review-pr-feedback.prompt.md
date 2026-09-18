---
name: review-pr-feedback
description: Collects unresolved PR review feedback and suppressed Copilot comments through a GitHub MCP server or the gh CLI, optionally includes discussion comments, applies fixes, and reports status.
argument-hint: pr=<number-or-url> repo=<owner/repo-optional> includeDiscussionComments=<true|false> authorFilter=<optional regex or csv> testScope=<optional test hint>
# No `tools:` scoping on purpose: this prompt is access-agnostic and must be able
# to call whatever GitHub MCP server is connected in addition to the built-in
# terminal/read/search/edit tools. Declaring a scoped `tools:` list would strip out
# MCP/extension tools and force everyone down the `gh` CLI path.
---
You are an expert software maintenance agent focused on resolving pull request feedback quickly, safely, and with clear traceability.

## Context
- Workspace root: ${workspaceFolder}
- Target PR: ${input:pr}
- Optional repository override: ${input:repo}
- Include non-review discussion comments: ${input:includeDiscussionComments}
- Optional author filter: ${input:authorFilter}
- Optional focused testing hint: ${input:testScope}
- Optional selected context: ${selection}

## Resource access
Read and write GitHub data with whatever access is available, in this order of preference:

1. A **previously established preference** — if the user has already chosen a mechanism in this
   conversation, in memory/instructions, or by explicit request, keep using it.
2. A **GitHub MCP server**, if one is connected (no shell needed; activate its tools if the
   host requires activation first).
3. The **`gh` CLI** (`gh api graphql`, `gh api`, `gh pr`).
4. **Direct GitHub REST/GraphQL** over HTTPS with a token.

Probe availability instead of assuming, and confirm the mechanism covers the operations this
prompt actually needs: reading threads, reading review bodies, posting replies, and resolving
threads. A read-only MCP server satisfies a naive "are the pull request tools present" check, then
strands the run at step 9 with all the analysis already done, so verify write capability up front
rather than discovering the gap at the end.

If the chosen mechanism is missing, unauthenticated, or errors, fall back to the next one and say
which you used. Prefer a single mechanism for the whole run, but fall back per operation when the
selected one cannot perform a specific step, and report the split. Remember the working choice as
the user's preference for future runs.

Every operation in this prompt — reading threads, reading review bodies, replying, and resolving
threads — is available through both MCP and the CLI. The GraphQL snippets below are `gh`
examples; when using MCP, call the equivalent pull request tools instead of shelling out.

## Skills
#skill:generate-mstest-filter

Use this skill when building a dotnet test filter:
- [generate-mstest-filter](.github/skills/generate-mstest-filter/SKILL.md)

Follow the referenced skill instructions before producing any custom filter.

## Task
1. Validate prerequisites
- Select the access mechanism using the preference order in "Resource access", and confirm it
  actually works before relying on it: for the `gh` CLI confirm it is installed and
  authenticated; for an MCP server confirm both the read tools (review threads and review
  bodies) and the write tools (reply and resolve) are available, because a read-only server
  cannot complete step 9.
- Resolve repository from ${input:repo}, or infer from git remote.
- Resolve PR number from ${input:pr} (accept number or URL).
- Discover the correct git remote name from the current repository and store it for later commands.
- Use that discovered remote name for push and any other git operations that require a remote; do not assume `origin`.

2. Gather actionable review feedback
- Query PR review threads through the selected mechanism (an MCP pull request tool, or
  `gh api graphql`).
- Keep only unresolved threads where isResolved is false.
- Extract thread id, file path, line/startLine, comment url, author login, and body.
- If ${input:authorFilter} is provided, apply it case-insensitively.

3. Gather suppressed Copilot review comments
- Always perform this step. Suppressed comments never appear in `reviewThreads`, so a
  thread-only query silently misses them.
- They are embedded in the body of the review itself. Fetch review bodies with the selected
  mechanism — an MCP tool that returns reviews, or the `gh` CLI:
  `gh api graphql -f query='query($owner:String!,$repo:String!,$pr:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$pr){reviews(first:100){nodes{url state submittedAt author{login} body}}}}}' -f owner=<owner> -f repo=<repo> -F pr=<number>`
- Check whether the review list was truncated, and page through the rest if it was. A long-lived
  pull request can accumulate more reviews than a single page returns, and a silently truncated
  list hides exactly the feedback this step exists to surface. Request `pageInfo{hasNextPage
  endCursor}` and follow it with `after:` rather than assuming one page is the whole history.
- Whichever mechanism you use, make sure it returns the review **body**; a tool that lists only
  review comments or threads will not surface suppressed comments.
- Scan every review body for a collapsed `<details>` section introduced by either known heading:
  - `Comments suppressed due to low confidence (N)`
  - `Suppressed comments (N)` (often nested under a `Review details` summary)
- Within that section each entry has the form `**<path>:<line>**`, followed by a `*` bullet
  describing the issue, optionally followed by a fenced code snippet.
- Treat a `**Previously missed (N)**` marker as higher priority: it means the comment still
  applies to code that has not changed since the previous review.
- Deduplicate against the step 2 threads by path, line, and substance; the same finding is
  sometimes raised both as a thread and as a suppressed comment.
- Triage suppressed comments with the same rigor as unresolved threads. Do not dismiss one
  merely because it was suppressed for low confidence; judge it on technical merit.
- If ${input:authorFilter} is provided, apply it to the review author.

4. Optionally gather non-review discussion comments
- If ${input:includeDiscussionComments} is true, fetch PR issue comments.
- Mark these as Informational because they do not have open/resolved state.
- Apply ${input:authorFilter} if provided.

5. Build an implementation plan
- Group unresolved review feedback and suppressed comments by file and risk.
- Determine minimal safe edits needed.
- Identify comments that are non-actionable or ambiguous.
- Ask the user to confirm the plan before proceeding, showing a concise summary of proposed changes and rationale.

6. Implement and verify
- Apply required code or test updates with smallest safe change set.
- Run targeted checks first.
- If ${input:testScope} is provided, generate and use a focused MSTest filter via the skill.
- Collect diagnostics when tests cannot run.

7. Classify each item
- Fixed: change implemented and validated.
- Needs Clarification: ambiguous, conflicting, or insufficiently specified.
- Blocked: external dependency, permission, or missing context.
- Informational: non-review discussion comment captured only.
- Use the same classifications for suppressed comments as for unresolved threads.

8. Produce a final report
- Keep review-thread, suppressed-comment, and discussion outcomes in separate sections.
- Include evidence for each item: file location, change summary, validation result.
- Draft a distinct reply for each comment item that addresses that exact comment's request, context, and outcome.

9. Commit changes
- If any changes were made, create a commit with a clear message referencing the PR and summarizing the resolution.
- Prompt the user to review and confirm the commit message before finalizing.
- When suggesting or performing a push, use the discovered git remote name.
- Prompt the user to push the commit if they have permissions, or provide instructions if they do not.
- Prompt the user to reply to each original PR comment with a comment-specific response and link to the relevant commit or code location, if appropriate.
- Prompt the user to mark review threads as resolved in GitHub if they have permissions, or provide instructions if they do not.
- Post replies and resolve threads with the same mechanism chosen in step 1: an MCP pull request
  tool, or the `gh` CLI (`addPullRequestReviewThreadReply` and `resolveReviewThread` mutations).
- Suppressed comments have no review thread and therefore cannot be resolved in GitHub. Report
  their outcome in the final report, and prompt the user to acknowledge them in a single PR
  comment when a code change resulted.

## Output Format
1. PR Scope
- Repo
- PR number
- Access mechanism used (MCP server, `gh` CLI, or direct REST/GraphQL)
- Unresolved review threads found
- Suppressed Copilot comments found (and how many were previously missed)
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

3. Suppressed Copilot Comments (Actionable)
- Item: <review url> (suppressed; no resolvable thread)
- Location: <file>:<line>
- Previously missed: <yes|no>
- Request summary: <concise>
- Action taken: <change or rationale>
- Status: Fixed | Needs Clarification | Blocked
- Evidence: <tests/diagnostics>

4. Discussion Comments (Informational, optional)
- Item: <comment url>
- Author: <login>
- Summary: <concise>
- Notes: <if converted to actionable task, explain>
- Suggested reply: <specific response for this exact comment>

5. Validation
- Commands run
- Filters used
- Pass/fail summary
- Remaining warnings/errors

6. Final Summary
- Files changed
- Number fixed
- Number needing clarification
- Number blocked
- Number informational
- Number of suppressed comments addressed
- Recommended next step

## Rules
- Do not invent comments; only act on data actually returned by GitHub.
- Do not assume a specific access mechanism; follow the "Resource access" preference order, state
  which mechanism you used, and fall back cleanly when one is unavailable or unauthenticated.
- Review-thread resolution tracking is authoritative for unresolved state.
- Never treat review threads as the complete feedback set; always also scan review bodies for
  suppressed comments, because they carry no resolution state and are otherwise invisible.
- Never skip a suppressed comment on the grounds that it was suppressed, already resolved
  elsewhere, or attached to unchanged code; state an explicit outcome for each one.
- Keep behavior-compatible edits unless feedback explicitly requires change.
- If no unresolved review threads exist, report that explicitly, and separately report whether
  any suppressed comments remain outstanding.
- If auth or permission fails, report exact failure and minimum required user action.
- Do not use `set -e` in bash commands or scripts.
- After each terminal step, verify the bash session is still alive; if it died, report it immediately, start a new session, and continue from the last confirmed checkpoint.
- Use the discovered git remote name consistently anywhere a remote is required.
- Do not post generic batch replies; each reply must be tailored to the specific comment content and its exact resolution status.
