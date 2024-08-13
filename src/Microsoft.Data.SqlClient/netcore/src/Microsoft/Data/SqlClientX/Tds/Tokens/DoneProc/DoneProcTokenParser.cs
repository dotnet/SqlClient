// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;
using Microsoft.Data.SqlClientX.Tds.Tokens.Done;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.DoneProc
{
    internal sealed class DoneProcTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            ushort status = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            DoneStatus doneStatus = (DoneStatus)status;

            ushort currentCommand = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);

            ulong rowCount = await tdsStream.TdsReader.ReadUInt64Async(isAsync, ct).ConfigureAwait(false);

            return new DoneProcToken(doneStatus, currentCommand, rowCount);
        }
    }
}
