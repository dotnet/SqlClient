// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Data.SqlClient.RateLimiter
{
    /// <summary>
    /// A rate limiter that enforces a concurrency limit. 
    /// When the limit is reached, new requests must wait until a spot is freed upon completion of an existing request.
    /// </summary>
    internal class ConcurrencyRateLimiter : RateLimiterBase
    {
        private readonly SemaphoreSlim _concurrencyLimitSemaphore;

        /// <summary>
        /// Initializes a new ConcurrencyRateLimiter with the specified concurrency limit.
        /// </summary>
        /// <param name="concurrencyLimit">The maximum number of concurrent requests.</param>
        /// <param name="next">The next rate limiter to apply.</param>
        internal ConcurrencyRateLimiter(int concurrencyLimit, RateLimiterBase? next = null) : base(next)
        {
            _concurrencyLimitSemaphore = new SemaphoreSlim(concurrencyLimit);
        }

        /// <inheritdoc/>
        internal sealed override async Task<TResult> Execute<State, TResult>(
            AsyncFlagFunc<State, Task<TResult>> callback, 
            State state, 
            bool isAsync, 
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO: in the future, we can enforce order
            if (isAsync)
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
                    return await Next.Execute(callback, state, isAsync, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await callback(state, isAsync, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _concurrencyLimitSemaphore.Release();
            }
        }

        public override void Dispose()
        {
            _concurrencyLimitSemaphore.Dispose();
            Next?.Dispose();
        }
    }
}
