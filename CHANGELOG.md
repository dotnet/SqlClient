# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [Preview Release 7.0.0-preview2.25289.6] - 2025-10-16

This update brings the following changes since [7.0.0-preview1.25257.1]

### Bug Fixes

- Fixed a debug assertion in connection pool (no impact to production code)
  ([#3587](https://github.com/dotnet/SqlClient/pull/3587))
- Prevent uninitialized performance counters escaping `CreatePerformanceCounters`
  ([#3623](https://github.com/dotnet/SqlClient/pull/3623))
- Fix SetProvider to return immediately if user-defined authentication provider found
  ([#3620](https://github.com/dotnet/SqlClient/pull/3620))
- Allow SqlBulkCopy to operate on hidden columns
  ([#3590](https://github.com/dotnet/SqlClient/pull/3590))
- Fix connection pool concurrency issue
  ([#3632](https://github.com/dotnet/SqlClient/pull/3632))

### Added

- App context switch for ignoring server-provided failover partner
  ([#3625](https://github.com/dotnet/SqlClient/pull/3625))
- App context switch for enabling asynchronous multi-packet improvements
  ([#3605](https://github.com/dotnet/SqlClient/pull/3605))

### Changed

- Deprecation of `SqlAuthenticationMethod.ActiveDirectoryPassword`
  ([#3671](https://github.com/dotnet/SqlClient/pull/3671))

#### Other changes

- Improve performance in `SqlStatistics` by using `Environment.TickCount` for calculating execution timing 
  ([#3609](https://github.com/dotnet/SqlClient/pull/3609))

- Improve performance in Always Encrypted scenarios by using lower-allocation primitives
  ([#3612](https://github.com/dotnet/SqlClient/pull/3612))

- Various test improvements:
  ([#3423](https://github.com/dotnet/SqlClient/pull/3423),
   [#3488](https://github.com/dotnet/SqlClient/pull/3488),
   [#3624](https://github.com/dotnet/SqlClient/pull/3624),
   [#3638](https://github.com/dotnet/SqlClient/pull/3638),
   [#3642](https://github.com/dotnet/SqlClient/pull/3642),
   [#3678](https://github.com/dotnet/SqlClient/pull/3678),
   [#3690](https://github.com/dotnet/SqlClient/pull/3690))

- Codebase merge project and related cleanup:
  ([#3555](https://github.com/dotnet/SqlClient/pull/3555),
   [#3603](https://github.com/dotnet/SqlClient/pull/3603),
   [#3608](https://github.com/dotnet/SqlClient/pull/3608),
   [#3611](https://github.com/dotnet/SqlClient/pull/3611),
   [#3619](https://github.com/dotnet/SqlClient/pull/3619),
   [#3622](https://github.com/dotnet/SqlClient/pull/3622),
   [#3630](https://github.com/dotnet/SqlClient/pull/3630),
   [#3631](https://github.com/dotnet/SqlClient/pull/3631),
   [#3634](https://github.com/dotnet/SqlClient/pull/3634),
   [#3637](https://github.com/dotnet/SqlClient/pull/3637),
   [#3644](https://github.com/dotnet/SqlClient/pull/3644),
   [#3647](https://github.com/dotnet/SqlClient/pull/3647),
   [#3655](https://github.com/dotnet/SqlClient/pull/3655))

- Code health improvements:
  ([#3645](https://github.com/dotnet/SqlClient/pull/3645))

- Internal infrastructure improvements:
  ([#3659](https://github.com/dotnet/SqlClient/pull/3659),
   [#3692](https://github.com/dotnet/SqlClient/pull/3692),
   [#3695](https://github.com/dotnet/SqlClient/pull/3695))

- Updated Dependencies
  ([#3638](https://github.com/dotnet/SqlClient/pull/3638)):
  - Updated `Azure.Core` to v1.49.0
  - Updated `Azure.Identity` to v1.16.0
  - Updated `Azure.Security.KeyVault.Keys` v4.8.0 (AKV provider)
  - Updated `Microsoft.Bcl.Cryptography` to v9.0.9 (net9)
  - Updated `Microsoft.Extensions.Caching.Memory` to v9.0.9 (net9)
  - Updated `Microsoft.IdentityModel.JsonWebTokens` to v8.14.0
  - Updated `Microsoft.IdentityModel.Protocols.OpenIdConnect` to v8.14.0
  - Updated `System.Buffers` to v4.6.1 (net462)
  - Updated `System.Memory` to v4.6.3 (net462)
  - Updated `System.Configuration.ConfigurationManager` to v9.0.9 (net9)
  - Updated `System.Security.Cryptography.Pkcs` to v9.0.9 (net9)
  - Updated `System.Text.Json` to v8.0.6 (net8), v9.0.9 (net9)

## [Stable Release 6.1.2] - 2025-10-07

This update brings the below changes over the previous stable release:

### Fixed

- Fixed an issue where initializing PerformanceCounters would throw `System.InvalidOperationException` [#3629](https://github.com/dotnet/sqlclient/pull/3629)
- Fixed an issue where a Custom SqlClientAuthenticationProvider was being overwritten by default implementation. [#3651](https://github.com/dotnet/SqlClient/pull/3651)
- Fixed a concurrency issue in connection pooling where the number of active connections could be lower than the configured maximum pool size. [#3653](https://github.com/dotnet/SqlClient/pull/3653)

## [Stable release 6.0.3] - 2025-10-07

This update brings the below changes over the previous stable release:

### Fixed

- Fixed an issue where a Custom SqlClientAuthenticationProvider was being overwritten by default implementation. [#3652](https://github.com/dotnet/SqlClient/pull/3652)
- Fixed a concurrency issue in connection pooling where the number of active connections could be lower than the configured maximum pool size. [#3654](https://github.com/dotnet/SqlClient/pull/3654)

### Changed

- Updated MSAL usage as per code compliance requirements [#3360](https://github.com/dotnet/SqlClient/pull/3360)
- Updated `SqlDecimal` implementation to improve code compliance [#3466](https://github.com/dotnet/SqlClient/pull/3466)
- Updated Azure.Identity and related dependencies [#3553](https://github.com/dotnet/SqlClient/pull/3553)

## [Preview Release 7.0.0-preview1.25257.1] - 2025-09-12

This update brings the following changes since the [6.1.0](release-notes/6.1/6.1.0.md)
release:

### Breaking Changes

- Removed `Constrained Execution Region` error handling blocks and associated
  `SqlConnection` cleanup which may affect how potentially-broken connections
  are expunged from the pool.
  ([#3535](https://github.com/dotnet/SqlClient/pull/3535))

### Bug Fixes

- Packet multiplexing disabled by default, and several bug fixes.
  ([#3534](https://github.com/dotnet/SqlClient/pull/3534),
   [#3537](https://github.com/dotnet/SqlClient/pull/3537))

### Added

- `SqlColumnEncryptionCertificateStoreProvider` now works on Windows, Linux,
  and macOS.
  ([#3014](https://github.com/dotnet/SqlClient/pull/3014))

### Changed

- Updated `SqlVector.Null` to return a nullable `SqlVector` instance in the
  reference API to match the implementation.
  ([#3521](https://github.com/dotnet/SqlClient/pull/3521))

- Performance improvements for all built-in
  `SqlColumnEncryptionKeyStoreProvider` implementations.
  ([#3554](https://github.com/dotnet/SqlClient/pull/3554))

- Various test improvements.
  ([#3456](https://github.com/dotnet/SqlClient/pull/3456),
   [#2968](https://github.com/dotnet/SqlClient/pull/2968),
   [#3458](https://github.com/dotnet/SqlClient/pull/3458),
   [#3494](https://github.com/dotnet/SqlClient/pull/3494),
   [#3559](https://github.com/dotnet/SqlClient/pull/3559),
   [#3575](https://github.com/dotnet/SqlClient/pull/3575))

- Codebase merge project and related cleanup.
  ([#3436](https://github.com/dotnet/SqlClient/pull/3436),
   [#3434](https://github.com/dotnet/SqlClient/pull/3434),
   [#3448](https://github.com/dotnet/SqlClient/pull/3448),
   [#3454](https://github.com/dotnet/SqlClient/pull/3454),
   [#3462](https://github.com/dotnet/SqlClient/pull/3462),
   [#3435](https://github.com/dotnet/SqlClient/pull/3435),
   [#3492](https://github.com/dotnet/SqlClient/pull/3492),
   [#3473](https://github.com/dotnet/SqlClient/pull/3473),
   [#3469](https://github.com/dotnet/SqlClient/pull/3469),
   [#3394](https://github.com/dotnet/SqlClient/pull/3394),
   [#3493](https://github.com/dotnet/SqlClient/pull/3493),
   [#3593](https://github.com/dotnet/SqlClient/pull/3593))

- Documentation improvements.
  ([#3490](https://github.com/dotnet/SqlClient/pull/3490))

- Updated `Azure.Identity` dependency to v1.14.2.
  ([#3538](https://github.com/dotnet/SqlClient/pull/3538))

## [Stable Release 6.1.1] - 2025-08-14

This update includes the following changes since the [6.1.0](6.1.0.md) release:

### Fixed

- Reverted changes related to improving partial packet detection, fixup, and replay functionality. This revert addresses regressions introduced in 6.1.0. ([#3556](https://github.com/dotnet/SqlClient/pull/3556))
- Applied reference assembly corrections supporting vector, fixed JSON tests, and ensured related tests are enabled. [#3562](https://github.com/dotnet/SqlClient/pull/3562)
- Fixed `SqlVector<T>.Null` API signature in Reference assembly. [#3521](https://github.com/dotnet/SqlClient/pull/3521)

### Changed

- Upgraded `Azure.Identity` and other dependencies to newer versions. ([#3538](https://github.com/dotnet/SqlClient/pull/3538)) ([#3552](https://github.com/dotnet/SqlClient/pull/3552))

## [Stable Release 6.1.0] - 2025-07-25

This update brings the following changes since the
[6.1.0-preview2](release-notes/6.1/6.1.0-preview2.md) release:

### Added

No new features were added.

### Fixed

- Fixed missing socket error codes on non-Windows platforms.
  ([#3475](https://github.com/dotnet/SqlClient/pull/3475))
- Fixed primary/secondary server SPN handling during SSPI negotiation.
  ([#3478](https://github.com/dotnet/SqlClient/pull/3478))
- Fixed AzureKeyVaultProvider package key caching to serialize Azure key fetch
  operations.
  ([#3477](https://github.com/dotnet/SqlClient/pull/3477))
- Fixed a rare error related to multi-packet async text reads.
  ([#3474](https://github.com/dotnet/SqlClient/pull/3474))
- Fixed some spelling errors in the API docs.
  ([#3500](https://github.com/dotnet/SqlClient/pull/3500))
- Fixed a rare multi-packet string corruption bug.
  ([#3513](https://github.com/dotnet/SqlClient/pull/3513))

### Changed

#### SqlDecimal type workarounds conversions

*What Changed:*

- Changed how SqlDecimal type workarounds perform conversions to meet
  compliance policies.
  ([#3467](https://github.com/dotnet/SqlClient/pull/3467))

*Who Benefits:*

- Microsoft products must not use undocumented APIs on other Microsoft products.
  This change removes calls to undocumented APIs and replaces them with
  compliant API use.

*Impact:*

- These changes impose an observed 5% decrease in performance on .NET Framework.

#### SqlVector API improvements

*What Changed:*

- Several changes were made to the SqlVector API published in the
  [6.1.0-preview2](release-notes/6.1/6.1.0-preview2.md) release
  ([#3472](https://github.com/dotnet/SqlClient/pull/3472)):
  - The SqlVector class was changed to a readonly struct.
  - The null value constructor was changed to a static `CreateNull()` method.
  - The `Size` property was removed.

*Who Benefits:*

- SqlVector instances gain the efficiencies of struct handling.

*Impact:*

- Early-adopter applications may require updates if they rely on the old APIs
  and any class-specific behaviour.

## [Preview Release 6.1.0-preview2.25178.5] - 2025-06-27

This update brings the following changes since the
[6.1.0-preview1](release-notes/6.1/6.1.0-preview1.md) release:

### Added

#### Added dedicated SQL Server vector datatype support

*What Changed:*

- Optimized vector communications between MDS and SQL Server 2025, employing a
  custom binary format over the TDS protocol.
  ([#3433](https://github.com/dotnet/SqlClient/pull/3433),
   [#3443](https://github.com/dotnet/SqlClient/pull/3443))
- Reduced processing load compared to existing JSON-based vector support.
- Initial support for 32-bit single-precision floating point vectors.

*Who Benefits:*

- Applications moving large vector data sets will see beneficial improvements
  to processing times and memory requirements.
- Vector-specific APIs are ready to support future numeric representations with
  a consistent look-and-feel.

*Impact:*

- Reduced transmission and processing times for vector operations versus JSON
  using SQL Server 2025 preview:
  - Reads:  50x improvement
  - Writes: 3.3x improvement
  - Bulk Copy: 19x improvement
  - (Observed with vector column of max 1998 size, and 10,000 records for each
    operation.)
- Improved memory footprint due to the elimination of JSON
  serialization/deserialization and string representation bloat.
- For backwards compatibility with earlier SQL Server Vector implementations,
  applications may continue to use JSON strings to send/receive vector data,
  although they will not see any of the performance improvements noted above.

#### Revived .NET Standard 2.0 target support

*What Changed:*

- Support for targeting .NET Standard 2.0 has returned.
  ([#3381](https://github.com/dotnet/SqlClient/pull/3381))
- Support had previously been removed in the 6.0 release, with the
  [community voicing concerns](https://github.com/dotnet/SqlClient/discussions/3115).

*Who Benefits:*

- Libraries that depend on MDS may seamlessly target any of the following
  frameworks:
  - .NET Standard 2.0
  - .NET Framework 4.6.2 and above
  - .NET 8.0
  - .NET 9.0
- Applications should continue to target runtimes.
  - The MDS .NET Standard 2.0 target framework support does not include an
    actual implementation, and cannot be used with a runtime.
  - An application's build/publish process should always pick the appropriate
    MDS .NET/.NET Framework runtime implementation.
  - Custom build/publish actions that incorrectly try to deploy the MDS .NET
    Standard 2.0 reference DLL at runtime are not supported.

*Impact:*

- Libraries targeting .NET Standard 2.0 will no longer receive warnings like
  this:
  - `warning NU1701: Package 'Microsoft.Data.SqlClient 6.0.2' was restored using '.NETFramework,Version=v4.6.1, .NETFramework,Version=v4.6.2, .NETFramework,Version=v4.7, .NETFramework,Version=v4.7.1, .NETFramework,Version=v4.7.2, .NETFramework,Version=v4.8, .NETFramework,Version=v4.8.1' instead of the project target framework '.NETStandard,Version=v2.0'. This package may not be fully compatible with your project.`

### Fixed

- Fixed missing &lt;NeutralLanguage&gt; property.
  ([#3325](https://github.com/dotnet/SqlClient/pull/3325))
- Fixed injection of UTF-8 BOM during bulk copy.
  ([#3399](https://github.com/dotnet/SqlClient/pull/3399))
- Fixed `SqlCachedBuffer` async read edge case.
  ([#3329](https://github.com/dotnet/SqlClient/pull/3329))
- Fixed `SqlSequentialTextReader` edge case with single-byte reads.
  ([#3383](https://github.com/dotnet/SqlClient/pull/3383))
- Fixed an incorrect error message when parsing connection string `PoolBlockingPeriod`.
  ([#3411](https://github.com/dotnet/SqlClient/pull/3411))
- Added missing `ToString()` override to `SqlJson`.
  ([#3427](https://github.com/dotnet/SqlClient/pull/3427))

### Changed

- Reduced allocations when opening a connection.
  ([#3364](https://github.com/dotnet/SqlClient/pull/3364))
- Various performance improvements related to TDS parsing.
  ([#3337](https://github.com/dotnet/SqlClient/pull/3337),
   [#3377](https://github.com/dotnet/SqlClient/pull/3377),
   [#3422](https://github.com/dotnet/SqlClient/pull/3422))
- Improved native AOT support.
  ([#3364](https://github.com/dotnet/SqlClient/pull/3364),
   [#3369](https://github.com/dotnet/SqlClient/pull/3369),
   [#3401](https://github.com/dotnet/SqlClient/pull/3401))
- Progress towards [SSPI extensibility](https://github.com/dotnet/SqlClient/issues/2253).
  ([#2454](https://github.com/dotnet/SqlClient/pull/2454))
- Progress towards [connection pooling improvements](https://github.com/dotnet/SqlClient/issues/3356).
  ([#3352](https://github.com/dotnet/SqlClient/pull/3352),
   [#3396](https://github.com/dotnet/SqlClient/pull/3396))
- Expanded/clarified SqlConnection's
  [AccessToken](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.accesstoken) and
  [AccessTokenCallback](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.accesstokencallback)
  documentation.
  ([#3339](https://github.com/dotnet/SqlClient/pull/3339))
- Fixed some poorly formatted tables in the API docs.
  ([#3391](https://github.com/dotnet/SqlClient/pull/3391))
- Code merge towards a unified SqlClient project, aligning .NET Framework and
  .NET Core implementations.
  ([#3262](https://github.com/dotnet/SqlClient/pull/3262),
   [#3291](https://github.com/dotnet/SqlClient/pull/3291),
   [#3305](https://github.com/dotnet/SqlClient/pull/3305),
   [#3306](https://github.com/dotnet/SqlClient/pull/3306),
   [#3310](https://github.com/dotnet/SqlClient/pull/3310),
   [#3323](https://github.com/dotnet/SqlClient/pull/3323),
   [#3326](https://github.com/dotnet/SqlClient/pull/3326),
   [#3335](https://github.com/dotnet/SqlClient/pull/3335),
   [#3338](https://github.com/dotnet/SqlClient/pull/3338),
   [#3340](https://github.com/dotnet/SqlClient/pull/3340),
   [#3341](https://github.com/dotnet/SqlClient/pull/3341),
   [#3343](https://github.com/dotnet/SqlClient/pull/3343),
   [#3345](https://github.com/dotnet/SqlClient/pull/3345),
   [#3353](https://github.com/dotnet/SqlClient/pull/3353),
   [#3355](https://github.com/dotnet/SqlClient/pull/3355),
   [#3368](https://github.com/dotnet/SqlClient/pull/3368),
   [#3373](https://github.com/dotnet/SqlClient/pull/3373),
   [#3376](https://github.com/dotnet/SqlClient/pull/3376),
   [#3388](https://github.com/dotnet/SqlClient/pull/3388),
   [#3389](https://github.com/dotnet/SqlClient/pull/3389),
   [#3393](https://github.com/dotnet/SqlClient/pull/3393),
   [#3405](https://github.com/dotnet/SqlClient/pull/3405),
   [#3414](https://github.com/dotnet/SqlClient/pull/3414),
   [#3416](https://github.com/dotnet/SqlClient/pull/3416),
   [#3417](https://github.com/dotnet/SqlClient/pull/3417),
   [#3420](https://github.com/dotnet/SqlClient/pull/3420),
   [#3431](https://github.com/dotnet/SqlClient/pull/3431),
   [#3438](https://github.com/dotnet/SqlClient/pull/3438))
- Test improvements include a new unit test project, updates to test
  dependencies, removal of hardcoded credentials, and improved robustness.
  ([#3204](https://github.com/dotnet/SqlClient/pull/3204),
   [#3379](https://github.com/dotnet/SqlClient/pull/3379),
   [#3380](https://github.com/dotnet/SqlClient/pull/3380),)
   [#3402](https://github.com/dotnet/SqlClient/pull/3402)
- Added dependency on `System.Text.Json`
  [8.0.5](https://www.nuget.org/packages/System.Text.Json/8.0.5) (.NET 8.0) and
  [9.0.5](https://www.nuget.org/packages/System.Text.Json/9.0.5) (.NET Standard 2.0, .NET 9.0)
  to avoid transitive vulnerabilities ([CVE-2024-43485](https://github.com/advisories/GHSA-8g4q-xg66-9fp4)).
  ([#3403](https://github.com/dotnet/SqlClient/pull/3403))

## [Preview Release 6.1.0-preview1.25120.4] - 2025-04-30

This update brings the following changes over the previous release:

### Added

- Added packet multiplexing support to improve large data read performance. [#2714](https://github.com/dotnet/SqlClient/pull/2714) [#3161](https://github.com/dotnet/SqlClient/pull/3161) [#3202](https://github.com/dotnet/SqlClient/pull/3202)
- Added support for special casing with Fabric endpoints. [#3084](https://github.com/dotnet/SqlClient/pull/3084)

### Fixed

- Fixed distributed transactions to be preserved during pooled connection resets. [#3019](https://github.com/dotnet/SqlClient/pull/3019).
- Fixed application crash when the `Data Source` parameter begins with a comma. [#3250](https://github.com/dotnet/SqlClient/pull/3250).
- Resolved synonym count discrepancies in debug mode. [#3098](https://github.com/dotnet/SqlClient/pull/3098).
- Addressed warnings for down-level SSL/TLS versions. [#3126](https://github.com/dotnet/SqlClient/pull/3126).

### Changed

- Optimized binary size for AOT. [#3091](https://github.com/dotnet/SqlClient/pull/3091)
- Refined bulk copy operations to handle unmatched column names more effectively. [#3205](https://github.com/dotnet/SqlClient/pull/3205).
- Enhanced `SqlBulkCopy` to explicitly identify mismatched column names. [#3183](https://github.com/dotnet/SqlClient/pull/3183).
- Optimized outgoing SSPI blob handling using `IBufferWriter<byte>`. [#2452](https://github.com/dotnet/SqlClient/pull/2452).
- Replaced `byte[]` with `string` for SNI to improve efficiency. [#2790](https://github.com/dotnet/SqlClient/pull/2790).
- Code cleanup to remove SQL 2000 support. [#2839](https://github.com/dotnet/SqlClient/pull/2839), [#3206](https://github.com/dotnet/SqlClient/pull/3206), [#3217](https://github.com/dotnet/SqlClient/pull/3217)
- Connection pool design refactor for a modular connection pool design. [#3199](https://github.com/dotnet/SqlClient/pull/3199)
- Updated various dependencies [#3229](https://github.com/dotnet/SqlClient/pull/3229), primarily:
  - System.Text.Encodings.Web to v8.0.0
  - System.Text.Json to v8.0.5
  - Azure.Identity to v1.13.2
  - Microsoft.Identity.Model.Json.Web.Tokens to v7.7.1
  - Microsoft.Identity.Model.Protocols.OpenIdConnect to v7.7.1
- Code merge towards a unified SqlClient project, aligning .NET Framework and .NET Core implementations. ([#2957](https://github.com/dotnet/sqlclient/pull/2957), [#2963](https://github.com/dotnet/sqlclient/pull/2963), [#2984](https://github.com/dotnet/sqlclient/pull/2984), [#2982](https://github.com/dotnet/sqlclient/pull/2982), [#3023](https://github.com/dotnet/sqlclient/pull/3023), [#3015](https://github.com/dotnet/sqlclient/pull/3015), [#2967](https://github.com/dotnet/sqlclient/pull/2967), [#3164](https://github.com/dotnet/sqlclient/pull/3164), [#3163](https://github.com/dotnet/sqlclient/pull/3163), [#3171](https://github.com/dotnet/sqlclient/pull/3171), [#3182](https://github.com/dotnet/sqlclient/pull/3182), [#3179](https://github.com/dotnet/sqlclient/pull/3179), [#3156](https://github.com/dotnet/sqlclient/pull/3156), [#3213](https://github.com/dotnet/sqlclient/pull/3213), [#3232](https://github.com/dotnet/sqlclient/pull/3232), [#3236](https://github.com/dotnet/sqlclient/pull/3236), [#3231](https://github.com/dotnet/sqlclient/pull/3231), [#3241](https://github.com/dotnet/sqlclient/pull/3241), [#3246](https://github.com/dotnet/sqlclient/pull/3246), [#3247](https://github.com/dotnet/sqlclient/pull/3247), [#3222](https://github.com/dotnet/sqlclient/pull/3222), [#3255](https://github.com/dotnet/sqlclient/pull/3255), [#3254](https://github.com/dotnet/sqlclient/pull/3254), [#3259](https://github.com/dotnet/sqlclient/pull/3259), [#3264](https://github.com/dotnet/sqlclient/pull/3264), [#3256](https://github.com/dotnet/sqlclient/pull/3256), [#3251](https://github.com/dotnet/sqlclient/pull/3251), [#3275](https://github.com/dotnet/sqlclient/pull/3275), [#3277](https://github.com/dotnet/sqlclient/pull/3277), [#3263](https://github.com/dotnet/sqlclient/pull/3263), [#3292](https://github.com/dotnet/sqlclient/pull/3292), [#3208](https://github.com/dotnet/sqlclient/pull/3208)).
- Test improvements include updates to test references, removal of hardcoded certificates, improved stability, and better coverage ([#3041](https://github.com/dotnet/sqlclient/pull/3041), [#3034](https://github.com/dotnet/sqlclient/pull/3034), [#3130](https://github.com/dotnet/sqlclient/pull/3130), [#3128](https://github.com/dotnet/sqlclient/pull/3128), [#3181](https://github.com/dotnet/sqlclient/pull/3181), [#3060](https://github.com/dotnet/sqlclient/pull/3060), [#3184](https://github.com/dotnet/sqlclient/pull/3184), [#3033](https://github.com/dotnet/sqlclient/pull/3033), [#3186](https://github.com/dotnet/sqlclient/pull/3186), [#3025](https://github.com/dotnet/sqlclient/pull/3025), [#3230](https://github.com/dotnet/sqlclient/pull/3230), [#3237](https://github.com/dotnet/sqlclient/pull/3237), [#3059](https://github.com/dotnet/sqlclient/pull/3059), [#3061](https://github.com/dotnet/sqlclient/pull/3061)).

## [Stable release 6.0.2] - 2025-04-25

This update brings the below changes over the previous release:

### Fixed

- Fixed possible `NullPointerException` during socket receive [#3283](https://github.com/dotnet/SqlClient/pull/3283)
- Fixed reference assembly definitions for SqlJson APIs [#3169](https://github.com/dotnet/SqlClient/pull/3169)
- Fixed an error reading the output parameter of type JSON while executing stored procedure [#3173](https://github.com/dotnet/SqlClient/pull/3173)

### Changed

- Updated the below dependencies:
  - Updated [Microsoft.Bcl.Cryptography](https://www.nuget.org/packages/Microsoft.Bcl.Cryptography/9.0.4) from 9.0.0 to 9.0.4 for .NET 9 targeted dll. [#3281](https://github.com/dotnet/SqlClient/pull/3281)
  - Updated [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/9.0.4) from 9.0.0 to 9.0.4 for .NET 9 targeted dll. [#3281](https://github.com/dotnet/SqlClient/pull/3281)
  - Updated [System.Configuration.ConfigurationManager](https://www.nuget.org/packages/System.Configuration.ConfigurationManager/9.0.4) from 9.0.0 to 9.0.4 for .NET 9 targeted dll. [#3281](https://github.com/dotnet/SqlClient/pull/3281)
  - Updated [System.Security.Cryptography.Pkcs](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/9.0.4) from 9.0.0 to 9.0.4 for .NET 9 targeted dll. [#3281](https://github.com/dotnet/SqlClient/pull/3281)

## [Stable release 6.0.1] - 2025-01-23

This update brings the below changes over the previous release:

### Fixed

- Fixed reference assembly definitions for SqlClientDiagnostic APIs [#3097](https://github.com/dotnet/SqlClient/pull/3097)
- Fixed issue with down-level SSL/TLS version warnings [#3126](https://github.com/dotnet/SqlClient/pull/3126)

### Changed

- Dependency changes
  - Updated SNI dependency `Microsoft.Data.SqlClient.SNI` and `Microsoft.Data.SqlClient.SNI.runtime` to `6.0.2` [#3116](https://github.com/dotnet/SqlClient/pull/3116) [#3117](https://github.com/dotnet/SqlClient/pull/3117)

## [Stable release 6.0.0] - 2024-12-09

_No changes since the last preview release_

## [Preview Release 6.0.0-preview3.24332.3] - 2024-11-27

This update brings the below changes over the previous release:

### Breaking Changes
- Dropped support for .NET 6 [#2927](https://github.com/dotnet/SqlClient/pull/2927)
- Removed SQL 2000 client-side debugging support for .NET Framework [#2981](https://github.com/dotnet/SqlClient/pull/2981), [#2940](https://github.com/dotnet/SqlClient/pull/2940)

### Added
- Enabled NuGet package auditing via NuGet.org audit source [#3024](https://github.com/dotnet/SqlClient/pull/3024)
- Added support for .NET 9 [#2946](https://github.com/dotnet/SqlClient/pull/2946)
- Added dependency on System.Security.Cryptography.Pkcs:9.0.0 to address [SYSLIB0057](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0057)[#2946](https://github.com/dotnet/SqlClient/pull/2946)
- Added dependency on Microsoft.Bcl.Cryptography:9.0.0 [#2946](https://github.com/dotnet/SqlClient/pull/2946)
- Added missing SqlCommand_BeginExecuteReader code sample [#3009](https://github.com/dotnet/SqlClient/pull/3009)
- Added support for SqlConnectionOverrides in OpenAsync() API [#2433](https://github.com/dotnet/SqlClient/pull/2433)
- Added localization in Czech, Polish, and Turkish [#2987](https://github.com/dotnet/SqlClient/pull/2987)

### Fixed
- Reverted default value of UseMinimumLoginTimeout context switch to 'true' [#2419](https://github.com/dotnet/SqlClient/pull/2419)
- Added missing DynamicallyAccessedMembers attributes in .NET Runtime reference assemblies. [#2946](https://github.com/dotnet/SqlClient/pull/2946)
- Synchronized dependencies of Reference Assemblies with Runtime assemblies [#2878](https://github.com/dotnet/SqlClient/pull/2878)
- Fixed lazy initialization of the _SqlMetaData hidden column map for .NET Framework [#2964](https://github.com/dotnet/SqlClient/pull/2964)

### Changed
- Updated Microsoft.Extensions.Caching.Memory to 9.0.0 for all frameworks [#2946](https://github.com/dotnet/SqlClient/pull/2946)
- Updated System.Configuration.ConfigurationManager to 9.0.0 [#2946](https://github.com/dotnet/SqlClient/pull/2946)
- Updated docs to use absolute links [#2949](https://github.com/dotnet/SqlClient/pull/2949)
- Removed System.Text.Json dependency from .NET 8 [#2930](https://github.com/dotnet/SqlClient/pull/2930)

## [Preview Release 6.0.0-preview2.24304.8] - 2024-10-30

This update brings the below changes over the previous release:

### Added

- Added a dependency on System.Text.Json 8.0.5 for .NET 8+ and 6.0.10 for other versions [#2921](https://github.com/dotnet/SqlClient/pull/2921)
- Added support for JSON datatype [#2916](https://github.com/dotnet/SqlClient/pull/2916), [#2892](https://github.com/dotnet/SqlClient/pull/2892), [#2891](https://github.com/dotnet/SqlClient/pull/2891), [#2880](https://github.com/dotnet/SqlClient/pull/2880), [#2882](https://github.com/dotnet/SqlClient/pull/2882), [#2829](https://github.com/dotnet/SqlClient/pull/2829), [#2830](https://github.com/dotnet/SqlClient/pull/2830)
- Added readme to nuget package [#2826](https://github.com/dotnet/SqlClient/pull/2826)

### Fixed

- Fixed scale serialization when explicitly set to 0 [#2411](https://github.com/dotnet/SqlClient/pull/2411)
- Fixed issue blocking GetSchema commands from being enrolled into the current transaction [#2876](https://github.com/dotnet/SqlClient/pull/2876)
- Adjusted retry logic to allow errors with negative numbers to be considered transient [#2896](https://github.com/dotnet/SqlClient/pull/2896)
- Fixed string formatting in OutOfMemory exceptions [#2797](https://github.com/dotnet/SqlClient/pull/2797)
- Increased routing attempts to 10 in netcore for LoginNoFailover and added routing support to LoginWithFailover to standardize routing behavior between netcore and netfx [#2873](https://github.com/dotnet/SqlClient/pull/2873)
- Restructured documentation into XML format so that it displays correctly in visual studio [#2836](https://github.com/dotnet/SqlClient/pull/2836), [#2822](https://github.com/dotnet/SqlClient/pull/2822), [#2834](https://github.com/dotnet/SqlClient/pull/2834), [#2851](https://github.com/dotnet/SqlClient/pull/2851), [#2863](https://github.com/dotnet/SqlClient/pull/2863), [#2864](https://github.com/dotnet/SqlClient/pull/2864), [#2865](https://github.com/dotnet/SqlClient/pull/2865), [#2869](https://github.com/dotnet/SqlClient/pull/2869), [#2871](https://github.com/dotnet/SqlClient/pull/2871), [#2837](https://github.com/dotnet/SqlClient/pull/2837), [#2821](https://github.com/dotnet/SqlClient/pull/2821)
- Fixed cleanup behavior when column decryption fails. Prevents leaving stale data on the wire for pooled connections [#2843](https://github.com/dotnet/SqlClient/pull/2843), [#2825](https://github.com/dotnet/SqlClient/pull/2825)

### Changed

- Updated System.Configuration.ConfigurationManager from 8.0.0 to 8.0.1 for .Net 8 [#2921](https://github.com/dotnet/SqlClient/pull/2921)
- Updated Microsoft.Extensions.Caching.Memory from 8.0.0 to 8.0.1 for .Net 8 [#2921](https://github.com/dotnet/SqlClient/pull/2921)
- Code Health Improvements [#2915](https://github.com/dotnet/SqlClient/pull/2915), [#2844](https://github.com/dotnet/SqlClient/pull/2844), [#2812](https://github.com/dotnet/SqlClient/pull/2812), [#2805](https://github.com/dotnet/SqlClient/pull/2805), [#2897](https://github.com/dotnet/SqlClient/pull/2897), [#2376](https://github.com/dotnet/SqlClient/pull/2376), [#2814](https://github.com/dotnet/SqlClient/pull/2814), [#2889](https://github.com/dotnet/SqlClient/pull/2889), [#2885](https://github.com/dotnet/SqlClient/pull/2885), [#2854](https://github.com/dotnet/SqlClient/pull/2854), [#2835](https://github.com/dotnet/SqlClient/pull/2835), [#2442](https://github.com/dotnet/SqlClient/pull/2442), [#2820](https://github.com/dotnet/SqlClient/pull/2820), [#2831](https://github.com/dotnet/SqlClient/pull/2831), [#2907](https://github.com/dotnet/SqlClient/pull/2907), [#2910](https://github.com/dotnet/SqlClient/pull/2910), [#2898](https://github.com/dotnet/SqlClient/pull/2898), [#2928](https://github.com/dotnet/SqlClient/pull/2928), [#2929](https://github.com/dotnet/SqlClient/pull/2929), [#2936](https://github.com/dotnet/SqlClient/pull/2936), [#2939](https://github.com/dotnet/SqlClient/pull/2939)

## [Preview Release 6.0.0-preview1.24240.8] - 2024-08-27

This update brings the below changes over the previous release:

### Breaking Changes

- Removed support for .NET Standard. [#2386](https://github.com/dotnet/SqlClient/pull/2386)
- Removed UWP (uap) references. [#2483](https://github.com/dotnet/SqlClient/pull/2483)

### Added

- Added `TokenCredential` object to take advantage of token caching in `ActiveDirectoryAuthenticationProvider`. [#2380](https://github.com/dotnet/SqlClient/pull/2380)
- Added support for using `DateOnly` and `TimeOnly` in `DataTable` and `SqlDataRecord` structured parameters. [#2258](https://github.com/dotnet/SqlClient/pull/2258)
- Added `Microsoft.Data.SqlClient.Diagnostics.SqlClientDiagnostic` type in .NET. [#2226](https://github.com/dotnet/SqlClient/pull/2226)
- Added scope trace for `GenerateSspiClientContext`. [#2497](https://github.com/dotnet/SqlClient/pull/2497), [#2725](https://github.com/dotnet/SqlClient/pull/2725)

### Fixed

- Fixed `Socket.Connect` timeout issue caused by thread starvation. [#2777](https://github.com/dotnet/SqlClient/pull/2777)
- Fixed pending data with `SqlDataReader` against an encrypted column. [#2618](https://github.com/dotnet/SqlClient/pull/2618)
- Fixed Entra authentication when using infinite connection timeout in `ActiveDirectoryAuthenticationProvider`. [#2651](https://github.com/dotnet/SqlClient/pull/2651)
- Fixed `GetSchema` by excluding unsupported engines due to lack of support for `ASSEMBLYPROPERTY` function. [#2593](https://github.com/dotnet/SqlClient/pull/2593)
- Fixed SSPI retry negotiation with default port in .NET. [#2559](https://github.com/dotnet/SqlClient/pull/2559)
- Fixed assembly path in .NET 8.0 and `.AssemblyAttributes`. [#2550](https://github.com/dotnet/SqlClient/pull/2550)
- Fixed certificate chain validation. [#2487](https://github.com/dotnet/SqlClient/pull/2487)
- Fixed clone of `SqlConnection` to include `AccessTokenCallback`. [#2525](https://github.com/dotnet/SqlClient/pull/2525)
- Fixed issue with `DateTimeOffset` in table-valued parameters, which was introduced in 5.2. [#2453](https://github.com/dotnet/SqlClient/pull/2453)
- Fixed `ArgumentNullException` on `SqlDataRecord.GetValue` when using user-defined data type on .NET. [#2448](https://github.com/dotnet/SqlClient/pull/2448)
- Fixed `SqlBuffer` and `SqlGuid` when it's null. [#2310](https://github.com/dotnet/SqlClient/pull/2310)
- Fixed `SqlBulkCopy.WriteToServer` state in a consecutive calls. [#2375](https://github.com/dotnet/SqlClient/pull/2375)
- Fixed null reference exception with `SqlConnection.FireInfoMessageEventOnUserErrors` after introducing the batch command. [#2399](https://github.com/dotnet/SqlClient/pull/2399)

### Changed

- Updated Microsoft.Data.SqlClient.SNI version to `6.0.0-preview1.24226.4`. [#2772](https://github.com/dotnet/SqlClient/pull/2772)
- Improved access to `SqlAuthenticationProviderManager.Instance` and avoid early object initiation. [#2636](https://github.com/dotnet/SqlClient/pull/2636)
- Removed undocumented properties of `Azure.Identity` in `ActiveDirectoryAuthenticationProvider`. [#2562](https://github.com/dotnet/SqlClient/pull/2562)
- Replaced `System.Runtime.Caching` with `Microsoft.Extensions.Caching.Memory`. [#2493](https://github.com/dotnet/SqlClient/pull/2493)
- Updated `EnableOptimizedParameterBinding` to only accept text mode commands. [#2417](https://github.com/dotnet/SqlClient/pull/2417)
- Updated `Azure.Identity` version from `1.10.3` to `1.11.4`. [#2577](https://github.com/dotnet/SqlClient/pull/2577)
- Updated `Azure.Core` version from `1.35.0` to `1.38.0`. [#2462](https://github.com/dotnet/SqlClient/pull/2462)
- Updated `Azure.Security.KeyVault.Keys` version from `4.4.0` to `4.5.0`. [#2462](https://github.com/dotnet/SqlClient/pull/2462)
- Updated `Microsoft.IdentityModel.JsonWebTokens` and `Microsoft.IdentityModel.Protocols.OpenIdConnect` from `6.35.0` to `7.5.0`. [#2429](https://github.com/dotnet/SqlClient/pull/2429)
- Removed direct dependency to `Microsoft.Identity.Client` to take the transient dependecy through `Azure.Identity`. [#2577](https://github.com/dotnet/SqlClient/pull/2577)
- Removed unnecessary references `Microsoft.Extensions.Caching.Memory` and `System.Security.Cryptography.Cng` after removing .NET Standard. [#2577](https://github.com/dotnet/SqlClient/pull/2577)
- Improved memory allocation when reader opened by `CommandBehavior.SequentialAccess` over the big string columns. [#2356](https://github.com/dotnet/SqlClient/pull/2356)
- Improved SSPI by consolidating the context generation to single abstraction and using memory/span for SSPI generation. [#2255](https://github.com/dotnet/SqlClient/pull/2255), [#2447](https://github.com/dotnet/SqlClient/pull/2447)
- Reverted the [#2281](https://github.com/dotnet/SqlClient/pull/2281) code changes on ManagedSNI. [#2395](https://github.com/dotnet/SqlClient/pull/2395)
- Updated assembly version to 6.0.0.0. [#2382](https://github.com/dotnet/SqlClient/pull/2382)
- Code health improvements: [#2366](https://github.com/dotnet/SqlClient/pull/2366), [#2369](https://github.com/dotnet/SqlClient/pull/2369), [#2381](https://github.com/dotnet/SqlClient/pull/2381), [#2390](https://github.com/dotnet/SqlClient/pull/2390), [#2392](https://github.com/dotnet/SqlClient/pull/2392), [#2403](https://github.com/dotnet/SqlClient/pull/2403), [#2410](https://github.com/dotnet/SqlClient/pull/2410), [#2413](https://github.com/dotnet/SqlClient/pull/2413), [#2425](https://github.com/dotnet/SqlClient/pull/2425), [#2428](https://github.com/dotnet/SqlClient/pull/2428), [#2440](https://github.com/dotnet/SqlClient/pull/2440), [#2443](https://github.com/dotnet/SqlClient/pull/2443), [#2450](https://github.com/dotnet/SqlClient/pull/2450), [#2466](https://github.com/dotnet/SqlClient/pull/2466), [#2486](https://github.com/dotnet/SqlClient/pull/2486), [#2521](https://github.com/dotnet/SqlClient/pull/2521), [#2522](https://github.com/dotnet/SqlClient/pull/2522), [#2533](https://github.com/dotnet/SqlClient/pull/2533), [#2552](https://github.com/dotnet/SqlClient/pull/2552), [#2560](https://github.com/dotnet/SqlClient/pull/2560), [#2726](https://github.com/dotnet/SqlClient/pull/2726), [#2751](https://github.com/dotnet/SqlClient/pull/2751), [#2811](https://github.com/dotnet/SqlClient/pull/2811)

## [Stable release 5.2.3] - 2025-04-29

This update brings the following changes since the 5.2.2 release:

### Fixed

- Fixed possible `NullPointerException` during socket receive (PR [#3284](https://github.com/dotnet/SqlClient/pull/3284))
- Fixed inconsistencies between source and reference projects (PR [#3124](https://github.com/dotnet/SqlClient/pull/3124))
- Adjusted retry logic to allow errors with negative numbers to be considered transient (PR [#3185](https://github.com/dotnet/SqlClient/pull/3185))

### Changed

- Updated the following dependencies:
  - [System.Private.Uri](https://www.nuget.org/packages/System.Private.Uri) 4.3.2 - Avoid transitive [CVE-2019-0820](https://msrc.microsoft.com/update-guide/en-US/advisory/CVE-2019-0820) (PR [#3076](https://github.com/dotnet/SqlClient/pull/3076))
  - [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/6.0.3) 6.0.1 to 6.0.3 - Avoid [CVE-2024-43483](https://github.com/advisories/GHSA-qj66-m88j-hmgj) (PR [#3280](https://github.com/dotnet/SqlClient/pull/3280))

## [Stable release 5.2.2] - 2024-08-27

### Fixed

- Fixed `AcquireTokenAsync` timeout handling for edge cases in `ActiveDirectoryAuthenticationProvider`. [#2650](https://github.com/dotnet/SqlClient/pull/2650)
- Fixed issue with `Socket.Connect` in managed SNI. [#2779](https://github.com/dotnet/SqlClient/pull/2779)
- Fixed path for `AssemblyAttributes` in obj folder causing NET 8.0 assembly to appear in NET 6.0 dll. [#2789](https://github.com/dotnet/SqlClient/pull/2789)
- Fixed SSPI retry negotiation with default port in .NET. [#2815](https://github.com/dotnet/SqlClient/pull/2815)
- Fixed `ArgumentNullException` on `SqlDataRecord.GetValue` when using user-defined data type on .NET.  [#2816](https://github.com/dotnet/SqlClient/pull/2816)
- Fixed pending data with `SqlDataReader` against an encrypted column. [#2817](https://github.com/dotnet/SqlClient/pull/2817)

### Changed

- Upgraded `Azure.Identity` version from 1.11.3 to 1.11.4 [#2648](https://github.com/dotnet/SqlClient/pull/2648) to address [CVE-2024-35255](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9).
- Upgraded `Microsoft.Identity.Client` version from 4.60.0 to 4.61.3 [#2648](https://github.com/dotnet/SqlClient/pull/2648) to address [CVE-2024-35255](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9).
- Added caching to `TokenCredential` objects to take advantage of token caching. [#2775](https://github.com/dotnet/SqlClient/pull/2775)

## [Stable release 5.2.1] - 2024-05-31

This update brings the below changes over the previous release:

### Fixed

- Fixed connection errors on Linux when Data Source property contains both named instance and port [#2436](https://github.com/dotnet/SqlClient/pull/2436)
- Fixed `SqlConnection.FireInfoMessageEventOnUserErrors` when set to true throws an exception [#2505](https://github.com/dotnet/SqlClient/pull/2505)
- Fixed exception when using `DATETIMEOFFSET(n)` in a TVP if `n` is 1, 2, 3, or 4 [#2506](https://github.com/dotnet/SqlClient/pull/2506)
- Reverted PR [#1983](https://github.com/dotnet/SqlClient/pull/1938) which caused connection failure delays when using `OpenAsync` [#2507](https://github.com/dotnet/SqlClient/pull/2507)
- Fixed `SqlConnection.Clone()` to include `AccessTokenCallback` [#2527](https://github.com/dotnet/SqlClient/pull/2527)

### Changed

- Upgraded `Azure.Identity` version from 1.10.3 to 1.11.3 [#2492](https://github.com/dotnet/SqlClient/pull/2492), [#2528](https://github.com/dotnet/SqlClient/pull/2528)
- Upgraded `Microsoft.Identity.Client` version from 4.56.0 to 4.60.3 [#2492](https://github.com/dotnet/SqlClient/pull/2492)
- Code Health improvements: [#2467](https://github.com/dotnet/SqlClient/pull/2467)

## [Stable release 5.2.0] - 2024-02-28

### Added

- Added a new `AccessTokenCallBack` API to `SqlConnection`. [#1260](https://github.com/dotnet/SqlClient/pull/1260)
- Added `SqlBatch` support on .NET 6+ [#1825](https://github.com/dotnet/SqlClient/pull/1825), [#2223](https://github.com/dotnet/SqlClient/pull/2223),[#2371](https://github.com/dotnet/SqlClient/pull/2371), [#2373](https://github.com/dotnet/SqlClient/pull/2373)
- Added support of `SqlDiagnosticListener` on **.NET Standard**. [#1931](https://github.com/dotnet/SqlClient/pull/1931)
- Added new property `RowsCopied64` to `SqlBulkCopy`. [#2004](https://github.com/dotnet/SqlClient/pull/2004)
- Added support for the `SuperSocketNetLib` registry option for Encrypt on .NET on Windows. [#2047](https://github.com/dotnet/SqlClient/pull/2047)
- Added the ability to generate debugging symbols in a separate package file [#2137](https://github.com/dotnet/SqlClient/pull/2137)
- Added Workload Identity authentication support [#2159](https://github.com/dotnet/SqlClient/pull/2159), [#2264](https://github.com/dotnet/SqlClient/pull/2264)
- Added support for Big Endian systems [#2170](https://github.com/dotnet/SqlClient/pull/2170)
- Added support for Georgian collation [#2194](https://github.com/dotnet/SqlClient/pull/2194)
- Added Localization support on .NET [#2210](https://github.com/dotnet/SqlClient/pull/2110)
- Added .NET 8 support [#2230](https://github.com/dotnet/SqlClient/pull/2230)
- Added explicit version for major .NET version dependencies on System.Runtime.Caching 8.0.0, System.Configuration.ConfigurationManager 8.0.0, and System.Diagnostics. 
- DiagnosticSource 8.0.0 [#2303](https://github.com/dotnet/SqlClient/pull/2303)

### Fixed

- Fixed Always Encrypted secure enclave retry logic for async queries. [#1988](https://github.com/dotnet/SqlClient/pull/1988)
- Fixed activity correlator to continue use of same GUID for connection activity. [#1997](https://github.com/dotnet/SqlClient/pull/1997)
- Fixed behavior when error class is greater than 20 on connection retry. [#1953](https://github.com/dotnet/SqlClient/pull/1953)
- Fixed error message when symmetric key decryption failed using Always Encrypted. [#1948](https://github.com/dotnet/SqlClient/pull/1948)
- Fixed TransactionScope connection issue when Enlist is enable, Pooling is disabled and network connection type is Redirect. [#1960](https://github.com/dotnet/SqlClient/pull/1960)
- Fixed TDS RPC error on large queries in SqlCommand.ExecuteReaderAsync. [#1936](https://github.com/dotnet/SqlClient/pull/1936)
- Fixed throttling of token requests by calling AcquireTokenSilent. [#1925](https://github.com/dotnet/SqlClient/pull/1925)
- Fixed Linux code coverage result in Build proj. [#1950](https://github.com/dotnet/SqlClient/pull/1950)
- Fixed NullReferenceException in GetBytesAsync. [#1906](https://github.com/dotnet/SqlClient/pull/1906)
- Fixed Transient fault handling issue with OpenAsync. [#1983](https://github.com/dotnet/SqlClient/pull/1983)
- Fixed invariant mode checks. [#1917](https://github.com/dotnet/SqlClient/pull/1917)
- Fixed GC behavior in TdsParser by adding array rental capability in TryReadPlpUnicodeChars. [#1866](https://github.com/dotnet/SqlClient/pull/1866)
- Fixed socket synchronization issue during connect in managed SNI. [#1029](https://github.com/dotnet/SqlClient/pull/1029)
- Fixed issue with `SqlConnectionStringBuilder` property indexer not supporting non-string values. [#2018](https://github.com/dotnet/SqlClient/pull/2018)
- Fixed `SqlDataAdapter.Fill` and configurable retry logic issue on .NET Framework. [#2084](https://github.com/dotnet/SqlClient/pull/2084)
- Fixed `SqlConnectionEncryptOption` type conversion by introducing the `SqlConnectionEncryptOptionConverter` attribute when using **appsettings.json** files. [#2057](https://github.com/dotnet/SqlClient/pull/2057)
- Fixed th-TH culture info issue on Managed SNI. [#2066](https://github.com/dotnet/SqlClient/pull/2066)
- Fixed an issue when using the Authentication option, but not encrypting on .NET Framework where the server certificate was being incorrectly validated [#2224](https://github.com/dotnet/SqlClient/pull/2224)
- Fixed a deadlock problem for distributed transactions when on .NET [#2161](https://github.com/dotnet/SqlClient/pull/2161)
- Fixed an issue with connecting to named instances on named pipes in managed SNI (Linux/macOS) [#2142](https://github.com/dotnet/SqlClient/pull/2142)
- Fixed LocalDb connection issue with an invalid source when using managed SNI [#2129](https://github.com/dotnet/SqlClient/pull/2129)
- Fixed an `AccessViolationException` when using a SQL Express user instance [#2101](https://github.com/dotnet/SqlClient/pull/2101)
- Fixed a metadata query issue when connecting to Azure SQL Edge [#2099](https://github.com/dotnet/SqlClient/pull/2099)
- Fixed file version information for .NET and .NET Standard binaries [#2093](https://github.com/dotnet/SqlClient/pull/2093)
- Fixed the SPN sent for a named instance when using Kerberos authentication on Linux/macOS [#2240](https://github.com/dotnet/SqlClient/pull/2240)
- Fixed connection to unsubscribe from transaction completion events before returning it to the connection pool [#2301](https://github.com/dotnet/SqlClient/pull/2301)
- Fixed InvalidCastException when reading an Always Encrypted date or time column [#2275](https://github.com/dotnet/SqlClient/pull/2275)
- Fixed token caching to prevent expired access tokens from being reused in a connection pool [#2273](https://github.com/dotnet/SqlClient/pull/2273)

### Changed

- Improved parsing buffered characters in `TdsParser`. [#1544](https://github.com/dotnet/SqlClient/pull/1544)
- Added Microsoft.SqlServer.Types to verify support for SqlHierarchyId and Spatial for .NET Core. [#1848](https://github.com/dotnet/SqlClient/pull/1848)
- Moved to new System.Data.SqlTypes APIs on **.NET 7** and up. [#1934](https://github.com/dotnet/SqlClient/pull/1934) and [#1981](https://github.com/dotnet/SqlClient/pull/1981)
- Removed reference to Microsoft.Win32.Registry since it's shipped starting with .NET 6.0. [#1974](https://github.com/dotnet/SqlClient/pull/1974)
- Changed **[UseOneSecFloorInTimeoutCalculationDuringLogin](https://learn.microsoft.com/sql/connect/ado-net/appcontext-switches#enable-a-minimum-timeout-during-login)** App Context switch default to **true** and extended its effect to .NET and .NET Standard. [#2012](https://github.com/dotnet/SqlClient/pull/2012)
- Updated `Microsoft.Identity.Client` version from 4.47.2 to 4.53.0. [#2031](https://github.com/dotnet/SqlClient/pull/2031), [#2055](https://github.com/dotnet/SqlClient/pull/2055)
- Switched to the new .NET [NegotiateAuthentication](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication?view=net-7.0) API on .NET 7.0 and above for SSPI token negotiation using Managed SNI. [#2063](https://github.com/dotnet/SqlClient/pull/2063)
- Removed `ignoreSniOpenTimeout` in open connection process on Windows. [#2067](https://github.com/dotnet/SqlClient/pull/2067)
- Enforce explicit ordinal for internal `StringComparison` operations. [#2068](https://github.com/dotnet/SqlClient/pull/2068)
- Improved error messages when validating server certificates in managed SNI (Linux/macOS) [#2060](https://github.com/dotnet/SqlClient/pull/2060)
- Improved CPU usage when `AppContext` switches are in use [#2227](https://github.com/dotnet/SqlClient/pull/2227)
- Upgraded `Azure.Identity` dependency version to [1.10.3](https://www.nuget.org/packages/Azure.Identity/1.10.3) to address [CVE-2023-36414](https://github.com/advisories/GHSA-5mfx-4wcx-rv27), [#2189](https://github.com/dotnet/SqlClient/pull/2189)
- Changed Microsoft.IdentityModel.JsonWebTokens and Microsoft.IdentityModel.Protocols.OpenIdConnect version 6.24.0 to 6.35.0 [#2290](https://github.com/dotnet/SqlClient/pull/2290) to address [CVE-2024-21319](https://www.cve.org/CVERecord?id=CVE-2024-21319)
- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET/.NET Standard dependency) version to `v5.2.0`. [#2363](https://github.com/dotnet/SqlClient/pull/2363), which includes removing dead code and addressing static analysis warnings
- Code health improvements: [#1198](https://github.com/dotnet/SqlClient/pull/1198), [#1829](https://github.com/dotnet/SqlClient/pull/1829), [#1943](https://github.com/dotnet/SqlClient/pull/1943), [#1949](https://github.com/dotnet/SqlClient/pull/1949), [#1959](https://github.com/dotnet/SqlClient/pull/1959), [#1985](https://github.com/dotnet/SqlClient/pull/1985), [#2071](https://github.com/dotnet/SqlClient/pull/2071), [#2073](https://github.com/dotnet/SqlClient/pull/2073), [#2088](https://github.com/dotnet/SqlClient/pull/2088), [#2091](https://github.com/dotnet/SqlClient/pull/2091), [#2098](https://github.com/dotnet/SqlClient/pull/2098), [#2121](https://github.com/dotnet/SqlClient/pull/2121), [#2122](https://github.com/dotnet/SqlClient/pull/2122), [#2132](https://github.com/dotnet/SqlClient/pull/2132), [#2136](https://github.com/dotnet/SqlClient/pull/2136), [#2144](https://github.com/dotnet/SqlClient/pull/2144), [#2147](https://github.com/dotnet/SqlClient/pull/2147), [#2157](https://github.com/dotnet/SqlClient/pull/2157), [#2164](https://github.com/dotnet/SqlClient/pull/2164), [#2166](https://github.com/dotnet/SqlClient/pull/2166), [#2168](https://github.com/dotnet/SqlClient/pull/2168), [#2186](https://github.com/dotnet/SqlClient/pull/2186), [#2254](https://github.com/dotnet/SqlClient/pull/2254), [#2288](https://github.com/dotnet/SqlClient/pull/2288), [#2305](https://github.com/dotnet/SqlClient/pull/2305), [#2317](https://github.com/dotnet/SqlClient/pull/2317)

## [Preview Release 5.2.0-preview5.24024.3] - 2024-01-24

This update brings the below changes over the previous release:

### Added

- Added .NET 8 support [#2230](https://github.com/dotnet/SqlClient/pull/2230)
- Added explicit version for major .NET version dependencies on System.Runtime.Caching 8.0.0, System.Configuration.ConfigurationManager 8.0.0, and System.Diagnostics.DiagnosticSource 8.0.0 [#2303](https://github.com/dotnet/SqlClient/pull/2303)
- Added the ability to generate debugging symbols in a separate package file [#2137](https://github.com/dotnet/SqlClient/pull/2137)

### Changed

- Changed Microsoft.IdentityModel.JsonWebTokens and Microsoft.IdentityModel.Protocols.OpenIdConnect version 6.24.0 to 6.35.0 [#2290](https://github.com/dotnet/SqlClient/pull/2290) to address [CVE-2024-21319](https://www.cve.org/CVERecord?id=CVE-2024-21319)

### Fixed

- Fixed connection to unsubscribe from transaction completion events before returning it to the connection pool [#2301](https://github.com/dotnet/SqlClient/pull/2301)
- Fixed InvalidCastException when reading an Always Encrypted date or time column [#2275](https://github.com/dotnet/SqlClient/pull/2275)
- Fixed token caching to prevent expired access tokens from being reused in a connection pool [#2273](https://github.com/dotnet/SqlClient/pull/2273)
- Code health improvements: [#2288](https://github.com/dotnet/SqlClient/pull/2288), [#2305](https://github.com/dotnet/SqlClient/pull/2305), [#2254](https://github.com/dotnet/SqlClient/pull/2254), [#2317](https://github.com/dotnet/SqlClient/pull/2317)

## [Preview Release 5.2.0-preview4.23342.2] - 2023-12-08

This update brings the below changes over the previous release:

### Added

- Added `SqlBatch` support on .NET 6+ [#1825](https://github.com/dotnet/SqlClient/pull/1825), [#2223](https://github.com/dotnet/SqlClient/pull/2223)
- Added Workload Identity authentication support [#2159](https://github.com/dotnet/SqlClient/pull/2159), [#2264](https://github.com/dotnet/SqlClient/pull/2264)
- Added Localization support on .NET [#2210](https://github.com/dotnet/SqlClient/pull/2110)
- Added support for Georgian collation [#2194](https://github.com/dotnet/SqlClient/pull/2194)
- Added support for Big Endian systems [#2170](https://github.com/dotnet/SqlClient/pull/2170)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET/.NET Standard dependency) version to `v5.2.0-preview1.23340.1`. [#2257](https://github.com/dotnet/SqlClient/pull/2257), which includes removing dead code and addressing static analysis warnings
- Improved CPU usage when `AppContext` switches are in use [#2227](https://github.com/dotnet/SqlClient/pull/2227)
- Upgraded `Azure.Identity` dependency version to [1.10.3](https://www.nuget.org/packages/Azure.Identity/1.10.3) to address [CVE-2023-36414](https://github.com/advisories/GHSA-5mfx-4wcx-rv27), [#2188](https://github.com/dotnet/SqlClient/pull/2188)
- Improved error messages when validating server certificates in managed SNI (Linux/macOS) [#2060](https://github.com/dotnet/SqlClient/pull/2060)

### Fixed

- Fixed an issue when using the Authentication option, but not encrypting on .NET Framework where the server certificate was being incorrectly validated [#2224](https://github.com/dotnet/SqlClient/pull/2224)
- Fixed a deadlock problem for distributed transactions when on .NET [#2161](https://github.com/dotnet/SqlClient/pull/2161)
- Fixed an issue with connecting to named instances on named pipes in managed SNI (Linux/macOS)[#2142](https://github.com/dotnet/SqlClient/pull/2142)
- Fixed LocalDb connection issue with an invalid source when using managed SNI [#2129](https://github.com/dotnet/SqlClient/pull/2129)
- Fixed an `AccessViolationException` when using a SQL Express user instance [#2101](https://github.com/dotnet/SqlClient/pull/2101)
- Fixed a metadata query issue when connecting to Azure SQL Edge [#2099](https://github.com/dotnet/SqlClient/pull/2099)
- Fixed file version information for .NET and .NET Standard binaries[#2093](https://github.com/dotnet/SqlClient/pull/2093)
- Fixed the SPN sent for a named instance when using Kerberos authentication on Linux/macOS [#2240](https://github.com/dotnet/SqlClient/pull/2240)
- Various code improvements [#2091](https://github.com/dotnet/SqlClient/pull/2091), [#2098](https://github.com/dotnet/SqlClient/pull/2098), [#2121](https://github.com/dotnet/SqlClient/pull/2121), [#2122](https://github.com/dotnet/SqlClient/pull/2122), [#2132](https://github.com/dotnet/SqlClient/pull/2132), [#2136](https://github.com/dotnet/SqlClient/pull/2136), [#2144](https://github.com/dotnet/SqlClient/pull/2144), [#2147](https://github.com/dotnet/SqlClient/pull/2147), [#2157](https://github.com/dotnet/SqlClient/pull/2157), [#2164](https://github.com/dotnet/SqlClient/pull/2164), [#2166](https://github.com/dotnet/SqlClient/pull/2166), [#2168](https://github.com/dotnet/SqlClient/pull/2168), [#2186](https://github.com/dotnet/SqlClient/pull/2186)

This update brings the below changes over the previous release:

## [Preview Release 5.2.0-preview3.23201.1] - 2023-07-20

This update brings the below changes over the previous release:

### Added

- Added a new `AccessTokenCallBack` API to `SqlConnection`. [#1260](https://github.com/dotnet/SqlClient/pull/1260)
- Added support for the `SuperSocketNetLib` registry option for Encrypt on .NET on Windows. [#2047](https://github.com/dotnet/SqlClient/pull/2047)

### Fixed

- Fixed `SqlDataAdapter.Fill` and configurable retry logic issue on .NET Framework. [#2084](https://github.com/dotnet/SqlClient/pull/2084)
- Fixed `SqlConnectionEncryptOption` type conversion by introducing the `SqlConnectionEncryptOptionConverter` attribute when using **appsettings.json** files. [#2057](https://github.com/dotnet/SqlClient/pull/2057)
- Fixed th-TH culture info issue on Managed SNI. [#2066](https://github.com/dotnet/SqlClient/pull/2066)

### Changed

- Switched to the new .NET [NegotiateAuthentication](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.negotiateauthentication?view=net-7.0) API on .NET 7.0 and above for SSPI token negotiation using Managed SNI. [#2063](https://github.com/dotnet/SqlClient/pull/2063)
- Removed `ignoreSniOpenTimeout` in open connection process on Windows. [#2067](https://github.com/dotnet/SqlClient/pull/2067)
- Enforce explicit ordinal for internal `StringComparison` operations. [#2068](https://github.com/dotnet/SqlClient/pull/2068)
- Code health improvements: [#1959](https://github.com/dotnet/SqlClient/pull/1959), [#2071](https://github.com/dotnet/SqlClient/pull/2071), [#2073](https://github.com/dotnet/SqlClient/pull/2073), [#2088](https://github.com/dotnet/SqlClient/pull/2088)

## [Preview Release 5.2.0-preview2.23159.1] - 2023-06-08

This update brings the below changes over the previous release:

### Added

- Added new property `RowsCopied64` to `SqlBulkCopy`. [#2004](https://github.com/dotnet/SqlClient/pull/2004)

### Fixed

- Fixed socket synchronization issue during connect in managed SNI. [#1029](https://github.com/dotnet/SqlClient/pull/1029)
- Fixed issue with `SqlConnectionStringBuilder` property indexer not supporting non-string values. [#2018](https://github.com/dotnet/SqlClient/pull/2018)

### Changed

- Moved to new System.Data.SqlTypes APIs in **.NET 7** and upper. [1934](https://github.com/dotnet/SqlClient/pull/1934) and [#1981](https://github.com/dotnet/SqlClient/pull/1981)
- Changed **[UseOneSecFloorInTimeoutCalculationDuringLogin](https://learn.microsoft.com/sql/connect/ado-net/appcontext-switches#enable-a-minimum-timeout-during-login)** App Context switch default to **true** and extended its effect to .NET and .NET Standard. [#2012](https://github.com/dotnet/SqlClient/pull/2012)
- Updated `Microsoft.Identity.Client` version from 4.47.2 to 4.53.0. [#2031](https://github.com/dotnet/SqlClient/pull/2031), [#2055](https://github.com/dotnet/SqlClient/pull/2055) 
- Code health improvement: [#1985](https://github.com/dotnet/SqlClient/pull/1985)

## [Preview Release 5.2.0-preview1.23109.1] - 2023-04-20

This update brings the below changes over the previous release:

### Added

- Added support of `SqlDiagnosticListener` on **.NET Standard**. [#1931](https://github.com/dotnet/SqlClient/pull/1931)

### Fixed

- Fixed AE enclave retry logic for async queries. [#1988](https://github.com/dotnet/SqlClient/pull/1988)
- Fixed activity correlator to continue use of same GUID for connection activity. [#1997](https://github.com/dotnet/SqlClient/pull/1997)
- Fixed behavior when error class is greater than 20 on connection retry. [#1953](https://github.com/dotnet/SqlClient/pull/1953)
- Fixed error message when symmetric key decryption failed using Always Encrypted. [#1948](https://github.com/dotnet/SqlClient/pull/1948)
- Fixed TransactionScope connection issue when Enlist is enable, Pooling is disabled and network connection type is Redirect. [#1960](https://github.com/dotnet/SqlClient/pull/1960)
- Fixed TDS RPC error on large queries in SqlCommand.ExecuteReaderAsync. [#1936](https://github.com/dotnet/SqlClient/pull/1936)
- Fixed throttling of token requests by calling AcquireTokenSilent. [#1925](https://github.com/dotnet/SqlClient/pull/1925)
- Fixed Linux code coverage result in Build proj. [#1950](https://github.com/dotnet/SqlClient/pull/1950)
- Fixed NullReferenceException in GetBytesAsync. [#1906](https://github.com/dotnet/SqlClient/pull/1906)
- Fixed Transient fault handling issue with OpenAsync. [#1983](https://github.com/dotnet/SqlClient/pull/1983)
- Fixed invariant mode checks. [#1917](https://github.com/dotnet/SqlClient/pull/1917)
- Fixed GC behavior in TdsParser by adding array rental capability in TryReadPlpUnicodeChars. [#1866](https://github.com/dotnet/SqlClient/pull/1866)

### Changed

- Updated Azure Identity version from 1.7.0 to 1.8.0. [#1921](https://github.com/dotnet/SqlClient/pull/1921)
- Improved parsing buffered characters in `TdsParser`. [#1544](https://github.com/dotnet/SqlClient/pull/1544)
- Removed reference to Microsoft.Win32.Registry since it's shipped starting with .NET 6.0. [#1974](https://github.com/dotnet/SqlClient/pull/1974)
- Added Microsoft.SqlServer.Types to verify support for SqlHierarchyId and Spatial for .NET Core. [#1848](https://github.com/dotnet/SqlClient/pull/1848)
- Code health improvements:[#1943](https://github.com/dotnet/SqlClient/pull/1943)[#1949](https://github.com/dotnet/SqlClient/pull/1949)[#1198](https://github.com/dotnet/SqlClient/pull/1198)[#1829](https://github.com/dotnet/SqlClient/pull/1829)

## [Stable release 5.1.7] - 2025-04-25

This update brings the following changes since the 5.1.6 release:

### Fixed

- Fixed possible `NullPointerException` during socket receive (PR [#3285](https://github.com/dotnet/SqlClient/pull/3285))
- Fixed inconsistencies between source and reference projects (PR [#3180](https://github.com/dotnet/SqlClient/pull/3180))

### Changed

- Updated the following dependencies:
  - [Microsoft.Data.SqlClient.SNI](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI/5.1.2) 5.1.1 to 5.1.2 for .NET Framework on Windows (PR [#3294](https://github.com/dotnet/SqlClient/pull/3294))
  - [Microsoft.Data.SqlClient.SNI.runtime](https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI.runtime/5.1.2) 5.1.1 to 5.1.2 for .NET on Windows (PR [#3294](https://github.com/dotnet/SqlClient/pull/3294))
  - [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/6.0.3) 6.0.1 to 6.0.3 - Avoid [CVE-2024-43483](https://github.com/advisories/GHSA-qj66-m88j-hmgj) (PR [#3068](https://github.com/dotnet/SqlClient/pull/3068))
  - [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/6.0.1) 6.0.0 to 6.0.1 - Avoid transitive dependency on vulnerable [System.Text.Json](https://www.nuget.org/packages/System.Text.Json/6.0.0) 6.0.0 (PR [#3207](https://github.com/dotnet/SqlClient/pull/3207))
  - [System.Private.Uri](https://www.nuget.org/packages/System.Private.Uri) 4.3.2 - Avoid transitive [CVE-2019-0820](https://msrc.microsoft.com/update-guide/en-US/advisory/CVE-2019-0820) (PR [#3077](https://github.com/dotnet/SqlClient/pull/3077))
  - [System.Text.Encodings.Web](https://www.nuget.org/packages/System.Text.Encodings.Web/6.0.1) 6.0.0 to 6.0.1 - Avoid transitive downgrade for .NET Framework targets (PR [#3279](https://github.com/dotnet/SqlClient/pull/3279))
  - [System.Text.Json](https://www.nuget.org/packages/System.Text.Json/6.0.11) 6.0.11 - Avoid transitive dependencies on older vulnerable versions for .NET Framework targets (PR [#3279](https://github.com/dotnet/SqlClient/pull/3279))

## [Stable release 5.1.6] - 2024-08-27

### Fixed

- Fixed Transient fault handling issue with `OpenAsync`. [#1983](https://github.com/dotnet/SqlClient/pull/1983) [#2508](https://github.com/dotnet/SqlClient/pull/2508)
- Fixed `AcquireTokenAsync` timeout handling for edge cases in `ActiveDirectoryAuthenticationProvider`. [#2706](https://github.com/dotnet/SqlClient/pull/2706)
- Fixed pending data with `SqlDataReader` against an encrypted column. [#2618](https://github.com/dotnet/SqlClient/pull/2618) [#2818](https://github.com/dotnet/SqlClient/pull/2818)

### Changed

- Upgraded `Azure.Identity` version from 1.11.3 to 1.11.4 [#2649] (https://github.com/dotnet/SqlClient/pull/2649) [#2529] (https://github.com/dotnet/SqlClient/pull/2529) to address [CVE-2024-35255](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9).
- Upgraded `Microsoft.Identity.Client` version from 4.60.0 to 4.61.3 [#2649] (https://github.com/dotnet/SqlClient/pull/2649) [#2529] (https://github.com/dotnet/SqlClient/pull/2529) to address [CVE-2024-35255](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9).
- Added caching to `TokenCredential` objects to take advantage of token caching. [#2776](https://github.com/dotnet/SqlClient/pull/2776)
- Code health improvements: [#2490] (https://github.com/dotnet/SqlClient/pull/2490)

## [Stable release 5.1.5] - 2024-01-29

This update brings the below changes over the previous release:

### Fixed

- Fixed connection to unsubscribe from transaction completion events before returning it to the connection pool [#2321](https://github.com/dotnet/SqlClient/pull/2321)
- Fixed InvalidCastException when reading an Always Encrypted date or time column [#2324](https://github.com/dotnet/SqlClient/pull/2324)

### Changed

- Changed Microsoft.IdentityModel.JsonWebTokens and Microsoft.IdentityModel.Protocols.OpenIdConnect version 6.24.0 to 6.35.0 [#2320](https://github.com/dotnet/SqlClient/pull/2320) to address [CVE-2024-21319](https://www.cve.org/CVERecord?id=CVE-2024-21319)

## [Stable release 5.1.4] - 2024-01-09

This update brings the below changes over the previous release:

### Fixed

- Fixed a deadlock problem for distributed transactions when on .NET.

### Changed

- Upgraded `Azure.Identity` dependency version to [1.10.3](https://www.nuget.org/packages/Azure.Identity/1.10.3) to address [CVE-2023-36414](https://github.com/advisories/GHSA-5mfx-4wcx-rv27).

## [Stable release 5.1.3] - 2024-01-09

This update brings the below changes over the previous release:

### Fixed

- Fixed encryption downgrade issue. [CVE-2024-0056](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-0056)
- Fixed certificate chain validation logic flow.

## [Stable release 5.1.2] - 2023-10-26

This update brings the below changes over the previous release:

### Fixed

- Fixed access violation when using SQL Express user instance. [#2101](https://github.com/dotnet/SqlClient/pull/2101)
- Fixed Always Encrypted secure enclave retry logic for async queries. [#1988](https://github.com/dotnet/SqlClient/pull/1988)
- Fixed LocalDb and managed SNI by improving the error messages and avoid falling back to the local service. [#2129](https://github.com/dotnet/SqlClient/pull/2129)
- Fixed .NET and .NET Standard file version. [2093](https://github.com/dotnet/SqlClient/pull/2093)
- Fixed non-string values and `SqlConnectionStringBuilder` property indexer issue. [#2018](https://github.com/dotnet/SqlClient/pull/2018)
- Fixed `SqlConnectionEncryptOption` type conversion by introducing the `SqlConnectionEncryptOptionConverter` attribute when using **appsettings.json** files. [#2057](https://github.com/dotnet/SqlClient/pull/2057)
- Fixed Transient fault handling issue with `OpenAsync`. [#1983](https://github.com/dotnet/SqlClient/pull/1983)
- Fixed activity correlator to continue use of same GUID for connection activity. [#1997](https://github.com/dotnet/SqlClient/pull/1997)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.1.1`. [#2123](https://github.com/dotnet/SqlClient/pull/2123)

## [Stable release 5.1.1] - 2023-03-28

This update brings the below changes over the previous release:

### Fixed

- Fixed an incorrect exception when a symmetric key fails to decrypt a column using Always Encrypted. [#1968](https://github.com/dotnet/SqlClient/pull/1968)
- Fixed `TransactionScope` connection issue when `Enlist` is `enabled`, `Pooling` is `disabled`, and `Network Connection Type` is set to `Redirect`. [#1967](https://github.com/dotnet/SqlClient/pull/1967)
- Fixed throttling of token requests by calling `AcquireTokenSilent`. [#1966](https://github.com/dotnet/SqlClient/pull/1966)
- Fixed TDS RPC error on large queries in `SqlCommand.ExecuteReaderAsync`. [#1965](https://github.com/dotnet/SqlClient/pull/1965)
- Fixed `NullReferenceException` in `GetBytesAsync`. [#1964](https://github.com/dotnet/SqlClient/pull/1964)

## [Stable release 5.1.0] - 2023-01-19

This update brings the below changes over the previous release:

### Fixed

- Fixed thread safety of transient error list in configurable retry logic. [#1882](https://github.com/dotnet/SqlClient/pull/1882)
- Fixed deadlock when using SinglePhaseCommit with distributed transactions. [#1801](https://github.com/dotnet/SqlClient/pull/1801)
- Fixed Dedicated Admin Connections (DAC) to localhost in managed SNI. [#1865](https://github.com/dotnet/SqlClient/pull/1865)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.1.0`. [#1889](https://github.com/dotnet/SqlClient/pull/1889) which includes fix for AppDomain crash in issue [#1418](https://github.com/dotnet/SqlClient/issues/1418), TLS 1.3 Support, removal of ARM32 binaries, and support for the `ServerCertificate` option.
- Code health improvements [#1867](https://github.com/dotnet/SqlClient/pull/1867) [#1849](https://github.com/dotnet/SqlClient/pull/1849)

## [Preview Release 5.1.0-preview2.22314.2] - 2022-11-10

This update brings the below changes over the previous release:

### Breaking changes over preview release v5.1.0-preview1

- Dropped support for .NET Core 3.1. [#1704](https://github.com/dotnet/SqlClient/pull/1704) [#1823](https://github.com/dotnet/SqlClient/pull/1823)

### Added

- Added support for .NET 6.0. [#1704](https://github.com/dotnet/SqlClient/pull/1704)
- Added support for `DateOnly` and `TimeOnly` for `SqlParameter` value and `GetFieldValue`. [#1813](https://github.com/dotnet/SqlClient/pull/1813)
- Added support for TLS 1.3 for .NET Core and SNI Native. [#1821](https://github.com/dotnet/SqlClient/pull/1821)
- Added `ServerCertificate` support for `Encrypt=Mandatory` or `Encrypt=Strict`. [#1822](https://github.com/dotnet/SqlClient/pull/1822)
- Added Windows ARM64 support when targeting .NET Framework. [#1828](https://github.com/dotnet/SqlClient/pull/1828)

### Fixed

- Fixed memory leak regression from [#1781](https://github.com/dotnet/SqlClient/pull/1781) using a `DisposableTemporaryOnStack` struct. [#1818](https://github.com/dotnet/SqlClient/pull/1818)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.1.0-preview2.22311.2`. [#1831](https://github.com/dotnet/SqlClient/pull/1831) which includes the fix for the TLS 1.3 timeout and double handshake issue, removal of ARM32 binaries, and support for the `ServerCertificate` option. [#1822](https://github.com/dotnet/SqlClient/issues/1822)
- Reverted "Excluding unsupported TLS protocols" for issue [#1151](https://github.com/dotnet/SqlClient/issues/1151) (i.e. removed `Switch.Microsoft.Data.SqlClient.EnableSecureProtocolsByOS`) by adding support for TLS 1.3. [#1824](https://github.com/dotnet/SqlClient/issues/1824)
- Code health improvements [#1812](https://github.com/dotnet/SqlClient/pull/1812) [#1520](https://github.com/dotnet/SqlClient/pull/1520)

## [Preview Release 5.1.0-preview1.22279.3] - 2022-10-19

This update brings the below changes over the previous release:

### Fixed

- Fixed `ReadAsync()` behavior to register Cancellation token action before streaming results. [#1781](https://github.com/dotnet/SqlClient/pull/1781)
- Fixed `NullReferenceException` when assigning `null` to `SqlConnectionStringBuilder.Encrypt`. [#1778](https://github.com/dotnet/SqlClient/pull/1778)
- Fixed missing `HostNameInCertificate` property in .NET Framework Reference Project. [#1776](https://github.com/dotnet/SqlClient/pull/1776)
- Fixed async deadlock issue when sending attention fails due to network failure. [#1766](https://github.com/dotnet/SqlClient/pull/1766)
- Fixed failed connection requests in ConnectionPool in case of PoolBlock. [#1768](https://github.com/dotnet/SqlClient/pull/1768)
- Fixed hang on infinite timeout and managed SNI. [#1742](https://github.com/dotnet/SqlClient/pull/1742)
- Fixed Default UTF8 collation conflict. [#1739](https://github.com/dotnet/SqlClient/pull/1739)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.1.0-preview1.22278.1`. [#1787](https://github.com/dotnet/SqlClient/pull/1787) which includes TLS 1.3 Support and fix for AppDomain crash in issue [#1418](https://github.com/dotnet/SqlClient/issues/1418)
- Changed the `SqlConnectionEncryptOption` string parser to public. [#1771](https://github.com/dotnet/SqlClient/pull/1771)
- Converted `ExecuteNonQueryAsync` to use async context object. [#1692](https://github.com/dotnet/SqlClient/pull/1692)
- Code health improvements [#1604](https://github.com/dotnet/SqlClient/pull/1604) [#1598](https://github.com/dotnet/SqlClient/pull/1598) [#1595](https://github.com/dotnet/SqlClient/pull/1595) [#1443](https://github.com/dotnet/SqlClient/pull/1443)

### Known issues

- When using `Encrypt=Strict` with TLS v1.3, the TLS handshake occurs twice on initial connection on .NET Framework due to a timeout during the TLS handshake and a retry helper re-establishes the connection; however, on .NET Core, it will throw a `System.ComponentModel.Win32Exception (258): The wait operation timed out.` and is being investigated. If you're using Microsoft.Data.SqlClient with .NET Core on Windows 11, you will need to enable the managed SNI on Windows context switch using following statement `AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);` to use TLS v1.3 or disabling TLS 1.3 from the registry by assigning `0` to the following `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Client\Enabled` registry key and it'll use TLS v1.2 for the connection. This will be fixed in a future release.

## [Stable release 5.0.2] - 2023-03-31

### Fixed

- Fixed memory leak regression from [#1785](https://github.com/dotnet/SqlClient/pull/1785) using a `DisposableTemporaryOnStack` struct. [#1980](https://github.com/dotnet/SqlClient/pull/1980)
- Fixed `TransactionScope` connection issue when `Enlist` is `enabled`, `Pooling` is `disabled`, and `Network Connection Type` is set to `Redirect`. [#1978](https://github.com/dotnet/SqlClient/pull/1978)
- Fixed an incorrect exception when a symmetric key fails to decrypt a column using Always Encrypted. [#1977](https://github.com/dotnet/SqlClient/pull/1977)
- Fixed TDS RPC error on large queries in `SqlCommand.ExecuteReaderAsync`. [#1976](https://github.com/dotnet/SqlClient/pull/1976)
- Fixed deadlock when using SinglePhaseCommit with distributed transactions. [#1975](https://github.com/dotnet/SqlClient/pull/1975)

## [Stable release 5.0.1] - 2022-10-07

### Fixed

- Fixed missing `HostNameInCertificate` connection string property in .NET Framework. [#1782](https://github.com/dotnet/SqlClient/pull/1782)
- Fixed async deadlock issue when sending attention fails due to network failure. [#1783](https://github.com/dotnet/SqlClient/pull/1783)
- Fixed **Null Reference Exception** on assigning `null` to `SqlConnectionStringBuilder.Encrypt`. [#1784](https://github.com/dotnet/SqlClient/pull/1784)
- Fixed `ReadAsync()` behavior to register Cancellation token action before streaming results. [#1785](https://github.com/dotnet/SqlClient/pull/1785)
- Fixed hang on infinite timeout and managed SNI. [#1798](https://github.com/dotnet/SqlClient/pull/1798)
- Fixed Default UTF8 collation conflict. [#1799](https://github.com/dotnet/SqlClient/pull/1799)

### Changed

- Updated `Microsoft.Data.SqlClient.SNI` (.NET Framework dependency) and `Microsoft.Data.SqlClient.SNI.runtime` (.NET Core/Standard dependency) version to `5.0.1` [#1795](https://github.com/dotnet/SqlClient/pull/1795), which includes the fix for AppDomain crash introducing in issue [#1418](https://github.com/dotnet/SqlClient/issues/1418).

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

## [Stable release 4.0.6] - 2024-08-21

### Fixed

- Fixed connection to unsubscribe from transaction completion events before returning it to the connection pool [#2301](https://github.com/dotnet/SqlClient/pull/2301) [#2435](https://github.com/dotnet/SqlClient/pull/2435)
- Fixed AcquireTokenAsync timeout handling for edge cases in ActiveDirectoryAuthenticationProvider [#2707](https://github.com/dotnet/SqlClient/pull/2707)

### Changed

- Code health improvements: [#2147](https://github.com/dotnet/SqlClient/pull/2147), [#2513](https://github.com/dotnet/SqlClient/pull/2513), [#2519](https://github.com/dotnet/SqlClient/pull/2519)

## [Stable release 4.0.5] - 2024-01-09

### Fixed

- Fixed encryption downgrade issue. [CVE-2024-0056](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-0056)
- Fixed certificate chain validation logic flow.

## [Stable release 4.0.4] - 2023-10-30

### Fixed

- Fixed Always Encrypted secure enclave retry logic for async queries. [#1988](https://github.com/dotnet/SqlClient/pull/1988)
- Fixed LocalDb and managed SNI by improving the error messages and avoid falling back to the local service. [#2129](https://github.com/dotnet/SqlClient/pull/2129)
- Fixed .NET and .NET Standard file version. [2093](https://github.com/dotnet/SqlClient/pull/2093)
- Fixed activity correlator to continue use of same GUID for connection activity. [#1997](https://github.com/dotnet/SqlClient/pull/1997)

## [Stable release 4.0.3] - 2023-04-20

### Fixed

- Fixed throttling of token requests by calling AcquireTokenSilent in AAD Integrated/Password flows when the account is already cached.[#1995](https://github.com/dotnet/SqlClient/pull/1995)
- Fixed TDS RPC error on large queries in `SqlCommand.ExecuteReaderAsync`.[#1987](https://github.com/dotnet/SqlClient/pull/1987)

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
- Various code improvements [#902](https://github.com/dotnet/SqlClient/pull/902) [#925](https://github.com/dotnet/SqlClient/pull/925) [#933](https://github.com/dotnet/SqlClient/pull/933) [#934](https://github.com/dotnet/SqlClient/pull/934) [#1024](https://github.com/dotnet/SqlClient/pull/1024) [#1057](https://github.com/dotnet/SqlClient/pull/1057) [#1122](https://github.com/dotnet/SqlClient/pull/1122) [#1133](https://github.com/dotnet/SqlClient/pull/1133) [#1134](https://github.com/dotnet/SqlClient/pull/1134) [#1141](https://github.com/dotnet/SqlClient/pull/1141) [#1187](https://github.com/dotnet/SqlClient/pull/1187) [#1188](https://github.com/dotnet/SqlClient/pull/1188) [#1223](https://github.com/dotnet/SqlClient/pull/1223) [#1225](https://github.com/dotnet/SqlClient/pull/1225)  [#1226](https://github.com/dotnet/SqlClient/pull/1226)

## [Stable release 3.1.7] - 2024-08-20

### Fixed

- Fixed connection to unsubscribe from transaction completion events before returning it to the connection pool. [#2301](https://github.com/dotnet/SqlClient/pull/2301) [#2434](https://github.com/dotnet/SqlClient/pull/2434)
- Fixed `AcquireTokenAsync` timeout handling for edge cases in `ActiveDirectoryAuthenticationProvider`. [#2709](https://github.com/dotnet/SqlClient/pull/2709)
- Fixed the signing issue with `Microsoft.Data.SqlClient` assembly. [#2789](https://github.com/dotnet/SqlClient/pull/2789)

### Changed

- Updated Microsoft.Data.SqlClient.SNI version 3.0.1 to 3.0.2 [#2676](https://github.com/dotnet/SqlClient/pull/2676) which includes the fix for AppDomain crashing in issue [#1418](https://github.com/dotnet/SqlClient/issues/1418) and various code refactors.
- Code health improvements: [#2147](https://github.com/dotnet/SqlClient/pull/2147), [#2515](https://github.com/dotnet/SqlClient/pull/2515), [#2517](https://github.com/dotnet/SqlClient/pull/2517) addresses [CVE-2019-0545](https://github.com/advisories/GHSA-2xjx-v99w-gqf3), [#2539](https://github.com/dotnet/SqlClient/pull/2539)

## [Stable release 3.1.5] - 2024-01-09

### Fixed

- Fixed encryption downgrade issue. [CVE-2024-0056](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-0056)
- Fixed certificate chain validation logic flow.

## [Stable release 3.1.4] - 2023-10-31

### Fixed

- Fixed Always Encrypted secure enclave retry logic for async queries. [#1988](https://github.com/dotnet/SqlClient/pull/1988)
- Fixed LocalDb and managed SNI by improving the error messages and avoid falling back to the local service. [#2129](https://github.com/dotnet/SqlClient/pull/2129)
- Fixed .NET and .NET Standard file version. [2093](https://github.com/dotnet/SqlClient/pull/2093)
- Fixed activity correlator to continue use of same GUID for connection activity. [#1997](https://github.com/dotnet/SqlClient/pull/1997)
- Fixed FormatException when event source tracing is enabled. [#1291](https://github.com/dotnet/SqlClient/pull/1291)

## [Stable release 3.1.3] - 2023-03-10

### Fixed

- Fixed throttling of token requests by calling AcquireTokenSilent in AAD Integrated/Password flows when the account is already cached.[#1926](https://github.com/dotnet/SqlClient/pull/1926)
- Fixed TDS RPC error on large queries in SqlCommand.ExecuteReaderAsync.[#1939](https://github.com/dotnet/SqlClient/pull/1939)

## [Stable release 3.1.2] - 2023-02-03

### Added

- Added Windows ARM64 support when targeting .NET Framework. [#1908](https://github.com/dotnet/SqlClient/pull/1908)

### Fixed

- Fixed thread safety of transient error list in configurable retry logic. [#1911](https://github.com/dotnet/SqlClient/pull/1911)
- Fixed deadlock when using SinglePhaseCommit with distributed transactions. [#1912](https://github.com/dotnet/SqlClient/pull/1912)
- Fixed Default UTF8 collation conflict. [#1910](https://github.com/dotnet/SqlClient/pull/1910)
- Added CommandText length validation when using stored procedure command types. [#1909](https://github.com/dotnet/SqlClient/pull/1909)

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

## [Stable Release 2.1.7] - 2024-01-09

### Fixed

- Fixed encryption downgrade issue. [CVE-2024-0056](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-0056)
- Fixed certificate chain validation logic flow.

## [Stable Release 2.1.6] - 2023-04-27

### Fixed

- Fixed TDS RPC error on large queries in `SqlCommand.ExecuteReaderAsync`.[#1986](https://github.com/dotnet/SqlClient/pull/1986)
- Fixed Default UTF8 collation conflict. [#1989](https://github.com/dotnet/SqlClient/pull/1989)
- Fixed async deadlock issue when sending attention fails due to network failure. [#1767](https://github.com/dotnet/SqlClient/pull/1767)

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
- The new **Microsoft.Data.SqlClient.SNI.runtime** dependency adds support for the _ARM_ platform along with the already supported platforms _ARM64_, _x64_ and _x86_ on Windows [#570](https://github.com/dotnet/SqlClient/pull/570)
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
- Changed `Receive()` and `ReceiveAsync()` implementation to receive null packets on failure [#350](https://github.com/dotnet/SqlClient/pull/350)
- Changed `EnclaveProviderBase` caching implementation to support Async Scenarios  _(Introduces breaking changes)_ [#346](https://github.com/dotnet/SqlClient/pull/346)

## [Stable Release 1.1.0] - 2019-11-20

### Added

- Added support for |DataDirectory| macro in `AttachDBFilename` for .NET Core client [#284](https://github.com/dotnet/SqlClient/pull/284)

### Fixed

- Fixed connection resiliency check [#310](https://github.com/dotnet/SqlClient/pull/310)
- Fixed `SNIPacket.ReadFromStreamAsync` to not consume same `ValueTask` twice [#295](https://github.com/dotnet/SqlClient/pull/295)
- Fixed driver behavior to not send Attention signal for successful Bulk Copy operation [#308](https://github.com/dotnet/SqlClient/pull/308)
- Fixed driver behavior to abort connection when encountering `SqlException` on `SqlTransaction.Commit` [#299](https://github.com/dotnet/SqlClient/pull/299)
- Fixed driver behavior to not throw exception on invalid _app.config_ files [#319](https://github.com/dotnet/SqlClient/pull/319)

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
