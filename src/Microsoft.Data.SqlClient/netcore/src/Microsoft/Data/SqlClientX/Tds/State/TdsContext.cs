// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Collections.Generic;
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

        internal SqlConnector SqlConnector { get; set; }

        internal TdsParserState ParserState { get; set; }

        internal TdsConnectionState ConnectionState { get; set; }

        internal TdsTimeoutState TimeoutState { get; set; }

        internal TdsSnapshotState TdsSnapshotState { get; set; }

        internal TdsTransactionState TdsTransactionState { get; set; }

        internal TdsErrorWarningsState TdsErrorWarningsState { get; set; }

        /// <summary>
        /// TRUE - accumulate info messages during TdsParser operations, 
        /// FALSE - fire them
        /// </summary>
        internal bool _accumulateInfoEvents;

        internal List<SqlError> _pendingInfoEvents;

        public TdsContext(SqlConnector sqlConnector)
        {
            SqlConnector = sqlConnector;
        }
    }
}
#endif
