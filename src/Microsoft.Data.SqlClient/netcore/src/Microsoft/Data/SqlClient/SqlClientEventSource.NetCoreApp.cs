// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// supported frameworks: .Net core 3.1 and .Net standard 2.1 and above
    /// </summary>
    internal partial class SqlClientEventSource : SqlClientEventSourceBase
    {
        private PollingCounter _activeHardConnections;
        private IncrementingPollingCounter _hardConnectsPerSecond;
        private IncrementingPollingCounter _hardDisconnectsPerSecond;

        private PollingCounter _activeSoftConnections;
        private IncrementingPollingCounter _softConnects;
        private IncrementingPollingCounter _softDisconnects;

        private PollingCounter _numberOfNonPooledConnections;
        private PollingCounter _numberOfPooledConnections;

        private PollingCounter _numberOfActiveConnectionPoolGroups;
        private PollingCounter _numberOfInactiveConnectionPoolGroups;

        private PollingCounter _numberOfActiveConnectionPools;
        private PollingCounter _numberOfInactiveConnectionPools;

        private PollingCounter _numberOfActiveConnections;
        private PollingCounter _numberOfFreeConnections;
        private PollingCounter _numberOfStasisConnections;
        private IncrementingPollingCounter _numberOfReclaimedConnections;

        private long _activeHardConnectionsCounter = 0;
        private long _hardConnectsCounter = 0;
        private long _hardDisconnectsCounter = 0;

        private long _activeSoftConnectionsCounter = 0;
        private long _softConnectsCounter = 0;
        private long _softDisconnectsCounter = 0;

        private long _nonPooledConnectionsCounter = 0;
        private long _pooledConnectionsCounter = 0;

        private long _activeConnectionPoolGroupsCounter = 0;
        private long _inactiveConnectionPoolGroupsCounter = 0;

        private long _activeConnectionPoolsCounter = 0;
        private long _inactiveConnectionPoolsCounter = 0;

        private long _activeConnectionsCounter = 0;
        private long _freeConnectionsCounter = 0;
        private long _stasisConnectionsCounter = 0;
        private long _reclaimedConnectionsCounter = 0;

        protected override void EventCommandMethodCall(EventCommandEventArgs command)
        {
            if(command.Command != EventCommand.Enable)
            {
                return;
            }

            _activeHardConnections = _activeHardConnections ??
             new PollingCounter("active-hard-connections", this, () => _activeHardConnectionsCounter)
             {
                 DisplayName = "Actual active connections are made to servers",
                 DisplayUnits = "count"
             };

            _hardConnectsPerSecond = _hardConnectsPerSecond ??
                new IncrementingPollingCounter("hard-connects", this, () => _hardConnectsCounter)
                {
                    DisplayName = "Actual connections are made to servers",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _hardDisconnectsPerSecond = _hardDisconnectsPerSecond ??
                new IncrementingPollingCounter("hard-disconnects", this, () => _hardDisconnectsCounter)
                {
                    DisplayName = "Actual disconnections are made to servers",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _activeSoftConnections = _activeSoftConnections ??
                new PollingCounter("active-soft-connects", this, () => _activeSoftConnectionsCounter)
                {
                    DisplayName = "Active connections got from connection pool",
                    DisplayUnits = "count"
                };

            _softConnects = _softConnects ??
                new IncrementingPollingCounter("soft-connects", this, () => _softConnectsCounter)
                {
                    DisplayName = "Connections got from connection pool",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _softDisconnects = _softDisconnects ??
                new IncrementingPollingCounter("soft-disconnects", this, () => _softDisconnectsCounter)
                {
                    DisplayName = "Connections returned to the connection pool",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _numberOfNonPooledConnections = _numberOfNonPooledConnections ??
                new PollingCounter("number-of-non-pooled-connections", this, () => _nonPooledConnectionsCounter)
                {
                    DisplayName = "Number of connections are not using connection pooling",
                    DisplayUnits = "count / sec"
                };

            _numberOfPooledConnections = _numberOfPooledConnections ??
                new PollingCounter("number-of-pooled-connections", this, () => _pooledConnectionsCounter)
                {
                    DisplayName = "Number of connections are managed by connection pooler",
                    DisplayUnits = "count / sec"
                };

            _numberOfActiveConnectionPoolGroups = _numberOfActiveConnectionPoolGroups ??
                new PollingCounter("number-of-active-connection-pool-groups", this, () => _activeConnectionPoolGroupsCounter)
                {
                    DisplayName = "Number of active unique connection strings",
                    DisplayUnits = "count"
                };

            _numberOfInactiveConnectionPoolGroups = _numberOfInactiveConnectionPoolGroups ??
                new PollingCounter("number-of-inactive-connection-pool-groups", this, () => _inactiveConnectionPoolGroupsCounter)
                {
                    DisplayName = "Number of unique connection strings waiting for pruning",
                    DisplayUnits = "count"
                };

            _numberOfActiveConnectionPools = _numberOfActiveConnectionPools ??
                new PollingCounter("number-of-active-connection-pools", this, () => _activeConnectionPoolsCounter)
                {
                    DisplayName = "Number of active connection pools",
                    DisplayUnits = "count"
                };

            _numberOfInactiveConnectionPools = _numberOfInactiveConnectionPools ??
                new PollingCounter("number-of-inactive-connection-pools", this, () => _inactiveConnectionPoolsCounter)
                {
                    DisplayName = "Number of inactive connection pools",
                    DisplayUnits = "count"
                };

            _numberOfActiveConnections = _numberOfActiveConnections ??
                new PollingCounter("number-of-active-connections", this, () => _activeConnectionsCounter)
                {
                    DisplayName = "Number of active connections",
                    DisplayUnits = "count"
                };

            _numberOfFreeConnections = _numberOfFreeConnections ??
                new PollingCounter("number-of-free-connections", this, () => _freeConnectionsCounter)
                {
                    DisplayName = "Number of free-ready connections",
                    DisplayUnits = "count"
                };

            _numberOfStasisConnections = _numberOfStasisConnections ??
                new PollingCounter("number-of-stasis-connections", this, () => _stasisConnectionsCounter)
                {
                    DisplayName = "Number of connections currently waiting to be ready",
                    DisplayUnits = "count"
                };

            _numberOfReclaimedConnections = _numberOfReclaimedConnections ??
                new IncrementingPollingCounter("number-of-reclaimed-connections", this, () => _reclaimedConnectionsCounter)
                {
                    DisplayName = "Number of reclaimed connections from GC",
                    DisplayUnits = "count",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };
        }

        /// <summary>
        /// The number of actual connections that are being made to servers
        /// </summary>
        [NonEvent]
        internal override void HardConnectRequest()
        {
            Interlocked.Increment(ref _activeHardConnectionsCounter);
            Interlocked.Increment(ref _hardConnectsCounter);
        }

        /// <summary>
        /// The number of actual disconnects that are being made to servers
        /// </summary>
        [NonEvent]
        internal override void HardDisconnectRequest()
        {
            Interlocked.Decrement(ref _activeHardConnectionsCounter);
            Interlocked.Increment(ref _hardDisconnectsCounter);
        }

        /// <summary>
        /// The number of connections we get from the pool
        /// </summary>
        [NonEvent]
        internal override void SoftConnectRequest()
        {
            Interlocked.Increment(ref _activeSoftConnectionsCounter);
            Interlocked.Increment(ref _softConnectsCounter);
        }

        /// <summary>
        /// The number of connections we return to the pool
        /// </summary>
        [NonEvent]
        internal override void SoftDisconnectRequest()
        {
            Interlocked.Decrement(ref _activeSoftConnectionsCounter);
            Interlocked.Increment(ref _softDisconnectsCounter);
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        [NonEvent]
        internal override void EnterNonPooledConnection()
        {
            Interlocked.Increment(ref _nonPooledConnectionsCounter);
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        [NonEvent]
        internal override void ExitNonPooledConnection()
        {
            Interlocked.Decrement(ref _nonPooledConnectionsCounter);
        }

        /// <summary>
        /// The number of connections that are managed by the connection pooler
        /// </summary>
        [NonEvent]
        internal override void EnterPooledConnection()
        {
            Interlocked.Increment(ref _pooledConnectionsCounter);
        }

        /// <summary>
        /// The number of connections that are managed by the connection pooler
        /// </summary>
        [NonEvent]
        internal override void ExitPooledConnection()
        {
            Interlocked.Decrement(ref _pooledConnectionsCounter);
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        [NonEvent]
        internal override void EnterActiveConnectionPoolGroup()
        {
            Interlocked.Increment(ref _activeConnectionPoolGroupsCounter);
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        [NonEvent]
        internal override void ExitActiveConnectionPoolGroup()
        {
            Interlocked.Decrement(ref _activeConnectionPoolGroupsCounter);
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        [NonEvent]
        internal override void EnterInactiveConnectionPoolGroup()
        {
            Interlocked.Increment(ref _inactiveConnectionPoolGroupsCounter);
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        [NonEvent]
        internal override void ExitInactiveConnectionPoolGroup()
        {
            Interlocked.Decrement(ref _inactiveConnectionPoolGroupsCounter);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        [NonEvent]
        internal override void EnterActiveConnectionPool()
        {
            Interlocked.Increment(ref _activeConnectionPoolsCounter);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        [NonEvent]
        internal override void ExitActiveConnectionPool()
        {
            Interlocked.Decrement(ref _activeConnectionPoolsCounter);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        [NonEvent]
        internal override void EnterInactiveConnectionPool()
        {
            Interlocked.Increment(ref _inactiveConnectionPoolsCounter);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        [NonEvent]
        internal override void ExitInactiveConnectionPool()
        {
            Interlocked.Decrement(ref _inactiveConnectionPoolsCounter);
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        [NonEvent]
        internal override void EnterActiveConnection()
        {
            Interlocked.Increment(ref _activeConnectionsCounter);
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        [NonEvent]
        internal override void ExitActiveConnection()
        {
            Interlocked.Decrement(ref _activeConnectionsCounter);
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        [NonEvent]
        internal override void EnterFreeConnection()
        {
            Interlocked.Increment(ref _freeConnectionsCounter);
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        [NonEvent]
        internal override void ExitFreeConnection()
        {
            Interlocked.Decrement(ref _freeConnectionsCounter);
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        [NonEvent]
        internal override void EnterStasisConnection()
        {
            Interlocked.Increment(ref _stasisConnectionsCounter);
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        [NonEvent]
        internal override void ExitStasisConnection()
        {
            Interlocked.Decrement(ref _stasisConnectionsCounter);
        }

        /// <summary>
        ///  The number of connections we reclaim from GC'd external connections
        /// </summary>
        [NonEvent]
        internal override void ReclaimedConnectionRequest()
        {
            Interlocked.Increment(ref _reclaimedConnectionsCounter);
        }
    }
}
