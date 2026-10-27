// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

#nullable enable

namespace System.Threading.Tasks;

internal static class TaskExtensions
{
    /// <summary>
    /// Gets a <see cref="Task"/> that will complete when this <see cref="Task"/> completes or when
    /// the specified <see cref="CancellationToken"/> has cancellation requested.
    /// </summary>
    /// <param name="task">The <see cref="Task"/> to wait for.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> to monitor for a cancellation request.
    /// </param>
    /// <returns>
    /// The <see cref="Task"/> representing the asynchronous wait.  It may or may not be the same
    /// instance as the current instance.
    /// </returns>
    public static Task WaitAsync(this Task task, CancellationToken cancellationToken) =>
        task.WaitAsync(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken);
}

#endif
