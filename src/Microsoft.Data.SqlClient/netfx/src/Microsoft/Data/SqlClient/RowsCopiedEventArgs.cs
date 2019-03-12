//------------------------------------------------------------------------------
// <copyright file="RowsCopiedEvent.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">mithomas</owner>
// <owner current="true" primary="false">blained</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {

    public class SqlRowsCopiedEventArgs : System.EventArgs {
        private bool            _abort;
        private long             _rowsCopied;

        private System.Data.SqlClient.SqlRowsCopiedEventArgs SysSqlRowsCopiedEventArgs { get; set; }

        internal SqlRowsCopiedEventArgs(System.Data.SqlClient.SqlRowsCopiedEventArgs sqlRowsCopiedEventArgs)
        {
            SysSqlRowsCopiedEventArgs = sqlRowsCopiedEventArgs;
        }

        public SqlRowsCopiedEventArgs (long rowsCopied) {
            _rowsCopied = rowsCopied;
        }

        public bool Abort {
            get {
                return SysSqlRowsCopiedEventArgs?.Abort ?? _abort;
            }
            set {
                if (SysSqlRowsCopiedEventArgs != null) {
                    SysSqlRowsCopiedEventArgs.Abort = value;
                }
                else {
                    _abort = value;
                }
            }
        }

        public long RowsCopied {
            get {
                return SysSqlRowsCopiedEventArgs?.RowsCopied ?? _rowsCopied;
            }
        }
    }
}
