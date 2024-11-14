// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/SqlBatchCommand/*'/>
    public partial class SqlBatchCommand
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ColumnEncryptionSetting/*'/>
        public Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } set { } }
    }

    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*'/>
    public partial class SqlConnection
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CanCreateBatch/*'/>
        public override bool CanCreateBatch { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateDbBatch/*'/>
        protected override System.Data.Common.DbBatch CreateDbBatch() => throw null;
    }

    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/SqlException/*' />
    public partial class SqlException
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/DbBatchCommand/*' />
        protected override System.Data.Common.DbBatchCommand DbBatchCommand => throw null;
    }
}
