# Contribution Workflow

You can contribute to Microsoft.Data.SqlClient with issues and PRs. Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.

## Suggested Workflow

We use and recommend the following workflow:

1. **Create an issue for your work.**
    - You can skip this step for trivial changes.
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

### Commit Guidelines

- It is OK for your PR to include a large number of commits.
- Once your change is accepted, you will be asked to squash your commits into one or some appropriately small number of commits before your PR is merged.

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
- **Merge conflicts**: Rebase your branch against the latest main branch.

### Getting Help

- Comment on your PR if you're stuck.
- Reach out in [GitHub Discussions](https://github.com/dotnet/SqlClient/discussions) for broader questions.

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
