// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    /// <summary>
    /// An interface for rate limiters that execute arbitraty code. Intended to be small and self contained and chained together to achieve more complex behavior.
    /// </summary>
    internal abstract class IRateLimiter
    {
        private IRateLimiter _next;

        /// <summary>
        /// The next rate limiter that should be executed within the context of this rate limiter.
        /// </summary>
        internal virtual IRateLimiter Next
        {
            get => _next;
            set => _next = value;
        }

        /// <summary>
        /// Execute the provided callback within the context of the rate limit, or pass the responsibility along to the next rate limiter.
        /// </summary>
        /// <typeparam name="State">The type accepted by the callback as input.</typeparam>
        /// <typeparam name="TResult">The type of the result returned by the callback.</typeparam>
        /// <param name="callback">The callback function to execute.</param>
        /// <param name="state">An instance of State to be passed to the callback.</param>
        /// <param name="async">Whether this method should run asynchronously.</param>
        /// <param name="cancellationToken">Cancels outstanding requests.</param>
        /// <returns>Returns the result of the callback or the next rate limiter.</returns>
        internal abstract ValueTask<TResult> Execute<State, TResult>(AsyncFlagFunc<State, ValueTask<TResult>> callback, State state, bool async, CancellationToken cancellationToken = default);
    }
}