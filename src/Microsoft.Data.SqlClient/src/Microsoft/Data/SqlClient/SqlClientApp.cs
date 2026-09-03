// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlClientApp/*' />
public enum SqlClientApp
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/Unknown/*' />
    Unknown = 0x0000,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/EntityFramework/*' />
    EntityFramework = 0x0001,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SemanticKernel/*' />
    SemanticKernel = 0x0002,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/ManagementStudio/*' />
    ManagementStudio = 0x0003,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlManagementObjects/*' />
    SqlManagementObjects = 0x0004,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/DataTierApplicationFramework/*' />
    DataTierApplicationFramework = 0x0005,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/SqlToolsService/*' />
    SqlToolsService = 0x0006,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/AspNetCoreDistributedSqlServerCache/*' />
    AspNetCoreDistributedSqlServerCache = 0x0007,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/EntityFramework6/*' />
    EntityFramework6 = 0x0008,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/AzureFunctionsSqlExtension/*' />
    AzureFunctionsSqlExtension = 0x0009,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/OrleansAdoNet/*' />
    OrleansAdoNet = 0x000A,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientApp.xml' path='docs/members[@name="SqlClientApp"]/DurableTaskSqlServer/*' />
    DurableTaskSqlServer = 0x000B
}
