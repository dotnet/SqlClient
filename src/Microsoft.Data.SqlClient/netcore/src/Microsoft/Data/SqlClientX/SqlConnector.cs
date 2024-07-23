// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Handlers;
using Microsoft.Data.SqlClientX.Handlers.Connection;

#nullable enable

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// Represents a physical connection with the database.
    /// </summary>
    internal class SqlConnector
    {
        internal SqlConnectionX? OwningConnection { get; set; }

        /// <summary>
        /// The data source that generated this connector.
        /// </summary>
        internal SqlDataSource DataSource { get; }

        /// <summary>
        /// The server version this connector is connected to.
        /// </summary>
        internal string ServerVersion => throw new NotImplementedException();

        /// <summary>
        /// Represents the current state of this connection.
        /// </summary>
        internal ConnectionState State => throw new NotImplementedException();

        internal SqlConnector(SqlConnectionX owningConnection, SqlDataSource dataSource)
        {
            OwningConnection = owningConnection;
            DataSource = dataSource;
        }

        /// <summary>
        /// Closes this connection. If this connection is pooled, it is cleaned and returned to the pool.
        /// </summary>
        /// <param name = "async" > Whether this method should run asynchronously.</param>
        /// <returns>A Task indicating the result of the operation.</returns>
        /// <exception cref="NotImplementedException"></exception>
        internal ValueTask Close(bool async)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens this connection.
        /// </summary>
        /// <param name = "timeout">The connection timeout for this operation.</param>
        /// <param name = "async">Whether this method should run asynchronously.</param>
        /// <param name = "cancellationToken">The token used to cancel an ongoing asynchronous call.</param>
        /// <returns>A Task indicating the result of the operation.</returns>
        /// <exception cref="NotImplementedException"></exception>
        internal async ValueTask Open(TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            HandlerOrchestrator orchestrator = HandlerOrchestrator.Instance;
            ConnectionHandlerContext context = new ConnectionHandlerContext
            {
                ConnectionString = new SqlConnectionString(DataSource.ConnectionString)
            };
            //TODO: a call to SqlCommandX once you reach end of chain.
            await orchestrator.ProcessRequestAsync(context, async, cancellationToken).ConfigureAwait(false);         
        }

        /// <summary>
        /// Returns this connection to the data source that generated it.
        /// </summary>
        /// <param name="async">Whether this method should run asynchronously.</param>
        /// <returns></returns>
        internal ValueTask Return(bool async) => DataSource.ReturnInternalConnection(async, this);
    }
}

#endif
