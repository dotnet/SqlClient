// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

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