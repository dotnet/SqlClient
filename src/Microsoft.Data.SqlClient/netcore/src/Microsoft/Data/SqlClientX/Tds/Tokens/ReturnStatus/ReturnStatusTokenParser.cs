// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.ReturnStatus
{
    internal class ReturnStatusTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
            => new ReturnStatusToken(await tdsStream.TdsReader.ReadInt32Async(isAsync, ct).ConfigureAwait(false));
    }
}
