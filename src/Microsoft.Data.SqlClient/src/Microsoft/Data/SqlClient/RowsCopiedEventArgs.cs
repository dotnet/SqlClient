// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/SqlRowsCopiedEventArgs/*' />
    public class SqlRowsCopiedEventArgs : System.EventArgs
    {
        private bool _abort;
        private long _rowsCopied;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/ctor/*' />
        public SqlRowsCopiedEventArgs(long rowsCopied)
        {
            _rowsCopied = rowsCopied;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/Abort/*' />
        public bool Abort
        {
            get
            {
                return _abort;
            }
            set
            {
                _abort = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/RowsCopied/*' />
        public long RowsCopied
        {
            get
            {
                return _rowsCopied;
            }
        }
    }
}
