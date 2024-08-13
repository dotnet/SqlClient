// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.Error
{
    internal class ErrorTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            _ = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            int number = await tdsStream.TdsReader.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
            byte state = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            byte severity = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            string message = await tdsStream.TdsReader.ReadUsVarCharAsync(isAsync, ct).ConfigureAwait(false);
            string serverName = await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false);
            string procName = await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false);

            int lineNumber = await tdsStream.TdsReader.ReadInt32Async(isAsync, ct).ConfigureAwait(false);

            return new ErrorToken(number, state, severity, message, serverName, procName, lineNumber);
        }
    }
}
