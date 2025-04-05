// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;

#nullable enable

namespace Microsoft.Data.SqlClient.Diagnostics
{
    internal sealed partial class SqlClientMetrics
    {
        private PollingCounter? _activeHardConnections;
        private IncrementingPollingCounter? _hardConnectsPerSecond;
        private IncrementingPollingCounter? _hardDisconnectsPerSecond;

        private PollingCounter? _activeSoftConnections;
        private IncrementingPollingCounter? _softConnects;
        private IncrementingPollingCounter? _softDisconnects;

        private PollingCounter? _numberOfNonPooledConnections;
        private PollingCounter? _numberOfPooledConnections;

        private PollingCounter? _numberOfActiveConnectionPoolGroups;
        private PollingCounter? _numberOfInactiveConnectionPoolGroups;

        private PollingCounter? _numberOfActiveConnectionPools;
        private PollingCounter? _numberOfInactiveConnectionPools;

        private PollingCounter? _numberOfActiveConnections;
        private PollingCounter? _numberOfFreeConnections;
        private PollingCounter? _numberOfStasisConnections;
        private IncrementingPollingCounter? _numberOfReclaimedConnections;

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

        private void EnableEventCounters()
        {
            _activeHardConnections = _activeHardConnections ??
                 new PollingCounter("active-hard-connections", _eventSource, () => _activeHardConnectionsCounter)
                 {
                     DisplayName = "Actual active connections currently made to servers",
                     DisplayUnits = "count"
                 };

            _hardConnectsPerSecond = _hardConnectsPerSecond ??
                new IncrementingPollingCounter("hard-connects", _eventSource, () => _hardConnectsCounter)
                {
                    DisplayName = "Actual connection rate to servers",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _hardDisconnectsPerSecond = _hardDisconnectsPerSecond ??
                new IncrementingPollingCounter("hard-disconnects", _eventSource, () => _hardDisconnectsCounter)
                {
                    DisplayName = "Actual disconnection rate from servers",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _activeSoftConnections = _activeSoftConnections ??
                new PollingCounter("active-soft-connects", _eventSource, () => _activeSoftConnectionsCounter)
                {
                    DisplayName = "Active connections retrieved from the connection pool",
                    DisplayUnits = "count"
                };

            _softConnects = _softConnects ??
                new IncrementingPollingCounter("soft-connects", _eventSource, () => _softConnectsCounter)
                {
                    DisplayName = "Rate of connections retrieved from the connection pool",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _softDisconnects = _softDisconnects ??
                new IncrementingPollingCounter("soft-disconnects", _eventSource, () => _softDisconnectsCounter)
                {
                    DisplayName = "Rate of connections returned to the connection pool",
                    DisplayUnits = "count / sec",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

            _numberOfNonPooledConnections = _numberOfNonPooledConnections ??
                new PollingCounter("number-of-non-pooled-connections", _eventSource, () => _nonPooledConnectionsCounter)
                {
                    DisplayName = "Number of connections not using connection pooling",
                    DisplayUnits = "count"
                };

            _numberOfPooledConnections = _numberOfPooledConnections ??
                new PollingCounter("number-of-pooled-connections", _eventSource, () => _pooledConnectionsCounter)
                {
                    DisplayName = "Number of connections managed by the connection pool",
                    DisplayUnits = "count"
                };

            _numberOfActiveConnectionPoolGroups = _numberOfActiveConnectionPoolGroups ??
                new PollingCounter("number-of-active-connection-pool-groups", _eventSource, () => _activeConnectionPoolGroupsCounter)
                {
                    DisplayName = "Number of active unique connection strings",
                    DisplayUnits = "count"
                };

            _numberOfInactiveConnectionPoolGroups = _numberOfInactiveConnectionPoolGroups ??
                new PollingCounter("number-of-inactive-connection-pool-groups", _eventSource, () => _inactiveConnectionPoolGroupsCounter)
                {
                    DisplayName = "Number of unique connection strings waiting for pruning",
                    DisplayUnits = "count"
                };

            _numberOfActiveConnectionPools = _numberOfActiveConnectionPools ??
                new PollingCounter("number-of-active-connection-pools", _eventSource, () => _activeConnectionPoolsCounter)
                {
                    DisplayName = "Number of active connection pools",
                    DisplayUnits = "count"
                };

            _numberOfInactiveConnectionPools = _numberOfInactiveConnectionPools ??
                new PollingCounter("number-of-inactive-connection-pools", _eventSource, () => _inactiveConnectionPoolsCounter)
                {
                    DisplayName = "Number of inactive connection pools",
                    DisplayUnits = "count"
                };

            _numberOfActiveConnections = _numberOfActiveConnections ??
                new PollingCounter("number-of-active-connections", _eventSource, () => _activeConnectionsCounter)
                {
                    DisplayName = "Number of active connections",
                    DisplayUnits = "count"
                };

            _numberOfFreeConnections = _numberOfFreeConnections ??
                new PollingCounter("number-of-free-connections", _eventSource, () => _freeConnectionsCounter)
                {
                    DisplayName = "Number of ready connections in the connection pool",
                    DisplayUnits = "count"
                };

            _numberOfStasisConnections = _numberOfStasisConnections ??
                new PollingCounter("number-of-stasis-connections", _eventSource, () => _stasisConnectionsCounter)
                {
                    DisplayName = "Number of connections currently waiting to be ready",
                    DisplayUnits = "count"
                };

            _numberOfReclaimedConnections = _numberOfReclaimedConnections ??
                new IncrementingPollingCounter("number-of-reclaimed-connections", _eventSource, () => _reclaimedConnectionsCounter)
                {
                    DisplayName = "Number of reclaimed connections from GC",
                    DisplayUnits = "count",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };
        }
    }
}
