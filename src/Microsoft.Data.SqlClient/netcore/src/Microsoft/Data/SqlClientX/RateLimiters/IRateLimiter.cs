using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal abstract class IRateLimiter
    {
        private IRateLimiter _next;
        internal virtual IRateLimiter Next
        {
            get => _next;
            set => _next = value;
        }

        internal abstract ValueTask<TResult> Execute<TResult>(Func<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default);
    }
}