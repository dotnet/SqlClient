// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/SqlNotificationInfo/*' />
    public enum SqlNotificationInfo
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Truncate/*' />
        Truncate = 0,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Insert/*' />
        Insert = 1,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Update/*' />
        Update = 2,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Delete/*' />
        Delete = 3,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Drop/*' />
        Drop = 4,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Alter/*' />
        Alter = 5,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Restart/*' />
        Restart = 6,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Error/*' />
        Error = 7,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Query/*' />
        Query = 8,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Invalid/*' />
        Invalid = 9,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Options/*' />
        Options = 10,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Isolation/*' />
        Isolation = 11,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Expired/*' />
        Expired = 12,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Resource/*' />
        Resource = 13,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/PreviousFire/*' />
        PreviousFire = 14,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/TemplateLimit/*' />
        TemplateLimit = 15,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Merge/*' />
        Merge = 16,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Unknown/*' />
        // use negative values for client-only-generated values
        Unknown = -1,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/AlreadyChanged/*' />
        AlreadyChanged = -2
    }
}
