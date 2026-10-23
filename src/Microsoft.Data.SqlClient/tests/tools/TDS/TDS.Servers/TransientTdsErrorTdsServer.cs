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
    /// TDS Server that returns TDS error token on login request for the specified number of times
    /// </summary>
    public class TransientTdsErrorTdsServer : GenericTdsServer<TransientTdsErrorTdsServerArguments>, IDisposable
    {
        private int RequestCounter = 0;
        private readonly CancellationTokenSource _disposeCts = new();
        private bool _disposed;
        private readonly object _errorLock = new object();

        public void SetErrorBehavior(bool isEnabledTransientError, uint errorNumber, int repeatCount = 1, string message = null)
        {
            lock (_errorLock)
            {
                Arguments.IsEnabledTransientError = isEnabledTransientError;
                Arguments.Number = errorNumber;
                Arguments.Message = message;
                Arguments.RepeatCount = repeatCount;
                RequestCounter = 0;
            }
        }

        public TransientTdsErrorTdsServer(TransientTdsErrorTdsServerArguments arguments) : base(arguments)
        {
        }

        public TransientTdsErrorTdsServer(TransientTdsErrorTdsServerArguments arguments, QueryEngine queryEngine) : base(arguments, queryEngine)
        {
        }

        private static string GetErrorMessage(uint errorNumber)
        {
            switch (errorNumber)
            {
                case 40613:
                    return "Database on server is not currently available. Please retry the connection later. " +
                        "If the problem persists, contact customer support, and provide them the session tracing ID.";
                case 42108:
                    return "Can not connect to the SQL pool since it is paused. Please resume the SQL pool and try again.";
                case 42109:
                    return "The SQL pool is warming up. Please try again.";
            }
            return "Unknown server error occurred";
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Inflate login7 request from the message
            TDSLogin7Token loginRequest = request[0] as TDSLogin7Token;

            // Check if we're still going to raise transient error
            if (TryReserveError(out TDSErrorToken error))
            {
                // Increment Login7 count since we won't call base.OnLogin7Request
                Interlocked.Increment(ref _login7Count);
                return GenerateErrorMessage(request, error);
            }

            if (Arguments.DelayAfterTransientErrors > TimeSpan.Zero)
            {
                _disposeCts.Token.WaitHandle.WaitOne(
                    Arguments.DelayAfterTransientErrors);
            }

            // Return login response from the base class
            return base.OnLogin7Request(session, request);
        }

        /// <inheritdoc/>
        public override TDSMessageCollection OnSQLBatchRequest(ITDSServerSession session, TDSMessage message)
        {
            if (TryReserveError(out TDSErrorToken error))
            {
                return GenerateErrorMessage(message, error);
            }

            return base.OnSQLBatchRequest(session, message);
        }

        private bool TryReserveError(out TDSErrorToken error)
        {
            lock (_errorLock)
            {
                error = null;
                if (!Arguments.IsEnabledTransientError || RequestCounter >= Arguments.RepeatCount)
                {
                    return false;
                }
                RequestCounter++;
                error = new TDSErrorToken(Arguments.Number, 1, Arguments.ErrorClass,
                    Arguments.Message ?? GetErrorMessage(Arguments.Number));
                return true;
            }
        }

        private TDSMessageCollection GenerateErrorMessage(TDSMessage request, TDSErrorToken errorToken)
        {
            // Log request to which we're about to send a failure
            TDSUtilities.Log(Arguments.Log, "Request", request);

            // Log response
            TDSUtilities.Log(Arguments.Log, "Response", errorToken);

            // Serialize the error token into the response packet
            TDSMessage responseMessage = new TDSMessage(TDSMessageType.Response, errorToken);

            // Create DONE token
            TDSDoneToken doneToken = new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Error);

            // Log response
            TDSUtilities.Log(Arguments.Log, "Response", doneToken);

            // Serialize DONE token into the response packet
            responseMessage.Add(doneToken);

            // Put a single message into the collection and return it
            return new TDSMessageCollection(responseMessage);
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _disposeCts.Cancel();
                base.Dispose();
                RequestCounter = 0;
            }
            finally
            {
                _disposeCts.Dispose();
            }
        }
    }
}
