// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/SqlError/*' />
    [Serializable]
    public sealed class SqlError
    {
        // bug fix - MDAC 48965 - missing source of exception
        private readonly string _source = DbConnectionStringDefaults.ApplicationName;
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
        [System.Runtime.Serialization.OptionalField(VersionAdded = 5)]
        private readonly Exception _exception;
        [System.Runtime.Serialization.OptionalField(VersionAdded = 6)]
        private readonly int _batchIndex;


        // NOTE: do not combine the overloads below using an optional parameter
        //  they must remain ditinct because external projects use private reflection
        //  to find and invoke the functions, changing the signatures will break many
        //  things elsewhere

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, int win32ErrorCode, Exception exception = null)
            : this(infoNumber, errorState, errorClass, server, errorMessage, procedure, lineNumber, win32ErrorCode, exception, -1)
        {
        }

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, int win32ErrorCode, Exception exception, int batchIndex)
            : this(infoNumber, errorState, errorClass, server, errorMessage, procedure, lineNumber, exception, batchIndex)
        {
            _server = server;
            _win32ErrorCode = win32ErrorCode;
        }

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, Exception exception = null)
            : this(infoNumber, errorState, errorClass, server, errorMessage, procedure, lineNumber, exception, -1)
        {
        }

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, Exception exception, int batchIndex)
        {
            _number = infoNumber;
            _state = errorState;
            _errorClass = errorClass;
            _server = server;
            _message = errorMessage;
            _procedure = procedure;
            _lineNumber = lineNumber;
            _win32ErrorCode = 0;
            _exception = exception;
            _batchIndex = batchIndex;
            if (errorClass != 0)
            {
                SqlClientEventSource.Log.TryTraceEvent("SqlError.ctor | ERR | Info Number {0}, Error State {1}, Error Class {2}, Error Message '{3}', Procedure '{4}', Line Number {5}, Batch Index {6}", infoNumber, (int)errorState, (int)errorClass, errorMessage, procedure ?? "None", (int)lineNumber, batchIndex);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/ToString/*' />
        // bug fix - MDAC #49280 - SqlError does not implement ToString();
        // There is no exception stack included because the correct exception stack is only available
        // on SqlException, and to obtain that the SqlError would have to have backpointers all the
        // way back to SqlException.  If the user needs a call stack, they can obtain it on SqlException.
        public override string ToString()
        {
            return typeof(SqlError).ToString() + ": " + Message; // since this is sealed so we can change GetType to typeof
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Source/*' />
        // bug fix - MDAC #48965 - missing source of exception
        public string Source => _source;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Number/*' />
        public int Number => _number;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/State/*' />
        public byte State => _state;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Class/*' />
        public byte Class => _errorClass;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Server/*' />
        public string Server => _server;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Message/*' />
        public string Message => _message;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Procedure/*' />
        public string Procedure => _procedure;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/LineNumber/*' />
        public int LineNumber => _lineNumber;

        internal int Win32ErrorCode => _win32ErrorCode;

        internal Exception Exception => _exception;

        internal int BatchIndex => _batchIndex;
    }
}
