// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;
using Microsoft.Data.SqlClientX.Tds.State;
using Microsoft.Data.SqlClientX.Tds.Tokens;

namespace Microsoft.Data.SqlClientX.Tds
{
    internal class TdsParserX
    {
        private TdsContext _tdsContext;

        private readonly TokenStreamHandler _tokenStreamHandler;

        private readonly TokenProcessingHandler _tokenProcessingHandler;

        public TdsParserX(TdsStream tdsStream, ITdsEventListener tdsEventListener)
        {
            _tdsContext = new TdsContext(tdsStream, tdsEventListener);
            _tokenStreamHandler = new TokenStreamHandler();
            _tokenProcessingHandler = new TokenProcessingHandler();
        }

        internal async ValueTask<bool> RunAsync(RunBehavior runBehavior, bool isAsync, CancellationToken ct)
        {
            do
            {
                if (_tdsContext.TimeoutState.IsTimeoutStateExpired)
                {
                    runBehavior = RunBehavior.Attention;
                }

                if (!_tdsContext.ErrorWarningsState._accumulateInfoEvents && (_tdsContext.ErrorWarningsState._pendingInfoEvents != null))
                {
                    if (RunBehavior.Clean != (RunBehavior.Clean & runBehavior))
                    {
                        // We are omitting checks for error.Class in the code below (see processing of INFO) since we know (and assert) that error class
                        // error.Class < TdsEnums.MIN_ERROR_CLASS for info message.
                        // Also we know that TdsEnums.MIN_ERROR_CLASS<TdsEnums.MAX_USER_CORRECTABLE_ERROR_CLASS
                        if ((_tdsContext.TdsEventListener != null) && _tdsContext.TdsEventListener.FireInfoMessageEventOnUserErrors)
                        {
                            foreach (SqlError error in _tdsContext.ErrorWarningsState._pendingInfoEvents)
                            {
                                TdsUtils.FireInfoMessageEvent(_tdsContext, error);
                            }
                        }
                        else
                        {
                            foreach (SqlError error in _tdsContext.ErrorWarningsState._pendingInfoEvents)
                            {
                                _tdsContext.ErrorWarningsState.AddWarning(error);
                            }
                        }
                    }
                    _tdsContext.ErrorWarningsState._pendingInfoEvents = null;
                }

                Token token = await _tokenStreamHandler.ReceiveTokenAsync(_tdsContext, isAsync, ct).ConfigureAwait(false);

                _tokenProcessingHandler.Process(token, ref _tdsContext, runBehavior);
            }

            // Loop while data pending & runbehavior not return immediately, OR
            // if in attention case, loop while no more pending data & attention has not yet been
            // received.
            while ( _tdsContext.TdsStream.PacketDataLeft > 0 &&
            (RunBehavior.ReturnImmediately != (RunBehavior.ReturnImmediately & runBehavior)));

            if (_tdsContext.ErrorWarningsState.HasErrorOrWarning)
            {
                TdsUtils.ThrowExceptionAndWarning(_tdsContext);
            }
            return true;
        }
    }
}
#endif
