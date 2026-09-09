// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlClientApp/*' />
[System.CLSCompliant(false)]
public enum SqlClientApp : ushort
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/Unknown/*' />
    Unknown = 0,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/EntityFrameworkCore/*' />
    EntityFrameworkCore = 1,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SemanticKernel/*' />
    SemanticKernel = 2,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/ManagementStudio/*' />
    ManagementStudio = 3,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlManagementObjects/*' />
    SqlManagementObjects = 4,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/DataTierApplicationFramework/*' />
    DataTierApplicationFramework = 5,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlToolsService/*' />
    SqlToolsService = 6,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/AspNetCoreDistributedSqlServerCache/*' />
    AspNetCoreDistributedSqlServerCache = 7,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/EntityFramework/*' />
    EntityFramework = 8,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/AzureFunctionsSqlExtension/*' />
    AzureFunctionsSqlExtension = 9,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/OrleansAdoNet/*' />
    OrleansAdoNet = 10,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/DurableTaskSqlServer/*' />
    DurableTaskSqlServer = 11,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlPackage/*' />
    SqlPackage = 12,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/DataApiBuilder/*' />
    DataApiBuilder = 13
}
