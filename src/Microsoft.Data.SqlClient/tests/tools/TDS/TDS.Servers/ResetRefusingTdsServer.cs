// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// A TDS server that refuses connection resets the way SQL Server does when the session
    /// can no longer be reset (for example after the login database's collation changed): a
    /// request that carries the reset-connection flag is answered with error 4021, 18456 and
    /// 596 ("session is in the kill state") and the connection is then closed abortively.
    /// Requests without the flag are handled by the query engine as usual.
    /// </summary>
    public class ResetRefusingTdsServer : GenericTdsServer<TdsServerArguments>
    {
        private int _refusedResets;

        public ResetRefusingTdsServer() : this(new TdsServerArguments())
        {
        }

        public ResetRefusingTdsServer(TdsServerArguments arguments) : base(arguments)
        {
        }

        /// <summary>
        /// Number of requests that carried the reset flag and were refused.
        /// </summary>
        public int RefusedResets => Volatile.Read(ref _refusedResets);

        /// <summary>
        /// Invoked after a refused request's connection has been closed abortively.
        /// </summary>
        public Action ConnectionAborted { get; set; }

        public override TDSMessageCollection OnSQLBatchRequest(
            ITDSServerSession session,
            TDSMessage message)
        {
            if (!message.IsResetConnectionRequestSet &&
                !message.IsResetConnectionSkipTransactionRequestSet)
            {
                return base.OnSQLBatchRequest(session, message);
            }

            Interlocked.Increment(ref _refusedResets);
            TDSUtilities.Log(Arguments.Log, "Request", message);

            TDSMessage response = new TDSMessage(TDSMessageType.Response,
                new TDSErrorToken(4021, 1, 16,
                    "Resetting the connection results in a different state than the initial " +
                    "login. The login fails."),
                new TDSErrorToken(18456, 1, 14,
                    "Login failed for user 'test'. Reason: Current collation did not match the " +
                    "database's collation during connection reset."),
                new TDSErrorToken(596, 1, 21,
                    "Cannot continue the execution because the session is in the kill state."),
                new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Error));
            TDSUtilities.Log(Arguments.Log, "Response", response);

            // SQL Server kills the session after these tokens, which ends the connection with
            // an RST (TCP) or DisconnectNamedPipe. Both discard unread client-side data.
            session.AbortConnectionAfterResponse = true;
            session.ConnectionAborted = ConnectionAborted;
            return new TDSMessageCollection(response);
        }
    }
}
