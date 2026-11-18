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

        /// <summary>
        /// Reader execution must stay pending while the provider's asynchronous key decryption is
        /// pending, then complete with the query result once the provider returns a valid key.
        /// This proves the decrypted key reaches the parameter metadata used for execution.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteReaderAsync_PendingProvider_CompletesAfterRelease(
            string connectionString)
        {
            // Arrange
            GatedKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateGatedCountCommand(connection);

            // Act
            // Start execution and wait until the provider is inside its pending decrypt call.
            Task<SqlDataReader> execution = command.ExecuteReaderAsync();
            await WaitForProviderEntryAsync(provider);
            bool pendingBeforeRelease = !execution.IsCompleted;

            // Let the provider return the key and finish the execution.
            provider.Release();
            using SqlDataReader reader = await execution;

            // Assert
            Assert.True(pendingBeforeRelease, "Execution completed before the key was loaded.");
            Assert.True(await reader.ReadAsync());
            Assert.Equal(0, reader.GetInt32(0));
        }

        /// <summary>
        /// Cancelling the caller's token while the provider's asynchronous key decryption is
        /// pending must fail execution with cancellation and leave the command reusable.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteReaderAsync_CanceledWhileProviderPending_ThrowsAndStaysReusable(
            string connectionString)
        {
            // Arrange
            GatedKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlCommand command = CreateGatedCountCommand(connection);
            using CancellationTokenSource cts = new();

            // Act
            // Start execution, then cancel once the provider is inside its pending decrypt call.
            Task<SqlDataReader> execution = command.ExecuteReaderAsync(cts.Token);
            await WaitForProviderEntryAsync(provider);
            cts.Cancel();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            await AssertCommandReusableAsync(command);
        }

        /// <summary>
        /// Changing the command's connection while the provider's asynchronous key decryption is
        /// pending must be rejected, and execution must still complete on the original connection.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [MemberData(nameof(AEConnectionStrings))]
        public async Task ExecuteReaderAsync_ConnectionChangedWhileProviderPending_Throws(
            string connectionString)
        {
            // Arrange
            GatedKeyStoreProvider provider = new();
            using SqlConnection connection =
                await OpenWithProviderAsync(connectionString, provider);
            using SqlConnection otherConnection = new(connectionString);
            using SqlCommand command = CreateGatedCountCommand(connection);

            // Act
            // Start execution and wait until the provider is inside its pending decrypt call.
            Task<SqlDataReader> execution = command.ExecuteReaderAsync();
            await WaitForProviderEntryAsync(provider);
            Exception reassignError = Record.Exception(() => command.Connection = otherConnection);

            // Let the provider return the key and finish the execution.
            provider.Release();
            using SqlDataReader reader = await execution;

            // Assert
            Assert.IsType<InvalidOperationException>(reassignError);
            Assert.Same(connection, command.Connection);
            Assert.True(await reader.ReadAsync());
            Assert.Equal(0, reader.GetInt32(0));
        }

        /// <summary>Opens a connection with the given provider registered on it.</summary>
        /// <param name="connectionString">Configured Always Encrypted test connection.</param>
        /// <param name="provider">Provider whose decrypt calls the test observes.</param>
        /// <returns>An open connection owned by the caller.</returns>
        private static async Task<SqlConnection> OpenWithProviderAsync(
            string connectionString, SqlColumnEncryptionKeyStoreProvider provider)
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
            await AssertCommandReusableAsync(command);
        }

        /// <summary>
        /// Checks that the command can run a plain query, which guards against cancellation
        /// leaving released describe-phase state locked.
        /// </summary>
        /// <param name="command">The command reused for a plain query.</param>
        /// <returns>A task completing after the check.</returns>
        private static async Task AssertCommandReusableAsync(SqlCommand command)
        {
            command.Parameters.Clear();
            command.CommandText = "SELECT 1";
            Assert.Equal(1, await command.ExecuteScalarAsync());
        }

        /// <summary>
        /// Waits until the gated provider is inside its asynchronous decrypt call, failing instead
        /// of hanging if execution never reaches the provider.
        /// </summary>
        /// <param name="provider">Provider expected to be entered.</param>
        /// <returns>A task completing once the provider has been entered.</returns>
        private static async Task WaitForProviderEntryAsync(GatedKeyStoreProvider provider)
        {
            Task completed = await Task.WhenAny(
                provider.Entered, Task.Delay(TimeSpan.FromSeconds(60)));
            Assert.True(
                completed == provider.Entered, "The asynchronous provider API was not called.");
        }

        /// <summary>Maps the fixture's dummy provider name to the given provider.</summary>
        /// <param name="provider">Provider whose decrypt calls the test observes.</param>
        /// <returns>A provider map for registration on a single connection.</returns>
        private static Dictionary<string, SqlColumnEncryptionKeyStoreProvider> Wrap(
            SqlColumnEncryptionKeyStoreProvider provider) =>
            new() { { DummyKeyStoreProvider.Name, provider } };

        /// <summary>
        /// Builds an unencrypted count over an encrypted predicate. The fixture never inserts rows
        /// into this table, so the count is 0. A unique comment keeps the query out of the query
        /// metadata cache, so every run takes the parameter discovery path.
        /// </summary>
        /// <param name="connection">Open connection with the gated provider registered.</param>
        /// <returns>A disposable command with a deterministic encrypted predicate.</returns>
        private SqlCommand CreateGatedCountCommand(SqlConnection connection)
        {
            SqlCommand command = new(
                $"SELECT COUNT(*) FROM [{_fixture.CustomKeyStoreProviderTestTable.Name}] "
                    + $"WHERE CustomerID = @id /* {Guid.NewGuid()} */",
                connection,
                transaction: null,
                SqlCommandColumnEncryptionSetting.Enabled);
            command.Parameters.AddWithValue("id", 9);
            return command;
        }

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

        /// <summary>
        /// Holds asynchronous key decryption pending until released, then returns the real key.
        /// The fixture encrypts the dummy provider's key with the certificate store provider, so
        /// decryption delegates to it.
        /// </summary>
        private sealed class GatedKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            private readonly SqlColumnEncryptionCertificateStoreProvider _certStoreProvider = new();
            private readonly TaskCompletionSource<bool> _entered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Entered => _entered.Task;

            public void Release() => _release.TrySetResult(true);

            public override byte[] DecryptColumnEncryptionKey(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] encryptedColumnEncryptionKey) =>
                _certStoreProvider.DecryptColumnEncryptionKey(
                    masterKeyPath, encryptionAlgorithm, encryptedColumnEncryptionKey);

            public override async Task<byte[]> DecryptColumnEncryptionKeyAsync(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] encryptedColumnEncryptionKey,
                CancellationToken cancellationToken = default)
            {
                _entered.TrySetResult(true);
                await Task.WhenAny(
                    _release.Task, Task.Delay(Timeout.Infinite, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
                return DecryptColumnEncryptionKey(
                    masterKeyPath, encryptionAlgorithm, encryptedColumnEncryptionKey);
            }

            public override byte[] EncryptColumnEncryptionKey(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] columnEncryptionKey) =>
                throw new NotImplementedException();
        }
    }
}
