// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Diagnostics
{
    /// <summary>
    /// The connection pool counters, as reported by <see cref="SqlClientMetrics"/>.
    /// </summary>
    /// <remarks>
    /// Components that report counters depend on this rather than on <see cref="SqlClientMetrics"/>
    /// so that tests can substitute a recording implementation. The production counters are not
    /// usable for assertions: on .NET they are only observable through an EventCounter listener,
    /// whose polling interval would make such tests slow and timing dependent, and on .NET
    /// Framework they are performance counters whose instance name is derived from the assembly
    /// name and process id, so every instance in the process shares one counter.
    /// </remarks>
    internal interface ISqlClientMetrics
    {
        /// <summary>
        /// The number of actual connections that are being made to servers
        /// </summary>
        void HardConnectRequest();

        /// <summary>
        /// The number of actual disconnects that are being made to servers
        /// </summary>
        void HardDisconnectRequest();

        /// <summary>
        /// The number of connections we get from the pool
        /// </summary>
        void SoftConnectRequest();

        /// <summary>
        /// The number of connections we return to the pool
        /// </summary>
        void SoftDisconnectRequest();

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        void EnterNonPooledConnection();

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        void ExitNonPooledConnection();

        /// <summary>
        /// The number of connections that are managed by the connection pool
        /// </summary>
        void EnterPooledConnection();

        /// <summary>
        /// The number of connections that are managed by the connection pool
        /// </summary>
        void ExitPooledConnection();

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        void EnterActiveConnectionPoolGroup();

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        void ExitActiveConnectionPoolGroup();

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        void EnterInactiveConnectionPoolGroup();

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        void ExitInactiveConnectionPoolGroup();

        /// <summary>
        /// The number of connection pools
        /// </summary>
        void EnterActiveConnectionPool();

        /// <summary>
        /// The number of connection pools
        /// </summary>
        void ExitActiveConnectionPool();

        /// <summary>
        /// The number of connection pools
        /// </summary>
        void EnterInactiveConnectionPool();

        /// <summary>
        /// The number of connection pools
        /// </summary>
        void ExitInactiveConnectionPool();

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        void EnterActiveConnection();

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        void ExitActiveConnection();

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        void EnterFreeConnection();

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        void ExitFreeConnection();

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        void EnterStasisConnection();

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        void ExitStasisConnection();

        /// <summary>
        /// The number of connections we reclaim from GC'd external connections
        /// </summary>
        void ReclaimedConnectionRequest();
    }
}
