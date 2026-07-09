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

**Independent Test**: Execute `ExecuteReaderAsync` against a table with encrypted columns using AKV-managed keys, and verify that no ThreadPool thread is blocked during CEK decryption.

**Acceptance Scenarios**:

1. **Given** a table with AE-encrypted columns and AKV-stored Column Master Key (CMK), **When** `ExecuteReaderAsync` is called, **Then** the CEK decryption calls `CryptographyClient.UnwrapKeyAsync()` instead of the sync `UnwrapKey()`.
2. **Given** a `CancellationToken` passed to `ExecuteReaderAsync`, **When** the token is cancelled during AKV key decryption, **Then** the operation is cancelled and a `TaskCanceledException` is thrown.
3. **Given** a custom `SqlColumnEncryptionKeyStoreProvider` that does NOT override async methods, **When** `ExecuteReaderAsync` is called, **Then** the default base class fallback wraps the sync method in `Task.FromResult` and the operation completes successfully (backward compatible).

---

### User Story 2 — Async Enclave Attestation

As an application developer using Always Encrypted with secure enclaves, I want enclave attestation (Azure Attestation or HGS) to be performed asynchronously, so that HTTP calls to attestation services do not block my async query execution path.

**Independent Test**: Execute an enclave-enabled query via `ExecuteReaderAsync` with Azure Attestation and verify that the OpenID configuration fetch and JWT validation are performed asynchronously.

**Acceptance Scenarios**:

1. **Given** an enclave-enabled query using Azure Attestation, **When** `ExecuteReaderAsync` is called, **Then** `ConfigurationManager<OpenIdConnectConfiguration>.GetConfigurationAsync()` is used instead of the sync variant.
2. **Given** an enclave-enabled query using HGS attestation, **When** `ExecuteReaderAsync` is called, **Then** the HTTP call to the HGS signing certificates endpoint uses `await HttpClient.GetStreamAsync()` instead of `.Result`.

---

### User Story 3 — Sync Path Preservation

As an application developer using synchronous APIs (`ExecuteReader`, `ExecuteNonQuery`, etc.) with Always Encrypted, I want the existing behavior to remain exactly the same, so that this feature introduces zero regressions in sync code paths.

**Independent Test**: Run the full existing AE test suite using sync APIs and verify all tests pass with no behavioral changes.

**Acceptance Scenarios**:

1. **Given** any sync API call with AE-encrypted parameters, **When** the call is executed, **Then** the sync code path uses the existing sync provider methods with no changes.

---

### User Story 4 — Custom Key Store Provider Backward Compatibility

As a third-party developer who has implemented a custom `SqlColumnEncryptionKeyStoreProvider`, I want the new async methods in the base class to have a safe default implementation, so that my existing provider continues to work without modification.

**Independent Test**: Register a custom provider that only overrides the abstract sync methods and verify it works correctly in both sync and async query paths.

**Acceptance Scenarios**:

1. **Given** a custom provider that only implements sync `DecryptColumnEncryptionKey`, **When** `DecryptColumnEncryptionKeyAsync` is called, **Then** the base class default wraps the sync call in `Task.FromResult` and returns successfully.
2. **Given** a custom provider whose sync method throws an exception, **When** the async variant is called, **Then** the returned Task is faulted (not a synchronous throw).

---

### Edge Cases

- **Cache hit for CEK**: If the decrypted CEK is already cached in `SqlSymmetricKeyCache`, the async path MUST return immediately from cache without invoking the provider. Under the check-release-fetch-relock pattern (Decision 7), cache hits are detected while the semaphore is held and returned before the lock is released — this is the fast path.
- **Mixed sync/async provider calls on same `SemaphoreSlim`**: Mixing `.Wait()` and `.WaitAsync()` on the same `SemaphoreSlim` instance is safe per .NET documentation. Sync callers continue using `.Wait()`; async callers use `.WaitAsync()`. Do NOT change the sync path to use `.WaitAsync().GetAwaiter().GetResult()` — that pattern deadlocks under a synchronization context.
- **CancellationToken in default fallback**: The default base class implementation wraps sync methods and ignores `CancellationToken` since sync calls are not cancellable. Document this behavior.
- **Multiple encrypted parameters serialized by lock**: When a query has N encrypted parameters, `DecryptSymmetricKeyAsync` is called N times. Under the old lock-during-I/O pattern, these would serialize on `_cacheLock`. Under the check-release-fetch-relock pattern (Decision 7), cache misses proceed concurrently with no lock held during I/O.
- **Concurrent cache misses for same key**: Two threads may both miss the cache and each independently fetch the same key from AKV. The last write wins (idempotent). This is an accepted trade-off; see FR-014 for optional deduplication.
- **Enclave session cache hit**: If an enclave session already exists in cache, the async path should return from cache without performing attestation.
- **Provider `EncryptColumnEncryptionKeyAsync`**: While decrypt is the hot path (used on every query), encrypt is used by tooling (SSMS, SqlPackage). Still needs async support for completeness.
- **`ColumnEncryptionKeyCacheTtl` mutation in `SqlSymmetricKeyCache`**: The existing sync code sets `provider.ColumnEncryptionKeyCacheTtl = new TimeSpan(0)` while the semaphore is held. In the async version, this property mutation must remain inside the semaphore-held section to avoid a race with concurrent async callers on the same provider instance.

## Design Decisions

### Decision 1: `Task<T>` over `ValueTask<T>` for Public APIs

**Chosen**: `Task<T>`

**Rationale**: Public virtual methods that consumers override. The primary beneficiary (AKV) always allocates a Task due to HTTP I/O. `ValueTask<T>` has stricter usage rules (single await, no caching) that would be error-prone for third-party implementers. `Task<T>` is the safer, simpler contract for public extensibility.

### Decision 2: `CancellationToken` on All Async Methods

**Chosen**: Provide two overloads per async method: a `virtual` overload accepting an explicit `CancellationToken` parameter, and a non-virtual convenience overload (without `CancellationToken`) that delegates to the first passing `CancellationToken.None`. The default implementation checks the token for cancellation before invoking the synchronous method.

**Rationale**: This follows the standard .NET library overload pattern for async APIs. It is critical for AKV timeout scenarios where HTTP calls may hang. The explicit token overload allows derived classes to honor cancellation. Since PR #3673 hasn't shipped, now is the time.

### Decision 3: Certificate/CNG/CSP Providers Keep Default Fallback

**Chosen**: No async overrides for `SqlColumnEncryptionCertificateStoreProvider`, `SqlColumnEncryptionCngProvider`, or `SqlColumnEncryptionCspProvider`.

**Rationale**: These providers perform local crypto (X509Store, CNG keys, CSP registry) — CPU-bound operations that don't benefit from async. The base class `Task.FromResult` fallback is appropriate. Avoids unnecessary code complexity.

### Decision 4: Enclave Provider Uses Tuples Instead of `out` Parameters

**Chosen**: Async enclave methods return tuples (e.g., `Task<(SqlEnclaveSession, long, byte[], int)>`) instead of using `out` parameters.

**Rationale**: C# async methods cannot have `out` parameters. `SqlColumnEncryptionEnclaveProvider` is **internal**, so the API change has no public surface impact.

### Decision 5: Sync Methods Remain Unchanged

**Chosen**: All existing sync methods are preserved as-is. New async methods are added alongside them.

**Rationale**: Zero behavioral change for sync callers. The `isAsync` flag already used throughout SqlCommand determines which path is taken.

### Decision 6: `ConfigureAwait(false)` Required on All `await` in Library Code

**Chosen**: Every `await` expression in implementation code (all phases) MUST use `.ConfigureAwait(false)`.

**Rationale**: SqlClient is a library, not an application. If `ConfigureAwait(false)` is omitted, the `await` continuation is scheduled back onto the caller's `SynchronizationContext`. In app models that have a synchronization context (ASP.NET Framework, WPF, WinForms), any code that calls async methods with `.Result` or `.Wait()` (common in app code calling library APIs) will deadlock. This is a required invariant for all library async code in the .NET ecosystem. Applies to Phases 2, 3, 4, and 5.

### Decision 7: Lock-During-I/O Must Use Check-Release-Fetch-Relock Pattern

**Chosen**: In all async code paths, the `SemaphoreSlim` MUST be released before performing I/O (HTTP calls to AKV, Azure Attestation, HGS), then re-acquired to update the cache.

**Rationale**: Three existing `SemaphoreSlim` sites hold their lock for the full duration of I/O:

- `SqlSymmetricKeyCache._cacheLock` — static singleton, held during `DecryptColumnEncryptionKey`
- `AzureSqlKeyCryptographer._keyDictionarySemaphore` — held during AKV key fetch
- `SqlColumnEncryptionAzureKeyVaultProvider._cacheSemaphore` — held during CEK decrypt

Simply swapping `.Wait()` to `.WaitAsync()` does not fix the problem. The lock is still held during HTTP I/O, which serializes all concurrent requests through a single thread gate — the opposite of what async is intended to achieve. `_cacheLock` in `SqlSymmetricKeyCache` is a **static field shared across all connections**, making this especially severe.

**Required pattern** for all three sites:

```csharp
await semaphore.WaitAsync(ct).ConfigureAwait(false);
try
{
    if (cache.TryGetValue(key, out var cached))
        return cached;
}
finally { semaphore.Release(); }  // release BEFORE any I/O

var value = await FetchAsync(key, ct).ConfigureAwait(false);

await semaphore.WaitAsync(ct).ConfigureAwait(false);
try { cache.Set(key, value); }
finally { semaphore.Release(); }
return value;
```

> **Important**: The first acquire MUST be wrapped in `try/finally` to guarantee semaphore release if `TryGetValue` or any cache-inspection code throws unexpectedly.

Two threads may independently fetch the same key on a concurrent miss; the second write is idempotent (same decrypted value). This is acceptable and already implicitly assumed by the existing comment in `SqlSymmetricKeyCache`: "first one wins."

### Decision 8: `IMemoryCache` Has No `GetOrCreateAsync` — Use Explicit `TryGetValue`/`Set`

**Chosen**: All async paths that currently call `IMemoryCache.GetOrCreate(key, Func<T>)` MUST be decomposed to `TryGetValue` + async compute + `Set`.

**Rationale**: `Microsoft.Extensions.Caching.Memory.IMemoryCache` does not provide a `GetOrCreateAsync(key, Func<Task<T>>)` overload. Using `GetOrCreate` with a sync factory in an async context either blocks (sync-over-async) or requires wrapping the cache call in `Task.Run`. The correct pattern is explicit decomposition, allowing the async factory to run free of any cache lock.

**Affected sites**: `SqlColumnEncryptionAzureKeyVaultProvider.GetOrCreateColumnEncryptionKey`. Note: `GetOrCreateSignatureVerificationResult` is purely sync (CPU-bound RSA verify) and can stay as-is for async paths.

### Decision 9: `DecryptSymmetricKeyAsync` Returns a Tuple Instead of `out` Parameters

**Chosen**: `SqlSecurityUtility.DecryptSymmetricKeyAsync` returns `Task<(SqlClientSymmetricKey Key, SqlEncryptionKeyInfo KeyInfoChosen)>` instead of using `out` parameters.

**Rationale**: The existing sync signature `void DecryptSymmetricKey(entry, out SqlClientSymmetricKey key, out SqlEncryptionKeyInfo keyInfo, ...)` cannot have an async counterpart because C# async methods cannot have `out` parameters. This is consistent with Decision 4 for enclave provider APIs.

### Decision 10: `GetParameterEncryptionDataReaderAsync` Becomes a True `async Task` Method

**Chosen**: `GetParameterEncryptionDataReaderAsync` in Phase 5 MUST be refactored to a proper `async Task` method signature, not retain the current `Task.Run(() => { ... })` structure.

**Rationale**: The current implementation wraps the entire CEK decryption pipeline inside `Task.Run`, which consumes a ThreadPool thread for the duration of AKV HTTP I/O. This is the primary sync-over-async bottleneck (SC-001 and SC-003). The replacement must be a genuine `async Task` with `await` at each I/O boundary. Similarly, the sync `GetParameterEncryptionDataReader` uses the `AsyncHelper.ContinueTaskWithState` callback pattern; Phase 5 should not extend this pattern — the async path should use clean `async/await`.

## Requirements

### Functional Requirements

**FR-001**: System MUST add `virtual async` counterparts for `DecryptColumnEncryptionKey`, `EncryptColumnEncryptionKey`, `SignColumnMasterKeyMetadata`, and `VerifyColumnMasterKeyMetadata` to the public `SqlColumnEncryptionKeyStoreProvider` base class.

**FR-002**: All new async public methods MUST provide two overloads: one accepting `CancellationToken cancellationToken` (virtual, overridable) and one without (non-virtual, delegates to the first with `CancellationToken.None`). The default implementation MUST check the token for cancellation before executing the synchronous fallback.

**FR-003**: Default implementations of async methods in the base class MUST wrap the sync counterpart via `Task.FromResult`, and MUST return faulted Tasks (not throw synchronously) when the sync method throws.

**FR-004**: `SqlColumnEncryptionAzureKeyVaultProvider` MUST override all 4 async methods with truly async implementations using Azure SDK async APIs (`UnwrapKeyAsync`, `WrapKeyAsync`, `SignDataAsync`, `VerifyDataAsync`, `GetKeyAsync`).

**FR-005**: `SqlColumnEncryptionAzureKeyVaultProvider` async methods MUST propagate `CancellationToken` to underlying Azure SDK calls.

**FR-006**: System MUST add internal async counterparts to enclave provider base class and all implementations (`NoneAttestationEnclaveProvider`, `AzureAttestationEnclaveProvider`, `HostGuardianServiceEnclaveProvider`).

**FR-007**: Enclave providers with HTTP I/O (Azure Attestation, HGS) MUST implement truly async attestation flows.

**FR-008**: System MUST add async counterparts to intermediate utility methods: `SqlSecurityUtility.DecryptSymmetricKeyAsync`, `SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync`, `SqlSymmetricKeyCache.GetKeyAsync`. `DecryptSymmetricKeyAsync` MUST return `Task<(SqlClientSymmetricKey Key, SqlEncryptionKeyInfo KeyInfoChosen)>` (per Decision 9, replacing `out` parameters). `CancellationToken` MUST be threaded through from each async utility method to the provider async call.

**FR-009**: `SqlCommand` async execution paths MUST use async AE methods when `isAsync=true`, eliminating `Task.Run` wrappers and sync-over-async patterns in `GetParameterEncryptionDataReaderAsync` and related methods. `GetParameterEncryptionDataReaderAsync` MUST be converted to a true `async Task` method (per Decision 10), not retain the `Task.Run` wrapper.

**FR-010**: System MUST NOT change any existing sync code paths or behavior.

**FR-011**: All new public APIs MUST be declared in the reference assembly at `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.cs`.

**FR-012**: All async paths involving `SemaphoreSlim` cache locks MUST implement the check-release-fetch-relock pattern (per Decision 7). Simply replacing `.Wait()` with `.WaitAsync()` while holding the semaphore during I/O is **not** acceptable. Affected: `SqlSymmetricKeyCache._cacheLock`, `AzureSqlKeyCryptographer._keyDictionarySemaphore`, `SqlColumnEncryptionAzureKeyVaultProvider._cacheSemaphore`.

**FR-013**: Every `await` expression in async implementation code (Phases 2–5) MUST use `.ConfigureAwait(false)` per Decision 6. This is a hard correctness requirement, not a style preference.

**FR-014**: Async cache-miss paths MUST tolerate concurrent fetches for the same key without deadlocking or throwing. When multiple threads concurrently miss the same cache entry and independently fetch the key, the last write MUST win silently (idempotent cache insert). The implementation MAY additionally deduplicate in-flight requests via a `ConcurrentDictionary<key, Task<value>>` pattern to avoid redundant AKV HTTP calls under load, though this is not required for correctness.

**FR-015**: If any async operation in Phase 3 must run before `EnclaveSessionCache.CreateSession` stores a session, the `lock(enclaveCacheLock)` statement in `EnclaveSessionCache` MUST be replaced with a `SemaphoreSlim`, because `lock` statements cannot span `await` expressions. Operations that are purely synchronous (key derivation from already-available material) may remain inside a `lock`.

### Key Entities

- **`SqlColumnEncryptionKeyStoreProvider`** (public abstract class): Base class for all column encryption key store providers. Gains 4 new virtual async methods.
- **`SqlColumnEncryptionAzureKeyVaultProvider`** (public class, separate package): The only built-in provider that benefits from truly async — every operation is HTTP I/O to Azure Key Vault.
- **`AzureSqlKeyCryptographer`** (internal class, AKV package): Wrapper around Azure SDK `CryptographyClient`/`KeyClient`. Gets async counterparts for `AddKey`, `UnwrapKey`, `WrapKey`, `SignData`, `VerifyData`.
- **`SqlColumnEncryptionEnclaveProvider`** (internal abstract class): Base class for enclave providers. Gains 4 internal abstract async methods with tuple returns.
- **`EnclaveDelegate`** (internal sealed class): Dispatcher that routes enclave calls to the correct provider. Gets async dispatcher methods.
- **`SqlSecurityUtility`** (internal static class): Orchestrator for encryption/decryption operations. Gets `DecryptSymmetricKeyAsync`, `VerifyColumnMasterKeySignatureAsync`.
- **`SqlSymmetricKeyCache`** (internal sealed class, singleton): Global cache for decrypted column encryption keys. Gets `GetKeyAsync` with async cache locking.

## Implementation Phases

### Phase 1: Base Class API Design (PR #3673 — in progress)

#### Depends on: nothing

Add 4 virtual async methods to `SqlColumnEncryptionKeyStoreProvider` with `CancellationToken`, update ref assemblies and XML docs.

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlColumnEncryptionKeyStoreProvider.cs`
- `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.cs`
- `doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml`

### Phase 2: Key Store Provider Implementations

*Depends on: Phase 1. Parallel with Phase 3.*

#### 2A. AzureKeyVaultProvider (truly async)

- `src/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider/src/AzureSqlKeyCryptographer.cs` — Add `AddKeyAsync`, `UnwrapKeyAsync`, `WrapKeyAsync`, `SignDataAsync`, `VerifyDataAsync` using Azure SDK async APIs. `AddKeyAsync` MUST apply the check-release-fetch-relock pattern to `_keyDictionarySemaphore` (per Decision 7 and FR-012) — the semaphore must be released before calling `KeyClient.GetKeyAsync()` over HTTP
- `src/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider/src/SqlColumnEncryptionAzureKeyVaultProvider.cs` — Override all 4 async methods; convert `_cacheSemaphore.Wait()` to the **check-release-fetch-relock pattern** in async paths (per Decision 7 and FR-012):
  - `GetOrCreateColumnEncryptionKey` cannot use `IMemoryCache.GetOrCreate` with a sync factory in the async path — replace with explicit `TryGetValue` + async compute + `Set` (per Decision 8 and FR-012)
  - All `await` MUST use `.ConfigureAwait(false)` (per Decision 6 and FR-013)

**Async key-unwrap flow** for `DecryptColumnEncryptionKeyAsync`:

1. `await _cacheSemaphore.WaitAsync(ct).ConfigureAwait(false)`
2. If cache hit: release semaphore, return cached value
3. If cache miss: release semaphore immediately
4. `await KeyCryptographer.UnwrapKeyAsync(..., ct).ConfigureAwait(false)`  ← I/O happens lock-free
5. `await _cacheSemaphore.WaitAsync(ct).ConfigureAwait(false)`, then cache the result, release

#### 2B-2D. CertificateStore, CNG, CSP Providers

- No changes — default base class fallback is sufficient for local crypto operations

### Phase 3: Enclave Provider Async APIs

*Depends on: nothing. Parallel with Phase 2.*

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlColumnEncryptionEnclaveProvider.cs` — 4 internal abstract async methods
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/EnclaveProviderBase.cs` — `GetEnclaveSessionHelperAsync()`
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/EnclaveSessionCache.cs` — Audit `lock(enclaveCacheLock)`: if any async operation must execute before `CreateSession` stores a session, replace `lock` with `SemaphoreSlim` per FR-015; cache-only paths (no `await` inside) may retain `lock`
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/NoneAttestationEnclaveProvider.cs` — Sync wrappers (no real I/O)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/AzureAttestationBasedEnclaveProvider.cs` — Truly async (`ConfigurationManager.GetConfigurationAsync()`)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/VirtualSecureModeEnclaveProviderBase.cs` — Abstract `MakeRequestAsync`, `VerifyAttestationInfoAsync`, `GetSigningCertificateAsync`
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/VirtualSecureModeEnclaveProvider.cs` — Truly async (`HttpClient.GetStreamAsync` without `.Result`)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/EnclaveDelegate.Crypto.cs` — Async dispatcher methods

All `await` in all files above MUST use `.ConfigureAwait(false)` per FR-013.

### Phase 4: Async Utility Layer

#### Depends on: Phase 1. (Phase 2 required for AKV end-to-end testing but not for compilation.)

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlSecurityUtility.cs`:
  - `DecryptSymmetricKeyAsync` — returns `Task<(SqlClientSymmetricKey Key, SqlEncryptionKeyInfo KeyInfoChosen)>` per Decision 9 (replaces `out` parameters); accepts and propagates `CancellationToken`
  - `GetKeyFromLocalProvidersAsync` — internal async counterpart; accepts and propagates `CancellationToken`
  - `VerifyColumnMasterKeySignatureAsync` — accepts and propagates `CancellationToken`
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlSymmetricKeyCache.cs`:
  - `GetKeyAsync` — implements check-release-fetch-relock pattern per Decision 7 (FR-012); `_cacheLock` is **static**, making the lock-during-I/O problem especially severe for concurrency; accepts and propagates `CancellationToken` to `provider.DecryptColumnEncryptionKeyAsync`

All `await` in all files above MUST use `.ConfigureAwait(false)` per FR-013.

> **Note**: Phases 1–4 deliver async infrastructure but provide no end-to-end benefit until Phase 5 ships. The `SqlCommand` async path (`GetParameterEncryptionDataReaderAsync`) still uses `Task.Run` wrapping sync calls until Phase 5. SC-001 and SC-003 are only achieved after Phase 5.

### Phase 5: Async Call-Site Integration

*Depends on: Phase 4.*

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlCommand.Encryption.cs`:
  1. `ReadDescribeEncryptionParameterResultsKeysAsync` → `VerifyColumnMasterKeySignatureAsync()`
  2. `ReadDescribeEncryptionParameterResultsMetadataAsync` → `DecryptSymmetricKeyAsync()` (**highest impact**); consume the `(Key, KeyInfoChosen)` tuple result per Decision 9
  3. `ReadDescribeEncryptionParameterResultsAsync` → orchestrates above
  4. **Refactor `GetParameterEncryptionDataReaderAsync` into a true `async Task` method** (per Decision 10): remove the `Task.Run(() => { ... })` wrapper entirely; the method must `await` each I/O step directly. Do NOT extend the `AsyncHelper.ContinueTaskWithState` callback pattern used by the sync counterpart — the async path should use clean `async/await`.
  5. `TryFetchInputParameterEncryptionInfoAsync` → `EnclaveDelegate.GetEnclaveSessionAsync()`, `GetAttestationParametersAsync()`

All `await` in all files above MUST use `.ConfigureAwait(false)` per FR-013.

### Phase 6: Testing & Documentation

*Spans all phases.*

**Unit Tests** (`src/Microsoft.Data.SqlClient/tests/UnitTests/`):

- Default async fallback behavior
- CancellationToken propagation
- Faulted Task exception behavior

**Static Analysis (CI gate)**:

- Run a Roslyn analyzer or `grep`-based CI check to verify every `await` in `Microsoft.Data.SqlClient` and `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` assemblies uses `.ConfigureAwait(false)`. A single missed instance can cause production deadlocks in ASP.NET Framework / WPF / WinForms hosts. This MUST be enforced in CI, not left to manual review.

**Functional Tests** (`src/Microsoft.Data.SqlClient/tests/FunctionalTests/AlwaysEncryptedTests/`):

- Extend `DummyKeyStoreProvider` with async overrides
- Test async methods on `SqlColumnEncryptionKeyStoreProvider`

**Manual/Integration Tests** (`src/Microsoft.Data.SqlClient/tests/ManualTests/AlwaysEncrypted/`):

- AKV async decrypt/encrypt end-to-end
- Enclave session creation async (Azure Attestation, HGS)
- End-to-end async query with encrypted parameters

**Documentation**:

- `doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml`
- `doc/samples/` — Async AE usage sample code

## New Public API Surface

### SqlColumnEncryptionKeyStoreProvider (4 new virtual methods)

```csharp
public abstract class SqlColumnEncryptionKeyStoreProvider
{
    // Existing abstract methods (unchanged)
    public abstract byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);
    public abstract byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);

    // Existing virtual methods (unchanged)
    public virtual byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations);
    public virtual bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature);
    public virtual TimeSpan? ColumnEncryptionKeyCacheTtl { get; set; }

    // NEW async virtual methods
    public virtual Task<byte[]> DecryptColumnEncryptionKeyAsync(
        string masterKeyPath,
        string encryptionAlgorithm,
        byte[] encryptedColumnEncryptionKey,
        CancellationToken cancellationToken = default);

    public virtual Task<byte[]> EncryptColumnEncryptionKeyAsync(
        string masterKeyPath,
        string encryptionAlgorithm,
        byte[] columnEncryptionKey,
        CancellationToken cancellationToken = default);

    public virtual Task<byte[]> SignColumnMasterKeyMetadataAsync(
        string masterKeyPath,
        bool allowEnclaveComputations,
        CancellationToken cancellationToken = default);

    public virtual Task<bool> VerifyColumnMasterKeyMetadataAsync(
        string masterKeyPath,
        bool allowEnclaveComputations,
        byte[] signature,
        CancellationToken cancellationToken = default);
}
```

### SqlColumnEncryptionAzureKeyVaultProvider (4 new override methods)

```csharp
public class SqlColumnEncryptionAzureKeyVaultProvider : SqlColumnEncryptionKeyStoreProvider
{
    // NEW async overrides (truly async via Azure SDK)
    public override Task<byte[]> DecryptColumnEncryptionKeyAsync(
        string masterKeyPath, string encryptionAlgorithm,
        byte[] encryptedColumnEncryptionKey,
        CancellationToken cancellationToken = default);

    public override Task<byte[]> EncryptColumnEncryptionKeyAsync(
        string masterKeyPath, string encryptionAlgorithm,
        byte[] columnEncryptionKey,
        CancellationToken cancellationToken = default);

    public override Task<byte[]> SignColumnMasterKeyMetadataAsync(
        string masterKeyPath, bool allowEnclaveComputations,
        CancellationToken cancellationToken = default);

    public override Task<bool> VerifyColumnMasterKeyMetadataAsync(
        string masterKeyPath, bool allowEnclaveComputations,
        byte[] signature,
        CancellationToken cancellationToken = default);
}
```

## Success Criteria

**SC-001**: After implementation, `ExecuteReaderAsync` with AE-encrypted columns and AKV keys MUST NOT block any ThreadPool thread on HTTP I/O (verifiable via diagnostic tracing or async call stack inspection).

**SC-002**: All existing AE tests (unit, functional, manual) pass unchanged — zero regressions in sync code paths.

**SC-003**: `Task.Run` is no longer used in `GetParameterEncryptionDataReaderAsync` for AE operations.

**SC-004**: `CancellationToken` is properly propagated from `SqlCommand` async methods through to Azure SDK calls.

**SC-005**: Custom third-party `SqlColumnEncryptionKeyStoreProvider` implementations that only implement sync methods continue to work in both sync and async query paths.

## Assumptions

- Azure SDK (`Azure.Security.KeyVault.Keys.Cryptography`) async methods (`UnwrapKeyAsync`, `WrapKeyAsync`, etc.) are stable and production-ready.
- `SemaphoreSlim` supports mixed `.Wait()` / `.WaitAsync()` usage on the same instance (per .NET documentation).
- `SqlColumnEncryptionEnclaveProvider` is internal and can have its API changed freely.
- The existing `isAsync` flag pattern in `SqlCommand` is the correct mechanism for choosing sync vs async AE paths.
- `HostGuardianServiceEnclaveProvider.MakeRequest()` currently calls `HttpClient.GetStreamAsync().Result` internally (hidden sync-over-async), which the async version should simply `await`.
- PR #3673 has not shipped, so adding `CancellationToken` to the base class methods is a non-breaking change.
- Certificate/CNG/CSP providers perform local crypto that does not benefit from async; `Task.FromResult` wrapping is sufficient.

## Further Considerations

1. **`ConfigureAwait(false)` enforcement**: Consider adding a Roslyn analyzer rule to flag missing `ConfigureAwait(false)` in this assembly, similar to how `ConfigureAwaitAnalyzer` can be applied. This would prevent regressions as new async code is added.
2. **Third-party provider migration**: Document guidance for custom provider authors on how to override the new async methods for providers that do real I/O. Specifically: they should use `.ConfigureAwait(false)`, implement proper cancellation support, and not hold locks during I/O.
3. **Performance benchmarking**: After Phase 5, benchmark async AE queries against sync baseline to quantify ThreadPool thread savings and latency improvements under load.
4. **In-flight request deduplication (optional optimization)**: If high concurrency with cold-cache scenarios is observed in benchmarks, consider a `ConcurrentDictionary<string, Task<byte[]>>` for in-flight AKV requests (per FR-014) so concurrent misses for the same key share a single HTTP call rather than each making an independent one.
5. **`GetOrCreateSignatureVerificationResult` in AKV provider**: `_columnMasterKeyMetadataSignatureVerificationCache.GetOrCreate` for CMK signature verification uses a sync factory (CPU-bound RSA). This is acceptable for async paths since RSA verification is fast and non-blocking; no async variant is needed here.
6. **`ValueTask<T>` for internal cache-hit fast paths**: Decision 1 correctly uses `Task<T>` for public virtual APIs (safer for third-party implementers). However, internal methods like `SqlSymmetricKeyCache.GetKeyAsync` have a high cache-hit rate where the result is returned synchronously from `MemoryCache`. Using `ValueTask<T>` for these internal methods would avoid a `Task` allocation on the fast path. This is a P2 optimization that can be adopted later without API impact since these methods are internal.
