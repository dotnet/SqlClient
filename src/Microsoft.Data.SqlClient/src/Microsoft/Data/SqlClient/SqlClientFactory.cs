// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.Sql;

#if NETFRAMEWORK
using System;
using System.Reflection;
using System.Security.Permissions;
using System.Security;
#endif

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/SqlClientFactory/*'/>
    #if NETFRAMEWORK
    public sealed class SqlClientFactory : DbProviderFactory, IServiceProvider
    #else
    public sealed class SqlClientFactory : DbProviderFactory
    #endif
    {
        #if NETFRAMEWORK
        #region Constants / Member Variables

        private const string ExtensionAssemblyRef = 
            "System.Data.Entity, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey;

        private const string MicrosoftDataSqlClientSqlProviderServicesTypeName =
            "Microsoft.Data.SqlClient.SQLProviderServices, " + ExtensionAssemblyRef;
        
        private const string SystemDataCommonDbProviderServicesTypeName =
            "System.Data.Common.DbProviderServices, " + ExtensionAssemblyRef;

        private static readonly Lazy<object> MicrosoftDataSqlClientProviderServicesInstance =
            new(static () =>
            {
                FieldInfo instanceFieldInfo = MicrosoftDataSqlClientSqlProviderServicesType.Value?.GetField(
                    "Instance",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                return instanceFieldInfo?.GetValue(null);
            });
        
        private static readonly Lazy<Type> MicrosoftDataSqlClientSqlProviderServicesType =
            new (static () => Type.GetType(MicrosoftDataSqlClientSqlProviderServicesTypeName, throwOnError: false));

        private static readonly Lazy<Type> SystemDataCommonDbProviderServicesType =
            new(static () => Type.GetType(SystemDataCommonDbProviderServicesTypeName, throwOnError: false));
        
        #endregion
        #endif
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/Instance/*'/>
        public static readonly SqlClientFactory Instance = new SqlClientFactory();

        private SqlClientFactory()
        {
        }
        
        #if NET
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateBatch/*'/>
        public override bool CanCreateBatch => true;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatch/*'/>
        public override DbBatch CreateBatch() => new SqlBatch();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatchCommand/*'/>
        public override DbBatchCommand CreateBatchCommand() => new SqlBatchCommand();
        #endif

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
        /// Extension mechanism for additional services; currently the only service supported is
        /// the <c>System.Data.Common.DbProviderServices</c> type.
        /// </summary>
        /// <returns>Requested service provider or <c>null</c>.</returns>
        object IServiceProvider.GetService(Type serviceType) =>
            serviceType == SystemDataCommonDbProviderServicesType.Value
                ? MicrosoftDataSqlClientProviderServicesInstance.Value
                : null;
        #endif

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateDataSourceEnumerator/*'/>
        public override DbDataSourceEnumerator CreateDataSourceEnumerator()
        {
            return SqlDataSourceEnumerator.Instance;
        }
    }
}
