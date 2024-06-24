// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// A data source that always creates a connection from scratch.
    /// </summary>
    internal sealed class UnpooledDataSource : SqlDataSource
    {
        /// <summary>
        /// Initializes a new instance of UnpooledDataSource.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="credential"></param>
        internal UnpooledDataSource(string connectionString, SqlCredential credential) : base(connectionString, credential)
        {
        }

        /// <summary>
        /// Creates and opens a new SqlConnector.
        /// </summary>
        /// <param name="async">Whether this method should be run asynchronously.</param>
        /// <param name="cancellationToken">Cancels an outstanding asynchronous operation.</param>
        /// <returns></returns>
        internal override async ValueTask<SqlConnector> GetInternalConnection(bool async, CancellationToken cancellationToken)
        {
            var connection = new SqlConnector();
            await connection.Open(async, cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}

#endif