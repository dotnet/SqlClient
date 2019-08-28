[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](https://raw.githubusercontent.com/dotnet/sqlclient/master/LICENSE)
[![Nuget](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient?label=Nuget.org%20Downloads&style=flat-square&color=blue)](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
[![Gitter](https://img.shields.io/gitter/room/badges/shields.svg?style=flat-square&color=blue)](https://gitter.im/Microsoft/mssql-developers)

# Microsoft SqlClient Data Provider for SQL Server

Welcome to the home of Microsoft ADO.NET driver for SQL Server aka Microsoft.Data.SqlClient GitHub repository.

Microsoft.Data.SqlClient is a data provider for Microsoft Sql Server. Now in General Availability, it is a union of the two System.Data.SqlClient components which live independently in .NET Framework and .NET Core. Going forward, support for new SQL Server features will be implemented in Microsoft.Data.SqlClient.

## Supportability
The Microsoft.Data.SqlClient package supports below environments:
- .NET Framework 4.6+
- .NET Core 2.1+
- .NET Standard 2.0+.

The source code of this library is now available under MIT license.

## Download

The Microsoft.Data.SqlClient NuGet package is available on [NuGet.org](https://www.nuget.org/packages/Microsoft.Data.SqlClient/).

## SNI Package References

For the .NET Framework driver on Windows, a package reference to [Microsoft.Data.SqlClient.SNI](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI/) loads `x64` and `x86` platform specific `SNI.dll` libraries into the client's build directories.

For the .NET Core driver on Windows, a package reference to [runtime.native.System.Data.SqlClient.sni](https://www.nuget.org/packages/runtime.native.System.Data.SqlClient.sni/) loads `arm64`, `x64` and `x86` platform specific `SNI.dll` libraries into the client's build directories.

## Building the Driver

All necessary details and commands for building the driver and running tests are available in the [BUILDGUIDE](BUILDGUIDE.md).

## Release Notes

All preview and stable driver release notes are available under [release-notes](release-notes).

## Guidelines for Creating Pull Requests

We love contributions from the community. To help improve the quality of our code, we encourage you to follow these guidelines:

- Code changes must adhere to the [C# Programming Guide](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/index).
- Driver code changes must be done considering cross-platform compatibility and supportability for all supported SQL and Azure Servers and client configurations.
- Tests must be added if non-existent to assure near to 100% code coverage for all future changes.
- Tests should be well structured and written well to be able to run in parallel using the same client and server configurations (in an isolated mode). E.g. Consider using dynamic table/database object names instead of hardcoded values (Use existing tests for reference).
- Tests should not leave any artifacts on the target server. Cleaning up all objects is highly appreciated to maintain test server health.
- Avoid skipping tests if possible or running them conditionally. If conditions are not met, test coverage will not be 100%.

Thank you!

## Guidelines for Reporting Issues

We appreciate you taking the time to test the driver, provide feedback and report any issues. It would be extremely helpful if you:

- Report each issue as a new issue (but check first if it's already been reported)
- Try to be detailed in your report. Useful information for good bug reports include:
  * What you are seeing and what the expected behavior is
  * The version of the driver in use.
  * Environment details: e.g. .NET Framework / .NET Core version, client operating system
  * Table schema (for some issues the data types make a big difference!)
  * Any other relevant information you want to share
- Providing a minimal repro app demonstrating the problem in isolation will greatly speed up the process of identifying and fixing problems.

Thank you!

## Reporting security issues and security bugs

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) [secure@microsoft.com](mailto:secure@microsoft.com). You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://technet.microsoft.com/en-us/security/ff852094.aspx).

## Still have questions?

Check out our [FAQ](https://github.com/dotnet/SqlClient/wiki/Frequently-Asked-Questions). Still not answered? Create an [issue](https://github.com/dotnet/SqlClient/issues/new/choose) to ask a question.

## License

The Microsoft.Data.SqlClient Driver for SQL Server is licensed under the MIT license. See the [LICENSE](https://github.com/dotnet/SqlClient/blob/master/LICENSE) file for more details.

## Code of conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
