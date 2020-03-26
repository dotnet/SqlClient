// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/SqlNotificationType/*' />
    public enum SqlNotificationType
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/Change/*' />
        Change = 0,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/Subscribe/*' />
        Subscribe = 1,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/Unknown/*' />
        // use negative values for client-only-generated values
        Unknown = -1
    }
}
