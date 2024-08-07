// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.Info
{
    /// <summary>
    /// Info token.
    /// </summary>
    internal sealed class InfoToken : Token
    {
        /// <summary>
        /// Token type.
        /// </summary>
        public override TokenType Type => TokenType.Info;

        /// <summary>
        /// Info number.
        /// </summary>
        public uint Number { get; }

        /// <summary>
        /// State.
        /// </summary>
        public byte State { get; }

        /// <summary>
        /// Severity.
        /// </summary>
        public byte Severity { get; }

        /// <summary>
        /// Message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Server name.
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// Process name.
        /// </summary>
        public string ProcName { get; }

        /// <summary>
        /// Line number.
        /// </summary>
        public uint LineNumber { get; }

        /// <summary>
        /// Creates a new instance of the token.
        /// </summary>
        /// <param name="number">Info number.</param>
        /// <param name="state">State.</param>
        /// <param name="severity">Severity.</param>
        /// <param name="message">Message.</param>
        /// <param name="serverName">Server name.</param>
        /// <param name="procName">Process name.</param>
        /// <param name="lineNumber">Line number.</param>
        public InfoToken(uint number, byte state, byte severity, string message, string serverName, string procName, uint lineNumber)
        {
            Number = number;
            State = state;
            Severity = severity;
            Message = message;
            ServerName = serverName;
            ProcName = procName;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Gets a human readable string representation of this token.
        /// </summary>
        /// <returns>Human readable string representation.</returns>
        public override string ToString()
        {
            return $"{nameof(InfoToken)}[{nameof(Number)}={Number}, {nameof(State)}={State}, {nameof(Severity)}={Severity}, {nameof(Message)}={Message}, {nameof(ServerName)}={ServerName}, {nameof(ProcName)}={ProcName}, {nameof(LineNumber)}={LineNumber}]";
        }
    }
}
