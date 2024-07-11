// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal class PassthroughRateLimiter : IRateLimiter
    {
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