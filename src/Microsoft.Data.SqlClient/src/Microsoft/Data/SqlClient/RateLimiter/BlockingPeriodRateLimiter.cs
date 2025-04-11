// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Data.SqlClient.RateLimiter
{
    /// <summary>
    /// A rate limiter that enforces a backoff (blocking) period upon error. 
    /// Each subsequent error increases the blocking duration, up to a maximum, until a success occurs.
    /// </summary>
    internal sealed class BlockingPeriodRateLimiter : RateLimiterBase
    {
        public BlockingPeriodRateLimiter(RateLimiterBase? next = null) : base(next)
        {
        }

        /// <inheritdoc/>
        internal override Task<TResult> Execute<State, TResult>(
            AsyncFlagFunc<State, Task<TResult>> callback, 
            State state, 
            bool isAsync, 
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
