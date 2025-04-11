// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.Data.SqlClient.RateLimiter
{
    /// <summary>
    /// A no-op rate limiter that simply executes the callback or passes through to the next rate limiter.
    /// </summary>
    internal sealed class PassthroughRateLimiter : RateLimiterBase
    {
        //TODO: no state, add static instance

        internal PassthroughRateLimiter(RateLimiterBase? next = null) : base(next)
        {
        }

        /// <inheritdoc/>
        internal override Task<TResult> Execute<State, TResult>(
            AsyncFlagFunc<State, Task<TResult>> callback,
            State state,
            bool isAsync,
            CancellationToken cancellationToken = default)
        {
            if (Next != null)
            {
                return Next.Execute(callback, state, isAsync, cancellationToken);
            }
            else
            {
                return callback(state, isAsync, cancellationToken);
            }
        }

        public override void Dispose()
        {
            Next?.Dispose();
        }
    }
}
