// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.ManagedSni;

namespace Microsoft.Data.SqlClient
{
    sealed internal partial class TdsParser
    {
        internal void PostReadAsyncForMars()
        {
            // No-Op
        }

        private void LoadSSPILibrary()
        {
            // No - Op
        }

        private void WaitForSSLHandShakeToComplete(ref uint error, ref int protocolVersion)
        {
            // No - Op
        }

        private TdsParserStateObject.SniErrorDetails GetSniErrorDetails()
        {
            SniError sniError = SniProxy.Instance.GetLastError();

            return new(
                sniError.errorMessage,
                sniError.nativeError,
                sniError.sniError,
                (int)sniError.provider,
                sniError.lineNumber,
                sniError.function,
                sniError.exception);
        }
    }
}
