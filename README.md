[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](https://raw.githubusercontent.com/dotnet/sqlclient/master/LICENSE)
[![Nuget](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient?label=Nuget.org%20Downloads&style=flat-square&color=blue)](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
[![Gitter](https://img.shields.io/gitter/room/badges/shields.svg?style=flat-square&color=blue)](https://gitter.im/Microsoft/mssql-developers)

# Microsoft SqlClient Data Provider for SQL Server

Welcome to the home of the Microsoft ADO.NET driver for SQL Server aka the Microsoft.Data.SqlClient GitHub repository.

Microsoft.Data.SqlClient is a data provider for Microsoft SQL Server and Azure SQL Database. Now in General Availability, it is a union of the two System.Data.SqlClient components which live independently in .NET Framework and .NET Core. Going forward, support for new SQL Server features will be implemented in Microsoft.Data.SqlClient.

## Supportability

The Microsoft.Data.SqlClient package supports the below environments:

- .NET Framework 4.6+
- .NET Core 2.1+
- .NET Standard 2.0+.

The source code of this library is now available under the MIT license.

## Download

The Microsoft.Data.SqlClient NuGet package is available on [NuGet.org](https://www.nuget.org/packages/Microsoft.Data.SqlClient/).

## SNI Package References

For the .NET Framework driver on Windows, a package reference to [Microsoft.Data.SqlClient.SNI](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI/) loads `x64` and `x86` native `SNI.dll` libraries into the client's build directories.

For the .NET Core driver on Windows, a package reference to [runtime.native.System.Data.SqlClient.sni](https://www.nuget.org/packages/runtime.native.System.Data.SqlClient.sni/) loads `arm64`, `x64` and `x86` native `SNI.dll` libraries into the client's build directories.

## Helpful Links

| Topic | Link to File |
| :---- | :------------- |
| Coding Style | [coding-style.md](coding-style.md) |
| Guidelines for building the driver | [BUILDGUIDE.md](BUILDGUIDE.md) |
| Guidelines for Contributors | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Changelog for all driver releases | [CHANGELOG.md](CHANGELOG.md) |
| Support Policy | [SUPPORT.md](SUPPORT.md) |
| Code of Conduct | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |
| Copyright Information | [COPYRIGHT.md](COPYRIGHT.md) |
| | |

## Our Featured Contributors

Special thanks to everyone who has contributed to the project.
We thank you for your continuous support in improving the SqlClient library!

- Wraith ([@Wraith2](https://github.com/Wraith2))
- Erik Ejlskov Jensen ([@ErikEJ](https://github.com/ErikEJ))
- Stefán Jökull Sigurðarson ([@stebet](https://github.com/stebet))
- Stephen Toub ([@stephentoub](https://github.com/stephentoub))
- Shay Rojansky ([@roji](https://github.com/roji))
- Rasmus Melchior Jacobsen ([@rmja](https://github.com/rmja))
- Phillip Haydon ([@phillip-haydon](https://github.com/phillip-haydon))
- Robin Sue ([@Suchiman](https://github.com/Suchiman))

Up-to-date list of contributors: [Contributor Insights](https://github.com/dotnet/SqlClient/graphs/contributors)

## Release Notes

All preview and stable driver release notes are available under [release-notes](release-notes).

## Porting from System.Data.SqlClient

Refer to [porting-cheat-sheet.md](porting-cheat-sheet.md) for a safe porting experience from System.Data.SqlClient to Microsoft.Data.SqlClient and share your experience with us by advancing this guide for future developers.

## Still have questions?

Check out our [FAQ](https://github.com/dotnet/SqlClient/wiki/Frequently-Asked-Questions). Still not answered? Create an [issue](https://github.com/dotnet/SqlClient/issues/new/choose) to ask a question.

## License

The Microsoft.Data.SqlClient Driver for SQL Server is licensed under the MIT license. See the [LICENSE](https://github.com/dotnet/SqlClient/blob/master/LICENSE) file for more details.

## Code of conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
