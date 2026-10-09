// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.EndPoint;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>Connection phases which can be held without delaying unrelated requests.</summary>
    public enum TdsConnectionPhase
    {
        Login7,
        FederatedAuthenticationInfo
    }

    /// <summary>
    /// Holds a selected connection phase until the test releases it. Arrival signals establish
    /// ordering without sleeps; release precedes server disposal so teardown cannot strand a handler.
    /// </summary>
    public sealed class GatedConnectionTdsServer : TdsServer
    {
        private readonly object _gateLock = new object();
        private readonly ManualResetEventSlim _release = new ManualResetEventSlim();
        private readonly List<KeyValuePair<int, TaskCompletionSource<bool>>> _arrivals =
            new List<KeyValuePair<int, TaskCompletionSource<bool>>>();
        private readonly TdsConnectionPhase _phase;
        private int _arrivalCount;
        private bool _disposed;

        /// <summary>Creates a server whose selected phase is initially held.</summary>
        public GatedConnectionTdsServer(TdsConnectionPhase phase, TdsServerArguments arguments = null)
            : base(arguments ?? new TdsServerArguments())
        {
            _phase = phase;
            ListenAddress = IPAddress.Loopback;
        }

        /// <summary>Number of requests that have reached the selected phase.</summary>
        public int ArrivalCount => Volatile.Read(ref _arrivalCount);

        /// <summary>
        /// Signals once the requested number of actual wire requests has arrived. The caller
        /// must bound its wait; a missed phase is not a successful reproduction.
        /// </summary>
        public Task WaitForArrivalsAsync(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            lock (_gateLock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(GatedConnectionTdsServer));
                }

                if (_arrivalCount >= count)
                {
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _arrivals.Add(new KeyValuePair<int, TaskCompletionSource<bool>>(count, completion));
                return completion.Task;
            }
        }

        /// <summary>Releases current and subsequent requests, including on an assertion failure.</summary>
        public void Release() => _release.Set();

        /// <summary>Releases handlers and observes server shutdown before disposing synchronization state.</summary>
        public override void Dispose()
        {
            lock (_gateLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                foreach (var arrival in _arrivals)
                {
                    arrival.Value.TrySetException(new ObjectDisposedException(nameof(GatedConnectionTdsServer)));
                }
                _arrivals.Clear();
            }

            Release();
            base.Dispose();
            _release.Dispose();
        }

        /// <summary>Holds Login7 before the server prepares its login response.</summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            Enter(TdsConnectionPhase.Login7);
            return base.OnLogin7Request(session, request);
        }

        /// <summary>Holds the fed-auth information needed before invoking the client's provider.</summary>
        protected override TDSMessageCollection OnFederatedAuthenticationInfoRequest(ITDSServerSession session)
        {
            Enter(TdsConnectionPhase.FederatedAuthenticationInfo);
            return base.OnFederatedAuthenticationInfoRequest(session);
        }

        /// <summary>Publishes arrival before blocking the server-side handler only.</summary>
        private void Enter(TdsConnectionPhase phase)
        {
            if (phase != _phase)
            {
                return;
            }

            lock (_gateLock)
            {
                Interlocked.Increment(ref _arrivalCount);
                for (int i = _arrivals.Count - 1; i >= 0; i--)
                {
                    if (_arrivals[i].Key <= _arrivalCount)
                    {
                        _arrivals[i].Value.TrySetResult(true);
                        _arrivals.RemoveAt(i);
                    }
                }
            }
            _release.Wait();
        }
    }
}
