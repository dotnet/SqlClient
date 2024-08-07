// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens
{
    /// <summary>
    /// Token parser.
    /// </summary>
    internal abstract class TokenParser
    {
        /// <summary>
        /// Parse a token from the token handler.
        /// </summary>
        /// <param name="tokenType">Token type.</param>
        /// <param name="tdsStream">Tds stream handler.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Awaitable value task. Parsed token.</returns>
        public abstract ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct);
    }
}
