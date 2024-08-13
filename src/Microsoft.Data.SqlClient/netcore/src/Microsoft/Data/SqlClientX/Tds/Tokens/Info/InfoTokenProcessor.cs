// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.Info
{
    internal class InfoTokenProcessor : TokenProcessor
    {
        public override void ProcessTokenData(Token token, ref TdsContext tdsContext, RunBehavior runBehavior)
        {
            InfoToken infoToken = (InfoToken)token;
            SqlError error = new SqlError((int)infoToken.Number, infoToken.State, infoToken.Severity, tdsContext.ConnectionState.Server, infoToken.Message, infoToken.ProcName, (int)infoToken.LineNumber, exception: null, -1);

            if (tdsContext.ErrorWarningsState._accumulateInfoEvents)
            {
                Debug.Assert(infoToken.Severity < TdsEnums.MIN_ERROR_CLASS, "INFO with class > TdsEnums.MIN_ERROR_CLASS");

                if (tdsContext.ErrorWarningsState._pendingInfoEvents == null)
                    tdsContext.ErrorWarningsState._pendingInfoEvents = new List<SqlError>();
                tdsContext.ErrorWarningsState._pendingInfoEvents.Add(error);
            }
        }
    }
}
#endif
