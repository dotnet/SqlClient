// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/SqlInfoMessageEventArgs/*' />
    public sealed class SqlInfoMessageEventArgs : System.EventArgs
    {
        private readonly SqlException _exception;

        internal SqlInfoMessageEventArgs(SqlException exception)
        {
            _exception = exception;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Errors/*' />
        public SqlErrorCollection Errors => _exception.Errors;

        // MDAC 65548
        private bool ShouldSerializeErrors() => _exception != null && (0 < _exception.Errors.Count);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Message/*' />
        public string Message => _exception.Message;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Source/*' />
        // MDAC 68482
        public string Source => _exception.Source;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/ToString/*' />
        // MDAC 68482
        override public string ToString() => Message;
    }
}
