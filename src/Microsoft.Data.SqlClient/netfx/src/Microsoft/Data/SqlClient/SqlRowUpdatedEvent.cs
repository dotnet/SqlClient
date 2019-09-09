// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed class SqlRowUpdatedEventArgs : RowUpdatedEventArgs {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="row"></param>
        /// <param name="command"></param>
        /// <param name="statementType"></param>
        /// <param name="tableMapping"></param>
        public SqlRowUpdatedEventArgs(DataRow row, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        : base(row, command, statementType, tableMapping) {
        }

        /// <summary>
        /// 
        /// </summary>
        new public SqlCommand Command {
            get {
                return(SqlCommand) base.Command;
            }
        }
    }
}
