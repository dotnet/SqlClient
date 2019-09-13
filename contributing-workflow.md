# Contribution Workflow

You can contribute to Microsoft.Data.SqlClient with issues and PRs. Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.

## Suggested Workflow

We use and recommend the following workflow:

1. Create an issue for your work. 
    - You can skip this step for trivial changes.
    - Reuse an existing issue on the topic, if there is one.
    - Get agreement from the team and the community that your proposed change is a good one.
    - If your requested change needs a new public API, add a Label "**Public API**" to your issue.
    - If your requested change address performance improvement in the driver, add a Label "**Performance**" to your issue.
    - For any other improvements in the driver, add a Label "**Enhancement**" to your issue.
    - Clearly state that you are going to take on implementing it, if that's the case. You can request that the issue be assigned to you. Note: The issue filer and the implementer don't have to be the same person.
2. Create a personal fork of the repository on GitHub (if you don't already have one).
3. Create a branch off of master (`git checkout -b mybranch`). 
    - Name the branch so that it clearly communicates your intentions, such as issue-123 or githubhandle-issue. 
    - Branches are useful since they isolate your changes from incoming changes from upstream. They also enable you to create multiple PRs from the same fork.
4. Make and commit your changes.
    - Please follow our [Commit Messages](contributing.md#commit-messages) guidance.
5. Add new tests corresponding to your change, if nor present and applicable.
6. Build the repository with your changes.
    - Make sure that the builds are clean.
    - Make sure that the tests are all passing, including your new tests.
7. Create a pull request (PR) against the upstream repository's **master** branch.
    - Push your changes to your fork on GitHub (if you haven't already).

> Note: It is OK for your PR to include a large number of commits. Once your change is accepted, you will be asked to squash your commits into one or some appropriately small number of commits before your PR is merged.

> Note: It is OK to create your PR as "[WIP]" on the upstream repo before the implementation is done. This can be useful if you'd like to start the feedback process concurrent with your implementation. State that this is the case in the initial PR comment.

## PR - CI Process

_[Coming Soon - Currently under development]_

The [SqlClient Continuous Integration](https://dev.azure.com/sqlclientdrivers-ci/sqlclient/) (CI) system will automatically perform the required builds and run tests (including the ones you are expected to run) for PRs. Builds and test runs must be clean.

If the CI build fails for any reason, the PR issue will be updated with a link that can be used to determine the cause of the failure.

## PR Feedback

Microsoft team and community members will provide feedback on your change. Community feedback is highly valued. You will often see the absence of team feedback if the community has already provided good review feedback. 

1 or more Microsoft team members will review every PR prior to merge. They will often reply with "LGTM, modulo comments". That means that the PR will be merged once the feedback is resolved. "LGTM" == "looks good to me".

## Merging Pull Requests (for contributors with write access)

Use ["Squash and Merge"](https://github.com/blog/2141-squash-your-commits) by default for individual contributions unless requested by the PR author.

Do so, even if the PR contains only one commit. It creates a simpler history than "Create a Merge Commit". Reasons that PR authors may request "Merge and Commit" may include (but are not limited to):
  - The change is easier to understand as a series of focused commits. Each commit in the series must be buildable so as not to break `git bisect`.
  - Contributor is using an e-mail address other than the primary GitHub address and wants that preserved in the history. Contributor must be willing to squash the commits manually before acceptance.

## PR Verification and Validation

For testing PR changes and ensure they work fine, we maintain a public feed that lists NuGet Packages built with merged commits.

**Link to Public Feed:** [Microsoft.Data.SqlClient.Commits](https://pkgs.dev.azure.com/sqlclientdrivers-ci/sqlclient/_packaging/Microsoft.Data.SqlClient.Commits/nuget/v3/index.json)

**Add this feed to NuGet Sources**

```cmd
nuget.exe sources Add -Name "Microsoft.Data.SqlClient.Commits" -Source "https://pkgs.dev.azure.com/sqlclientdrivers-ci/sqlclient/_packaging/Microsoft.Data.SqlClient.Commits/nuget/v3/index.json"
```

### Pushing to Public Feed

Maintainers of [dotnet/sqlclient](https://github.com/dotnet/SqlClient) manually push NuGet Packages to this feed after the PR is merged to the `master` branch and a commit ID has been generated on `master`.

Not every commit is built and published on the public CI feed, but only the commits that make considerable difference to the driver will be uploaded in this feed.

The label **Push to Commit Feed** will be added to the Pull Request if it is pushed to the feed.

### Downloading Packages

The package naming conventions follow SemVer 2.0.0 and also provide changeset information to ensure the right PR changes are being downloaded for testing:

Package names will be like: `Microsoft.Data.SqlClient.1.1.0-build.19250.1-c21aa7c.nupkg`

Breaking down:
- `1.1.0-build` - Identitier for currently active driver version in Build.
- `19250.1` - Unique identifier to keep latest PRs on top of the feed.
- `c21aa7c` - Short Commit Id to identify merged commit in `master`.

> Note: This public feed is only for testing and validating new PR changes. Packages from feed will be eventually removed when the maximum NuGet Package limit of **50** is reached. We do not recommend using packages from this feed in client applications.
