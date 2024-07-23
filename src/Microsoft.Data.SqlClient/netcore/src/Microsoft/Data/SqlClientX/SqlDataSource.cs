﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER 
#nullable enable

using System;
using System.Data.Common;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    //TODO: update this whole class to return SqlConnection after it is changed to wrap SqlConnectionX 

    /// <summary>
    /// Represents a data source that can be used to obtain SqlConnections. 
    /// SqlDataSource can also create and open SqlConnectors, which are the internal/physical connections wrapped by SqlConnection.
    /// </summary>
    internal abstract class SqlDataSource : DbDataSource
    {
        #region private
        private readonly SqlConnectionStringBuilder _connectionStringBuilder;
        private protected volatile int _isDisposed;
        #endregion

        #region constructors
        /// <summary>
        /// Initializes a new instance of SqlDataSource.
        /// </summary>
        /// <param name="connectionStringBuilder">The connection string that connections produced by this data source should use.</param>
        /// <param name="credential">The credentials that connections produced by this data source should use.</param>
        internal SqlDataSource(
            SqlConnectionStringBuilder connectionStringBuilder,
            SqlCredential credential)
        {
            _connectionStringBuilder = connectionStringBuilder;
            Credential = credential;
        }
        #endregion

        #region properties
        /// <inheritdoc />
        public override string ConnectionString => _connectionStringBuilder.ConnectionString;

        internal SqlCredential Credential { get; }

        internal abstract (int Total, int Idle, int Busy) Statistics { get; }
        #endregion

        /// <summary>
        /// Creates a new, unopened SqlConnection.
        /// </summary>
        /// <returns>Returns the newly created SqlConnection</returns>
        protected override SqlConnectionX CreateDbConnection()
        {
            return SqlConnectionX.FromDataSource(this);
        }

        /// <summary>
        /// Creates a new <see cref="SqlConnection"/> object.
        /// </summary>
        public new SqlConnectionX CreateConnection()
        {
            return CreateDbConnection();
        }

        /// <summary>
        /// Opens a new <see cref="SqlConnection"/>.
        /// </summary>
        public new SqlConnectionX OpenConnection()
        {
            return (SqlConnectionX)base.OpenConnection();
        }

        /// <summary>
        /// Asynchronously opens a new <see cref="SqlConnection"/>.
        /// </summary>
        public new async ValueTask<SqlConnectionX> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            return (SqlConnectionX)await base.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new <see cref="SqlCommand"/> object.
        /// </summary>
        public new SqlCommand CreateCommand(string? commandText = null)
        {
            return (SqlCommand)CreateDbCommand(commandText);
        }

        /// <summary>
        /// Creates a new <see cref="SqlBatch"/> object.
        /// </summary>
        public new SqlBatch CreateBatch()
        {
            return (SqlBatch)CreateDbBatch();
        }

        /// <summary>
        /// Returns an opened SqlConnector.
        /// </summary>
        /// <param name="owningConnection">The SqlConnectionX object that will exclusively own and use this connector.</param>
        /// <param name="timeout">The connection timeout for this operation.</param>
        /// <param name="async">Whether this method should be run asynchronously.</param>
        /// <param name="cancellationToken">Cancels an outstanding asynchronous operation.</param>
        /// <returns></returns>
        internal abstract ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken);


        /// <summary>
        /// Returns a SqlConnector to the data source for recycling or finalization.
        /// </summary>
        /// <param name="connector">The connection returned to the data source.</param>
        internal abstract void ReturnInternalConnection(SqlConnector connector);

        /// <summary>
        /// Opens a new SqlConnector.
        /// </summary>
        /// <param name="owningConnection">The SqlConnectionX object that will exclusively own and use this connector.</param>
        /// <param name="timeout">The connection timeout for this operation.</param>
        /// <param name="async">Whether this method should be run asynchronously.</param>
        /// <param name="cancellationToken">Cancels an outstanding asynchronous operation.</param>
        /// <returns></returns>
        internal abstract ValueTask<SqlConnector?> OpenNewInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken);

        private protected void CheckDisposed()
        {
            //TODO: implement disposal
            if (_isDisposed == 1)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}

#endif