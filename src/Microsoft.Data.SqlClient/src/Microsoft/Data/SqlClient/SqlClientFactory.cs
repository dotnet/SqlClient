// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Sql;
using System;
using System.Data.Common;
using System.Security.Permissions;
using System.Security;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/SqlClientFactory/*'/>
    public sealed class SqlClientFactory : DbProviderFactory
#if NETFRAMEWORK
        , IServiceProvider
#endif
    {

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/Instance/*'/>
        public static readonly SqlClientFactory Instance = new SqlClientFactory();

        private SqlClientFactory()
        {
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateDataSourceEnumerator/*'/>
        public override bool CanCreateDataSourceEnumerator => true;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateCommand/*'/>
        public override DbCommand CreateCommand()
        {
            return new SqlCommand();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateCommandBuilder/*'/>
        public override DbCommandBuilder CreateCommandBuilder()
        {
            return new SqlCommandBuilder();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateConnection/*'/>
        public override DbConnection CreateConnection()
        {
            return new SqlConnection();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateConnectionStringBuilder/*'/>
        public override DbConnectionStringBuilder CreateConnectionStringBuilder()
        {
            return new SqlConnectionStringBuilder();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateDataAdapter/*'/>
        public override DbDataAdapter CreateDataAdapter()
        {
            return new SqlDataAdapter();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateParameter/*'/>
        public override DbParameter CreateParameter()
        {
            return new SqlParameter();
        }

#if NETFRAMEWORK
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreatePermission/*'/>
        public override CodeAccessPermission CreatePermission(PermissionState state)
        {
            return new SqlClientPermission(state);
        }

        /// <summary>
        /// Extension mechanism for additional services; currently the only service
        /// supported is the DbProviderServices
        /// </summary>
        /// <returns>requested service provider or null.</returns>
        object IServiceProvider.GetService(Type serviceType)
        {
            object result = null;
            if (serviceType == GreenMethods.SystemDataCommonDbProviderServices_Type)
            {
                result = GreenMethods.MicrosoftDataSqlClientSqlProviderServices_Instance();
            }
            return result;
        }
#endif

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateDataSourceEnumerator/*'/>
        public override DbDataSourceEnumerator CreateDataSourceEnumerator()
        {
            return SqlDataSourceEnumerator.Instance;
        }

#if NET6_0_OR_GREATER
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateBatch/*'/>
        public override bool CanCreateBatch => true;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatch/*'/>
        public override DbBatch CreateBatch() => new SqlBatch();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatchCommand/*'/>
        public override DbBatchCommand CreateBatchCommand() => new SqlBatchCommand();
#endif

    }
}
