# GitHub mechanics: read and post

Commands for collecting PR evidence and posting a review. Use them only when the
host authorizes shell and the needed GitHub scope. They do not grant permission
to post; see [publication gates](reporting.md#automated-publication-gates).
Replace `<N>` with the PR number. Commands assume `dotnet/SqlClient`; read the
target repository from the PR URL when it differs.

## Read PR evidence

### Metadata and diff

```bash
R=repos/dotnet/SqlClient
gh pr view <N> -R dotnet/SqlClient \
  --json title,body,author,state,isDraft,baseRefName,baseRefOid,headRefName,headRefOid,changedFiles,commits
gh pr diff <N> -R dotnet/SqlClient --name-only
```

Do not assume `main`; PRs also target `release/*`, `feat/*`, and `dev/*`.
Review the net diff from the merge base. Stacked PRs and merges from the base
branch make per-commit stats misleading: scaffolding added and removed inside
the same PR is not a removal.

When unchanged code decides a finding (callers, the other pool implementation,
the `#if NETFRAMEWORK` or `.unix.cs` variant), read the PR in a separate worktree
so local work is untouched:

```bash
git fetch origin <base>
git fetch origin pull/<N>/head
git worktree add --detach ../SqlClient-pr<N>-review FETCH_HEAD
cd ../SqlClient-pr<N>-review
BASE=$(git merge-base origin/<base> HEAD)
git diff --stat $BASE..HEAD
```

Remove the worktree when done. Never run PR build hooks or tests in a job that
holds publishing credentials.

### Existing feedback: all three endpoints

Reviewer names are not review content. Save raw JSON in a private session artifact
directory so source links, IDs, reply linkage, and reviewed commits are retained.
Set `EVIDENCE_DIR` to that existing directory before these commands:

```bash
gh api --paginate --slurp $R/pulls/<N>/comments > "$EVIDENCE_DIR/inline.json"
gh api --paginate --slurp $R/pulls/<N>/reviews > "$EVIDENCE_DIR/reviews.json"
gh api --paginate --slurp $R/issues/<N>/comments > "$EVIDENCE_DIR/toplevel.json"
jq -r '.[][] | "--- \(.html_url) id=\(.id) reply_to=\(.in_reply_to_id // "none") review=\(.pull_request_review_id // "none") commit=\(.commit_id // "none") original_commit=\(.original_commit_id // "none")\n\(.user.login) [\(.user.type)] \(.path // ""):\(.line // .original_line // "") \(.state // "")\n\(.body)"' \
  "$EVIDENCE_DIR/inline.json" "$EVIDENCE_DIR/reviews.json" "$EVIDENCE_DIR/toplevel.json" \
  > "$EVIDENCE_DIR/feedback.txt"
```

- Review bodies hold Copilot's suppressed low-confidence findings inside a
  collapsed `<details>` block. They never appear as inline comments.
- Author rebuttals often sit in top-level comments, not in the thread.
- Before drafting a finding, search the saved files for the affected symbol.
  One search usually finds the thread that already settled it.

Thread resolution state exists only in GraphQL. Connections cap at 100 nodes
per page without warning. `--paginate` needs `$endCursor` and
`pageInfo{hasNextPage endCursor}`, or it stops at the first page.

```bash
gh api graphql --paginate -F owner=dotnet -F repo=SqlClient -F n=<N> -f query='
  query($owner:String!,$repo:String!,$n:Int!,$endCursor:String){
    repository(owner:$owner,name:$repo){pullRequest(number:$n){
      reviewThreads(first:100,after:$endCursor){
        pageInfo{hasNextPage endCursor}
        nodes{id isResolved isOutdated path line
          comments(first:100){totalCount pageInfo{hasNextPage endCursor}
            nodes{id databaseId url replyTo{id} commit{oid} originalCommit{oid} author{login} body}}}}}}}' \
  > "$EVIDENCE_DIR/threads.json"
```

Outer pagination does not page nested comments. For each thread whose comments
have `hasNextPage: true`, fetch all its comments by node ID:

```bash
gh api graphql --paginate -F thread=<THREAD_NODE_ID> -f query='
  query($thread:ID!,$endCursor:String){
    node(id:$thread){... on PullRequestReviewThread{
      comments(first:100,after:$endCursor){
        pageInfo{hasNextPage endCursor}
        nodes{id databaseId url replyTo{id} commit{oid} originalCommit{oid} author{login} body}}}}}' \
  > "$EVIDENCE_DIR/thread-<THREAD_NODE_ID>-comments.json"
```

Check each call succeeded and inspect its output before using it. Keep thread
state separate from whether the head addresses the concern; neither REST
comments nor a reply alone proves resolution. Remove temporary evidence after
the review, or retain it only under the host's private-artifact policy.

### CI and coverage

```bash
gh pr checks <N> -R dotnet/SqlClient
```

- Driver CI runs in Azure DevOps (for example `PR-SqlClient-Project`,
  `PR-SqlClient-Package`, `sqlclient-pr`) and reports back as checks. CodeQL and
  milestone checks run as GitHub Actions. Check names change; read them from
  the PR rather than this list.
- PR pipelines are path filtered. Changes only under `.github/`, `policy/`, or
  root Markdown files do not trigger driver CI. Read the `pr:` `paths` block in
  `eng/pipelines/` before reporting a missing run.
- When a `codecov[bot]` comment exists, read it before raising coverage. The
  diff-coverage target is in `azurepipelines-coverage.yml`. Its absence is not
  a CI failure on its own.

## Post a review

### Shape

Use the host's designated review channel first. A pending-review staging tool
has not published a review. When direct API posting is permitted,
`gh pr review --comment --body-file` posts only a top-level body; inline
comments need the reviews API:

```bash
gh api $R/pulls/<N>/reviews -X POST --input review.json
```

```jsonc
{
  "commit_id": "<reviewed head SHA>",
  "event": "COMMENT",
  "body": "<summary from reporting.md>",
  "comments": [
    { "path": "src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnection.cs",
      "line": 634, "side": "RIGHT", "body": "<finding>" },
    { "path": "...", "start_line": 120, "start_side": "RIGHT",
      "line": 128, "side": "RIGHT", "body": "<multi-line finding>" }
  ]
}
```

- `event` is `COMMENT`. Never `APPROVE`. Use `REQUEST_CHANGES` only with
  explicit configured authority; an automated approval can satisfy branch
  protection.
- Set `commit_id` to the head SHA you reviewed. Recheck it right before posting.
- On your own PR, `COMMENT` reviews with inline comments are allowed. Do not
  fold inline findings into one top-level comment.

### Anchoring

- Inline comments must land on lines in the diff, or the API returns `422`.
  List every hunk before choosing a line:

  ```bash
  git diff $BASE..HEAD -- <file> | grep -n '^@@'
  ```

  `@@ -115,21 +143,206 @@` covers new-side lines 143-348. A line outside every
  hunk is not anchorable; put that finding in the summary.
- Use `side: RIGHT` for added or context lines, `LEFT` only for removed lines.
- A `422` on one comment fails the whole batch. Fix the anchor and resend the
  batch once; do not send comments one by one.

### Body text

- Remove hard line wraps inside paragraphs before posting so GitHub can wrap
  text. Wrapped text is fine while previewing in chat.
- Use repository-relative paths and backticks for identifiers.
- Do not include secrets, connection strings with credentials, customer data,
  or private logs. Suspected vulnerabilities go to MSRC, not the PR.

### Verify delivery

The comments endpoint pages at 30. Without `--paginate` a fresh review can look
missing.

```bash
gh api --paginate $R/pulls/<N>/comments \
  -q '.[] | select(.pull_request_review_id==<REVIEW_ID>) | "\(.path):\(.line)"'
```

Match the count to what you sent. If a write timed out, read remote state
before retrying so the batch is not duplicated.

### After posting

Do not resolve threads you did not open, dismiss reviews, change labels, or
merge. Follow-up reviews reply in existing threads instead of reposting.
