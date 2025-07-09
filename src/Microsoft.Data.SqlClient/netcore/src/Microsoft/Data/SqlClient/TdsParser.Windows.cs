// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        private void WaitForSSLHandShakeToComplete(ref uint error, ref int protocolVersion)
        {
            // in the case where an async connection is made, encryption is used and Windows Authentication is used, 
            // wait for SSL handshake to complete, so that the SSL context is fully negotiated before we try to use its 
            // Channel Bindings as part of the Windows Authentication context build (SSL handshake must complete 
            // before calling SNISecGenClientContext).
            error = _physicalStateObj.WaitForSSLHandShakeToComplete(out protocolVersion);
            if (error != TdsEnums.SNI_SUCCESS)
            {
                _physicalStateObj.AddError(ProcessSNIError(_physicalStateObj));
                ThrowExceptionAndWarning(_physicalStateObj);
            }
        }
    }    // tdsparser
}//namespace
