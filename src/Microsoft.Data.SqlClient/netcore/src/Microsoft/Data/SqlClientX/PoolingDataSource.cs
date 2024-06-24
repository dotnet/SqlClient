// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    internal sealed class PoolingDataSource : SqlDataSource
    {
        private SemaphoreSlim poolSemaphore;
        private ConcurrentQueue<SqlConnector> _sqlConnectors;

        private DbConnectionPoolGroupOptions _connectionPoolGroupOptions;

        internal int MinPoolSize => _connectionPoolGroupOptions.MinPoolSize;
        internal int MaxPoolSize => _connectionPoolGroupOptions.MaxPoolSize;

        //TODO: support auth contexts and provider info
        PoolingDataSource(string connectionString, SqlCredential credential, DbConnectionPoolGroupOptions options) : base(connectionString, credential)
        {
            _connectionPoolGroupOptions = options;
            poolSemaphore = new SemaphoreSlim(MaxPoolSize);
        }

        internal override ValueTask<SqlConnector> GetInternalConnection(bool async, CancellationToken cancellationToken)
        {
            try
            {
                if (async)
                {
                    poolSemaphore.WaitAsync();
                } else
                {
                    poolSemaphore.Wait();
                }


            }
            catch
            {
                poolSemaphore.Release(1);
            }
        }
    }
}

#endif