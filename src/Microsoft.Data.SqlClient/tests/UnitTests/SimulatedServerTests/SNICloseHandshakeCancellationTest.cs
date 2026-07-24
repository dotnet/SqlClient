// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Regression test for the connection-teardown paths related to ADO.Net #43847 /
/// ICM 775308542. Where <see cref="SNICloseDeadlockTest"/> tears the connection
/// down with an <b>external</b> <c>Close()</c>/<c>Dispose()</c>, this drives the
/// driver's <b>internal</b> abort/close path by cancelling <c>OpenAsync</c> while
/// the TLS handshake read is in flight.
///
/// <para>
/// A bare TCP listener completes just enough of the pre-login exchange to push
/// the client into the TLS handshake (it advertises ENCRYPT_ON, reads the
/// client's ClientHello, then withholds the ServerHello). The client's
/// <c>OpenAsync</c> is then cancelled while that handshake read is pending. The
/// cancellation must abort the in-flight I/O and complete the operation promptly;
/// a deadlock in the internal close path manifests as the bounded wait timing
/// out, which fails the test.
/// </para>
/// </summary>
public class SNICloseHandshakeCancellationTest
{
    /// <summary>
    /// Upper bound for how long the cancelled OpenAsync should take to settle.
    /// Generous so it only distinguishes "completed" from "deadlocked".
    /// </summary>
    private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound for waiting until the client's TLS handshake read is pending.
    /// </summary>
    private static readonly TimeSpan HandshakeBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A minimal, protocol-valid TDS PRELOGIN response advertising ENCRYPT_ON so
    /// a client that requested encryption proceeds into the TLS handshake.
    /// </summary>
    private static readonly byte[] s_preLoginEncryptOnResponse =
    {
        // ---- TDS packet header (8 bytes) ----
        0x12,       // Type: PRELOGIN
        0x01,       // Status: EOM
        0x00, 0x1A, // Length: 26 (8 header + 18 payload), big-endian
        0x00, 0x00, // SPID
        0x01,       // PacketID
        0x00,       // Window
        // ---- PRELOGIN option table ----
        0x00, 0x00, 0x0B, 0x00, 0x06, // VERSION: offset 11, length 6
        0x01, 0x00, 0x11, 0x00, 0x01, // ENCRYPTION: offset 17, length 1
        0xFF,                         // TERMINATOR
        // ---- Option data ----
        0x11, 0x00, 0x00, 0x00, 0x00, 0x00, // VERSION 17.0.0.0
        0x01,                               // ENCRYPTION = ENCRYPT_ON
    };

    [Fact]
    public void CancelOpenAsyncDuringTlsHandshake_DoesNotDeadlock()
    {
        using ManualResetEventSlim handshakeInFlight = new(false);
        using ManualResetEventSlim releaseServer = new(false);

        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Server: complete the pre-login exchange advertising encryption, read the
        // client's TLS ClientHello, then withhold the ServerHello so the client's
        // handshake read stays pending.
        Task serverTask = Task.Run(() =>
        {
            using TcpClient acceptedClient = listener.AcceptTcpClient();
            using NetworkStream stream = acceptedClient.GetStream();
            byte[] buffer = new byte[4096];
            try
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return;
                }

                stream.Write(s_preLoginEncryptOnResponse, 0, s_preLoginEncryptOnResponse.Length);
                stream.Flush();

                // A byte here proves the client entered the TLS handshake and is
                // awaiting the ServerHello with its read pending.
                if (stream.ReadByte() < 0)
                {
                    return;
                }

                handshakeInFlight.Set();
                releaseServer.Wait();
            }
            catch
            {
                // The client may tear down the socket mid-handshake; ignore.
            }
        });

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{port}",
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            ConnectTimeout = 60,
            ConnectRetryCount = 0,
            Pooling = false,
#if NETFRAMEWORK
            TransparentNetworkIPResolution = false,
#endif
        };

        SqlConnection connection = new(builder.ConnectionString);
        using CancellationTokenSource cts = new();
        Task? openTask = null;
        bool cancelAttempted = false;
        bool settledInTime = false;
        try
        {
            openTask = connection.OpenAsync(cts.Token);

            Assert.True(
                handshakeInFlight.Wait(HandshakeBudget),
                "The client never reached the TLS handshake; no read was in flight to cancel.");

            // Cancel while the handshake read is pending: this drives the driver's
            // internal abort/close path against the in-flight I/O.
            cancelAttempted = true;
            cts.Cancel();

            try
            {
                settledInTime = openTask.Wait(CloseBudget);
            }
            catch (AggregateException)
            {
                // OpenAsync faulting/cancelling is the expected outcome; what
                // matters is that it *settled* rather than hanging.
                settledInTime = true;
            }

            Assert.True(
                settledInTime,
                $"OpenAsync did not settle within {CloseBudget.TotalSeconds:N0}s after cancellation while a " +
                "TLS handshake read was in flight. This indicates a deadlock in the internal close path " +
                "(ADO.Net #43847 / ICM 775308542).");
        }
        finally
        {
            releaseServer.Set();
            listener.Stop();

            if (openTask != null)
            {
                try { openTask.Wait(CloseBudget); } catch { /* expected fault/cancel */ }
            }

            // Dispose unless a deadlock was detected (Dispose could also block).
            if (!cancelAttempted || settledInTime)
            {
                connection.Dispose();
            }

            try { serverTask.Wait(CloseBudget); } catch { /* ignore */ }
        }
    }
}
