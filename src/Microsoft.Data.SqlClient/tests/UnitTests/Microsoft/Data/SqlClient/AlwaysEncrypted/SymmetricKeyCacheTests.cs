// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Verifies asynchronous CEK loading, cancellation, and shared cache publication.
    /// </summary>
    [Collection(SymmetricKeyCacheTestCollection.Name)]
    public class SymmetricKeyCacheTests
    {
        private const string ProviderName = "TEST_ASYNC_PROVIDER";
        private const string KeyPath = "test-key-path";
        private const string EncryptionAlgorithm = "RSA_OAEP";

        /// <summary>Creates a deterministic 256-bit root key for encryption tests.</summary>
        /// <param name="seed">Starting value for the key bytes.</param>
        /// <returns>Key material of the length required by AEAD_AES_256_CBC_HMAC_SHA256.</returns>
        private static byte[] NewPlaintextKey(byte seed)
        {
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(seed + i);
            }
            return key;
        }

        /// <summary>
        /// Produces key material whose cache lookup key cannot collide with any other test. The CEK
        /// cache is a process-wide singleton shared by every test in this assembly.
        /// </summary>
        /// <param name="providerName">Name of the provider that can decrypt this key.</param>
        /// <param name="keyPath">Master key path supplied to that provider.</param>
        /// <returns>Key information with unique encrypted bytes for cache isolation.</returns>
        private static SqlEncryptionKeyInfo NewKeyInfo(string providerName = ProviderName, string keyPath = KeyPath)
            => new SqlEncryptionKeyInfo
            {
                encryptedKey = Guid.NewGuid().ToByteArray(),
                databaseId = 1,
                cekId = 1,
                cekVersion = 1,
                cekMdVersion = 1,
                keyPath = keyPath,
                keyStoreName = providerName,
                algorithmName = EncryptionAlgorithm,
            };

        /// <summary>Registers test providers on an unopened connection without changing global registrations.</summary>
        /// <param name="providers">Provider names and instances used by this connection.</param>
        /// <returns>An unopened connection that the caller must dispose.</returns>
        private static SqlConnection NewConnection(params (string Name, SqlColumnEncryptionKeyStoreProvider Provider)[] providers)
        {
            SqlConnection connection = new SqlConnection("Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> map = new();
            foreach ((string name, SqlColumnEncryptionKeyStoreProvider provider) in providers)
            {
                map[name] = provider;
            }
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(map);
            return connection;
        }

        #region SymmetricKeyCache.GetKey

        /// <summary>
        /// A key cached synchronously is reused asynchronously without another provider call.
        /// </summary>
        [Fact]
        public async Task GetKey_SynchronouslyCachedKey_ReusesEntry()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 11) };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            SqlEncryptionKeyInfo syncFirst = NewKeyInfo();
            SymmetricKey fromSync = cache.GetKey(
                syncFirst, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Act
            SymmetricKey fromAsync = await cache.GetKey(
                syncFirst, connection, command: null, isAsync: true, CancellationToken.None);

            // Assert
            Assert.Same(fromSync, fromAsync);
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// A key cached asynchronously is reused synchronously without another provider call.
        /// </summary>
        [Fact]
        public async Task GetKey_AsynchronouslyCachedKey_ReusesEntry()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 11) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo asyncFirst = NewKeyInfo();
            SymmetricKey asyncKey = await cache.GetKey(
                asyncFirst, connection, command: null, isAsync: true, CancellationToken.None);

            // Act
            SymmetricKey syncKey = cache.GetKey(
                asyncFirst, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Assert
            Assert.Same(asyncKey, syncKey);
            Assert.Equal(0, provider.DecryptCallCount);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// A synchronously populated cache must still honor cancellation on async lookup.
        /// </summary>
        [Fact]
        public async Task GetKey_SynchronouslyCachedKey_ObservesCancellation()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 17) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            SymmetricKey cachedKey = cache.GetKey(
                keyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult();
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Task<SymmetricKey> cancelled =
                cache.GetKey(keyInfo, connection, command: null, isAsync: true, cts.Token).AsTask();
            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.True(cancelled.IsCanceled);
            Assert.Same(cachedKey, cache.GetKey(
                keyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult());
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// An asynchronously populated cache must still honor cancellation on later lookup.
        /// </summary>
        [Fact]
        public async Task GetKey_AsynchronouslyCachedKey_ObservesCancellation()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 17) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            SymmetricKey cachedKey = await cache.GetKey(
                keyInfo, connection, command: null, isAsync: true, CancellationToken.None);
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Task<SymmetricKey> cancelled =
                cache.GetKey(keyInfo, connection, command: null, isAsync: true, cts.Token).AsTask();
            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.True(cancelled.IsCanceled);
            Assert.Same(cachedKey, cache.GetKey(
                keyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult());
            Assert.Equal(0, provider.DecryptCallCount);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Concurrent cold-cache callers share one provider fetch and the published key instance.
        /// </summary>
        [Fact]
        public async Task GetKey_WithConcurrentCacheMisses_FetchesOnceAndPublishesOneKey()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = async _ =>
                {
                    await release.Task;
                    return NewPlaintextKey(seed: 21);
                }
            };

            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            // Act
            Task<SymmetricKey> first =
                cache.GetKey(
                    keyInfo, connection, command: null,
                    isAsync: true, CancellationToken.None).AsTask();
            Task<SymmetricKey>[] followers = Enumerable.Range(0, 8)
                .Select(_ =>
                    cache.GetKey(
                        keyInfo, connection, command: null,
                        isAsync: true, CancellationToken.None).AsTask())
                .ToArray();
            int callsBeforeRelease = provider.DecryptAsyncCallCount;
            release.TrySetResult(true);

            SymmetricKey firstKey = await first;
            SymmetricKey[] followerKeys = await Task.WhenAll(followers);

            // Assert
            Assert.Equal(1, callsBeforeRelease);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.All(followerKeys, key => Assert.Same(firstKey, key));

            // Subsequent callers, sync or async, see the published instance without another fetch.
            Assert.Same(firstKey, cache.GetKey(
                keyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult());
            Assert.Same(firstKey, await cache.GetKey(
                keyInfo, connection, command: null, isAsync: true, CancellationToken.None));
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// Cancelling a waiting caller must not cancel the active fetch or prevent another key from loading.
        /// </summary>
        [Fact]
        public async Task GetKey_CancelledWaiter_DoesNotBlockOtherCallersOrKeys()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new() { DecryptAsyncCallback = _ => result.Task };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            // Act
            Task<SymmetricKey> first =
                cache.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
            using CancellationTokenSource cts = new();
            Task<SymmetricKey> cancelled =
                cache.GetKey(keyInfo, connection, null, isAsync: true, cts.Token).AsTask();
            Task<SymmetricKey> follower =
                cache.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
            cts.Cancel();

            try
            {
                // Assert
                Assert.Same(cancelled, await Task.WhenAny(cancelled, Task.Delay(TimeSpan.FromSeconds(5))));
                OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
                Assert.Equal(cts.Token, error.CancellationToken);
                Assert.False(first.IsCompleted);
                provider.DecryptAsyncCallback = null;
                Task<SymmetricKey> otherKey =
                    cache.GetKey(
                        NewKeyInfo(), connection, null,
                        isAsync: true, CancellationToken.None).AsTask();
                Assert.Same(otherKey, await Task.WhenAny(otherKey, Task.Delay(TimeSpan.FromSeconds(5))));
                await otherKey;
                Assert.Equal(2, provider.DecryptAsyncCallCount);
            }
            finally
            {
                // Cleanup
                result.TrySetResult(NewPlaintextKey(seed: 22));
                await first;
                await follower;
                // Drain the task even when an assertion fails before the cancellation check.
                try { await cancelled; }
                catch (OperationCanceledException) { }
            }

            Assert.Same(await first, await follower);
            Assert.Equal(2, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// A cancelled owner releases the per-key gate so a queued caller can retry independently.
        /// </summary>
        [Fact]
        public async Task GetKey_CancelledOwner_AllowsWaitingCallerToRetry()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new() { DecryptAsyncCallback = _ => result.Task };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new();
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            // Act
            Task<SymmetricKey> first =
                cache.GetKey(keyInfo, connection, null, isAsync: true, cts.Token).AsTask();
            Task<SymmetricKey> follower =
                cache.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
            int callsBeforeRelease = provider.DecryptAsyncCallCount;
            provider.DecryptAsyncCallback = null;
            cts.Cancel();
            result.TrySetCanceled();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.True(first.IsCanceled);

            Assert.Same(follower, await Task.WhenAny(follower, Task.Delay(TimeSpan.FromSeconds(5))));
            SymmetricKey key = await follower;
            Assert.Equal(1, callsBeforeRelease);
            Assert.Equal(2, provider.DecryptAsyncCallCount);
            Assert.Same(key, await cache.GetKey(
                keyInfo, connection, null, isAsync: true, CancellationToken.None));
        }

        /// <summary>
        /// A failed owner releases the per-key gate so a queued caller can retry independently.
        /// </summary>
        [Fact]
        public async Task GetKey_FailedOwner_AllowsWaitingCallerToRetry()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new() { DecryptAsyncCallback = _ => result.Task };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new();
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            // Act
            Task<SymmetricKey> first =
                cache.GetKey(keyInfo, connection, null, isAsync: true, cts.Token).AsTask();
            Task<SymmetricKey> follower =
                cache.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
            int callsBeforeRelease = provider.DecryptAsyncCallCount;
            provider.DecryptAsyncCallback = null;
            result.TrySetException(new InvalidOperationException("Provider failure"));

            // Assert
            await Assert.ThrowsAsync<SqlException>(() => first);

            Assert.Same(follower, await Task.WhenAny(follower, Task.Delay(TimeSpan.FromSeconds(5))));
            SymmetricKey key = await follower;
            Assert.Equal(1, callsBeforeRelease);
            Assert.Equal(2, provider.DecryptAsyncCallCount);
            Assert.Same(key, await cache.GetKey(
                keyInfo, connection, null, isAsync: true, CancellationToken.None));
        }

        /// <summary>
        /// An async caller waits for a same-key synchronous load and can cancel while waiting.
        /// </summary>
        [Fact]
        public async Task GetKey_SynchronousOwnerSameKey_WaitsForKeyGateAndHonorsCancellation()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            int calls = 0;
            SynchronousKeyStoreProvider provider = new()
            {
                DecryptCallback = () =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                    return NewPlaintextKey(seed: 45);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo ownerInfo = NewKeyInfo();
            // Act
            Task<SymmetricKey> owner = Task.Factory.StartNew(
                () => Task.FromResult(cache.GetKey(
                    ownerInfo, connection, null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult()),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            using CancellationTokenSource cts = new();
            Task<SymmetricKey>? waiter = null;
            SqlEncryptionKeyInfo waiterInfo = ownerInfo;

            try
            {
                // Assert
                Assert.Same(entered.Task, await Task.WhenAny(
                    entered.Task, Task.Delay(TimeSpan.FromSeconds(5))));
                waiter = cache.GetKey(
                    waiterInfo, connection, null, isAsync: true, cts.Token).AsTask();
                Assert.False(waiter.IsCompleted);
                Assert.Equal(1, Volatile.Read(ref calls));
                cts.Cancel();
                Assert.Same(waiter, await Task.WhenAny(
                    waiter, Task.Delay(TimeSpan.FromSeconds(5))));
                OperationCanceledException error =
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
                Assert.Equal(cts.Token, error.CancellationToken);
                Assert.True(waiter.IsCanceled);
                Assert.Equal(1, Volatile.Read(ref calls));
            }
            finally
            {
                // Cleanup
                release.Set();
                await owner;
                if (waiter is not null)
                {
                    try { await waiter; }
                    catch (OperationCanceledException) { }
                }
            }

            Assert.Same(
                await owner,
                await cache.GetKey(
                    waiterInfo, connection, null, isAsync: true, CancellationToken.None));
            Assert.Equal(1, Volatile.Read(ref calls));
        }

        /// <summary>
        /// A key published while an async caller waits for the provider gate avoids another decrypt.
        /// </summary>
        [Fact]
        public async Task GetKey_SynchronousOwnerPublishesWhileWaiting_ReusesPublishedKey()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            int calls = 0;
            byte[] expectedKey = NewPlaintextKey(seed: 46);
            SynchronousKeyStoreProvider provider = new()
            {
                DecryptCallback = () =>
                {
                    if (Interlocked.Increment(ref calls) != 1)
                    {
                        throw new InvalidOperationException("The key was already decrypted.");
                    }
                    entered.TrySetResult(true);
                    release.Wait();
                    return expectedKey;
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            Task<SymmetricKey>? waiter = null;

            // Act
            Task<SymmetricKey> owner = Task.Factory.StartNew(
                () => cache.GetKey(
                    keyInfo, connection, null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                Assert.Same(entered.Task, await Task.WhenAny(
                    entered.Task, Task.Delay(TimeSpan.FromSeconds(5))));
                waiter =
                    cache.GetKey(
                        keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
                bool waitedForOwner = !waiter.IsCompleted;
                release.Set();
                SymmetricKey ownerKey = await owner;
                SymmetricKey waiterKey = await waiter;

                // Assert
                Assert.True(waitedForOwner);
                Assert.Same(ownerKey, waiterKey);
                Assert.Equal(expectedKey, waiterKey.RootKey);
                Assert.Equal(1, Volatile.Read(ref calls));
            }
            finally
            {
                // Cleanup
                release.Set();
                await owner;
                if (waiter is not null)
                {
                    await waiter;
                }
            }
        }

        /// <summary>
        /// An async caller waits for a same-key base async fallback load and can cancel while waiting.
        /// </summary>
        [Fact]
        public async Task GetKey_BaseAsyncFallbackOwnerSameKey_WaitsForKeyGateAndHonorsCancellation()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            int calls = 0;
            SynchronousKeyStoreProvider provider = new()
            {
                DecryptCallback = () =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                    }
                    return NewPlaintextKey(seed: 45);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo ownerInfo = NewKeyInfo();
            // Act
            Task<SymmetricKey> owner = Task.Factory.StartNew(
                () =>
                    cache.GetKey(
                        ownerInfo, connection, null, isAsync: true, CancellationToken.None)
                        .AsTask(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            using CancellationTokenSource cts = new();
            Task<SymmetricKey>? waiter = null;
            SqlEncryptionKeyInfo waiterInfo = ownerInfo;

            try
            {
                // Assert
                Assert.Same(entered.Task, await Task.WhenAny(
                    entered.Task, Task.Delay(TimeSpan.FromSeconds(5))));
                waiter = cache.GetKey(
                    waiterInfo, connection, null, isAsync: true, cts.Token).AsTask();
                Assert.False(waiter.IsCompleted);
                Assert.Equal(1, Volatile.Read(ref calls));
                cts.Cancel();
                Assert.Same(waiter, await Task.WhenAny(
                    waiter, Task.Delay(TimeSpan.FromSeconds(5))));
                OperationCanceledException error =
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
                Assert.Equal(cts.Token, error.CancellationToken);
                Assert.True(waiter.IsCanceled);
                Assert.Equal(1, Volatile.Read(ref calls));
            }
            finally
            {
                // Cleanup
                release.Set();
                await owner;
                if (waiter is not null)
                {
                    try { await waiter; }
                    catch (OperationCanceledException) { }
                }
            }

            Assert.Same(
                await owner,
                await cache.GetKey(
                    waiterInfo, connection, null, isAsync: true, CancellationToken.None));
            Assert.Equal(1, Volatile.Read(ref calls));
        }

        /// <summary>
        /// Completed async decryption must not wait for unrelated synchronous provider I/O to publish its key.
        /// </summary>
        [Fact]
        public async Task GetKey_UnrelatedSynchronousProviderBlocked_PublishesWithoutWaiting()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> asyncResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> syncEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim releaseSync = new();
            byte[] expectedKey = NewPlaintextKey(seed: 43);
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ => asyncResult.Task,
                DecryptCallback = () =>
                {
                    syncEntered.TrySetResult(true);
                    releaseSync.Wait();
                    return NewPlaintextKey(seed: 44);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo asyncKeyInfo = NewKeyInfo();
            SqlEncryptionKeyInfo syncKeyInfo = NewKeyInfo();
            // Act
            Task<SymmetricKey> asyncKey =
                cache.GetKey(
                    asyncKeyInfo, connection, command: null,
                    isAsync: true, CancellationToken.None).AsTask();
            // Keep blocking provider I/O off the pool needed by the async continuations.
            Task<SymmetricKey> syncKey = Task.Factory.StartNew(
                () => cache.GetKey(
                    syncKeyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            try
            {
                // Assert
                Assert.Same(syncEntered.Task, await Task.WhenAny(syncEntered.Task, Task.Delay(TimeSpan.FromSeconds(5))));
                asyncResult.TrySetResult(expectedKey);
                Assert.Same(asyncKey, await Task.WhenAny(asyncKey, Task.Delay(TimeSpan.FromSeconds(5))));
                Assert.Equal(expectedKey, (await asyncKey).RootKey);
                Assert.False(syncKey.IsCompleted);
                Assert.Same(await asyncKey, cache.GetKey(
                    asyncKeyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult());
            }
            finally
            {
                // Cleanup
                asyncResult.TrySetResult(expectedKey);
                releaseSync.Set();
                await syncKey;
                await asyncKey;
            }
        }

        /// <summary>
        /// A blocked synchronous load does not block a synchronous load for a different key.
        /// </summary>
        [Fact]
        public async Task GetKey_DifferentKeySynchronousLoadBlocked_LoadsWithoutWaiting()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<bool> entered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            byte[] blockedKey = NewPlaintextKey(seed: 47);
            byte[] otherKey = NewPlaintextKey(seed: 48);
            int calls = 0;
            SynchronousKeyStoreProvider provider = new()
            {
                DecryptCallback = () =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        entered.TrySetResult(true);
                        release.Wait();
                        return blockedKey;
                    }
                    return otherKey;
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo blockedInfo = NewKeyInfo();
            SqlEncryptionKeyInfo otherInfo = NewKeyInfo();
            Task<SymmetricKey> blocked = Task.Factory.StartNew(
                () => cache.GetKey(
                    blockedInfo, connection, command: null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task<SymmetricKey>? other = null;

            try
            {
                Assert.Same(entered.Task, await Task.WhenAny(
                    entered.Task, Task.Delay(TimeSpan.FromSeconds(5))));

                // Act
                // Use a dedicated worker so a cross-key wait fails the test instead of hanging.
                other = Task.Factory.StartNew(
                    () => cache.GetKey(
                        otherInfo, connection, command: null,
                        isAsync: false, CancellationToken.None).GetAwaiter().GetResult(),
                    CancellationToken.None, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                Task completed = await Task.WhenAny(
                    other, Task.Delay(TimeSpan.FromSeconds(5)));

                // Assert
                Assert.Same(other, completed);
                SymmetricKey key = await other;
                Assert.Equal(otherKey, key.RootKey);
                Assert.False(blocked.IsCompleted);
                Assert.Same(key, cache.GetKey(
                    otherInfo, connection, command: null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult());
            }
            finally
            {
                // Cleanup
                release.Set();
                await blocked;
                if (other is not null)
                {
                    await other;
                }
            }
        }

        /// <summary>
        /// A synchronous caller waits for a same-key async load and reuses its key without
        /// decrypting again.
        /// </summary>
        [Fact]
        public async Task GetKey_SameKeyAsyncLoadPending_WaitsAndReusesAsyncKey()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> asyncResult =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] expectedKey = NewPlaintextKey(seed: 43);
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ => asyncResult.Task,
                DecryptCallback = () => NewPlaintextKey(seed: 44)
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            // Act
            // The async owner holds the key gate while its provider call is pending.
            Task<SymmetricKey> asyncKey =
                cache.GetKey(
                    keyInfo, connection, command: null,
                    isAsync: true, CancellationToken.None).AsTask();
            // Keep the blocked synchronous caller off the pool needed by async continuations.
            Task<SymmetricKey> syncKey = Task.Factory.StartNew(
                () => cache.GetKey(
                    keyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                    .GetAwaiter().GetResult(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            try
            {
                // Assert
                Assert.NotSame(syncKey, await Task.WhenAny(
                    syncKey, Task.Delay(TimeSpan.FromMilliseconds(200))));
                asyncResult.TrySetResult(expectedKey);
                Assert.Same(syncKey, await Task.WhenAny(
                    syncKey, Task.Delay(TimeSpan.FromSeconds(5))));
                Assert.Same(await asyncKey, await syncKey);
                Assert.Equal(expectedKey, (await syncKey).RootKey);
                Assert.Equal(1, provider.DecryptAsyncCallCount);
                Assert.Equal(0, provider.DecryptCallCount);
            }
            finally
            {
                // Cleanup
                asyncResult.TrySetResult(expectedKey);
                await asyncKey;
                await syncKey;
            }
        }

        /// <summary>
        /// A decryption that has already succeeded must not be thrown away just because the caller
        /// cancelled while the result was being published. The remote work is done at that point, so
        /// discarding it would only force the next caller to repeat it.
        /// </summary>
        [Fact]
        public async Task GetKey_WhenCancelledAfterDecryptSucceeds_StillPublishesTheKey()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            using CancellationTokenSource cts = new CancellationTokenSource();
            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = _ =>
                {
                    // Cancel between a successful provider call and the cache insertion.
                    cts.Cancel();
                    return Task.FromResult(NewPlaintextKey(seed: 57));
                }
            };

            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            // Act
            SymmetricKey key = await cache.GetKey(
                keyInfo, connection, command: null, isAsync: true, cts.Token);

            // Assert
            Assert.NotNull(key);
            Assert.Equal(1, provider.DecryptAsyncCallCount);

            // The key was published despite the cancellation, so no second decryption is needed.
            Assert.Same(key, cache.GetKey(
                keyInfo, connection, command: null, isAsync: false, CancellationToken.None)
                .GetAwaiter().GetResult());
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// Pre-cancelled requests do not invoke the provider or populate the cache.
        /// </summary>
        [Fact]
        public async Task GetKey_PreCancelledToken_DoesNotInvokeProvider()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 31) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            using CancellationTokenSource preCancelled = new CancellationTokenSource();
            preCancelled.Cancel();

            // Act
            Task preCancelledTask =
                cache.GetKey(
                    keyInfo, connection, command: null, isAsync: true, preCancelled.Token).AsTask();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preCancelledTask);
            Assert.True(preCancelledTask.IsCanceled);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
            SymmetricKey key = await cache.GetKey(
                keyInfo, connection, null, isAsync: true, CancellationToken.None);
            Assert.Equal(provider.PlaintextKey, key.RootKey);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// In-flight cancellation remains unwrapped and does not populate the cache.
        /// </summary>
        [Fact]
        public async Task GetKey_InFlightCancellation_ProducesCancelledTaskAndDoesNotCache()
        {
            // Arrange
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 31) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> providerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.DecryptAsyncCallback = async token =>
            {
                providerEntered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return NewPlaintextKey(seed: 32);
            };

            // Act
            Task inFlight =
                cache.GetKey(keyInfo, connection, command: null, isAsync: true, cts.Token).AsTask();
            await providerEntered.Task;
            cts.Cancel();

            // Assert
            // Cancellation is not wrapped in SQL.KeyDecryptionFailed.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
            Assert.True(inFlight.IsCanceled);

            // Nothing was cached, so a subsequent successful call still reaches the provider.
            provider.DecryptAsyncCallback = null;
            SymmetricKey key = await cache.GetKey(
                keyInfo, connection, command: null, isAsync: true, CancellationToken.None);
            Assert.Equal(provider.PlaintextKey, key.RootKey);
        }

        #endregion

        /// <summary>
        /// Uncached requests for the same key overlap provider I/O and do not publish their results.
        /// </summary>
        [Fact]
        public async Task GetKey_ZeroTtl_LoadsSameKeyConcurrentlyWithoutCaching()
        {
            // Arrange
            TimeSpan originalTtl = SqlConnection.ColumnEncryptionKeyCacheTtl;
            PendingDecryptionKeyStoreProvider provider = new();
            using SqlConnection connection = new("Data Source=async-ae-unit-test");
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
                {
                    ["PENDING_DECRYPTION_PROVIDER"] = provider
                });
            SqlEncryptionKeyInfo keyInfo = new()
            {
                encryptedKey = Guid.NewGuid().ToByteArray(),
                keyPath = "test-key-path",
                keyStoreName = "PENDING_DECRYPTION_PROVIDER",
                algorithmName = "RSA_OAEP"
            };
            byte[] plaintextKey = new byte[32];
            Task<SymmetricKey>? first = null;
            Task<SymmetricKey>? second = null;

            try
            {
                SqlConnection.ColumnEncryptionKeyCacheTtl = TimeSpan.Zero;

                // Act
                first = SymmetricKeyCache.Instance.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
                second = SymmetricKeyCache.Instance.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None).AsTask();
                int callsBeforeCompletion = provider.CallCount;
                provider.Completion.TrySetResult(plaintextKey);
                SymmetricKey[] keys = await Task.WhenAll(first, second);
                SymmetricKey subsequent = await SymmetricKeyCache.Instance.GetKey(
                    keyInfo, connection, null, isAsync: true, CancellationToken.None);

                // Assert
                Assert.Equal(2, callsBeforeCompletion);
                Assert.Equal(3, provider.CallCount);
                Assert.NotSame(keys[0], keys[1]);
                Assert.NotSame(keys[0], subsequent);
                Assert.All(keys, key => Assert.Equal(plaintextKey, key.RootKey));
                Assert.Equal(plaintextKey, subsequent.RootKey);
            }
            finally
            {
                // Cleanup
                provider.Completion.TrySetResult(plaintextKey);
                try
                {
                    if (first is not null)
                    {
                        await first;
                    }
                    if (second is not null)
                    {
                        await second;
                    }
                }
                finally
                {
                    SqlConnection.ColumnEncryptionKeyCacheTtl = originalTtl;
                }
            }
        }
    }
}
