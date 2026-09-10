// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Verifies that the asynchronous command execution paths dispatch column encryption key
    /// operations through the <b>asynchronous</b> key store provider APIs, while the synchronous
    /// paths continue to use the synchronous APIs.
    /// </summary>
    [Trait("Set", "AE")]
    public sealed class AsyncKeyStoreProviderTests : IClassFixture<SQLSetupStrategyCertStoreProvider>
    {
        private readonly SQLSetupStrategy _fixture;

        public AsyncKeyStoreProviderTests(SQLSetupStrategyCertStoreProvider fixture)
        {
            _fixture = fixture;
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void SyncExecutionUsesSyncKeyStoreProviderApi(string connectionString)
        {
            RecordingKeyStoreProvider provider = new();

            using SqlConnection connection = new(connectionString);
            connection.Open();
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(Wrap(provider));

            using SqlCommand command = CreateEncryptedParameterCommand(connection);
            Assert.Throws<SqlException>(() => command.ExecuteReader());

            Assert.True(provider.SyncCalls > 0, "Expected the synchronous provider API to be used by ExecuteReader.");
            Assert.Equal(0, provider.AsyncCalls);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public async Task ExecuteReaderAsyncUsesAsyncKeyStoreProviderApi(string connectionString)
        {
            await AssertAsyncApiUsed(connectionString, command => command.ExecuteReaderAsync());
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public async Task ExecuteNonQueryAsyncUsesAsyncKeyStoreProviderApi(string connectionString)
        {
            await AssertAsyncApiUsed(connectionString, command => command.ExecuteNonQueryAsync());
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public async Task AsyncExecutionHonoursCancellationToken(string connectionString)
        {
            RecordingKeyStoreProvider provider = new();

            using SqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(Wrap(provider));

            using CancellationTokenSource cts = new();
            using SqlCommand command = CreateEncryptedParameterCommand(connection);

            // The provider cancels the token from inside the async decrypt call, which proves the
            // caller's token was threaded all the way down into the key store operation.
            provider.OnAsyncCall = () => cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => command.ExecuteReaderAsync(cts.Token));

            Assert.True(provider.AsyncCalls > 0, "Expected the asynchronous provider API to be used.");
        }

        private async Task AssertAsyncApiUsed(string connectionString, Func<SqlCommand, Task> execute)
        {
            RecordingKeyStoreProvider provider = new();

            using SqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(Wrap(provider));

            using SqlCommand command = CreateEncryptedParameterCommand(connection);
            await Assert.ThrowsAsync<SqlException>(() => execute(command));

            Assert.True(provider.AsyncCalls > 0, "Expected the asynchronous provider API to be used.");
            Assert.Equal(0, provider.SyncCalls);
        }

        private static Dictionary<string, SqlColumnEncryptionKeyStoreProvider> Wrap(
            RecordingKeyStoreProvider provider) =>
            new() { { DummyKeyStoreProvider.Name, provider } };

        private SqlCommand CreateEncryptedParameterCommand(SqlConnection connection)
        {
            SqlCommand command = new(
                $"SELECT * FROM [{_fixture.CustomKeyStoreProviderTestTable.Name}] WHERE CustomerID = @id",
                connection,
                transaction: null,
                SqlCommandColumnEncryptionSetting.Enabled);
            command.Parameters.AddWithValue("id", 9);
            return command;
        }

        /// <summary>
        /// Records which key store provider overload the driver invoked. Both overloads throw so
        /// that the test never depends on real key material; the assertions are made on the counters.
        /// </summary>
        private sealed class RecordingKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            private int _syncCalls;
            private int _asyncCalls;

            public int SyncCalls => Volatile.Read(ref _syncCalls);

            public int AsyncCalls => Volatile.Read(ref _asyncCalls);

            public Action OnAsyncCall { get; set; }

            public override byte[] DecryptColumnEncryptionKey(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] encryptedColumnEncryptionKey)
            {
                Interlocked.Increment(ref _syncCalls);
                throw new NotImplementedException();
            }

            public override Task<byte[]> DecryptColumnEncryptionKeyAsync(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] encryptedColumnEncryptionKey,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _asyncCalls);
                OnAsyncCall?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                throw new NotImplementedException();
            }

            public override byte[] EncryptColumnEncryptionKey(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] columnEncryptionKey) =>
                throw new NotImplementedException();
        }
    }
}
