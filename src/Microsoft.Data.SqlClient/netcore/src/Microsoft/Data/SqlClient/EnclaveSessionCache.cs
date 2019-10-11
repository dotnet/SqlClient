//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Runtime.Caching;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Maintains a cache of SqlEnclaveSession instances
    /// </summary>
    internal class EnclaveSessionCache
    {
        private readonly MemoryCache enclaveMemoryCache = new MemoryCache("EnclaveMemoryCache");
        private readonly Object enclaveCacheLock = new Object();

        /// <summary>
        /// Nonce for each message sent by the client to the server to prevent replay attacks by the server,
        /// given that for Always Encrypted scenarios, the server is considered an "untrusted" man-in-the-middle.
        /// </summary>
        private long _counter;

        /// <summary>
        /// Cache timeout of 8 hours to be consistent with jwt validity.
        /// </summary>
        private static int enclaveCacheTimeOutInHours = 8;

        /// <summary>
        /// Retrieves a SqlEnclaveSession from the cache
        /// </summary>
        /// <param name="servername"></param>
        /// <param name="attestationUrl"></param>
        /// <param name="counter"></param>
        public SqlEnclaveSession GetEnclaveSession(string servername, string attestationUrl, out long counter)
        {
            string cacheKey = GenerateCacheKey(servername, attestationUrl);
            SqlEnclaveSession enclaveSession = enclaveMemoryCache[cacheKey] as SqlEnclaveSession;
            counter = Interlocked.Increment(ref _counter);
            return enclaveSession;
        }

        /// <summary>
        /// Invalidates a SqlEnclaveSession entry in the cache
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="enclaveAttestationUrl"></param>
        /// <param name="enclaveSessionToInvalidate"></param>
        public void InvalidateSession(string serverName, string enclaveAttestationUrl, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            string cacheKey = GenerateCacheKey(serverName, enclaveAttestationUrl);

            lock (enclaveCacheLock)
            {
                long counter;
                SqlEnclaveSession enclaveSession = GetEnclaveSession(serverName, enclaveAttestationUrl, out counter);

                if (enclaveSession != null && enclaveSession.SessionId == enclaveSessionToInvalidate.SessionId)
                {
                    SqlEnclaveSession enclaveSessionRemoved = enclaveMemoryCache.Remove(cacheKey) as SqlEnclaveSession;
                    if (enclaveSessionRemoved == null)
                    {
                        throw new InvalidOperationException(SR.EnclaveSessionInvalidationFailed);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new SqlEnclaveSession and adds it to the cache
        /// </summary>
        /// <param name="attestationUrl"></param>
        /// <param name="serverName"></param>
        /// <param name="sharedSecret"></param>
        /// <param name="sessionId"></param>
        /// <param name="counter"></param>
        public SqlEnclaveSession CreateSession(string attestationUrl, string serverName, byte[] sharedSecret, long sessionId, out long counter)
        {
            string cacheKey = GenerateCacheKey(serverName, attestationUrl);
            SqlEnclaveSession enclaveSession = null;
            lock (enclaveCacheLock)
            {
                enclaveSession = new SqlEnclaveSession(sharedSecret, sessionId);
                enclaveMemoryCache.Add(cacheKey, enclaveSession, DateTime.UtcNow.AddHours(enclaveCacheTimeOutInHours));
                counter = Interlocked.Increment(ref _counter);
            }

            return enclaveSession;
        }

        /// <summary>
        /// Generates the cache key for the enclave session cache
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="attestationUrl"></param>
        /// <returns></returns>
        private string GenerateCacheKey(string serverName, string attestationUrl)
        {
            return (serverName + attestationUrl).ToLowerInvariant();
        }
    }
}
