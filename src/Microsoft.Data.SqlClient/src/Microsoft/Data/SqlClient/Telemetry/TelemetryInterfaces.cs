// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Telemetry
{
    // These are to be allocated at the connection pool level by default, and passed down to the connections
    // which come from it. If somebody specifies additional tags then it'll need to be created on a per-connection
    // basis.
    // These are marked as fields to avoid the copies which come from calling property accessors.
    internal sealed class ConnectionMetricTagListCollection
    {
        public readonly TagList GenericConnectionPoolTags;

        public readonly TagList ActiveConnectionsTags;

        public readonly TagList IdleConnectionsTags;

        public readonly TagList StasisConnectionsTags;

        public readonly TagList ReclaimedConnectionsTags;

        public readonly TagList HardConnectionsTags;

        public readonly TagList SoftConnectionsTags;

        public readonly TagList TimeoutsTags;

        public readonly TagList PendingRequestTags;

        public readonly TagList ConnectionCreationTimeTags;

        public readonly TagList ConnectionWaitTimeTags;

        public readonly TagList ConnectionUsageTimeTags;

        public readonly TagList ActiveConnectionPoolGroupsTags;

        public readonly TagList IdleConnectionPoolGroupsTags;

        public readonly TagList ActiveConnectionPoolsTags;

        public readonly TagList IdleConnectionPoolsTags;

        public readonly TagList PooledHardConnectionUsageTags;

        public readonly TagList NonPooledHardConnectionUsageTags;

        public readonly TagList HardConnectsTags;

        public readonly TagList SoftConnectsTags;

        public readonly TagList HardDisconnectsTags;

        public readonly TagList SoftDisconnectsTags;

        public ConnectionMetricTagListCollection(string poolName, params KeyValuePair<string, object>[] additionalTags)
        {
            KeyValuePair<string, object> poolNameKVP = new(MetricTagNames.PoolName, poolName);

            GenericConnectionPoolTags = new TagList() { poolNameKVP };

            ActiveConnectionsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.ActiveState } };
            IdleConnectionsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.IdleState } };
            StasisConnectionsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.StasisState } };
            ReclaimedConnectionsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.ReclaimedState } };

            HardConnectionsTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.HardConnectionType } };
            SoftConnectionsTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.SoftConnectionType } };

            TimeoutsTags = new TagList() { poolNameKVP };

            PendingRequestTags = new TagList() { poolNameKVP };

            ConnectionCreationTimeTags = new TagList() { poolNameKVP };
            ConnectionWaitTimeTags = new TagList() { poolNameKVP };
            ConnectionUsageTimeTags = new TagList() { poolNameKVP };

            ActiveConnectionPoolGroupsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.ActiveState } };
            IdleConnectionPoolGroupsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.IdleState } };

            ActiveConnectionPoolsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.ActiveState } };
            IdleConnectionPoolsTags = new TagList() { poolNameKVP, { MetricTagNames.State, MetricTagValues.IdleState } };

            PooledHardConnectionUsageTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.PooledConnectionType } };
            NonPooledHardConnectionUsageTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.NonPooledConnectionType } };

            HardConnectsTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.HardActionType } };
            SoftConnectsTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.SoftActionType } };
            HardDisconnectsTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.HardActionType } };
            SoftDisconnectsTags = new TagList() { poolNameKVP, { MetricTagNames.Type, MetricTagValues.SoftActionType } };

            foreach (KeyValuePair<string, object> additionalTag in additionalTags)
            {
                GenericConnectionPoolTags.Add(additionalTag);

                ActiveConnectionsTags.Add(additionalTag);
                IdleConnectionsTags.Add(additionalTag);
                StasisConnectionsTags.Add(additionalTag);
                ReclaimedConnectionsTags.Add(additionalTag);

                ActiveConnectionPoolGroupsTags.Add(additionalTag);
                IdleConnectionPoolGroupsTags.Add(additionalTag);

                ActiveConnectionPoolsTags.Add(additionalTag);
                IdleConnectionPoolsTags.Add(additionalTag);

                PooledHardConnectionUsageTags.Add(additionalTag);
                NonPooledHardConnectionUsageTags.Add(additionalTag);

                HardConnectsTags.Add(additionalTag);
                SoftConnectsTags.Add(additionalTag);
                HardDisconnectsTags.Add(additionalTag);
                SoftDisconnectsTags.Add(additionalTag);
            }
        }
    }

    internal interface IMetrics
    { }

    internal interface IConnectionMetrics : IMetrics
    {
        void HardConnectRequest(ConnectionMetricTagListCollection tagList);

        void HardDisconnectRequest(ConnectionMetricTagListCollection tagList);

        void SoftConnectRequest(ConnectionMetricTagListCollection tagList);

        void SoftDisconnectRequest(ConnectionMetricTagListCollection tagList);

        void Timeout(ConnectionMetricTagListCollection tagList);

        void ConnectionCreationTime(in TimeSpan creationTime, ConnectionMetricTagListCollection tagList);

        void ConnectionWaitTime(in TimeSpan waitTime, ConnectionMetricTagListCollection tagList);

        void ConnectionUsageTime(in TimeSpan usageTime, ConnectionMetricTagListCollection tagList);

        void EnterPendingRequest(ConnectionMetricTagListCollection tagList);

        void ExitPendingRequest(ConnectionMetricTagListCollection tagList);

        void EnterNonPooledConnection(ConnectionMetricTagListCollection tagList);

        void ExitNonPooledConnection(ConnectionMetricTagListCollection tagList);

        void EnterPooledConnection(ConnectionMetricTagListCollection tagList);

        void ExitPooledConnection(ConnectionMetricTagListCollection tagList);

        void EnterActiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList);

        void ExitActiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList);

        void EnterInactiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList);

        void ExitInactiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList);

        void EnterActiveConnectionPool(ConnectionMetricTagListCollection tagList);

        void ExitActiveConnectionPool(ConnectionMetricTagListCollection tagList);

        void EnterInactiveConnectionPool(ConnectionMetricTagListCollection tagList);

        void ExitInactiveConnectionPool(ConnectionMetricTagListCollection tagList);

        void EnterActiveConnection(ConnectionMetricTagListCollection tagList);

        void ExitActiveConnection(ConnectionMetricTagListCollection tagList);

        void EnterFreeConnection(ConnectionMetricTagListCollection tagList);

        void ExitFreeConnection(ConnectionMetricTagListCollection tagList);

        void EnterStasisConnection(ConnectionMetricTagListCollection tagList);

        void ExitStasisConnection(ConnectionMetricTagListCollection tagList);

        void ReclaimedConnectionRequest(ConnectionMetricTagListCollection tagList);
    }

    internal interface ITransactionMetrics : IMetrics
    {
        //
    }

    internal interface ICommandMetrics : IMetrics
    {
        //
    }
}
