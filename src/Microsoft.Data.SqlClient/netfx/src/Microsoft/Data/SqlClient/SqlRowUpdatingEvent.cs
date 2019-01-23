//------------------------------------------------------------------------------
// <copyright file="SqlRowUpdatingEvent.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {
    using System.Data;
    using System.Data.Common;

    public sealed class SqlRowUpdatingEventArgs : RowUpdatingEventArgs {

        public SqlRowUpdatingEventArgs(DataRow row, IDbCommand command, StatementType statementType, DataTableMapping tableMapping) 
        : base(row, command, statementType, tableMapping) {
        }

        new public SqlCommand Command {
            get { return (base.Command as SqlCommand); }
            set { base.Command = value; }
        }

        override protected IDbCommand BaseCommand {
            get { return base.BaseCommand; }
            set { base.BaseCommand = (value as SqlCommand); }
        }
    }
}
