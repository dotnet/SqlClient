// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/SqlRowUpdatingEventArgs/*' />
    public sealed class SqlRowUpdatingEventArgs : RowUpdatingEventArgs
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/ctor/*' />
        public SqlRowUpdatingEventArgs(DataRow row, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        : base(row, command, statementType, tableMapping)
        {
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/Command/*' />
        new public SqlCommand Command
        {
            get { return (base.Command as SqlCommand); }
            set { base.Command = value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/BaseCommand/*' />
        override protected IDbCommand BaseCommand
        {
            get { return base.BaseCommand; }
            set { base.BaseCommand = (value as SqlCommand); }
        }
    }
}
