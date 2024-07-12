// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    /// <summary>
    /// A no-op rate limiter that simply executes the callback or passes through to the next rate limiter.
    /// </summary>
    internal sealed class PassthroughRateLimiter : RateLimiterBase
    {
        //TODO: no state, add static instance

        /// <inheritdoc/>
        internal override ValueTask<TResult> Execute<State, TResult>(AsyncFlagFunc<State, ValueTask<TResult>> callback, State state, bool async, CancellationToken cancellationToken = default)
        {
            if (Next != null)
            {
                return Next.Execute<State, TResult>(callback, state, async, cancellationToken);
            }
            else
            {
                return callback(state, async, cancellationToken);
            }
        }
    }
}