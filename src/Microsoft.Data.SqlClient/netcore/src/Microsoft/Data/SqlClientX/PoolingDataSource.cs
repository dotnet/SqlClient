// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.RateLimiters;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// A pooling implementation of SqlDataSource. Connections are recycled upon return to be reused by another operation.
    /// </summary>
    internal sealed class PoolingDataSource : SqlDataSource
    {
        private DbConnectionPoolGroupOptions _connectionPoolGroupOptions;
        private RateLimiterBase _connectionRateLimiter;

        internal int MinPoolSize => _connectionPoolGroupOptions.MinPoolSize;
        internal int MaxPoolSize => _connectionPoolGroupOptions.MaxPoolSize;

        internal int ObjectID => objectID;

        private static int _objectTypeCount; // EventSource counter
        private readonly int objectID = Interlocked.Increment(ref _objectTypeCount);

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        //TODO: support auth contexts and provider info
        internal PoolingDataSource(SqlConnectionStringBuilder connectionStringBuilder,
            SqlCredential credential,
            DbConnectionPoolGroupOptions options,
            RateLimiterBase connectionRateLimiter) : base(connectionStringBuilder, credential)
        {
            _connectionPoolGroupOptions = options;
            _connectionRateLimiter = connectionRateLimiter;
            //TODO: other construction
        }

        /// <inheritdoc/>
        internal override ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal struct OpenInternalConnectionState
        {
            readonly SqlConnectionX _owningConnection;
            readonly TimeSpan _timeout;

            internal OpenInternalConnectionState(SqlConnectionX owningConnection, TimeSpan timeout)
            {
                _owningConnection = owningConnection;
                _timeout = timeout;
            }
        }

        /// <inheritdoc/>
        internal override ValueTask<SqlConnector> OpenNewInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            return _connectionRateLimiter.Execute<OpenInternalConnectionState, SqlConnector>(
                RateLimitedOpen,
                new OpenInternalConnectionState(owningConnection, timeout),
                async,
                cancellationToken
            );

            ValueTask<SqlConnector> RateLimitedOpen(OpenInternalConnectionState state, bool async, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        internal override ValueTask ReturnInternalConnection(bool async, SqlConnector connection)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Closes extra idle connections.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void PruneIdleConnections()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Warms up the pool to bring it up to min pool size.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void WarmUp()
        {
            throw new NotImplementedException();
        }

        internal void Shutdown()
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.Shutdown|RES|INFO|CPOOL> {0}", ObjectID);
            if (_connectionRateLimiter != null) {
                _connectionRateLimiter.Dispose();
                _connectionRateLimiter = null;
            }
        }
    }
}

#endif
