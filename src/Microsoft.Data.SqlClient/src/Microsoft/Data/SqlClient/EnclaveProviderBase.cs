// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

// Enclave session locking model
// 1. For doing the enclave attestation, driver makes either 1, 2 or 3 API calls(in order)
// - GetEnclaveSession
// - GetAttestationParameters
// - CreateEnclaveSession

// First API call when an enclave session is cached.
// First 2 API calls when we are running a non-enclave query with no session cached.
// All 3 API calls in order when you run an enclave query with no session cached.

// In case if the enclave session is cached and validate(not expired), GetEnclaveSession API returns the SqlEnclaveSession.
// In case if the enclave session is not cached then driver end up calling GetAttestationParameters and CreateEnclaveSession.
// Note: When we have non-enclave query, then in those cases we never call CreateEnclaveSession. This is one of main pivot point for designing the below locking model.
// After the change to the API design, driver passes attestation url and servername during GetEnclaveSession and GetAttestationParameters. The extra correlation ID, such as nonce,
// will be generated during GetEnclaveSession on demand and be passed back to driver as customData and customDataLength. Later, GetAttestationParameters and CreateEnclaveSession 
// use the customData and customDataLength to do the attestation. 

// 2. In case during application start, if app spins of multiple threads at the same time (during stress test or benchmarking) where DB connection is Always encrypted enabled,
// then with the existing design we end up creating multiple enclave session. Each enclave session adds an extra memory overhead to the system and also it generates multiple calls to attestation
// service, which customer may be paying for.
// Current design try to collapse multiple GetEnclaveSession calls into a single call to CreateEnclaveSession.We achieve this goal by introducing a lock in GetEnclaveSession
// such that when we have some outstanding call doing the attestation, then all the other call to GetEnclaveSession wait for the ongoing attestation loop to complete.
// To avoid infinite thread starvation, we also added a lock timeout.
// If the ongoing attestation request completes successfully, then it creates the enclave session and release the lock so that all the subsequent request reads uses the cached value.
// In cases if the network is extremely slow and the lock timeout expires before the current ongoing attestation complete, in those cases we end up triggering the enclave attestation
// on the current thread.

// Scenario (1)
// we have 2 threads, where the both threads require enclave computation.
// When thread one invokes GetEnclaveSession then it successfully sets the event (as its the first request in the system).
// Later when the 2nd thread comes along then it gets blocked on sessionLockEvent.WaitOne till attestation on thread 1 completes or event timeout.

// case 1: Attestation on thread 1 completes before event timeout happens
// In this case thread 1 signal the event after completing the attestation and save the enclave session value.
// Thread 2 gets the event signaled and read the cache enclave session value and return.

// case 2: Attestation on thread 1 does not complete before lock time happens
// In this case thread 1 is unable to signal on time. Hence thread 2 starts its own attestation process and reduces the timeout to 0 so that any
// further request doesn't get block on timeout.
// If the attestation request on either thread completes it will signal the event and reset the time out to default value.
// In this case if we have multiple threads (say n threads) waiting for the attestation request to complete and it never completes on the first thread then we end up making n attestation requests.

// Scenario (2)
// We have 2 threads, where first query on thread 1 does not require enclave computation but subsequent query on thread 1 does, whereas on thread 2 all query needs enclave computation.
// When thread one invokes GetEnclaveSession then it successfully sets the event (as it the first request in the system).
// Later when the 2nd thread comes along then it gets blocked on sessionLockEvent.WaitOne till attestation on thread 1 completes or event timeout.

// Running first query on thread 1 while thread 2 waiting (no timeout)
// In this case thread 1 never signal the event (while running 1st query) as it does not require enclave computation.
// So thread 2 keeps waiting either for timeout to happen or thread 1 again comes in setup the enclave session to signal it.

// Running second query on thread 1 while thread 2 waiting (no timeout)
// In this case thread 1 don't have to wait for event as it’s already did it while running 1st query.
// Now thread 2 keeps waiting either for timeout to happen or thread 1 finish up setting the session.

namespace Microsoft.Data.SqlClient
{
    internal abstract class EnclaveProviderBase : SqlColumnEncryptionEnclaveProvider
    {
        #region Constants
        private const int NonceSize = 256;
        private const int LockTimeoutMaxInMilliseconds = 15 * 1000; // 15 seconds
        #endregion

        #region Members
        private static readonly EnclaveSessionCache SessionCache = new EnclaveSessionCache();

        private static AutoResetEvent sessionLockEvent = new AutoResetEvent(true);

        private static int lockTimeoutInMilliseconds = LockTimeoutMaxInMilliseconds;

        private static bool isSessionLockAcquired = false;

        private static readonly Object lockUpdateSessionLock = new Object();

        // Attestation gate used by the asynchronous path.
        //
        // The async path deliberately uses its own gate rather than sharing 'sessionLockEvent' with
        // the synchronous path. AutoResetEvent has no awaitable wait, and sharing a single
        // SemaphoreSlim across both paths would let a synchronous caller block a thread for the whole
        // duration of an awaited attestation round trip (and vice versa), which is exactly the
        // thread-pool starvation this work is meant to remove.
        //
        // Unlike the synchronous gate, the async gate is never held across API calls: it is taken and
        // released inside CreateEnclaveSessionAsync, which is the only member that talks to the
        // attestation service. That keeps acquisition and release in a single try/finally scope and
        // removes any need to track which thread (or which continuation) owns the gate — an ownership
        // model that is unsound in async code, where ConfigureAwait(false) continuations routinely
        // resume on a different thread pool thread.
        //
        // Calls to the attestation service are still collapsed: the gate is re-checked against the
        // session cache after it is taken, so the first caller performs the attestation and caches the
        // session, and every caller queued behind it observes that session and returns without
        // contacting the attestation service.
        //
        // Unlike the synchronous path, the work *before* the attestation is no longer collapsed.
        // Because this gate is not held across GetEnclaveSessionAsync, concurrent cold-start callers
        // each generate a Diffie-Hellman key in GetAttestationParametersAsync and each receive enclave
        // attestation info from the server; only the attestation service call itself is shared. This
        // is a deliberate trade: it buys an ownership model that is sound under ConfigureAwait(false)
        // at the cost of some redundant per-caller work during a cold start.
        //
        // This gate uses LockTimeoutMaxInMilliseconds directly and does not participate in the
        // synchronous path's adaptive 'lockTimeoutInMilliseconds', which sync callers drive to zero on
        // a failed acquisition and restore on a successful one. The decoupling is deliberate and
        // symmetric: async callers neither degrade that value for sync callers nor are degraded by it,
        // so the async timeout cannot be collapsed to zero by unrelated synchronous contention.
        private static readonly SemaphoreSlim s_asyncAttestationGate = new SemaphoreSlim(1, 1);

        // Records the managed thread IDs that are part way through an attestation sequence, so that
        // GetEnclaveSessionHelper can tell a re-entrant call on such a thread from a fresh caller and
        // avoid making it wait on a gate it effectively already holds. The entry is keyed by, and
        // holds, the thread ID; entries expire after s_threadRetryCacheTimeout in case the sequence is
        // abandoned. Used by the synchronous path only.
        protected static readonly MemoryCache ThreadRetryCache = new MemoryCache(new MemoryCacheOptions());
        private static readonly TimeSpan s_threadRetryCacheTimeout = TimeSpan.FromMinutes(10);
        #endregion

        #region protected methods
        // Helper method to get the enclave session from the cache if present
        protected void GetEnclaveSessionHelper(EnclaveSessionParameters enclaveSessionParameters, bool shouldGenerateNonce, bool isRetry, out SqlEnclaveSession sqlEnclaveSession, out long counter, out byte[] customData, out int customDataLength)
        {
            customData = null;
            customDataLength = 0;
            sqlEnclaveSession = SessionCache.GetEnclaveSession(enclaveSessionParameters, out counter);

            if (sqlEnclaveSession == null)
            {
                bool sessionCacheLockTaken = false;
                bool sameThreadRetry = false;

                // In case if on some thread we are running SQL workload which don't require attestation, then in those cases we don't want same thread to wait for event to be signaled.
                // hence skipping it
                string retryThreadID = ThreadRetryCache.Get<string>(Thread.CurrentThread.ManagedThreadId.ToString());
                if (!string.IsNullOrEmpty(retryThreadID))
                {
                    sameThreadRetry = true;
                }
                else if (!isRetry)
                {
                    // We are explicitly not signalling the event here, as we want to hold the event till driver calls CreateEnclaveSession
                    // If we signal the event now, then multiple thread end up calling GetAttestationParameters which triggers the attestation workflow.
                    sessionCacheLockTaken = sessionLockEvent.WaitOne(lockTimeoutInMilliseconds);

                    if (sessionCacheLockTaken)
                    {
                        lock (lockUpdateSessionLock)
                        {
                            isSessionLockAcquired = true;
                        }
                    }
                }

                // In case of multi-threaded application, first thread will set the event and all the subsequent threads will wait here either until the enclave
                // session is created or timeout happens.
                if (sessionCacheLockTaken || sameThreadRetry || isRetry)
                {
                    // While the current thread is waiting for event to be signaled and in the meanwhile we already completed the attestation on different thread
                    // then we need to signal the event here
                    sqlEnclaveSession = SessionCache.GetEnclaveSession(enclaveSessionParameters, out counter);
                    if (sqlEnclaveSession != null && !sameThreadRetry)
                    {
                        lock (lockUpdateSessionLock)
                        {
                            isSessionLockAcquired = false;
                            sessionLockEvent.Set();
                        }
                    }
                }
                else
                {
                    // In case if we are unable to signal the event, then it represents either
                    // 1. On other thread we have an ongoing attestation request which is taking more time may due to slow network or
                    // 2. Current workload doesn't require enclave computation due to which driver is not invoking the CreateEnclaveSession, hence sqlEnclaveSession is never set.
                    // In both cases we need to reduce the timeout to 0 so that subsequent request should not wait.
                    Interlocked.Exchange(ref lockTimeoutInMilliseconds, 0);
                }

                if (sqlEnclaveSession == null)
                {
                    if (shouldGenerateNonce)
                    {
                        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                        {
                            // Client decides to initiate the process of attesting the enclave and to establish a secure session with the enclave.
                            // To ensure that server send new attestation request instead of replaying / re-sending the old token, we will create a nonce for current attestation request.
                            byte[] nonce = new byte[NonceSize];
                            rng.GetBytes(nonce);
                            customData = nonce;
                            customDataLength = nonce.Length;
                        }
                    }

                    if (!sameThreadRetry)
                    {
                        retryThreadID = Thread.CurrentThread.ManagedThreadId.ToString();
                    }

                    ThreadRetryCache.Set<string>(Thread.CurrentThread.ManagedThreadId.ToString(), retryThreadID,
                        absoluteExpirationRelativeToNow: s_threadRetryCacheTimeout);
                }
            }
        }

        // Helper method to get the enclave session from the cache if present.
        // Asynchronous counterpart of GetEnclaveSessionHelper.
        //
        // Because C# async methods cannot declare 'out' parameters, the four values reported through
        // 'out' parameters by the synchronous helper are returned as a tuple.
        //
        // Unlike the synchronous helper, this method takes no lock: the async attestation gate is
        // taken and released entirely within CreateEnclaveSessionAsync (see s_asyncAttestationGate),
        // so a caller that only needs to probe the session cache never waits on another caller's
        // in-flight attestation. 'isRetry' is accepted for signature parity with the synchronous
        // helper and is unused for the same reason.
        protected Task<(SqlEnclaveSession SqlEnclaveSession, long Counter, byte[] CustomData, int CustomDataLength)> GetEnclaveSessionHelperAsync(
            EnclaveSessionParameters enclaveSessionParameters,
            bool shouldGenerateNonce,
            bool isRetry,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<(SqlEnclaveSession, long, byte[], int)>(cancellationToken);
            }

            SqlEnclaveSession sqlEnclaveSession = SessionCache.GetEnclaveSession(enclaveSessionParameters, out long counter);

            if (sqlEnclaveSession != null)
            {
                return Task.FromResult((sqlEnclaveSession, counter, (byte[])null, 0));
            }

            byte[] customData = null;
            int customDataLength = 0;

            if (shouldGenerateNonce)
            {
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    // Client decides to initiate the process of attesting the enclave and to establish a secure session with the enclave.
                    // To ensure that server send new attestation request instead of replaying / re-sending the old token, we will create a nonce for current attestation request.
                    byte[] nonce = new byte[NonceSize];
                    rng.GetBytes(nonce);
                    customData = nonce;
                    customDataLength = nonce.Length;
                }
            }

            return Task.FromResult((sqlEnclaveSession, counter, customData, customDataLength));
        }

        // How long a caller waits for the async attestation gate before giving up and attesting on its
        // own. Overridable so that tests can exercise the timeout fallthrough without waiting out the
        // full production timeout; production providers use the default.
        protected virtual int AsyncAttestationGateTimeoutInMilliseconds => LockTimeoutMaxInMilliseconds;

        // Indicates whether this provider's attestation protocol uses a client-generated nonce.
        // Mirrors the value each provider passes to GetEnclaveSessionHelper on the synchronous path.
        protected abstract bool GeneratesNonceForAttestation { get; }

        // Looks up an existing enclave session in the session cache.
        //
        // This member is sealed because the inherited default would call the *synchronous*
        // GetEnclaveSession, which takes the synchronous attestation gate and relies on a later
        // synchronous CreateEnclaveSession to release it. An async caller never makes that call, so the
        // sync gate would be held until its timeout expired and would stall unrelated sync callers.
        // Routing through GetEnclaveSessionHelperAsync keeps async callers off the sync gate entirely.
        internal sealed override Task<(SqlEnclaveSession SqlEnclaveSession, long Counter, byte[] CustomData, int CustomDataLength)> GetEnclaveSessionAsync(
            EnclaveSessionParameters enclaveSessionParameters,
            bool generateCustomData,
            bool isRetry,
            CancellationToken cancellationToken = default)
        {
            return GetEnclaveSessionHelperAsync(
                enclaveSessionParameters,
                GeneratesNonceForAttestation && generateCustomData,
                isRetry,
                cancellationToken);
        }

        // Creates a new enclave session, serializing concurrent attestations behind the async gate.
        //
        // This member is sealed so that the gate protocol lives in exactly one place and cannot be
        // bypassed (or leaked) by a derived provider. Providers supply only the protocol-specific
        // attestation logic by overriding CreateEnclaveSessionCoreAsync; acquiring the gate,
        // re-checking the session cache once it is held, and releasing the gate on every exit path
        // are all handled here.
        internal sealed override async Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionAsync(
            byte[] enclaveAttestationInfo,
            ECDiffieHellman clientDiffieHellmanKey,
            EnclaveSessionParameters enclaveSessionParameters,
            byte[] customData,
            int customDataLength,
            CancellationToken cancellationToken = default)
        {
            // If the gate cannot be taken within the lock timeout we proceed with our own attestation
            // rather than failing. This mirrors the synchronous design's deliberate choice to favour
            // progress over strict collapsing when the gate holder is unusually slow.
            bool gateAcquired = await s_asyncAttestationGate
                .WaitAsync(AsyncAttestationGateTimeoutInMilliseconds, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                // Another caller may have completed the attestation while we waited for the gate.
                SqlEnclaveSession sqlEnclaveSession = SessionCache.GetEnclaveSession(enclaveSessionParameters, out long counter);
                if (sqlEnclaveSession != null)
                {
                    return (sqlEnclaveSession, counter);
                }

                return await CreateEnclaveSessionCoreAsync(
                    enclaveAttestationInfo,
                    clientDiffieHellmanKey,
                    enclaveSessionParameters,
                    customData,
                    customDataLength,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (gateAcquired)
                {
                    s_asyncAttestationGate.Release();
                }
            }
        }

        // Performs the provider-specific enclave attestation and adds the resulting session to the
        // session cache. Asynchronous counterpart of the body of CreateEnclaveSession.
        //
        // Called by CreateEnclaveSessionAsync with the async attestation gate already held and the
        // session cache already re-checked, so implementations only need to attest and cache. They
        // must not take the gate themselves.
        protected abstract Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionCoreAsync(
            byte[] enclaveAttestationInfo,
            ECDiffieHellman clientDiffieHellmanKey,
            EnclaveSessionParameters enclaveSessionParameters,
            byte[] customData,
            int customDataLength,
            CancellationToken cancellationToken);

        // Reset the session lock status
        protected void UpdateEnclaveSessionLockStatus(SqlEnclaveSession sqlEnclaveSession)
        {
            // As per current design, we want to minimize the number of create session calls. To achieve this we block all the GetEnclaveSession calls until the first call to
            // GetEnclaveSession -> GetAttestationParameters -> CreateEnclaveSession completes or the event timeout happens.
            // Case 1: When the first request successfully creates the session, then all outstanding GetEnclaveSession will use the current session.
            // Case 2: When the first request unable to create the enclave session (may be due to some error or the first request doesn't require enclave computation) then in those case we set the event timeout to 0.
            if (sqlEnclaveSession != null && isSessionLockAcquired)
            {
                lock (lockUpdateSessionLock)
                {
                    if (isSessionLockAcquired)
                    {
                        isSessionLockAcquired = false;
                        Interlocked.Exchange(ref lockTimeoutInMilliseconds, LockTimeoutMaxInMilliseconds);
                        sessionLockEvent.Set();
                    }
                }
            }
        }

        // Helper method to remove the enclave session from the cache
        protected void InvalidateEnclaveSessionHelper(EnclaveSessionParameters enclaveSessionParameters, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            SessionCache.InvalidateSession(enclaveSessionParameters, enclaveSessionToInvalidate);
        }

        // Helper method for getting the enclave session from the session cache
        protected SqlEnclaveSession GetEnclaveSessionFromCache(EnclaveSessionParameters enclaveSessionParameters, out long counter)
        {
            return SessionCache.GetEnclaveSession(enclaveSessionParameters, out counter);
        }

        // Helper method for adding the enclave session to the session cache
        protected SqlEnclaveSession AddEnclaveSessionToCache(EnclaveSessionParameters enclaveSessionParameters, byte[] sharedSecret, long sessionId, out long counter)
        {
            return SessionCache.CreateSession(enclaveSessionParameters, sharedSecret, sessionId, out counter);
        }
    }
    #endregion
}
