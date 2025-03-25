// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
#if NET
using System.Threading;
#endif

#nullable enable

namespace Microsoft.Data.SqlClient.Diagnostics
{
    // This encapsulates three types of metrics:
    // * Default: These metrics are always enabled.
    // * Verbose: These metrics aren't enabled by default, but can be enabled on application startup if a trace switch is set to verbose.
    // * Trace: These metrics are enabled with the SqlClientEventSource.
    // Verbose metrics are only present for backwards compatibility.
    internal sealed partial class SqlClientMetrics
    {
        private readonly SqlClientEventSource _eventSource;

        public SqlClientMetrics(SqlClientEventSource eventSource)
        {
            _eventSource = eventSource;
            EnableDefaultMetrics();
        }

        private void EnableDefaultMetrics()
        {
#if NETFRAMEWORK
            EnablePerformanceCounters();
#endif
        }

        public void EnableTraceMetrics()
        {
#if NET
            EnableEventCounters();
#endif
        }

        /// <summary>
        /// The number of actual connections that are being made to servers
        /// </summary>
        internal void HardConnectRequest()
        {
#if NET
            Interlocked.Increment(ref _activeHardConnectionsCounter);
            Interlocked.Increment(ref _hardConnectsCounter);
#endif
#if NETFRAMEWORK
            _hardConnectsPerSecond?.Increment();
#endif
        }

        /// <summary>
        /// The number of actual disconnects that are being made to servers
        /// </summary>
        internal void HardDisconnectRequest()
        {
#if NET
            Interlocked.Decrement(ref _activeHardConnectionsCounter);
            Interlocked.Increment(ref _hardDisconnectsCounter);
#endif
#if NETFRAMEWORK
            _hardDisconnectsPerSecond?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections we get from the pool
        /// </summary>
        internal void SoftConnectRequest()
        {
#if NET
            Interlocked.Increment(ref _activeSoftConnectionsCounter);
            Interlocked.Increment(ref _softConnectsCounter);
#endif
#if NETFRAMEWORK
            _softConnectsPerSecond?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections we return to the pool
        /// </summary>
        internal void SoftDisconnectRequest()
        {
#if NET
            Interlocked.Decrement(ref _activeSoftConnectionsCounter);
            Interlocked.Increment(ref _softDisconnectsCounter);
#endif
#if NETFRAMEWORK
            _softDisconnectsPerSecond?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        internal void EnterNonPooledConnection()
        {
#if NET
            Interlocked.Increment(ref _nonPooledConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfNonPooledConnections?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        internal void ExitNonPooledConnection()
        {
#if NET
            Interlocked.Decrement(ref _nonPooledConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfNonPooledConnections?.Decrement();
#endif
        }

        /// <summary>
        /// The number of connections that are managed by the connection pool
        /// </summary>
        internal void EnterPooledConnection()
        {
#if NET
            Interlocked.Increment(ref _pooledConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfPooledConnections?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections that are managed by the connection pool
        /// </summary>
        internal void ExitPooledConnection()
        {
#if NET
            Interlocked.Decrement(ref _pooledConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfPooledConnections?.Decrement();
#endif
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        internal void EnterActiveConnectionPoolGroup()
        {
#if NET
            Interlocked.Increment(ref _activeConnectionPoolGroupsCounter);
#endif
#if NETFRAMEWORK
            _numberOfActiveConnectionPoolGroups?.Increment();
#endif
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        internal void ExitActiveConnectionPoolGroup()
        {
#if NET
            Interlocked.Decrement(ref _activeConnectionPoolGroupsCounter);
#endif
#if NETFRAMEWORK
            _numberOfActiveConnectionPoolGroups?.Decrement();
#endif
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        internal void EnterInactiveConnectionPoolGroup()
        {
#if NET
            Interlocked.Increment(ref _inactiveConnectionPoolGroupsCounter);
#endif
#if NETFRAMEWORK
            _numberOfInactiveConnectionPoolGroups?.Increment();
#endif
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        internal void ExitInactiveConnectionPoolGroup()
        {
#if NET
            Interlocked.Decrement(ref _inactiveConnectionPoolGroupsCounter);
#endif
#if NETFRAMEWORK
            _numberOfInactiveConnectionPoolGroups?.Decrement();
#endif
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void EnterActiveConnectionPool()
        {
#if NET
            Interlocked.Increment(ref _activeConnectionPoolsCounter);
#endif
#if NETFRAMEWORK
            _numberOfActiveConnectionPools?.Increment();
#endif
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void ExitActiveConnectionPool()
        {
#if NET
            Interlocked.Decrement(ref _activeConnectionPoolsCounter);
#endif
#if NETFRAMEWORK
            _numberOfActiveConnectionPools?.Decrement();
#endif
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void EnterInactiveConnectionPool()
        {
#if NET
            Interlocked.Increment(ref _inactiveConnectionPoolsCounter);
#endif
#if NETFRAMEWORK
            _numberOfInactiveConnectionPools?.Increment();
#endif
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void ExitInactiveConnectionPool()
        {
#if NET
            Interlocked.Decrement(ref _inactiveConnectionPoolsCounter);
#endif
#if NETFRAMEWORK
            _numberOfInactiveConnectionPools?.Decrement();
#endif
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        internal void EnterActiveConnection()
        {
#if NET
            Interlocked.Increment(ref _activeConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfActiveConnections?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        internal void ExitActiveConnection()
        {
#if NET
            Interlocked.Decrement(ref _activeConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfActiveConnections?.Decrement();
#endif
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        internal void EnterFreeConnection()
        {
#if NET
            Interlocked.Increment(ref _freeConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfFreeConnections?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        internal void ExitFreeConnection()
        {
#if NET
            Interlocked.Decrement(ref _freeConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfFreeConnections?.Decrement();
#endif
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        internal void EnterStasisConnection()
        {
#if NET
            Interlocked.Increment(ref _stasisConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfStasisConnections?.Increment();
#endif
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        internal void ExitStasisConnection()
        {
#if NET
            Interlocked.Decrement(ref _stasisConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfStasisConnections?.Decrement();
#endif
        }

        /// <summary>
        ///  The number of connections we reclaim from GC'd external connections
        /// </summary>
        internal void ReclaimedConnectionRequest()
        {
#if NET
            Interlocked.Increment(ref _reclaimedConnectionsCounter);
#endif
#if NETFRAMEWORK
            _numberOfReclaimedConnections?.Increment();
#endif
        }
    }
}
