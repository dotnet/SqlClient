// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlClientEventSource : EventSource
    {
        private EventCounter _activeHardConnections;
        private EventCounter _hardConnectsPerSecond;
        private EventCounter _hardDisconnectsPerSecond;

        private EventCounter _activeSoftConnections;
        private EventCounter _softConnects;
        private EventCounter _softDisconnects;

        private EventCounter _numberOfNonPooledConnections;
        private EventCounter _numberOfPooledConnections;

        private EventCounter _numberOfActiveConnectionPoolGroups;
        private EventCounter _numberOfInactiveConnectionPoolGroups;

        private EventCounter _numberOfActiveConnectionPools;
        private EventCounter _numberOfInactiveConnectionPools;

        private EventCounter _numberOfActiveConnections;
        private EventCounter _numberOfFreeConnections;
        private EventCounter _numberOfStasisConnections;
        private EventCounter _numberOfReclaimedConnections;

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

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command != EventCommand.Enable)
            {
                return;
            }

            _activeHardConnections = _activeHardConnections ??
                new EventCounter("active-hard-connections", this)
                {
#if NETCORE3
                    DisplayName = "Actual active connections are made to servers",
                    DisplayUnits = "count"
#endif
                };

            _hardConnectsPerSecond = _hardConnectsPerSecond ??
                new EventCounter("hard-connects", this)
                {
#if NETCORE3
                    DisplayName = "Actual connections are made to servers",
                    DisplayUnits = "count / sec"
#endif
                };

            _hardDisconnectsPerSecond = _hardDisconnectsPerSecond ??
                new EventCounter("hard-disconnects", this)
                {
#if NETCORE3
                    DisplayName = "Actual disconnections are made to servers",
                    DisplayUnits = "count / sec"
#endif
                };

            _activeSoftConnections = _activeSoftConnections ??
                new EventCounter("active-soft-connects", this)
                {
#if NETCORE3
                    DisplayName = "Active connections got from connection pool",
                    DisplayUnits = "count"
#endif
                };

            _softConnects = _softConnects ??
                new EventCounter("soft-connects", this)
                {
#if NETCORE3
                    DisplayName = "Connections got from connection pool",
                    DisplayUnits = "count / sec"
#endif
                };

            _softDisconnects = _softDisconnects ??
                new EventCounter("soft-disconnects", this)
                {
#if NETCORE3
                    DisplayName = "Connections returned to the connection pool",
                    DisplayUnits = "count / sec"
#endif
                };

            _numberOfNonPooledConnections = _numberOfNonPooledConnections ??
                new EventCounter("number-of-non-pooled-connections", this)
                {
#if NETCORE3
                    DisplayName = "Number of connections are not using connection pooling",
                    DisplayUnits = "count / sec"
#endif
                };

            _numberOfPooledConnections = _numberOfPooledConnections ??
                new EventCounter("number-of-pooled-connections", this)
                {
#if NETCORE3
                    DisplayName = "Number of connections are managed by connection pooler",
                    DisplayUnits = "count / sec"
#endif
                };

            _numberOfActiveConnectionPoolGroups = _numberOfActiveConnectionPoolGroups ??
                new EventCounter("number-of-active-connection-pool-groups", this)
                {
#if NETCORE3
                    DisplayName = "Number of active unique connection strings",
                    DisplayUnits = "count"
#endif
                };

            _numberOfInactiveConnectionPoolGroups = _numberOfInactiveConnectionPoolGroups ??
                new EventCounter("number-of-inactive-connection-pool-groups", this)
                {
#if NETCORE3
                    DisplayName = "Number of unique connection strings waiting for pruning",
                    DisplayUnits = "count"
#endif
                };

            _numberOfActiveConnectionPools = _numberOfActiveConnectionPools ??
                new EventCounter("number-of-active-connection-pools", this)
                {
#if NETCORE3
                    DisplayName = "Number of active connection pools",
                    DisplayUnits = "count"
#endif
                };

            _numberOfInactiveConnectionPools = _numberOfInactiveConnectionPools ??
                new EventCounter("number-of-inactive-connection-pools", this)
                {
#if NETCORE3
                    DisplayName = "Number of inactive connection pools",
                    DisplayUnits = "count"
#endif
                };

            _numberOfActiveConnections = _numberOfActiveConnections ??
                new EventCounter("number-of-active-connections", this)
                {
#if NETCORE3
                    DisplayName = "Number of active connections",
                    DisplayUnits = "count"
#endif
                };

            _numberOfFreeConnections = _numberOfFreeConnections ??
                new EventCounter("number-of-free-connections", this)
                {
#if NETCORE3
                    DisplayName = "Number of free-ready connections",
                    DisplayUnits = "count"
#endif
                };

            _numberOfStasisConnections = _numberOfStasisConnections ??
                new EventCounter("number-of-stasis-connections", this)
                {
#if NETCORE3
                    DisplayName = "Number of connections currently waiting to be ready",
                    DisplayUnits = "count"
#endif
                };

            _numberOfReclaimedConnections = _numberOfReclaimedConnections ??
                new EventCounter("number-of-reclaimed-connections", this)
                {
#if NETCORE3
                    DisplayName = "Number of reclaimed connections from GC",
                    DisplayUnits = "count"
#endif
                };
        }

        /// <summary>
        /// The number of actual connections that are being made to servers
        /// </summary>
        [NonEvent]
        internal void HardConnectRequest()
        {
            if (IsEnabled())
            {
                var counter = Interlocked.Increment(ref _activeHardConnectionsCounter);
                _activeHardConnections.WriteMetric(counter);

                counter = Interlocked.Increment(ref _hardConnectsCounter);
                _hardConnectsPerSecond.WriteMetric(counter);
            }
        }

        /// <summary>
        /// The number of actual disconnects that are being made to servers
        /// </summary>
        [NonEvent]
        internal void HardDisconnectRequest()
        {
            if (IsEnabled())
            {
                var counter = Interlocked.Decrement(ref _activeHardConnectionsCounter);
                _activeHardConnections.WriteMetric(counter);

                counter = Interlocked.Increment(ref _hardDisconnectsCounter);
                _hardDisconnectsPerSecond.WriteMetric(counter);
            }
        }

        /// <summary>
        /// The number of connections we get from the pool
        /// </summary>
        [NonEvent]
        internal void SoftConnectRequest()
        {
            if (IsEnabled())
            {
                var counter = Interlocked.Increment(ref _activeSoftConnectionsCounter);
                _activeSoftConnections.WriteMetric(counter);

                counter = Interlocked.Increment(ref _softConnectsCounter);
                _softConnects.WriteMetric(counter);
            }
        }

        /// <summary>
        /// The number of connections we return to the pool
        /// </summary>
        [NonEvent]
        internal void SoftDisconnectRequest()
        {
            if (IsEnabled())
            {
                var counter = Interlocked.Decrement(ref _activeSoftConnectionsCounter);
                _activeSoftConnections.WriteMetric(counter);

                counter = Interlocked.Increment(ref _softDisconnectsCounter);
                _softDisconnects.WriteMetric(counter);
            }
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void NonPooledConnectionRequest(bool increment = true)
        {
            Request(ref _numberOfNonPooledConnections, ref _nonPooledConnectionsCounter, increment);
        }

        /// <summary>
        /// The number of connections that are managed by the connection pooler
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void PooledConnectionRequest(bool increment = true)
        {
            Request(ref _numberOfPooledConnections, ref _pooledConnectionsCounter, increment);
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void ActiveConnectionPoolGroupRequest(bool increment = true)
        {
            Request(ref _numberOfActiveConnectionPoolGroups, ref _activeConnectionPoolGroupsCounter, increment);
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void InactiveConnectionPoolGroupRequest(bool increment = true)
        {
            Request(ref _numberOfInactiveConnectionPoolGroups, ref _inactiveConnectionPoolGroupsCounter, increment);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void ActiveConnectionPoolRequest(bool increment = true)
        {
            Request(ref _numberOfActiveConnectionPools, ref _activeConnectionPoolsCounter, increment);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void InactiveConnectionPoolRequest(bool increment = true)
        {
            Request(ref _numberOfInactiveConnectionPools, ref _inactiveConnectionPoolsCounter, increment);
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void ActiveConnectionRequest(bool increment = true)
        {
            Request(ref _numberOfActiveConnections, ref _activeConnectionsCounter, increment);
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void FreeConnectionRequest(bool increment = true)
        {
            Request(ref _numberOfFreeConnections, ref _freeConnectionsCounter, increment);
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        /// <param name="increment"></param>
        [NonEvent]
        internal void StasisConnectionRequest(bool increment = true)
        {
            Request(ref _numberOfStasisConnections, ref _stasisConnectionsCounter, increment);
        }

        /// <summary>
        ///  The number of connections we reclaim from GC'd external connections
        /// </summary>
        [NonEvent]
        internal void ReclaimedConnectionRequest()
        {
            Request(ref _numberOfReclaimedConnections, ref _reclaimedConnectionsCounter, true);
        }

        [NonEvent]
        private void Request(ref EventCounter eventCounter, ref long counter, bool increment)
        {
            if (IsEnabled())
            {
                long innerCounter;
                if (increment)
                {
                    innerCounter = Interlocked.Increment(ref counter);
                }
                else
                {
                    innerCounter = Interlocked.Decrement(ref counter);
                }
                eventCounter.WriteMetric(innerCounter);
            }
        }
    }
}
