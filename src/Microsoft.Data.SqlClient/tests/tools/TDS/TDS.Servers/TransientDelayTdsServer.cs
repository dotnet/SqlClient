// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.SqlServer.TDS.EndPoint;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// TDS Server that delays response to simulate transient network delays
    /// </summary>
    public class TransientDelayTdsServer : GenericTdsServer<TransientDelayTdsServerArguments>, IDisposable
    {
        private int RequestCounter = 0;

        /// <summary>
        /// Cancelled on Dispose to interrupt any in-progress delay so that the
        /// processor task can complete promptly and Dispose does not block
        /// waiting for a sleep to elapse.
        /// </summary>
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();

        /// <summary>
        /// Guards against running Dispose logic more than once.
        /// </summary>
        private bool _disposed;

        public TransientDelayTdsServer(TransientDelayTdsServerArguments arguments) 
            : base(arguments)
        {
        }

        public TransientDelayTdsServer(TransientDelayTdsServerArguments arguments, QueryEngine queryEngine) 
            : base(arguments, queryEngine)
        {
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            // Guard against multiple Dispose calls: cancelling/disposing the CTS twice would
            // throw ObjectDisposedException and break test teardown paths.
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            try
            {
                // Wake any in-progress delay before joining the processor task.
                _disposeCts.Cancel();
                base.Dispose();
                RequestCounter = 0;
            }
            finally
            {
                // Always release the CTS, even if base.Dispose() throws.
                _disposeCts.Dispose();
            }
        }

        /// <summary>
        /// Waits for the configured delay, returning early if the server is being
        /// disposed.
        /// </summary>
        private void Delay()
        {
            // WaitHandle.WaitOne returns immediately once the token is cancelled;
            // otherwise it blocks for the full delay duration.
            _disposeCts.Token.WaitHandle.WaitOne(Arguments.DelayDuration);
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Check if we're still going to raise transient error
            if (Arguments.IsEnabledPermanentDelay ||
                (Arguments.IsEnabledTransientDelay && RequestCounter < Arguments.RepeatCount))
            {
                Delay();

                Interlocked.Increment(ref RequestCounter);
            }

            // Return login response from the base class
            return base.OnLogin7Request(session, request);
        }

        /// <inheritdoc/>
        public override TDSMessageCollection OnSQLBatchRequest(ITDSServerSession session, TDSMessage message)
        {
            if (Arguments.IsEnabledPermanentDelay ||
                (Arguments.IsEnabledTransientDelay && RequestCounter < Arguments.RepeatCount))
            {
                Delay();

                Interlocked.Increment(ref RequestCounter);
            }

            return base.OnSQLBatchRequest(session, message);
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
            Arguments.IsEnabledTransientDelay = isEnabledTransientTimeout;
            Arguments.IsEnabledPermanentDelay = isEnabledPermanentTimeout;
            Arguments.DelayDuration = sleepDuration;
        }
    }
}
