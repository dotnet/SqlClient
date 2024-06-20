// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER 

using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX
{
    internal abstract class SqlDataSource : DbDataSource
    {
        private SqlCredential _credential;
        private string _connectionString;
        public override string ConnectionString => _connectionString;
        internal SqlCredential Credential => _credential;

        internal SqlDataSource(string connectionString, SqlCredential credential)
        {
            _connectionString = connectionString;
            _credential = credential;
        }

        protected override SqlConnectionX CreateDbConnection()
        {
            return SqlConnectionX.FromDataSource(this);
        }

        internal abstract protected ValueTask<SqlInternalConnectionX> GetInternalConnection();
    }
}

#endif
