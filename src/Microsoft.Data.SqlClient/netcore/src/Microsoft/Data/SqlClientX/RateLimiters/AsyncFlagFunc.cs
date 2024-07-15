// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    /// <summary>
    /// A function that operates asynchronously based on a flag. If isAsync is true, the function operates asynchronously.
    /// If isAsync is false, the function operates synchronously.
    /// </summary>
    /// <typeparam name="TState">The type accepted by the callback as input.</typeparam>
    /// <typeparam name="TResult">The type returned by the callback.</typeparam>
    /// <param name="state">An instance of State to be passed to the callback.</param>
    /// <param name="isAsync">Indicates whether the function should operate asynchronously.</param>
    /// <param name="cancellationToken">Allows cancellation of the operation.</param>
    /// <returns>Returns the result of the callback.</returns>
    internal delegate TResult AsyncFlagFunc<in TState, out TResult>(TState state, bool isAsync, CancellationToken cancellationToken);
}
