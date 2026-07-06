# Manual Test Conditional Attribute Inventory

This inventory was generated from `Microsoft.Data.SqlClient/tests/ManualTests/**/*.cs`.

- Conditional attributes found: 704
- Distinct condition combinations: 76
- Counting rule: each `[ConditionalFact(...)]` or `[ConditionalTheory(...)]` attribute counts once.
- Semantics: when multiple conditions are listed on one attribute, all listed predicates must pass.

## Condition Combinations

| Count | Conditions | Meaning |
| ---: | --- | --- |
| 155 | `AreConnStringsSetup + IsNotAzureSynapse` | Manual connection strings are configured and the target is not Azure Synapse. |
| 149 | `AreConnStringsSetup` | Manual TCP and named-pipe connection strings are configured. |
| 91 | `AreConnStringsSetup + IsNotAzureServer` | Manual connection strings are configured and the target is not Azure SQL. |
| 45 | `IsTargetReadyForAeWithKeyStore` | Always Encrypted target is configured; non-Windows requires AKV. |
| 27 | `IsAKVSetupAvailable` | Azure Key Vault test configuration is available and target is not Synapse. |
| 18 | `IsAADConnStringsSetup` | Local AAD test helper says the AAD password connection string is configured. |
| 18 | `IsUdtTestDatabasePresent + AreConnStringsSetup` | UDT database exists, target is not Synapse, and manual connection strings are configured. |
| 17 | `CanRunSchemaTests` | Schema-test local helper passes. |
| 10 | `IsNotAzureSynapse + IsNotManagedInstance` | Target is neither Synapse nor Managed Instance. |
| 9 | `AreConnStringSetupForAE` | Always Encrypted connection string set is available and target is not Synapse. |
| 9 | `IsLocalDBEnvironmentSet` | LocalDB app name is configured and integrated security is supported. |
| 8 | `IsJsonSupported` | TCP target exposes the `json` type. |
| 8 | `IsSqlVectorSupported` | TCP target exposes the `vector` type. |
| 7 | `AreConnStringsSetup + IsSqlVectorFloat16Supported` | Manual connection strings are configured and vector `float16` probe succeeds. |
| 7 | `AreConnStringsSetup + IsSqlVectorSupported` | Manual connection strings are configured and target supports `vector`. |
| 7 | `IsAccessTokenSetup + IsAADConnStringsSetup` | Local AAD helper can acquire an access token and AAD connection string is configured. |
| 7 | `IsUdtTestDatabasePresent + AreConnStringsSetup + IsNotAzureServer` | UDT database exists, manual connection strings are configured, and target is not Azure SQL. |
| 6 | `IsTCPConnStringSetup + IsLocalHost + IsNotNamedInstance` | TCP connection string is configured for localhost and not a named instance. |
| 5 | `IsIntegratedSecurityEnvironmentSet + AreConnectionStringsSetup` | Local integrated-security helper and connection strings are available. |
| 4 | `AreConnStringSetupForAE + IsAKVSetupAvailable` | Always Encrypted and AKV setup are available. |
| 4 | `AreConnStringSetupForAE + IsNotAzureServer` | Always Encrypted setup is available and target is not Azure SQL. |
| 4 | `AreConnStringsSetup + IsJsonSupported` | Manual connection strings are configured and target supports `json`. |
| 4 | `AreConnStringsSetup + IsNotAzureServer + IsNotAzureSynapse` | Manual connection strings are configured and target is neither Azure SQL nor Synapse. |
| 4 | `AreConnStringsSetup + IsNotAzureSynapse + IsNotManagedInstance` | Manual connection strings are configured and target is neither Synapse nor Managed Instance. |
| 4 | `AreConnStringsSetup + IsUsingManagedSNI` | Manual connection strings are configured and managed SNI is enabled. |
| 4 | `IsNotAzureServer + IsNotAzureSynapse` | Target is neither Azure SQL nor Synapse. |
| 4 | `IsNotAzureServer + IsNotAzureSynapse + AreConnStringsSetup` | Same as `AreConnStringsSetup + IsNotAzureServer + IsNotAzureSynapse`; ordering differs in source. |
| 3 | `AreConnStringsSetup + IsNotAzureServer + IsNotNamedInstance` | Manual connection strings are configured, target is not Azure SQL, and TCP data source is not a named instance. |
| 3 | `AreConnStringsSetup + IsNotAzureSynapse + IsAtLeastSQL2017` | Manual connection strings are configured, target is not Synapse, and server major version is 14 or later. |
| 3 | `IsFileStreamEnvironmentSet + IsIntegratedSecurityEnvironmentSet + AreConnectionStringsSetup` | FILESTREAM, integrated security, and connection strings are configured. |
| 3 | `IsLocalDbSharedInstanceSet` | LocalDB shared instance and integrated security are configured. |
| 3 | `IsNotAzureSynapse + AreConnStringsSetup + IsNotAzureServer` | Same logical set as connection strings plus non-Azure-SQL plus non-Synapse; ordering differs in source. |
| 2 | `AreConnStringSetupForAE + EnclaveEnabled` | Always Encrypted setup is available and enclave testing is enabled. |
| 2 | `AreConnStringsSetup + IsNotAzureServer + IsLocalHost` | Manual connection strings are configured for localhost and not Azure SQL. |
| 2 | `AreConnStringsSetup + IsNotAzureServer + IsLocalHost + IsAdmin` | Same as above, plus process is elevated. |
| 2 | `AreConnStringsSetup + IsNotAzureServer + IsNotAzureSynapse + IsAtLeastSQL2019` | Manual connection strings are configured, target is not Azure SQL/Synapse, and server major version is 15 or later. |
| 2 | `AreConnStringsSetup + IsNotAzureServer + TcpConnectionStringDoesNotUseAadAuth` | Manual connection strings are configured, target is not Azure SQL, and TCP auth is SQL password or unspecified. |
| 2 | `AreConnStringsSetup + TcpConnectionStringDoesNotUseAadAuth` | Manual connection strings are configured and TCP auth is SQL password or unspecified. |
| 2 | `IsAADConnStringsSetup + IsManagedIdentitySetup` | AAD password connection string is configured and managed identity is enabled. |
| 2 | `IsDNSCachingSetup` | DNS caching connection string is configured. |
| 2 | `IsDataClassificationSupported` | Target exposes `SYS.SENSITIVITY_CLASSIFICATIONS`. |
| 2 | `IsEnvironmentAvailable` | Local SQL data-source-enumerator environment helper passes. |
| 1 | `AreConnStringsSetup + IsAzure + IsAccessTokenSetup + IsManagedIdentitySetup` | Local AAD helper requires connection strings, Azure SQL, access token, and managed identity support. |
| 1 | `AreConnStringsSetup + IsAzure + IsAccessTokenSetup + IsManagedIdentitySetup + SupportsSystemAssignedManagedIdentity` | Same as above, plus system-assigned managed identity support. |
| 1 | `AreConnStringsSetup + IsNotAzureServer + IsNotAzureSynapse + IsUTF8Supported` | Manual connection strings are configured, target is not Azure SQL/Synapse, and UTF-8 support probe passes. |
| 1 | `AreConnStringsSetup + IsNotAzureServer + IsNotManagedInstance + IsNotKerberosTest` | Manual connection strings are configured, target is not Azure SQL/MI, and Kerberos test config is absent. |
| 1 | `AreConnStringsSetup + IsNotAzureServer + IsNotNamedInstance + IsUsingNativeSNI` | Manual connection strings are configured for non-Azure, non-named-instance native SNI. |
| 1 | `AreConnStringsSetup + IsNotAzureSynapse + IsAzureServer` | Manual connection strings are configured for Azure SQL but not Synapse. |
| 1 | `AreConnStringsSetup + IsNotAzureSynapse + IsDataClassificationSupported` | Manual connection strings are configured, target is not Synapse, and data classification is supported. |
| 1 | `AreConnStringsSetup + IsSQLAliasSetup` | Manual connection strings are configured and SQL alias name is configured. |
| 1 | `AreConnStringsSetup + IsSupportingDistributedTransactions` | Manual connection strings are configured and distributed transactions are supported by platform/target. |
| 1 | `AreConnStringsSetup + IsUTF8Supported + IsNotAzureSynapse` | Same logical set as connection strings plus UTF-8 plus non-Synapse; ordering differs in source. |
| 1 | `AreConnStringsSetup + TcpConnectionStringDoesNotUseAadAuth + IsNotAzureServer` | Same logical set as connection strings plus SQL-password auth plus non-Azure-SQL; ordering differs in source. |
| 1 | `AreConnStringsSetup + UseManagedSNIOnWindows + IsNotAzureServer + IsLocalHost + IsAdmin` | Connection strings configured, managed SNI requested, local non-Azure target, and elevated process. |
| 1 | `CanUseDacConnection` | Local helper says a DAC connection can be used. |
| 1 | `DoesHostAddressContainBothIPv4AndIPv6 + IsUsingManagedSNI` | DNS caching host resolves to IPv4 and IPv6 and managed SNI is enabled. |
| 1 | `EmployeesTableHasFullTextIndex` | Local helper says the Employees table has a full-text index. |
| 1 | `IsAADConnStringsSetup + IsManagedIdentitySetup + SupportsSystemAssignedManagedIdentity` | AAD password connection string, managed identity, and system-assigned managed identity support are configured. |
| 1 | `IsAADPasswordConnStrSetup` | AAD password connection string is configured. |
| 1 | `IsAADPasswordConnStrSetup + IsAADAuthorityURLSetup` | AAD password connection string and AAD authority URL are configured. |
| 1 | `IsEnclaveAzureDatabaseSetup` | Enclave Azure database connection string is configured, enclave is enabled, and target is not Synapse. |
| 1 | `IsKerberosTest` | Kerberos domain user and password are configured. |
| 1 | `IsLocalDBEnvironmentSet + IsNativeSNI` | LocalDB is configured and native SNI is being used. |
| 1 | `IsNotAzureSynapse` | Target is not Azure Synapse. |
| 1 | `IsNotKerberos` | Local helper says Kerberos test config is absent. |
| 1 | `IsNotUsingManagedSNIOnWindows + IsNotAzureSynapse + AreConnStringsSetup + IsNotAzureServer` | Manual connection strings are configured for non-Azure-SQL/non-Synapse and managed SNI is not enabled. |
| 1 | `IsSGXEnclaveConnStringSetup` | SGX enclave connection string is configured. |
| 1 | `IsSPNPortNumberTestForNP` | Local named-pipes SPN-port test helper passes. |
| 1 | `IsSPNPortNumberTestForTCP` | Local TCP SPN-port test helper passes. |
| 1 | `IsTCPConnectionStringPasswordIncluded` | TCP connection string contains `Password` or `PWD`. |
| 1 | `IsTCPConnectionStringSetup + IsValidDataSource` | Local configurable-IP-preference helper has a TCP connection string and valid data source. |
| 1 | `IsTargetReadyForAeWithKeyStore + IsAKVSetupAvailable` | Always Encrypted key-store target is ready and AKV is configured. |
| 1 | `IsUsingManagedSNI` | Managed SNI is enabled. |
| 1 | `UsernamePasswordNonEncryptedConnectionSetup` | Local helper says username/password non-encrypted connection setup is available. |
| 1 | `s_DelegatedTransactionCondition` | Local distributed-transaction condition: connection strings, not Azure SQL, not x86. |
| 1 | `s_EnlistedTransactionPreservedWhilePooledCondition` | Local transaction condition: connection strings, not x86, not Managed Instance. |

## Individual Condition Frequencies

| Count | Condition | Meaning |
| ---: | --- | --- |
| 486 | `AreConnStringsSetup` | `DataTestUtility`: both named-pipe and TCP connection strings are configured. |
| 195 | `IsNotAzureSynapse` | `DataTestUtility`: target engine edition is not Azure Synapse. |
| 134 | `IsNotAzureServer` | `DataTestUtility`: TCP data source is not Azure SQL, or connection strings are not configured. |
| 46 | `IsTargetReadyForAeWithKeyStore` | `DataTestUtility`: AE connection setup is available; on non-Windows AKV must also be available. |
| 32 | `IsAKVSetupAvailable` | `DataTestUtility`: AKV URI, managed identity client id, tenant id, and non-Synapse target are available. |
| 28 | `IsAADConnStringsSetup` | Local helper, mainly AAD tests: AAD password connection string is configured. |
| 25 | `IsUdtTestDatabasePresent` | `DataTestUtility`: `UdtTestDb` exists and target is not Synapse. |
| 19 | `AreConnStringSetupForAE` | `DataTestUtility`: AE connection-string list is non-empty and target is not Synapse. |
| 17 | `CanRunSchemaTests` | Local schema-test helper. Defined in adapter/schema test files. |
| 15 | `IsNotManagedInstance` | `DataTestUtility`: config does not identify target as Managed Instance. |
| 15 | `IsSqlVectorSupported` | `DataTestUtility`: target exposes the `vector` type. |
| 12 | `IsJsonSupported` | `DataTestUtility`: target exposes the `json` type. |
| 11 | `IsLocalHost` | `DataTestUtility`: TCP data source host parses as `localhost`. |
| 10 | `IsLocalDBEnvironmentSet` | LocalDB test helper: LocalDB app name is configured and integrated security is supported. |
| 10 | `IsNotNamedInstance` | `DataTestUtility`: TCP data source does not contain a named-instance separator. |
| 9 | `IsAccessTokenSetup` | `DataTestUtility` or local AAD wrapper: an AAD access token can be obtained. |
| 8 | `AreConnectionStringsSetup` | Local wrapper around `DataTestUtility.AreConnStringsSetup`. |
| 8 | `IsIntegratedSecurityEnvironmentSet` | Local wrapper around `DataTestUtility.IsIntegratedSecuritySetup`. |
| 7 | `IsSqlVectorFloat16Supported` | `DataTestUtility`: target supports `vector` and a `VECTOR(..., float16)` probe succeeds. |
| 6 | `IsTCPConnStringSetup` | `DataTestUtility`: TCP connection string is configured. |
| 6 | `IsUsingManagedSNI` | `DataTestUtility`: managed SNI is enabled by config. |
| 5 | `IsManagedIdentitySetup` | Local AAD helper: managed identity support is enabled by config. |
| 5 | `TcpConnectionStringDoesNotUseAadAuth` | `DataTestUtility`: TCP auth is SQL password or unspecified. |
| 3 | `IsAdmin` | `DataTestUtility`: process is running as Windows administrator. |
| 3 | `IsAtLeastSQL2017` | `DataTestUtility`: SQL Server major version is 14 or later. |
| 3 | `IsDataClassificationSupported` | `DataTestUtility`: `SYS.SENSITIVITY_CLASSIFICATIONS` object exists. |
| 3 | `IsFileStreamEnvironmentSet` | Local wrapper around `DataTestUtility.IsFileStreamSetup`. |
| 3 | `IsLocalDbSharedInstanceSet` | LocalDB test helper: shared instance name and integrated security are configured. |
| 2 | `EnclaveEnabled` | `DataTestUtility`: enclave testing is enabled by config. |
| 2 | `IsAADPasswordConnStrSetup` | `DataTestUtility`: AAD password connection string is configured. |
| 2 | `IsAtLeastSQL2019` | `DataTestUtility`: SQL Server major version is 15 or later. |
| 2 | `IsAzure` | Local AAD helper: target is Azure SQL. |
| 2 | `IsDNSCachingSetup` | `DataTestUtility`: DNS caching connection string is configured. |
| 2 | `IsEnvironmentAvailable` | Local SQL data-source-enumerator environment helper. |
| 2 | `IsUTF8Supported` | `DataTestUtility`: target reports UTF-8 support and is not Synapse. |
| 2 | `SupportsSystemAssignedManagedIdentity` | `DataTestUtility`: system-assigned managed identity is enabled by config. |
| 1 | `CanUseDacConnection` | Local connectivity helper for DAC support. |
| 1 | `DoesHostAddressContainBothIPv4AndIPv6` | `DataTestUtility`: DNS caching host resolves to both IPv4 and IPv6. |
| 1 | `EmployeesTableHasFullTextIndex` | Local exception-test helper. |
| 1 | `IsAADAuthorityURLSetup` | `DataTestUtility`: AAD authority URL is configured. |
| 1 | `IsAzureServer` | `DataTestUtility`: TCP data source is Azure SQL. |
| 1 | `IsEnclaveAzureDatabaseSetup` | `DataTestUtility`: enclave Azure DB config is present and target is not Synapse. |
| 1 | `IsKerberosTest` | `DataTestUtility`: Kerberos domain user/password are configured. |
| 1 | `IsNativeSNI` | LocalDB test wrapper around `DataTestUtility.IsUsingNativeSNI`. |
| 1 | `IsNotKerberos` | Local exception-test helper: Kerberos test config is not active. |
| 1 | `IsNotKerberosTest` | `DataTestUtility`: Kerberos test config is not active. |
| 1 | `IsNotUsingManagedSNIOnWindows` | `DataTestUtility`: managed SNI is not enabled. |
| 1 | `IsSGXEnclaveConnStringSetup` | `DataTestUtility`: SGX enclave connection string is configured. |
| 1 | `IsSPNPortNumberTestForNP` | Local instance-name helper for named-pipes SPN-port test. |
| 1 | `IsSPNPortNumberTestForTCP` | Local instance-name helper for TCP SPN-port test. |
| 1 | `IsSQLAliasSetup` | `DataTestUtility`: SQL alias name is configured. |
| 1 | `IsSupportingDistributedTransactions` | `DataTestUtility`: platform and target support distributed transactions. |
| 1 | `IsTCPConnectionStringPasswordIncluded` | `DataTestUtility`: TCP connection string includes a password key. |
| 1 | `IsTCPConnectionStringSetup` | Local configurable-IP-preference helper. |
| 1 | `IsUsingNativeSNI` | `DataTestUtility`: native SNI path is active. |
| 1 | `IsValidDataSource` | Local configurable-IP-preference helper validates TCP data source. |
| 1 | `UseManagedSNIOnWindows` | `DataTestUtility`: managed SNI config flag. |
| 1 | `UsernamePasswordNonEncryptedConnectionSetup` | Local connectivity helper. |
| 1 | `s_DelegatedTransactionCondition` | Local distributed-transaction condition. |
| 1 | `s_EnlistedTransactionPreservedWhilePooledCondition` | Local transaction pooling condition. |

## Feasibility of a Readable Attribute

A readable connection-target attribute looks feasible and would help the common cases. The top three combinations alone cover 395 of 704 conditional attributes:

- `AreConnStringsSetup`
- `AreConnStringsSetup + IsNotAzureSynapse`
- `AreConnStringsSetup + IsNotAzureServer`

A flag enum such as `ConnectionTypes` would be useful if it models target exclusions and setup requirements separately. For example:

```csharp
[ConnectionStringFact(Requires = ConnectionRequirement.Configured, Exclude = ConnectionTypes.AzureSynapse)]
[ConnectionStringTheory(Exclude = ConnectionTypes.AzureSql | ConnectionTypes.AzureSynapse)]
```

The proposed `[ConnectionStringFact(~ConnectionTypes.AzureSynapse)]` shape is compact, but a naked bitwise complement can be hard to read because it does not say what universe is being complemented. An explicit `Exclude` or `Targets` named argument would be clearer in test code and easier to evolve.

The main benefit would be readability and consistency around target categories: configured connection strings, Azure SQL, Azure Synapse, Managed Instance, localhost, named instance, and SNI mode. The main limitation is that many rare conditions are feature probes or environment probes (`IsJsonSupported`, `IsSqlVectorSupported`, AKV, LocalDB, Kerberos, admin, DNS, distributed transactions). Those should probably remain composable predicates instead of being forced into a connection-type enum.

Recommended direction: introduce a small family of attributes for the common connection target cases, while preserving the current predicate-based mechanism for specialized feature/environment checks.
