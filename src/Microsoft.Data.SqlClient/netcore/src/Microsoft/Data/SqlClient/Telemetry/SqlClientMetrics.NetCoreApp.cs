// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Telemetry
{
    internal sealed partial class SqlClientMetrics
    {
#if NETSTANDARD2_0
        private void InitializePlatformSpecificMetrics() { }

        private void IncrementPlatformSpecificMetric(string metricName, in TagList tagList) { }

        private void DecrementPlatformSpecificMetric(string metricName, in TagList tagList) { }

        private void DisposePlatformSpecificMetrics() { }
#else

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

        // This counter is simply used as a placeholder - GetPlatformSpecificMetric has to return a ref to something.
        // It should always be zero.
        private long _watchdogCounter = 0;

        private void InitializePlatformSpecificMetrics()
        {
            _activeHardConnections = new PollingCounter("active-hard-connections", SqlClientEventSource.Log, () => _activeHardConnectionsCounter)
            {
                DisplayName = "Actual active connections currently made to servers",
                DisplayUnits = "count"
            };

            _hardConnectsPerSecond = new IncrementingPollingCounter("hard-connects", SqlClientEventSource.Log, () => _hardConnectsCounter)
            {
                DisplayName = "Actual connection rate to servers",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _hardDisconnectsPerSecond = new IncrementingPollingCounter("hard-disconnects", SqlClientEventSource.Log, () => _hardDisconnectsCounter)
            {
                DisplayName = "Actual disconnection rate from servers",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _activeSoftConnections = new PollingCounter("active-soft-connects", SqlClientEventSource.Log, () => _activeSoftConnectionsCounter)
            {
                DisplayName = "Active connections retrieved from the connection pool",
                DisplayUnits = "count"
            };

            _softConnects = new IncrementingPollingCounter("soft-connects", SqlClientEventSource.Log, () => _softConnectsCounter)
            {
                DisplayName = "Rate of connections retrieved from the connection pool",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _softDisconnects = new IncrementingPollingCounter("soft-disconnects", SqlClientEventSource.Log, () => _softDisconnectsCounter)
            {
                DisplayName = "Rate of connections returned to the connection pool",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _numberOfNonPooledConnections = new PollingCounter("number-of-non-pooled-connections", SqlClientEventSource.Log, () => _nonPooledConnectionsCounter)
            {
                DisplayName = "Number of connections not using connection pooling",
                DisplayUnits = "count"
            };

            _numberOfPooledConnections = new PollingCounter("number-of-pooled-connections", SqlClientEventSource.Log, () => _pooledConnectionsCounter)
            {
                DisplayName = "Number of connections managed by the connection pool",
                DisplayUnits = "count"
            };

            _numberOfActiveConnectionPoolGroups = new PollingCounter("number-of-active-connection-pool-groups", SqlClientEventSource.Log, () => _activeConnectionPoolGroupsCounter)
            {
                DisplayName = "Number of active unique connection strings",
                DisplayUnits = "count"
            };

            _numberOfInactiveConnectionPoolGroups = new PollingCounter("number-of-inactive-connection-pool-groups", SqlClientEventSource.Log, () => _inactiveConnectionPoolGroupsCounter)
            {
                DisplayName = "Number of unique connection strings waiting for pruning",
                DisplayUnits = "count"
            };

            _numberOfActiveConnectionPools = new PollingCounter("number-of-active-connection-pools", SqlClientEventSource.Log, () => _activeConnectionPoolsCounter)
            {
                DisplayName = "Number of active connection pools",
                DisplayUnits = "count"
            };

            _numberOfInactiveConnectionPools = new PollingCounter("number-of-inactive-connection-pools", SqlClientEventSource.Log, () => _inactiveConnectionPoolsCounter)
            {
                DisplayName = "Number of inactive connection pools",
                DisplayUnits = "count"
            };

            _numberOfActiveConnections = new PollingCounter("number-of-active-connections", SqlClientEventSource.Log, () => _activeConnectionsCounter)
            {
                DisplayName = "Number of active connections",
                DisplayUnits = "count"
            };

            _numberOfFreeConnections = new PollingCounter("number-of-free-connections", SqlClientEventSource.Log, () => _freeConnectionsCounter)
            {
                DisplayName = "Number of ready connections in the connection pool",
                DisplayUnits = "count"
            };

            _numberOfStasisConnections = new PollingCounter("number-of-stasis-connections", SqlClientEventSource.Log, () => _stasisConnectionsCounter)
            {
                DisplayName = "Number of connections currently waiting to be ready",
                DisplayUnits = "count"
            };

            _numberOfReclaimedConnections = new IncrementingPollingCounter("number-of-reclaimed-connections", SqlClientEventSource.Log, () => _reclaimedConnectionsCounter)
            {
                DisplayName = "Number of reclaimed connections from GC",
                DisplayUnits = "count",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };
        }

        private KeyValuePair<string, object> GetTagByName(string tagName, int likelyIndex, in TagList tagList)
        {
            KeyValuePair<string, object> tagValue;

            // We have control over the initial tag list, so in almost every circumstance this shortcut will be used.
            // It spares us from a loop, however small.
            // In most cases, index 0 is the connection pool name.
            if (likelyIndex > 0 && likelyIndex < tagList.Count)
            {
                tagValue = tagList[likelyIndex];

                if (string.Equals(tagValue.Key, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return tagValue;
                }
            }

            for(int i = 0; i < tagList.Count; i++)
            {
                tagValue = tagList[i];

                if (string.Equals(tagValue.Key, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return tagValue;
                }
            }

            throw ADP.CollectionIndexString(typeof(KeyValuePair<string, object>), nameof(KeyValuePair<string, object>.Key), tagName, typeof(TagList));
        }

        private ref long GetPlatformSpecificMetric(string metricName, in TagList tagList, out bool successful)
        {
            KeyValuePair<string, object> associatedTag;

            successful = true;

            switch (metricName)
            {
                case MetricNames.Connections.Usage:
                    associatedTag = GetTagByName(MetricTagNames.State, 1, in tagList);

                    if (string.Equals((string)associatedTag.Value, MetricTagValues.ActiveState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _activeConnectionsCounter;
                    }
                    else if(string.Equals((string)associatedTag.Value, MetricTagValues.IdleState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _freeConnectionsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.StasisState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _stasisConnectionsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.ReclaimedState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _reclaimedConnectionsCounter;
                    }
                    break;
                case MetricNames.ConnectionPoolGroups.Usage:
                    associatedTag = GetTagByName(MetricTagNames.State, 1, in tagList);

                    if (string.Equals((string)associatedTag.Value, MetricTagValues.ActiveState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _activeConnectionPoolGroupsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.IdleState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _inactiveConnectionPoolGroupsCounter;
                    }
                    break;
                case MetricNames.ConnectionPools.Usage:
                    associatedTag = GetTagByName(MetricTagNames.State, 1, in tagList);

                    if (string.Equals((string)associatedTag.Value, MetricTagValues.ActiveState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _activeConnectionPoolsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.IdleState, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _inactiveConnectionPoolsCounter;
                    }
                    break;
                case MetricNames.Connections.HardUsage:
                    associatedTag = GetTagByName(MetricTagNames.Type, 1, in tagList);

                    if (string.Equals((string)associatedTag.Value, MetricTagValues.PooledConnectionType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _pooledConnectionsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.NonPooledConnectionType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _nonPooledConnectionsCounter;
                    }
                    break;
                case MetricNames.Connections.Connects:
                    associatedTag = GetTagByName(MetricTagNames.Type, 1, in tagList);

                    if (string.Equals((string)associatedTag.Value, MetricTagValues.HardActionType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _hardConnectsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.SoftActionType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _softConnectsCounter;
                    }
                    break;
                case MetricNames.Connections.Disconnects:
                    associatedTag = GetTagByName(MetricTagNames.Type, 1, in tagList);

                    if (string.Equals((string)associatedTag.Value, MetricTagValues.HardActionType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _hardDisconnectsCounter;
                    }
                    else if (string.Equals((string)associatedTag.Value, MetricTagValues.SoftActionType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ref _softDisconnectsCounter;
                    }
                    break;
                case MetricNames.Connections.HardTotal:
                    return ref _activeHardConnectionsCounter;
                case MetricNames.Connections.SoftTotal:
                    return ref _activeSoftConnectionsCounter;
            }

            successful = false;
            return ref _watchdogCounter;
        }

        private void IncrementPlatformSpecificMetric(string metricName, in TagList tagList)
        {
            ref long counterPointer = ref GetPlatformSpecificMetric(metricName, in tagList, out bool successful);

            if (successful)
            {
                Interlocked.Increment(ref counterPointer);
            }
        }

        private void DecrementPlatformSpecificMetric(string metricName, in TagList tagList)
        {
            ref long counterPointer = ref GetPlatformSpecificMetric(metricName, in tagList, out bool successful);

            if (successful)
            {
                Interlocked.Decrement(ref counterPointer);
            }
        }

        private void DisposePlatformSpecificMetrics()
        {
            _activeHardConnections?.Dispose();
            _activeHardConnections = null;

            _hardConnectsPerSecond?.Dispose();
            _hardConnectsPerSecond = null;

            _hardDisconnectsPerSecond?.Dispose();
            _hardDisconnectsPerSecond = null;

            _activeSoftConnections?.Dispose();
            _activeSoftConnections = null;

            _softConnects?.Dispose();
            _softConnects = null;

            _softDisconnects?.Dispose();
            _softDisconnects = null;

            _numberOfNonPooledConnections?.Dispose();
            _numberOfNonPooledConnections = null;

            _numberOfPooledConnections?.Dispose();
            _numberOfPooledConnections = null;

            _numberOfActiveConnectionPoolGroups?.Dispose();
            _numberOfActiveConnectionPoolGroups = null;

            _numberOfInactiveConnectionPoolGroups?.Dispose();
            _numberOfInactiveConnectionPoolGroups = null;

            _numberOfActiveConnectionPools?.Dispose();
            _numberOfActiveConnectionPools = null;

            _numberOfInactiveConnectionPools?.Dispose();
            _numberOfInactiveConnectionPools = null;

            _numberOfActiveConnections?.Dispose();
            _numberOfActiveConnections = null;

            _numberOfFreeConnections?.Dispose();
            _numberOfFreeConnections = null;

            _numberOfStasisConnections?.Dispose();
            _numberOfStasisConnections = null;

            _numberOfReclaimedConnections?.Dispose();
            _numberOfReclaimedConnections = null;
        }
#endif
    }
}
