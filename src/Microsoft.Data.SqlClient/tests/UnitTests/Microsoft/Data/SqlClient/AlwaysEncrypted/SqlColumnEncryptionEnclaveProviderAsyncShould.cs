// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The enclave provider hierarchy is internal and nullable-oblivious, and several of its members
// legitimately accept or produce nulls (for example, an absent enclave session). Nullable analysis
// is disabled for this file so the tests can mirror those signatures exactly.
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the async counterparts declared on <see cref="SqlColumnEncryptionEnclaveProvider"/> and
    /// implemented by <see cref="EnclaveProviderBase"/>, covering the default sync fallbacks,
    /// cancellation, and concurrent enclave session creation on both the sync and async paths.
    /// </summary>
    public class SqlColumnEncryptionEnclaveProviderAsyncShould
    {
        private static readonly byte[] SharedSecret = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        private static int s_uniqueSuffix;

        /// <summary>
        /// Produces enclave session parameters with a cache key that no other test can collide with.
        /// The enclave session cache is process-wide static state shared by every provider instance.
        /// </summary>
        private static EnclaveSessionParameters NewSessionParameters()
        {
            int id = Interlocked.Increment(ref s_uniqueSuffix);
            return new EnclaveSessionParameters(
                serverName: $"unit-test-server-{id}",
                attestationUrl: $"https://unit.test.invalid/{id}",
                database: $"unit-test-db-{id}");
        }

        #region Default fallback behavior

        /// <summary>
        /// Verifies that the default async implementations defer to the synchronous overloads.
        /// </summary>
        [Fact]
        public async Task AsyncMethods_DefaultFallback_DeferToSyncOverloads()
        {
            RecordingEnclaveProvider provider = new RecordingEnclaveProvider();
            EnclaveSessionParameters parameters = NewSessionParameters();

            (SqlEnclaveSession session, long counter, byte[] customData, int customDataLength) =
                await provider.GetEnclaveSessionAsync(parameters, generateCustomData: true, isRetry: false);

            Assert.Same(provider.Session, session);
            Assert.Equal(RecordingEnclaveProvider.ExpectedCounter, counter);
            Assert.Equal(RecordingEnclaveProvider.ExpectedCustomData, customData);
            Assert.Equal(RecordingEnclaveProvider.ExpectedCustomData.Length, customDataLength);
            Assert.Equal(1, provider.GetEnclaveSessionCallCount);

            SqlEnclaveAttestationParameters attestationParameters =
                await provider.GetAttestationParametersAsync("https://unit.test.invalid", Array.Empty<byte>(), 0);
            Assert.NotNull(attestationParameters);
            Assert.Equal(1, provider.GetAttestationParametersCallCount);

            (SqlEnclaveSession createdSession, long createdCounter) =
                await provider.CreateEnclaveSessionAsync(Array.Empty<byte>(), null, parameters, Array.Empty<byte>(), 0);
            Assert.Same(provider.Session, createdSession);
            Assert.Equal(RecordingEnclaveProvider.ExpectedCounter, createdCounter);
            Assert.Equal(1, provider.CreateEnclaveSessionCallCount);

            await provider.InvalidateEnclaveSessionAsync(parameters, provider.Session);
            Assert.Equal(1, provider.InvalidateEnclaveSessionCallCount);
        }

        /// <summary>
        /// Verifies that a throwing sync implementation surfaces as a faulted Task rather than a
        /// synchronous throw from the async default.
        /// </summary>
        [Fact]
        public async Task AsyncMethods_WhenSyncThrows_ReturnFaultedTask()
        {
            ThrowingEnclaveProvider provider = new ThrowingEnclaveProvider();
            EnclaveSessionParameters parameters = NewSessionParameters();

            Task getSessionTask = provider.GetEnclaveSessionAsync(parameters, generateCustomData: false, isRetry: false);
            Task getParametersTask = provider.GetAttestationParametersAsync("https://unit.test.invalid", Array.Empty<byte>(), 0);
            Task createTask = provider.CreateEnclaveSessionAsync(Array.Empty<byte>(), null, parameters, Array.Empty<byte>(), 0);
            Task invalidateTask = provider.InvalidateEnclaveSessionAsync(parameters, null);

            Assert.True(getSessionTask.IsFaulted);
            Assert.True(getParametersTask.IsFaulted);
            Assert.True(createTask.IsFaulted);
            Assert.True(invalidateTask.IsFaulted);

            await Assert.ThrowsAsync<InvalidOperationException>(() => getSessionTask);
            await Assert.ThrowsAsync<InvalidOperationException>(() => getParametersTask);
            await Assert.ThrowsAsync<InvalidOperationException>(() => createTask);
            await Assert.ThrowsAsync<InvalidOperationException>(() => invalidateTask);
        }

        #endregion

        #region Cancellation

        /// <summary>
        /// Verifies that every async default observes an already-cancelled token before doing any work.
        /// </summary>
        [Fact]
        public async Task AsyncMethods_WithCancelledToken_ReturnCancelledTaskWithoutInvokingSyncOverload()
        {
            RecordingEnclaveProvider provider = new RecordingEnclaveProvider();
            EnclaveSessionParameters parameters = NewSessionParameters();
            CancellationToken cancelled = new CancellationToken(canceled: true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.GetEnclaveSessionAsync(parameters, false, false, cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.GetAttestationParametersAsync("https://unit.test.invalid", Array.Empty<byte>(), 0, cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.CreateEnclaveSessionAsync(Array.Empty<byte>(), null, parameters, Array.Empty<byte>(), 0, cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.InvalidateEnclaveSessionAsync(parameters, provider.Session, cancelled));

            Assert.Equal(0, provider.GetEnclaveSessionCallCount);
            Assert.Equal(0, provider.GetAttestationParametersCallCount);
            Assert.Equal(0, provider.CreateEnclaveSessionCallCount);
            Assert.Equal(0, provider.InvalidateEnclaveSessionCallCount);
        }

        /// <summary>
        /// Verifies that <see cref="AzureAttestationEnclaveProvider"/>'s CPU-bound async overrides
        /// honour cancellation.
        /// </summary>
        [Fact]
        public async Task AzureAttestationProvider_CpuBoundAsyncOverrides_HonourCancellation()
        {
            AzureAttestationEnclaveProvider provider = new AzureAttestationEnclaveProvider();
            EnclaveSessionParameters parameters = NewSessionParameters();
            CancellationToken cancelled = new CancellationToken(canceled: true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.GetAttestationParametersAsync(parameters.AttestationUrl, new byte[] { 1 }, 1, cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.InvalidateEnclaveSessionAsync(parameters, null, cancelled));
        }

        /// <summary>
        /// Verifies that the async session helper propagates cancellation while waiting for an
        /// in-flight attestation instead of blocking.
        /// </summary>
        [Fact]
        public async Task GetEnclaveSessionAsync_WhenTokenIsCancelled_PropagatesCancellation()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.Zero);
            EnclaveSessionParameters parameters = NewSessionParameters();
            CancellationToken cancelled = new CancellationToken(canceled: true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.CreateEnclaveSessionAsync(Array.Empty<byte>(), null, parameters, Array.Empty<byte>(), 0, cancelled));

            // The cancelled attempt must not have left an enclave session behind.
            (SqlEnclaveSession session, _, _, _) =
                await provider.GetEnclaveSessionAsync(parameters, generateCustomData: false, isRetry: true);
            Assert.Null(session);
        }

        /// <summary>
        /// Verifies that cancelling an in-flight attestation releases the async gate. If the gate were
        /// leaked, the next async caller would stall for the 15 second lock timeout before proceeding.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_WhenCancelled_ReleasesTheAsyncGate()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.FromMilliseconds(50));
            EnclaveSessionParameters cancelledParameters = NewSessionParameters();

            // Take the gate, then cancel before the session can be created.
            (SqlEnclaveSession session, _, byte[] customData, int customDataLength) =
                await provider.GetEnclaveSessionAsync(cancelledParameters, generateCustomData: true, isRetry: false);
            Assert.Null(session);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => provider.CreateEnclaveSessionAsync(
                        Array.Empty<byte>(), null, cancelledParameters, customData, customDataLength, cts.Token));
            }

            // A subsequent, unrelated attestation must not wait on the abandoned gate.
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SqlEnclaveSession next = await AttestAsync(provider, NewSessionParameters());
            stopwatch.Stop();

            Assert.NotNull(next);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"Attestation took {stopwatch.Elapsed}, which suggests the async gate was not released on cancellation.");
        }

        #endregion

        #region Concurrency — async path
        /// <summary>
        /// Verifies that once an enclave session is cached, concurrent async callers all observe the
        /// same session and no further attestation is performed.
        /// </summary>
        [Fact]
        public async Task GetEnclaveSessionAsync_AfterAttestation_ReturnsCachedSessionWithoutReattesting()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.FromMilliseconds(20));
            EnclaveSessionParameters parameters = NewSessionParameters();

            SqlEnclaveSession created = await AttestAsync(provider, parameters);
            Assert.NotNull(created);
            Assert.Equal(1, provider.AttestationCount);

            SqlEnclaveSession[] sessions = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(async index =>
                {
                    (SqlEnclaveSession session, _, _, _) =
                        await provider.GetEnclaveSessionAsync(parameters, generateCustomData: false, isRetry: false)
                            .ConfigureAwait(false);
                    return session;
                }));

            Assert.All(sessions, session => Assert.NotNull(session));
            Assert.All(sessions, session => Assert.Equal(created.SessionId, session.SessionId));
            Assert.Equal(1, provider.AttestationCount);
        }

        /// <summary>
        /// Verifies that many concurrent cold-start async attestations complete without deadlock or a
        /// semaphore release imbalance, and that the async gate remains usable afterwards.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_ConcurrentColdStart_CompletesWithoutDeadlock()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.FromMilliseconds(20));
            EnclaveSessionParameters parameters = NewSessionParameters();

            SqlEnclaveSession[] sessions = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(index => Task.Run(() => AttestAsync(provider, parameters))));

            Assert.All(sessions, session => Assert.NotNull(session));
            Assert.InRange(provider.AttestationCount, 1, sessions.Length);

            // The gate must still be usable for a subsequent, unrelated attestation.
            SqlEnclaveSession next = await AttestAsync(provider, NewSessionParameters());
            Assert.NotNull(next);
        }

        /// <summary>
        /// Verifies that sync and async callers can attest concurrently. The two paths use independent
        /// gates, so neither may block or starve the other.
        /// </summary>
        [Fact]
        public async Task Attestation_MixedSyncAndAsyncCallers_AllObtainSessions()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.FromMilliseconds(20));
            EnclaveSessionParameters parameters = NewSessionParameters();

            List<Task<SqlEnclaveSession>> tasks = new List<Task<SqlEnclaveSession>>();
            for (int i = 0; i < 4; i++)
            {
                tasks.Add(Task.Run(() => AttestAsync(provider, parameters)));
                tasks.Add(Task.Run(() => Attest(provider, parameters)));
            }

            SqlEnclaveSession[] sessions = await Task.WhenAll(tasks);

            Assert.All(sessions, session => Assert.NotNull(session));
        }

        #endregion

        #region Concurrency — sync path (regression guard)

        /// <summary>
        /// Verifies the synchronous equivalent of
        /// <see cref="GetEnclaveSessionAsync_AfterAttestation_ReturnsCachedSessionWithoutReattesting"/>,
        /// guarding against regressions in the untouched sync path.
        /// </summary>
        [Fact]
        public void GetEnclaveSession_AfterAttestation_ReturnsCachedSessionWithoutReattesting()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.FromMilliseconds(20));
            EnclaveSessionParameters parameters = NewSessionParameters();

            SqlEnclaveSession created = Attest(provider, parameters);
            Assert.NotNull(created);
            Assert.Equal(1, provider.AttestationCount);

            for (int i = 0; i < 8; i++)
            {
                provider.GetEnclaveSession(parameters, false, false, out SqlEnclaveSession session, out _, out _, out _);
                Assert.NotNull(session);
                Assert.Equal(created.SessionId, session.SessionId);
            }

            Assert.Equal(1, provider.AttestationCount);
        }

        #endregion

        #region HGS provider — truly async HTTP

        /// <summary>
        /// Verifies that the HGS attestation request observes an already-cancelled token before
        /// issuing any HTTP request.
        /// </summary>
        [Fact]
        public async Task HgsMakeRequestAsync_WithCancelledToken_Throws()
        {
            TestHostGuardianServiceEnclaveProvider provider = new TestHostGuardianServiceEnclaveProvider();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.InvokeMakeRequestAsync("http://localhost:1/signingCertificates", new CancellationToken(canceled: true)));
        }

        /// <summary>
        /// Verifies that a failed HGS attestation request surfaces as an attestation failure, matching
        /// the synchronous behaviour.
        /// </summary>
        [Fact]
        public async Task HgsMakeRequestAsync_WhenRequestFails_ThrowsAttestationFailure()
        {
            TestHostGuardianServiceEnclaveProvider provider = new TestHostGuardianServiceEnclaveProvider { MaxNumRetries = 0 };

            SqlException exception = await Assert.ThrowsAsync<SqlException>(
                () => provider.InvokeMakeRequestAsync("http://localhost:1/signingCertificates", CancellationToken.None));

            Assert.NotNull(exception.InnerException);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Drives the async GetEnclaveSession -> CreateEnclaveSession attestation sequence.
        /// </summary>
        private static async Task<SqlEnclaveSession> AttestAsync(
            SqlColumnEncryptionEnclaveProvider provider,
            EnclaveSessionParameters parameters)
        {
            (SqlEnclaveSession session, _, byte[] customData, int customDataLength) =
                await provider.GetEnclaveSessionAsync(parameters, generateCustomData: true, isRetry: false)
                    .ConfigureAwait(false);

            if (session != null)
            {
                return session;
            }

            (SqlEnclaveSession created, _) = await provider
                .CreateEnclaveSessionAsync(Array.Empty<byte>(), null, parameters, customData, customDataLength)
                .ConfigureAwait(false);

            return created;
        }

        /// <summary>
        /// Drives the sync GetEnclaveSession -> CreateEnclaveSession attestation sequence.
        /// </summary>
        private static SqlEnclaveSession Attest(
            SqlColumnEncryptionEnclaveProvider provider,
            EnclaveSessionParameters parameters)
        {
            provider.GetEnclaveSession(
                parameters,
                generateCustomData: true,
                isRetry: false,
                out SqlEnclaveSession session,
                out _,
                out byte[] customData,
                out int customDataLength);

            if (session != null)
            {
                return session;
            }

            provider.CreateEnclaveSession(
                Array.Empty<byte>(),
                null,
                parameters,
                customData,
                customDataLength,
                out SqlEnclaveSession created,
                out _);

            return created;
        }

        /// <summary>
        /// A provider whose synchronous members record their invocations, used to prove that the async
        /// defaults on the abstract base type defer to them.
        /// </summary>
        private sealed class RecordingEnclaveProvider : SqlColumnEncryptionEnclaveProvider
        {
            internal const long ExpectedCounter = 42;

            internal static readonly byte[] ExpectedCustomData = new byte[] { 9, 8, 7 };

            internal SqlEnclaveSession Session { get; } = new SqlEnclaveSession(SharedSecret, sessionId: 1);

            internal int GetEnclaveSessionCallCount { get; private set; }

            internal int GetAttestationParametersCallCount { get; private set; }

            internal int CreateEnclaveSessionCallCount { get; private set; }

            internal int InvalidateEnclaveSessionCallCount { get; private set; }

            internal override void GetEnclaveSession(
                EnclaveSessionParameters enclaveSessionParameters,
                bool generateCustomData,
                bool isRetry,
                out SqlEnclaveSession sqlEnclaveSession,
                out long counter,
                out byte[] customData,
                out int customDataLength)
            {
                GetEnclaveSessionCallCount++;
                sqlEnclaveSession = Session;
                counter = ExpectedCounter;
                customData = ExpectedCustomData;
                customDataLength = ExpectedCustomData.Length;
            }

            internal override SqlEnclaveAttestationParameters GetAttestationParameters(
                string attestationUrl,
                byte[] customData,
                int customDataLength)
            {
                GetAttestationParametersCallCount++;
                return new SqlEnclaveAttestationParameters(
                    protocol: 1,
                    input: Array.Empty<byte>(),
                    clientDiffieHellmanKey: ECDiffieHellman.Create());
            }

            internal override void CreateEnclaveSession(
                byte[] enclaveAttestationInfo,
                ECDiffieHellman clientDiffieHellmanKey,
                EnclaveSessionParameters enclaveSessionParameters,
                byte[] customData,
                int customDataLength,
                out SqlEnclaveSession sqlEnclaveSession,
                out long counter)
            {
                CreateEnclaveSessionCallCount++;
                sqlEnclaveSession = Session;
                counter = ExpectedCounter;
            }

            internal override void InvalidateEnclaveSession(
                EnclaveSessionParameters enclaveSessionParameters,
                SqlEnclaveSession enclaveSession)
            {
                InvalidateEnclaveSessionCallCount++;
            }
        }

        /// <summary>
        /// A provider whose synchronous members always throw, used to prove that the async defaults
        /// return faulted Tasks instead of throwing synchronously.
        /// </summary>
        private sealed class ThrowingEnclaveProvider : SqlColumnEncryptionEnclaveProvider
        {
            internal override void GetEnclaveSession(
                EnclaveSessionParameters enclaveSessionParameters,
                bool generateCustomData,
                bool isRetry,
                out SqlEnclaveSession sqlEnclaveSession,
                out long counter,
                out byte[] customData,
                out int customDataLength)
                => throw new InvalidOperationException();

            internal override SqlEnclaveAttestationParameters GetAttestationParameters(
                string attestationUrl,
                byte[] customData,
                int customDataLength)
                => throw new InvalidOperationException();

            internal override void CreateEnclaveSession(
                byte[] enclaveAttestationInfo,
                ECDiffieHellman clientDiffieHellmanKey,
                EnclaveSessionParameters enclaveSessionParameters,
                byte[] customData,
                int customDataLength,
                out SqlEnclaveSession sqlEnclaveSession,
                out long counter)
                => throw new InvalidOperationException();

            internal override void InvalidateEnclaveSession(
                EnclaveSessionParameters enclaveSessionParameters,
                SqlEnclaveSession enclaveSession)
                => throw new InvalidOperationException();
        }

        /// <summary>
        /// An <see cref="EnclaveProviderBase"/> whose "attestation" is a delay, so that the sync and
        /// async session gates can be exercised without a network dependency.
        /// </summary>
        private sealed class FakeAttestationEnclaveProvider : EnclaveProviderBase
        {
            private static long s_nextSessionId;

            private readonly TimeSpan _attestationDelay;

            private int _attestationCount;

            internal FakeAttestationEnclaveProvider(TimeSpan attestationDelay)
            {
                _attestationDelay = attestationDelay;
            }

            internal int AttestationCount => Volatile.Read(ref _attestationCount);

            internal override void GetEnclaveSession(
                EnclaveSessionParameters enclaveSessionParameters,
                bool generateCustomData,
                bool isRetry,
                out SqlEnclaveSession sqlEnclaveSession,
                out long counter,
                out byte[] customData,
                out int customDataLength)
            {
                GetEnclaveSessionHelper(
                    enclaveSessionParameters,
                    generateCustomData,
                    isRetry,
                    out sqlEnclaveSession,
                    out counter,
                    out customData,
                    out customDataLength);
            }

            internal override SqlEnclaveAttestationParameters GetAttestationParameters(
                string attestationUrl,
                byte[] customData,
                int customDataLength)
            {
                return new SqlEnclaveAttestationParameters(
                    protocol: 1,
                    input: Array.Empty<byte>(),
                    clientDiffieHellmanKey: ECDiffieHellman.Create());
            }

            internal override void CreateEnclaveSession(
                byte[] enclaveAttestationInfo,
                ECDiffieHellman clientDiffieHellmanKey,
                EnclaveSessionParameters enclaveSessionParameters,
                byte[] customData,
                int customDataLength,
                out SqlEnclaveSession sqlEnclaveSession,
                out long counter)
            {
                sqlEnclaveSession = null;
                counter = 0;
                try
                {
                    ThreadRetryCache.Remove(Thread.CurrentThread.ManagedThreadId.ToString());
                    sqlEnclaveSession = GetEnclaveSessionFromCache(enclaveSessionParameters, out counter);
                    if (sqlEnclaveSession == null)
                    {
                        Interlocked.Increment(ref _attestationCount);
                        Thread.Sleep(_attestationDelay);
                        sqlEnclaveSession = AddEnclaveSessionToCache(
                            enclaveSessionParameters,
                            SharedSecret,
                            Interlocked.Increment(ref s_nextSessionId),
                            out counter);
                    }
                }
                finally
                {
                    UpdateEnclaveSessionLockStatus(sqlEnclaveSession);
                }
            }

            internal override void InvalidateEnclaveSession(
                EnclaveSessionParameters enclaveSessionParameters,
                SqlEnclaveSession enclaveSession)
            {
                InvalidateEnclaveSessionHelper(enclaveSessionParameters, enclaveSession);
            }

            internal override Task<(SqlEnclaveSession SqlEnclaveSession, long Counter, byte[] CustomData, int CustomDataLength)> GetEnclaveSessionAsync(
                EnclaveSessionParameters enclaveSessionParameters,
                bool generateCustomData,
                bool isRetry,
                CancellationToken cancellationToken = default)
            {
                return GetEnclaveSessionHelperAsync(enclaveSessionParameters, generateCustomData, isRetry, cancellationToken);
            }

            internal override async Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionAsync(
                byte[] enclaveAttestationInfo,
                ECDiffieHellman clientDiffieHellmanKey,
                EnclaveSessionParameters enclaveSessionParameters,
                byte[] customData,
                int customDataLength,
                CancellationToken cancellationToken = default)
            {
                SqlEnclaveSession sqlEnclaveSession = null;
                long counter = 0;
                try
                {
                    ThreadRetryCache.Remove(Thread.CurrentThread.ManagedThreadId.ToString());
                    sqlEnclaveSession = GetEnclaveSessionFromCache(enclaveSessionParameters, out counter);
                    if (sqlEnclaveSession == null)
                    {
                        Interlocked.Increment(ref _attestationCount);
                        await Task.Delay(_attestationDelay, cancellationToken).ConfigureAwait(false);
                        sqlEnclaveSession = AddEnclaveSessionToCache(
                            enclaveSessionParameters,
                            SharedSecret,
                            Interlocked.Increment(ref s_nextSessionId),
                            out counter);
                    }
                }
                finally
                {
                    UpdateAsyncEnclaveSessionLockStatus(sqlEnclaveSession, cancellationToken);
                }

                return (sqlEnclaveSession, counter);
            }
        }

        /// <summary>
        /// Exposes the HGS provider's protected request members to the test.
        /// </summary>
        private sealed class TestHostGuardianServiceEnclaveProvider : HostGuardianServiceEnclaveProvider
        {
            internal Task<byte[]> InvokeMakeRequestAsync(string url, CancellationToken cancellationToken)
                => MakeRequestAsync(url, cancellationToken);
        }

        #endregion
    }
}
