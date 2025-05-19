[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)
[![Nuget](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient?label=Nuget.org%20Downloads&style=flat-square&color=blue)](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
[![Build status](https://sqlclientdrivers.visualstudio.com/public/_apis/build/status/ADO/CI-SqlClient)](https://sqlclientdrivers.visualstudio.com/public/_build/latest?definitionId=1879)

# Microsoft SqlClient Data Provider for SQL Server

Microsoft.Data.SqlClient is a .NET data provider for [Microsoft SQL Server]([url](https://aka.ms/sql)) and the [Azure SQL]([url](https://aka.ms/azure_sql)) family of databases. It grew from a union of the two System.Data.SqlClient components which live independently in .NET and .NET Framework. Going forward, support for new SQL Server and Azure SQL features will only be implemented in Microsoft.Data.SqlClient.

## Supportability

The Microsoft.Data.SqlClient package supports the following environments:

- .NET Framework 4.6.2+
- .NET 8.0+

## Download

The Microsoft.Data.SqlClient NuGet package is available on [NuGet.org](https://www.nuget.org/packages/Microsoft.Data.SqlClient/).

## SNI Package References

When targeting .NET Framework on Windows, a package reference to [Microsoft.Data.SqlClient.SNI](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI/) loads native `Microsoft.Data.SqlClient.SNI.<platform>.dll` libraries into the client's build directories.

When targeting .NET on Windows, a package reference to [Microsoft.Data.SqlClient.SNI.runtime](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI.runtime/) loads `arm64`, `x64` and `x86` native `Microsoft.Data.SqlClient.SNI.dll` libraries into subdirectories in the client's build directory.

## Helpful Links

| Topic | Link to File |
| :---- | :------------- |
| Coding Style | [coding-style.md](/policy/coding-style.md) |
| Best Practices | [coding-best-practices.md](/policy/coding-best-practices.md) |
| Review Process | [review-process.md](/policy/review-process.md) |
| Guidelines for building the driver | [BUILDGUIDE.md](BUILDGUIDE.md) |
| Guidelines for Contributors | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Changelog for all driver releases | [CHANGELOG.md](CHANGELOG.md) |
| Support Policy | [SUPPORT.md](SUPPORT.md) |
| Code of Conduct | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |
| Copyright Information | [COPYRIGHT.md](COPYRIGHT.md) |

## Our Featured Contributors

Special thanks to everyone who has contributed to the project.
We thank you for your continuous support in improving the SqlClient library!

- Edward Neal ([@edwardneal](https://github.com/edwardneal))
- Erik Ejlskov Jensen ([@ErikEJ](https://github.com/ErikEJ))
- Michel Zehnder ([@MichelZ](https://github.com/MichelZ))
- Shay Rojansky ([@roji](https://github.com/roji))
- Phillip Haydon ([@phillip-haydon](https://github.com/phillip-haydon))
- Rasmus Melchior Jacobsen ([@rmja](https://github.com/rmja))
- Robin Sue ([@Suchiman](https://github.com/Suchiman))
- Simon Cropp ([@SimonCropp](https://github.com/SimonCropp))
- Stefán Jökull Sigurðarson ([@stebet](https://github.com/stebet))
- Stephen Toub ([@stephentoub](https://github.com/stephentoub))
- Wraith ([@Wraith2](https://github.com/Wraith2))

Up-to-date list of contributors: [Contributor Insights](https://github.com/dotnet/SqlClient/graphs/contributors)

## Release Notes

All preview and stable driver release notes are available under [release-notes](release-notes).

## Porting from System.Data.SqlClient

Refer to [porting-cheat-sheet.md](porting-cheat-sheet.md) for a safe porting experience from System.Data.SqlClient to Microsoft.Data.SqlClient and share your experience with us by enhancing this guide for future developers.

## Still have questions?

Check out our [FAQ](https://github.com/dotnet/SqlClient/wiki/Frequently-Asked-Questions). Still not answered? Create an [issue](https://github.com/dotnet/SqlClient/issues/new/choose) to ask a question.

<!-- BEGIN MICROSOFT SECURITY.MD V0.0.3 BLOCK -->

## Security

Microsoft takes the security of our software products and services seriously, which includes all source code repositories managed through our GitHub organizations, which include [Microsoft](https://github.com/Microsoft), [Azure](https://github.com/Azure), [DotNet](https://github.com/dotnet), [AspNet](https://github.com/aspnet), [Xamarin](https://github.com/xamarin), and [our GitHub organizations](https://opensource.microsoft.com/).

If you believe you have found a security vulnerability in any Microsoft-owned repository that meets [Microsoft's definition of a security vulnerability](https://www.microsoft.com/en-us/msrc/definition-of-a-security-vulnerability), please report it to us as described below.

## Reporting Security Issues

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them to the Microsoft Security Response Center (MSRC) at [https://msrc.microsoft.com/create-report](https://msrc.microsoft.com/create-report).

If you prefer to submit without logging in, send email to [secure@microsoft.com](mailto:secure@microsoft.com).  If possible, encrypt your message with our PGP key; please download it from the [Microsoft Security Response Center PGP Key page](https://www.microsoft.com/en-us/msrc/pgp-key-msrc).

You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Additional information can be found at [microsoft.com/msrc](https://www.microsoft.com/msrc).

Please include the requested information listed below (as much as you can provide) to help us better understand the nature and scope of the possible issue:

- Type of issue (e.g. buffer overflow, SQL injection, cross-site scripting, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit the issue

This information will help us triage your report more quickly.

If you are reporting for a bug bounty, more complete reports can contribute to a higher bounty award. Please visit our [Microsoft Bug Bounty Program](https://microsoft.com/msrc/bounty) page for more details about our active programs.

## Preferred Languages

We prefer all communications to be in English.

## Policy

Microsoft follows the principle of [Coordinated Vulnerability Disclosure](https://www.microsoft.com/en-us/msrc/cvd).

<!-- END MICROSOFT SECURITY.MD BLOCK -->

## License

The Microsoft.Data.SqlClient Driver for SQL Server is licensed under the MIT license. See the [LICENSE](LICENSE) file for more details.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow Microsoft's Trademark & Brand Guidelines. Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
