// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;
using Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange;
using Microsoft.Data.SqlClientX.Tds.Tokens.Error;
using Microsoft.Data.SqlClientX.Tds.Tokens.Info;
using Microsoft.Data.SqlClientX.Tds.Tokens.LoginAck;

namespace Microsoft.Data.SqlClientX.Tds.Tokens
{
    internal class TokenProcessingHandler
    {
        private readonly Dictionary<TokenType, TokenProcessor> _processors;

        internal TokenProcessingHandler()
        {
            _processors = new Dictionary<TokenType, TokenProcessor>
            {
                [TokenType.EnvChange] = new EnvChangeTokenProcessor(),
                [TokenType.LoginAck] = new LoginAckTokenProcessor(),
                [TokenType.Info] = new InfoTokenProcessor(),
                [TokenType.Error] = new ErrorTokenProcessor(),
            };
        }

        /// <summary>
        /// Process token data into provided context
        /// </summary>
        /// 
        public void Process(Token token, ref TdsContext context, RunBehavior runBehavior)
        {
            if (!_processors.TryGetValue(token.Type, out TokenProcessor value))
            {
                // Skip Data Processing and return.
                return;
            }

            value.ProcessTokenData(token, ref context, runBehavior);
        }
    }
}
#endif
