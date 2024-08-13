// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

#nullable enable

using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// Captures context information required to run TDS parser operations.
    /// </summary>
    internal class TdsContext
    {
        internal TdsStream TdsStream { get; set; }

        internal TdsParserState ParserState { get; set; }

        internal ITdsEventListener? TdsEventListener { get; set; }

        internal TdsConnectionState ConnectionState { get; set; }

        internal TdsTimeoutState TimeoutState { get; set; }

        internal TdsSnapshotState SnapshotState { get; set; }

        internal TdsTransactionState TransactionState { get; set; }

        internal TdsErrorWarningsState ErrorWarningsState { get; set; }

        public TdsContext(TdsStream tdsStream, ITdsEventListener tdsEventListener)
        {
            TdsStream = tdsStream;
            TdsEventListener = tdsEventListener;
            // Initialize States
            ConnectionState = new TdsConnectionState();
            TimeoutState = new TdsTimeoutState();
            SnapshotState = new TdsSnapshotState();
            TransactionState = new TdsTransactionState();
            ErrorWarningsState = new TdsErrorWarningsState();
        }
    }
}
#nullable disable
#endif
