// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Bounds cancellation and cleanup so an attention regression cannot hang the test host.
    /// </summary>
    internal static class CancellationTestHelper
    {
        internal static readonly TimeSpan WatchdogTimeout = TimeSpan.FromSeconds(45);

        /// <summary>
        /// Runs cancellation callbacks off the test thread and observes their outcome.
        /// </summary>
        /// <param name="cts">Source whose callbacks may block while sending attention.</param>
        /// <param name="context">Scenario included in watchdog failures.</param>
        internal static Task CancelWithoutBlockingAsync(
            CancellationTokenSource cts, string context)
        {
#if NET
            Task cancelTask = cts.CancelAsync();
#else
            // CancelAsync is unavailable on .NET Framework.
            Task cancelTask = Task.Run(() => cts.Cancel());
#endif
            return WaitWithoutBlockingAsync(cancelTask, $"Cancellation callbacks ({context})");
        }

        /// <summary>
        /// Disposes off the test thread, reporting both cleanup exceptions and watchdog failures.
        /// </summary>
        /// <param name="disposable">Object whose cleanup may block on an active command.</param>
        internal static Task DisposeWithoutBlockingAsync(IDisposable disposable) =>
            WaitWithoutBlockingAsync(Task.Run(disposable.Dispose), "Dispose");

        /// <summary>
        /// Bounds a task without discarding its exception if it completes after the watchdog.
        /// </summary>
        /// <param name="task">Operation whose completion and exceptions must be observed.</param>
        /// <param name="context">Operation included in watchdog failures.</param>
        internal static async Task WaitWithoutBlockingAsync(Task task, string context)
        {
            if (await Task.WhenAny(task, Task.Delay(WatchdogTimeout)) != task)
            {
                ObserveWhenComplete(task);
                Assert.Fail($"{context} did not complete within {WatchdogTimeout.TotalSeconds}s.");
            }

            await task;
        }

        /// <summary>
        /// Observes an abandoned task's eventual fault without blocking the test's failure path.
        /// </summary>
        /// <param name="task">Task that may outlive the assertion being reported.</param>
        internal static void ObserveWhenComplete(Task task)
        {
            if (task.IsCompleted)
            {
                _ = task.Exception;
            }
            else
            {
                _ = task.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            }
        }
    }
}
