# Contributing to Microsoft.Data.SqlClient

General contribution guidance is included in this document. Additional guidance is defined in the documents linked below.

- [Copyright](COPYRIGHT.md) describes the licensing practices for the project.
- [Contribution Workflow](contributing-workflow.md) describes the workflow that the team uses for considering and accepting changes.

## Up for Grabs

The team marks the most straightforward issues as "up for grabs" and issues that are well suited to get you started as "Good first issue". This set of issues is the place to start if you are interested in contributing but new to the codebase.

- [dotnet/sqlclient - Up-for-Grabs :raised_hands:](https://github.com/dotnet/sqlclient/labels/Up-for-Grabs%20%3Araised_hands%3A)
- [dotnet/sqlclient - :sparkles: Good first issue](https://github.com/dotnet/sqlclient/labels/Good%20first%20issue%20%3Asparkles%3A)

## Getting Started

1. **Fork** the repository.
2. **Clone** your fork locally.
3. **Create a branch** for your changes.
4. **Build and test** following the [build guide](BUILDGUIDE.md).
5. **Make your changes** following our coding standards.
6. **Submit a pull request** with a clear description.

For detailed build instructions, see [BUILDGUIDE.md](BUILDGUIDE.md).

## Development Process

### Before You Start

- Check existing issues and PRs to avoid duplicates.
- For large changes, create an issue first to discuss the approach.
- Ensure you can build and test the project locally.

### PR Lifecycle

- PRs are automatically marked stale after 30 days of inactivity.
- Stale PRs are closed after 7 additional days.
- Convert PR to Draft to prevent auto-closure.
- All PRs require maintainer approval and passing CI checks.

## DOs and DON'Ts

### Issue Reporting

Please do:

- **DO** report each issue as a new issue (but check first if it's already been reported).
- **DO** respect Issue Templates and provide detailed information.
- **DO** provide a minimal repro app demonstrating the problem.

## Code Contributions

Please do:

- **DO** follow our [coding style](/policy/coding-style.md)
- **DO** include tests when adding new features.
- **DO** consider cross-platform compatibility.
- **DO** keep discussions focused.
- **DO** give priority to the current style of the project or file you're changing even if it diverges from general guidelines.
- **DO** blog and tweet (or whatever) about your contributions, frequently!

Please do not:

- **DON'T** make PRs for style changes only.
- **DON'T** surprise us with large PRs without prior discussion.
- **DON'T** commit code you didn't write without discussion.
- **DON'T** submit PRs that alter licensing related files or headers. If you believe there's a problem, file an issue first.

## Testing Requirements

### Test Guidelines

- **DO** include tests when adding new features or fixing bugs.
- **DO** use dynamic table/database object names to avoid conflicts in parallel runs.
- **DO** clean up all test objects and close resources properly.
- **DO** consider concurrency - our pipelines run tests in parallel.
- **DON'T** skip tests or run them conditionally unless necessary.
- **DON'T** leave artifacts on test servers or leave open resources.

### Test Categories

- Unit tests for individual components.
- Functional tests for feature validation.
- Manual tests for complex scenarios requiring SQL Server instances.

## File Headers

The following file header is used for Microsoft.Data.SqlClient. Please use it for new files.

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
```

## Using Labels

As adding labels [is not possible](https://stackoverflow.com/questions/13829466/how-to-put-a-label-on-an-issue-in-github-if-you-are-not-a-contributor-owner/13829505#13829505) for contributors, please comment in the issue and pull request on what labels should be added.
The below labels are required for new Pull Requests if applicable:

| Label | When to Use |
| ----- | ----------- |
| [Public API :new:](https://github.com/dotnet/SqlClient/labels/Public%20API%20%3Anew%3A) | Use this label if a new Public API is added to the Pull Request |
| [Breaking Change :hammer:](https://github.com/dotnet/SqlClient/labels/Breaking%20Change%20%3Ahammer%3A) | Use this label if the Pull Request breaks an existing API |
| [Area\Managed SNI](https://github.com/dotnet/SqlClient/labels/Area%5cManaged%20SNI) | Use this label for Managed SNI related issues/PRs |
| [Area\Tests](https://github.com/dotnet/SqlClient/labels/Area%5cTests) | Use this label for pull requests that add only tests |

You can find all possible labels [here](https://github.com/dotnet/SqlClient/labels)

## Automated Issue and PR Management

This repository uses automated workflows to maintain project hygiene:

### Stale Issues

- Issues labeled "Needs More Info" or "Waiting for Customer" are marked stale after 30 days of inactivity.
- Stale issues are automatically closed after 7 additional days.
- Issues with P0, P1, or P2 priority labels are exempt from auto-closure.

### Stale Pull Requests

- PRs are marked stale after 30 days of inactivity.
- Stale PRs are automatically closed after 7 additional days.
- PRs lin Draft are exempt from auto-closure.

To prevent closure, simply add comments, push commits, or respond to feedback.

## Reporting security issues and vulnerabilities

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) [secure@microsoft.com](mailto:secure@microsoft.com). You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [MSRC FAQ](https://www.microsoft.com/en-us/msrc/faqs-report-an-issue?rtc=1&oneroute=true).

## Contribution Standards

Project maintainers will merge changes that improve the product significantly and broadly and that align with the [Microsoft.Data.SqlClient roadmap](roadmap.md).

### Requirements

- Changes must be compatible with all supported .NET versions.
- Consider compatibility with supported SQL Server and Azure SQL versions.
- Include appropriate tests and documentation.
- Follow established coding patterns and architecture.

## Contributor License Agreement

You must sign a [.NET Foundation Contribution License Agreement (CLA)](https://cla.dotnetfoundation.org) before your PR will be merged. This is a one-time requirement for projects in the .NET Foundation. You can read more about [Contribution License Agreements (CLA)](http://en.wikipedia.org/wiki/Contributor_License_Agreement) on Wikipedia.

The agreement: [contribution-license-agreement.pdf](https://cla.dotnetfoundation.org)

You don't have to do this up-front. You can simply clone, fork, and submit your pull request as usual. When your pull request is created, it is classified by a CLA bot. If the change is trivial (for example, you just fixed a typo), then the PR is labelled with `cla-not-required`. Otherwise it's classified as `cla-required`. Once you signed a CLA, the current and all future pull requests will be labelled as `cla-signed`.

## Code of Conduct

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information, see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## Getting Help

- **Questions**: Use [GitHub Discussions](https://github.com/dotnet/SqlClient/discussions)
- **Bugs**: File an [issue](https://github.com/dotnet/SqlClient/issues/new/choose)
- **Security**: Email [secure@microsoft.com](mailto:secure@microsoft.com)
