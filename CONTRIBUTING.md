# Contributing to Microsoft.Data.SqlClient

General contribution guidance is included in this document. Additional guidance is defined in the documents linked below.

- [Copyright](copyright.md) describes the licensing practices for the project.
- [Contribution Workflow](contributing-workflow.md) describes the workflow that the team uses for considering and accepting changes.

## Up for Grabs

The team marks the most straightforward issues as "up for grabs" and issues that are well suited to get you started as "Good first issue". This set of issues is the place to start if you are interested in contributing but new to the codebase.

- [dotnet/sqlclient - :raised_hands: Up-for-Grabs](https://github.com/dotnet/sqlclient/labels/%3Araised_hands%3A%20Up-for-Grabs)
- [dotnet/sqlclient - :sparkles: Good first issue](https://github.com/dotnet/sqlclient/labels/%3Asparkles%3A%20Good%20first%20issue)

## Contribution "Bar"

Project maintainers will merge changes that improve the product significantly and broadly and that align with the [Microsoft.Data.SqlClient roadmap](roadmap.md).

Contributions must also satisfy the other published guidelines defined in this document.

## DOs and DON'Ts

Please do:
- **DO** report each issue as a new issue (but check first if it's already been reported)
- **DO** respect Issue Templates and provide detailed information. It will make the process to reproduce the issue and provide a fix faster.
- **DO** provide a minimal repro app demonstrating the problem in isolation will greatly speed up the process of identifying and fixing problems.
- **DO** follow our [coding style](/policy/coding-style.md) (C# code-specific) when working on a Pull Request.
- **DO** give priority to the current style of the project or file you're changing even if it diverges from the general guidelines.
- **DO** consider cross-platform compatibility and supportability for all supported SQL and Azure Servers and client configurations.
- **DO** include tests when adding new features. When fixing bugs, start with adding a test that highlights how the current behavior is broken.
- **DO** consider concurrency when writing tests. Our pipelines run builds and tests in parallel using the same client and server configurations (in an isolated mode). E.g. Consider using dynamic table/database object names instead of hardcoded values (Use existing tests for reference).
- **DO** keep the discussions focused. When a new or related topic comes up it's often better to create new issue than to side track the discussion.
- **DO** blog and tweet (or whatever) about your contributions, frequently!

Please do not:

- **DON'T** make PRs for style changes.
- **DON'T** leave any artifacts on server in tests or leave open resources. Cleaning up all objects is highly appreciated to maintain test server health.
- **DON'T** skip tests or run them conditionally unless necessary. If conditions are not met, test coverage will not be 100%. Use only pre-defined conditions that are already being run in pipelines.
- **DON'T** surprise us with big pull requests. Instead, file an issue and start a discussion so we can agree on a direction before you invest a large amount of time.
- **DON'T** commit code that you didn't write. If you find code that you think is a good fit to add, file an issue and start a discussion before proceeding.
- **DON'T** submit PRs that alter licensing related files or headers. If you believe there's a problem with them, file an issue and we'll be happy to discuss it.

## Using Labels

As adding labels [is not possible](https://stackoverflow.com/questions/13829466/how-to-put-a-label-on-an-issue-in-github-if-you-are-not-a-contributor-owner/13829505#13829505) for contributors, please comment in the issue and pull request on what labels should be added.
The below variables are required for new Pull Requests if applicable:

| Label | Description |
| ----- | ----------- |
| [:new: Public API](https://github.com/dotnet/SqlClient/labels/%3Anew%3A%20Public%20API) | Use this variable if a new Public API is added to the Pull Request.
| [:hammer: Breaking Change](https://github.com/dotnet/SqlClient/labels/%3Ahammer%3A%20Breaking%20Change) | Use this variable if the Pull Request breaks an existing API. |
| [Area\Managed SNI](https://github.com/dotnet/SqlClient/labels/Area%5cManaged%20SNI) | Use this label if the issue/PR relates to issues in Managed SNI |
| [Area\Tests](https://github.com/dotnet/SqlClient/labels/Area%5cTests) | Use this label for pull requests that add only tests to the repository. |

You can find all possible labels [here](https://github.com/dotnet/SqlClient/labels)

## Reporting security issues and security bugs

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) [secure@microsoft.com](mailto:secure@microsoft.com). You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [MSRC FAQ](https://www.microsoft.com/en-us/msrc/faqs-report-an-issue?rtc=1&oneroute=true).

## File Headers

The following file header is used for Microsoft.Data.SqlClient. Please use it for new files.

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
```

## Contributor License Agreement

You must sign a [.NET Foundation Contribution License Agreement (CLA)](https://cla.dotnetfoundation.org) before your PR will be merged. This is a one-time requirement for projects in the .NET Foundation. You can read more about [Contribution License Agreements (CLA)](http://en.wikipedia.org/wiki/Contributor_License_Agreement) on Wikipedia.

The agreement: [contribution-license-agreement.pdf](https://cla.dotnetfoundation.org)

You don't have to do this up-front. You can simply clone, fork, and submit your pull-request as usual. When your pull-request is created, it is classified by a CLA bot. If the change is trivial (for example, you just fixed a typo), then the PR is labelled with `cla-not-required`. Otherwise it's classified as `cla-required`. Once you signed a CLA, the current and all future pull-requests will be labelled as `cla-signed`.

## Code Of Conduct

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information, see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).
