// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER 

using System;
using System.Data.Common;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// Represents a data source that can be used to obtain SqlConnections. 
    /// SqlDataSource can also create and open SqlConnectors, which are the internal/physical connections wrapped by SqlConnection.
    /// </summary>
    internal abstract class SqlDataSource : DbDataSource
    {
        private readonly SqlConnectionStringBuilder _connectionStringBuilder;

        private readonly RemoteCertificateValidationCallback _userCertificateValidationCallback;

        private readonly Action<X509CertificateCollection> _clientCertificatesCallback;

        internal SqlCredential Credential { get; }

        //TODO: return SqlConnection after it is updated to wrap SqlConnectionX 
        /// <summary>
        /// Creates a new, unopened SqlConnection.
        /// </summary>
        /// <returns>Returns the newly created SqlConnection</returns>
        protected override SqlConnectionX CreateDbConnection()
        {
            return SqlConnectionX.FromDataSource(this);
        }

        internal SqlDataSource(
            SqlConnectionStringBuilder connectionStringBuilder,
            SqlCredential credential,
            RemoteCertificateValidationCallback userCertificateValidationCallback,
            Action<X509CertificateCollection> clientCertificatesCallback)
        {
            _connectionStringBuilder = connectionStringBuilder;
            Credential = credential;
            _userCertificateValidationCallback = userCertificateValidationCallback;
            _clientCertificatesCallback = clientCertificatesCallback;
        }

        /// <inheritdoc />
        public override string ConnectionString => _connectionStringBuilder.ConnectionString;


        /// <summary>
        /// Creates a new <see cref="SqlConnection"/> object.
        /// </summary>
        public new SqlConnection CreateConnection()
        {
            throw new NotImplementedException();
            // TODO: return (SqlConnection)CreateDbConnection();
        }

        /// <summary>
        /// Opens a new <see cref="SqlConnection"/>.
        /// </summary>
        public new SqlConnection OpenConnection()
        {
            return (SqlConnection)base.OpenConnection();
        }

        /// <summary>
        /// Asynchronously opens a new <see cref="SqlConnection"/>.
        /// </summary>
        public new async ValueTask<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            return (SqlConnection)await base.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new <see cref="SqlCommand"/> object.
        /// </summary>
        public new SqlCommand CreateCommand(string commandText = null)
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

        // TODO: make abstract
        /// <summary>
        /// Returns an opened SqlConnector.
        /// </summary>
        /// <param name="owningConnection">The SqlConnectionX object that will exclusively own and use this connector.</param>
        /// <param name="timeout">The connection timeout for this operation.</param>
        /// <param name="async">Whether this method should be run asynchronously.</param>
        /// <param name="cancellationToken">Cancels an outstanding asynchronous operation.</param>
        /// <returns></returns>
        internal abstract ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken);
    }
}

#endif
