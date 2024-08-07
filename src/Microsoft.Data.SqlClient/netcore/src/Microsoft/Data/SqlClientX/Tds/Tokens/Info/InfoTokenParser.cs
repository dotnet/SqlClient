// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.Info
{
    internal class InfoTokenParser : TokenParser
    {
        /// <inheritdoc/>
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            _ = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            uint number = await tdsStream.TdsReader.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            byte state = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            byte severity = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            string message = await tdsStream.TdsReader.ReadUsVarCharAsync(isAsync, ct).ConfigureAwait(false);
            string serverName = await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false);
            string procName = await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false);

            uint lineNumber;
            if (tdsStream.TdsVersion < TdsVersion.V7_2)
            {
                lineNumber = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            }
            else
            {
                lineNumber = await tdsStream.TdsReader.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            }

            return new InfoToken(number, state, severity, message, serverName, procName, lineNumber);
        }
    }
}
