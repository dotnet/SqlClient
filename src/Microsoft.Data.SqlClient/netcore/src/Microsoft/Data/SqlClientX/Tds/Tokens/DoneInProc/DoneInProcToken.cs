// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClientX.Tds.Tokens.Done;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.DoneInProc
{
    /// <summary>
    /// Token indicating the completion status of a statement in a procedure.
    /// </summary>
    internal sealed class DoneInProcToken : DoneToken
    {
        /// <summary>
        /// Token type.
        /// </summary>
        public override TokenType Type => TokenType.DoneInProc;

        /// <summary>
        /// Create a new instance with a status, current command and row count.
        /// </summary>
        /// <param name="status">Status.</param>
        /// <param name="currentCommand">Current command.</param>
        /// <param name="rowCount">Row count.</param>
        public DoneInProcToken(DoneStatus status, ushort currentCommand, ulong rowCount)
            : base(status, currentCommand, rowCount)
        {
        }

        /// <summary>
        /// Gets a human readable string representation of this token.
        /// </summary>
        /// <returns>Human readable string representation.</returns>
        public override string ToString()
        {
            return $"DoneInProcToken[Status=0x{Status:X}({Status}), CurrentCommand={CurrentCommand}, RowCount={RowCount}]";
        }
    }
}
