// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.Data.SqlClient;

/// <summary>Provides shared exception-handling policy for authentication operations.</summary>
internal static class ExceptionHelpers
{
    /// <summary>Determines whether a failure can be logged and handled without masking a fatal runtime error.</summary>
    /// <param name="exception">The failure to examine, including its inner exception chain.</param>
    /// <returns>False if any exception in the chain indicates memory exhaustion, stack overflow, access violation or thread abort; otherwise true.</returns>
    internal static bool IsRecoverableException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OutOfMemoryException or StackOverflowException or AccessViolationException or ThreadAbortException)
            {
                return false;
            }
        }
        return true;
    }
}
