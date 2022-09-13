# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [Stable release 5.0.0] - 2022-08-05

This update brings the below changes over the previous release:

### Added

- Added support for `TDS 8`. To use TDS 8, users should specify `Encrypt=Strict` in the connection string. [#1608](https://github.com/dotnet/SqlClient/pull/1608)
- Added `TDS 8` version for TDSLogin. [#1657](https://github.com/dotnet/SqlClient/pull/1657)

### Fixed

- Fixed null SqlBinary as rowversion. [#1688](https://github.com/dotnet/SqlClient/pull/1688)
- Fixed **KeyNotFoundException** for the `FailoverPartner` key on SQL servers with availability group configured. [#1614](https://github.com/dotnet/SqlClient/pull/1614)
- Fixed small inconsistency between netcore and netfx for `EncryptionOptions`. [#1672](https://github.com/dotnet/SqlClient/pull/1672)
- Fixed `Microsoft.SqlServer.Server` netcore project package reference. [#1654](https://github.com/dotnet/SqlClient/pull/1654)

### Changed

- Updated `AuthProviderInfo` struct to be matched the changes in native SNI for `TDS 8` server certificate validation. [#1680](https://github.com/dotnet/SqlClient/pull/1680)
- Updated default system protocol for `TDS 8` on managed code. [#1678](https://github.com/dotnet/SqlClient/pull/1678)
- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.0.0`. [#1680](https://github.com/dotnet/SqlClient/pull/1680)
- Updated **IdentityModel** dependency from 6.8.0 to 6.21.0 and **IdentityClient** from 4.32.2 to 4.45.0. [#1646](https://github.com/dotnet/SqlClient/pull/1646)
- Changed from union overlay design to reflected interfaces for SqlTypes. [1647](https://github.com/dotnet/SqlClient/pull/1647)

## [Preview Release 5.0.0-preview3.22168.1] - 2022-06-16

This update brings the below changes over the previous release:

### Breaking changes over preview release v5.0.0-preview2

- Dropped classes from the `Microsoft.Data.SqlClient.Server` namespace and replaced them with supported types from the [Microsoft.SqlServer.Server](https://github.com/dotnet/SqlClient/tree/main/src/Microsoft.SqlServer.Server) package.[#1585](https://github.com/dotnet/SqlClient/pull/1585) The affected classes and enums are:
  - Microsoft.Data.SqlClient.Server.IBinarySerialize -> Microsoft.SqlServer.Server.IBinarySerialize
  - Microsoft.Data.SqlClient.Server.InvalidUdtException -> Microsoft.SqlServer.Server.InvalidUdtException
  - Microsoft.Data.SqlClient.Server.SqlFacetAttribute -> Microsoft.SqlServer.Server.SqlFacetAttribute
  - Microsoft.Data.SqlClient.Server.SqlFunctionAttribute -> Microsoft.SqlServer.Server.SqlFunctionAttribute
  - Microsoft.Data.SqlClient.Server.SqlMethodAttribute -> Microsoft.SqlServer.Server.SqlMethodAttribute
  - Microsoft.Data.SqlClient.Server.SqlUserDefinedAggregateAttribute -> Microsoft.SqlServer.Server.SqlUserDefinedAggregateAttribute
  - Microsoft.Data.SqlClient.Server.SqlUserDefinedTypeAttribute -> Microsoft.SqlServer.Server.SqlUserDefinedTypeAttribute
  - (enum) Microsoft.Data.SqlClient.Server.DataAccessKind -> Microsoft.SqlServer.Server.DataAccessKind
  - (enum) Microsoft.Data.SqlClient.Server.Format -> Microsoft.SqlServer.Server.Format
  - (enum) Microsoft.Data.SqlClient.Server.SystemDataAccessKind -> Microsoft.SqlServer.Server.SystemDataAccessKind

### Added

- Added support for `TDS 8`. To use TDS 8, users should specify Encrypt=Strict in the connection string. Strict mode disables TrustServerCertificate (always treated as False in Strict mode). HostNameInCertificate has been added to help some Strict mode scenarios. [#1608](https://github.com/dotnet/SqlClient/pull/1608)
- Added support for specifying Server SPN and Failover Server SPN on the connection. [#1607](https://github.com/dotnet/SqlClient/pull/1607)
- Added support for aliases when targeting .NET Core on Windows. [#1588](https://github.com/dotnet/SqlClient/pull/1588)

### Fixed

- Fixed naming, order, and formatting for `SqlDiagnosticsListener` on .NET Core and .NET. [#1637](https://github.com/dotnet/SqlClient/pull/1637)
- Fixed NullReferenceException during Azure Active Directory authentication. [#1625](https://github.com/dotnet/SqlClient/pull/1625)
- Added CommandText length validation when using stored procedure command types. [#1484](https://github.com/dotnet/SqlClient/pull/1484)
- Fixed `GetSchema("StructuredTypeMembers")` to return correct schema information. [#1500](https://github.com/dotnet/SqlClient/pull/1500), [#1639](https://github.com/dotnet/SqlClient/pull/1639)
- Fixed NullReferenceException when using `SqlDependency.Start` against an Azure SQL Database.[#1294](https://github.com/dotnet/SqlClient/pull/1294)
- Send the correct retained transaction descriptor in the MARS TDS Header when there is no current transaction on .NET 5+ and .NET Core. [#1624](https://github.com/dotnet/SqlClient/pull/1624)
- Parallelize SSRP requests (instance name resolution) on Linux and macOS when MultiSubNetFailover is specified. [#1578](https://github.com/dotnet/SqlClient/pull/1578)
- Adjust the default ConnectRetryCount against Azure Synapse OnDemand endpoints [#1626](https://github.com/dotnet/SqlClient/pull/1626)

### Changed

- Code health improvements [#1353](https://github.com/dotnet/SqlClient/pull/1353) [#1354](https://github.com/dotnet/SqlClient/pull/1354) [#1525](https://github.com/dotnet/SqlClient/pull/1525) [#1186](https://github.com/dotnet/SqlClient/pull/1186)
- Update Azure Identity dependency from 1.5.0 to 1.6.0.[#1611](https://github.com/dotnet/SqlClient/pull/1611)
- Improved Regex for SqlCommandSet [#1548](https://github.com/dotnet/SqlClient/pull/1548)
- Rework on `TdsParserStateObjectManaged` with nullable annotations. [#1555](https://github.com/dotnet/SqlClient/pull/1555)

## [Preview Release 5.0.0-preview2.22096.2] - 2022-04-06

This update brings the below changes over the previous release:

### Breaking changes over preview release v5.0.0-preview1

- Dropped support for .NET Framework 4.6.1 [#1574](https://github.com/dotnet/SqlClient/pull/1574)

### Fixed

- Fixed connection failure by skipping Certificate Revocation List (CRL) check during authentication [#1559](https://github.com/dotnet/SqlClient/pull/1559)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.0.0-preview2.22084.1`. [#1563](https://github.com/dotnet/SqlClient/pull/1563)
- Updated `Azure.Identity`  version to `1.5.0` and `Microsoft.Identity.Client` version to `4.30.1` [#1462](https://github.com/dotnet/SqlClient/pull/1462)
- Replaced AlwaysEncryptedAttestationException with SqlException [#1515](https://github.com/dotnet/SqlClient/pull/1515)
- Improved error message when adding wrong type to SqlParameterCollection [#1547](https://github.com/dotnet/SqlClient/pull/1547)
- Code health improvements [#1343](https://github.com/dotnet/SqlClient/pull/1343) [#1370](https://github.com/dotnet/SqlClient/pull/1370) [#1371](https://github.com/dotnet/SqlClient/pull/1371) [#1438](https://github.com/dotnet/SqlClient/pull/1438) [#1483](https://github.com/dotnet/SqlClient/pull/1483)

## [Preview Release 5.0.0-preview1.22069.1] - 2022-03-09

### Added

- Added SqlDataSourceEnumerator. [#1430](https://github.com/dotnet/SqlClient/pull/1430)
- Added new attestation protocol `None` option to forgo enclave attestation when using VBS enclaves. [#1425](https://github.com/dotnet/SqlClient/pull/1425) and [#1419](https://github.com/dotnet/SqlClient/pull/1419)
- Added a new AppContext switch to suppress insecure TLS warnings. [#1457](https://github.com/dotnet/SqlClient/pull/1457)

### Fixed

- Fixed all documentation paths to Unix format path. [#1442](https://github.com/dotnet/SqlClient/pull/1442)
- Fixed thread safety issue for `GetEnclaveProvider` by converting dictionary to concurrent dictionary. [#1451](https://github.com/dotnet/SqlClient/pull/1451)

### Changed
- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `v5.0.0-preview1.22062.1`. [#1537](https://github.com/dotnet/SqlClient/pull/1537)
- Modernized style in ValueUtilSmi. [#1351](https://github.com/dotnet/SqlClient/pull/1351)
- Changed SQL server codenames to version names. [#1439](https://github.com/dotnet/SqlClient/pull/1439)
- Prevented subtype generation in project files. [#1452](https://github.com/dotnet/SqlClient/pull/1452)
- Changed `Array.Copy` to `Buffer.BlockCopy` for byte arrays. [#1366](https://github.com/dotnet/SqlClient/pull/1366)
- Changed files in csproj to be alphabetically sorted in netfx and netcore. [#1364](https://github.com/dotnet/SqlClient/pull/1364)
- Sqlstream, SqlInternalTransaction and MetaDataUtilsSmi are moved to shared folder. [#1337](https://github.com/dotnet/SqlClient/pull/1337), [#1346](https://github.com/dotnet/SqlClient/pull/1346) and [#1339](https://github.com/dotnet/SqlClient/pull/1339)
- Various code improvements: [#1197](https://github.com/dotnet/SqlClient/pull/1197), [#1313](https://github.com/dotnet/SqlClient/pull/1313),[#1330](https://github.com/dotnet/SqlClient/pull/1330),[#1366](https://github.com/dotnet/SqlClient/pull/1366), [#1435](https://github.com/dotnet/SqlClient/pull/1435),[#1478](https://github.com/dotnet/SqlClient/pull/1478)

## [Stable release 4.1.1] - 2022-09-13

### Fixed

- Fixed connection failure by not requiring Certificate Revocation List (CRL) check during authentication. [#1706](https://github.com/dotnet/SqlClient/pull/1706)
- Parallelize SSRP requests on Linux and macOS when MultiSubNetFailover is specified. [#1708](https://github.com/dotnet/SqlClient/pull/1708), [#1746](https://github.com/dotnet/SqlClient/pull/1746)
- Added CommandText length validation when using stored procedure command types. [#1709](https://github.com/dotnet/SqlClient/pull/1709)
- Fixed NullReferenceException during Azure Active Directory authentication. [#1710](https://github.com/dotnet/SqlClient/pull/1710)
- Fixed null SqlBinary as rowversion. [#1712](https://github.com/dotnet/SqlClient/pull/1712)
- Fixed table's collation overriding with default UTF8 collation. [#1749](https://github.com/dotnet/SqlClient/pull/1749)

## Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `v4.0.1` [#1755](https://github.com/dotnet/SqlClient/pull/1755), which includes the fix for AppDomain crash introducing in issue [#1418](https://github.com/dotnet/SqlClient/issues/1418)
- Various code improvements: [#1711](https://github.com/dotnet/SqlClient/pull/1711)

## [Stable release 4.1.0] - 2022-01-31

### Added

- Added new Attestation Protocol `None` for `VBS` enclave types. This protocol will allow users to forgo enclave attestation for VBS enclaves. [#1419](https://github.com/dotnet/SqlClient/pull/1419) [#1425](https://github.com/dotnet/SqlClient/pull/1425)

## [Stable release 4.0.2] - 2022-09-13

### Fixed

- Fixed connection failure by not requiring Certificate Revocation List (CRL) check during authentication. [#1718](https://github.com/dotnet/SqlClient/pull/1718)
- Parallelize SSRP requests on Linux and macOS when MultiSubNetFailover is specified. [#1720](https://github.com/dotnet/SqlClient/pull/1720), [#1747](https://github.com/dotnet/SqlClient/pull/1747)
- Added CommandText length validation when using stored procedure command types. [#1721](https://github.com/dotnet/SqlClient/pull/1721)
- Fixed NullReferenceException during Azure Active Directory authentication. [#1722](https://github.com/dotnet/SqlClient/pull/1722)
- Fixed null SqlBinary as rowversion. [#1724](https://github.com/dotnet/SqlClient/pull/1724)
- Fixed table's collation overriding with default UTF8 collation. [#1750](https://github.com/dotnet/SqlClient/pull/1750)

## Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `v4.0.1` [#1754](https://github.com/dotnet/SqlClient/pull/1754), which includes the fix for AppDomain crash introducing in issue [#1418](https://github.com/dotnet/SqlClient/issues/1418)
- Various code improvements: [#1723](https://github.com/dotnet/SqlClient/pull/1723)

## [Stable release 4.0.1] - 2022-01-17

### Added

Added AppContext switch `SuppressInsecureTLSWarning` to allow suppression of TLS security warning when using `Encrypt=false` in the connection string. [#1457](https://github.com/dotnet/SqlClient/pull/1457) 

### Fixed

- Fixed Kerberos authentication failure when using .NET 6. [#1411](https://github.com/dotnet/SqlClient/pull/1411)
- Fixed connection failure when using `SqlLocalDB` instance pipe name. [#1433](https://github.com/dotnet/SqlClient/pull/1433)
- Fixed a failure when executing concurrent queries requiring enclaves. [#1451](https://github.com/dotnet/SqlClient/pull/1451)
- Updated obsolete API calls targeting .NET 6. [#1401](https://github.com/dotnet/SqlClient/pull/1401)

## [Stable Release 4.0.0] - 2021-11-18

### Added

- Added missing `SqlClientLogger` class to .NET Core refs and missing `SqlClientLogger.LogWarning` method in .NET Framework refs [#1392](https://github.com/dotnet/SqlClient/pull/1392)

### Changed

- Avoid throwing unnecessary exception when an invalid `SqlNotificationInfo` value is received from SQL Server [#1378](https://github.com/dotnet/SqlClient/pull/1378)
- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `v4.0.0` [#1391](https://github.com/dotnet/SqlClient/pull/1391)

## [Preview Release 4.0.0-preview3.21293.2] - 2021-10-20

This update brings the below changes over the previous release:

### Breaking changes over preview release v4.0.0-preview2

- Dropped support for .NET Core 2.1 [#1272](https://github.com/dotnet/SqlClient/pull/1272)
- [.NET Framework] Exception will not be thrown if a User ID is provided in the connection string when using `Active Directory Integrated` authentication [#1359](https://github.com/dotnet/SqlClient/pull/1359)

### Added

- Add `GetFieldValueAsync<T>` and `GetFieldValue<T>` support for `XmlReader`, `TextReader`, `Stream` [#1019](https://github.com/dotnet/SqlClient/pull/1019)

### Fixed

- Fixed `FormatException` when opening a connection with event tracing enabled [#1291](https://github.com/dotnet/SqlClient/pull/1291)
- Fixed improper initialization of `ActiveDirectoryAuthenticationProvider` [#1328](https://github.com/dotnet/SqlClient/pull/1328)
- Fixed `MissingMethodException` when accessing `SqlAuthenticationParameters.ConnectionTimeout` [#1336](https://github.com/dotnet/SqlClient/pull/1336)
- Fixed data corruption issues by reverting changes to async cancellations [#1352](https://github.com/dotnet/SqlClient/pull/1352)
- Fixed performance degradation by reverting changes to MARS state machine [#1357](https://github.com/dotnet/SqlClient/pull/1357)
- Fixed bug where environment variables are ignored when using `Active Directory Default` authentication [#1360](https://github.com/dotnet/SqlClient/pull/1360)

### Changed

- Removed attributes for classes used in Microsoft.VSDesigner due to lack of support for Microsoft.Data.SqlClient [#1296](https://github.com/dotnet/SqlClient/pull/1296)
- Disable encryption when connecting to SQL LocalDB [#1312](https://github.com/dotnet/SqlClient/pull/1312)
- Various code health and performance improvements. See [milestone](https://github.com/dotnet/SqlClient/milestone/31?closed=1) for more info.

## [Preview Release 4.0.0-preview2.21264.2] - 2021-09-21

This update brings the below changes over the previous release:

### Breaking changes over preview release v4.0.0-preview1

- Removed `Configurable Retry Logic` safety switch. [#1254](https://github.com/dotnet/SqlClient/pull/1254)

### Added

- Added support for `SqlFileStream` on Windows using .NET Standard 2.0 and above. [#1240](https://github.com/dotnet/SqlClient/pull/1240)
- Added support for **localdb** `shared instance` using managed SNI. [#1237](https://github.com/dotnet/SqlClient/pull/1237)

### Fixed

- Fixed `.NET decimal` conversion from `SqlDecimal`. [#1179](https://github.com/dotnet/SqlClient/pull/1179)
- Fixed `Event Source` changes on **TryBeginExecuteEvent** and **WriteEndExecuteEvent** to address the failure on other MS products such as OpenTelemetry and Application Insight. [#1258](https://github.com/dotnet/SqlClient/pull/1258)
- Fixed command's async cancellation. [#956](https://github.com/dotnet/SqlClient/pull/956)
- Fixed deadlock in transaction using .NET Framework. [#1242](https://github.com/dotnet/SqlClient/pull/1242)
- Fixed unknown transaction state issues when prompting delegated transaction. [1216](https://github.com/dotnet/SqlClient/pull/1216)

### Changed

- Various code improvements [#1155](https://github.com/dotnet/SqlClient/pull/1155) [#1236](https://github.com/dotnet/SqlClient/pull/1236) [#1251](https://github.com/dotnet/SqlClient/pull/1251) [#1266](https://github.com/dotnet/SqlClient/pull/1266)

## [Preview Release 4.0.0-preview1.21237.2] - 2021-08-25

### Breaking changes over stable release 3.0.0
- Changed `Encrypt` connection string property to be `true` by default. [#1210](https://github.com/dotnet/SqlClient/pull/1210)
- The driver now throws `SqlException` replacing `AggregateException` for active directory authentication modes. [#1213](https://github.com/dotnet/SqlClient/pull/1213)
- Dropped obsolete `Asynchronous Processing` connection property from .NET Framework. [#1148](https://github.com/dotnet/SqlClient/pull/1148)

### Added
- Added `SqlCommand.EnableOptimizedParameterBinding` property that when enabled increases performance for commands with very large numbers of parameters. [#1041](https://github.com/dotnet/SqlClient/pull/1041)
- Included `42108` and `42109` error codes to retriable transient errors list. [#1215](https://github.com/dotnet/SqlClient/pull/1215)
- Added new App Context switch to use OS enabled client protocols only. [#1168](https://github.com/dotnet/SqlClient/pull/1168)
- Added `PoolBlockingPeriod` connection property support in .NET Standard. [#1181](https://github.com/dotnet/SqlClient/pull/1181)
- Added support for `SqlDataReader.GetColumnSchema()` in .NET Standard. [#1181](https://github.com/dotnet/SqlClient/pull/1181)
- Added PropertyGrid support with component model annotations to `SqlConnectionStringBuilder` properties for .NET Core. [#1152](https://github.com/dotnet/SqlClient/pull/1152)

### Fixed
- Fixed issue with connectivity when TLS 1.3 is enabled on client and server. [#1168](https://github.com/dotnet/SqlClient/pull/1168)
- Fixed issue with connection encryption to ensure connections fail when encryption is required. [#1210](https://github.com/dotnet/SqlClient/pull/1210)
- Fixed issue where connection goes to unusable state. [#1128](https://github.com/dotnet/SqlClient/pull/1128)
- Fixed recursive calls to `RetryLogicProvider` when calling `SqlCommand.ExecuteScalarAsync`. [#1220](https://github.com/dotnet/SqlClient/pull/1220)
- Fixed async deadlock scenarios in web contexts with configurable retry logic provider. [#1220](https://github.com/dotnet/SqlClient/pull/1220)
- Fixed `EntryPointNotFoundException` in `InOutOfProcHelper` constructor. [#1120](https://github.com/dotnet/SqlClient/pull/1120)
- Fixed async thread blocking issues on `SqlConnection.Open()` for active directory authentication modes. [#1213](https://github.com/dotnet/SqlClient/pull/1213)
- Fixed driver behavior for Always Encrypted with secure enclaves to not fail when no user parameters have been provided. [#1115](https://github.com/dotnet/SqlClient/pull/1115)
- Fixed bug with `LegacyRowVersionNullBehavior` App Context switch. [#1182](https://github.com/dotnet/SqlClient/pull/1182)
- Fixed issues in Strings.resx file containing error messages. [#1136](https://github.com/dotnet/SqlClient/pull/1136) [#1178](https://github.com/dotnet/SqlClient/pull/1178)

### Changed
- Updated error code to match with Windows when certificate validation fails in non-Windows client environments. [#1130](https://github.com/dotnet/SqlClient/pull/1130)
- Removed designer attributes from `SqlCommand` and `SqlDataAdapter`. [#1132](https://github.com/dotnet/SqlClient/pull/1132)
- Updated configurable retry logic default retriable error list. [#1125](https://github.com/dotnet/SqlClient/pull/1125)
- Improved performance by changing `SqlParameter` bool fields to flags. [#1064](https://github.com/dotnet/SqlClient/pull/1064)
- Improved performance by implementing static delegates. [#1060](https://github.com/dotnet/SqlClient/pull/1060)
- Optimized async method allocations in .NET Framework by porting changes from .NET Core. [#1084](https://github.com/dotnet/SqlClient/pull/1084)
- Various code improvements [#902](https://github.com/dotnet/SqlClient/pull/902) [#925](https://github.com/dotnet/SqlClient/pull/925) [#933](https://github.com/dotnet/SqlClient/pull/933) [#934](https://github.com/dotnet/SqlClient/pull/934) [#1024](https://github.com/dotnet/SqlClient/pull/1024) [#1057](https://github.com/dotnet/SqlClient/pull/1057) [#1122](https://github.com/dotnet/SqlClient/pull/1122) [#1133]((https://github.com/dotnet/SqlClient/pull/1133)) [#1134](https://github.com/dotnet/SqlClient/pull/1134) [#1141](https://github.com/dotnet/SqlClient/pull/1141) [#1187](https://github.com/dotnet/SqlClient/pull/1187) [#1188](https://github.com/dotnet/SqlClient/pull/1188) [#1223](https://github.com/dotnet/SqlClient/pull/1223) [#1225](https://github.com/dotnet/SqlClient/pull/1225)  [#1226](https://github.com/dotnet/SqlClient/pull/1226)

## [Stable release 3.1.1] - 2022-08-12

### Fixed

- Fixed null SqlBinary as rowversion. [#1700](https://github.com/dotnet/SqlClient/pull/1700)
- Fixed Kerberos authentication failure when using .NET 6. [#1696](https://github.com/dotnet/SqlClient/pull/1696)
- Fixed NullReferenceException during Azure Active Directory authentication. [#1695](https://github.com/dotnet/SqlClient/pull/1695)
- Removed union overlay design and use reflection in `SqlTypeWorkarounds`. [#1699](https://github.com/dotnet/SqlClient/pull/1699)

## [Stable release 3.1.0] - 2022-03-30

### Added

- Added new Attestation Protocol `None` for `VBS` enclave types. This protocol will allow users to forgo enclave attestation for VBS enclaves. [#1539](https://github.com/dotnet/SqlClient/pull/1539)
- Included `42108` and `42109` error codes to retriable transient errors list. [#1560](https://github.com/dotnet/SqlClient/pull/1560)

### Fixed

- Changed EnclaveDelegate.Crypto GetEnclaveProvider to use a thread safe concurrent dictionary. [#1564](https://github.com/dotnet/SqlClient/pull/1564

## [Stable Release 3.0.1] - 2021-09-24

### Fixed

- Fixed async thread blocking issues on `SqlConnection.Open()` for active directory authentication modes. [#1270](https://github.com/dotnet/SqlClient/pull/1270)
- Fixed unknown transaction state issues when prompting delegated transaction. [1247](https://github.com/dotnet/SqlClient/pull/1247)
- Fixed issue with connection encryption to ensure connections fail when encryption is required. [#1233](https://github.com/dotnet/SqlClient/pull/1233)
- Fixed bug with `LegacyRowVersionNullBehavior` App Context switch. [#1246](https://github.com/dotnet/SqlClient/pull/1246)
- Fixed recursive calls to `RetryLogicProvider` when calling `SqlCommand.ExecuteScalarAsync`. [#1245](https://github.com/dotnet/SqlClient/pull/1245)
- Fixed async deadlock scenarios in web contexts with configurable retry logic provider. [#1245](https://github.com/dotnet/SqlClient/pull/1245)
- Fixed deadlock in transaction using .NET Framework. [#1243](https://github.com/dotnet/SqlClient/pull/1243)
- Fixed issue where connection goes to unusable state. [#1238](https://github.com/dotnet/SqlClient/pull/1238)

## [Stable Release 3.0.0] - 2021-06-09

### Added
- Added support for column encryption key caching when the server supports retrying queries that require enclave computations [#1062](https://github.com/dotnet/SqlClient/pull/1062)
- Added support for configurable retry logic configuration file in .NET Standard [#1090](https://github.com/dotnet/SqlClient/pull/1090)

### Changed
- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `v3.0.0` [#1102](https://github.com/dotnet/SqlClient/pull/1102)
- Improved event counter display information [#1091](https://github.com/dotnet/SqlClient/pull/1091)

### Breaking Changes
- Modified column encryption key store provider registrations to give built-in system providers precedence over providers registered on connection and command instances. [#1101](https://github.com/dotnet/SqlClient/pull/1101)

## [Stable Release 2.1.5] - 2022-08-30

### Fixed

- Added CommandText length validation when using stored procedure command types. [#1726](https://github.com/dotnet/SqlClient/pull/1726)
- Fixed Kerberos authentication failure when using .NET 6. [#1727](https://github.com/dotnet/SqlClient/pull/1727)
- Removed union overlay design and use reflection in `SqlTypeWorkarounds`. [#1729](https://github.com/dotnet/SqlClient/pull/1729)

## [Stable Release 2.1.4] - 2021-09-20

### Fixed

- Fixed issue with connection encryption to ensure connections fail when encryption is required. [#1232](https://github.com/dotnet/SqlClient/pull/1232)
- Fixed issue where connection goes to unusable state. [#1239](https://github.com/dotnet/SqlClient/pull/1239)

## [Stable Release 2.1.3] - 2021-05-21

### Fixed

- Fixed wrong data blended with transactions in .NET Core by marking a connection as doomed if the transaction completes or aborts while there is an open result set [#1051](https://github.com/dotnet/SqlClient/pull/1051)
- Fixed race condition issues between SinglePhaseCommit and TransactionEnded events [#1049](https://github.com/dotnet/SqlClient/pull/1049)

## [Preview Release 3.0.0-preview3.21140.5] - 2021-05-20

### Added

- Added support for "Active Directory Default" authentication mode [#1043](https://github.com/dotnet/SqlClient/pull/1043)
- Added support for connection-level and command-level registration of custom key store providers to enable multi-tenant applications to control key store access [#1045](https://github.com/dotnet/SqlClient/pull/1045) [#1056](https://github.com/dotnet/SqlClient/pull/1056) [#1078](https://github.com/dotnet/SqlClient/pull/1078)
- Added IP address preference support for TCP connections [#1015](https://github.com/dotnet/SqlClient/pull/1015)

### Fixed

- Fixed corrupted connection issue when an exception occurs during RPC execution with TVP types [#1068](https://github.com/dotnet/SqlClient/pull/1068)
- Fixed race condition issues between SinglePhaseCommit and TransactionEnded events [#1042](https://github.com/dotnet/SqlClient/pull/1042)

### Changed

- Updated error messages for enclave exceptions to include a link to a troubleshooting guide. [#994](https://github.com/dotnet/SqlClient/pull/994)
- Changes to share common files between projects [#1022](https://github.com/dotnet/SqlClient/pull/1022) [#1038](https://github.com/dotnet/SqlClient/pull/1038) [#1040](https://github.com/dotnet/SqlClient/pull/1040) [#1033](https://github.com/dotnet/SqlClient/pull/1033) [#1028](https://github.com/dotnet/SqlClient/pull/1028) [#1039](https://github.com/dotnet/SqlClient/pull/1039)


## [Preview Release 3.0.0-preview2.21106.5] - 2021-04-16

### Breaking Changes over preview release v3.0.0-preview1
- `User Id` connection property now requires `Client Id` instead of `Object Id` for **User-Assigned Managed Identity** [#1010](https://github.com/dotnet/SqlClient/pull/1010)
- `SqlDataReader` now returns a `DBNull` value instead of an empty `byte[]`. Legacy behavior can be enabled by setting `AppContext` switch **Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior** [#998](https://github.com/dotnet/SqlClient/pull/998)

### Added
- **Microsoft.Data.SqlClient** now depends on **Azure.Identity** library to acquire a token for "Active Directory Managed Identity/MSI" and "Active Directory Service Principal" authentication modes. [#1010](https://github.com/dotnet/SqlClient/pull/1010)
- Upgraded Native SNI dependency to **v3.0.0-preview1** along with enhanced event tracing support [#1006](https://github.com/dotnet/SqlClient/pull/1006)

### Fixed
- Fixed wrong data blended with transactions in .NET Core by marking a connection as doomed if the transaction completes or aborts while there is an open result set[#1023](https://github.com/dotnet/SqlClient/pull/1023)
- Fixed derived parameters containing incorrect TypeName [#1020](https://github.com/dotnet/SqlClient/pull/1020)
- Fixed server connection leak possibilities when an exception occurs in pooling layer [#890](https://github.com/dotnet/SqlClient/pull/890)
- Fixed IP connection resolving logic in .NET Core [#1016](https://github.com/dotnet/SqlClient/pull/1016) [#1031](https://github.com/dotnet/SqlClient/pull/1031)

### Changed
- Performance improvements in `SqlDateTime` to `DateTime` internal conversion method [#912](https://github.com/dotnet/SqlClient/pull/912)
- Improved memory allocation by avoiding unnecessary context switching [1008](https://github.com/dotnet/SqlClient/pull/1008)
- Updated `Microsoft.Identity.Client` version from **4.21.1** to **4.22.0** [#1036](https://github.com/dotnet/SqlClient/pull/1036)
- Various performance improvements [#963](https://github.com/dotnet/SqlClient/pull/963) [#996](https://github.com/dotnet/SqlClient/pull/996) [#1004](https://github.com/dotnet/SqlClient/pull/1004) [#1012](https://github.com/dotnet/SqlClient/pull/1012) [#1017](https://github.com/dotnet/SqlClient/pull/1017)
- Event source tracing improvements [#1018](https://github.com/dotnet/SqlClient/pull/1018)
- Changes to share common files between NetFx and NetCore source code [#871](https://github.com/dotnet/SqlClient/pull/871) [#887](https://github.com/dotnet/SqlClient/pull/887)

## [Preview Release 3.0.0-preview1.21075.2] - 2021-03-15

### Breaking Changes over stable release v2.1
- The minimum supported .NET Framework version has been increased to v4.6.1. .NET Framework v4.6.0 is no longer supported. [#899](https://github.com/dotnet/SqlClient/pull/899)

### Added
- Added support for Configurable Retry Logic [#693](https://github.com/dotnet/SqlClient/pull/693) [#966](https://github.com/dotnet/SqlClient/pull/966)
- Added support for Event counters in .NET Core 3.1+ and .NET Standard 2.1+ [#719](https://github.com/dotnet/SqlClient/pull/719)
- Added support for Assembly Context Unloading in .NET Core [#913](https://github.com/dotnet/SqlClient/pull/913)
- Added missing `System.Runtime.Caching` dependency for .NET Standard assemblies [#877](https://github.com/dotnet/SqlClient/pull/877)

### Fixed
- Fixed wrong results issues by changing the timeout timer to ensure a correct execution state [#906](https://github.com/dotnet/SqlClient/pull/906)
- Fixed Kerberos authentication issues when configured Server Principal Name (SPN) didn't contain default port [#930](https://github.com/dotnet/SqlClient/pull/930)
- Fixed MARS header errors when `MakeReadAsyncBlocking` App Context switch is set to `false` [#910](https://github.com/dotnet/SqlClient/pull/910) [#922](https://github.com/dotnet/SqlClient/pull/922)
- Fixed unwanted exceptions being thrown from `SqlDataReader.Dispose` [#920](https://github.com/dotnet/SqlClient/pull/920)
- Fixed issues connecting to SQL Server instance with instance name specified from Unix environment [#870](https://github.com/dotnet/SqlClient/pull/870)
- Fixed TCP Keep Alive issues in .NET Core [#854](https://github.com/dotnet/SqlClient/pull/854)
- Fixed Kerberos Authentication issues caused due to regression [#845](https://github.com/dotnet/SqlClient/pull/845)
- Fixed issues with System-Assigned Managed Identity in Azure Functions [#829](https://github.com/dotnet/SqlClient/pull/829)
- Fixed missing error messages in Managed SNI [#882](https://github.com/dotnet/SqlClient/pull/882)
- Fixed event source trace string issue [#940](https://github.com/dotnet/SqlClient/pull/940)

### Changed
- Changed App Context switch `MakeReadAsyncBlocking` default to `false` [#937](https://github.com/dotnet/SqlClient/pull/937)
- Replaced usage of `BinaryFormatter` with `DataContractSerializer` [#869](https://github.com/dotnet/SqlClient/pull/869)
- Prohibited `DtdProcessing` on `XmlTextReader` instance in .NET Core [#884](https://github.com/dotnet/SqlClient/pull/884)
- Improved performance by reducing memory allocations in `SerializeEncodingChar`/`WriteEncodingChar` and some options boxing [#785](https://github.com/dotnet/SqlClient/pull/785)
- Improved performance by preventing orphaned active packets being GC'ed without clear [#888](https://github.com/dotnet/SqlClient/pull/888)
- Various performance improvements [#889](https://github.com/dotnet/SqlClient/pull/889) [#900](https://github.com/dotnet/SqlClient/pull/900)
- Partial event source tracing improvements in .NET Core [#867](https://github.com/dotnet/SqlClient/pull/867) [#897](https://github.com/dotnet/SqlClient/pull/897)
- Changes to share common files between NetFx and NetCore source code [#827](https://github.com/dotnet/SqlClient/pull/827) [#835](https://github.com/dotnet/SqlClient/pull/835) [#838](https://github.com/dotnet/SqlClient/pull/838) [#881](https://github.com/dotnet/SqlClient/pull/881)


## [Stable Release 1.1.4] - 2021-03-10

### Fixed
- Fixed wrong results issues by changing the timeout timer to ensure a correct execution state [#950](https://github.com/dotnet/SqlClient/pull/950)
- Fixed MARS header contains errors issue against .NET Framework 4.8+ [#959](https://github.com/dotnet/SqlClient/pull/959)


## [Stable Release 2.1.2] - 2021-03-03

### Fixed
- Fixed issue connecting with instance name from a Linux/macOS environment [#874](https://github.com/dotnet/SqlClient/pull/874)
- Fixed wrong results issues by changing the timeout timer to ensure a correct execution state [#929](https://github.com/dotnet/SqlClient/pull/929)
- Fixed a vulnerability by prohibiting `DtdProcessing` on `XmlTextReader` instances in .NET Core [#885](https://github.com/dotnet/SqlClient/pull/885)
- Fixed Kerberos authentication when an SPN does not contain the port [#935](https://github.com/dotnet/SqlClient/pull/935)
- Fixed missing error messages in Managed SNI [#883](https://github.com/dotnet/SqlClient/pull/883)
- Fixed missing `System.Runtime.Caching` dependency for .NET Standard assemblies [#878](https://github.com/dotnet/SqlClient/pull/878)
- Fixed event source tracing issues [#941](https://github.com/dotnet/SqlClient/pull/941)
- Fixed MARS header contains errors issue against .NET Framework 4.8.1 [#928](https://github.com/dotnet/SqlClient/pull/928)


## [Stable Release 2.1.1] - 2020-12-18

### Fixed
- Fixed issue with System-Assigned Managed Identity in Azure Functions [#841](https://github.com/dotnet/SqlClient/pull/841)
- Fixed issue with Kerberos Authentication for .NET Core in Unix environments [#848](https://github.com/dotnet/SqlClient/pull/848)
- Fixed issue with TCP Keep Alive for .NET Core in Unix environments [#855](https://github.com/dotnet/SqlClient/pull/855)


## [Stable Release 2.1.0] - 2020-11-19

### Added
- Microsoft.Data.SqlClient symbols are now source-linked [#789](https://github.com/dotnet/SqlClient/pull/789)
- Added an API to clear cached access tokens from the token provider [#800](https://github.com/dotnet/SqlClient/pull/800)
- Added `SqlFacetAttribute` implementation [#757](https://github.com/dotnet/SqlClient/pull/757)

### Fixed
- Fixed `InvalidOperationException` and `NotSupportedException` errors due to `WriteAsync` collisions [#796](https://github.com/dotnet/SqlClient/pull/796)
- Fixed incorrect Settings.Async flag in `ExecuteXmlReaderAsync` [#782](https://github.com/dotnet/SqlClient/pull/782)
- Fixed a regression in Windows Integrated authentication when using managed networking [#777](https://github.com/dotnet/SqlClient/pull/777)
- Fixed Bulk Copy Async deadlock issues with custom `IDataReader` when using `SqlDataReader` internally [#779](https://github.com/dotnet/SqlClient/pull/779)
- Fixed a serialization issue with `SqlException` in .NET Core [#780](https://github.com/dotnet/SqlClient/pull/780)

### Changed
- Updated versions of `Microsoft.IdentityModel` package dependencies [#794](https://github.com/dotnet/SqlClient/pull/794)


## [Preview Release 2.1.0-preview2.20297.7] - 2020-10-23

### Added
- Added support for Azure Active Directory Managed Identity authentication [#730](https://github.com/dotnet/SqlClient/pull/730)
- Added support to provide a user-defined application client ID when using Active Directory authentication [#740](https://github.com/dotnet/SqlClient/pull/740)
- Added the "Command Timeout" connection string property to set a default timeout for all commands executed with the connection [#722](https://github.com/dotnet/SqlClient/pull/722)
- Added support for Always Encrypted on all supported platforms for .NET Standard 2.0 [#756](https://github.com/dotnet/SqlClient/pull/756)

### Fixed
- Fixed unobserved exception issue when a timeout occurs before a faulted task completes with an exception [#688](https://github.com/dotnet/SqlClient/pull/688) [#773](https://github.com/dotnet/SqlClient/pull/773)
- Fixed an issue where driver continues to prompt for credentials when using Azure Active Directory authentication [#770](https://github.com/dotnet/SqlClient/pull/770)

### Changed
- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `v2.1.1` and removed symbols from `Microsoft.Data.SqlClient.SNI.runtime`, which are now published to Microsoft Symbols Server [#764](https://github.com/dotnet/SqlClient/pull/764)
- Updated `Microsoft.Identity.Client` dependency version to `v4.21.1` [#765](https://github.com/dotnet/SqlClient/pull/765)
- Performance improvements when establishing an encrypted channel by removing sync over async method calls [#541](https://github.com/dotnet/SqlClient/pull/541)
- Performance improvements by replacing heap-allocated arrays with Spans [#667](https://github.com/dotnet/SqlClient/pull/667)
- Moved common files to shared folder between .NET Framework and .NET Core implementation [#734](https://github.com/dotnet/SqlClient/pull/734) [#753](https://github.com/dotnet/SqlClient/pull/753)


## [Stable Release 2.0.1] - 2020-08-25

### Added
- Added support for a new Configuration Section, `SqlClientAuthenticationProviders` (duplicate of existing `SqlAuthenticationProviders`), to allow co-existence of configurations for both drivers, "System.Data.SqlClient" and "Microsoft.Data.SqlClient" [#701](https://github.com/dotnet/SqlClient/pull/701)

### Fixed
- Fixed pooled connection re-use on access token expiry issue when using Active Directory authentication modes [#639](https://github.com/dotnet/SqlClient/pull/639)
- Fixed transient fault handling for Pooled connections [#638](https://github.com/dotnet/SqlClient/pull/638)
- Fixed Enclave session cache issue with Azure Database [#628](https://github.com/dotnet/SqlClient/pull/628)
- Reverted changes to return empty DataTable from GetSchemaTable to return null as before. [#697](https://github.com/dotnet/SqlClient/pull/697)
- Fixed configuration section collision issue with System.Data.SqlClient type [#701](https://github.com/dotnet/SqlClient/pull/701)
- Fixed blank error message [HTTP Provider] issues due to unexpected pre-login failures when using Native SNI. Fixed with Microsoft.Data.SqlClient.SNI v2.0.1 and Microsoft.Data.SqlClient.SNI.runtime v2.0.1 release versions.


## [Preview Release 2.1.0-preview1.20235.1] - 2020-08-21

### Added
- Added support for Always Encrypted with secure enclaves on Unix for .NET Core 2.1+ and on all supported platforms for .NET Standard 2.1+ [#676](https://github.com/dotnet/SqlClient/pull/676)
- Added support for Azure Active Directory Device Code Flow authentication [#597](https://github.com/dotnet/SqlClient/pull/597)
- Added Sensitivity Rank support in Sensitivity Classification information [#626](https://github.com/dotnet/SqlClient/pull/626)
- Added support to obtain `ServerProcessId` (SPID) information on an active `SqlConnection` instance [#660](https://github.com/dotnet/SqlClient/pull/660)
- Added support for a new Configuration Section, `SqlClientAuthenticationProviders` (duplicate of existing `SqlAuthenticationProviders`), to allow co-existence of configurations for both drivers, "System.Data.SqlClient" and "Microsoft.Data.SqlClient" [#702](https://github.com/dotnet/SqlClient/pull/702)
- Added TraceLogging in Native SNI to extend `SqlClientEventSource` support [#650](https://github.com/dotnet/SqlClient/pull/650)
- Updated Microsoft.Data.SqlClient.SNI (.NET Framework dependency) and Microsoft.Data.SqlClient.SNI.runtime (.NET Core/Standard dependency) version to v2.1.0 with trace logging implementation [#705](https://github.com/dotnet/SqlClient/pull/705)

### Fixed
- Fixed Enclave session cache issue with Azure Database [#686](https://github.com/dotnet/SqlClient/pull/686)
- Fixed pooled connection re-use on access token expiry issue when using Active Directory authentication modes [#635](https://github.com/dotnet/SqlClient/pull/635)
- Fixed transient fault handling for Pooled connections [#637](https://github.com/dotnet/SqlClient/pull/637)
- Fixed SPN generation issue when no port is provided [#629](https://github.com/dotnet/SqlClient/pull/629)
- Fixed missing null checks for `SqlErrors` in `SqlException` for .NET Framework implementation [#698](https://github.com/dotnet/SqlClient/pull/698)

### Changed
- Performance improvements by fixing unnecessary allocations with EventSource implementation [#684](https://github.com/dotnet/SqlClient/pull/684)
- Reverted changes to return empty DataTable from GetSchemaTable to return null as before. [#696](https://github.com/dotnet/SqlClient/pull/696)
- Removed multiple `CacheConnectionStringProperties` calls when setting `ConnectionString` properties [#683](https://github.com/dotnet/SqlClient/pull/683)
- Code Improvements by only checking inexact match when no exact match is found for an embedded resource [#668](https://github.com/dotnet/SqlClient/pull/668)
- Changed `_SqlMetaData` to lazy initialize hidden column map [#521](https://github.com/dotnet/SqlClient/pull/521)
- Renamed internal string resource file and helpers for .NET Core implementation [#671](https://github.com/dotnet/SqlClient/pull/671)
- Performance improvements by reworking `ExecuteReaderAsync` to minimize allocations [#528](https://github.com/dotnet/SqlClient/pull/528)
- Performance improvements by moving `DataReader` caches to internal connection [#499](https://github.com/dotnet/SqlClient/pull/499)
- Moved common files to shared folder between .NET Framework and .NET Core implementation [#618](https://github.com/dotnet/SqlClient/pull/618) [#625](https://github.com/dotnet/SqlClient/pull/625)


## [Stable Release 2.0.0] - 2020-06-16

### Added
- Added internal driver support to provide resiliency to DNS failures [#594](https://github.com/dotnet/SqlClient/pull/594)
- Added support for `Active Directory Integrated`, `Active Directory Interactive` and `Active Directory Service Principal` authentication mode for .NET Core and .NET Standard [#560](https://github.com/dotnet/SqlClient/pull/560)
- Added support for `Active Directory Service Principal` authentication mode for .NET Framework [#560](https://github.com/dotnet/SqlClient/pull/560)
- Added support for optional `ORDER` hints in `SqlBulkCopy` for improved performance [#540](https://github.com/dotnet/SqlClient/pull/540)

### Fixed
- Fixed `SqlSequentialStream` multipacket read stalling issue in .NET Core [#603](https://github.com/dotnet/SqlClient/pull/603)
- Fixed code page issue for Kazakh collation in SQL Server [#584](https://github.com/dotnet/SqlClient/pull/584)
- Fixed stalled application issues when end of stream is reached [#577](https://github.com/dotnet/SqlClient/pull/577)
- Fixed driver behavior to not throw exception for invalid configuration file [#573](https://github.com/dotnet/SqlClient/pull/573)
- Fixed Object null reference issue when failover partner is set [#588](https://github.com/dotnet/SqlClient/pull/588)
- Fixed `applicationintent` connection string property issue [#585](https://github.com/dotnet/SqlClient/pull/585)

### Changed
- Raise warning message when insecure TLS protocols are in use [#591](https://github.com/dotnet/SqlClient/pull/591)

### Breaking Changes
- Modified enclave provider interface `SqlColumnEncryptionEnclaveProvider` to be internal [#602](https://github.com/dotnet/SqlClient/pull/602) - _This change is not likely to impact customer applications since secure enclaves is a relatively new feature and they would have had to implement their own enclave provider, which is not a trivial task_.
- Updated `SqlClientMetaDataCollectionNames` exposed constants by removing non-existing constants and adding new to the metadata collection [#580](https://github.com/dotnet/SqlClient/pull/580)


## [Preview Release 2.0.0-preview4.20142.4] - 2020-05-21

### Added
- Microsoft.Data.SqlClient (.NET Core and .NET Standard) on Windows is now dependent on **Microsoft.Data.SqlClient.SNI.runtime**, replacing the previous dependency on **runtime.native.System.Data.SqlClient.SNI** [#570](https://github.com/dotnet/SqlClient/pull/570)
- The new **Microsoft.Data.SqlClient.SNI.runtime** dependency adds support for the *ARM* platform along with the already supported platforms *ARM64*, *x64* and *x86* on Windows [#570](https://github.com/dotnet/SqlClient/pull/570)
- Improved driver performance by introducing managed packet recycling [#389](https://github.com/dotnet/SqlClient/pull/389)

### Fixed
- Fixed `SqlBulkCopy` to work with database columns containing metadata about data classification [#568](https://github.com/dotnet/SqlClient/pull/568)
- Fixed unsafe cast in `SqlException` for `SerializationEntry.Value`
- Fixed null reference exceptions in `SqlDelegatedTransaction` methods [#563](https://github.com/dotnet/SqlClient/pull/563)

### Changed
- Standardized connection string properties for enhanced user experience [#534](https://github.com/dotnet/SqlClient/pull/534)
- Improved performance by reducing eventsource tracing related to allocations from TVP write methods [#557](https://github.com/dotnet/SqlClient/pull/557) [#564](https://github.com/dotnet/SqlClient/pull/564)

### Breaking Changes
- For .NET Framework applications consuming **Microsoft.Data.SqlClient**, the `SNI.dll` files previously downloaded to the `bin\x64` and `bin\x86` folders are now named `Microsoft.Data.SqlClient.SNI.x64.dll` and `Microsoft.Data.SqlClient.SNI.x86.dll` and will be downloaded to the `bin` directory, to support auto-loading in the application process [#570](https://github.com/dotnet/SqlClient/pull/570). This change is not going to impact client applications unless a direct reference has been made to `SNI.dll` or the x86 and x64 folders.


## [Stable Release 1.1.3] - 2020-05-15

### Fixed
- Fixed driver behavior to not perform enlistment of pooled connection on aborted transaction [#551](https://github.com/dotnet/SqlClient/pull/551)
- Fixed issues introduced with MARS TDS Header fix in last release by reverting original change that caused issues. [#550](https://github.com/dotnet/SqlClient/pull/550)


## [Preview Release 2.0.0-preview3.20122.2] - 2020-05-01

### Added
- Allow passing username with Active Directory Interactive Authentication in .NET Framework [#492](https://github.com/dotnet/SqlClient/pull/492)
- Allow large UDT buffers for .NET Framework [#456](https://github.com/dotnet/SqlClient/pull/456)
- Added "Transaction Id" and "Client Version" in Diagnostic Source traces [#515](https://github.com/dotnet/SqlClient/pull/515)
- Added new `SqlConnectionOverrides` APIs to perform `SqlConnection.Open()` with fail fast option [#463](https://github.com/dotnet/SqlClient/pull/463)

### Fixed
- Addressed MARS TDS Header errors by reverting changes to make `SqlDataReader.ReadAsync()` non-blocking [#547](https://github.com/dotnet/SqlClient/pull/547)
- Fixed driver behavior to not perform enlistment of pooled connection in aborted transaction [#543](https://github.com/dotnet/SqlClient/pull/543)
- Fixed wrong application domain selected when starting `SqlDependencyListener` [#410](https://github.com/dotnet/SqlClient/pull/410)
- Added missing refs for `RowCopied` property in `SqlBulkCopy` [#508](https://github.com/dotnet/SqlClient/pull/508)

### Changed
- Improved performance by removing unwanted method calls in Event Source tracing [#506](https://github.com/dotnet/SqlClient/pull/506)
- Removed Diagnostic Source and Configuration Manager dependencies from .NET Standard implementation [#535](https://github.com/dotnet/SqlClient/pull/535)
- Removed redundant calls to `DbConnectionPoolKey.GetType()` [#512](https://github.com/dotnet/SqlClient/pull/512)

### Breaking Changes
- Updated driver to perform decimal scale rounding to match SQL Server behavior [#470](https://github.com/dotnet/SqlClient/pull/470)
- Standardized App Context switch name that enables Managed SNI on Windows for .NET Core and .NET Standard (break only applies to 2.0 preview releases that introduced the switch) [#548](https://github.com/dotnet/SqlClient/pull/548)


## [Stable Release 1.1.2] - 2020-04-15

### Added
- Allowed passing username with Active Directory Interactive Authentication [#493](https://github.com/dotnet/SqlClient/pull/493) [#516](https://github.com/dotnet/SqlClient/pull/516)

### Fixed
- Fixed the ConnectionString's password persistence in .NET Core. [#489](https://github.com/dotnet/SqlClient/pull/489)
- Addressed MARS TDS header containing errors [#510](https://github.com/dotnet/SqlClient/pull/510)

### Changed
- Updated driver libraries to be CLS Compliant [#522](https://github.com/dotnet/SqlClient/pull/522)


## [Preview Release 2.0.0-preview2.20084.1] - 2020-03-24

### Added
- Added support for capturing EventSource traces in .NET Framework, .NET Core, and .NET Standard applications [#399](https://github.com/dotnet/SqlClient/pull/399) [#461](https://github.com/dotnet/SqlClient/pull/461) [#479](https://github.com/dotnet/SqlClient/pull/479) [#483](https://github.com/dotnet/SqlClient/pull/483) [#484](https://github.com/dotnet/SqlClient/pull/484)
- Added support for Cross-platform TCP Keep Alive applicable to .NET Core 3.1+ applications [#395](https://github.com/dotnet/SqlClient/pull/395)
- Added support for enabling Managed networking implementation on Windows applicable to .NET Core and .NET Standard applications [#477](https://github.com/dotnet/SqlClient/pull/477)
- Added `RowsCopied` property in `SqlBulkCopy` to expose count of copied rows [#409](https://github.com/dotnet/SqlClient/pull/409)
- Added "NeutralResourcesLanguage" attribute for .NET Framework assembly [#433](https://github.com/dotnet/SqlClient/pull/433)
- Added caching for invariant culture check result [#376](https://github.com/dotnet/SqlClient/pull/376)
- Added cached `SqlReferenceCollection.FindLiveReaderContext` objects [#380](https://github.com/dotnet/SqlClient/pull/380)

### Fixed
- Fixed Access Token behavior in connection pool to perform string comparison [#443](https://github.com/dotnet/SqlClient/pull/443)
- Fixed concurrent connection speed issues when connecting with Azure Active Directory Authentication modes in .NET Core [#466](https://github.com/dotnet/SqlClient/pull/466)
- Fixed issues with `Password` persistence in Connection String [#453](https://github.com/dotnet/SqlClient/pull/453)

### Changed
- Updated all driver assemblies to be CLS Compliant [#396](https://github.com/dotnet/SqlClient/pull/396)
- Updated Bulk Copy error messages to also include Column, Row and non-encrypted Data information [#437](https://github.com/dotnet/SqlClient/pull/437)
- Updated error messages for "Always Encrypted - Secure Enclaves" to handle 'Attestation Protocol' and fixed typos [#421](https://github.com/dotnet/SqlClient/pull/421) [#397](https://github.com/dotnet/SqlClient/pull/397)
- Removed sync over async in `SNINpHandle.EnableSsl` [#474](https://github.com/dotnet/SqlClient/pull/474)
- Changed non-generic `ArrayList` to `List<T>` in `SqlBulkCopy` [#457](https://github.com/dotnet/SqlClient/pull/457)
- Multiple performance improvements [#377](https://github.com/dotnet/SqlClient/pull/377) [#378](https://github.com/dotnet/SqlClient/pull/378) [#379](https://github.com/dotnet/SqlClient/pull/379)

### Breaking Changes
- The driver will now perform Server Certificate validation when TLS encryption is enforced by the target Server, which is the default for Azure connections [#391](https://github.com/dotnet/SqlClient/pull/391)
- `SqlDataReader.GetSchemaTable()` now returns an empty `DataTable` instead of returning `null` [#419](https://github.com/dotnet/SqlClient/pull/419)


## [Stable Release 1.1.1] - 2020-02-14

### Fixed
- Fixed deadlock issues by reverting async changes to `SNIPacket` [#425](https://github.com/dotnet/SqlClient/pull/425)

### Changed
- Updated SNI package reference to include version range [#425](https://github.com/dotnet/SqlClient/pull/425)


## [Preview Release 2.0.0-preview1.20021.1] - 2020-01-21

### Added
- Added support to allow large UDT buffer size (_upto_ `Int.MaxValue`) as supported by SQL Server starting TDS 7.3 [#340](https://github.com/dotnet/SqlClient/pull/340)

### Fixed
- Fixed issues with `SqlCommandSet` not working with Byte Array parameters [#360](https://github.com/dotnet/SqlClient/pull/360)
- Fixed Statement command cancellation in Managed SNI [#248](https://github.com/dotnet/SqlClient/pull/248) - Ported [dotnet/corefx#38271](https://github.com/dotnet/corefx/pull/38271)
- Fixed zero connection timeout issue in Managed SNI [#332](https://github.com/dotnet/SqlClient/pull/332)
- Fixed "DataType" metadata information for TinyInt datatype to be `System.Byte` [#338](https://github.com/dotnet/SqlClient/pull/338)
- Fixed driver behavior to use `CancellationTokenResource` only for non-infinite timeout and cleanup after usage [#339](https://github.com/dotnet/SqlClient/pull/339)
- Fixed `ConnectionTime` and `ClientConnectionId` reported by `SqlStatistics` when connection is closed [#341](https://github.com/dotnet/SqlClient/pull/341)
- Fixed deadlock issues by reverting async changes to `SNIPacket` [#349](https://github.com/dotnet/SqlClient/pull/349)

### Changed
- Improved performance of Managed SNI by removing double fetch of domain name [#366](https://github.com/dotnet/SqlClient/pull/366)
- Improved performance of Async Method Allocations in Managed SNI [#328](https://github.com/dotnet/SqlClient/pull/328)
- Improved performance of Managed SNI by enhancing utilization of resources [#173](https://github.com/dotnet/SqlClient/pull/173) - Ported [dotnet/corefx#35363](https://github.com/dotnet/corefx/pull/35363) and [dotnet/corefx#40732](https://github.com/dotnet/corefx/pull/40732)
- Improved performance of Managed SNI RPC Parameter Usage [#209](https://github.com/dotnet/SqlClient/pull/209) - Ported [dotnet/corefx#34049](https://github.com/dotnet/corefx/pull/34049)
- Changed enclave key map to be lazy initialized [#372](https://github.com/dotnet/SqlClient/pull/372)
- Changed `Recieve()` and `ReceiveAsync()` implementation to receive null packets on failure [#350](https://github.com/dotnet/SqlClient/pull/350)
- Changed `EnclaveProviderBase` caching implementation to support Async Scenarios  _(Introduces breaking changes)_ [#346](https://github.com/dotnet/SqlClient/pull/346)


## [Stable Release 1.1.0] - 2019-11-20

### Added
- Added support for |DataDirectory| macro in `AttachDBFilename` for .NET Core client [#284](https://github.com/dotnet/SqlClient/pull/284)

### Fixed
- Fixed connection resiliency check [#310](https://github.com/dotnet/SqlClient/pull/310)
- Fixed `SNIPacket.ReadFromStreamAsync` to not consume same `ValueTask` twice [#295](https://github.com/dotnet/SqlClient/pull/295)
- Fixed driver behavior to not send Attention signal for successful Bulk Copy operation [#308](https://github.com/dotnet/SqlClient/pull/308)
- Fixed driver behavior to abort connection when encountering `SqlException` on `SqlTransaction.Commit` [#299](https://github.com/dotnet/SqlClient/pull/299)
- Fixed driver behavior to not throw exception on invalid *app.config* files [#319](https://github.com/dotnet/SqlClient/pull/319)

### Changed
- Improved async read performance by adding multi-packet target buffer caching [#285](https://github.com/dotnet/SqlClient/pull/285)
- Improved performance of `TdsParserStateObject` and `SqlDataReader` snapshot mechanisms [#198](https://github.com/dotnet/SqlClient/pull/198)
- Updated `SqlDataReader.Close` documentation [#314](https://github.com/dotnet/SqlClient/pull/314)


## [Preview Release 1.1.0-preview2.19309.1] - 2019-11-04

### Added
- Add support for secure enclaves with Always Encrypted [#293](https://github.com/dotnet/SqlClient/pull/293)

### Fixed
- Setting the value `DbParameter.DbType` to `DbType.Time` property fails after setting the Value property [#5](https://github.com/dotnet/SqlClient/issues/5)
- `SQLDataAdapter.FillSchema` doesn't mark computed columns as readonly [#275](https://github.com/dotnet/SqlClient/issues/275)
- `SqlDependency.Start` throws `FileNotFoundException` [#260](https://github.com/dotnet/SqlClient/issues/260)
- Misleading `ADP_OpenReaderExists` exception message on MARS-disabled Sql Connection when incorrectly doing parallel requests [#82](https://github.com/dotnet/SqlClient/issues/82)
- SqlClient ManualTest `MARSSyncTimeoutTest` fails in managed mode [#108](https://github.com/dotnet/SqlClient/issues/108)
- `System.Data.SqlClient.SqlInternalConnectionTds` constructor purges original call stack when re-throwing an exception [#100](https://github.com/dotnet/SqlClient/issues/100)
- `InvalidOperationException(SqlException)` on `SqlBulkCopy` [#221](https://github.com/dotnet/SqlClient/issues/221)
- Exception message grammar: "An SqlParameter [...] is not contained by this `SqlParameterCollection`" [#159](https://github.com/dotnet/SqlClient/issues/159)
- Fixing incorrect event id and opcode for the `SqlEventSource` [#241](https://github.com/dotnet/SqlClient/pull/241)

### Changed
- Update dependency to Microsoft.Data.SqlClient.SNI v1.1.0 [#276](https://github.com/dotnet/SqlClient/pull/276)
- Correct timeout remarks for async command methods [#264](https://github.com/dotnet/SqlClient/pull/264)
- Improve `SqlBulkCopy` truncation error message [#256](https://github.com/dotnet/SqlClient/issues/256)
- Intellisense tooltip for `SqlCommand`'s `CommandTimeout` doesn't describe units [#33](https://github.com/dotnet/SqlClient/issues/33)
- Enable SQL Command text for non-stored procs in EventSource events for .NET Framework [242](https://github.com/dotnet/SqlClient/pull/242)
- Many test changes to support a public CI


## [Preview Release 1.1.0-preview1.19275.1] - 2019-10-02

### Added
- Added `SqlFileStream` support for .NET Framework with `Microsoft.Data.SqlTypes.SqlFileStream` class introduced. [#210](https://github.com/dotnet/SqlClient/pull/210)
- Added support for Visual Studio Intellisense with XML Documentation. [#210](https://github.com/dotnet/SqlClient/pull/210)

### Changed
- Synchronized ref definitions with driver classes. [#180](https://github.com/dotnet/SqlClient/pull/180)
- Updated `SNINativeMethodWrapper` to provide the underlying error in the inner exception when we fail to load SNI.dll. [#225](https://github.com/dotnet/SqlClient/pull/225)
- Added .editorconfig file and set formatting rules. [#193](https://github.com/dotnet/SqlClient/pull/193)
- Changes done to handle statistics well and to cleanup `AutoResetEvent` on disconnect. [#232](https://github.com/dotnet/SqlClient/pull/232)


## [Hotfix & Stable Release 1.0.19269.1] - 2019-09-26

### Fixed Issues
- `SqlCommand.StatementCompleted` event never being fired [#212](https://github.com/dotnet/SqlClient/issues/212)
- Added missing `Authentication` property to `SqlConnectionStringBuilder` reference assembly
- Reverted API changes in `SqlAuthenticationParameters` which had changed the `public string Resource` property to `public string[] Scopes`

## [Hotfix & Stable Release 1.0.19249.1] - 2019-09-06

### Fixed Issues
- Fixed issues with large data reading in Unix applications when data is spanned over multiple packets. [#171](https://github.com/dotnet/SqlClient/pull/171)

## [Stable Release 1.0.19239.1] - 2019-08-27

Initial release. Release Notes uploaded in [1.0.md](release-notes\1.0\1.0.md)
