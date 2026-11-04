// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <summary>Provides exception-handling policy for authentication activation and registration.</summary>
internal static class ExceptionHelpers
{
    /// <summary>Determines whether handling a failure would mask a fatal runtime error.</summary>
    /// <param name="exception">The failure, including nested and aggregate exceptions.</param>
    /// <returns>False if any nested failure is fatal or cannot be examined safely.</returns>
    internal static bool IsRecoverableException(Exception exception)
    {
        static bool IsFatal(Exception ex) =>
            ex is OutOfMemoryException or StackOverflowException or
                AccessViolationException or ThreadAbortException;

        if (IsFatal(exception))
        {
            return false;
        }

        try
        {
            Stack<Exception> pending = new();
            while (true)
            {
                if (exception is AggregateException aggregate)
                {
                    foreach (Exception inner in aggregate.InnerExceptions)
                    {
                        pending.Push(inner);
                    }
                }
                else if (exception.InnerException is Exception inner)
                {
                    pending.Push(inner);
                }

                if (pending.Count == 0)
                {
                    return true;
                }

                exception = pending.Pop();
                if (IsFatal(exception))
                {
                    return false;
                }
            }
        }
        catch (Exception ex) when (ex is OutOfMemoryException or StackOverflowException)
        {
            // Exception-tree traversal may itself exhaust memory; never recover in that case.
            return false;
        }
    }
}
