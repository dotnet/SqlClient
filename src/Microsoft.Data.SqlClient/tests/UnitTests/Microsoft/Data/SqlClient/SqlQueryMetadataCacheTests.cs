// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.QueryMetadataCacheTestCommands;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies that <see cref="SqlQueryMetadataCache"/> lookups split into an in-memory probe and
/// an awaitable key load, so asynchronous execution does not block on key store I/O.
/// </summary>
public class SqlQueryMetadataCacheTests
{
    /// <summary>
    /// The probe must not touch the key store; the async completion must load the key through the
    /// asynchronous provider API and report a cache hit.
    /// </summary>
    [Fact]
    public async Task CompleteCachedQueryMetadataAsync_ValidKey_UsesAsyncProviderAndReportsHit()
    {
        // Arrange
        TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 41) };
        using SqlCommand command = NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;
        SqlQueryMetadataCache cache = SqlQueryMetadataCache.GetInstance();
        Assert.True(cache.TryGetCachedQueryMetadata(
            command, out SqlQueryMetadataCache.CachedQueryMetadata metadata));
        int decryptCallsAfterProbe = provider.DecryptCallCount + provider.DecryptAsyncCallCount;

        // Act
        bool hit = await cache.CompleteCachedQueryMetadataAsync(
            command, metadata, CancellationToken.None);

        // Assert
        // The probe itself must defer all key store work to the completion step.
        Assert.Equal(0, decryptCallsAfterProbe);
        Assert.NotEmpty(metadata.KeysToLoad);

        // The completion loads the key asynchronously and publishes it to the parameter.
        Assert.True(hit);
        Assert.Equal(1, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
        Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
    }

    /// <summary>
    /// A stale key must degrade to a cache miss that clears parameter metadata and evicts the
    /// entry, so execution falls back to describing parameters again.
    /// </summary>
    [Fact]
    public async Task CompleteCachedQueryMetadataAsync_StaleKey_ReturnsFalseAndEvictsEntry()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback = _ => throw new ArgumentException("stale key"),
        };
        using SqlCommand command = NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;
        SqlQueryMetadataCache cache = SqlQueryMetadataCache.GetInstance();
        Assert.True(cache.TryGetCachedQueryMetadata(
            command, out SqlQueryMetadataCache.CachedQueryMetadata metadata));

        // Act
        bool hit = await cache.CompleteCachedQueryMetadataAsync(
            command, metadata, CancellationToken.None);

        // Assert
        Assert.False(hit);
        Assert.Null(command.Parameters[0].CipherMetadata);
        Assert.False(cache.TryGetCachedQueryMetadata(command, out _));
    }

    /// <summary>
    /// Cancellation does not prove the cached metadata is stale, so it must propagate without
    /// evicting the entry; a later uncancelled completion must still hit.
    /// </summary>
    [Fact]
    public async Task CompleteCachedQueryMetadataAsync_Canceled_ThrowsAndKeepsEntry()
    {
        // Arrange
        TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 43) };
        using SqlCommand command = NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;
        SqlQueryMetadataCache cache = SqlQueryMetadataCache.GetInstance();
        Assert.True(cache.TryGetCachedQueryMetadata(
            command, out SqlQueryMetadataCache.CachedQueryMetadata metadata));
        using CancellationTokenSource source = new();
        source.Cancel();

        // Act
        Exception? exception = await Record.ExceptionAsync(
            () => cache.CompleteCachedQueryMetadataAsync(command, metadata, source.Token));

        // Assert
        // Cancellation propagates to the caller.
        Assert.IsAssignableFrom<OperationCanceledException>(exception);

        // The entry survives, and an uncancelled retry completes as a hit.
        Assert.True(cache.TryGetCachedQueryMetadata(
            command, out SqlQueryMetadataCache.CachedQueryMetadata retryMetadata));
        Assert.True(await cache.CompleteCachedQueryMetadataAsync(
            command, retryMetadata, CancellationToken.None));
        Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
    }

    /// <summary>
    /// The synchronous lookup now shares the probe, so it must still load keys through the
    /// synchronous provider API and report a hit.
    /// </summary>
    [Fact]
    public void GetQueryMetadataIfExists_ValidKey_UsesSyncProviderAndReturnsTrue()
    {
        // Arrange
        TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 47) };
        using SqlCommand command = NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;

        // Act
        bool hit = SqlQueryMetadataCache.GetInstance().GetQueryMetadataIfExists(command);

        // Assert
        Assert.True(hit);
        Assert.Equal(1, provider.DecryptCallCount);
        Assert.Equal(0, provider.DecryptAsyncCallCount);
        Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
    }

    /// <summary>
    /// The synchronous lookup must still treat a stale key as a miss that clears parameter
    /// metadata and evicts the entry.
    /// </summary>
    [Fact]
    public void GetQueryMetadataIfExists_StaleKey_ReturnsFalseAndEvictsEntry()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            DecryptCallback = () => throw new ArgumentException("stale key"),
        };
        using SqlCommand command = NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;
        SqlQueryMetadataCache cache = SqlQueryMetadataCache.GetInstance();

        // Act
        bool hit = cache.GetQueryMetadataIfExists(command);

        // Assert
        Assert.False(hit);
        Assert.Null(command.Parameters[0].CipherMetadata);
        Assert.False(cache.TryGetCachedQueryMetadata(command, out _));
    }
}
