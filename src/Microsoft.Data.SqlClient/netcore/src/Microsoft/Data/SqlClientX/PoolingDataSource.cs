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
        private IRateLimiter _rateLimiter;

        internal int MinPoolSize => _connectionPoolGroupOptions.MinPoolSize;
        internal int MaxPoolSize => _connectionPoolGroupOptions.MaxPoolSize;

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        //TODO: support auth contexts and provider info
        PoolingDataSource(SqlConnectionStringBuilder connectionStringBuilder,
            SqlCredential credential,
            DbConnectionPoolGroupOptions options,
            IRateLimiter rateLimiter) : base(connectionStringBuilder, credential, userCertificateValidationCallback, clientCertificatesCallback)
        {
            _connectionPoolGroupOptions = options;
            _rateLimiter = rateLimiter;
            //TODO: other construction
        }

        /// <inheritdoc/>
        internal override ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        internal override ValueTask<SqlConnector> OpenNewInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            return _rateLimiter.Execute<SqlConnector>(() => RateLimitedOpen(owningConnection, timeout, async, cancellationToken), async, cancellationToken);

            ValueTask<SqlConnector> RateLimitedOpen(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
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
    }
}

#endif