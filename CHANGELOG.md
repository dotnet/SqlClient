# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [Preview Release 2.1.0-preview2.20297.7] - 2020-10-23

### Added
- Added support for Azure Active Directory Managed Identity authentication [#730](https://github.com/dotnet/SqlClient/pull/730)
- Added support to provide a user-defined application client ID when using Active Directory authentication [#740](https://github.com/dotnet/SqlClient/pull/740)
- Added the "Command Timeout" connection string property to set a default timeout for all commands executed with the connection [#722](https://github.com/dotnet/SqlClient/pull/722)
- Added support for Always Encrypted on all supported platforms for .NET Standard 2.0 [#756](https://github.com/dotnet/SqlClient/pull/756)

### Fixed
- Fixed unobserved exception issue when a timeout occurs before a faulted task completes with an exception [#688](https://github.com/dotnet/SqlClient/pull/688) [#773](https://github.com/dotnet/SqlClient/pull/773)
- Fixed an issue where driver continues to prompt for credentials when using Azure Active Directory authentication [#770](https://github.com/dotnet/SqlClient/pull/770)

### Changes
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

### Changes
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

### Changes
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

### Changes
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

### Changes
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

### Changes
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

### Changes
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

### Changes
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

### Changes
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

### Changes
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
