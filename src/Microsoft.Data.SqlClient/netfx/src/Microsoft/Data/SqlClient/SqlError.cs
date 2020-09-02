// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/SqlError/*' />
    [Serializable]
    public sealed class SqlError
    {
        // bug fix - MDAC 48965 - missing source of exception
        private readonly string _source = TdsEnums.SQL_PROVIDER_NAME;
        private readonly int _number;
        private readonly byte _state;
        private readonly byte _errorClass;
        [System.Runtime.Serialization.OptionalField(VersionAdded = 2)]
        private readonly string _server;
        private readonly string _message;
        private readonly string _procedure;
        private readonly int _lineNumber;
        [System.Runtime.Serialization.OptionalField(VersionAdded = 4)]
        private readonly int _win32ErrorCode;

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, uint win32ErrorCode)
            : this(infoNumber, errorState, errorClass, server, errorMessage, procedure, lineNumber)
        {
            _win32ErrorCode = (int)win32ErrorCode;
        }

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber)
        {
            _number = infoNumber;
            _state = errorState;
            _errorClass = errorClass;
            _server = server;
            _message = errorMessage;
            _procedure = procedure;
            _lineNumber = lineNumber;
            if (errorClass != 0)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlError.SqlError|ERR> infoNumber={0}, errorState={1}, errorClass={2}, errorMessage='{3}', procedure='{4}', lineNumber={5}", infoNumber, (int)errorState, (int)errorClass, errorMessage, procedure, (int)lineNumber);
            }
            _win32ErrorCode = 0;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/ToString/*' />
        // bug fix - MDAC #49280 - SqlError does not implement ToString();
        // I did not include an exception stack because the correct exception stack is only available 
        // on SqlException, and to obtain that the SqlError would have to have backpointers all the
        // way back to SqlException.  If the user needs a call stack, they can obtain it on SqlException.
        public override string ToString()
        {
            //return GetType().ToString() + ": " + message;
            return typeof(SqlError).ToString() + ": " + _message; // since this is sealed so we can change GetType to typeof
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Source/*' />
        // bug fix - MDAC #48965 - missing source of exception
        public string Source
        {
            get { return _source; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Number/*' />
        public int Number
        {
            get { return _number; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/State/*' />
        public byte State
        {
            get { return _state; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Class/*' />
        public byte Class
        {
            get { return _errorClass; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Server/*' />
        public string Server
        {
            get { return _server; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Message/*' />
        public string Message
        {
            get { return _message; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Procedure/*' />
        public string Procedure
        {
            get { return _procedure; }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/LineNumber/*' />
        public int LineNumber
        {
            get { return _lineNumber; }
        }

        internal int Win32ErrorCode
        {
            get { return _win32ErrorCode; }
        }
    }
}
