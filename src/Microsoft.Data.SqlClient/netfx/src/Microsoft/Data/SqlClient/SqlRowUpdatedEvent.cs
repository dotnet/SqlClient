//------------------------------------------------------------------------------
// <copyright file="SqlRowUpdatedEvent.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

using System.Data;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed class SqlRowUpdatedEventArgs : RowUpdatedEventArgs {
        public SqlRowUpdatedEventArgs(DataRow row, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        : base(row, command, statementType, tableMapping) {
        }

        new public SqlCommand Command {
            get {
                return(SqlCommand) base.Command;
            }
        }
    }
}
