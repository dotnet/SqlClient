// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClientX.Tds.Tokens
{
    /// <summary>
    /// Token parser.
    /// </summary>
    internal abstract class TokenProcessor
    {
        /// <summary>
        /// Processes token data for the Tds Context..
        /// </summary>
        /// <param name="token">Token data.</param>
        /// <param name="tdsContext">SqlConnector to update</param>
        /// <param name="runBehavior">Run behavior of parsing</param>
        /// <returns>Awaitable value task. Parsed token.</returns>
        public abstract void ProcessTokenData(Token token, ref TdsContext tdsContext, RunBehavior runBehavior);
    }
}
#endif
