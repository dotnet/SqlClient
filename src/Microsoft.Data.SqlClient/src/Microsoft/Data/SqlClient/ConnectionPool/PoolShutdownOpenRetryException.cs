// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Internal signal used when an asynchronous open is parked on a pool that is shut down before
    /// the request is satisfied. The connection layer consumes this and retries against the current
    /// pool group.
    /// </summary>
    internal sealed class PoolShutdownOpenRetryException : Exception
    {
        internal static PoolShutdownOpenRetryException Create() => new();

        private PoolShutdownOpenRetryException()
            : base("The connection pool shut down while an asynchronous open was pending.")
        {
        }
    }
}
