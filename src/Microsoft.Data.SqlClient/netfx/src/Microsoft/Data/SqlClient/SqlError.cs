// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/SqlError/*' />
    [Serializable]
    public sealed class SqlError
    {

        // bug fix - MDAC 48965 - missing source of exception
        private string source = TdsEnums.SQL_PROVIDER_NAME;
        private int number;
        private byte state;
        private byte errorClass;
        [System.Runtime.Serialization.OptionalFieldAttribute(VersionAdded = 2)]
        private string server;
        private string message;
        private string procedure;
        private int lineNumber;
        [System.Runtime.Serialization.OptionalFieldAttribute(VersionAdded = 4)]
        private int win32ErrorCode;

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, uint win32ErrorCode)
            : this(infoNumber, errorState, errorClass, server, errorMessage, procedure, lineNumber)
        {
            this.win32ErrorCode = (int)win32ErrorCode;
        }

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber)
        {
            this.number = infoNumber;
            this.state = errorState;
            this.errorClass = errorClass;
            this.server = server;
            this.message = errorMessage;
            this.procedure = procedure;
            this.lineNumber = lineNumber;
            if (errorClass != 0)
            {
                Bid.Trace("<sc.SqlError.SqlError|ERR> infoNumber=%d, errorState=%d, errorClass=%d, errorMessage='%ls', procedure='%ls', lineNumber=%d\n",
                    infoNumber, (int)errorState, (int)errorClass, errorMessage,
                    procedure == null ? "None" : procedure, (int)lineNumber);
            }
            this.win32ErrorCode = 0;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/ToString/*' />
        // bug fix - MDAC #49280 - SqlError does not implement ToString();
        // I did not include an exception stack because the correct exception stack is only available 
        // on SqlException, and to obtain that the SqlError would have to have backpointers all the
        // way back to SqlException.  If the user needs a call stack, they can obtain it on SqlException.
        public override string ToString()
        {
            //return this.GetType().ToString() + ": " + this.message;
            return typeof(SqlError).ToString() + ": " + this.message; // since this is sealed so we can change GetType to typeof
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/Source/*' />
        // bug fix - MDAC #48965 - missing source of exception
        public string Source
        {
            get { return this.source; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/Number/*' />
        public int Number
        {
            get { return this.number; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/State/*' />
        public byte State
        {
            get { return this.state; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/Class/*' />
        public byte Class
        {
            get { return this.errorClass; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/Server/*' />
        public string Server
        {
            get { return this.server; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/Message/*' />
        public string Message
        {
            get { return this.message; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/Procedure/*' />
        public string Procedure
        {
            get { return this.procedure; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlError.xml' path='docs/members[@name="SqlError"]/LineNumber/*' />
        public int LineNumber
        {
            get { return this.lineNumber; }
        }

        internal int Win32ErrorCode
        {
            get { return this.win32ErrorCode; }
        }
    }
}
