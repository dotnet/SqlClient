// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    /// <summary>
    /// A rate limiter that enforces a backoff (blocking) period upon error. 
    /// Each subsequent error increases the blocking duration, up to a maximum, until a success occurs.
    /// </summary>
    internal sealed class BlockingPeriodRateLimiter : RateLimiterBase
    {

        /// <inheritdoc/>
        internal override ValueTask<TResult> Execute<State, TResult>(AsyncFlagFunc<State, ValueTask<TResult>> callback, State state, bool async, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}