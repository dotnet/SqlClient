using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal class ConcurrencyRateLimiter : IRateLimiter
    {
        private SemaphoreSlim _concurrencyLimitSemaphore;

        internal ConcurrencyRateLimiter(int concurrencyLimit)
        {
            _concurrencyLimitSemaphore = new SemaphoreSlim(concurrencyLimit);
        }

        internal override async ValueTask<TResult> Execute<TResult>(Func<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            //TODO: in future, can enforce order
            if (async)
            {
                await _concurrencyLimitSemaphore.WaitAsync(cancellationToken);
            }
            else
            {
                _concurrencyLimitSemaphore.Wait(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            TResult result;

            try
            {
                result = await callback().ConfigureAwait(false);
            }
            finally
            {
                _concurrencyLimitSemaphore.Release();
            }

            return result;
        }
    }
}