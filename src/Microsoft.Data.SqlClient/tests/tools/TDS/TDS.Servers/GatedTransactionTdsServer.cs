// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.EnvChange;
using Microsoft.SqlServer.TDS.TransactionManager;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>Supports real local enlistment and can hold its BEGIN response; it does not implement DTC or SQL transaction semantics.</summary>
    public sealed class GatedTransactionTdsServer : TdsServer, ITDSTransactionServer
    {
        private readonly ConcurrentDictionary<uint, ulong> _transactions = new ConcurrentDictionary<uint, ulong>();
        private readonly ManualResetEventSlim _release = new ManualResetEventSlim();
        private readonly TaskCompletionSource<bool> _begin = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _beginCount;
        private int _commitCount;
        private int _rollbackCount;
        private int _disposed;

        public Task BeginArrived => _begin.Task;
        public int BeginCount => Volatile.Read(ref _beginCount);
        public int CommitCount => Volatile.Read(ref _commitCount);
        public int RollbackCount => Volatile.Read(ref _rollbackCount);

        /// <summary>Creates a loopback peer with an initially held BEGIN response.</summary>
        public GatedTransactionTdsServer() : base(new TdsServerArguments())
        {
            ListenAddress = IPAddress.Loopback;
        }

        /// <summary>Releases present and future BEGIN responses, including during cleanup.</summary>
        public void ReleaseBegin() => _release.Set();

        /// <summary>Tracks descriptors per session and returns genuine transaction ENVCHANGE and DONE tokens.</summary>
        public TDSMessageCollection OnTransactionManagerRequest(ITDSServerSession session, TDSMessage message)
        {
            var request = (TDSTransactionManagerToken)message[0];
            ulong descriptor;
            TDSEnvChangeToken change;
            bool begin = request.Request == TDSTransactionManagerRequest.Begin;
            if (begin)
            {
                descriptor = session.SessionID + 1UL;
                if (request.Descriptor != 0 || !_transactions.TryAdd(session.SessionID, descriptor))
                {
                    throw new InvalidOperationException("Nested or duplicate local transaction BEGIN.");
                }
                Interlocked.Increment(ref _beginCount);
                _begin.TrySetResult(true);
                _release.Wait();
                change = new TDSEnvChangeToken(TDSEnvChangeTokenType.BeginTransaction, BitConverter.GetBytes(descriptor), null);
            }
            else
            {
                if (!_transactions.TryRemove(session.SessionID, out descriptor) || descriptor != request.Descriptor)
                {
                    throw new InvalidOperationException("Transaction completion did not match the session descriptor.");
                }
                if (request.Request == TDSTransactionManagerRequest.Commit)
                {
                    Interlocked.Increment(ref _commitCount);
                    change = new TDSEnvChangeToken(TDSEnvChangeTokenType.CommitTransaction, null, BitConverter.GetBytes(descriptor));
                }
                else if (request.Request == TDSTransactionManagerRequest.Rollback)
                {
                    Interlocked.Increment(ref _rollbackCount);
                    change = new TDSEnvChangeToken(TDSEnvChangeTokenType.RollbackTransaction, null, BitConverter.GetBytes(descriptor));
                }
                else
                {
                    throw new NotSupportedException("Unsupported local transaction-manager operation.");
                }
            }
            var response = new TDSMessage(TDSMessageType.Response);
            response.Add(change);
            response.Add(new TDSDoneToken(begin ? TDSDoneTokenStatusType.TransactionInProgress : TDSDoneTokenStatusType.Final));
            return new TDSMessageCollection(response);
        }

        /// <summary>Removes only this session's transaction bookkeeping.</summary>
        public override void CloseSession(ITDSServerSession session)
        {
            _transactions.TryRemove(session.SessionID, out _);
            base.CloseSession(session);
        }

        /// <summary>Unblocks transport handlers before joining peer threads and disposing the gate.</summary>
        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            ReleaseBegin();
            _begin.TrySetException(new ObjectDisposedException(nameof(GatedTransactionTdsServer)));
            base.Dispose();
            _release.Dispose();
        }
    }
}
