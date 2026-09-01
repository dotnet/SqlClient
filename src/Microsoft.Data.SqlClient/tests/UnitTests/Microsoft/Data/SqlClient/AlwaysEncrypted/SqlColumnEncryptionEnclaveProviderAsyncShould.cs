// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The enclave provider hierarchy is internal and nullable-oblivious, and several of its members
// legitimately accept or produce nulls (for example, an absent enclave session). Nullable analysis
// is disabled for this file so the tests can mirror those signatures exactly.
#nullable disable

using System;
using System.Diagnostics;
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
        /// Verifies that a cancelled CreateEnclaveSessionAsync surfaces cancellation and leaves no
        /// enclave session behind in the session cache.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_WhenTokenIsCancelled_CachesNoSession()
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

        #region Failure handling

        /// <summary>
        /// Verifies that a failed attestation releases the async gate. A caller that leaks the gate on
        /// the failure path would stall every subsequent async caller for the 15 second lock timeout.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_WhenAttestationFails_ReleasesTheAsyncGate()
        {
            // The same provider instance is reused for both attestations so that this test keeps its
            // coverage if the gate ever stops being static.
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.Zero);

            provider.FailNextAttestation = true;
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => AttestAsync(provider, NewSessionParameters()));

            // A subsequent, unrelated attestation must not wait on the abandoned gate.
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SqlEnclaveSession next = await AttestAsync(provider, NewSessionParameters());
            stopwatch.Stop();

            Assert.NotNull(next);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"Attestation took {stopwatch.Elapsed}, which suggests the async gate was not released on failure.");
        }

        /// <summary>
        /// Verifies that an abandoned async session lookup does not hold the synchronous attestation
        /// gate.
        /// </summary>
        /// <remarks>
        /// <see cref="EnclaveProviderBase"/> seals <c>GetEnclaveSessionAsync</c> precisely to prevent
        /// this: the inherited default would call the synchronous <c>GetEnclaveSession</c>, which takes
        /// the sync gate and expects a later synchronous <c>CreateEnclaveSession</c> to release it. An
        /// async caller never makes that call, so a provider that failed to override the member would
        /// strand the sync gate and stall unrelated synchronous callers for the full lock timeout.
        /// </remarks>
        [Fact]
        public async Task GetEnclaveSessionAsync_WhenAbandoned_DoesNotHoldTheSyncGate()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.Zero);

            // Probe for a session that does not exist, then abandon the sequence without creating one.
            (SqlEnclaveSession session, _, _, _) = await Task.Run(
                () => provider.GetEnclaveSessionAsync(NewSessionParameters(), generateCustomData: true, isRetry: false));
            Assert.Null(session);

            // The synchronous attestation must run on a different thread than the abandoned probe:
            // GetEnclaveSessionHelper short-circuits the gate wait for a thread that is already
            // mid-attestation, which would mask a stranded gate if both ran on the same thread. A
            // dedicated thread is used rather than the thread pool, which may hand back the same thread.
            SqlEnclaveSession next = null;
            Thread syncCaller = new Thread(() => next = Attest(provider, NewSessionParameters()));

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            syncCaller.Start();
            syncCaller.Join();
            stopwatch.Stop();

            Assert.NotNull(next);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"Sync attestation took {stopwatch.Elapsed}, which suggests the async path took the sync gate.");
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
        /// Verifies that many concurrent cold-start async attestations collapse into a single
        /// attestation, complete without deadlock or a semaphore release imbalance, and leave the
        /// async gate usable afterwards.
        /// </summary>
        [Fact]
        public async Task CreateEnclaveSessionAsync_ConcurrentColdStart_CompletesWithoutDeadlock()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.FromMilliseconds(20));
            EnclaveSessionParameters parameters = NewSessionParameters();

            SqlEnclaveSession[] sessions = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(index => Task.Run(() => AttestAsync(provider, parameters))));

            Assert.All(sessions, session => Assert.NotNull(session));

            // The gate serializes the cold start and the post-gate cache re-check makes every queued
            // caller reuse the session created by the winner, so exactly one attestation is performed.
            Assert.Equal(1, provider.AttestationCount);
            Assert.All(sessions, session => Assert.Equal(sessions[0].SessionId, session.SessionId));

            // The gate is not held across GetEnclaveSessionAsync, so the work before the attestation is
            // deliberately not collapsed: each cold-start caller generates its own attestation
            // parameters. Only the attestation service call itself is shared.
            Assert.Equal(sessions.Length, provider.AttestationParametersCount);

            // The gate serialized the round trips, so no two ever overlapped.
            Assert.Equal(1, provider.MaxConcurrentAttestations);

            // The gate must still be usable for a subsequent, unrelated attestation.
            SqlEnclaveSession next = await AttestAsync(provider, NewSessionParameters());
            Assert.NotNull(next);
        }

        /// <summary>
        /// Verifies that a caller which cannot take the async gate within the timeout falls through and
        /// attests on its own rather than failing or deadlocking, and that it does not release a gate
        /// it never took.
        /// </summary>
        /// <remarks>
        /// Over-releasing would throw <see cref="SemaphoreFullException"/> and, worse, would admit an
        /// extra caller into the gate. The follow-up attestation asserts the gate is still balanced.
        /// </remarks>
        [Fact]
        public async Task CreateEnclaveSessionAsync_WhenGateWaitTimesOut_AttestsAnyway()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.Zero);
            TaskCompletionSource<bool> hold =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.HoldAttestation = hold;

            // Distinct parameters, so the blocked caller cannot be satisfied by the post-gate cache
            // re-check and is forced down the timeout fallthrough path.
            EnclaveSessionParameters holderParameters = NewSessionParameters();
            EnclaveSessionParameters blockedParameters = NewSessionParameters();

            Task<SqlEnclaveSession> holder = Task.Run(() => AttestAsync(provider, holderParameters));

            // The holder now owns the gate and is parked inside its attestation until we release it.
            Assert.True(provider.AttestationStarted.Wait(TimeSpan.FromSeconds(30)));
            provider.GateTimeoutInMilliseconds = 1;

            Task<SqlEnclaveSession> blocked = Task.Run(() => AttestAsync(provider, blockedParameters));

            // Reaching two attestations while the first is still parked is only possible if the second
            // caller gave up on the gate. Without the fallthrough this wait times out.
            await provider.WaitForAttestationCountAsync(2);
            Assert.Equal(2, provider.MaxConcurrentAttestations);

            hold.SetResult(true);
            provider.HoldAttestation = null;

            SqlEnclaveSession heldSession = await holder;
            SqlEnclaveSession blockedSession = await blocked;

            Assert.NotNull(heldSession);
            Assert.NotNull(blockedSession);
            Assert.NotEqual(heldSession.SessionId, blockedSession.SessionId);

            // The gate must still be balanced and usable. An over-release would have thrown
            // SemaphoreFullException; a lost release would hang this call.
            provider.GateTimeoutInMilliseconds = 15 * 1000;
            Assert.NotNull(await AttestAsync(provider, NewSessionParameters()));
            Assert.Equal(3, provider.AttestationCount);
        }

        /// <summary>
        /// Verifies that after a cached session is invalidated, the next async caller re-attests and
        /// obtains a new session rather than returning the evicted one.
        /// </summary>
        [Fact]
        public async Task GetEnclaveSessionAsync_AfterInvalidation_ReattestsAndReturnsNewSession()
        {
            FakeAttestationEnclaveProvider provider = new FakeAttestationEnclaveProvider(TimeSpan.Zero);
            EnclaveSessionParameters parameters = NewSessionParameters();

            SqlEnclaveSession original = await AttestAsync(provider, parameters);
            Assert.NotNull(original);
            Assert.Equal(1, provider.AttestationCount);

            await provider.InvalidateEnclaveSessionAsync(parameters, original);

            // The evicted session must no longer be visible to a plain lookup.
            (SqlEnclaveSession afterInvalidation, _, _, _) =
                await provider.GetEnclaveSessionAsync(parameters, generateCustomData: false, isRetry: false);
            Assert.Null(afterInvalidation);

            SqlEnclaveSession replacement = await AttestAsync(provider, parameters);

            Assert.NotNull(replacement);
            Assert.NotEqual(original.SessionId, replacement.SessionId);
            Assert.Equal(2, provider.AttestationCount);

            // The replacement is cached, so a subsequent caller reuses it.
            SqlEnclaveSession cached = await AttestAsync(provider, parameters);
            Assert.Equal(replacement.SessionId, cached.SessionId);
            Assert.Equal(2, provider.AttestationCount);
        }

        /// <summary>
        /// Verifies that sync and async callers can attest concurrently and that every caller ends up
        /// with a usable session.
        /// </summary>
        /// <remarks>
        /// The two paths use independent gates, so neither blocks or starves the other, but they do not
        /// collapse against each other: a sync and an async cold start that race can both attest. That
        /// is deliberate and safe. It matches the tolerance the sync design already has (when its lock
        /// timeout expires, n threads perform n attestations), and it is idempotent because
        /// <c>EnclaveSessionCache.CreateSession</c> writes under a lock and every session created for the
        /// same parameters is equally valid. The invariant that matters, and the one asserted here, is
        /// that no caller is starved or returns without a session; the cache converging on a single
        /// entry afterwards is asserted separately.
        /// </remarks>
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

            // Whichever callers raced, the cache converges on exactly one session, and every later
            // caller — sync or async — observes that same session without re-attesting.
            int attestationsBefore = provider.AttestationCount;

            SqlEnclaveSession cachedAsync = await AttestAsync(provider, parameters);
            SqlEnclaveSession cachedSync = Attest(provider, parameters);

            Assert.Equal(cachedAsync.SessionId, cachedSync.SessionId);
            Assert.Equal(attestationsBefore, provider.AttestationCount);
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
        /// Drives the full async attestation sequence the driver uses:
        /// GetEnclaveSessionAsync -> GetAttestationParametersAsync -> CreateEnclaveSessionAsync.
        /// </summary>
        private static async Task<SqlEnclaveSession> AttestAsync(
            SqlColumnEncryptionEnclaveProvider provider,
            EnclaveSessionParameters parameters,
            CancellationToken cancellationToken = default)
        {
            (SqlEnclaveSession session, _, byte[] customData, int customDataLength) =
                await provider.GetEnclaveSessionAsync(parameters, generateCustomData: true, isRetry: false, cancellationToken)
                    .ConfigureAwait(false);

            if (session != null)
            {
                return session;
            }

            // Call 2 generates the client Diffie-Hellman key that call 3 consumes.
            SqlEnclaveAttestationParameters attestationParameters = await provider
                .GetAttestationParametersAsync(parameters.AttestationUrl, customData, customDataLength, cancellationToken)
                .ConfigureAwait(false);

            (SqlEnclaveSession created, _) = await provider
                .CreateEnclaveSessionAsync(
                    Array.Empty<byte>(),
                    attestationParameters.ClientDiffieHellmanKey,
                    parameters,
                    customData,
                    customDataLength,
                    cancellationToken)
                .ConfigureAwait(false);

            return created;
        }

        /// <summary>
        /// Drives the full sync attestation sequence the driver uses:
        /// GetEnclaveSession -> GetAttestationParameters -> CreateEnclaveSession.
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

            // Call 2 generates the client Diffie-Hellman key that call 3 consumes.
            SqlEnclaveAttestationParameters attestationParameters =
                provider.GetAttestationParameters(parameters.AttestationUrl, customData, customDataLength);

            provider.CreateEnclaveSession(
                Array.Empty<byte>(),
                attestationParameters.ClientDiffieHellmanKey,
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
            private int _concurrentAttestations;
            private int _maxConcurrentAttestations;

            private int _attestationCount;

            private int _attestationParametersCount;

            internal FakeAttestationEnclaveProvider(TimeSpan attestationDelay)
            {
                _attestationDelay = attestationDelay;
            }

            /// <summary>
            /// When set, the next attestation throws and the flag is cleared, so a single provider
            /// instance can be used to exercise both a failed and a subsequent successful attestation.
            /// </summary>
            internal bool FailNextAttestation { get; set; }

            protected override bool GeneratesNonceForAttestation => true;

            /// <summary>
            /// How long this provider waits for the async attestation gate. Tests that exercise the
            /// timeout fallthrough set a small value so they do not wait out the production timeout.
            /// </summary>
            internal int GateTimeoutInMilliseconds { get; set; } = 15 * 1000;

            protected override int AsyncAttestationGateTimeoutInMilliseconds => GateTimeoutInMilliseconds;

            internal int AttestationCount => Volatile.Read(ref _attestationCount);

            /// <summary>
            /// The high-water mark of attestations running at the same time. A value of one proves the
            /// gate serialized every caller; a value above one proves at least one caller entered
            /// without holding the gate.
            /// </summary>
            internal int MaxConcurrentAttestations => Volatile.Read(ref _maxConcurrentAttestations);

            /// <summary>
            /// Signalled once an attestation round trip is actually under way. Tests use this instead of
            /// a sleep so they do not race against first-call JIT and key generation costs.
            /// </summary>
            internal ManualResetEventSlim AttestationStarted { get; } = new ManualResetEventSlim(false);

            /// <summary>
            /// When set, every asynchronous attestation parks on this source until the test completes it.
            /// This lets a test pin callers inside the attestation step and observe overlap without
            /// depending on sleeps or scheduling luck.
            /// </summary>
            internal TaskCompletionSource<bool> HoldAttestation { get; set; }

            /// <summary>
            /// Spins until <see cref="AttestationCount"/> reaches <paramref name="count"/>.
            /// </summary>
            internal async Task WaitForAttestationCountAsync(int count)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (AttestationCount < count)
                {
                    Assert.True(
                        stopwatch.Elapsed < TimeSpan.FromSeconds(30),
                        $"Timed out waiting for {count} attestations; saw {AttestationCount}.");
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }

            private void EnterAttestation()
            {
                int concurrent = Interlocked.Increment(ref _concurrentAttestations);

                int observed = Volatile.Read(ref _maxConcurrentAttestations);
                while (concurrent > observed)
                {
                    int previous = Interlocked.CompareExchange(
                        ref _maxConcurrentAttestations, concurrent, observed);
                    if (previous == observed)
                    {
                        break;
                    }

                    observed = previous;
                }

                Interlocked.Increment(ref _attestationCount);
                AttestationStarted.Set();
            }

            private void ExitAttestation() => Interlocked.Decrement(ref _concurrentAttestations);

            /// <summary>
            /// How many times attestation parameters (including the client Diffie-Hellman key) were
            /// generated. Unlike <see cref="AttestationCount"/>, this step sits outside the async gate,
            /// so it runs once per cold-start caller.
            /// </summary>
            internal int AttestationParametersCount => Volatile.Read(ref _attestationParametersCount);

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
                Interlocked.Increment(ref _attestationParametersCount);
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
                        EnterAttestation();
                        try
                        {
                            Thread.Sleep(_attestationDelay);
                        }
                        finally
                        {
                            ExitAttestation();
                        }
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

            protected override async Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionCoreAsync(
                byte[] enclaveAttestationInfo,
                ECDiffieHellman clientDiffieHellmanKey,
                EnclaveSessionParameters enclaveSessionParameters,
                byte[] customData,
                int customDataLength,
                CancellationToken cancellationToken)
            {
                EnterAttestation();
                try
                {
                    await Task.Delay(_attestationDelay, cancellationToken).ConfigureAwait(false);

                    TaskCompletionSource<bool> hold = HoldAttestation;
                    if (hold != null)
                    {
                        await hold.Task.ConfigureAwait(false);
                    }
                }
                finally
                {
                    ExitAttestation();
                }

                if (FailNextAttestation)
                {
                    FailNextAttestation = false;
                    throw new InvalidOperationException("Simulated attestation failure.");
                }

                SqlEnclaveSession sqlEnclaveSession = AddEnclaveSessionToCache(
                    enclaveSessionParameters,
                    SharedSecret,
                    Interlocked.Increment(ref s_nextSessionId),
                    out long counter);

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
