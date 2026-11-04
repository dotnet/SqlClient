// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/RegisteredApplication/*' />
[System.CLSCompliant(false)]
public enum RegisteredApplication : ushort
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/Unknown/*' />
    Unknown = 0,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/EntityFrameworkCore/*' />
    EntityFrameworkCore = 1,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/SemanticKernel/*' />
    SemanticKernel = 2,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/ManagementStudio/*' />
    ManagementStudio = 3,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/SqlManagementObjects/*' />
    SqlManagementObjects = 4,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/DataTierApplicationFramework/*' />
    DataTierApplicationFramework = 5,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/SqlToolsService/*' />
    SqlToolsService = 6,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/AspNetCoreDistributedSqlServerCache/*' />
    AspNetCoreDistributedSqlServerCache = 7,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/EntityFramework/*' />
    EntityFramework = 8,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/AzureFunctionsSqlExtension/*' />
    AzureFunctionsSqlExtension = 9,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/OrleansAdoNet/*' />
    OrleansAdoNet = 10,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/DurableTaskSqlServer/*' />
    DurableTaskSqlServer = 11,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/SqlPackage/*' />
    SqlPackage = 12,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/RegisteredApplication.xml' path='docs/members[@name="RegisteredApplication"]/DataApiBuilder/*' />
    DataApiBuilder = 13
}
