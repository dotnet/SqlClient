// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Numerics;

namespace Microsoft.Data.SqlClient.Telemetry
{
    internal sealed partial class SqlClientMetrics : IDisposable, IConnectionMetrics, ICommandMetrics, ITransactionMetrics
    {
        // This version is separate to the assembly version, following semantic versioning.
        public const string MeteringVersion = "0.1.0";

        private bool _disposedValue;

        private readonly bool _enablePlatformSpecificMetrics;

        private readonly Meter _meter;
        /// <summary>
        /// Standard: db.client.connections.usage{pool.name="", state="active|idle"}
        /// Extended: db.client.connections.usage{pool.name="", state="stasis"}
        /// Extended: db.client.connections.usage{pool.name="", type="hard|soft"}
        /// </summary>
        private readonly UpDownCounter<long> _connectionUsageCounter;
        /// <summary>
        /// Standard: db.client.connections.idle.max{pool.name=""}
        /// </summary>
        private readonly ObservableCounter<long> _connectionMaxIdleCounter;
        /// <summary>
        /// Standard: db.client.connections.idle.min{pool.name=""}
        /// </summary>
        private readonly ObservableCounter<long> _connectionMinIdleCounter;
        /// <summary>
        /// Standard: db.client.connections.max{pool.name=""}
        /// </summary>
        private readonly ObservableCounter<long> _connectionMaxCounter;
        /// <summary>
        /// Standard: db.client.connections.pending_requests{pool.name=""}
        /// </summary>
        private readonly UpDownCounter<long> _connectionPendingRequestsCounter;
        /// <summary>
        /// Standard: db.client.connections.timeouts{pool.name=""}
        /// </summary>
        private readonly Counter<long> _connectionTimeoutsCounter;
        /// <summary>
        /// Standard: db.client.connections.create_time{pool.name=""}
        /// </summary>
        private readonly Histogram<double> _connectionCreationTimeHistogram;
        /// <summary>
        /// Standard: db.client.connections.wait_time{pool.name=""}
        /// </summary>
        private readonly Histogram<double> _connectionWaitTimeHistogram;
        /// <summary>
        /// Standard: db.client.connections.use_time{pool.name=""}
        /// </summary>
        private readonly Histogram<double> _connectionUsageTimeHistogram;
        /// <summary>
        /// Extended: sqlclient.db.client.connection_pools.usage{pool.name="", state="active|idle"}
        /// </summary>
        private readonly UpDownCounter<long> _connectionPoolUsageCounter;
        /// <summary>
        /// Extended: sqlclient.db.client.connection_pool_groups.usage{pool.name="", state="active|idle"}
        /// </summary>
        private readonly UpDownCounter<long> _connectionPoolGroupUsageCounter;
        /// <summary>
        /// Extended: sqlclient.db.client.connections.hard.usage{pool.name="", type="pooled|unpooled"}
        /// </summary>
        private readonly UpDownCounter<long> _connectionHardUsageCounter;
        /// <summary>
        /// Extended: sqlclient.db.client.connections.connects{pool.name="", type="hard|soft"}
        /// </summary>
        private readonly Counter<long> _connectionConnectCounter;
        /// <summary>
        /// Extended: sqlclient.db.client.connections.disconnects{pool.name="", type="hard|soft"}
        /// </summary>
        private readonly Counter<long> _connectionDisconnectCounter;

        public SqlClientMetrics(bool enablePlatformSpecificMetrics)
        {
            _enablePlatformSpecificMetrics = enablePlatformSpecificMetrics;

            _meter = new Meter("Microsoft.Data.SqlClient", MeteringVersion);
            _connectionUsageCounter = _meter.CreateUpDownCounter<long>(MetricNames.Connections.Usage, MetricUnits.Connection);
            _connectionMaxIdleCounter = _meter.CreateObservableCounter<long>(MetricNames.Connections.MaxIdle, GetMaxConnectionPoolSizes, MetricUnits.Connection);
            _connectionMinIdleCounter = _meter.CreateObservableCounter<long>(MetricNames.Connections.MinIdle, GetMinConnectionPoolSizes, MetricUnits.Connection);
            _connectionMaxCounter = _meter.CreateObservableCounter<long>(MetricNames.Connections.Max, GetMaxConnectionPoolSizes, MetricUnits.Connection);

            // The five metrics below are new, and have not yet been integrated.

            // This must be reported from a SqlCommand. The connection string will need to come from Command.Connection.ConnectionOptions.UsersConnectionStringForTrace
            // A command begins a pending request when it starts. It leaves the pending request when one of the four states below occurs:
            // 1. It completes successfully
            // 2. It completes, having encountered server-side errors
            // 3. It times out
            // 4. Its underlying connection times out/is broken
            // It does not leave and re-enter the pending state when it's being retried.
            _connectionPendingRequestsCounter = _meter.CreateUpDownCounter<long>(MetricNames.Connections.PendingRequests, MetricUnits.Request);
            
            // This is to be reported from TdsParserStateObject.OnTimeoutCore for both .NET Core and Framework. The connection string will come from
            // _parser.Connection.ConnectionOptions.UsersConnectionStringForTrace.
            // Besides OnTimeoutCore, two other places add a SqlError with an error core of TdsEnums.TIMEOUT_EXPIRED. These are:
            // 1. TdsParserStateObject.ReadSniError
            // 2. TdsParserStateObject.WriteSni
            // 3. TdsParserStateObject.CheckResetConnection
            // If they are only generated when a connection timeout (rather than a command timeout) occurs, they will also report a timeout.
            // The client's connection resiliency functionality could result in multiple connection timeouts, but still result in an open connection.
            _connectionTimeoutsCounter = _meter.CreateCounter<long>(MetricNames.Connections.Timeouts, MetricUnits.Timeout);

            // This creation time refers specifically refers to the time taken to open a new hard connection. It is to be reported as the time
            // taken to execute the body of SqlConnectionFactory.CreateConnection.
            _connectionCreationTimeHistogram = _meter.CreateHistogram<double>(MetricNames.Connections.CreationTime, MetricUnits.Millisecond);

            // The connection wait time refers to the time taken to obtain an open connection from the connection pool. It thus encompasses the creation time.
            // It's reported from SqlConnection.Open, but the data reported by this metric (and the one below it) is also reported by SqlStatistics.
            // With that in mind, SqlStatistics will now always be collected. SqlConnection.StatisticsEnabled will control whether or not GetStatistics
            // returns these values. Setting its value to false will set the connection's closed timestamp in the statistics, (if the connection is open)
            // and setting its value to true will set the connection's open timestamp (if the connection is open)
            _connectionWaitTimeHistogram = _meter.CreateHistogram<double>(MetricNames.Connections.WaitTime, MetricUnits.Millisecond);

            // This is the time difference between the connection's open and close timestamps. It is not affected by disabling and enabling SqlStatistics.
            _connectionUsageTimeHistogram = _meter.CreateHistogram<double>(MetricNames.Connections.UsageTime, MetricUnits.Millisecond);

            _connectionPoolUsageCounter = _meter.CreateUpDownCounter<long>(MetricNames.ConnectionPools.Usage, MetricUnits.ConnectionPool);
            _connectionPoolGroupUsageCounter = _meter.CreateUpDownCounter<long>(MetricNames.ConnectionPoolGroups.Usage, MetricUnits.ConnectionPoolGroup);
            _connectionHardUsageCounter = _meter.CreateUpDownCounter<long>(MetricNames.Connections.HardUsage, MetricUnits.Connection);
            _connectionConnectCounter = _meter.CreateCounter<long>(MetricNames.Connections.Connects, MetricUnits.Connect);
            _connectionDisconnectCounter = _meter.CreateCounter<long>(MetricNames.Connections.Disconnects, MetricUnits.Disconnect);

            if (_enablePlatformSpecificMetrics)
            {
                InitializePlatformSpecificMetrics();
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_enablePlatformSpecificMetrics)
                    {
                        DisposePlatformSpecificMetrics();
                    }

                    _meter.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // This method is designed to handle backwards compatibility with the older performance counters (.NET Framework) and event counters.
        // Since they're all counters, we only need to implement the compatibility layer here.
        private void WriteInstrumentValue(Counter<long> counter, long value, in TagList tagList)
        {
            counter.Add(value, in tagList);
            if (_enablePlatformSpecificMetrics && value > 0)
            {
                IncrementPlatformSpecificMetric(counter.Name, in tagList);
            }
        }

        private void WriteInstrumentValue(UpDownCounter<long> counter, long value, in TagList tagList)
        {
            counter.Add(value, in tagList);
            if (_enablePlatformSpecificMetrics)
            {
                if (value < 0)
                {
                    DecrementPlatformSpecificMetric(counter.Name, in tagList);
                }
                else if (value > 0)
                {
                    IncrementPlatformSpecificMetric(counter.Name, in tagList);
                }
            }
        }

        #region IConnectionMetrics implementation

        void IConnectionMetrics.HardConnectRequest(ConnectionMetricTagListCollection tagList)
        {
            WriteInstrumentValue(_connectionUsageCounter, 1, in tagList.HardConnectionsTags);
            WriteInstrumentValue(_connectionConnectCounter, 1, in tagList.HardConnectsTags);
        }

        void IConnectionMetrics.HardDisconnectRequest(ConnectionMetricTagListCollection tagList)
        {
            WriteInstrumentValue(_connectionUsageCounter, -1, in tagList.HardConnectionsTags);
            WriteInstrumentValue(_connectionDisconnectCounter, 1, in tagList.HardDisconnectsTags);
        }

        void IConnectionMetrics.SoftConnectRequest(ConnectionMetricTagListCollection tagList)
        {
            WriteInstrumentValue(_connectionUsageCounter, 1, in tagList.SoftConnectionsTags);
            WriteInstrumentValue(_connectionConnectCounter, 1, in tagList.SoftConnectsTags);
        }

        void IConnectionMetrics.SoftDisconnectRequest(ConnectionMetricTagListCollection tagList)
        {
            WriteInstrumentValue(_connectionUsageCounter, -1, in tagList.SoftConnectionsTags);
            WriteInstrumentValue(_connectionDisconnectCounter, 1, in tagList.SoftDisconnectsTags);
        }

        void IConnectionMetrics.Timeout(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionTimeoutsCounter, 1, in tagList.TimeoutsTags);

        void IConnectionMetrics.ConnectionCreationTime(in TimeSpan creationTime, ConnectionMetricTagListCollection tagList)
            => _connectionCreationTimeHistogram.Record(creationTime.TotalMilliseconds, in tagList.ConnectionCreationTimeTags);

        void IConnectionMetrics.ConnectionWaitTime(in TimeSpan waitTime, ConnectionMetricTagListCollection tagList)
            => _connectionWaitTimeHistogram.Record(waitTime.TotalMilliseconds, in tagList.ConnectionWaitTimeTags);

        void IConnectionMetrics.ConnectionUsageTime(in TimeSpan usageTime, ConnectionMetricTagListCollection tagList)
            => _connectionUsageTimeHistogram.Record(usageTime.TotalMilliseconds, in tagList.ConnectionUsageTimeTags);

        void IConnectionMetrics.EnterPendingRequest(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPendingRequestsCounter, 1, in tagList.PendingRequestTags);

        void IConnectionMetrics.ExitPendingRequest(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPendingRequestsCounter, -1, in tagList.PendingRequestTags);

        void IConnectionMetrics.EnterNonPooledConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionHardUsageCounter, 1, in tagList.NonPooledHardConnectionUsageTags);

        void IConnectionMetrics.ExitNonPooledConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionHardUsageCounter, -1, in tagList.NonPooledHardConnectionUsageTags);

        void IConnectionMetrics.EnterPooledConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionHardUsageCounter, 1, in tagList.PooledHardConnectionUsageTags);

        void IConnectionMetrics.ExitPooledConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionHardUsageCounter, -1, in tagList.PooledHardConnectionUsageTags);

        void IConnectionMetrics.EnterActiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolGroupUsageCounter, 1, in tagList.ActiveConnectionPoolGroupsTags);

        void IConnectionMetrics.ExitActiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolGroupUsageCounter, -1, in tagList.ActiveConnectionPoolGroupsTags);

        void IConnectionMetrics.EnterInactiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolGroupUsageCounter, 1, in tagList.IdleConnectionPoolGroupsTags);

        void IConnectionMetrics.ExitInactiveConnectionPoolGroup(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolGroupUsageCounter, -1, in tagList.IdleConnectionPoolGroupsTags);

        void IConnectionMetrics.EnterActiveConnectionPool(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolUsageCounter, 1, in tagList.ActiveConnectionPoolsTags);

        void IConnectionMetrics.ExitActiveConnectionPool(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolUsageCounter, -1, in tagList.ActiveConnectionPoolsTags);

        void IConnectionMetrics.EnterInactiveConnectionPool(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolUsageCounter, 1, in tagList.IdleConnectionPoolsTags);

        void IConnectionMetrics.ExitInactiveConnectionPool(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionPoolUsageCounter, -1, in tagList.IdleConnectionPoolsTags);

        void IConnectionMetrics.EnterActiveConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, 1, in tagList.ActiveConnectionsTags);

        void IConnectionMetrics.ExitActiveConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, -1, in tagList.ActiveConnectionsTags);

        void IConnectionMetrics.EnterFreeConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, 1, in tagList.IdleConnectionsTags);

        void IConnectionMetrics.ExitFreeConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, -1, in tagList.IdleConnectionsTags);

        void IConnectionMetrics.EnterStasisConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, 1, in tagList.StasisConnectionsTags);

        void IConnectionMetrics.ExitStasisConnection(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, -1, in tagList.StasisConnectionsTags);

        void IConnectionMetrics.ReclaimedConnectionRequest(ConnectionMetricTagListCollection tagList)
            => WriteInstrumentValue(_connectionUsageCounter, 1, in tagList.ReclaimedConnectionsTags);
        #endregion

        #region Observable metrics implementation
        // TotalMinPoolSize and TotalMaxPoolSize are grouped by connection string, and thus come from DbConnectionPoolGroup. This can result in inaccurate metrics
        // if Windows (or Kerberos, or a future authentication mechanism which permits process-level impersonation) authentication is used. A client could connect
        // to one database using one connection string, impersonate a different Windows/Kerberos identity at the process level, then use the same connection string
        // to connect to a different database. In this situation, both connection pools will be rolled up into a single metric.
        private IEnumerable<Measurement<long>> GetMinConnectionPoolSizes()
        {
            Dictionary<string, DbConnectionFactoryTelemetry> connectionPoolMetrics = SqlConnectionFactory.SingletonInstance.GetFactoryMetrics();

            foreach (KeyValuePair<string, DbConnectionFactoryTelemetry> poolMetric in connectionPoolMetrics)
                yield return new Measurement<long>(poolMetric.Value.TotalMinPoolSize,
                    new KeyValuePair<string, object>(MetricTagNames.PoolName, poolMetric.Key));
        }

        private IEnumerable<Measurement<long>> GetMaxConnectionPoolSizes()
        {
            Dictionary<string, DbConnectionFactoryTelemetry> connectionPoolMetrics = SqlConnectionFactory.SingletonInstance.GetFactoryMetrics();

            foreach (KeyValuePair<string, DbConnectionFactoryTelemetry> poolMetric in connectionPoolMetrics)
                yield return new Measurement<long>(poolMetric.Value.TotalMaxPoolSize,
                    new KeyValuePair<string, object>(MetricTagNames.PoolName, poolMetric.Key));
        }
        #endregion
    }
}
