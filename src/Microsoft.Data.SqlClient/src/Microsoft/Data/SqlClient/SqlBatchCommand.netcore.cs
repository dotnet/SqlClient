// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public partial class SqlBatchCommand
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CreateParameter/*'/>
        public override DbParameter CreateParameter() => new SqlParameter();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CanCreateParameter/*'/>
        public override bool CanCreateParameter => true;
    }
}

#endif
