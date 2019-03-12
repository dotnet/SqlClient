//------------------------------------------------------------------------------
// <copyright file="SqlInfoMessageEvent.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {

    public sealed class SqlInfoMessageEventArgs : System.EventArgs {
        private SqlException exception;

        internal SqlInfoMessageEventArgs(SqlException exception) {
            this.exception = exception;
        }

        internal SqlInfoMessageEventArgs(System.Data.SqlClient.SqlInfoMessageEventArgs sqlInfoMessageEventArgs)
        {
            SysSqlInfoMessageEventArgs = sqlInfoMessageEventArgs;
        }

        private System.Data.SqlClient.SqlInfoMessageEventArgs SysSqlInfoMessageEventArgs { get; set; }

        public SqlErrorCollection Errors {
            get {
                if (null != SysSqlInfoMessageEventArgs)
                {
                    return new SqlErrorCollection(SysSqlInfoMessageEventArgs.Errors);
                }
                return exception.Errors;
            }
        }

        /*virtual protected*/private bool ShouldSerializeErrors() { // MDAC 65548
            return (null != exception) && (0 < exception.Errors.Count);
        }

        public string Message { // MDAC 68482
            get { return SysSqlInfoMessageEventArgs?.Message ?? exception.Message; }
        }

        public string Source { // MDAC 68482
            get { return SysSqlInfoMessageEventArgs?.Message ?? exception.Source; }
        }

        override public string ToString() { // MDAC 68482
            return Message;
        }
    }
}
