// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.Telemetry
{
    // Stores telemetry for a single DbConnectionPoolGroup. Maintained by the connection pool group itself
    internal sealed class DbConnectionPoolTelemetry
    {
        private readonly DbConnectionPoolGroupOptions _options;
        private long _poolCount;

        public long TotalMaxPoolSize => _poolCount * (long)_options.MaxPoolSize;

        public long TotalMinPoolSize => _poolCount * (long)_options.MinPoolSize;

        public DbConnectionPoolTelemetry(DbConnectionPoolGroupOptions poolGroupOptions)
        {
            _options = poolGroupOptions;
        }

        public void ReportNewPool()
        {
            Interlocked.Increment(ref _poolCount);
        }

        public void ReportRemovedPool()
        {
            Interlocked.Decrement(ref _poolCount);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _poolCount, 0);
        }
    }

    internal struct DbConnectionFactoryTelemetry
    {
        public long TotalMinPoolSize { get; private set; }

        public long TotalMaxPoolSize { get; private set; }

        public DbConnectionFactoryTelemetry(DbConnectionPoolTelemetry telemetry)
        {
            TotalMinPoolSize = telemetry.TotalMinPoolSize;
            TotalMaxPoolSize = telemetry.TotalMaxPoolSize;
        }

        public void AddConnectionPool(DbConnectionPoolTelemetry telemetry)
        {
            TotalMinPoolSize += telemetry.TotalMinPoolSize;
            TotalMaxPoolSize += telemetry.TotalMaxPoolSize;
        }
    }
}
