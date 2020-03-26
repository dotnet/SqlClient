// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventArgs.xml' path='docs/members[@name="SqlRowUpdatedEventArgs"]/SqlRowUpdatedEventArgs/*' />
    public sealed class SqlRowUpdatedEventArgs : RowUpdatedEventArgs
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventArgs.xml' path='docs/members[@name="SqlRowUpdatedEventArgs"]/ctor/*' />
        public SqlRowUpdatedEventArgs(DataRow row, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        : base(row, command, statementType, tableMapping)
        {
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventArgs.xml' path='docs/members[@name="SqlRowUpdatedEventArgs"]/Command/*' />
        new public SqlCommand Command
        {
            get
            {
                return (SqlCommand)base.Command;
            }
        }
    }
}
