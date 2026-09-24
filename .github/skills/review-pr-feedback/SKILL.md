---
name: review-pr-feedback
description: Collects PR feedback from every source — review threads, review bodies including Copilot's hidden low-confidence suppressed findings, and discussion comments — applies fixes, replies to every item, and resolves only bot-authored threads. Infers the PR from the current branch when none is given, and asks for explicit approval before committing, pushing, replying or resolving. Works through whichever GitHub access path is available (gh CLI, GitHub MCP server, or other). Invoke explicitly with /review-pr-feedback.
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
| **Pre-approved actions** | Only the gated actions the request explicitly names as not needing approval, for example "commit without asking". Default none. See Approvals. |

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
| Workspace is on the PR's branch | Steps 6, 9 | Confirm the checked-out branch is the PR's head branch, and that the workspace can reach the head repository, which is a fork whenever the PR comes from one. Never assume the workspace is already on the right branch: a mismatch means every edit, commit and push would land on whatever branch happens to be checked out. |
| Authorship of the PR itself | Steps 6, 10 | Determine whether the authenticated user is the PR's author. Acting on someone else's PR is a different posture; see step 1. |
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

## Approvals

Four actions change something outside this workspace and are gated. Each one needs the
user's explicit approval, every time:

| Gated action | Step | Show before asking |
| --- | --- | --- |
| Commit | Step 9 | The exact commit message and the list of files it covers |
| Push | Step 9 | The remote name and branch, and the commits being pushed |
| Reply | Step 10 | The full text of every reply, and where each one will be posted |
| Resolve | Step 11 | The list of threads to resolve, each with its author and why it qualifies as bot-authored |

How approval works:

- Ask separately for each action. Approval of one is never approval of another. Agreeing
  to a commit does not authorise a push; approving reply text does not authorise
  resolving the threads those replies were posted to.
- Present the exact content first, then ask. Never ask for approval of something the user
  has not been shown in full.
- Only an unambiguous yes to the action you asked about counts. Treat silence, a question,
  a partial answer, a "looks good" about something else, or any uncertainty as not
  approved.
- Not approved means do not perform the action. Say so plainly in the report, leave the
  work in place for the user, and carry on with the other steps that are still valid.
- Approval covers exactly what was shown. If the content changes afterwards — replies
  reworded, another commit added, the thread list grown — the earlier approval is void and
  you must ask again.

Skipping an approval gate:

- The only reason to skip a gate is that the user explicitly asked for that action to
  proceed without approval, for example "commit without asking" or "post the replies, no
  need to check with me".
- A blanket instruction covers only the actions it actually names. "Commit without asking"
  says nothing about pushing, replying or resolving; those stay gated.
- Standing approval does not accumulate across turns. An approval given earlier in the
  conversation, or in a previous run of this skill, applies to that action then, not to
  this one now. Reuse it only when the user has clearly said it should carry forward, such
  as "for the rest of this session, never ask before replying".
- When in doubt about whether an instruction was meant as standing approval, ask.
- Record in the report which gates were approved, which were pre-approved by explicit
  instruction, and which were declined.

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
- Check the workspace against the PR before planning any edit:
  - Compare the checked-out branch with the PR's head branch, and the workspace's remotes
    with the PR's head repository. A PR from a fork needs that fork reachable.
  - If they do not match, say so and stop. Offer either to switch the workspace to the
    PR's branch, or to continue in analysis-only mode with steps 6 and 9 skipped.
  - Never switch branches without the user agreeing. Changing their checked-out branch,
    or applying edits to the wrong one, is exactly the kind of damage the approval gates
    exist to prevent.
  - Say plainly which mode the run is in, because analysis-only changes what the later
    steps can deliver.
- Establish whether the authenticated user is the PR's author:
  - When they are, this is the normal case: fix the feedback and reply as the author.
  - When they are not, this is someone else's PR. Default to analysis and advice: draft
    everything, but do not post replies unless the user explicitly asks, because comments
    arrive under their name on another person's work. Never assume a drive-by reply is
    wanted.
  - Report which case applies.

2. Gather review thread feedback
- Query the PR's review threads through the validated read path.
- Keep only unresolved threads where isResolved is false.
- Extract thread id, file path, line/startLine, comment url, author login, and body.
- Also record, for every comment in the thread, whether its author is a bot or a human,
  using the author type field rather than the login. Step 11 depends on this.
- Mark a thread as author commentary when the PR's own author opened it. Authors routinely
  annotate their own diff to walk reviewers through a change, and those threads are
  explanation, not requests. A reply from the author inside someone else's thread is not
  commentary; that is a response to feedback.
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
- Mark a body written by the PR's own author as author commentary. A review the author
  submits on their own PR is a walkthrough for reviewers, often a short framing note such
  as "Comments to aid review" attached to a set of explanatory inline comments.
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
- Separate operational noise from feedback before classifying. Pipeline commands such as
  `/azp run`, CI status posts, coverage reports, and stale-bot notices are not feedback.
  Count them, say how many you set aside, and exclude them from planning and from replies.
  Replying to a build-status post is worse than not replying at all.
- A human's response to operational noise can still be feedback; judge it on its own
  content rather than on what it replies to.
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
- Merge duplicates across sources. The same request often arrives twice, for example as a
  review body and again as a discussion comment. Combine them into one item that lists
  every source it came from, then plan and fix it once. Do not count it twice, and do not
  write two separate answers to the same question.
- Check whether each item is already addressed before planning any edit. Feedback is often
  fixed by the PR author, or by a later commit, while the thread stays open. Compare the
  request against the current state of the code on the PR's head. If it is already
  handled, record it as Already Addressed with that evidence and plan no change.
- Leave author commentary out of the planned work. Read it first, though: it usually
  explains why the code looks the way it does, and that context often changes how other
  feedback should be addressed.
- Ask the user to confirm the plan before proceeding, showing a concise summary of proposed changes and rationale, with each item's source shown.

6. Implement and verify
- Skip this step entirely in analysis-only mode, and say so rather than editing the wrong branch.
- Apply required code or test updates with smallest safe change set.
- Run targeted checks first.
- If a test scope was given, use the `generate-mstest-filter` skill to build a focused filter for it.
- Collect diagnostics when tests cannot run.

7. Classify each item
- Fixed: change implemented and validated in this run.
- Already Addressed: the request was satisfied before this run, by the PR author or a later commit. Cite the evidence, usually a commit or the current state of the code. Never report this as Fixed; claiming someone else's work is both wrong and misleading about what this run did.
- Author Commentary: the PR's author explaining their own change to reviewers, through a thread they opened on their own diff or a review body on their own PR. Not actionable by default: it answers questions rather than asking them, and there is nothing to fix, reply to or resolve.
- Needs Clarification: ambiguous, conflicting, or insufficiently specified.
- Blocked: external dependency, permission, or missing context.
- Informational: captured only, with no change required.
- Promote author commentary out of that category only when it genuinely asks for something: an open question put to reviewers, a flagged TODO, or a decision the author says they want challenged. Say why you promoted it, and classify it normally from then on.
- Tag every item with its source or sources from the Feedback sources table, and for review threads whether the authorship is bot or human. This determines where its reply goes in step 10 and whether it may be resolved in step 11.

8. Produce a final report
- Give review threads, review bodies, Copilot suppressed findings and discussion comments their own sections, and label every item with its source.
- Include evidence for each item: file location, change summary, validation result.
- Draft a distinct reply for every item of feedback that this run acted on, rejected, or needs something from the reviewer for, addressing that exact item's request, context, and outcome.
- Every reply must state plainly either what was changed to address the feedback, or that the feedback is rejected and why.
- Do not draft replies for items classified Already Addressed or Author Commentary, for operational noise, or for a duplicate already answered through another source. A thread whose request was satisfied by someone else is waiting on its reviewer, and another comment adds nothing. Answering the author's own explanation of their own code adds less.

9. Commit changes
- Skip this step entirely in analysis-only mode; there is nothing committable and the branch is not the PR's.
- Commit and push are two separate gated actions. See Approvals.
- If any changes were made, draft a commit message that references the PR and summarizes the resolution.
- Show the user the exact message and the files it covers, then ask for approval to commit. Do not commit until they approve.
- Ask separately for approval to push, showing the discovered remote name, the branch and the commits involved. Approval to commit is not approval to push.
- If push is declined or unavailable, leave the commit local and tell the user the exact command to push it themselves.
- Push before replying where possible, so replies can link to the pushed commit. If the push was declined, say so in the replies rather than linking to a commit the reviewer cannot see.

10. Reply to all feedback
- Every item of feedback this run engaged with gets a reply, whether it was acted on or rejected. There are no silent dismissals. Items classified Already Addressed or Author Commentary, operational noise, and duplicates answered elsewhere are excluded by step 8.
- When the authenticated user is not the PR's author, draft the replies but do not post them unless the user explicitly asks. See step 1.
- Posting replies is a gated action. See Approvals.
- Show the user the complete set of drafted replies, each with its destination, and ask for approval to post them. Posting is public and hard to undo.
- Do not post anything until approval is given. If the user approves some replies and not others, post only the approved ones and record the rest as withheld.
- If you change any reply text after approval, ask again for the changed ones.
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
- Resolving is a gated action, separate from the reply gate. See Approvals.
- Work out which threads qualify: a thread qualifies only when every comment in it was authored by a bot, and only after its reply from step 10 was posted successfully.
- Show the user that list, each entry with its author and the reason it qualifies, and ask for approval to resolve. Approval of the replies in step 10 does not authorise this.
- Never resolve a thread that any human participated in. Leave it open so the human can judge the reply and accept or reject it themselves. This holds even when the fix is obviously correct and fully applied, and it holds even if the user approves the resolve gate — approval cannot promote a human thread into a resolvable one.
- A bot-opened thread that a human later commented in counts as human. Treat it as human.
- Decide from the author type recorded in step 2, never from the login alone. If the type is missing or ambiguous for any comment in a thread, treat that thread as human and leave it unresolved.
- Never resolve anything that came from step 3 or step 4; review-body feedback, suppressed findings and discussion comments have no thread and no resolved state.
- Author commentary threads are human-authored and so are never resolvable here, including when the user is the PR's author. Closing your own explanatory note is the author's own call to make outside this skill.
- Report which threads were resolved and which were deliberately left open, with the reason.
- If resolution is unavailable, list the bot threads that would have been resolved and let the user do it.

## Output Format
1. PR Scope
- Repo
- PR number, title and state, and whether it was given or inferred from the branch
- Run mode: full, or analysis-only with the reason
- Whether the authenticated user is the PR's author
- Access paths used, one line per capability (read, edit, test, commit/push, reply/resolve)
- Capabilities pre-flight found unavailable, and the resulting limits on this run
- Feedback found per source, each stated explicitly including zero counts:
  - Review threads (unresolved), and how many of those are author commentary
  - Review bodies (actionable / informational / author commentary)
  - Copilot suppressed findings
  - Discussion comments (actionable / informational / operational noise set aside)
- Items merged as duplicates across sources

2. Review Thread Feedback (Actionable)
- Item: <comment url>
- Source(s): review thread (list every source if it arrived more than once)
- Location: <file>:<line>
- Author: <login> (<bot or human>)
- Request summary: <concise>
- Action taken: <change or rationale>
- Status: Fixed | Already Addressed | Author Commentary | Needs Clarification | Blocked
- Evidence: <tests/diagnostics>
- Reply posted: <the reply text for this exact comment>
- Thread resolved: <yes, bot-authored | no, human feedback awaiting their response>

3. Review Body Feedback
- Item: <review url or date>
- Source(s): review body
- Author: <login> (<bot or human>)
- Review state: <APPROVED | CHANGES_REQUESTED | COMMENTED | DISMISSED>
- Request summary: <concise>
- Action taken: <change or rationale>
- Status: Fixed | Already Addressed | Author Commentary | Needs Clarification | Blocked | Informational
- Evidence: <tests/diagnostics>
- Covered in summary comment: <yes | no, and why not>

4. Copilot Suppressed Feedback (always reported, even when zero)
- Item: suppressed finding <n> of <total>
- Source(s): review body, Copilot suppressed
- Location: <file>:<line>
- Source review: <review url or date>
- Finding summary: <concise>
- Assessment: <valid, or why rejected>
- Action taken: <change or rationale>
- Status: Fixed | Already Addressed | Author Commentary | Needs Clarification | Blocked
- Evidence: <tests/diagnostics>
- Covered in summary comment: <yes | no, and why not>

5. Discussion Comments (always inspected)
- Item: <comment url>
- Source(s): discussion comment
- Author: <login> (<bot or human>)
- Summary: <concise>
- Action taken: <change or rationale, or none required>
- Status: Fixed | Already Addressed | Author Commentary | Needs Clarification | Blocked | Informational
- Covered in summary comment: <yes | no, and why not>

6. Validation
- Commands run
- Filters used
- Pass/fail summary
- Remaining warnings/errors

7. Final Summary
- Files changed
- Totals by status: fixed, already addressed, author commentary, needing clarification, blocked, informational
- Totals by source, so it is visible that every source was inspected
- Approvals: commit, push, reply, resolve — each marked approved, pre-approved by explicit instruction, declined, or not reached
- Replies posted: <thread replies> in threads, plus <0 or 1> summary comment
- Threads resolved, and threads left open for human response
- Actions left undone because approval was withheld, and how the user can complete them
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
- Commit, push, reply and resolve each require the user's explicit approval for that specific action, every time. See Approvals.
- Never treat approval of one action as approval of another, and never carry an approval across turns or runs unless the user clearly said it should carry forward.
- Treat anything short of an unambiguous yes as a no, and never perform a gated action the user was not shown in full beforehand.
- If auth or permission fails, report the exact failure, the path it failed on, and the minimum required user action.
- Never substitute a different access path for one the user explicitly requested without telling them and getting agreement.
- Record the access paths used in the final report so later runs can prefer them.
- Do not use `set -e` in bash commands or scripts.
- After each terminal step, verify the bash session is still alive; if it died, report it immediately, start a new session, and continue from the last confirmed checkpoint.
- Use the discovered git remote name consistently anywhere a remote is required.
- Do not post generic batch replies; each reply must be tailored to the specific comment content and its exact resolution status. The single summary comment is the one exception, and it must still address each item it covers individually.
- Never resolve a review thread a human participated in, regardless of how complete the fix is. Resolution there is the human's decision to make.
- Treat unknown or ambiguous authorship as human.
- Reply to every item of feedback this run engaged with, including ones you reject; rejections need a reason.
- Never claim credit for a fix someone else made; that is what Already Addressed is for.
- Do not treat the PR author's explanation of their own change as a request. Read it for context, report it as Author Commentary, and act on it only when it actually asks for something.
- Never edit, commit or push while the workspace is on a branch other than the PR's.
- Do not treat pipeline commands, CI status, coverage reports or stale-bot notices as feedback.
- Count an item once when it arrives through several sources, listing every source it came from.
