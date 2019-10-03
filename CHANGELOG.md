# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

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
