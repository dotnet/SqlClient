// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    // Maintains a cache of SqlEnclaveSession instances
    internal class EnclaveSessionCache
    {
        // Cache timeout of 8 hours to be consistent with JWT validity.
        private static readonly TimeSpan s_enclaveCacheTimeout = TimeSpan.FromHours(8);

        private readonly MemoryCache enclaveMemoryCache = new MemoryCache(new MemoryCacheOptions());
        private readonly object enclaveCacheLock = new object();

        // Nonce for each message sent by the client to the server to prevent replay attacks by the server,
        // given that for Always Encrypted scenarios, the server is considered an "untrusted" man-in-the-middle.
        private long _counter;

        // Retrieves a SqlEnclaveSession from the cache
        internal SqlEnclaveSession GetEnclaveSession(EnclaveSessionParameters enclaveSessionParameters, out long counter)
        {
            Tuple<SqlConnectionAttestationProtocol, string, string, string> cacheKey =
                GenerateCacheKey(enclaveSessionParameters);
            SqlEnclaveSession enclaveSession = enclaveMemoryCache.Get<SqlEnclaveSession>(cacheKey);
            counter = Interlocked.Increment(ref _counter);
            return enclaveSession;
        }

        // Invalidates a SqlEnclaveSession entry in the cache
        internal void InvalidateSession(EnclaveSessionParameters enclaveSessionParameters, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            Tuple<SqlConnectionAttestationProtocol, string, string, string> cacheKey =
                GenerateCacheKey(enclaveSessionParameters);

            lock (enclaveCacheLock)
            {
                SqlEnclaveSession enclaveSession = GetEnclaveSession(enclaveSessionParameters, out _);

                if (enclaveSession != null && enclaveSession.SessionId == enclaveSessionToInvalidate.SessionId)
                {
                    enclaveMemoryCache.TryGetValue<SqlEnclaveSession>(cacheKey, out SqlEnclaveSession enclaveSessionToRemove);
                    if (enclaveSessionToRemove != null)
                    {
                        enclaveMemoryCache.Remove(cacheKey);
                    }
                    else
                    {
                        throw new InvalidOperationException(Strings.EnclaveSessionInvalidationFailed);
                    }
                }
            }
        }

        // Creates a new SqlEnclaveSession and adds it to the cache
        internal SqlEnclaveSession CreateSession(EnclaveSessionParameters enclaveSessionParameters, byte[] sharedSecret, long sessionId, out long counter)
        {
            Tuple<SqlConnectionAttestationProtocol, string, string, string> cacheKey =
                GenerateCacheKey(enclaveSessionParameters);
            SqlEnclaveSession enclaveSession = null;
            lock (enclaveCacheLock)
            {
                enclaveSession = new SqlEnclaveSession(sharedSecret, sessionId);
                enclaveMemoryCache.Set<SqlEnclaveSession>(cacheKey, enclaveSession,
                    absoluteExpirationRelativeToNow: s_enclaveCacheTimeout);
                counter = Interlocked.Increment(ref _counter);
            }

            return enclaveSession;
        }

        // Keep the attestation policy and field boundaries in the identity while preserving
        // the existing case-insensitive, null/empty-equivalent string matching.
        private static Tuple<SqlConnectionAttestationProtocol, string, string, string> GenerateCacheKey(
            EnclaveSessionParameters enclaveSessionParameters)
        {
            return Tuple.Create(
                enclaveSessionParameters.AttestationProtocol,
                (enclaveSessionParameters.ServerName ?? string.Empty).ToLowerInvariant(),
                (enclaveSessionParameters.Database ?? string.Empty).ToLowerInvariant(),
                (enclaveSessionParameters.AttestationUrl ?? string.Empty).ToLowerInvariant());
        }
    }
}
