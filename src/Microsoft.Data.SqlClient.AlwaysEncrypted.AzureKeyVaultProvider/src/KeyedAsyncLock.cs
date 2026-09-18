// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    /// <summary>
    /// Provides best-effort deduplication scoped to an individual key, so that concurrent callers asking
    /// for the same key are normally serialized while callers asking for different keys proceed in parallel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exclusion is deliberately best-effort rather than guaranteed. Gates are reclaimed once nobody holds
    /// or waits on them, so a caller sitting between fetching a gate and waiting on it can acquire a gate
    /// that has just been reclaimed while a later caller creates a fresh one for the same key. Two callers
    /// can therefore run the guarded work concurrently for one key. This is acceptable only where the
    /// guarded work is idempotent and duplicating it is merely wasteful, which is the case for the key
    /// store fetches this type guards. Do not reuse it where exclusion must be absolute.
    /// </para>
    /// <para>
    /// The lock is only ever awaited, never blocked on, so no thread is held while the work it guards is
    /// in flight. It must not be combined with a synchronous wait on the same gate: doing so would block a
    /// thread pool thread for the duration of an asynchronous, potentially network bound, operation.
    /// </para>
    /// <para>
    /// Cancellation applies to the caller requesting it and never to the work already in flight. A caller
    /// whose wait is cancelled simply gives up its place in line.
    /// </para>
    /// <para>
    /// Gates are removed once no caller holds or waits on them, so the number of retained gates is bounded
    /// by the number of keys being acquired concurrently rather than by the number of distinct keys seen.
    /// The gates themselves are never disposed, which is safe because <see cref="SemaphoreSlim"/> only
    /// allocates a disposable wait handle when <see cref="SemaphoreSlim.AvailableWaitHandle"/> is used.
    /// </para>
    /// </remarks>
    internal sealed class KeyedAsyncLock<TKey>
    {
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _gates = new();

        /// <summary>
        /// Gets the number of gates currently retained. Used by tests to verify that gates do not accumulate.
        /// </summary>
        internal int GateCount => _gates.Count;

        /// <summary>
        /// Asynchronously acquires the lock for the specified key.
        /// </summary>
        /// <param name="key">The key to lock.</param>
        /// <param name="cancellationToken">Token used to request cancellation of the wait.</param>
        /// <returns>A value that releases the lock when disposed.</returns>
        internal async Task<Releaser> AcquireAsync(TKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SemaphoreSlim gate = _gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));

            try
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // The gate was published before the wait, so an abandoned wait must not leave it behind.
                RemoveIfUnused(key, gate);
                throw;
            }

            return new Releaser(this, key, gate);
        }

        /// <summary>
        /// Removes the gate for the specified key if no caller holds or waits on it. A caller that fetched
        /// this instance just before removal may still acquire it, which at worst allows the guarded work
        /// to run twice and never produces an incorrect result.
        /// </summary>
        private void RemoveIfUnused(TKey key, SemaphoreSlim gate)
        {
            if (gate.CurrentCount == 1)
            {
                // ConcurrentDictionary.TryRemove(KeyValuePair) is not available on netstandard2.0, so the
                // explicitly implemented ICollection member is used to remove only a matching entry.
                ICollection<KeyValuePair<TKey, SemaphoreSlim>> gates = _gates;
                gates.Remove(new KeyValuePair<TKey, SemaphoreSlim>(key, gate));
            }
        }

        /// <summary>
        /// Releases a lock acquired from <see cref="KeyedAsyncLock{TKey}"/>. Disposal is idempotent, so a
        /// second disposal cannot inflate the gate's count and hand the key to two callers at once.
        /// </summary>
        internal sealed class Releaser : IDisposable
        {
            private readonly KeyedAsyncLock<TKey> _owner;
            private readonly TKey _key;
            private SemaphoreSlim _gate;

            internal Releaser(KeyedAsyncLock<TKey> owner, TKey key, SemaphoreSlim gate)
            {
                _owner = owner;
                _key = key;
                _gate = gate;
            }

            /// <summary>
            /// Releases the lock and discards its gate if no other caller is using it.
            /// </summary>
            public void Dispose()
            {
                SemaphoreSlim gate = Interlocked.Exchange(ref _gate, null);
                if (gate is null)
                {
                    return;
                }

                gate.Release();
                _owner.RemoveIfUnused(_key, gate);
            }
        }
    }
}
