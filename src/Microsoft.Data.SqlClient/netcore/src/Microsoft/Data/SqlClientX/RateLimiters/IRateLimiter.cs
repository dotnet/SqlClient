// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal abstract class IRateLimiter
    {
        private IRateLimiter _next;
        internal virtual IRateLimiter Next
        {
            get => _next;
            set => _next = value;
        }

        internal abstract ValueTask<TResult> Execute<TResult>(Func<ValueTask<TResult>> callback, bool async, CancellationToken cancellationToken = default);
    }
}