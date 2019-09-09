// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using System.Security.Permissions;
using System.Data.Common;
using System;
using Microsoft.Data.Common;
using System.Data.Sql;

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// 
    /// </summary>
    public sealed class SqlClientFactory : DbProviderFactory, IServiceProvider {

        /// <summary>
        /// 
        /// </summary>
        public static readonly SqlClientFactory Instance = new SqlClientFactory();

        private SqlClientFactory() {
        }

        /// <summary>
        /// 
        /// </summary>
        public override bool CanCreateDataSourceEnumerator {
            get { 
                return true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DbCommand CreateCommand() {
            return new SqlCommand();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DbCommandBuilder CreateCommandBuilder() {
            return new SqlCommandBuilder();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DbConnection CreateConnection() {
            return new SqlConnection();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DbConnectionStringBuilder CreateConnectionStringBuilder() { 
            return new SqlConnectionStringBuilder();
        }

        public override DbDataAdapter CreateDataAdapter() {
            return new SqlDataAdapter();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DbParameter CreateParameter() {
            return new SqlParameter();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public override CodeAccessPermission CreatePermission(PermissionState state) {
            return new SqlClientPermission(state);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DbDataSourceEnumerator CreateDataSourceEnumerator() {
            return SqlDataSourceEnumerator.Instance;
        }

        /// <summary>
        /// Extension mechanism for additional services; currently the only service
        /// supported is the DbProviderServices
        /// </summary>
        /// <param name="serviceType"></param>
        /// <returns>requested service provider or null</returns>
        object IServiceProvider.GetService(Type serviceType)
        {
            object result = null;
            if (serviceType == GreenMethods.SystemDataCommonDbProviderServices_Type)
            {
                result = GreenMethods.MicrosoftDataSqlClientSqlProviderServices_Instance();
            }
            return result;
        }
    }
}

