// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// A pooling implementation of SqlDataSource. Connections are recycled upon return to be reused by another operation.
    /// </summary>
    internal sealed class PoolingDataSource : SqlDataSource
    {
        private Channel<SqlConnector> _sqlConnectors;
        private DbConnectionPoolGroupOptions _connectionPoolGroupOptions;

        internal int MinPoolSize => _connectionPoolGroupOptions.MinPoolSize;
        internal int MaxPoolSize => _connectionPoolGroupOptions.MaxPoolSize;

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        /// <param name="connectionString">The connection string used for connections.</param>
        /// <param name="credential">The credential used for connections.</param>
        /// <param name="options">The options used for this pool.</param>
        //TODO: support auth contexts and provider info
        PoolingDataSource(string connectionString, SqlCredential credential, DbConnectionPoolGroupOptions options) : base(connectionString, credential)
        {
            _connectionPoolGroupOptions = options;
            //TODO: other construction
        }

        /// <inheritdoc/>
        internal override ValueTask<SqlConnector> GetInternalConnection(bool async, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal ValueTask<SqlConnector> OpenNewInternalConnection(bool async, CancellationToken cancellationToken)
        {

        }

        /// <inheritdoc/>
        internal override void ReturnInternalConnection(SqlConnector connection)
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
    }
}

#endif