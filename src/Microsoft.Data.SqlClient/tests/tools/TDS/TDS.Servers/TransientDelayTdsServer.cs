// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.SqlServer.TDS.EndPoint;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// TDS Server that authenticates clients according to the requested parameters
    /// </summary>
    public class TransientDelayTdsServer : GenericTdsServer<TransientDelayTdsServerArguments>, IDisposable
    {
        private static int RequestCounter = 0;

        public TransientDelayTdsServer(TransientDelayTdsServerArguments arguments) : base(arguments)
        {
        }

        public TransientDelayTdsServer(TransientDelayTdsServerArguments arguments, QueryEngine queryEngine) : base(arguments, queryEngine)
        {
        }

        public void ResetRequestCounter()
        {
            RequestCounter = 0;
        }

        public void SetTransientTimeoutBehavior(bool isEnabledTransientTimeout, TimeSpan sleepDuration) 
        {
            SetTransientTimeoutBehavior(isEnabledTransientTimeout, false, sleepDuration);
        }

        public void SetTransientTimeoutBehavior(bool isEnabledTransientTimeout, bool isEnabledPermanentTimeout, TimeSpan sleepDuration)
        {
            Arguments.IsEnabledTransientTimeout = isEnabledTransientTimeout;
            Arguments.IsEnabledPermanentTimeout = isEnabledPermanentTimeout;
            Arguments.SleepDuration = sleepDuration;
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Check if we're still going to raise transient error
            if (Arguments.IsEnabledPermanentTimeout || 
                (Arguments.IsEnabledTransientTimeout && RequestCounter < Arguments.RepeatCount))
            {
                Thread.Sleep(Arguments.SleepDuration);

                RequestCounter++;
            }

            // Return login response from the base class
            return base.OnLogin7Request(session, request);
        }

        /// <inheritdoc/>
        public override TDSMessageCollection OnSQLBatchRequest(ITDSServerSession session, TDSMessage message)
        {
            if (Arguments.IsEnabledPermanentTimeout ||
                (Arguments.IsEnabledTransientTimeout && RequestCounter < 1))
            {
                Thread.Sleep(Arguments.SleepDuration);

                RequestCounter++;
            }

            return base.OnSQLBatchRequest(session, message);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            base.Dispose();
            RequestCounter = 0;
        }
    }
}
