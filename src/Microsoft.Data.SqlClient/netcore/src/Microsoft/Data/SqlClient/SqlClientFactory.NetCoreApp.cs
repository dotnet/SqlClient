// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlClientFactory : DbProviderFactory
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateBatch/*'/>
        public override bool CanCreateBatch => true;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatch/*'/>
        public override DbBatch CreateBatch() => new SqlBatch();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatchCommand/*'/>
        public override DbBatchCommand CreateBatchCommand() => new SqlBatchCommand();
    }
}
