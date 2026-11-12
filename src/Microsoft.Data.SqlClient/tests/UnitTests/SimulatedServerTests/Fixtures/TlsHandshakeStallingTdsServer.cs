// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Holds every TLS handshake a client starts and never answers its ClientHello, like a server whose
/// network path stalls right after the TCP connect. Every connection is held, so a client that retries
/// the login on a new connection stalls again. Disposal drops the held connections.
/// </summary>
internal sealed class TlsHandshakeStallingTdsServer : TdsServer
{
    private readonly ManualResetEventSlim _release = new(false);
    private readonly TaskCompletionSource<bool> _stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public TlsHandshakeStallingTdsServer(TdsServerArguments arguments) : base(arguments)
    {
    }

    /// <summary>
    /// Completes once the client has sent the start of its TLS handshake and the server holds it.
    /// </summary>
    public Task HandshakeStalled => _stalled.Task;

    /// <summary>
    /// Reads the start of the client's handshake, blocks until disposal, then drops the connection
    /// like a network failure.
    /// </summary>
    public override void OnTlsHandshakeStarting(ITDSServerSession session, Stream transport)
    {
        if (transport.ReadByte() >= 0)
        {
            _stalled.TrySetResult(true);
        }

        _release.Wait();
        throw new IOException("The stalled TLS handshake was dropped when the server stopped.");
    }

    /// <summary>
    /// Releases held handshakes before joining their workers; no protocol worker can outlive the server.
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
