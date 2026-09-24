---
name: review-pr-feedback
description: Collects PR feedback from every source — review threads, review bodies including Copilot's hidden low-confidence suppressed findings, and discussion comments — applies fixes, replies to every item, and resolves only bot-authored threads. Infers the PR from the current branch when none is given. Works through whichever GitHub access path is available (gh CLI, GitHub MCP server, or other). Invoke explicitly with /review-pr-feedback.
disable-model-invocation: true
argument-hint: "[pr-number-or-url] [repo owner/name] [author filter] [test scope]"
---
You are an expert software maintenance agent focused on resolving pull request feedback quickly, safely, and with clear traceability.

## Inputs

Read the following from the text the user supplies after the slash command, for example
`/review-pr-feedback 3412 from dnfadmin`. Nothing is strictly required: with no arguments
at all, infer the PR from the current branch.

| Input | How to resolve it |
| --- | --- |
| **PR** | A PR number or URL in the request. If absent, infer it from the current branch (see step 1). |
| **Repository** | An explicit `owner/name` in the request; otherwise infer from the git remote of the current workspace. |
| **Author filter** | A name, regex, or comma-separated list in the request. Default no filtering. Never applies to Copilot suppressed findings unless it names Copilot. |
| **Test scope** | A hint about which tests to run. Default is to choose targeted tests yourself. |
| **Tooling** | A preferred access path (for example "use the GitHub MCP server" or "use gh"). See Tool selection. |

Discussion comments are always inspected and are not an input; see step 4.

Work in the current workspace folder. If the user has selected text or attached files,
treat that as additional context about which feedback matters most.

## Feedback sources

Feedback reaches a PR through several channels, and each is gathered by a different step.
Tag every item you collect with its source and carry that tag through planning,
reporting, replying and resolving. The source determines where a reply can go and whether
the item can ever be resolved.

| Source | Gathered in | Has a thread | Reply goes to | Resolvable |
| --- | --- | --- | --- | --- |
| Review thread | Step 2 | Yes | The thread | Only if bot-authored |
| Review body | Step 3 | No | Summary comment | No |
| Review body, Copilot suppressed | Step 3 | No | Summary comment | No |
| Discussion comment | Step 4 | No | Summary comment | No |

Never report an item without its source.

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
| Identify repo and PR | All steps | Resolve the repo and confirm the PR exists and is readable. When the PR was inferred from the branch, confirm the match before anything else. |
| Look up PRs by branch | Step 1 | Only when no PR was supplied. Confirm the path can search PRs by head branch. |
| Read review threads with resolved state | Steps 2, 7 | Fetch one page of review threads and confirm an `isResolved` (or equivalent) field is present. A path that returns review comments but not their resolved state cannot drive this skill on its own. |
| Read full review bodies | Step 3 | Fetch the body text and state of every review on the PR. This is a different field from review threads, and both human review-body feedback and Copilot's suppressed feedback exist only here. A path that lists review comments but cannot return review bodies will silently miss both. |
| Read discussion comments | Step 4 | Fetch one page of PR issue comments. Always required. |
| Read and edit local files | Step 6 | Confirm the workspace is the right repository and is writable. |
| Run tests/builds | Step 6 | Confirm the needed runner exists, for example that `dotnet` is on PATH. |
| Identify comment authorship | Steps 10, 11 | Confirm the path reports author type, not just login: `__typename` of `Bot` or `User` in GraphQL, `user.type` in REST. Resolution decisions depend on this, so a path that cannot distinguish bots from humans must not be used to drive step 11. |
| Commit and push | Step 9 | Confirm the git remote name (do not assume `origin`) and check push permission without pushing. |
| Post thread replies | Step 10 | Confirm the path can reply to an existing review thread and the credential carries write scope. |
| Post a PR comment | Step 10 | Needed for the single summary comment covering non-thread feedback. |
| Resolve review threads | Step 11 | Confirm the path exposes thread resolution and the credential carries write scope. Resolving needs a GraphQL mutation that some paths, including some MCP servers, do not expose. |

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
- Discover the correct git remote name from the current repository and store it for later commands.
- Use that discovered remote name for push and any other git operations that require a remote; do not assume `origin`.
- Resolve the PR number from the request (accept a number or a URL).
- If the request names no PR, infer it from the workspace's current branch:
  - Read the current branch name and find the PR whose head branch matches it.
  - Prefer an open PR. If the only matches are merged or closed, say so explicitly, since
    replying to or resolving feedback on a closed PR is rarely intended.
  - If the branch belongs to a fork, match on the head repository as well as the branch
    name so a same-named branch in another fork cannot be picked up by mistake.
- Stop and ask the user which PR to use when inference cannot give a single confident
  answer: no branch (detached HEAD), the branch is the repository's default branch, no PR
  matches the branch, or more than one open PR matches.
- State the inferred PR — number, title and state — and get the user's confirmation before
  going further. This run posts public comments and resolves threads, so acting on the
  wrong PR is not silently recoverable.

2. Gather actionable review feedback
- Query the PR's review threads through the validated read path.
- Keep only unresolved threads where isResolved is false.
- Extract thread id, file path, line/startLine, comment url, author login, and body.
- Also record, for every comment in the thread, whether its author is a bot or a human,
  using the author type field rather than the login. Step 11 depends on this.
- If an author filter was given, apply it case-insensitively.

3. Always gather review body feedback
- This step is mandatory. Never skip it, and never make it conditional on user request.
- A review body is the text submitted with a review. It is a different field from the
  review's inline comments, so a query that returns review threads or review comments
  will not contain it. Feedback here has no thread and no resolved state.
- Fetch the body and state of every review on the PR, from every author.

3a. Review body text (any author)
- Reviewers routinely request changes in the body alone, with no inline comment at all,
  so ignoring bodies drops that feedback entirely.
- Read every non-empty body and judge whether it asks for anything. Treat a body as
  actionable when it requests a change, raises a concern, asks a question, or points at a
  failure to investigate, whatever the review state.
- Use the review state as a signal, not a verdict: `CHANGES_REQUESTED` is almost always
  actionable, but an `APPROVED`, `COMMENTED` or `DISMISSED` review can carry a real
  request too. Bare approvals such as "LGTM" with no request are informational.
- Skip the boilerplate a bot wraps around its findings, such as Copilot's overview,
  file tables and marketing footer. Keep only its substantive assessment.
- Apply the author filter if one was given.

3b. Copilot suppressed findings
- Copilot's code review hides low-confidence findings instead of posting them as review
  comments. They live inside the body of the Copilot review. They are frequently the
  majority of Copilot's findings on a PR.
- Fetch the body of every review on the PR authored by the Copilot reviewer bot. Identify
  it by author type plus a login containing `copilot`; the exact login is path-dependent
  and is reported as `copilot-pull-request-reviewer` by GraphQL but `Copilot` by REST.
- In each body, find the suppressed-feedback block. The wording varies by Copilot version,
  so match case-insensitively on a heading or `<details><summary>` containing
  "suppressed comments" or "comments suppressed due to low confidence", including when it
  is nested inside another collapsed section such as "Review details".
- Each entry is a `**path:line**` marker, followed by the finding text, optionally
  followed by a fenced snippet of the code it refers to. Extract path, line and full text.
- Collect across all Copilot reviews on the PR, not just the newest. Re-reviews repeat
  earlier findings, so deduplicate on path, line and substance.
- Apply the author filter only if the user's filter explicitly names Copilot; a filter
  naming other reviewers must not discard this feedback.
- Assess each on merit and treat it as actionable. Low confidence is Copilot's estimate,
  not a verdict: judge the finding against the actual code. If you reject one, say why.
- If a Copilot review exists but has no suppressed block, state that explicitly so the
  user can tell that apart from the step having been skipped.

4. Always gather non-review discussion comments
- This step is mandatory. Discussion comments are not an opt-in.
- Fetch the PR's comments, which are separate from reviews and from review threads.
- Inspect every one for review feedback. Maintainers regularly request changes in a plain
  PR comment instead of a formal review, and that feedback is as binding as any other.
- Classify each as actionable when it asks for a change, raises a concern, asks a
  question, or points at a failure to investigate; otherwise informational.
- Actionable discussion comments are planned, fixed and replied to on the same footing as
  review feedback. Informational ones are recorded and still replied to.
- These have no open/resolved state, so they cannot be resolution-filtered or resolved.
- Apply the author filter if one was given.

5. Build an implementation plan
- Group unresolved review feedback by file and risk.
- Include every actionable item from steps 3 and 4 in this grouping; review-body feedback, Copilot suppressed findings and actionable discussion comments are planned and fixed on the same footing as posted review comments.
- Determine minimal safe edits needed.
- Identify comments that are non-actionable or ambiguous.
- Ask the user to confirm the plan before proceeding, showing a concise summary of proposed changes and rationale, with each item's source shown.

6. Implement and verify
- Apply required code or test updates with smallest safe change set.
- Run targeted checks first.
- If a test scope was given, use the `generate-mstest-filter` skill to build a focused filter for it.
- Collect diagnostics when tests cannot run.

7. Classify each item
- Fixed: change implemented and validated.
- Needs Clarification: ambiguous, conflicting, or insufficiently specified.
- Blocked: external dependency, permission, or missing context.
- Informational: captured only, with no change required.
- Tag every item with its source from the Feedback sources table, and for review threads whether the authorship is bot or human. This determines where its reply goes in step 10 and whether it may be resolved in step 11.

8. Produce a final report
- Give review threads, review bodies, Copilot suppressed findings and discussion comments their own sections, and label every item with its source.
- Include evidence for each item: file location, change summary, validation result.
- Draft a distinct reply for every item of feedback, from any source, that addresses that exact item's request, context, and outcome.
- Every reply must state plainly either what was changed to address the feedback, or that the feedback is rejected and why.

9. Commit changes
- If any changes were made, create a commit with a clear message referencing the PR and summarizing the resolution.
- Prompt the user to review and confirm the commit message before finalizing.
- When suggesting or performing a push, use the discovered git remote name.
- Prompt the user to push the commit if they have permissions, or provide instructions if they do not.
- Push before replying, so replies can link to the pushed commit.

10. Reply to all feedback
- Every item of feedback gets a reply, whether it was acted on or rejected. There are no silent dismissals.
- Show the user the complete set of drafted replies and get confirmation before posting anything. Posting is public and hard to undo.
- Reply to each review thread in that thread, linking to the relevant commit or code location where useful.
- Cover all non-thread feedback in exactly one new PR comment, not one comment per item.
  This single comment covers the review-body feedback and Copilot suppressed findings from
  step 3 and the discussion comments from step 4, because none of them has a thread to
  reply in. Group it by source, name the source of each item, list each with its file and
  line where it has one, and give the same changed-or-rejected-and-why treatment each item
  would have received in a thread.
- If no non-thread feedback was found, post no summary comment.
- If a write path is unavailable, output the exact reply text for each target so the user can post it manually.

11. Resolve threads, non-human feedback only
- Resolve a review thread only when every comment in it was authored by a bot, and only after the reply from step 10 succeeded.
- Never resolve a thread that any human participated in. Leave it open so the human can judge the reply and accept or reject it themselves. This holds even when the fix is obviously correct and fully applied.
- A bot-opened thread that a human later commented in counts as human. Treat it as human.
- Decide from the author type recorded in step 2, never from the login alone. If the type is missing or ambiguous for any comment in a thread, treat that thread as human and leave it unresolved.
- Never resolve anything that came from step 3 or step 4; review-body feedback, suppressed findings and discussion comments have no thread and no resolved state.
- Report which threads were resolved and which were deliberately left open, with the reason.
- If resolution is unavailable, list the bot threads that would have been resolved and let the user do it.

## Output Format
1. PR Scope
- Repo
- PR number, title and state, and whether it was given or inferred from the branch
- Access paths used, one line per capability (read, edit, test, commit/push, reply/resolve)
- Capabilities pre-flight found unavailable, and the resulting limits on this run
- Feedback found per source, each stated explicitly including zero counts:
  - Review threads (unresolved)
  - Review bodies (actionable / informational)
  - Copilot suppressed findings
  - Discussion comments (actionable / informational)

2. Review Thread Feedback (Actionable)
- Item: <comment url>
- Source: review thread
- Location: <file>:<line>
- Author: <login> (<bot or human>)
- Request summary: <concise>
- Action taken: <change or rationale>
- Status: Fixed | Needs Clarification | Blocked
- Evidence: <tests/diagnostics>
- Reply posted: <the reply text for this exact comment>
- Thread resolved: <yes, bot-authored | no, human feedback awaiting their response>

3. Review Body Feedback
- Item: <review url or date>
- Source: review body
- Author: <login> (<bot or human>)
- Review state: <APPROVED | CHANGES_REQUESTED | COMMENTED | DISMISSED>
- Request summary: <concise>
- Action taken: <change or rationale>
- Status: Fixed | Needs Clarification | Blocked | Informational
- Evidence: <tests/diagnostics>
- Covered in summary comment: <yes>

4. Copilot Suppressed Feedback (always reported, even when zero)
- Item: suppressed finding <n> of <total>
- Source: review body, Copilot suppressed
- Location: <file>:<line>
- Source review: <review url or date>
- Finding summary: <concise>
- Assessment: <valid, or why rejected>
- Action taken: <change or rationale>
- Status: Fixed | Needs Clarification | Blocked
- Evidence: <tests/diagnostics>
- Covered in summary comment: <yes>

5. Discussion Comments (always inspected)
- Item: <comment url>
- Source: discussion comment
- Author: <login> (<bot or human>)
- Summary: <concise>
- Action taken: <change or rationale, or none required>
- Status: Fixed | Needs Clarification | Blocked | Informational
- Covered in summary comment: <yes>

6. Validation
- Commands run
- Filters used
- Pass/fail summary
- Remaining warnings/errors

7. Final Summary
- Files changed
- Totals by status: fixed, needing clarification, blocked, informational
- Totals by source, so it is visible that every source was inspected
- Replies posted: <thread replies> in threads, plus <0 or 1> summary comment
- Threads resolved, and threads left open for human response
- Steps left to the user because a path was unavailable
- Recommended next step

## Rules
- Do not invent comments; only act on data actually fetched from GitHub.
- Review-thread resolution tracking is authoritative for unresolved state.
- Keep behavior-compatible edits unless feedback explicitly requires change.
- Always inspect all four feedback sources: review threads, review bodies, Copilot suppressed findings, and discussion comments. Reporting zero for a source is a valid outcome; not looking is not.
- Label every reported item with its source, and report a per-source count even when it is zero, so the user can see nothing was skipped.
- Never dismiss a suppressed finding merely because Copilot marked it low confidence; reject it only on its merits, and say why.
- Never treat a discussion comment as non-feedback just because it is not a formal review; judge it on content.
- If a source yields nothing, report that explicitly rather than omitting the section.
- Never act on an inferred PR without confirming it with the user first.
- If auth or permission fails, report the exact failure, the path it failed on, and the minimum required user action.
- Never substitute a different access path for one the user explicitly requested without telling them and getting agreement.
- Record the access paths used in the final report so later runs can prefer them.
- Do not use `set -e` in bash commands or scripts.
- After each terminal step, verify the bash session is still alive; if it died, report it immediately, start a new session, and continue from the last confirmed checkpoint.
- Use the discovered git remote name consistently anywhere a remote is required.
- Do not post generic batch replies; each reply must be tailored to the specific comment content and its exact resolution status. The single summary comment is the one exception, and it must still address each item it covers individually.
- Never resolve a review thread a human participated in, regardless of how complete the fix is. Resolution there is the human's decision to make.
- Treat unknown or ambiguous authorship as human.
- Reply to every item of feedback, including ones you reject; rejections need a reason.
