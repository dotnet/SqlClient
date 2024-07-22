// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// Represents a physical connection with the database.
    /// </summary>
    internal class SqlConnector
    {
        #region private
        private static int SpoofedServerProcessId = 1;
        #endregion

        #region constructors
        internal SqlConnector(SqlConnectionX owningConnection, SqlDataSource dataSource)
        {
            OwningConnection = owningConnection;
            DataSource = dataSource;
            ServerProcessId = Interlocked.Increment(ref SpoofedServerProcessId);
        }
        #endregion

        #region properties
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
        /// TODO: set and change state appropriately
        internal ConnectionState State = ConnectionState.Open;

        internal bool IsOpen => State == ConnectionState.Open;
        internal bool IsClosed => State == ConnectionState.Closed;
        internal bool IsBroken => State == ConnectionState.Broken;

        //TODO: set this based on login info
        internal int ServerProcessId { get; private set; }
        #endregion

        /// <summary>
        /// Closes this connection. If this connection is pooled, it is cleaned and returned to the pool.
        /// </summary>
        /// <returns>A Task indicating the result of the operation.</returns>
        /// <exception cref="NotImplementedException"></exception>
        internal void Close()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens this connection.
        /// </summary>
        /// <param name = "timeout">The connection timeout for this operation.</param>
        /// <param name = "isAsync">Whether this method should run asynchronously.</param>
        /// <param name = "cancellationToken">The token used to cancel an ongoing asynchronous call.</param>
        /// <returns>A Task indicating the result of the operation.</returns>
        /// <exception cref="NotImplementedException"></exception>
        internal ValueTask Open(TimeSpan timeout, bool isAsync, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns this connection to the data source that generated it.
        /// </summary>
        internal void Return() => DataSource.ReturnInternalConnection(this);
    }
}

#endif
