//------------------------------------------------------------------------------
// <copyright file="StatementCompletedEventArgs.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">markash</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data {
    using System;

    public sealed class StatementCompletedEventArgs : System.EventArgs {
        private readonly int _recordCount;

        public StatementCompletedEventArgs(int recordCount) {
            _recordCount = recordCount;
        }

        public int RecordCount {
            get {
                return _recordCount;
            }
        }
    }
}

