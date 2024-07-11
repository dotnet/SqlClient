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
    internal class BlockingPeriodRateLimiter : IRateLimiter
    {
        /// <summary>
        /// Executes the provided callback in the context of the blocking period rate limit logic.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the callback.</typeparam>
        /// <param name="callback">The callback function to execute.</param>
        /// <param name="async">Whether this method should run asynchronously.</param>
        /// <param name="cancellationToken">Cancels outstanding requests.</param>
        /// <returns>Returns the result of the callback or the next rate limiter.</returns>
        /// <exception cref="NotImplementedException"></exception>
        internal override ValueTask<TResult> Execute<TResult>(Func<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}