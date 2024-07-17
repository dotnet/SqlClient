#if NET8_0_OR_GREATER

using System;
using Microsoft.Data.SqlClientX;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Util
{
    internal class TestBase
    {
        /// <summary>
        /// The connection string that will be used when opening the connection to the tests database.
        /// May be overridden in fixtures, e.g. to set special connection parameters
        /// </summary>
        public virtual string ConnectionString => TestUtil.ConnectionString;

        internal virtual SqlDataSource CreateDataSource()
            => CreateDataSource(ConnectionString);

        internal SqlDataSource CreateDataSource(string connectionString)
            => new SqlDataSourceBuilder(connectionString).Build();

        internal SqlDataSource CreateDataSource(Action<SqlConnectionStringBuilder> connectionStringBuilderAction)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder(ConnectionString);
            connectionStringBuilderAction(connectionStringBuilder);
            return new SqlDataSourceBuilder(connectionStringBuilder, null).Build();
        }

        internal SqlDataSource CreateDataSource(Action<SqlDataSourceBuilder> configure)
        {
            var builder = new SqlDataSourceBuilder(ConnectionString);
            configure(builder);
            return builder.Build();
        }
    }
}

#endif
