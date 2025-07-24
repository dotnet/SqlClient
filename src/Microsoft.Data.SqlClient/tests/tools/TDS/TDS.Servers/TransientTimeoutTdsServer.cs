// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.Login7;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// TDS Server that authenticates clients according to the requested parameters
    /// </summary>
    public class TransientTimeoutTdsServer : GenericTdsServer<TransientTimeoutTdsServerArguments>, IDisposable
    {
        private static int RequestCounter = 0;

        public TransientTimeoutTdsServer(TransientTimeoutTdsServerArguments arguments) : base(arguments)
        {
        }

        public TransientTimeoutTdsServer(TransientTimeoutTdsServerArguments arguments, QueryEngine queryEngine) : base(arguments, queryEngine)
        {
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Inflate login7 request from the message
            TDSLogin7Token loginRequest = request[0] as TDSLogin7Token;

            // Check if we're still going to raise transient error
            if (Arguments.IsEnabledTransientTimeout
                && RequestCounter < 1) // Fail first time, then connect
            {
                Thread.Sleep(Arguments.SleepDuration);

                RequestCounter++;
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
