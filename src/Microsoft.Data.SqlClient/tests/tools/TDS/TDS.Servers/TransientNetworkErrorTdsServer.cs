// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.SqlServer.TDS.EndPoint;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// TDS Server that drops connection on login request for the specified number of times to simulate transient network errors
    /// </summary>
    public class TransientNetworkErrorTdsServer : GenericTdsServer<TransientNetworkErrorTdsServerArguments>, IDisposable
    {
        private int RequestCounter = 0;

        public void SetErrorBehavior(bool isEnabledTransientFault = true, int repeatCount = 1)
        {
            Arguments.IsEnabledTransientError = isEnabledTransientFault;
            Arguments.RepeatCount = repeatCount;
        }

        public TransientNetworkErrorTdsServer(TransientNetworkErrorTdsServerArguments arguments) : base(arguments)
        {
        }

        public TransientNetworkErrorTdsServer(TransientNetworkErrorTdsServerArguments arguments, QueryEngine queryEngine) : base(arguments, queryEngine)
        {
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Check if we're still going to raise transient error
            if (Arguments.IsEnabledTransientError && RequestCounter < Arguments.RepeatCount)
            {
                KillAllConnections();
                RequestCounter++;
                return null;
            }

            // Return login response from the base class
            return base.OnLogin7Request(session, request);
        }

        public override void Dispose()
        {
            base.Dispose();
            RequestCounter = 0;
        }
    }
}
