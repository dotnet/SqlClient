// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.Error
{
    internal class ErrorTokenProcessor : TokenProcessor
    {
        public override void ProcessTokenData(Token token, ref TdsContext tdsContext, RunBehavior runBehavior)
        {
            tdsContext.SnapshotState.HasReceivedError = true;

            ErrorToken errorToken = (ErrorToken)token;
            SqlError error = new SqlError((int)errorToken.Number, errorToken.State, errorToken.Severity, tdsContext.ConnectionState.Server, errorToken.Message, errorToken.ProcName, (int)errorToken.LineNumber, exception: null, -1);

            if (RunBehavior.Clean != (RunBehavior.Clean & runBehavior))
            {
                if ((tdsContext.TdsEventListener != null) &&
                    (tdsContext.TdsEventListener.FireInfoMessageEventOnUserErrors == true) &&
                    (error.Class <= TdsEnums.MAX_USER_CORRECTABLE_ERROR_CLASS))
                {
                    // Fire SqlInfoMessage here
                    TdsUtils.FireInfoMessageEvent(tdsContext, error);
                }
                else
                {
                    // insert error/info into the appropriate exception - warning if info, exception if error
                    if (error.Class < TdsEnums.MIN_ERROR_CLASS)
                    {
                        tdsContext.ErrorWarningsState.AddWarning(error);
                    }
                    else if (error.Class < TdsEnums.FATAL_ERROR_CLASS)
                    {
                        // Continue results processing for all non-fatal errors (<20)
                        tdsContext.ErrorWarningsState.AddError(error);
                    }
                    else
                    {
                        tdsContext.ErrorWarningsState.AddError(error);

                        // Else we have a fatal error and we need to change the behavior
                        // since we want the complete error information in the exception.
                        // Besides - no further results will be received.
                        runBehavior = RunBehavior.UntilDone;
                    }
                }
            }
            else if (error.Class >= TdsEnums.FATAL_ERROR_CLASS)
            {
                tdsContext.ErrorWarningsState.AddError(error);
            }
        }
    }
}
#endif
