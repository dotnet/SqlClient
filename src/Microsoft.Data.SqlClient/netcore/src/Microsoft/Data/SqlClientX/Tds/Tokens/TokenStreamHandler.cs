// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;
using Microsoft.Data.SqlClientX.Tds.Tokens.DataClassification;
using Microsoft.Data.SqlClientX.Tds.Tokens.Done;
using Microsoft.Data.SqlClientX.Tds.Tokens.DoneInProc;
using Microsoft.Data.SqlClientX.Tds.Tokens.DoneProc;
using Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange;
using Microsoft.Data.SqlClientX.Tds.Tokens.Error;
using Microsoft.Data.SqlClientX.Tds.Tokens.FeatureExtAck;
using Microsoft.Data.SqlClientX.Tds.Tokens.FedAuthInfo;
using Microsoft.Data.SqlClientX.Tds.Tokens.Info;
using Microsoft.Data.SqlClientX.Tds.Tokens.LoginAck;
using Microsoft.Data.SqlClientX.Tds.Tokens.ReturnStatus;

namespace Microsoft.Data.SqlClientX.Tds.Tokens
{
    internal sealed class TokenStreamHandler
    {
        private readonly Dictionary<TokenType, TokenParser> _parsers;

        internal TokenStreamHandler()
        {
            _parsers = new Dictionary<TokenType, TokenParser>
            {
                [TokenType.EnvChange] = new EnvChangeTokenParser(),
                [TokenType.LoginAck] = new LoginAckTokenParser(),
                [TokenType.FeatureExtAck] = new FeatureExtAckTokenParser(),
                [TokenType.Done] = new DoneTokenParser(),
                [TokenType.DoneInProc] = new DoneInProcTokenParser(),
                [TokenType.DoneProc] = new DoneProcTokenParser(),
                [TokenType.FedAuthInfo] = new FedAuthInfoTokenParser(),
                [TokenType.DataClassification] = new DataClassificationTokenParser(),
                [TokenType.Info] = new InfoTokenParser(),
                [TokenType.Error] = new ErrorTokenParser(),
                [TokenType.ReturnStatus] = new ReturnStatusTokenParser()
            };
        }

        /// <summary>
        /// Receive a token.
        /// </summary>
        /// <returns>Awaitable task. Token.</returns>
        public async ValueTask<Token> ReceiveTokenAsync(TdsContext context, bool isAsync, CancellationToken ct)
        {
            byte type = await context.TdsStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            if (!Enum.IsDefined(typeof(TokenType), type))
            {
                Debug.Fail($"unexpected token; token = {type-2:X2}");
                context.ParserState = TdsParserState.Broken;
                context.SqlConnector.BreakConnection();
                // TODO SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.Run|ERR> Potential multi-threaded misuse of connection, unexpected TDS token found {0}", ObjectID);
                throw SQL.ParsingError();
            }

            TokenType tokenType = (TokenType)type;

            if (!_parsers.TryGetValue(tokenType, out TokenParser value))
            {
                Debug.Fail($"unexpected token; token = {type - 2:X2}");
                context.ParserState = TdsParserState.Broken;
                context.SqlConnector.BreakConnection();
                // TODO SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.Run|ERR> Potential multi-threaded misuse of connection, unexpected TDS token found {0}", ObjectID);
                throw SQL.ParsingError();
            }

            return await value.ParseAsync(tokenType, context.TdsStream, isAsync, ct).ConfigureAwait(false);
        }
    }
}
#endif
