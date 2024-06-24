// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// Represents a physical connection with the database.
    /// </summary>
    internal class SqlConnector
    {
        /// <summary>
        /// The datasource accessed by this connector.
        /// </summary>
        public string DataSource => throw new NotImplementedException();

        /// <summary>
        /// The server version this connector is connected to.
        /// </summary>
        public string ServerVersion => throw new NotImplementedException();

        /// <summary>
        /// Represents the current state of this connection.
        /// </summary>
        public ConnectionState State => throw new NotImplementedException();

        /// <summary>
        /// Closes this connection. If this connection is pooled, it is cleaned and returned to the pool.
        /// </summary>
        /// <param name = "async" > Whether this method should run asynchronously.</param>
        /// <returns>A Task indicating the result of the operation.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task Close(bool async)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens this connection.
        /// </summary>
        /// <param name = "async" > Whether this method should run asynchronously.</param>
        /// <param name="cancellationToken">The token used to cancel an ongoing asynchronous call.</param>
        /// <returns>A Task indicating the result of the operation.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task Open(bool async, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
