// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionMode.xml' path='docs/members[@name="SqlConnectionEncryptionMode"]/SqlConnectionEncryptionMode/*' />
    public enum SqlConnectionEncryptionMode
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionMode.xml' path='docs/members[@name="SqlConnectionEncryptionMode"]/None/*' />
        None = 0,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionMode.xml' path='docs/members[@name="SqlConnectionEncryptionMode"]/LoginOnly/*' />
        LoginOnly = 1,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionMode.xml' path='docs/members[@name="SqlConnectionEncryptionMode"]/Full/*' />
        Full = 2,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionMode.xml' path='docs/members[@name="SqlConnectionEncryptionMode"]/Strict/*' />
        Strict = 3,
    }
}
