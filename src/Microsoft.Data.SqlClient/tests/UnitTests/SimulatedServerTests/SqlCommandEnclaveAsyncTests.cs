// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Exercises command dispatch while enclave provider operations are genuinely incomplete.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public sealed class SqlCommandEnclaveAsyncTests
    {
        /// <summary>Write and pre-write reconnect failures release only a reservation they actually acquired.</summary>
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public async Task DescribeSetup_Failure_ReleasesReservation(bool cancel, bool closeConnection, bool writeStarted)
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            SqlConnectionInternal inner = (SqlConnectionInternal)fixture.Connection.InnerConnection;
            if (writeStarted)
            {
                inner.IncrementAsyncCount();
                SetField(command, "_describeParameterEncryptionConnection", inner);
            }
            var write = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task setup = writeStarted
                ? Invoke(command, "RunExecuteReaderTdsSetupContinuation",
                    RunBehavior.ReturnImmediately, null, null, write.Task, true)
                : write.Task;
            Task completion = Invoke(command, "CompleteParameterEncryptionAsync",
                setup, null, null, true, false, CancellationToken.None);
            Assert.Equal(writeStarted ? 1 : 0, GetAsyncCount(inner));
            if (closeConnection)
            {
                fixture.Connection.Close();
            }
            if (cancel)
            {
                write.SetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
                Assert.True(completion.IsCanceled);
            }
            else
            {
                write.SetException(fixture.Provider.Error);
                Assert.Same(fixture.Provider.Error,
                    await Assert.ThrowsAsync<InvalidOperationException>(() => completion));
            }
            Assert.Equal(0, GetAsyncCount(inner));
        }

        /// <summary>A delayed reader registration must not expose the connection to another command.</summary>
        [Fact]
        public async Task DescribeCompletion_DelayedRegistration_KeepsConnectionReserved()
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = new SqlCommand("/* empty describe response */", fixture.Connection);
            SqlConnectionInternal inner = (SqlConnectionInternal)fixture.Connection.InnerConnection;
            object[] arguments =
            {
                CommandBehavior.Default, RunBehavior.ReturnImmediately, true, true, 30, null,
                true, false, null, false
            };
            SqlDataReader reader = (SqlDataReader)typeof(SqlCommand)
                .GetMethod("RunExecuteReaderTds", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(command, arguments);
            if (arguments[5] is Task write)
            {
                await write;
            }
            SetField(command, "_describeParameterEncryptionConnection", inner);
            SetField(command, "_rpcArrayOf1", new[] { new _SqlRPC() });
            Assert.NotNull(GetField(command, "_stateObj"));
            object gate = typeof(DbReferenceCollection)
                .GetField("_itemLock", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inner.ReferenceCollection);
            Task completion;
            Exception reservationFailure;
            lock (gate)
            {
                completion = Invoke(command, "CompleteParameterEncryptionAsync",
                    null, reader, null, true, false, CancellationToken.None);
                reservationFailure = Record.Exception(() =>
                {
                    Assert.True(SpinWait.SpinUntil(
                        () => GetField(command, "_stateObj") is null || completion.IsCompleted,
                        TimeSpan.FromSeconds(10)), "The completion worker did not reach reader registration.");
                    Assert.False(completion.IsCompleted);
                    Assert.Equal(1, GetAsyncCount(inner));
                    using SqlCommand other = new SqlCommand("SELECT 1", fixture.Connection);
                    Assert.Throws<InvalidOperationException>(() => other.ExecuteNonQuery());
                });
            }
            await completion;
            Assert.Null(reservationFailure);
            Assert.Equal(0, GetAsyncCount(inner));
            using SqlCommand reusable = new SqlCommand("/* reuse empty batch */", fixture.Connection);
            Assert.Equal(-1, await reusable.ExecuteNonQueryAsync());
        }

        /// <summary>Reads the connection reservation count without affecting production ownership.</summary>
        private static int GetAsyncCount(SqlConnectionInternal inner) =>
            (int)typeof(SqlConnectionInternal).GetField("_asyncCommandCount",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inner);

        /// <summary>
        /// Every task-based execution API forwards cancellation and provider failures without blocking.
        /// </summary>
        [Theory]
        [InlineData(0, false, false)]
        [InlineData(1, false, false)]
        [InlineData(2, false, false)]
        [InlineData(3, false, false)]
        [InlineData(0, true, false)]
        [InlineData(1, true, false)]
        [InlineData(2, true, false)]
        [InlineData(3, true, false)]
        [InlineData(0, false, true)]
        [InlineData(1, false, true)]
        [InlineData(2, false, true)]
        [InlineData(3, false, true)]
        [InlineData(0, true, true)]
        [InlineData(1, true, true)]
        [InlineData(2, true, true)]
        [InlineData(3, true, true)]
        public async Task ExecuteAsync_PendingProvider_PropagatesCompletion(int api, bool attestation, bool cancel)
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            fixture.Provider.Attestation = attestation;

            Task operation = ExecuteAsync(command, api, cancellation.Token);

            Assert.False(operation.IsCompleted);
            Assert.Equal(cancellation.Token, fixture.Provider.Token);
            Assert.Throws<InvalidOperationException>(() => command.Connection = null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(0, fixture.Provider.SyncCalls);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
                Assert.True(operation.IsCanceled);
            }
            else
            {
                fixture.Provider.Fail();
                Assert.Same(fixture.Provider.Error, await Assert.ThrowsAsync<InvalidOperationException>(() => operation));
            }

            // The same command must be reusable after provider failure or cancellation.
            fixture.Provider.Reset();
            Task retry = ExecuteAsync(command, api, CancellationToken.None);
            Assert.False(retry.IsCompleted);
            fixture.Provider.Fail();
            Assert.Same(fixture.Provider.Error, await Assert.ThrowsAsync<InvalidOperationException>(() => retry));
        }

        /// <summary>
        /// APM entry points also use provider async overrides, even without a caller token.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task BeginExecute_PendingLookup_UsesAsyncProvider(int api)
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            IAsyncResult result = api switch
            {
                0 => command.BeginExecuteReader(),
                1 => command.BeginExecuteNonQuery(),
                _ => command.BeginExecuteXmlReader()
            };
            Assert.False(result.IsCompleted);
            fixture.Provider.Fail();
            Assert.Same(fixture.Provider.Error,
                await Assert.ThrowsAsync<InvalidOperationException>(() => (Task)result));
            Assert.Same(fixture.Provider.Error, Assert.Throws<InvalidOperationException>(() =>
            {
                switch (api)
                {
                    case 0:
                        command.EndExecuteReader(result);
                        break;
                    case 1:
                        command.EndExecuteNonQuery(result);
                        break;
                    default:
                        command.EndExecuteXmlReader(result);
                        break;
                }
            }));
            Assert.Equal(0, fixture.Provider.SyncCalls);
        }

        /// <summary>
        /// A failed async setup must release its cancellation registration before command reuse.
        /// </summary>
        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        [InlineData(3, false)]
        [InlineData(3, true)]
        public async Task ExecuteAsync_SetupFailure_DoesNotRetainCancellation(int api, bool closedConnection)
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            using SqlConnection unopenedConnection = new SqlConnection();
            using CancellationTokenSource firstCancellation = new CancellationTokenSource();
            command.Connection = closedConnection ? unopenedConnection : null;
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExecuteAsync(command, api, firstCancellation.Token));
            command.Connection = fixture.Connection;
            Task operation = ExecuteAsync(command, api, CancellationToken.None);
            Assert.False(operation.IsCompleted);
            firstCancellation.Cancel();
            Assert.False((bool)GetField(command, "_pendingCancel"));
            fixture.Provider.Fail();
            Assert.Same(fixture.Provider.Error,
                await Assert.ThrowsAsync<InvalidOperationException>(() => operation));
        }

        /// <summary>
        /// Batched execution shares enclave lookup and caller cancellation with standalone commands.
        /// </summary>
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        public async Task ExecuteBatchAsync_PendingLookup_PropagatesCompletion(int api, bool cancel)
        {
            using Fixture fixture = new Fixture();
            using SqlBatch batch = new SqlBatch { Connection = fixture.Connection };
            batch.BatchCommands.Add(new SqlBatchCommand("SELECT 1",
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled));
            batch.BatchCommands.Add(new SqlBatchCommand("SELECT 2",
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled));
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task operation = api switch
            {
                0 => batch.ExecuteReaderAsync(cancellation.Token),
                1 => batch.ExecuteNonQueryAsync(cancellation.Token),
                _ => batch.ExecuteScalarAsync(cancellation.Token)
            };
            Assert.False(operation.IsCompleted);
            Assert.Equal(cancellation.Token, fixture.Provider.Token);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            }
            else
            {
                fixture.Provider.Fail();
                Assert.Same(fixture.Provider.Error,
                    await Assert.ThrowsAsync<InvalidOperationException>(() => operation));
            }
            Assert.Equal(0, fixture.Provider.SyncCalls);
        }

        /// <summary>
        /// Synchronous commands retain synchronous enclave lookup and its original exception.
        /// </summary>
        [Fact]
        public void ExecuteScalar_UsesSynchronousProvider()
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            Assert.Same(fixture.Provider.Error, Assert.Throws<InvalidOperationException>(() => command.ExecuteScalar()));
            Assert.Equal(1, fixture.Provider.SyncCalls);
            Assert.Equal(0, fixture.Provider.AsyncCalls);
        }

        /// <summary>
        /// Session creation and invalidation await provider completion and preserve cancellation and failures.
        /// </summary>
        [Theory]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        public async Task SessionOperation_AwaitsProvider(bool create, int outcome)
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            using ECDiffieHellman key = ECDiffieHellman.Create();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            fixture.Provider.SessionOperation = true;
            SqlEnclaveAttestationParameters parameters = new SqlEnclaveAttestationParameters(1, Array.Empty<byte>(), key);
            SetField(command, "enclaveAttestationParameters", parameters);
            SetField(command, "enclavePackage", new EnclavePackage(Array.Empty<byte>(), fixture.Provider.Session));
            Task operation = create
                ? Invoke(command, "CreateEnclaveSessionAsync", new byte[] { 1 }, true, cancellation.Token)
                : Invoke(command, "InvalidateEnclaveSessionAsync", cancellation.Token);

            Assert.False(operation.IsCompleted);
            Assert.Equal(cancellation.Token, fixture.Provider.Token);
            Assert.Equal(0, fixture.Provider.SyncCalls);
            if (outcome == 0)
            {
                fixture.Provider.CompleteSessionOperation();
                await operation;
                if (create)
                {
                    Assert.Null(GetField(command, "enclaveAttestationParameters"));
                }
            }
            else if (outcome == 1)
            {
                fixture.Provider.FailSessionOperation();
                Assert.Same(fixture.Provider.Error,
                    await Assert.ThrowsAsync<InvalidOperationException>(() => operation));
                Assert.Same(parameters, GetField(command, "enclaveAttestationParameters"));
            }
            else
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
                Assert.Same(parameters, GetField(command, "enclaveAttestationParameters"));
            }
        }

        /// <summary>
        /// A warm lookup skips attestation preparation and does not lose provider custom data.
        /// </summary>
        [Fact]
        public async Task PrepareEnclaveAttestationAsync_CacheHit_PreservesCustomData()
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            fixture.Provider.CacheHit = true;
            var operation = (Task<byte[]>)Invoke(command, "PrepareEnclaveAttestationAsync", true, cancellation.Token);
            Assert.Null(await operation);
            Assert.Same(fixture.Provider.CustomData, GetField(command, "customData"));
            Assert.Equal(fixture.Provider.CustomData.Length, GetField(command, "customDataLength"));
            Assert.Equal(cancellation.Token, fixture.Provider.Token);
            Assert.Equal(0, fixture.Provider.SyncCalls);
        }

        /// <summary>
        /// The command retry cannot start before invalidation completes, and must not start after failure or cancellation.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Retry_AwaitsInvalidationBeforeRestarting(int outcome)
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            SetField(command, "enclavePackage", new EnclavePackage(Array.Empty<byte>(), fixture.Provider.Session));
            typeof(SqlCommand).GetMethod("GetStateObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(command, new object[] { null });
            SqlConnectionInternal inner = (SqlConnectionInternal)fixture.Connection.InnerConnection;
            inner.IncrementAsyncCount();
            var local = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var global = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            int retries = 0;
            Func<SqlCommand, IAsyncResult, bool, string, object> end =
                (cmd, result, internalEnd, name) =>
                    throw new EnclaveDelegate.RetryableEnclaveQueryExecutionException("Retry", null);
            Func<SqlCommand, CommandBehavior, AsyncCallback, object, int, bool, bool, IAsyncResult> retry =
                (cmd, behavior, callback, state, timeout, isRetry, asyncWrite) =>
                {
                    retries++;
                    return Task.FromResult<object>(null);
                };
            typeof(SqlCommand).GetMethod("CreateLocalCompletionTask", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(command, new object[]
                {
                    CommandBehavior.Default, null, 30, false, true, global, local, end, retry,
                    nameof(SqlCommand.EndExecuteReader), ADP.TimerCurrent(), cancellation.Token
                });
            local.SetResult(null);
            await await Task.WhenAny(fixture.Provider.Entered.Task, global.Task);
            Assert.False(global.Task.IsCompleted);
            Assert.Equal(0, retries);
            Assert.Equal(cancellation.Token, fixture.Provider.Token);
            if (outcome == 0)
            {
                fixture.Provider.CompleteSessionOperation();
                await global.Task;
                Assert.Equal(1, retries);
                inner.DecrementAsyncCount();
            }
            else if (outcome == 1)
            {
                fixture.Provider.FailSessionOperation();
                Assert.Same(fixture.Provider.Error,
                    await Assert.ThrowsAsync<InvalidOperationException>(() => global.Task));
                Assert.Equal(0, retries);
            }
            else
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => global.Task);
                Assert.Equal(0, retries);
            }
            Assert.Equal(0, fixture.Provider.SyncCalls);
            Assert.Equal(0, typeof(SqlConnectionInternal).GetField("_asyncCommandCount", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inner));
        }

        /// <summary>Invokes command-owned session logic without requiring a real attestation service.</summary>
        private static Task Invoke(SqlCommand command, string method, params object[] args) =>
            (Task)typeof(SqlCommand).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(command, args);

        /// <summary>Cold preparation serializes awaited parameters and preserves provider custom data.</summary>
        [Fact]
        public async Task PrepareEnclaveAttestationAsync_CacheMiss_SerializesParameters()
        {
            using Fixture fixture = new Fixture();
            using SqlCommand command = fixture.CreateCommand();
            using ECDiffieHellman key = ECDiffieHellman.Create();
            key.KeySize = 384;
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            fixture.Provider.Attestation = true;
            var parameters = new SqlEnclaveAttestationParameters(1, new byte[] { 4, 5 }, key);
            Task<byte[]> preparation = (Task<byte[]>)Invoke(
                command, "PrepareEnclaveAttestationAsync", false, cancellation.Token);
            Assert.False(preparation.IsCompleted);
            fixture.Provider.CompleteParameters(parameters);
            byte[] serialized = await preparation;
            Assert.Equal(EnclaveDelegate.Instance.GetSerializedAttestationParameters(parameters, "VBS"), serialized);
            Assert.Same(parameters, GetField(command, "enclaveAttestationParameters"));
            Assert.Same(fixture.Provider.CustomData, GetField(command, "customData"));
            Assert.Equal(fixture.Provider.CustomData.Length, GetField(command, "customDataLength"));
            Assert.Equal(cancellation.Token, fixture.Provider.Token);
            Assert.Equal(0, fixture.Provider.SyncCalls);
        }

        /// <summary>Reads command enclave state for success-only cleanup assertions.</summary>
        private static object GetField(SqlCommand command, string name) =>
            typeof(SqlCommand).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(command);

        /// <summary>Seeds command state normally obtained from the describe-encryption response.</summary>
        private static void SetField(SqlCommand command, string name, object value) =>
            typeof(SqlCommand).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(command, value);

        /// <summary>
        /// Selects a public execution API without introducing a worker thread around provider I/O.
        /// </summary>
        private static Task ExecuteAsync(SqlCommand command, int api, CancellationToken token) => api switch
        {
            0 => command.ExecuteReaderAsync(token),
            1 => command.ExecuteNonQueryAsync(token),
            2 => command.ExecuteScalarAsync(token),
            _ => command.ExecuteXmlReaderAsync(token)
        };

        /// <summary>
        /// Uses an in-process SQL connection with isolated provider registration and negotiated capabilities.
        /// </summary>
        private sealed class Fixture : IDisposable
        {
            private static int s_protocol = int.MinValue;
            private readonly TdsServer _server = new TdsServer(new TdsServerArguments());
            private readonly ConcurrentDictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider> _providers =
                (ConcurrentDictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider>)
                typeof(EnclaveDelegate).GetField("s_enclaveProviders", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            private readonly SqlConnectionAttestationProtocol _protocol =
                (SqlConnectionAttestationProtocol)Interlocked.Increment(ref s_protocol);
            private readonly SqlConnection _connection;
            internal readonly PendingProvider Provider = new PendingProvider();
            internal SqlConnection Connection => _connection;

            internal Fixture()
            {
                _server.Start();
                _connection = new SqlConnection(new SqlConnectionStringBuilder
                {
                    DataSource = $"localhost,{_server.EndPoint.Port}",
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    Pooling = false
                }.ConnectionString);
                _connection.Open();
                SqlConnectionInternal inner = (SqlConnectionInternal)_connection.InnerConnection;
                inner.Capabilities.ColumnEncryptionVersion = 3;
                inner.Capabilities.ColumnEncryptionEnclaveType = "VBS";
                typeof(SqlConnectionOptions).GetField("_enclaveAttestationUrl", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(_connection.ConnectionOptions, "https://unit.test.invalid");
                typeof(SqlConnectionOptions).GetField("_attestationProtocol", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(_connection.ConnectionOptions, _protocol);
                Assert.True(_providers.TryAdd(_protocol, Provider));
            }

            /// <summary>
            /// Enables encryption only on the command so the server need not implement enclave negotiation.
            /// </summary>
            internal SqlCommand CreateCommand() =>
                new SqlCommand("SELECT 1", _connection, null, SqlCommandColumnEncryptionSetting.Enabled);

            public void Dispose()
            {
                _connection.Dispose();
                _server.Dispose();
                Assert.True(_providers.TryRemove(_protocol, out _));
            }
        }

        /// <summary>
        /// Suspends one provider operation until explicitly faulted or canceled by its caller.
        /// </summary>
        private sealed class PendingProvider : SqlColumnEncryptionEnclaveProvider
        {
            internal readonly InvalidOperationException Error = new InvalidOperationException("Provider failure");
            internal bool Attestation;
            internal bool SessionOperation;
            internal bool CacheHit;
            internal readonly SqlEnclaveSession Session = new SqlEnclaveSession(new byte[32], 42);
            internal readonly byte[] CustomData = { 2, 3 };
            internal CancellationToken Token;
            internal int SyncCalls;
            internal int AsyncCalls;
            internal readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private TaskCompletionSource<SqlEnclaveAttestationParameters> _pending =
                new TaskCompletionSource<SqlEnclaveAttestationParameters>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _sessionOperation =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Releases a pending session operation successfully.</summary>
            internal void CompleteSessionOperation() => _sessionOperation.SetResult(true);

            /// <summary>Fails a pending session operation with the provider's original error.</summary>
            internal void FailSessionOperation() => _sessionOperation.SetException(Error);

            /// <summary>Fails only the currently pending operation.</summary>
            internal void Fail() => _pending.SetException(Error);

            /// <summary>Completes a pending attestation-parameter request.</summary>
            internal void CompleteParameters(SqlEnclaveAttestationParameters parameters) => _pending.SetResult(parameters);

            /// <summary>Creates an independent operation for command reuse checks.</summary>
            internal void Reset() => _pending =
                new TaskCompletionSource<SqlEnclaveAttestationParameters>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal override async Task<(SqlEnclaveSession SqlEnclaveSession, long Counter, byte[] CustomData, int CustomDataLength)>
                GetEnclaveSessionAsync(EnclaveSessionParameters parameters, bool generateCustomData, bool isRetry,
                    CancellationToken cancellationToken = default)
            {
                AsyncCalls++;
                Token = cancellationToken;
                if (CacheHit)
                {
                    return (Session, 42, CustomData, CustomData.Length);
                }
                if (!Attestation && !SessionOperation)
                {
                    await GetAttestationParametersAsync(null, null, 0, cancellationToken).ConfigureAwait(false);
                }
                return (null, 0, CustomData, CustomData.Length);
            }

            internal override async Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionAsync(
                byte[] info, ECDiffieHellman key, EnclaveSessionParameters parameters, byte[] data, int length,
                CancellationToken cancellationToken = default)
            {
                await AwaitSessionOperation(cancellationToken).ConfigureAwait(false);
                return (Session, 42);
            }

            internal override Task InvalidateEnclaveSessionAsync(EnclaveSessionParameters parameters,
                SqlEnclaveSession session, CancellationToken cancellationToken = default)
            {
                Assert.Same(Session, session);
                return AwaitSessionOperation(cancellationToken);
            }

            /// <summary>Waits deterministically for a provider-owned completion or caller cancellation.</summary>
            private async Task AwaitSessionOperation(CancellationToken cancellationToken)
            {
                Token = cancellationToken;
                Entered.TrySetResult(true);
                using (cancellationToken.Register(() => _sessionOperation.TrySetCanceled()))
                {
                    await _sessionOperation.Task.ConfigureAwait(false);
                }
            }

            internal override async Task<SqlEnclaveAttestationParameters> GetAttestationParametersAsync(
                string url, byte[] customData, int customDataLength, CancellationToken cancellationToken = default)
            {
                Token = cancellationToken;
                using (cancellationToken.Register(() => _pending.TrySetCanceled()))
                {
                    return await _pending.Task.ConfigureAwait(false);
                }
            }

            internal override void GetEnclaveSession(EnclaveSessionParameters parameters, bool generateCustomData,
                bool isRetry, out SqlEnclaveSession session, out long counter, out byte[] customData, out int customDataLength)
            {
                SyncCalls++;
                throw Error;
            }

            internal override SqlEnclaveAttestationParameters GetAttestationParameters(string url, byte[] data, int length) =>
                throw Error;

            internal override void CreateEnclaveSession(byte[] info, ECDiffieHellman key, EnclaveSessionParameters parameters,
                byte[] data, int length, out SqlEnclaveSession session, out long counter) => throw Error;

            internal override void InvalidateEnclaveSession(EnclaveSessionParameters parameters, SqlEnclaveSession session) =>
                throw Error;
        }
    }
}
