# Feature Specification: Async-Friendly Always Encrypted Support

**Issue**: [#3672](https://github.com/dotnet/SqlClient/issues/3672)  
**Feature Branch**: `dev/cheena/async-ae`  
**Created**: 2026-05-13  
**Status**: Draft  
**Milestone**: 7.1.0-preview2  

## Summary

Eliminate sync-over-async in Always Encrypted (AE) code paths by adding async counterparts to all provider base classes and implementations, then rewiring async execution paths in SqlCommand to use them. Currently, async operations like `ExecuteReaderAsync` block ThreadPool threads when AE is enabled because all key decryption, signature verification, and enclave attestation calls are synchronous — including HTTP calls to Azure Key Vault and attestation services.

## User Scenarios & Testing

### User Story 1 — Async Key Decryption with Azure Key Vault

As an application developer using Always Encrypted with Azure Key Vault, I want `ExecuteReaderAsync` to perform column encryption key (CEK) decryption asynchronously via the Azure Key Vault SDK, so that my async code paths do not block ThreadPool threads waiting for HTTP responses from Azure Key Vault.

**Acceptance Scenarios**:

1. **Given** a table with AE-encrypted columns and AKV-stored CMK, **When** `ExecuteReaderAsync` is called, **Then** CEK decryption calls `CryptographyClient.UnwrapKeyAsync()` instead of the sync `UnwrapKey()`.
2. **Given** a `CancellationToken` passed to `ExecuteReaderAsync`, **When** the token is cancelled during AKV key decryption, **Then** the operation is cancelled and a `TaskCanceledException` is thrown.
3. **Given** a custom provider that does NOT override async methods, **When** `ExecuteReaderAsync` is called, **Then** the base class fallback wraps the sync method in `Task.FromResult` and completes successfully.

### User Story 2 — Async Enclave Attestation

As an application developer using Always Encrypted with secure enclaves, I want enclave attestation to be performed asynchronously, so that HTTP calls to attestation services do not block my async query execution path.

**Acceptance Scenarios**:

1. **Given** an enclave-enabled query using Azure Attestation, **When** `ExecuteReaderAsync` is called, **Then** `ConfigurationManager<OpenIdConnectConfiguration>.GetConfigurationAsync()` is used.
2. **Given** an enclave-enabled query using HGS attestation, **When** `ExecuteReaderAsync` is called, **Then** the HTTP call uses `await HttpClient.GetStreamAsync()` instead of `.Result`.

### User Story 3 — Sync Path Preservation

All existing sync methods are preserved as-is. The `isAsync` flag in `SqlCommand` determines which path is taken. Zero behavioral change for sync callers.

### User Story 4 — Custom Provider Backward Compatibility

Custom providers that only implement sync `DecryptColumnEncryptionKey` continue to work. The base class default wraps the sync call in `Task.FromResult`. If the sync method throws, the returned Task is faulted (not a synchronous throw).

### Edge Cases

- **Cache hit for CEK**: Async path returns immediately from cache without invoking the provider.
- **Mixed sync/async on `SemaphoreSlim`**: For sites still using `SemaphoreSlim` (non-`IMemoryCache` caches), mixing `.Wait()` and `.WaitAsync()` is safe per .NET docs. For `IMemoryCache`-backed caches, the async path uses `GetOrCreateAsync` and does not need `SemaphoreSlim`.
- **CancellationToken in default fallback**: The base class wraps sync methods and checks the token before invocation; sync calls themselves are not cancellable.
- **Concurrent cache misses**: Two threads may independently fetch the same key. Last write wins (idempotent). See FR-014 for optional deduplication.
- **Enclave session cache hit**: Returns from cache without performing attestation.
- **`ColumnEncryptionKeyCacheTtl` mutation**: Property mutation must remain inside the semaphore-held section in `SqlSymmetricKeyCache` to avoid races.

## Design Decisions

### 1. `Task<T>` for Public APIs

Public virtual methods use `Task<T>`, not `ValueTask<T>`. The primary beneficiary (AKV) always allocates a Task due to HTTP I/O. `ValueTask<T>` has stricter usage rules that would be error-prone for third-party implementers.

### 2. Two Overloads Per Async Method

Each async method provides: a `virtual` overload accepting `CancellationToken` (overridable), and a non-virtual convenience overload that delegates with `CancellationToken.None`. Follows the standard .NET library pattern.

### 3. Certificate/CNG/CSP Providers Keep Default Fallback

No async overrides — these perform local crypto (CPU-bound) that doesn't benefit from async. The base class `Task.FromResult` fallback is sufficient.

### 4. Async Methods Return Tuples Instead of `out` Parameters

C# async methods cannot have `out` parameters. Internal enclave methods return tuples (e.g., `Task<(SqlEnclaveSession, long, byte[], int)>`). `SqlSecurityUtility.DecryptSymmetricKeyAsync` returns `Task<(SqlClientSymmetricKey Key, SqlEncryptionKeyInfo KeyInfoChosen)>`.

### 5. `ConfigureAwait(false)` on Every `await`

Every `await` in library code MUST use `.ConfigureAwait(false)`. Omission causes deadlocks in ASP.NET Framework, WPF, and WinForms hosts. This is a hard correctness requirement enforced in CI.

### 6. `MemoryCache.GetOrCreateAsync` for Async Cache Patterns

**API**: [`CacheExtensions.GetOrCreateAsync<TItem>(IMemoryCache, Object, Func<ICacheEntry, Task<TItem>>)`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.cacheextensions.getorcreateasync?view=net-10.0-pp)

For `IMemoryCache`-backed caches, use `GetOrCreateAsync` with an async factory delegate instead of `SemaphoreSlim` + manual `TryGetValue`/`Set`. This eliminates lock-during-I/O without requiring the check-release-fetch-relock pattern.

```csharp
var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
{
    entry.SlidingExpiration = _columnEncryptionKeyCacheTtl;
    return await provider.DecryptColumnEncryptionKeyAsync(
        masterKeyPath, algorithm, encryptedKey, ct)
        .ConfigureAwait(false);
}).ConfigureAwait(false);
```

**Affected**: `SqlColumnEncryptionAzureKeyVaultProvider._columnEncryptionKeyCache`

For sites NOT using `IMemoryCache` (custom `Dictionary`/`ConcurrentDictionary` caches), use the check-release-fetch-relock pattern with `SemaphoreSlim`:

```csharp
await semaphore.WaitAsync(ct).ConfigureAwait(false);
try
{
    if (cache.TryGetValue(key, out var cached))
        return cached;
}
finally { semaphore.Release(); }  // release BEFORE I/O

var value = await FetchAsync(key, ct).ConfigureAwait(false);

await semaphore.WaitAsync(ct).ConfigureAwait(false);
try { cache.Set(key, value); }
finally { semaphore.Release(); }
return value;
```

**Affected**: `SqlSymmetricKeyCache._cacheLock`, `AzureSqlKeyCryptographer._keyDictionarySemaphore`

### 7. `GetParameterEncryptionDataReaderAsync` Must Be True `async Task`

Remove the `Task.Run(() => { ... })` wrapper entirely. The method must `await` each I/O step directly using clean `async/await`, not the `AsyncHelper.ContinueTaskWithState` callback pattern.

## Requirements

### Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-001 | Add `virtual` async counterparts for `DecryptColumnEncryptionKey`, `EncryptColumnEncryptionKey`, `SignColumnMasterKeyMetadata`, and `VerifyColumnMasterKeyMetadata` to `SqlColumnEncryptionKeyStoreProvider`. |
| FR-002 | Each async method provides two overloads: `virtual` with `CancellationToken` + non-virtual without. Default checks token before sync fallback. |
| FR-003 | Default async implementations wrap sync via `Task.FromResult`; return faulted Tasks (not synchronous throws) on exception. |
| FR-004 | `SqlColumnEncryptionAzureKeyVaultProvider` overrides all 4 async methods with truly async Azure SDK calls. |
| FR-005 | AKV provider propagates `CancellationToken` to all Azure SDK calls. |
| FR-006 | Internal async counterparts added to enclave provider base class and all implementations. |
| FR-007 | Enclave providers with HTTP I/O (Azure Attestation, HGS) implement truly async attestation. |
| FR-008 | Async utility methods added: `SqlSecurityUtility.DecryptSymmetricKeyAsync` (returns tuple), `VerifyColumnMasterKeySignatureAsync`, `SqlSymmetricKeyCache.GetKeyAsync`. All propagate `CancellationToken`. |
| FR-009 | `SqlCommand` async paths use async AE methods when `isAsync=true`. `GetParameterEncryptionDataReaderAsync` becomes true `async Task`. |
| FR-010 | No changes to existing sync code paths or behavior. |
| FR-011 | All new public APIs declared in `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.cs`. |
| FR-012 | Async caching: `IMemoryCache` sites use `GetOrCreateAsync`; custom-cache sites use check-release-fetch-relock with `SemaphoreSlim`. No lock held during I/O. |
| FR-013 | Every `await` uses `.ConfigureAwait(false)`. Enforced in CI. |
| FR-014 | Concurrent cache misses tolerated without deadlock. Last write wins. Optional deduplication via `ConcurrentDictionary<key, Task<value>>`. |
| FR-015 | `lock(enclaveCacheLock)` replaced with `SemaphoreSlim` if async operations must run before session storage. Sync-only paths may retain `lock`. |

### Key Entities

| Entity | Scope | Changes |
|--------|-------|---------|
| `SqlColumnEncryptionKeyStoreProvider` | public abstract | +4 virtual async methods, +4 non-virtual convenience overloads |
| `SqlColumnEncryptionAzureKeyVaultProvider` | public (separate package) | Overrides all 4 async methods with Azure SDK async calls |
| `AzureSqlKeyCryptographer` | internal (AKV package) | +async counterparts for `AddKey`, `UnwrapKey`, `WrapKey`, `SignData`, `VerifyData` |
| `SqlColumnEncryptionEnclaveProvider` | internal abstract | +4 abstract async methods with tuple returns |
| `EnclaveDelegate` | internal sealed | +async dispatcher methods |
| `SqlSecurityUtility` | internal static | +`DecryptSymmetricKeyAsync`, `VerifyColumnMasterKeySignatureAsync` |
| `SqlSymmetricKeyCache` | internal sealed, singleton | +`GetKeyAsync` with check-release-fetch-relock |

## Implementation Phases

### Phase 1: Base Class API (PR #3673 — in progress)

*Depends on: nothing*

Add 4 virtual async methods + 4 convenience overloads to `SqlColumnEncryptionKeyStoreProvider`, update ref assemblies and XML docs.

**Files**:
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlColumnEncryptionKeyStoreProvider.cs`
- `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.cs`
- `doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml`

### Phase 2: Key Store Provider Implementations

*Depends on: Phase 1. Parallel with Phase 3.*

#### 2A. AzureKeyVaultProvider (truly async)

- `AzureSqlKeyCryptographer.cs` — Add async counterparts using Azure SDK. `AddKeyAsync` uses check-release-fetch-relock on `_keyDictionarySemaphore` (uses `Dictionary`, not `IMemoryCache`).
- `SqlColumnEncryptionAzureKeyVaultProvider.cs` — Override 4 async methods. Use `GetOrCreateAsync` on `_columnEncryptionKeyCache`:

**Async key-unwrap flow**:
1. `await _columnEncryptionKeyCache.GetOrCreateAsync(cacheKey, async entry => { ... }).ConfigureAwait(false)`
2. Inside factory: `await KeyCryptographer.UnwrapKeyAsync(..., ct).ConfigureAwait(false)` — I/O runs without user-managed locks
3. Configure entry expiration via `ICacheEntry`
4. `MemoryCache` handles cache insertion

The sync path remains unchanged (`_cacheSemaphore.Wait()` + `GetOrCreate`). `GetOrCreateSignatureVerificationResult` stays sync (CPU-bound RSA).

#### 2B–2D. CertificateStore, CNG, CSP Providers

No changes — default base class fallback is sufficient.

### Phase 3: Enclave Provider Async APIs

*Depends on: nothing. Parallel with Phase 2.*

- `SqlColumnEncryptionEnclaveProvider.cs` — 4 internal abstract async methods
- `EnclaveProviderBase.cs` — `GetEnclaveSessionHelperAsync()`
- `EnclaveSessionCache.cs` — Replace `lock` with `SemaphoreSlim` where async operations precede session storage (FR-015)
- `NoneAttestationEnclaveProvider.cs` — Sync wrappers (no real I/O)
- `AzureAttestationBasedEnclaveProvider.cs` — Truly async (`GetConfigurationAsync()`)
- `VirtualSecureModeEnclaveProviderBase.cs` — Abstract `MakeRequestAsync`, `VerifyAttestationInfoAsync`
- `VirtualSecureModeEnclaveProvider.cs` — Truly async (`HttpClient.GetStreamAsync`)
- `EnclaveDelegate.Crypto.cs` — Async dispatcher methods

### Phase 4: Async Utility Layer

*Depends on: Phase 1.*

- `SqlSecurityUtility.cs`:
  - `DecryptSymmetricKeyAsync` → returns `Task<(SqlClientSymmetricKey Key, SqlEncryptionKeyInfo KeyInfoChosen)>`
  - `GetKeyFromLocalProvidersAsync` — propagates `CancellationToken`
  - `VerifyColumnMasterKeySignatureAsync` — propagates `CancellationToken`
- `SqlSymmetricKeyCache.cs`:
  - `GetKeyAsync` — check-release-fetch-relock on static `_cacheLock`; propagates `CancellationToken`

> **Note**: Phases 1–4 deliver async infrastructure. End-to-end benefit requires Phase 5.

### Phase 5: Async Call-Site Integration

*Depends on: Phase 4.*

- `SqlCommand.Encryption.cs`:
  1. `ReadDescribeEncryptionParameterResultsKeysAsync` → `VerifyColumnMasterKeySignatureAsync()`
  2. `ReadDescribeEncryptionParameterResultsMetadataAsync` → `DecryptSymmetricKeyAsync()` (**highest impact**)
  3. `ReadDescribeEncryptionParameterResultsAsync` → orchestrates above
  4. **Refactor `GetParameterEncryptionDataReaderAsync`** into true `async Task` — remove `Task.Run` wrapper
  5. `TryFetchInputParameterEncryptionInfoAsync` → `EnclaveDelegate.GetEnclaveSessionAsync()`

### Phase 6: Testing & Documentation

*Spans all phases.*

**Unit Tests** (`tests/UnitTests/`): Default fallback, cancellation, faulted Tasks  
**Functional Tests** (`tests/FunctionalTests/`): `DummyKeyStoreProvider` async overrides  
**Manual Tests** (`tests/ManualTests/`): AKV end-to-end, enclave attestation async, encrypted query async  
**CI Gate**: Roslyn analyzer or grep check for `.ConfigureAwait(false)` on every `await`  
**Documentation**: XML docs, `doc/samples/` async AE examples

## New Public API Surface

### SqlColumnEncryptionKeyStoreProvider

```csharp
public abstract class SqlColumnEncryptionKeyStoreProvider
{
    // Existing (unchanged)
    public abstract byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);
    public abstract byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);
    public virtual byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations);
    public virtual bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature);
    public virtual TimeSpan? ColumnEncryptionKeyCacheTtl { get; set; }

    // NEW: Async with CancellationToken (virtual, overridable)
    public virtual Task<byte[]> DecryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey, CancellationToken cancellationToken);
    public virtual Task<byte[]> EncryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey, CancellationToken cancellationToken);
    public virtual Task<byte[]> SignColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, CancellationToken cancellationToken);
    public virtual Task<bool> VerifyColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, byte[] signature, CancellationToken cancellationToken);

    // NEW: Convenience overloads (non-virtual, delegate with CancellationToken.None)
    public Task<byte[]> DecryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);
    public Task<byte[]> EncryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);
    public Task<byte[]> SignColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations);
    public Task<bool> VerifyColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, byte[] signature);
}
```

### SqlColumnEncryptionAzureKeyVaultProvider

```csharp
public class SqlColumnEncryptionAzureKeyVaultProvider : SqlColumnEncryptionKeyStoreProvider
{
    // Overrides — truly async via Azure SDK
    public override Task<byte[]> DecryptColumnEncryptionKeyAsync(..., CancellationToken cancellationToken);
    public override Task<byte[]> EncryptColumnEncryptionKeyAsync(..., CancellationToken cancellationToken);
    public override Task<byte[]> SignColumnMasterKeyMetadataAsync(..., CancellationToken cancellationToken);
    public override Task<bool> VerifyColumnMasterKeyMetadataAsync(..., CancellationToken cancellationToken);
}
```

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-001 | `ExecuteReaderAsync` with AE + AKV does NOT block any ThreadPool thread on HTTP I/O. |
| SC-002 | All existing AE tests pass unchanged — zero sync regressions. |
| SC-003 | `Task.Run` is no longer used in `GetParameterEncryptionDataReaderAsync`. |
| SC-004 | `CancellationToken` propagates from `SqlCommand` async methods through to Azure SDK calls. |
| SC-005 | Custom providers with only sync implementations continue to work in async paths. |

## Assumptions

- Azure SDK async methods (`UnwrapKeyAsync`, `WrapKeyAsync`, etc.) are stable and production-ready.
- `SemaphoreSlim` supports mixed `.Wait()` / `.WaitAsync()` on the same instance.
- `SqlColumnEncryptionEnclaveProvider` is internal — API changes have no public impact.
- The existing `isAsync` flag in `SqlCommand` determines sync vs async AE paths.
- PR #3673 has not shipped — adding `CancellationToken` is non-breaking.
- Certificate/CNG/CSP providers perform local crypto; `Task.FromResult` is sufficient.
- `Microsoft.Extensions.Caching.Abstractions` (already referenced by AKV provider) provides `GetOrCreateAsync`.

## Further Considerations

1. **`ConfigureAwait(false)` CI enforcement**: Add a Roslyn analyzer or grep-based check to prevent regressions.
2. **Third-party provider guidance**: Document how custom providers should override async methods, use `ConfigureAwait(false)`, support cancellation, and avoid lock-during-I/O.
3. **Performance benchmarking**: After Phase 5, quantify ThreadPool savings and latency improvements under load.
4. **In-flight request deduplication**: Optional `ConcurrentDictionary<string, Task<byte[]>>` for concurrent cold-cache AKV requests (FR-014).
5. **`ValueTask<T>` for internal fast paths**: Internal methods like `SqlSymmetricKeyCache.GetKeyAsync` have high cache-hit rates — `ValueTask<T>` avoids Task allocations. P2 optimization with no API impact.
