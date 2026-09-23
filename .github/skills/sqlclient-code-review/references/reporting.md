# Reporting and publication

## Finding format

One root cause per finding. Use a short imperative title, an exact file/range,
and two to four plain-language sentences: trigger, consequence, evidence, and
correction. Anchor to the smallest relevant changed range; cite another location
in the body when that is needed to establish the call path. Do not invent line
numbers or attach a comment to unrelated code.

| Priority | Meaning |
| --- | --- |
| P1 | Urgent: a reachable change can cause data corruption, persistent hangs, severe resource loss, or break a common supported scenario. State the conditions; do not assume universal impact. |
| P2 | Normal: a concrete correctness or compatibility defect in a supported scenario that should be fixed. |
| P3 | Low: a demonstrated, limited-impact defect worth correcting; not a style preference. |

Severity follows impact and reachability, not the number of files changed.
Escalate release-critical issues to maintainers; do not infer release-blocking
authority. Suspected security vulnerabilities follow the private MSRC process in
`CONTRIBUTING.md`. Do not publish exploit details, secrets, or sensitive payloads.

## Comment examples

These are invented calibration scenarios, not findings about current code.
Use them only if the actual diff and call path establish the stated facts.

**Useful ownership finding**

> [P1] Retain the buffer until the send completes
>
> When the send is pending, this return makes the buffer available to another
> renter while the transport still reads it. That can replace bytes in the
> outgoing packet. Return the buffer after send completion, including the
> faulted and canceled paths.

**Useful compatibility finding**

> [P2] Keep the existing public overload
>
> Replacing this method with a signature that adds an optional parameter removes
> the entry point used by already-compiled callers. Those applications will fail
> with `MissingMethodException` after upgrading. Keep the original overload and
> delegate to the new one.

**Useful coverage request, not a proven implementation bug**

> The new test checks that cancellation completes, but not what happens to the
> physical connection afterward. Add a deterministic check that it is either
> safely reused for a successful command or discarded when unusable. Opening
> another connection with the same pool key alone can pass by using a different
> physical connection.

**Do not publish**

> This might have a race. Consider making it thread-safe.

No interleaving, violated contract, or consequence has been established. Likewise,
avoid "use Span for performance" without evidence or "add more tests" without a
specific uncovered behavior.

## Review summary

Lead with findings, ordered by priority. Include a short scope/limitations note:
reviewed revision or local comparison, any unreviewed surfaces, and material
execution or coverage gaps. Say "No actionable findings in the reviewed scope"
only when no new or previously reported actionable defects remain in that scope;
do not equate that with approval or proof of correctness. On follow-up reviews,
distinguish new findings, existing concerns still present, and fixes verified
in the current code. Link existing threads instead of reposting their findings.
If the review covers only new commits, say so; unexamined earlier findings must
not be described as fixed or absent.

Label test status accurately: executed and passed/failed, inspected only, skipped,
or not run with a reason. Keep unresolved questions and coverage requests separate
from proven defects. Do not dump internal reasoning, all rejected candidates,
generic praise, a full risk checklist, or an unsupported merge verdict.

## Automated publication gates

1. **Confirm authorization and channel.** Publish only when the user or trusted
   automation configuration requests it and the host grants an appropriate write
   channel. Otherwise return a draft. Prefer the host's designated review/safe-output
   mechanism; do not work around a restricted channel with another API. A tool
   that stages a pending review has not published it.
2. **Keep execution separate.** Review untrusted PR content read-only. Run builds,
   tests, or scripts only in an authorized isolated environment without publishing
   credentials or production secrets. A privileged review job must not execute PR
   code, including build hooks. Missing infrastructure is a limitation, not a pass.
3. **Check completeness and freshness.** Before sending, reread the PR's base/head
   SHAs and open state. If it is closed or merged, stop publication and return the
   draft with that limitation. If either SHA changed, recompute the diff and revalidate
   findings and anchors; do not post the stale batch. A truncated or partial review
   must be labeled as partial. Bind submission to the reviewed head SHA where supported.
4. **Deduplicate.** Read existing comments/reviews, including earlier bot runs and
   human replies. Compare root cause, affected symbol/path, and scenario, not only
   line numbers. Re-runs must not repost an unchanged concern. A resolved concern
   needs new evidence before it is raised again. Deduplication suppresses duplicate
   comments, not outstanding defects: link still-applicable concerns in the summary,
   and do not treat a thread's resolved status as proof that the code was fixed.
5. **Publish one coherent review.** Default to a non-approving `COMMENT` review
   through the permitted channel, with substantive inline findings on valid diff
   lines and a compact summary. A request-changes event requires explicit configured
   authority; do not submit approvals. If no valid inline anchor exists, put the
   supported finding in the summary rather than inventing one.
6. **Handle empty and uncertain results.** Use the automation's configured summary
   policy; at most one no-findings summary per reviewed revision, with no empty
   inline comments. No new findings after deduplication does not mean no outstanding
   findings. Keep unproven concerns out of inline defect findings. Important coverage
   requests or verification gaps can appear in the summary without asserting that
   the driver is broken.
7. **Verify delivery.** Confirm returned review/comment identifiers and the submitted
   revision. If a write times out or only partially succeeds, inspect remote state
   before retrying; do not duplicate the batch. If permissions or tools prevent
   publication, retain the draft and report that it was not posted.
8. **Leave decisions to reviewers.** Publication does not authorize merging, label
   changes, review dismissal, code edits, or resolution of human review threads.
   User-requested fixes are a separate implementation phase, not a publishing
   action. Follow-up review should recheck the current code without marking another
   reviewer's concern resolved.
