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
    internal class ConcurrencyRateLimiter : IRateLimiter
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

        /// <summary>
        /// Executes the provided callback in the context of the blocking period rate limit logic.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the callback.</typeparam>
        /// <param name="callback">The callback function to execute.</param>
        /// <param name="async">Whether this method should run asynchronously.</param>
        /// <param name="cancellationToken">Cancels outstanding requests.</param>
        /// <returns>Returns the result of the callback or the next rate limiter.</returns>
        internal override async ValueTask<TResult> Execute<TResult>(AsyncFlagFunc<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default)
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

            cancellationToken.ThrowIfCancellationRequested();

            TResult result;

            try
            {
                result = await callback(async, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _concurrencyLimitSemaphore.Release();
            }

            return result;
        }
    }
}