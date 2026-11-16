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
    /// Verifies that asynchronous command execution decrypts column encryption keys found during
    /// parameter discovery through the <b>asynchronous</b> key store provider API, forwarding the
    /// caller's cancellation token, while synchronous execution keeps using the synchronous API.
    /// </summary>
    [Trait("Set", "AE")]
    public sealed class AsyncKeyStoreProviderTests
        : IClassFixture<SQLSetupStrategyCertStoreProvider>
    {
        private readonly SQLSetupStrategy _fixture;

        /// <summary>Captures the fixture that owns the encrypted test table.</summary>
        /// <param name="fixture">Always Encrypted setup shared by the tests in this class.</param>
        public AsyncKeyStoreProviderTests(SQLSetupStrategyCertStoreProvider fixture)
        {
            _fixture = fixture;
        }

        /// <summary>Configured Always Encrypted connection strings.</summary>
        public static TheoryData<string> AEConnectionStrings
        {
            get
            {
                TheoryData<string> data = new();
                foreach (string connectionString in DataTestUtility.AEConnStrings)
                {
                    data.Add(connectionString);
                }
                return data;
            }
        }

        /// <summary>
        /// Synchronous reader execution must keep using only the synchronous provider API, so sync
        /// callers see no behavior change from asynchronous key loading.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public void ExecuteReader_EncryptedParameter_UsesSyncProviderApi(string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection = new(connectionString);
            connection.Open();
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(Wrap(provider));
            using SqlCommand command = CreateEncryptedParameterCommand(connection);

            // Act
            Assert.Throws<SqlException>(() => command.ExecuteReader());

            // Assert
            Assert.True(provider.SyncCalls > 0, "Expected the synchronous provider API.");
            Assert.Equal(0, provider.AsyncCalls);
        }

        /// <summary>
        /// Asynchronous reader execution must decrypt keys through the asynchronous provider API.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteReaderAsync_EncryptedParameter_UsesAsyncProviderApi(
            string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateEncryptedParameterCommand(connection);

            // Act
            await Assert.ThrowsAsync<SqlException>(() => command.ExecuteReaderAsync());

            // Assert
            AssertOnlyAsyncApiUsed(provider);
        }

        /// <summary>
        /// Asynchronous non-query execution must decrypt keys through the asynchronous provider
        /// API.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteNonQueryAsync_EncryptedParameter_UsesAsyncProviderApi(
            string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateEncryptedParameterCommand(connection);

            // Act
            await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());

            // Assert
            AssertOnlyAsyncApiUsed(provider);
        }

        /// <summary>
        /// XML execution must use the asynchronous provider API just like reader and non-query
        /// execution.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteXmlReaderAsync_EncryptedParameter_UsesAsyncProviderApi(
            string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateEncryptedParameterCommand(connection, forXml: true);

            // Act
            await Assert.ThrowsAsync<SqlException>(() => command.ExecuteXmlReaderAsync());

            // Assert
            AssertOnlyAsyncApiUsed(provider);
        }

        /// <summary>
        /// Reader execution must forward the caller's token through parameter encryption to the
        /// provider, and the command must stay reusable after the cancellation.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteReaderAsync_ProviderCancels_ForwardsCallerToken(
            string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateEncryptedParameterCommand(connection);
            using CancellationTokenSource cts = CancelFromProvider(provider);

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => command.ExecuteReaderAsync(cts.Token));

            // Assert
            await AssertTokenForwardedAsync(provider, cts.Token, command);
        }

        /// <summary>
        /// Non-query execution must preserve provider cancellation after releasing describe-phase
        /// state, and the command must stay reusable after the cancellation.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteNonQueryAsync_ProviderCancels_ForwardsCallerToken(
            string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateEncryptedParameterCommand(connection);
            using CancellationTokenSource cts = CancelFromProvider(provider);

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => command.ExecuteNonQueryAsync(cts.Token));

            // Assert
            await AssertTokenForwardedAsync(provider, cts.Token, command);
        }

        /// <summary>
        /// XML execution has separate token capture and must forward the exact caller token to the
        /// provider, and the command must stay reusable after the cancellation.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteXmlReaderAsync_ProviderCancels_ForwardsCallerToken(
            string connectionString)
        {
            // Arrange
            RecordingKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateEncryptedParameterCommand(connection, forXml: true);
            using CancellationTokenSource cts = CancelFromProvider(provider);

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => command.ExecuteXmlReaderAsync(cts.Token));

            // Assert
            await AssertTokenForwardedAsync(provider, cts.Token, command);
        }

        /// <summary>Opens a connection with the recording provider registered on it.</summary>
        /// <param name="connectionString">Configured Always Encrypted test connection.</param>
        /// <param name="provider">Provider whose decrypt calls the test observes.</param>
        /// <returns>An open connection owned by the caller.</returns>
        private static async Task<SqlConnection> OpenWithProviderAsync(
            string connectionString, RecordingKeyStoreProvider provider)
        {
            SqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(Wrap(provider));
            return connection;
        }

        /// <summary>
        /// Makes the provider cancel the returned source from inside its async decrypt call, which
        /// proves the caller's token was threaded all the way down into the key store operation.
        /// </summary>
        /// <param name="provider">Provider that triggers the cancellation.</param>
        /// <returns>A token source owned by the caller.</returns>
        private static CancellationTokenSource CancelFromProvider(
            RecordingKeyStoreProvider provider)
        {
            CancellationTokenSource cts = new();
            provider.OnAsyncCall = () => cts.Cancel();
            return cts;
        }

        /// <summary>Checks that only the asynchronous provider API was used.</summary>
        /// <param name="provider">Provider whose counters are checked.</param>
        private static void AssertOnlyAsyncApiUsed(RecordingKeyStoreProvider provider)
        {
            Assert.True(provider.AsyncCalls > 0, "Expected the asynchronous provider API.");
            Assert.Equal(0, provider.SyncCalls);
        }

        /// <summary>
        /// Checks that the provider received the caller's token and that the command can run again,
        /// which guards against cancellation leaving released describe-phase state locked.
        /// </summary>
        /// <param name="provider">Provider whose counters and token are checked.</param>
        /// <param name="expectedToken">The token passed to the execution.</param>
        /// <param name="command">The command reused for a plain query after cancellation.</param>
        /// <returns>A task completing after the checks.</returns>
        private static async Task AssertTokenForwardedAsync(
            RecordingKeyStoreProvider provider, CancellationToken expectedToken, SqlCommand command)
        {
            AssertOnlyAsyncApiUsed(provider);
            Assert.Equal(expectedToken, provider.LastCancellationToken);

            command.Parameters.Clear();
            command.CommandText = "SELECT 1";
            Assert.Equal(1, await command.ExecuteScalarAsync());
        }

        /// <summary>Maps the fixture's dummy provider name to the recording instance.</summary>
        /// <param name="provider">Provider whose decrypt calls the test observes.</param>
        /// <returns>A provider map for registration on a single connection.</returns>
        private static Dictionary<string, SqlColumnEncryptionKeyStoreProvider> Wrap(
            RecordingKeyStoreProvider provider) =>
            new() { { DummyKeyStoreProvider.Name, provider } };

        /// <summary>
        /// Builds an encrypted predicate; XML queries project only a constant because SQL Server
        /// does not support FOR XML over encrypted result columns.
        /// </summary>
        /// <param name="connection">Open connection with the recording provider registered.</param>
        /// <param name="forXml">Whether to request XML instead of the encrypted columns.</param>
        /// <returns>A disposable command with a deterministic encrypted predicate.</returns>
        private SqlCommand CreateEncryptedParameterCommand(
            SqlConnection connection, bool forXml = false)
        {
            string projection = forXml ? "1 AS Value" : "*";
            string xmlClause = forXml ? " FOR XML PATH('row')" : string.Empty;
            SqlCommand command = new(
                $"SELECT {projection} FROM [{_fixture.CustomKeyStoreProviderTestTable.Name}] "
                    + $"WHERE CustomerID = @id{xmlClause}",
                connection,
                transaction: null,
                SqlCommandColumnEncryptionSetting.Enabled);
            command.Parameters.AddWithValue("id", 9);
            return command;
        }

        /// <summary>
        /// Records which key store provider overload the driver invoked. Both overloads throw so
        /// the tests never depend on real key material; assertions use the counters. The
        /// deterministic equality predicate requests neither enclave computations nor CMK
        /// signature verification.
        /// </summary>
        private sealed class RecordingKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            private int _syncCalls;
            private int _asyncCalls;

            public int SyncCalls => Volatile.Read(ref _syncCalls);

            public int AsyncCalls => Volatile.Read(ref _asyncCalls);

            public Action OnAsyncCall { get; set; }

            public CancellationToken LastCancellationToken { get; private set; }

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
                LastCancellationToken = cancellationToken;
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
