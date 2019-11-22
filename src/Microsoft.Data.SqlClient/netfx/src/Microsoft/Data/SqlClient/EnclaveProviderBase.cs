// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Threading;


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
// As per current API design driver passes attestation url and the server name during GetEnclaveSession but not in GetAttestationParameters.
// In order to create the attestation parameter enclave provider needs to know the attestation url. To overcome this limitation we added a AttestationInfoCache memory cache
// which save the attestation url and nonce with current thread as the lookup key.
// Later we use the AttestationInfoCache object to retrieve the attestation url in GetAttestationParameters which we saved during CreateEnclaveSession call.

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
        private const int AttestationInfoCacheTimeoutInMinutes = 10;
        private const int LockTimeoutMaxInMilliseconds = 15 * 1000; // 15 seconds
        #endregion

        #region Members
        private static readonly EnclaveSessionCache SessionCache = new EnclaveSessionCache();

        private static AutoResetEvent sessionLockEvent = new AutoResetEvent(true);

        private static int lockTimeoutInMilliseconds = LockTimeoutMaxInMilliseconds;

        private static bool isSessionLockAcquired = false;

        private static readonly Object lockUpdateSessionLock = new Object();

        internal class AttestationInfoCacheItem
        {
            public string AttestationUrl { get; private set; }

            public byte[] AttestNonce { get; private set; }
            public AttestationInfoCacheItem(string attestationUri, byte[] nonce)
            {
                AttestationUrl = attestationUri;
                AttestNonce = nonce;
            }
        }

        // It is used to save the attestation url and nonce value across API calls
        protected static readonly MemoryCache AttestationInfoCache = new MemoryCache("AttestationInfoCache");
        #endregion

        #region Public methods

        // Helper method to get the enclave session from the cache if present
        protected void GetEnclaveSessionHelper(string servername, string attestationUrl, bool shouldGenerateNonce, out SqlEnclaveSession sqlEnclaveSession, out long counter)
        {
            sqlEnclaveSession = SessionCache.GetEnclaveSession(servername, attestationUrl, out counter);
            if (sqlEnclaveSession == null)
            {
                bool sessionCacheLockTaken = false;
                bool sameThreadRetry = false;

                // In case if on some thread we are running SQL workload which don't require attestation, then in those cases we don't want same thread to wait for event to be signaled.
                // hence skipping it
                AttestationInfoCacheItem attestationInfoCacheItem = AttestationInfoCache[Thread.CurrentThread.ManagedThreadId.ToString()] as AttestationInfoCacheItem;
                if (attestationInfoCacheItem != null)
                {
                    sameThreadRetry = true;
                }
                else
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
                if (sessionCacheLockTaken || sameThreadRetry)
                {
                    // While the current thread is waiting for event to be signaled and in the meanwhile we already completed the attestation on different thread
                    // then we need to signal the event here
                    sqlEnclaveSession = SessionCache.GetEnclaveSession(servername, attestationUrl, out counter);
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
                    if (!sameThreadRetry)
                    {
                        // Client decides to initiate the process of attesting the enclave and to establish a secure session with the enclave.
                        // To ensure that server send new attestation request instead of replaying / re-sending the old token, we will create a nonce for current attestation request.
                        byte[] nonce = new byte[NonceSize];
                        if (shouldGenerateNonce)
                        {
                            using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
                            {
                                rng.GetBytes(nonce);
                            }
                        }

                        attestationInfoCacheItem = new AttestationInfoCacheItem(attestationUrl, nonce);
                    }

                    AttestationInfoCache.Set(Thread.CurrentThread.ManagedThreadId.ToString(), attestationInfoCacheItem, DateTime.UtcNow.AddMinutes(AttestationInfoCacheTimeoutInMinutes));
                }
            }
        }

        // Reset the session lock status
        protected void UpdateEnclaveSessionLockStatus(SqlEnclaveSession sqlEnclaveSession)
        {
            // As per current design, we want to minimize the number of create session calls. To acheive this we block all the GetEnclaveSession calls until the first call to
            // GetEnclaveSession -> GetAttestationParameters -> CreateEnclaveSession completes or the event timeout happens.
            // Case 1: When the first request successfully creates the session, then all outstanding GetEnclaveSession will use the current session.
            // Case 2: When the first request unable to create the encalve session (may be due to some error or the first request doesn't require enclave computation) then in those case we set the event timeout to 0.
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
        protected void InvalidateEnclaveSessionHelper(string servername, string attestationUrl, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            SessionCache.InvalidateSession(servername, attestationUrl, enclaveSessionToInvalidate);
        }

        // Helper method for getting the enclave session from the session cache
        protected SqlEnclaveSession GetEnclaveSessionFromCache(string attestationUrl, string servername, out long counter)
        {
            return SessionCache.GetEnclaveSession(servername, attestationUrl, out counter);
        }

        // Helper method for adding the enclave session to the session cache
        protected SqlEnclaveSession AddEnclaveSessionToCache(string attestationUrl, string servername, byte[] sharedSecret, long sessionId, out long counter)
        {
            return SessionCache.CreateSession(attestationUrl, servername, sharedSecret, sessionId, out counter);
        }
    }
    #endregion
}
