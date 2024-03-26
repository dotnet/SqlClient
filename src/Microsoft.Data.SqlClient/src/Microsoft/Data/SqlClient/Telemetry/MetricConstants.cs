// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Telemetry
{
    internal static class MetricNames
    {
        // Metric names prefixed with "db.client." are documented at
        // https://opentelemetry.io/docs/specs/semconv/database/database-metrics/
        private const string StandardsPrefix = "db.client.";
        private const string LibrarySpecificPrefix = "sqlclient.db.client.";

        public static class Connections
        {
            private const string StandardsPrefix = MetricNames.StandardsPrefix + "connections.";
            private const string LibrarySpecificPrefix = MetricNames.LibrarySpecificPrefix + "connections.";

            public const string Usage = StandardsPrefix + "usage";

            public const string MaxIdle = StandardsPrefix + "idle.max";

            public const string MinIdle = StandardsPrefix + "idle.min";

            public const string Max = StandardsPrefix + "max";

            public const string PendingRequests = StandardsPrefix + "pending_requests";

            public const string Timeouts = StandardsPrefix + "timeouts";

            public const string CreationTime = StandardsPrefix + "create_time";

            public const string WaitTime = StandardsPrefix + "wait_time";

            public const string UsageTime = StandardsPrefix + "use_time";

            public const string HardUsage = LibrarySpecificPrefix + "hard.usage";

            public const string Connects = LibrarySpecificPrefix + "connects";

            public const string Disconnects = LibrarySpecificPrefix + "disconnects";

            public const string ServerCommandTime = LibrarySpecificPrefix + "server_command_time";

            public const string NetworkWaitTime = LibrarySpecificPrefix + "network_wait_time";

            public const string ByteIo = LibrarySpecificPrefix + "io";

            public const string BuffersIo = LibrarySpecificPrefix + "buffers";

            public const string HardTotal = LibrarySpecificPrefix + "hard.total";

            public const string SoftTotal = LibrarySpecificPrefix + "soft.total";
        }

        public static class ConnectionPools
        {
            private const string LibrarySpecificPrefix = MetricNames.LibrarySpecificPrefix + "connection_pools.";

            public const string Usage = LibrarySpecificPrefix + "usage";
        }

        public static class ConnectionPoolGroups
        {
            private const string LibrarySpecificPrefix = MetricNames.LibrarySpecificPrefix + "connection_pool_groups.";

            public const string Usage = LibrarySpecificPrefix + "usage";
        }

        public static class Commands
        {
            private const string LibrarySpecificPrefix = MetricNames.LibrarySpecificPrefix + "commands.";

            public const string Failed = LibrarySpecificPrefix + ".failed";

            public const string UsageTime = LibrarySpecificPrefix + "use_time";

            public const string PreparedRatio = LibrarySpecificPrefix + "prepared_ratio";

            public const string Total = LibrarySpecificPrefix + "total";
        }

        public static class Transactions
        {
            private const string LibrarySpecificPrefix = MetricNames.LibrarySpecificPrefix + "transactions.";

            public const string Committed = LibrarySpecificPrefix + ".committed";

            public const string Active = LibrarySpecificPrefix + ".active";

            public const string RolledBack = LibrarySpecificPrefix + ".rolled_back";

            public const string Total = LibrarySpecificPrefix + "total";

            public const string CommitTime = LibrarySpecificPrefix + "commit_time";

            public const string RollbackTime = LibrarySpecificPrefix + "rollback_time";

            public const string ActiveTime = LibrarySpecificPrefix + "active_time";
        }
    }

    internal static class MetricTagNames
    {
        public const string PoolName = "pool.name";

        public const string State = "state";

        public const string Direction = "direction";

        public const string Type = "type";
    }

    internal static class MetricTagValues
    {
        public const string IdleState = "idle";

        public const string ActiveState = "active";

        public const string StasisState = "stasis";

        public const string ReclaimedState = "reclaimed";

        public const string TransmitDirection = "transmit";

        public const string ReceiveDirection = "receive";

        public const string PooledConnectionType = "pooled";

        public const string NonPooledConnectionType = "unpooled";

        public const string HardConnectionType = "hard";

        public const string SoftConnectionType = "soft";

        public const string HardActionType = "hard";

        public const string SoftActionType = "soft";
    }

    internal static class MetricUnits
    {
        public const string Connection = "{connection}";

        public const string ConnectionPool = "{connection_pool}";

        public const string ConnectionPoolGroup = "{connection_pool_group}";

        public const string Command = "{command}";

        public const string Transaction = "{transaction}";

        public const string Request = "{request}";

        public const string Timeout = "{timeout}";

        public const string Connect = "{connect}";

        public const string Disconnect = "{disconnect}";

        public const string Buffer = "{buffer}";

        public const string Millisecond = "ms";

        public const string Byte = "By";
    }
}
