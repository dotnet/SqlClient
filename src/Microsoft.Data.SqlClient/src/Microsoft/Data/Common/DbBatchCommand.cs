// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;

namespace Microsoft.Data.Common
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommand.xml' path='docs/members[@name="DbBatchCommand"]/DbBatchCommand/*' />
    public abstract class DbBatchCommand
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommand.xml' path='docs/members[@name="DbBatchCommand"]/CommandText/*'/>
        public abstract string CommandText { get; set; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommand.xml' path='docs/members[@name="DbBatchCommand"]/CommandType/*'/>
        public abstract CommandType CommandType { get; set; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommand.xml' path='docs/members[@name="DbBatchCommand"]/RecordsAffected/*'/>
        public abstract int RecordsAffected { get; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommand.xml' path='docs/members[@name="DbBatchCommand"]/DbParameterCollection/*'/>
        public DbParameterCollection Parameters => DbParameterCollection;
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommand.xml' path='docs/members[@name="DbBatchCommand"]/DbParameterCollection/*'/>
        protected abstract DbParameterCollection DbParameterCollection { get; }
    }
}
