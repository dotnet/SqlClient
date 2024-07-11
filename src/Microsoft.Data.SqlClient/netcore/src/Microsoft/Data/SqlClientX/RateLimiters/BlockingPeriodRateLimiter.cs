using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal class BlockingPeriodRateLimiter : IRateLimiter
    {
        private const int ERROR_WAIT_DEFAULT = 5 * 1000; // 5 seconds

        private Exception? _error;
        private bool _errorOccurred;
        private Timer? _errorTimer;
        private int _errorWait;

        internal BlockingPeriodRateLimiter()
        {
            _error = null;
            _errorOccurred = false;
            _errorTimer = null;
            _errorWait = ERROR_WAIT_DEFAULT;
        }

        internal override async ValueTask<TResult> Execute<TResult>(Func<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}