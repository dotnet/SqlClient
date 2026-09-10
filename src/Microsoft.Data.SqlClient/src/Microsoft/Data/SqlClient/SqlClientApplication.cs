// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/SqlClientApplication/*' />
[System.CLSCompliant(false)]
public enum SqlClientApplication : ushort
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/Unknown/*' />
    Unknown = 0,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/EntityFrameworkCore/*' />
    EntityFrameworkCore = 1,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/SemanticKernel/*' />
    SemanticKernel = 2,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/ManagementStudio/*' />
    ManagementStudio = 3,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/SqlManagementObjects/*' />
    SqlManagementObjects = 4,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/DataTierApplicationFramework/*' />
    DataTierApplicationFramework = 5,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/SqlToolsService/*' />
    SqlToolsService = 6,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/AspNetCoreDistributedSqlServerCache/*' />
    AspNetCoreDistributedSqlServerCache = 7,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/EntityFramework/*' />
    EntityFramework = 8,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/AzureFunctionsSqlExtension/*' />
    AzureFunctionsSqlExtension = 9,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/OrleansAdoNet/*' />
    OrleansAdoNet = 10,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/DurableTaskSqlServer/*' />
    DurableTaskSqlServer = 11,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/SqlPackage/*' />
    SqlPackage = 12,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApplication.xml' path='docs/members[@name="SqlClientApplication"]/DataApiBuilder/*' />
    DataApiBuilder = 13
}
