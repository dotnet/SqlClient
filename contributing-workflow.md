# Contribution Workflow

You can contribute to Microsoft.Data.SqlClient with issues and PRs. Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.

## Suggested Workflow

We use and recommend the following workflow:

1. **Create an issue for your work.**
    - **For all significant changes you intend to make in this repository, an issue is required.** This gives maintainers a chance to review the proposal before implementation begins and prevents wasted effort. Bug fixes and small improvements are generally welcome without a prior issue, but a linked issue helps us triage and prioritize your contribution.
    - Reuse an existing issue on the topic, if there is one.
    - Get agreement from the team and the community that your proposed change is a good one.
    - Suggest [Labels](CONTRIBUTING.md#using-labels) to add for your issue.
    - Clearly state that you are going to take on implementing it, if that's the case. You can request that the issue be assigned to you. Note: The issue filer and the implementer don't have to be the same person.

2. **Create a personal fork of the repository on GitHub** (if you don't already have one).

3. **Create a branch off of main** (`git checkout -b mybranch`).
    - Name the branch so that it clearly communicates your intentions, such as `issue-123` or `githubhandle-issue`.
    - Branches are useful since they isolate your changes from incoming changes from upstream. They also enable you to create multiple PRs from the same fork.

4. **Make and commit your changes.**
    - Please follow our guidance [here](CONTRIBUTING.md#dos-and-donts).

5. **Add new tests corresponding to your change**, if not present and applicable.

6. **Build the repository with your changes.**
    - Make sure that the builds are clean.
    - Make sure that the tests are all passing, including your new tests.

7. **Create a pull request (PR) against the upstream repository's main branch.**
    - Push your changes to your fork on GitHub (if you haven't already).
    - Create a PR to the **main** branch.
    - Describe the changes that you have done in the PR.
    - Suggest [Labels](CONTRIBUTING.md#using-labels) to add to the PR in the description.

### Draft PRs

- It is OK to create your PR as Draft on the upstream repo before the implementation is done.
- This can be useful if you'd like to start the feedback process concurrent with your implementation.
- State that this is the case in the initial PR comment.
- **Keep your PR in Draft until CI validation passes.** The PR pipeline runs without maintainer intervention, so contributors should ensure all checks are green before marking the PR as ready for review. PRs appearing ready for review with failing checks will not be picked up by reviewers.

### Commit Guidelines

- It is OK for your PR to include a large number of commits.
- PRs are merged via the default "Squash and Merge" strategy (see [Merging Pull Requests](#merging-pull-requests)), so your commit history is automatically collapsed into a single commit at merge time.
- If the PR is planned NOT to follow the default squash-and-merge path (for example, when the author requests "Merge and Commit" to preserve a meaningful commit series), you will be asked to squash your commits into one or some appropriately small number of commits before your PR is merged.

## PR - CI Process

The [SqlClient Continuous Integration](https://dev.azure.com/sqlclientdrivers/public/_build) (CI) system will automatically perform the required builds and run tests (including the ones you are expected to run) for PRs. Builds and test runs must be clean.

### CI Requirements

- All builds must pass successfully.
- All tests must pass, including any new tests you've added.
- Code coverage requirements must be met.

If the CI build fails for any reason, the PR issue will be updated with a link that can be used to determine the cause of the failure.

## Troubleshooting

### Common Issues

- **Build failures**: Check the CI logs for specific error messages.
- **Test failures**: Ensure all tests pass locally before pushing.
- **Merge conflicts**:
  - *Before review has started*: Rebase your branch against the latest `main` branch to keep history clean.
  - *After a reviewer has started reviewing*: Do **not** rebase. Rebasing rewrites commit history and makes it impossible for reviewers to tell which commits they have already reviewed, forcing them to start over. Instead, resolve conflicts by merging the latest `main` into your branch (`git merge main`) so that previously reviewed commits remain intact and reviewers can pick up where they left off.

### Getting Help

- Comment on your PR if you're stuck.
- Reach out in [GitHub Discussions](https://github.com/dotnet/SqlClient/discussions) for broader questions.

## Tracking PRs in the GitHub Project

The [SqlClient Board](https://github.com/orgs/dotnet/projects/588) (dotnet org project #588) is the team's triage board for tracking issues and PRs. When a PR is linked to an issue, its progress is tracked through the project's fields described below.

### Project Fields

| Field | Type | Values | Purpose |
|-------|------|--------|---------|
| **Status** | Single select | `To triage`, `Waiting for customer`, `In progress`, `In review`, `In validation`, `Done` | Tracks the current workflow stage of the item |
| **Priority** | Single select | `P0`, `P1`, `P2`, `P3` | Indicates urgency and scheduling priority |
| **Size** | Single select | `XS`, `S`, `M`, `L`, `XL` | Estimates effort/complexity of the work |
| **API Impact** | Single select | `Breaking Change`, `New API`, `None` | Flags whether the change affects public API surface |
| **PM Approved** | Single select | `N/A`, `Pending`, `Approved` | Tracks product manager sign-off for API or behavioral changes |
| **Assignees** | Field | *(GitHub users)* | Who is responsible for the work |
| **Labels** | Field | *(GitHub labels)* | Categorization and area tags |
| **Reviewers** | Field | *(GitHub users)* | Assigned code reviewers |
| **Milestone** | Field | *(GitHub milestones)* | Target release version |
| **Linked pull requests** | Field | *(PR references)* | PRs associated with the issue |
| **Parent issue** | Field | *(issue reference)* | Epic or parent tracking issue |
| **Sub-issues progress** | Field | *(auto-calculated)* | Completion progress of child issues |
| **Comment** | Text | *(free text)* | Additional context or notes |
| **AB-Link** | Text | *(URL)* | Link to Azure Boards work item (if applicable) |

### Status Stages for PRs

PRs (and their linked issues) move through the **Status** field to communicate reviewability and progress:

| Status | What it means for a PR |
|--------|------------------------|
| **To triage** | PR just opened or linked issue is awaiting initial assessment. Maintainers will review scope, assign reviewers, and set priority. |
| **In progress** | Author is actively developing the change. PR may be in Draft state. |
| **In review** | PR is ready for code review. Reviewers are assigned and the author considers the implementation complete. |
| **Waiting for customer** | Review feedback has been given; the PR author needs to respond or push changes. When a reviewer requests changes, they will also apply the **`Author attention needed`** label so the PR is easy to surface in queries and dashboards. Once the author addresses the feedback, they should remove the `Author attention needed` label if permissions allow or comment `/ready` on the PR to remove the label `Author attention needed` and re-engage the reviewers to move the PR back to `In review`. |
| **In validation** | PR is approved and being validated (CI, manual testing, integration checks) before merge. |
| **Done** | PR has been merged and the associated work is complete. |

### Status Transitions

When a reviewer leaves feedback that needs author action, they move the PR to `Waiting for customer` **and** apply the `Author attention needed` label. The author removes the label (and updates Status back to `In review`/`In progress`) once the feedback is addressed.

```
+--------------+       +---------------+       +-------------------------+       +-----------------+       +--------+
|  To triage   |------>|  In progress  |------>|        In review        |------>|  In validation  |------>|  Done  |
+--------------+       +---------------+       +------------+------------+       +-----------------+       +--------+
                              ^                      |            ^
                              |                      | changes    | author addresses
                              |                      | requested  | feedback (minor) +
                              |                      | + label    | removes label
                              |                      v            |
                              |          +------------------------+
                              |          | Waiting for customer    |
                              |          | + Author attention      |
                              |          |   needed (label)        |
                              |          +------------------------+
                              |                      |
                              |                      | major rework needed
                              +----------------------+
```

### Additional Field Usage

- **Priority**: `P0` items are critical fixes that should be reviewed and merged urgently. `P1` items are high priority for the current milestone. `P2`/`P3` items are scheduled as capacity allows.
- **Size**: Helps reviewers estimate review effort. `XS`/`S` PRs should get faster turnaround. `L`/`XL` PRs may need multiple reviewers or phased review.
- **API Impact**: PRs marked `Breaking Change` or `New API` require **PM Approved = Approved** before merge. `ref/` project updates must accompany these PRs.
- **PM Approved**: Set to `Pending` when a PR introduces API changes. Must reach `Approved` before the PR can be merged. `N/A` for internal or non-API changes.

### Guidelines for Contributors

> **Note:** Setting labels and project board fields (Status, Priority, etc.) requires maintainer or triage access. External contributors cannot modify these directly. Instead, suggest labels in your PR description and the team will apply them. If review feedback asks you to "remove the label" or "update Status", a maintainer will handle it if you don't have access—just let the team know in a comment.

1. **Link your PR to an issue** — This ensures the PR appears on the project board and is tracked through the workflow.
2. **Keep Status current** — If you're the author, move your linked issue to `In review` when your PR is ready for feedback.
3. **Respond promptly** — When status is `Waiting for customer` and the **`Author attention needed`** label is applied, the team is blocked on your response. Address the feedback, push the updates, **remove the `Author attention needed` label**, and move Status back to `In review` (or `In progress` for major rework) so reviewers know the PR is ready for another pass. If you are a community contributor, you may post a comment `/ready` on the PR to automate removing the label and re-engaging reviewers on the PR.
4. **Flag API changes early** — Set **API Impact** appropriately so PM review can happen in parallel with code review.
5. **Don't skip validation** — Even after approval, the PR stays in `In validation` until CI passes and any manual verification is complete.

### Guidelines for Reviewers

1. **Signal that author action is required** — When your review requests changes (whether via formal "Request changes" or a comment that requires author follow-up), apply the **`Author attention needed`** label to the PR and move its linked issue to `Waiting for customer`. This pair (label + status) is what the team uses to find PRs that are blocked on the author versus those still awaiting review.
2. **Don't remove the label yourself** — Leave the `Author attention needed` label in place until the author pushes updates addressing the feedback; the author is responsible for removing it when they hand the PR back for review.
3. **Re-review promptly** — Once the author removes the label, the PR status should be updated either by Author (if they have permissions) or the reviewer to `In review`, so it doesn't stall and is picked up for review on time.
4. **Only reviewers resolve their own feedback threads** — A review comment thread should only be resolved by the reviewer who created it, once they are satisfied the feedback has been addressed. Authors should not resolve reviewer threads themselves.

## Stale PR Management

The SqlClient repository uses automated workflows to manage inactive pull requests and maintain repository hygiene.

### Timeline

- **30 days**: PRs marked as stale if no activity.
- **7 days**: Stale PRs automatically closed (37 days total).

### Exemptions

PRs in Draft are **not** marked as stale.

### Preventing Closure

To prevent your PR from being closed:

- Add comments or updates to the PR.
- Push new commits.
- Respond to review feedback.

### If Your PR Gets Closed

Closed stale PRs can be:

- Reopened by the author with updates.
- Replaced with a new PR containing updated changes.

Contributors can convert PRs to draft to prevent auto-closure of important PRs that may appear inactive.

## PR Feedback

Microsoft team and community members will provide feedback on your change. Community feedback is highly valued. You will often see the absence of team feedback if the community has already provided good review feedback.

One or more Microsoft team members will review every PR prior to merge. They will often reply with "LGTM, modulo comments". That means that the PR will be merged once the feedback is resolved. "LGTM" == "looks good to me".

### Feedback Process

- Address all review comments before the PR can be merged.
- Feel free to ask questions if feedback is unclear.
- Push additional commits to address feedback (these will be squashed later).
- When a reviewer leaves feedback that needs your action, they will set Status to `Waiting for customer` and apply the **`Author attention needed`** label. After you push updates, **remove the `Author attention needed` label** or post a comment `/ready` to do so, so the reviewers know the PR is ready for another pass.

## Merging Pull Requests

For contributors with write access:

### Default Merge Strategy

Use ["Squash and Merge"](https://github.com/blog/2141-squash-your-commits) by default for individual contributions unless requested by the PR author.

Do so, even if the PR contains only one commit. It creates a simpler history than "Create a Merge Commit".

### When to Use "Merge and Commit"

PR authors may request "Merge and Commit" for reasons including (but not limited to):

- The change is easier to understand as a series of focused commits.
- Each commit in the series must be buildable so as not to break `git bisect`.
- Contributor is using an e-mail address other than the primary GitHub address and wants that preserved in the history.
- Contributor must be willing to squash the commits manually before acceptance.
