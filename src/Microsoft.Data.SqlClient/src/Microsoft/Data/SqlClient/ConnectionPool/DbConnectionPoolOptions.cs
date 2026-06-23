// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal sealed class DbConnectionPoolGroupOptions
    {
        private readonly bool _poolByIdentity;
        private readonly int _minPoolSize;
        private readonly int _maxPoolSize;
        private readonly int _creationTimeout;
        private readonly TimeSpan _loadBalanceTimeout;
        private readonly TimeSpan _idleTimeout;
        private readonly bool _hasTransactionAffinity;
        private readonly bool _useLoadBalancing;

        public DbConnectionPoolGroupOptions(
                                        bool poolByIdentity,
                                        int minPoolSize,
                                        int maxPoolSize,
                                        int creationTimeout,
                                        int loadBalanceTimeout,
                                        bool hasTransactionAffinity,
                                        int idleTimeout
        )
        {
            _poolByIdentity = poolByIdentity;
            _minPoolSize = minPoolSize;
            _maxPoolSize = maxPoolSize;
            _creationTimeout = creationTimeout;

            if (0 != loadBalanceTimeout)
            {
                _loadBalanceTimeout = new TimeSpan(0, 0, loadBalanceTimeout);
                _useLoadBalancing = true;
            }

            if (idleTimeout < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(idleTimeout), idleTimeout, "Idle timeout cannot be negative.");
            }

            if (idleTimeout != 0)
            {
                _idleTimeout = TimeSpan.FromSeconds(idleTimeout);
            }

            _hasTransactionAffinity = hasTransactionAffinity;
        }

        /// <summary>
        /// The time (in milliseconds) to wait for a connection to be created/returned before terminating the attempt.
        /// </summary>
        public int CreationTimeout
        {
            get { return _creationTimeout; }
        }
        public bool HasTransactionAffinity
        {
            get { return _hasTransactionAffinity; }
        }
        public TimeSpan LoadBalanceTimeout
        {
            get { return _loadBalanceTimeout; }
        }
        /// <summary>
        /// The maximum time a pooled connection can sit unused (idle) in the pool before it becomes
        /// eligible for eviction. Eviction is best-effort: a connection that has been idle longer
        /// than this value is discarded either on the next retrieval attempt or during a periodic
        /// pool maintenance pass (in pool implementations that perform periodic maintenance),
        /// whichever happens first. Implementations may check this threshold opportunistically, so
        /// eviction may occur somewhat before or after the exact timeout depending on the pool
        /// implementation and maintenance cadence.
        /// <see cref="TimeSpan.Zero"/> disables idle expiration.
        /// </summary>
        public TimeSpan IdleTimeout
        {
            get { return _idleTimeout; }
        }
        public int MaxPoolSize
        {
            get { return _maxPoolSize; }
        }
        public int MinPoolSize
        {
            get { return _minPoolSize; }
        }
        public bool PoolByIdentity
        {
            get { return _poolByIdentity; }
        }
        public bool UseLoadBalancing
        {
            get { return _useLoadBalancing; }
        }
    }
}


