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
    /// Provides mutual exclusion scoped to an individual key, so that concurrent callers asking for the
    /// same key are serialized while callers asking for different keys proceed in parallel.
    /// </summary>
    /// <remarks>
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
        /// Releases a lock acquired from <see cref="KeyedAsyncLock{TKey}"/>.
        /// </summary>
        internal readonly struct Releaser : IDisposable
        {
            private readonly KeyedAsyncLock<TKey> _owner;
            private readonly TKey _key;
            private readonly SemaphoreSlim _gate;

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
                _gate.Release();
                _owner.RemoveIfUnused(_key, _gate);
            }
        }
    }
}
