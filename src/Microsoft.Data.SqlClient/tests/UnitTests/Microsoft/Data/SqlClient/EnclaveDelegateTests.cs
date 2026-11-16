// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Internal enclave signatures predate nullable annotations and accept null commands.
#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies the column encryption key resolution used to build an enclave package. The package
/// itself needs an attested enclave session, so these tests cover the step that does key store
/// I/O.
/// </summary>
public class EnclaveDelegateTests
{
    private const string ProviderName = "TEST_ENCLAVE_ASYNC_PROVIDER";
    private const int DatabaseId = 42;
    private const int CekId = 7;
    private const ulong CekMdVersion = 3;

    /// <summary>
    /// The async path must use the provider's async API so key store I/O does not block a pooled
    /// thread, and must return the decrypted key with its identifiers.
    /// </summary>
    [Fact]
    public async Task GetDecryptedKeysToBeSentToEnclaveAsync_OneKey_UsesAsyncProviderApi()
    {
        // Arrange
        byte[] expectedKey = QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 11);
        TestKeyStoreProvider provider = new() { PlaintextKey = expectedKey };
        using SqlConnection connection = NewConnection(provider);

        // Act
        List<ColumnEncryptionKeyInfo> keys =
            await EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                NewKeyTable(NewCipherInfoEntry()),
            connection,
            command: null,
            CancellationToken.None);

        // Assert
        ColumnEncryptionKeyInfo key = Assert.Single(keys);
        Assert.Equal(expectedKey, key.DecryptedKeyBytes);
        Assert.Equal(BitConverter.GetBytes((uint)DatabaseId), key.DatabaseIdBytes);
        Assert.Equal(BitConverter.GetBytes((ushort)CekId), key.KeyIdBytes);
        Assert.Equal(CekMdVersion, key.KeyMetadataVersion);
        Assert.Equal(1, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
    }

    /// <summary>Every requested key is resolved, each through the async provider API.</summary>
    [Fact]
    public async Task GetDecryptedKeysToBeSentToEnclaveAsync_ThreeKeys_ResolvesEveryKey()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            PlaintextKey = QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 23),
        };
        using SqlConnection connection = NewConnection(provider);
        ConcurrentDictionary<int, SqlTceCipherInfoEntry> table = NewKeyTable(
            NewCipherInfoEntry(ordinal: 0),
            NewCipherInfoEntry(ordinal: 1),
            NewCipherInfoEntry(ordinal: 2));

        // Act
        List<ColumnEncryptionKeyInfo> keys =
            await EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                table, connection, command: null, CancellationToken.None);

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Equal(3, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
    }

    /// <summary>
    /// Async key loads run one at a time, so a provider never sees overlapping calls for one
    /// package.
    /// </summary>
    [Fact]
    public async Task GetDecryptedKeysToBeSentToEnclaveAsync_ThreeKeys_LoadsSequentially()
    {
        // Arrange
        int active = 0;
        int maxActive = 0;
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback = async _ =>
            {
                int current = Interlocked.Increment(ref active);
                maxActive = Math.Max(maxActive, current);
                await Task.Yield();
                Interlocked.Decrement(ref active);
                return QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 29);
            },
        };
        using SqlConnection connection = NewConnection(provider);
        ConcurrentDictionary<int, SqlTceCipherInfoEntry> table = NewKeyTable(
            NewCipherInfoEntry(ordinal: 0),
            NewCipherInfoEntry(ordinal: 1),
            NewCipherInfoEntry(ordinal: 2));

        // Act
        List<ColumnEncryptionKeyInfo> keys =
            await EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                table, connection, command: null, CancellationToken.None);

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Equal(1, maxActive);
    }

    /// <summary>
    /// The sync path keeps using the sync provider API, so providers that only override the sync
    /// members keep working.
    /// </summary>
    [Fact]
    public void GetDecryptedKeysToBeSentToEnclave_OneKey_UsesSyncProviderApi()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            PlaintextKey = QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 37),
        };
        using SqlConnection connection = NewConnection(provider);

        // Act
        List<ColumnEncryptionKeyInfo> keys =
            EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclave(
            NewKeyTable(NewCipherInfoEntry()), connection, command: null);

        // Assert
        Assert.Single(keys);
        Assert.Equal(1, provider.DecryptCallCount);
        Assert.Equal(0, provider.DecryptAsyncCallCount);
    }

    /// <summary>
    /// A canceled token surfaces as cancellation, not as an Always Encrypted exception, and no
    /// provider call is made.
    /// </summary>
    [Fact]
    public async Task GetDecryptedKeysToBeSentToEnclaveAsync_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            PlaintextKey = QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 53),
        };
        using SqlConnection connection = NewConnection(provider);
        using CancellationTokenSource source = new();
        source.Cancel();

        // Act
        Task action() => EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
            NewKeyTable(NewCipherInfoEntry()), connection, command: null, source.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
        Assert.Equal(0, provider.DecryptAsyncCallCount);
    }

    /// <summary>
    /// A provider failure is wrapped as the sync path wraps it, instead of returning a partial key
    /// list.
    /// </summary>
    [Fact]
    public async Task GetDecryptedKeysToBeSentToEnclaveAsync_ProviderFails_ThrowsSqlException()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback =
                _ => throw new InvalidOperationException("key store unavailable"),
        };
        using SqlConnection connection = NewConnection(provider);

        // Act
        Task action() => EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
            NewKeyTable(NewCipherInfoEntry()),
            connection,
            command: null,
            CancellationToken.None);

        // Assert
        SqlException exception = await Assert.ThrowsAsync<SqlException>(action);
        InvalidOperationException inner = Assert.IsType<InvalidOperationException>(
            exception.InnerException);
        Assert.Equal("key store unavailable", inner.Message);
    }

    /// <summary>
    /// Registers a provider on an unopened connection without global registration.
    /// </summary>
    /// <param name="provider">Provider used to resolve this test's enclave keys.</param>
    /// <returns>An unopened connection that the caller must dispose.</returns>
    private static SqlConnection NewConnection(SqlColumnEncryptionKeyStoreProvider provider)
    {
        SqlConnection connection = new(
            "Data Source=async-ae-enclave-unit-test;Column Encryption Setting=Enabled");
        connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(
            new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
            {
                [ProviderName] = provider
            });
        return connection;
    }

    /// <summary>
    /// Creates a cipher entry with unique encrypted bytes, so the process-wide key cache cannot
    /// return another test's key.
    /// </summary>
    /// <param name="ordinal">Ordinal assigned to the entry.</param>
    /// <returns>A cipher entry with one encrypted key.</returns>
    private static SqlTceCipherInfoEntry NewCipherInfoEntry(int ordinal = 0)
    {
        SqlTceCipherInfoEntry entry = new(ordinal);
        entry.Add(
            encryptedKey: Guid.NewGuid().ToByteArray(),
            databaseId: DatabaseId,
            cekId: CekId,
            cekVersion: 1,
            cekMdVersion: CekMdVersion,
            keyPath: "test-enclave-key-path",
            keyStoreName: ProviderName,
            algorithmName: "RSA_OAEP");
        return entry;
    }

    /// <summary>Builds an enclave key table with sequential ordinals.</summary>
    /// <param name="entries">Cipher metadata for the keys the enclave needs.</param>
    /// <returns>A key table for sync or async key resolution.</returns>
    private static ConcurrentDictionary<int, SqlTceCipherInfoEntry> NewKeyTable(
        params SqlTceCipherInfoEntry[] entries)
    {
        ConcurrentDictionary<int, SqlTceCipherInfoEntry> table = new();
        for (int i = 0; i < entries.Length; i++)
        {
            table[i] = entries[i];
        }
        return table;
    }
}
