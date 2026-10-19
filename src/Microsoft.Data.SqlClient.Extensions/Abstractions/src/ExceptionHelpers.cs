// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Provides shared exception-handling policy for authentication operations.
/// </summary>
internal static class ExceptionHelpers
{
    /// <summary>
    /// Determines whether a failure can be logged and handled without masking a fatal runtime
    /// error.
    ///
    /// This will attempt to allocate memory to determine if nested exceptions are
    /// recoverable, which itself may fail if the system is under severe memory pressure.
    /// </summary>
    /// <param name="exception">
    /// The failure to examine, including all inner exceptions and aggregate branches.
    /// </param>
    /// <returns>
    /// <see langword="false" /> if any exception indicates memory exhaustion, stack overflow,
    /// access violation or thread abort; otherwise <see langword="true" />.
    /// </returns>
    internal static bool IsRecoverableException(Exception exception)
    {
        // A small number of exceptions are considered fatal and not recoverable.
        static bool IsFatal(Exception ex)
        {
            return ex is OutOfMemoryException or
                         StackOverflowException or
                         AccessViolationException or
                         ThreadAbortException;
        }

        // Is the top-level exception fatal?
        if (IsFatal(exception))
        {
            // Yes, so it's not recoverable.
            return false;
        }

        try
        {
            // Walk inner and aggregate exceptions to determine if any fatal exceptions are present.
            //
            // This is already an unexpected failure path, so the cost of walking the tree is
            // acceptable.
            //
            // Note that this allocation and the Push()/Pop() calls may fail.
            Stack<Exception> pending = new();

            // Flatten the exception tree into a stack for examination.
            void PushInnerExceptions(Exception ex)
            {
                if (ex is AggregateException aggregate)
                {
                    foreach (Exception inner in aggregate.InnerExceptions)
                    {
                        pending.Push(inner);
                    }
                }
                else if (ex.InnerException is Exception inner)
                {
                    pending.Push(inner);
                }
            }

            // Process the stack until all exceptions have been examined.
            while (true)
            {
                // Is it an aggregate exception or has inner exceptions?
                PushInnerExceptions(exception);

                // Do we have anything to examine?
                if (pending.Count == 0)
                {
                    // No, so we're done examining exceptions, and didn't find any fatal ones.
                    return true;
                }

                // Pop the next exception from the stack for examination.
                exception = pending.Pop();

                // Is it fatal?
                if (IsFatal(exception))
                {
                    // Yes, so it's not recoverable.
                    return false;
                }

                // Continue examining the next exception in the stack.
            }
            // The loop will continue until all exceptions have been examined.
        }
        catch
        {
            // If an exception occurs while walking the tree, consider it non-recoverable.
            //
            // This can't be unit tested: only the stack allocation and growth can fail here, and
            // InnerException/InnerExceptions can't be overridden to throw, so reaching this path
            // would require fault injection.
            return false;
        }
    }
}
