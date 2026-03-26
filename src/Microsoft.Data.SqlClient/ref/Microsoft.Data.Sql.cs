// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.Sql;

/// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/SqlDataSourceEnumerator/*' />
public sealed class SqlDataSourceEnumerator : System.Data.Common.DbDataSourceEnumerator
{
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/Instance/*' />
    public static SqlDataSourceEnumerator Instance { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/GetDataSources/*' />
    public override System.Data.DataTable GetDataSources() { throw null; }
}

/// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/SqlNotificationRequest/*' />
public sealed class SqlNotificationRequest
{
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor1/*' />
    public SqlNotificationRequest() { }
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor2/*' />
    public SqlNotificationRequest(string userData, string options, int timeout) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Options/*' />
    public string Options { get { throw null; } set { } }
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Timeout/*' />
    public int Timeout { get { throw null; } set { } }
    /// <include file='../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/UserData/*' />
    public string UserData { get { throw null; } set { } }
}
