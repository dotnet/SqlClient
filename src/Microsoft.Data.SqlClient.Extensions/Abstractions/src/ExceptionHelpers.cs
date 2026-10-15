// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Data.SqlClient;

/// <summary>Provides shared exception-handling policy for authentication operations.</summary>
internal static class ExceptionHelpers
{
    /// <summary>Determines whether a failure can be logged and handled without masking a fatal runtime error.</summary>
    /// <param name="exception">The failure to examine, including all inner exceptions and aggregate branches.</param>
    /// <returns>False if any exception indicates memory exhaustion, stack overflow, access violation or thread abort; otherwise true.</returns>
    internal static bool IsRecoverableException(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            Exception current = pending.Pop();
            if (current is OutOfMemoryException or StackOverflowException or AccessViolationException or ThreadAbortException)
            {
                return false;
            }
            if (current is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is Exception inner)
            {
                pending.Push(inner);
            }
        }
        return true;
    }
}
