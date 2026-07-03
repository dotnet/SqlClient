// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Regression tests for ADO.Net work item 43847 / ICM 775308542:
/// "SNIClose can deadlock with in-flight async I/O during connection close".
///
/// <para>
/// These tests reproduce the ordering hazard where a connection is closed or
/// disposed while an asynchronous network read is still pending. The close
/// path must drain (or cancel) the in-flight async I/O before releasing the
/// SNI handle; if it releases the handle first, the closing thread and the
/// pending completion callback can wait on each other and deadlock.
/// </para>
///
/// <para>
/// Each test uses an in-process TDS server whose query engine deliberately
/// stalls after receiving the client's SQL batch. This guarantees that the
/// client's async read for the response is genuinely in flight at the moment
/// the connection is closed. The close is performed on a worker thread and
/// wrapped in a bounded wait: a healthy close completes promptly, whereas a
/// deadlock manifests as the wait timing out, which fails the test.
/// </para>
///
/// <para>
/// <b>Expected state:</b> these tests PASS against the current code base.
/// Microsoft.Data.SqlClient does not suffer from this deadlock: the close path
/// drains (or cancels) the in-flight async I/O and completes well within the
/// bounded wait. They therefore serve as regression guards - if a future change
/// reintroduced the SNIClose deadlock, the bounded wait would time out and the
/// tests would fail.
/// </para>
/// </summary>
public class SNICloseDeadlockTest
{
    /// <summary>
    /// Upper bound for how long a non-deadlocked close should take. Close of a
    /// healthy connection is effectively instantaneous; this generous budget
    /// exists only to distinguish "completed" from "deadlocked" on slow CI
    /// agents without hanging the whole test run.
    /// </summary>
    private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound for waiting on the server to receive the client's batch,
    /// i.e. for the client's async read to become pending.
    /// </summary>
    private static readonly TimeSpan HandshakeBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A query engine that signals when a SQL batch has arrived and then blocks
    /// (withholding the response) until the test releases it. While it is
    /// blocked, the client's async read for the response remains in flight.
    /// </summary>
    private sealed class StallingQueryEngine : QueryEngine
    {
        private readonly ManualResetEventSlim _batchReceived;
        private readonly ManualResetEventSlim _releaseResponse;

        public StallingQueryEngine(
            TdsServerArguments arguments,
            ManualResetEventSlim batchReceived,
            ManualResetEventSlim releaseResponse)
            : base(arguments)
        {
            _batchReceived = batchReceived;
            _releaseResponse = releaseResponse;
        }

        protected override TDSMessageCollection CreateQueryResponse(
            ITDSServerSession session,
            TDSSQLBatchToken batchRequest)
        {
            // The client has sent the batch and posted an async receive for the
            // response by the time we get here. Signal the test, then stall so
            // the client's read stays in flight while the connection is closed.
            _batchReceived.Set();
            _releaseResponse.Wait();
            return base.CreateQueryResponse(session, batchRequest);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseOrDispose_WithPendingAsyncRead_DoesNotDeadlock(bool disposeInsteadOfClose)
    {
        using ManualResetEventSlim batchReceived = new(false);
        using ManualResetEventSlim releaseResponse = new(false);

        TdsServerArguments arguments = new();
        using TdsServer server = new(
            new StallingQueryEngine(arguments, batchReceived, releaseResponse),
            arguments);
        server.Start();

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            // Disable pooling so Close()/Dispose() tears down the physical
            // connection (and reaches SNIClose) instead of returning it to the
            // pool.
            Pooling = false,
#if NETFRAMEWORK
            TransparentNetworkIPResolution = false,
#endif
        };

        SqlConnection connection = new(builder.ConnectionString);
        connection.Open();

        SqlCommand command = new("SELECT 1", connection);

        // Start an async read that will pend: the server stalls in
        // CreateQueryResponse, so this Task will not complete until we either
        // release the server or tear down the connection.
        Task<SqlDataReader> readTask = command.ExecuteReaderAsync();

        // Ensure the async read is genuinely in flight before we close.
        Assert.True(
            batchReceived.Wait(HandshakeBudget),
            "The server never received the SQL batch, so the async read was " +
            "not in flight; the test cannot exercise the close-with-pending-IO path.");

        // The server is stalled withholding the response, so the client's async
        // read must still be pending: the Task cannot have completed and the
        // connection must still be open. If either of these is false, the read
        // is not actually in flight and the test is not exercising the
        // close-with-pending-IO path.
        Assert.False(
            readTask.IsCompleted,
            "The async read completed before the server released the response; " +
            "the read was not in flight at close time.");
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        // Close/dispose the connection on a worker thread while the async read
        // is in flight. This is the operation that can deadlock in SNIClose.
        Task closeTask = Task.Run(() =>
        {
            if (disposeInsteadOfClose)
            {
                connection.Dispose();
            }
            else
            {
                connection.Close();
            }
        });

        bool closedInTime = closeTask.Wait(CloseBudget);

        // Always release the stalled server thread so it can unwind and the
        // server can be disposed cleanly, regardless of the outcome above.
        releaseResponse.Set();

        // Observe the read task so its (expected) failure is not unobserved.
        try
        {
            readTask.Wait(CloseBudget);
        }
        catch
        {
            // The pending read is expected to fault or cancel once the
            // connection is torn down. That is not what this test asserts.
        }
        finally
        {
            command.Dispose();
            // Only perform potentially-blocking cleanup if the close completed.
            // If it deadlocked (closedInTime == false), calling Dispose() here
            // could also block indefinitely and defeat the bounded-wait
            // regression signal asserted below.
            if (closedInTime && !disposeInsteadOfClose)
            {
                connection.Dispose();
            }
        }

        Assert.True(
            closedInTime,
            $"{(disposeInsteadOfClose ? "Dispose()" : "Close()")} did not complete " +
            $"within {CloseBudget.TotalSeconds:N0}s while an async read was in flight. " +
            "This indicates the SNIClose deadlock (ADO.Net #43847 / ICM 775308542).");
    }

    /// <summary>
    /// Reproduces the ICM scenario more faithfully: the connection is torn down
    /// while it is still in the middle of connection establishment (the
    /// pre-login / TLS handshake), rather than after a successful login.
    ///
    /// <para>
    /// A bare TCP listener accepts the socket and reads the client's pre-login
    /// packet but deliberately never sends a response. This leaves the client's
    /// async read for the pre-login/handshake response in flight. The close is
    /// performed on a worker thread under a bounded wait; a deadlock in the
    /// handshake-phase close path manifests as the wait timing out.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseOrDispose_DuringPreLoginHandshake_DoesNotDeadlock(bool disposeInsteadOfClose)
    {
        using ManualResetEventSlim clientConnected = new(false);
        using ManualResetEventSlim releaseServer = new(false);

        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Server: accept the socket, read the client's pre-login bytes, then
        // withhold any response so the client's handshake read stays pending.
        Task serverTask = Task.Run(() =>
        {
            using TcpClient acceptedClient = listener.AcceptTcpClient();
            using NetworkStream stream = acceptedClient.GetStream();

            try
            {
                // Read one byte of the client's pre-login packet. Once this
                // returns the client has sent its pre-login and posted its
                // async read for the response. We only need to know data
                // arrived, so a single byte is sufficient.
                stream.ReadByte();
            }
            catch
            {
                // The client may tear down the socket; ignore.
            }

            clientConnected.Set();

            // Hold the socket open (withholding the response) until the test
            // releases us, so the client's read cannot complete naturally.
            releaseServer.Wait();
        });

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{port}",
            // Force TLS negotiation intent during the handshake, matching the
            // ICM scenario.
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            // Long timeout so the connect attempt does not abort on its own
            // before we get a chance to close it.
            ConnectTimeout = 60,
            ConnectRetryCount = 0,
            Pooling = false,
#if NETFRAMEWORK
            TransparentNetworkIPResolution = false,
#endif
        };

        SqlConnection connection = new(builder.ConnectionString);

        // Begin connection establishment. This will not complete because the
        // server never answers the pre-login handshake.
        Task openTask = connection.OpenAsync();

        // Wait until the server has received the client's pre-login, i.e. the
        // client's handshake read is in flight.
        Assert.True(
            clientConnected.Wait(HandshakeBudget),
            "The server never received the client's pre-login packet, so the " +
            "handshake read was not in flight; the test cannot exercise the " +
            "close-during-handshake path.");

        // Close/dispose the connection on a worker thread while the handshake
        // read is in flight. This is the operation that can deadlock.
        Task closeTask = Task.Run(() =>
        {
            if (disposeInsteadOfClose)
            {
                connection.Dispose();
            }
            else
            {
                connection.Close();
            }
        });

        bool closedInTime = closeTask.Wait(CloseBudget);

        // Release the server thread so it can unwind cleanly regardless of the
        // outcome above.
        releaseServer.Set();

        // Observe the open task so its (expected) failure is not unobserved.
        try
        {
            openTask.Wait(CloseBudget);
        }
        catch
        {
            // OpenAsync is expected to fault or cancel once the connection is
            // torn down. That is not what this test asserts.
        }
        finally
        {
            // Only perform potentially-blocking cleanup if the close completed.
            // If it deadlocked (closedInTime == false), calling Dispose() here
            // could also block indefinitely and defeat the bounded-wait
            // regression signal asserted below.
            if (closedInTime && !disposeInsteadOfClose)
            {
                connection.Dispose();
            }

            try
            {
                serverTask.Wait(CloseBudget);
            }
            catch
            {
                // Ignore server teardown faults.
            }

            listener.Stop();
        }

        Assert.True(
            closedInTime,
            $"{(disposeInsteadOfClose ? "Dispose()" : "Close()")} did not complete " +
            $"within {CloseBudget.TotalSeconds:N0}s while a pre-login response " +
            "read was in flight. This indicates the SNIClose deadlock " +
            "(ADO.Net #43847 / ICM 775308542).");
    }

    /// <summary>
    /// A minimal, protocol-valid TDS PRELOGIN response advertising ENCRYPT_ON
    /// so that a client which requested encryption proceeds into the TLS
    /// handshake. Offsets are relative to the start of the payload (immediately
    /// after the 8-byte TDS packet header):
    ///
    /// <code>
    ///   Option table:
    ///     VERSION     token 0x00, offset 11 (0x000B), length 6
    ///     ENCRYPTION  token 0x01, offset 17 (0x0011), length 1
    ///     TERMINATOR  0xFF
    ///   Data:
    ///     VERSION     6 bytes (17.0.0.0)
    ///     ENCRYPTION  1 byte  (0x01 = ENCRYPT_ON)
    /// </code>
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

    /// <summary>
    /// Reproduces the ICM scenario faithfully: the connection is torn down while
    /// the client is inside the TLS handshake over TDS, with an SNI read pending
    /// for the server's handshake response.
    ///
    /// <para>
    /// A bare TCP listener speaks just enough of the TDS pre-login exchange to
    /// drive the client into the TLS handshake: it reads the client's PRELOGIN
    /// packet, replies with a PRELOGIN response advertising ENCRYPT_ON, then
    /// reads the client's TLS ClientHello (delivered as a TDS pre-login packet)
    /// and deliberately never sends a ServerHello. At that point the client is
    /// blocked inside <c>SslStream.AuthenticateAsClient</c> with an SNI read in
    /// flight on the SSL-over-TDS transport. The close is performed on a worker
    /// thread under a bounded wait; a deadlock in the handshake-phase close path
    /// manifests as the wait timing out.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseOrDispose_DuringTlsHandshake_DoesNotDeadlock(bool disposeInsteadOfClose)
    {
        using ManualResetEventSlim handshakeInFlight = new(false);
        using ManualResetEventSlim releaseServer = new(false);

        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Server: complete the pre-login exchange advertising encryption, read
        // the client's TLS ClientHello, then withhold the ServerHello so the
        // client's handshake read stays pending.
        Task serverTask = Task.Run(() =>
        {
            using TcpClient acceptedClient = listener.AcceptTcpClient();
            using NetworkStream stream = acceptedClient.GetStream();

            byte[] buffer = new byte[4096];
            try
            {
                // Read the client's PRELOGIN packet.
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return;
                }

                // Advertise ENCRYPT_ON so the client begins the TLS handshake.
                stream.Write(s_preLoginEncryptOnResponse, 0, s_preLoginEncryptOnResponse.Length);
                stream.Flush();

                // Read the first byte of the client's TLS ClientHello (wrapped
                // in a TDS pre-login packet). A byte here proves the client
                // accepted the pre-login response and entered the TLS handshake;
                // it is now awaiting the ServerHello with its SNI read pending.
                if (stream.ReadByte() < 0)
                {
                    // Client tore down without sending a ClientHello: the
                    // handshake was never actually in flight.
                    return;
                }

                handshakeInFlight.Set();

                // Withhold the ServerHello until the test releases us.
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
            // Require encryption so the client performs the TLS handshake.
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

        // Begin connection establishment. This will not complete because the
        // server never finishes the TLS handshake.
        Task openTask = connection.OpenAsync();

        // Wait until the client is inside the TLS handshake with a pending read.
        Assert.True(
            handshakeInFlight.Wait(HandshakeBudget),
            "The client never reached the TLS handshake, so no SNI read was in " +
            "flight; the test cannot exercise the close-during-TLS-handshake path.");

        // Close/dispose the connection on a worker thread while the TLS
        // handshake read is in flight. This is the operation that can deadlock.
        Task closeTask = Task.Run(() =>
        {
            if (disposeInsteadOfClose)
            {
                connection.Dispose();
            }
            else
            {
                connection.Close();
            }
        });

        bool closedInTime = closeTask.Wait(CloseBudget);

        // Release the server thread so it can unwind cleanly regardless of the
        // outcome above.
        releaseServer.Set();

        // Observe the open task so its (expected) failure is not unobserved.
        try
        {
            openTask.Wait(CloseBudget);
        }
        catch
        {
            // OpenAsync is expected to fault or cancel once the connection is
            // torn down. That is not what this test asserts.
        }
        finally
        {
            // Only perform potentially-blocking cleanup if the close completed.
            // If it deadlocked (closedInTime == false), calling Dispose() here
            // could also block indefinitely and defeat the bounded-wait
            // regression signal asserted below.
            if (closedInTime && !disposeInsteadOfClose)
            {
                connection.Dispose();
            }

            try
            {
                serverTask.Wait(CloseBudget);
            }
            catch
            {
                // Ignore server teardown faults.
            }

            listener.Stop();
        }

        Assert.True(
            closedInTime,
            $"{(disposeInsteadOfClose ? "Dispose()" : "Close()")} did not complete " +
            $"within {CloseBudget.TotalSeconds:N0}s while a TLS handshake read was " +
            "in flight. This indicates the SNIClose deadlock " +
            "(ADO.Net #43847 / ICM 775308542).");
    }
}
