// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Verifies key loading when process-wide CEK caching is disabled.
    /// </summary>
    [Collection(SymmetricKeyCacheTestCollection.Name)]
    public class SymmetricKeyCacheTests
    {
        /// <summary>
        /// Uncached requests for the same key overlap provider I/O and do not publish their results.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_ZeroTtl_LoadsSameKeyConcurrentlyWithoutCaching()
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
                first = SymmetricKeyCache.Instance.GetKeyAsync(
                    keyInfo, connection, null, CancellationToken.None);
                second = SymmetricKeyCache.Instance.GetKeyAsync(
                    keyInfo, connection, null, CancellationToken.None);
                int callsBeforeCompletion = provider.CallCount;
                provider.Completion.TrySetResult(plaintextKey);
                SymmetricKey[] keys = await Task.WhenAll(first, second);
                SymmetricKey subsequent = await SymmetricKeyCache.Instance.GetKeyAsync(
                    keyInfo, connection, null, CancellationToken.None);

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
