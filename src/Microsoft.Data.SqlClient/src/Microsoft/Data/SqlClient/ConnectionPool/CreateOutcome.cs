// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.ExceptionServices;
using Microsoft.Data.ProviderBase;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// The payload carried by the pool's idle/creation channel. A single value can represent one
    /// of three things that a waiting request may observe:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <strong>A connection</strong> (<see cref="Connection"/> is non-null): either an idle
    /// connection that was returned to the pool, or a freshly created connection published by a
    /// background create ("pump") task. The waiter takes it.
    /// </description></item>
    /// <item><description>
    /// <strong>An error</strong> (<see cref="Error"/> is non-null): a background create task failed.
    /// The FIFO-head waiter rethrows the captured exception. One failure is consumed by exactly one
    /// waiter, so demand and error delivery stay balanced.
    /// </description></item>
    /// <item><description>
    /// <strong>A bare wake</strong> (both <see cref="Connection"/> and <see cref="Error"/> null —
    /// a <c>default</c> value): a "capacity changed,
    /// re-evaluate" signal written when a slot is freed but no connection or error is available to
    /// hand out (e.g. a dead connection was removed, or a create completed without producing one).
    /// A woken waiter loops and re-pumps using its own owning connection.
    /// </description></item>
    /// </list>
    /// </summary>
    internal readonly struct CreateOutcome
    {
        /// <summary>
        /// The connection to hand to a waiter, or <see langword="null"/> when this outcome carries
        /// an error or is a bare wake.
        /// </summary>
        internal DbConnectionInternal? Connection { get; }

        /// <summary>
        /// The captured creation error to rethrow on a waiter, or <see langword="null"/> when this
        /// outcome carries a connection or is a bare wake.
        /// </summary>
        internal ExceptionDispatchInfo? Error { get; }

        /// <summary>
        /// Creates an outcome that hands a connection to a waiter.
        /// </summary>
        internal CreateOutcome(DbConnectionInternal connection)
        {
            Connection = connection;
            Error = null;
        }

        /// <summary>
        /// Creates an outcome that rethrows a captured creation error on a waiter.
        /// </summary>
        internal CreateOutcome(ExceptionDispatchInfo error)
        {
            Connection = null;
            Error = error;
        }

        /// <summary>
        /// <see langword="true"/> when this outcome carries a connection.
        /// </summary>
        internal bool HasConnection => Connection is not null;
    }
}
