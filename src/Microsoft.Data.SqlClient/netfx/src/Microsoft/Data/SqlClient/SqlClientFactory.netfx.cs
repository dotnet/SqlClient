// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Data.Sql;
using System.Security;
using System.Security.Permissions;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/SqlClientFactory/*'/>
    public sealed partial class SqlClientFactory : IServiceProvider
    {
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
    }
}
