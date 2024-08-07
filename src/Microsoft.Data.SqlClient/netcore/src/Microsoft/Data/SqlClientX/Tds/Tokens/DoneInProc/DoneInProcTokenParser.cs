// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;
using Microsoft.Data.SqlClientX.Tds.Tokens.Done;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.DoneInProc
{
    internal sealed class DoneInProcTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            ushort status = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            DoneStatus doneStatus = (DoneStatus)status;

            ushort currentCommand = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);

            ulong rowCount;
            if (tdsStream.TdsVersion > TdsVersion.V7_2)
            {
                rowCount = await tdsStream.TdsReader.ReadUInt64Async(isAsync, ct).ConfigureAwait(false);
            }
            else
            {
                rowCount = await tdsStream.TdsReader.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            }

            return new DoneInProcToken(doneStatus, currentCommand, rowCount);
        }
    }
}
