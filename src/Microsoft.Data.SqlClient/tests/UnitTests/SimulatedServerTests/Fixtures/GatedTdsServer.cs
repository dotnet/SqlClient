// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Holds new connections at pre-login without releasing the TCP port or disturbing existing sessions.
/// Unlike a login delay, this cannot turn into a post-login failure when the client is scheduled slowly.
/// </summary>
internal sealed class GatedTdsServer : TdsServer
{
    private readonly ManualResetEventSlim _release = new(true);
    private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public Task PreLoginBlocked => _entered.Task;

    public GatedTdsServer(TdsServerArguments arguments) : base(arguments)
    {
    }

    /// <summary>
    /// Blocks responses to future pre-login requests until released or disposed.
    /// </summary>
    public void WithholdPreLoginResponses() => _release.Reset();

    /// <summary>
    /// Allows blocked pre-login responses to be sent without waiting for a fixed delay.
    /// </summary>
    public void ReleasePreLoginResponses() => _release.Set();

    public override TDSMessageCollection OnPreLoginRequest(ITDSServerSession session, TDSMessage request)
    {
        TDSMessageCollection response = base.OnPreLoginRequest(session, request);
        if (!_release.IsSet)
        {
            _entered.TrySetResult(true);
            _release.Wait();
        }
        return response;
    }

    /// <summary>
    /// Releases blocked workers before joining them; no protocol worker can outlive the gate.
    /// </summary>
    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _release.Set();
            try
            {
                base.Dispose();
            }
            finally
            {
                _release.Dispose();
            }
        }
    }
}
