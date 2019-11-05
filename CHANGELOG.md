# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [Preview Release 1.1.0-preview1.19309.1] - 2019-11-04

### Added

- Add support for secure enclaves with Always Encrypted [#293](https://github.com/dotnet/SqlClient/pull/293)

### Fixed

- Setting the value DbParameter.DbType to DbType.Time property fails after setting the Value property [#5](https://github.com/dotnet/SqlClient/issues/5)
- SQLDataAdapter.FillSchema doesn't mark computed columns as readonly [#275](https://github.com/dotnet/SqlClient/issues/275)
- SqlDependency.Start throws FileNotFoundException [#260](https://github.com/dotnet/SqlClient/issues/260)
- Misleading ADP_OpenReaderExists exception message on MARS-disabled Sql Connection when incorrectly doing parallel requests [#82](https://github.com/dotnet/SqlClient/issues/82)
- SqlClient ManualTest MARSSyncTimeoutTest fails in managed mode [#108](https://github.com/dotnet/SqlClient/issues/108)
- System.Data.SqlClient.SqlInternalConnectionTds constructor purges original call stack when re-throwing an exception [#100](https://github.com/dotnet/SqlClient/issues/100)
- InvalidOperationException(SqlException) on SqlBulkCopy [#221](https://github.com/dotnet/SqlClient/issues/221)
- Exception message grammar: "An SqlParameter [...] is not contained by this SqlParameterCollection" [#159](https://github.com/dotnet/SqlClient/issues/159)
- Fixing incorrect event id and opcode for the SqlEventSource [#241](https://github.com/dotnet/SqlClient/pull/241)

### Changes

- Update dependency to Microsoft.Data.SqlClient.SNI v1.1.0 [#276](https://github.com/dotnet/SqlClient/pull/276)
- Correct timeout remarks for async command methods [#264](https://github.com/dotnet/SqlClient/pull/264)
- Improve SqlBulkCopy truncation error message [#256](https://github.com/dotnet/SqlClient/issues/256)
- Intellisense tooltip for SqlCommand's CommandTimeout doesn't describe units [#33](https://github.com/dotnet/SqlClient/issues/33)
- Enable SQL Command text for non-stored procs in EventSource events for .NET Framework [242](https://github.com/dotnet/SqlClient/pull/242)
- Many test changes to support a public CI


## [Preview Release 1.1.0-preview1.19275.1] - 2019-10-02

### Added
- Added SqlFileStream support for .NET Framework with `Microsoft.Data.SqlTypes.SqlFileStream` class introduced. [#210](https://github.com/dotnet/SqlClient/pull/210)
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
