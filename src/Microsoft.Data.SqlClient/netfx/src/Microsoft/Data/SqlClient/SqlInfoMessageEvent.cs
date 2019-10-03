// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/SqlInfoMessageEventArgs/*' />
    public sealed class SqlInfoMessageEventArgs : System.EventArgs
    {
        private SqlException exception;

        internal SqlInfoMessageEventArgs(SqlException exception)
        {
            this.exception = exception;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Errors/*' />
        public SqlErrorCollection Errors
        {
            get { return exception.Errors; }
        }

        /*virtual protected*/
        private bool ShouldSerializeErrors()
        { // MDAC 65548
            return (null != exception) && (0 < exception.Errors.Count);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Message/*' />
        public string Message
        { // MDAC 68482
            get { return exception.Message; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Source/*' />
        public string Source
        { // MDAC 68482
            get { return exception.Source; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/ToString/*' />
        override public string ToString()
        { // MDAC 68482
            return Message;
        }
    }
}
