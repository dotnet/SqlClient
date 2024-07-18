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
        volatile int _numConnectors;

        internal override (int Total, int Idle, int Busy) Statistics => (_numConnectors, 0, _numConnectors);

        /// <summary>
        /// Initializes a new instance of UnpooledDataSource.
        /// </summary>
        /// <param name="connectionStringBuilder"></param>
        /// <param name="credential"></param>
        internal UnpooledDataSource(SqlConnectionStringBuilder connectionStringBuilder, SqlCredential credential) :
            base(connectionStringBuilder, credential)
        {
        }


        /// <inheritdoc/>
        internal override async ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            SqlConnector connector = await OpenNewInternalConnection(owningConnection, timeout, async, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _numConnectors);
            return connector;
        }

        /// <inheritdoc/>
        internal override async ValueTask<SqlConnector> OpenNewInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            var connector = new SqlConnector(owningConnection, this);
            await connector.Open(timeout, async, cancellationToken).ConfigureAwait(false);
            return connector;
        }

        /// <inheritdoc/>
        internal override void ReturnInternalConnection(SqlConnector connection)
        {
            Interlocked.Decrement(ref _numConnectors);
            connection.Close();
        }
    }
}

#endif
