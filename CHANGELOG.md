# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

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
