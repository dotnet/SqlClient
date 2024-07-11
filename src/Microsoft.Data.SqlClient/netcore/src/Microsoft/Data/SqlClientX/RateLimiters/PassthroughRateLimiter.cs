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
    internal class PassthroughRateLimiter : IRateLimiter
    {
        //TODO: no state, add static instance

        /// <summary>
        /// Executes the provided callback or passes through to the next rate limiter.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the callback.</typeparam>
        /// <param name="callback">The callback function to execute.</param>
        /// <param name="async">Whether this method should run asynchronously.</param>
        /// <param name="cancellationToken">Cancels outstanding requests.</param>
        /// <returns>Returns the result of the callback or the next rate limiter.</returns>
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