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
        // Collapsing concurrent attestations is preserved by re-checking the session cache after the
        // gate is taken: the first caller performs the attestation and caches the session, and every
        // caller queued behind it observes that session and returns without contacting the
        // attestation service.
        private static readonly SemaphoreSlim s_asyncAttestationGate = new SemaphoreSlim(1, 1);

        // It is used to save the attestation url and nonce value across API calls
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

        // Takes the async attestation gate, returning a lease that releases it when disposed.
        //
        // Callers are expected to use the lease with a 'using' statement so that the gate is released
        // on every exit path, including failures and cancellation:
        //
        //     using (await AcquireAsyncAttestationGateAsync(cancellationToken).ConfigureAwait(false))
        //     {
        //         // re-check the session cache, then attest
        //     }
        //
        // If the gate cannot be taken within the lock timeout the returned lease is empty and the
        // caller proceeds with its own attestation. This mirrors the synchronous design's deliberate
        // choice to favour progress over strict collapsing when the gate holder is unusually slow.
        protected static async Task<AsyncAttestationGateLease> AcquireAsyncAttestationGateAsync(CancellationToken cancellationToken)
        {
            bool acquired = await s_asyncAttestationGate
                .WaitAsync(LockTimeoutMaxInMilliseconds, cancellationToken)
                .ConfigureAwait(false);

            return new AsyncAttestationGateLease(acquired);
        }

        // Represents ownership of the async attestation gate. Disposal releases the gate only when it
        // was actually taken, so a 'using' statement is safe even when the wait timed out. The lease
        // must be disposed exactly once, which 'using' guarantees.
        protected readonly struct AsyncAttestationGateLease : IDisposable
        {
            private readonly bool _acquired;

            internal AsyncAttestationGateLease(bool acquired)
            {
                _acquired = acquired;
            }

            public void Dispose()
            {
                if (_acquired)
                {
                    s_asyncAttestationGate.Release();
                }
            }
        }

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
