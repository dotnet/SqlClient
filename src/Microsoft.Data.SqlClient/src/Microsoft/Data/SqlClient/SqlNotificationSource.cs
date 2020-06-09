// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/SqlNotificationSource/*' />
    public enum SqlNotificationSource
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Data/*' />
        Data = 0,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Timeout/*' />
        Timeout = 1,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Object/*' />
        Object = 2,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Database/*' />
        Database = 3,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/System/*' />
        System = 4,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Statement/*' />
        Statement = 5,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Environment/*' />
        Environment = 6,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Execution/*' />
        Execution = 7,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Owner/*' />
        Owner = 8,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Unknown/*' />
        // use negative values for client-only-generated values
        Unknown = -1,
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Client/*' />
        Client = -2
    }
}
