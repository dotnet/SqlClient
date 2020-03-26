// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/SqlInfoMessageEventArgs/*' />
    public sealed class SqlInfoMessageEventArgs : System.EventArgs
    {
        private SqlException _exception;

        internal SqlInfoMessageEventArgs(SqlException exception)
        {
            _exception = exception;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Errors/*' />
        public SqlErrorCollection Errors
        {
            get { return _exception.Errors; }
        }

        private bool ShouldSerializeErrors()
        {
            return (null != _exception) && (0 < _exception.Errors.Count);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Message/*' />
        public string Message
        {
            get { return _exception.Message; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Source/*' />
        public string Source
        {
            get { return _exception.Source; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/ToString/*' />
        override public string ToString()
        {
            return Message;
        }
    }
}
