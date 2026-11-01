// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionOverrides.xml' path='docs/members[@name="SqlConnectionOverrides"]/SqlConnectionOverrides/*' />
[Flags]
public enum SqlConnectionOverrides
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionOverrides.xml' path='docs/members[@name="SqlConnectionOverrides"]/None/*' />
    None = 0,
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionOverrides.xml' path='docs/members[@name="SqlConnectionOverrides"]/OpenWithoutRetry/*' />
    OpenWithoutRetry = 1,
}
