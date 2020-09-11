// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/SqlError/*' />
    public sealed class SqlError
    {
        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, uint win32ErrorCode, Exception exception = null)
            : this(infoNumber, errorState, errorClass, server, errorMessage, procedure, lineNumber, exception)
        {
            Win32ErrorCode = (int)win32ErrorCode;
        }

        internal SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, string procedure, int lineNumber, Exception exception = null)
        {
            Number = infoNumber;
            State = errorState;
            Class = errorClass;
            Server = server;
            Message = errorMessage;
            Procedure = procedure;
            LineNumber = lineNumber;
            Win32ErrorCode = 0;
            Exception = exception;
            if (errorClass != 0)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlError.SqlError|ERR> infoNumber={0}, errorState={1}, errorClass={2}, errorMessage='{3}', procedure='{4}', lineNumber={5}", infoNumber, (int)errorState, (int)errorClass, errorMessage, procedure ?? "None", (int)lineNumber);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/ToString/*' />
        // There is no exception stack included because the correct exception stack is only available 
        // on SqlException, and to obtain that the SqlError would have to have backpointers all the
        // way back to SqlException.  If the user needs a call stack, they can obtain it on SqlException.
        public override string ToString()
        {
            return typeof(SqlError).ToString() + ": " + Message; // since this is sealed so we can change GetType to typeof
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Source/*' />
        public string Source { get; private set; } = TdsEnums.SQL_PROVIDER_NAME;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Number/*' />
        public int Number { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/State/*' />
        public byte State { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Class/*' />
        public byte Class { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Server/*' />
        public string Server { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Message/*' />
        public string Message { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Procedure/*' />
        public string Procedure { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/LineNumber/*' />
        public int LineNumber { get; private set; }

        internal int Win32ErrorCode { get; private set; }

        internal Exception Exception { get; private set; }
    }
}
