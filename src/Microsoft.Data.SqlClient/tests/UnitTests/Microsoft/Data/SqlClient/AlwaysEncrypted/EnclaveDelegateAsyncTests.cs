// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Verifies delegate dispatch, cache rechecks, and sync/async parity without network attestation.
    /// </summary>
    public class EnclaveDelegateAsyncTests
    {
        /// <summary>
        /// Incomplete provider tasks must be awaited and their inputs and results preserved.
        /// </summary>
        [Fact]
        public async Task AsyncMethods_AwaitProviderOverrides()
        {
            using ProviderRegistration registration = new ProviderRegistration();
            TestProvider provider = registration.Provider;
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            byte[] customData = { 1, 2 };
            var lookup = new TaskCompletionSource<(SqlEnclaveSession, long, byte[], int)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            provider.Lookup = (
                parameters,
                generate,
                retry,
                ct) =>
            {
                Assert.Same(registration.Parameters, parameters);
                Assert.True(generate);
                Assert.True(retry);
                Assert.Equal(token, ct);
                return lookup.Task;
            };

            var lookupTask = EnclaveDelegate.Instance.GetEnclaveSessionAsync(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                true,
                true,
                token);
            Assert.False(lookupTask.IsCompleted);
            lookup.SetResult((provider.Session, 42, customData, customData.Length));
            var result = await lookupTask;
            Assert.Same(provider.Session, result.SqlEnclaveSession);
            Assert.Equal(42, result.Counter);
            Assert.Same(customData, result.CustomData);
            Assert.Equal(customData.Length, result.CustomDataLength);

            var attestation = new TaskCompletionSource<SqlEnclaveAttestationParameters>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            provider.Attest = (
                url,
                data,
                length,
                ct) =>
            {
                Assert.Equal(registration.Parameters.AttestationUrl, url);
                Assert.Same(customData, data);
                Assert.Equal(customData.Length, length);
                Assert.Equal(token, ct);
                return attestation.Task;
            };
            Task<SqlEnclaveAttestationParameters> attestationTask =
                EnclaveDelegate.Instance.GetAttestationParametersAsync(
                    registration.Protocol,
                    "VBS",
                    registration.Parameters.AttestationUrl,
                    customData,
                    customData.Length,
                    token);
            Assert.False(attestationTask.IsCompleted);
            SqlEnclaveAttestationParameters expected = new SqlEnclaveAttestationParameters(
                1,
                Array.Empty<byte>(),
                provider.Key);
            attestation.SetResult(expected);
            Assert.Same(expected, await attestationTask);

            var invalidation = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            provider.Invalidate = (
                parameters,
                session,
                ct) =>
            {
                Assert.Same(registration.Parameters, parameters);
                Assert.Same(provider.Session, session);
                Assert.Equal(token, ct);
                return invalidation.Task;
            };
            Task invalidationTask = EnclaveDelegate.Instance.InvalidateEnclaveSessionAsync(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                provider.Session,
                token);
            Assert.False(invalidationTask.IsCompleted);
            invalidation.SetResult(true);
            await invalidationTask;
            Assert.Equal(0, provider.SyncCalls);
        }

        /// <summary>
        /// Cold creation awaits the async cache probe and attestation; warm creation skips attestation.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_RechecksCacheBeforeCreating()
        {
            using ProviderRegistration registration = new ProviderRegistration();
            TestProvider provider = registration.Provider;
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            byte[] info = { 3 };
            byte[] data = { 4, 5 };
            using ECDiffieHellman key = ECDiffieHellman.Create();
            SqlEnclaveAttestationParameters attestation = new SqlEnclaveAttestationParameters(
                1,
                data,
                key);
            var lookup = new TaskCompletionSource<(SqlEnclaveSession, long, byte[], int)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var creation = new TaskCompletionSource<(SqlEnclaveSession, long)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var creationEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            provider.Lookup = (
                parameters,
                generate,
                retry,
                token) =>
            {
                Assert.Same(registration.Parameters, parameters);
                Assert.False(generate);
                Assert.True(retry);
                Assert.Equal(cancellation.Token, token);
                return lookup.Task;
            };
            provider.Create = (
                bytes,
                dh,
                parameters,
                custom,
                length,
                token) =>
            {
                Assert.Same(info, bytes);
                Assert.Same(key, dh);
                Assert.Same(registration.Parameters, parameters);
                Assert.Same(data, custom);
                Assert.Equal(data.Length, length);
                Assert.Equal(cancellation.Token, token);
                creationEntered.SetResult(true);
                return creation.Task;
            };

            Task task = EnclaveDelegate.Instance.CreateEnclaveSessionAsync(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                info,
                attestation,
                data,
                data.Length,
                true,
                cancellation.Token);
            Assert.False(task.IsCompleted);
            Assert.Equal(0, provider.CreateCalls);
            lookup.SetResult((null, 0, null, 0));
            await await Task.WhenAny(creationEntered.Task, task);
            Assert.False(task.IsCompleted);
            provider.Lookup = (
                parameters,
                generate,
                retry,
                token) =>
                Task.FromResult((provider.Session, 2L, (byte[])null, 0));
            Task warmTask = EnclaveDelegate.Instance.CreateEnclaveSessionAsync(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                info,
                null,
                data,
                data.Length,
                false);
            Assert.True(warmTask.IsCompleted);
            await warmTask;
            Assert.Equal(1, provider.CreateCalls);
            creation.SetResult((provider.Session, 1));
            await task;

            await EnclaveDelegate.Instance.CreateEnclaveSessionAsync(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                info,
                null,
                data,
                data.Length,
                false);
            Assert.Equal(1, provider.CreateCalls);
            Assert.Equal(0, provider.SyncCalls);
        }

        /// <summary>
        /// A provider returning no newly created session retains the synchronous validation error.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_NullSessionMatchesSyncError()
        {
            using ProviderRegistration registration = new ProviderRegistration();
            TestProvider provider = registration.Provider;
            provider.Session = null;
            SqlEnclaveAttestationParameters attestation =
                new SqlEnclaveAttestationParameters(
                    1,
                    Array.Empty<byte>(),
                    provider.Key);
            Exception sync = Record.Exception(() => EnclaveDelegate.Instance.CreateEnclaveSession(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                Array.Empty<byte>(),
                attestation,
                null,
                0,
                false));
            Exception asyncError = await Record.ExceptionAsync(() =>
                EnclaveDelegate.Instance.CreateEnclaveSessionAsync(
                    registration.Protocol,
                    "VBS",
                    registration.Parameters,
                    Array.Empty<byte>(),
                    attestation,
                    null,
                    0,
                    false));
            Assert.NotNull(sync);
            Assert.NotNull(asyncError);
            Assert.Equal(sync.GetType(), asyncError.GetType());
            Assert.Equal(sync.Message, asyncError.Message);
        }

        /// <summary>
        /// Every async operation rejects pre-cancellation without invoking any provider method.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task AsyncMethods_PreCanceled_DoNotInvokeProvider(int operation)
        {
            using ProviderRegistration registration = new ProviderRegistration();
            CancellationToken token = new CancellationToken(true);
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => InvokeAsync(
                    registration,
                    operation,
                    token));
            Assert.Equal(token, error.CancellationToken);
            Assert.Equal(0, registration.Provider.AsyncCalls);
            Assert.Equal(0, registration.Provider.SyncCalls);
        }

        /// <summary>
        /// Provider failures and in-flight cancellation escape unchanged, never as retryable errors.
        /// </summary>
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        [InlineData(4, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(4, true)]
        public async Task AsyncMethods_PropagateProviderFailures(int operation, bool cancel)
        {
            using ProviderRegistration registration = new ProviderRegistration();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TestProvider provider = registration.Provider;
            if (operation == 0 || operation == 4)
            {
                provider.Lookup = async (
                    parameters,
                    generate,
                    retry,
                    token) =>
                {
                    await completion.Task;
                    return (null, 0, null, 0);
                };
            }
            provider.Attest = async (
                url,
                data,
                length,
                token) =>
            {
                await completion.Task;
                return null;
            };
            provider.Create = async (
                info,
                key,
                parameters,
                data,
                length,
                token) =>
            {
                await completion.Task;
                return (null, 0);
            };
            provider.Invalidate = (
                parameters,
                session,
                token) => completion.Task;
            Task task = InvokeAsync(
                registration,
                operation,
                cancellation.Token);
            Assert.False(task.IsCompleted);
            Exception expected;
            if (cancel)
            {
                cancellation.Cancel();
                expected = new OperationCanceledException(cancellation.Token);
            }
            else
            {
                expected = new InvalidOperationException("Provider failure");
            }
            completion.SetException(expected);
            Assert.Same(expected, await Record.ExceptionAsync(() => task));
            Assert.Equal(cancel, task.IsCanceled);
            if (operation == 4)
            {
                Assert.Equal(0, provider.CreateCalls);
            }
        }

        /// <summary>
        /// Existing synchronous delegate methods continue dispatching only synchronous provider APIs.
        /// </summary>
        [Fact]
        public void SyncMethods_KeepSynchronousDispatch()
        {
            using ProviderRegistration registration = new ProviderRegistration();
            TestProvider provider = registration.Provider;
            EnclaveDelegate.Instance.GetEnclaveSession(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                false,
                false,
                out SqlEnclaveSession session,
                out _,
                out _);
            Assert.Null(session);
            SqlEnclaveAttestationParameters attestation = EnclaveDelegate.Instance.GetAttestationParameters(
                registration.Protocol,
                "VBS",
                registration.Parameters.AttestationUrl,
                null,
                0);
            EnclaveDelegate.Instance.CreateEnclaveSession(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                Array.Empty<byte>(),
                attestation,
                null,
                0,
                false);
            EnclaveDelegate.Instance.InvalidateEnclaveSession(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                provider.Session);
            Assert.Equal(5, provider.SyncCalls);
            Assert.Equal(0, provider.AsyncCalls);
        }

        /// <summary>
        /// Unknown protocols retain the synchronous provider-resolution error on every async entry.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task AsyncMethods_UnknownProtocolMatchesSyncError(int operation)
        {
            using ProviderRegistration registration = new ProviderRegistration();
            registration.Unregister();
            Exception sync = Record.Exception(() => EnclaveDelegate.Instance.GetEnclaveSession(
                registration.Protocol,
                "VBS",
                registration.Parameters,
                false,
                false,
                out _,
                out _,
                out _));
            Exception asyncError = await Record.ExceptionAsync(() =>
                InvokeAsync(
                    registration,
                    operation,
                    CancellationToken.None));
            Assert.NotNull(sync);
            Assert.NotNull(asyncError);
            Assert.Equal(sync.GetType(), asyncError.GetType());
            Assert.Equal(sync.Message, asyncError.Message);
        }

        /// <summary>
        /// Dispatches one delegate operation; operation four fails the creation cache recheck.
        /// </summary>
        private static Task InvokeAsync(
            ProviderRegistration registration,
            int operation,
            CancellationToken token)
        {
            switch (operation)
            {
                case 0:
                    return EnclaveDelegate.Instance.GetEnclaveSessionAsync(
                        registration.Protocol,
                        "VBS",
                        registration.Parameters,
                        true,
                        false,
                        token);
                case 1:
                    return EnclaveDelegate.Instance.GetAttestationParametersAsync(
                        registration.Protocol,
                        "VBS",
                        registration.Parameters.AttestationUrl,
                        null,
                        0,
                        token);
                case 2:
                case 4:
                    return EnclaveDelegate.Instance.CreateEnclaveSessionAsync(
                        registration.Protocol,
                        "VBS",
                        registration.Parameters,
                        Array.Empty<byte>(),
                        new SqlEnclaveAttestationParameters(
                            1,
                            Array.Empty<byte>(),
                            registration.Provider.Key),
                        null,
                        0,
                        false,
                        token);
                default:
                    return EnclaveDelegate.Instance.InvalidateEnclaveSessionAsync(
                        registration.Protocol,
                        "VBS",
                        registration.Parameters,
                        registration.Provider.Session,
                        token);
            }
        }

        /// <summary>
        /// Registers an isolated test-only protocol without replacing built-in providers or their caches.
        /// </summary>
        private sealed class ProviderRegistration : IDisposable
        {
            private static int s_protocol = int.MaxValue;
            private bool _registered;
            private readonly ConcurrentDictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider> _providers =
                (ConcurrentDictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider>)
                typeof(EnclaveDelegate).GetField("s_enclaveProviders", BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null);

            internal readonly SqlConnectionAttestationProtocol Protocol =
                (SqlConnectionAttestationProtocol)Interlocked.Decrement(ref s_protocol);
            internal readonly TestProvider Provider = new TestProvider();
            internal readonly EnclaveSessionParameters Parameters = new EnclaveSessionParameters(
                "unit.test.invalid",
                "https://unit.test.invalid",
                "database",
                SqlConnectionAttestationProtocol.HGS);

            internal ProviderRegistration()
            {
                Assert.True(_providers.TryAdd(Protocol, Provider));
                _registered = true;
            }

            /// <summary>
            /// Removes only this fixture's protocol so provider-resolution failures can be exercised.
            /// </summary>
            internal void Unregister()
            {
                Assert.True(_providers.TryRemove(Protocol, out _));
                _registered = false;
            }

            public void Dispose()
            {
                if (_registered)
                {
                    Unregister();
                }
                Provider.Key.Dispose();
            }
        }

        /// <summary>
        /// Separates synchronous dispatch from controllable, genuinely asynchronous provider operations.
        /// </summary>
        private sealed class TestProvider : SqlColumnEncryptionEnclaveProvider
        {
            internal readonly ECDiffieHellman Key = ECDiffieHellman.Create();
            internal SqlEnclaveSession Session = new SqlEnclaveSession(new byte[] { 1, 2, 3 }, 1);
            internal int SyncCalls;
            internal int AsyncCalls;
            internal int CreateCalls;
            internal Func<EnclaveSessionParameters, bool, bool, CancellationToken,
                Task<(SqlEnclaveSession, long, byte[], int)>> Lookup =
                (
                    parameters,
                    generate,
                    retry,
                    token) => Task.FromResult(((SqlEnclaveSession)null, 0L, (byte[])null, 0));
            internal Func<string, byte[], int, CancellationToken, Task<SqlEnclaveAttestationParameters>> Attest;
            internal Func<byte[], ECDiffieHellman, EnclaveSessionParameters, byte[], int, CancellationToken,
                Task<(SqlEnclaveSession, long)>> Create;
            internal Func<EnclaveSessionParameters, SqlEnclaveSession, CancellationToken, Task> Invalidate;

            internal override Task<(SqlEnclaveSession SqlEnclaveSession, long Counter, byte[] CustomData, int CustomDataLength)>
                GetEnclaveSessionAsync(
                    EnclaveSessionParameters parameters,
                    bool generateCustomData,
                    bool isRetry,
                    CancellationToken cancellationToken = default)
            {
                AsyncCalls++;
                return Lookup(
                    parameters,
                    generateCustomData,
                    isRetry,
                    cancellationToken);
            }

            internal override Task<SqlEnclaveAttestationParameters> GetAttestationParametersAsync(
                string url,
                byte[] customData,
                int customDataLength,
                CancellationToken cancellationToken = default)
            {
                AsyncCalls++;
                return Attest(
                    url,
                    customData,
                    customDataLength,
                    cancellationToken);
            }

            internal override Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionAsync(
                byte[] info,
                ECDiffieHellman key,
                EnclaveSessionParameters parameters,
                byte[] customData,
                int customDataLength,
                CancellationToken cancellationToken = default)
            {
                AsyncCalls++;
                CreateCalls++;
                return Create == null ? Task.FromResult((Session, 1L)) :
                    Create(
                        info,
                        key,
                        parameters,
                        customData,
                        customDataLength,
                        cancellationToken);
            }

            internal override Task InvalidateEnclaveSessionAsync(
                EnclaveSessionParameters parameters,
                SqlEnclaveSession session,
                CancellationToken cancellationToken = default)
            {
                AsyncCalls++;
                return Invalidate(
                    parameters,
                    session,
                    cancellationToken);
            }

            internal override void GetEnclaveSession(
                EnclaveSessionParameters parameters,
                bool generateCustomData,
                bool isRetry,
                out SqlEnclaveSession session,
                out long counter,
                out byte[] customData,
                out int customDataLength)
            {
                SyncCalls++;
                session = null;
                counter = 0;
                customData = null;
                customDataLength = 0;
            }

            internal override SqlEnclaveAttestationParameters GetAttestationParameters(
                string url,
                byte[] customData,
                int customDataLength)
            {
                SyncCalls++;
                return new SqlEnclaveAttestationParameters(
                    1,
                    Array.Empty<byte>(),
                    Key);
            }

            internal override void CreateEnclaveSession(
                byte[] info,
                ECDiffieHellman key,
                EnclaveSessionParameters parameters,
                byte[] customData,
                int customDataLength,
                out SqlEnclaveSession session,
                out long counter)
            {
                SyncCalls++;
                session = Session;
                counter = 1;
            }

            internal override void InvalidateEnclaveSession(EnclaveSessionParameters parameters, SqlEnclaveSession session)
                => SyncCalls++;
        }
    }
}
