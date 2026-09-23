# Hotfix Label Automation

Three workflows work together to automate backporting a fix from an issue
through to a merged cherry-pick PR, all keyed off a single `Hotfix X.Y.Z`
label.

```mermaid
flowchart TD
    A["Maintainer labels an issue<br/><b>Hotfix 7.1.1</b>"] --> B["<b>hotfix-label-issue.yml</b><br/>(on: issues.labeled)"]
    B --> B1["create-backport-issue.sh"]
    B1 --> B2["Creates child issue<br/>'[7.1.1] &lt;title&gt;'<br/>milestoned 7.1.1<br/>+ linked as sub-issue"]

    C["Contributor opens a PR<br/>with 'Fixes #&lt;parent issue&gt;'"] --> D["<b>sync-hotfix-label-to-pr.yml</b><br/>(on: pull_request_target)"]
    D --> D1["sync-hotfix-label-to-pr.sh"]
    D1 --> D2{"Does the closed issue<br/>carry a Hotfix X.Y.Z label?"}
    D2 -- yes --> D3["Copies 'Hotfix 7.1.1'<br/>label onto the PR"]
    D2 -- no --> D4["No-op"]

    E["PR merges to main<br/>(with Hotfix label present)"] --> F["<b>cherry-pick-hotfix.yml</b><br/>(on: pull_request_target<br/>closed / labeled)"]
    F --> F1["cherry-pick-to-release.sh"]
    F1 --> F2["Cherry-picks the merge commit<br/>onto release/7.1"]
    F1 --> F3["lookup_backport_issue():<br/>finds the sub-issue (B2)<br/>milestoned 7.1.1"]
    F2 --> F4["Opens '[7.1.1 Cherry-pick] &lt;title&gt;' PR<br/>targeting release/7.1<br/>body: 'Fixes #&lt;backport issue&gt;'"]
    F3 --> F4

    B2 -.->|"sub-issue provides<br/>the link target"| F3
    D3 -.->|"label makes the merged PR<br/>eligible for cherry-pick"| F

    style B2 fill:#e6f3ff
    style D3 fill:#e6f3ff
    style F4 fill:#e6f3ff
```

## Steps

1. **[`hotfix-label-issue.yml`](hotfix-label-issue.yml)** fires when an issue
   receives a `Hotfix X.Y.Z` label. It runs
   [`create-backport-issue.sh`](../scripts/create-backport-issue.sh), which
   creates a child "backport issue" titled `[X.Y.Z] <original title>`,
   milestones it to `X.Y.Z`, and links it as a native GitHub sub-issue of the
   parent. If the label is later removed and re-added (or the workflow
   re-runs), a duplicate-guard skips creating a second backport issue for the
   same milestone.

2. **[`sync-hotfix-label-to-pr.yml`](sync-hotfix-label-to-pr.yml)** fires when
   a pull request is opened, reopened, edited, or synchronized. It runs
   [`sync-hotfix-label-to-pr.sh`](../scripts/sync-hotfix-label-to-pr.sh),
   which inspects the PR's closing keywords (`Fixes`/`Closes`/`Resolves #N`)
   and, for any referenced issue in this repository that carries a
   `Hotfix X.Y.Z` label, copies that label onto the PR itself. This means a
   contributor never has to remember to label the PR by hand: the parent
   issue's label is enough.

3. **[`cherry-pick-hotfix.yml`](cherry-pick-hotfix.yml)** fires when a labeled
   PR is merged (or a `Hotfix X.Y.Z` label is added after merge). It runs
   [`cherry-pick-to-release.sh`](../scripts/cherry-pick-to-release.sh), which
   cherry-picks the merge commit onto `release/X.Y` and opens a
   `[X.Y.Z Cherry-pick] <title>` PR. `lookup_backport_issue()` looks up the
   sub-issue created in step 1 and appends a `Fixes #<backport issue>` line
   to the cherry-pick PR body, so merging the cherry-pick automatically
   closes the backport issue tracking that release.

## Why sub-issues (not just linking)

Using GitHub's native sub-issue relationship (rather than a plain
cross-reference) lets the backport issue be discovered purely from the
parent issue's sub-issue list at cherry-pick time, without depending on the
PR body text, comment history, or any other stateful link that could be
edited or go missing.
