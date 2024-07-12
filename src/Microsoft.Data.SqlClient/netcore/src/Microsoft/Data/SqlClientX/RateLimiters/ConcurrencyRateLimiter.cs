// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    /// <summary>
    /// A rate limiter that enforces a concurrency limit. 
    /// When the limit is reached, new requests must wait until a spot is freed upon completion of an existing request.
    /// </summary>
    internal class ConcurrencyRateLimiter : RateLimiterBase
    {
        private SemaphoreSlim _concurrencyLimitSemaphore;

        /// <summary>
        /// Initializes a new ConcurrencyRateLimiter with the specified concurrency limit.
        /// </summary>
        /// <param name="concurrencyLimit">The maximum number of concurrent requests.</param>
        internal ConcurrencyRateLimiter(int concurrencyLimit)
        {
            _concurrencyLimitSemaphore = new SemaphoreSlim(concurrencyLimit);
        }

        /// <inheritdoc/>
        internal sealed override async ValueTask<TResult> Execute<State, TResult>(AsyncFlagFunc<State, ValueTask<TResult>> callback, State state, bool async, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO: in the future, we can enforce order
            if (async)
            {
                await _concurrencyLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _concurrencyLimitSemaphore.Wait(cancellationToken);
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Next != null)
                {
                    return await Next.Execute<State, TResult>(callback, state, async, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await callback(state, async, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _concurrencyLimitSemaphore.Release();
            }
        }
    }
}