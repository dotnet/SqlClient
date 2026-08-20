// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.Data.SqlClient.Diagnostics;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Records every counter <see cref="ISqlClientMetrics"/> exposes, into fields the tests can
    /// read directly.
    /// </summary>
    /// <remarks>
    /// The production counters are not usable for assertions. On .NET they are only observable
    /// through an EventCounter listener, whose polling interval would make these tests slow and
    /// timing dependent. On .NET Framework they are performance counters whose instance name is
    /// derived from the assembly name and process id, so every instance in the process shares one
    /// counter, and constructing one resets it.
    /// </remarks>
    internal sealed class FakeSqlClientMetrics : ISqlClientMetrics
    {
        private long _hardConnects;
        private long _hardDisconnects;
        private long _softConnects;
        private long _softDisconnects;
        private long _nonPooledConnections;
        private long _pooledConnections;
        private long _activeConnectionPoolGroups;
        private long _inactiveConnectionPoolGroups;
        private long _activeConnectionPools;
        private long _inactiveConnectionPools;
        private long _activeConnections;
        private long _freeConnections;
        private long _stasisConnections;
        private long _reclaimedConnections;

        /// <summary>Physical connections opened.</summary>
        internal long HardConnects => Interlocked.Read(ref _hardConnects);

        /// <summary>Physical connections closed.</summary>
        internal long HardDisconnects => Interlocked.Read(ref _hardDisconnects);

        /// <summary>Physical connections currently open.</summary>
        internal long ActiveHardConnections => HardConnects - HardDisconnects;

        /// <summary>Connections handed out from the pool.</summary>
        internal long SoftConnects => Interlocked.Read(ref _softConnects);

        /// <summary>Connections returned to the pool.</summary>
        internal long SoftDisconnects => Interlocked.Read(ref _softDisconnects);

        /// <summary>Connections currently handed out.</summary>
        internal long ActiveSoftConnections => SoftConnects - SoftDisconnects;

        /// <summary>Connections currently bypassing the pool.</summary>
        internal long NonPooledConnections => Interlocked.Read(ref _nonPooledConnections);

        /// <summary>Connections currently owned by a pool.</summary>
        internal long PooledConnections => Interlocked.Read(ref _pooledConnections);

        /// <summary>Connection pool groups currently active.</summary>
        internal long ActiveConnectionPoolGroups => Interlocked.Read(ref _activeConnectionPoolGroups);

        /// <summary>Connection pool groups currently awaiting pruning.</summary>
        internal long InactiveConnectionPoolGroups => Interlocked.Read(ref _inactiveConnectionPoolGroups);

        /// <summary>Connection pools currently active.</summary>
        internal long ActiveConnectionPools => Interlocked.Read(ref _activeConnectionPools);

        /// <summary>Connection pools currently awaiting pruning.</summary>
        internal long InactiveConnectionPools => Interlocked.Read(ref _inactiveConnectionPools);

        /// <summary>Connections currently in use by the application.</summary>
        internal long ActiveConnections => Interlocked.Read(ref _activeConnections);

        /// <summary>Connections currently idle in a pool.</summary>
        internal long FreeConnections => Interlocked.Read(ref _freeConnections);

        /// <summary>Connections currently awaiting cleanup.</summary>
        internal long StasisConnections => Interlocked.Read(ref _stasisConnections);

        /// <summary>Connections reclaimed after being abandoned without being closed.</summary>
        internal long ReclaimedConnections => Interlocked.Read(ref _reclaimedConnections);

        public void HardConnectRequest() => Interlocked.Increment(ref _hardConnects);

        public void HardDisconnectRequest() => Interlocked.Increment(ref _hardDisconnects);

        public void SoftConnectRequest() => Interlocked.Increment(ref _softConnects);

        public void SoftDisconnectRequest() => Interlocked.Increment(ref _softDisconnects);

        public void EnterNonPooledConnection() => Interlocked.Increment(ref _nonPooledConnections);

        public void ExitNonPooledConnection() => Interlocked.Decrement(ref _nonPooledConnections);

        public void EnterPooledConnection() => Interlocked.Increment(ref _pooledConnections);

        public void ExitPooledConnection() => Interlocked.Decrement(ref _pooledConnections);

        public void EnterActiveConnectionPoolGroup() => Interlocked.Increment(ref _activeConnectionPoolGroups);

        public void ExitActiveConnectionPoolGroup() => Interlocked.Decrement(ref _activeConnectionPoolGroups);

        public void EnterInactiveConnectionPoolGroup() => Interlocked.Increment(ref _inactiveConnectionPoolGroups);

        public void ExitInactiveConnectionPoolGroup() => Interlocked.Decrement(ref _inactiveConnectionPoolGroups);

        public void EnterActiveConnectionPool() => Interlocked.Increment(ref _activeConnectionPools);

        public void ExitActiveConnectionPool() => Interlocked.Decrement(ref _activeConnectionPools);

        public void EnterInactiveConnectionPool() => Interlocked.Increment(ref _inactiveConnectionPools);

        public void ExitInactiveConnectionPool() => Interlocked.Decrement(ref _inactiveConnectionPools);

        public void EnterActiveConnection() => Interlocked.Increment(ref _activeConnections);

        public void ExitActiveConnection() => Interlocked.Decrement(ref _activeConnections);

        public void EnterFreeConnection() => Interlocked.Increment(ref _freeConnections);

        public void ExitFreeConnection() => Interlocked.Decrement(ref _freeConnections);

        public void EnterStasisConnection() => Interlocked.Increment(ref _stasisConnections);

        public void ExitStasisConnection() => Interlocked.Decrement(ref _stasisConnections);

        public void ReclaimedConnectionRequest() => Interlocked.Increment(ref _reclaimedConnections);
    }
}
