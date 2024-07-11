using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal class PassthroughRateLimiter : IRateLimiter
    {
        internal override ValueTask<TResult> Execute<TResult>(Func<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default)
        {
            if (Next != null)
            {
                return Next.Execute<TResult>(callback, async, cancellationToken);
            }
            else
            {
                return callback();
            }
        }
    }
}