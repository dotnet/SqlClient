// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
        /// <param name="connectionStringBuilder"></param>
        /// <param name="credential"></param>
        /// <param name="userCertificateValidationCallback"></param>
        /// <param name="clientCertificatesCallback"></param>
        internal UnpooledDataSource(
            SqlConnectionStringBuilder connectionStringBuilder,
            SqlCredential credential,
            RemoteCertificateValidationCallback userCertificateValidationCallback,
            Action<X509CertificateCollection> clientCertificatesCallback) :
            base(
                connectionStringBuilder,
                credential,
                userCertificateValidationCallback,
                clientCertificatesCallback)
        {
        }


        /// <inheritdoc/>
        internal override ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            return OpenNewInternalConnection(owningConnection, timeout, async, cancellationToken);
        }

        /// <inheritdoc/>
        internal override async ValueTask<SqlConnector> OpenNewInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            var connector = new SqlConnector(owningConnection, this);
            await connector.Open(timeout, async, cancellationToken).ConfigureAwait(false);
            return connector;
        }

        /// <inheritdoc/>
        internal override async ValueTask ReturnInternalConnection(bool async, SqlConnector connection)
        {
            await connection.Close(async).ConfigureAwait(false);
        }
    }
}

#endif
