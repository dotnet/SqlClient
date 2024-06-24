// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    internal sealed class NonPooledDataSource : SqlDataSource
    {
        internal NonPooledDataSource(string connectionString, SqlCredential credential) : base(connectionString, credential)
        {
        }

        internal override async ValueTask<SqlConnector> GetInternalConnection(bool async, CancellationToken cancellationToken)
        {
            var connection = new SqlConnector();
            await connection.Open(async, cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}

#endif