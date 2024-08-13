// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.LoginAck
{
    internal class LoginAckTokenProcessor : TokenProcessor
    {
        public override void ProcessTokenData(Token token, ref TdsContext tdsContext, RunBehavior runBehavior)
        {
            LoginAckToken loginAckTokenData = (LoginAckToken)token;
            tdsContext.TdsStream.TdsVersion = loginAckTokenData.TdsVersion;

            // Successfully logged in
            tdsContext.ParserState = TdsParserState.OpenLoggedIn;
        }
    }
}
#endif
