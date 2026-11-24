// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies simulator scheduling and fault ownership without relying on connection timeouts.
/// </summary>
[Collection("SimulatedServerTests")]
public class ServerLifecycleTests
{
#if NET
    /// <summary>
    /// Client certificate rejection is a transport outcome, not a simulator teardown failure.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TlsHandshake_CertificateValidation_DoesNotFailDisposal(bool trustServerCertificate, bool useAsync)
    {
        using X509Certificate2 certificate = CreateServerCertificate();
        using WorkerTrackingServer server = new(new TdsServerArguments
        {
            Encryption = TDSPreLoginTokenEncryptionType.On,
            EncryptionCertificate = certificate,
            ExpectTlsHandshakeFailure = !trustServerCertificate
        });
        server.Start();
        using SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            HostNameInCertificate = "localhost",
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = trustServerCertificate,
            Pooling = false,
            ConnectRetryCount = 0
        }.ConnectionString);

        if (trustServerCertificate)
        {
            if (useAsync)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }
            Assert.Equal(1, server.Login7Count);
        }
        else
        {
            SqlException error = useAsync
                ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                : Assert.Throws<SqlException>(() => connection.Open());
            Assert.Contains("certificate", error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, server.Login7Count);
        }

        connection.Close();
        Assert.True(SpinWait.SpinUntil(() => server.ActiveSessions == 0, TimeSpan.FromSeconds(30)),
            "The server did not finish processing the TLS connection.");
        server.Dispose();
    }

    /// <summary>
    /// A peer TLS alert is tolerated only when the scenario explicitly expects handshake failure.
    /// Keeping the peer socket open ensures the alert is processed before server teardown.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TlsHandshake_PeerAlert_RequiresExplicitExpectation(bool expectedFailure)
    {
        using X509Certificate2 certificate = CreateServerCertificate();
        using WorkerTrackingServer server = new(new TdsServerArguments
        {
            Encryption = TDSPreLoginTokenEncryptionType.On,
            EncryptionCertificate = certificate,
            ExpectTlsHandshakeFailure = expectedFailure
        });
        server.Start();
        using TcpClient peer = new();
        peer.Connect("127.0.0.1", server.EndPoint.Port);
        using TDSStream transport = new(peer.GetStream(), false) { PacketSize = 4096 };
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.On)).Deflate(transport);
        using AutoTDSStream tls = new(transport, false) { OutgoingMessageType = TDSMessageType.PreLogin };
        byte[] alert = { 0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x2A }; // TLS 1.2 fatal bad_certificate alert.
        tls.Write(alert, 0, alert.Length);
        tls.Flush();

        Assert.True(server.SessionClosed.Wait(TimeSpan.FromSeconds(30)), "The peer TLS alert was not processed.");
        Assert.Equal(0, server.Login7Count);
        if (expectedFailure)
        {
            server.Dispose();
        }
        else
        {
            AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());
            Assert.Contains(error.Flatten().InnerExceptions,
                exception => exception is AuthenticationException);
        }
    }

    /// <summary>
    /// A certificate without its private key is a server setup error, not a peer rejection.
    /// </summary>
    [Fact]
    public void TlsHandshake_MissingPrivateKey_ReportsSetupFailure()
    {
        using X509Certificate2 certificate = CreateServerCertificate();
        using X509Certificate2 publicCertificate =
#if NET9_0_OR_GREATER
            X509CertificateLoader.LoadCertificate(certificate.RawData);
#else
            new X509Certificate2(certificate.RawData);
#endif
        using TdsServer server = new(new TdsServerArguments
        {
            Encryption = TDSPreLoginTokenEncryptionType.On,
            EncryptionCertificate = publicCertificate,
            ExpectTlsHandshakeFailure = true
        });
        server.Start();
        using SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectRetryCount = 0
        }.ConnectionString);

        Assert.Throws<SqlException>(() => connection.Open());
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());
        Assert.Contains(error.Flatten().InnerExceptions,
            exception => exception is AuthenticationException);
    }

    /// <summary>
    /// Creates a short-lived certificate without installing it in a machine or user trust store.
    /// </summary>
    private static X509Certificate2 CreateServerCertificate()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Schannel requires an imported key rather than the ephemeral key returned above.
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
#else
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
#endif
    }
#endif

    /// <summary>
    /// Negotiating TLS without a configured certificate must still fail simulator teardown.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TlsHandshake_MissingCertificate_ReportsSetupFailure(bool useAsync)
    {
        using TdsServer server = new(new TdsServerArguments
        {
            Encryption = TDSPreLoginTokenEncryptionType.On,
            ExpectTlsHandshakeFailure = true
        });
        server.Start();
        using SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectRetryCount = 0
        }.ConnectionString);

        if (useAsync)
        {
            await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());
        }
        else
        {
            Assert.Throws<SqlException>(() => connection.Open());
        }
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());
        Assert.Contains(error.Flatten().InnerExceptions,
            exception => exception is AuthenticationException);
    }

    /// <summary>
    /// Omitting encryption metadata must not start TLS after the client rejects the pre-login response.
    /// </summary>
    [Theory]
    [InlineData(TDSPreLoginTokenEncryptionType.On)]
    [InlineData(TDSPreLoginTokenEncryptionType.Off)]
    [InlineData(TDSPreLoginTokenEncryptionType.NotSupported)]
    public void MissingEncryptionOption_DoesNotNegotiateTls(TDSPreLoginTokenEncryptionType client)
    {
        Assert.Equal(TDSEncryptionType.None,
            TDSUtilities.ResolveEncryption(client, TDSPreLoginTokenEncryptionType.None));
    }

    /// <summary>
    /// Blocking server I/O must not consume the same worker pool as client opens and continuations.
    /// </summary>
    [Fact]
    public void ConnectionHandler_DoesNotBlockThreadPool()
    {
        using WorkerTrackingServer server = new();
        server.Start();
        using SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        }.ConnectionString);
        connection.Open();
        Assert.False(server.IsThreadPoolWorker);
    }

    /// <summary>
    /// A request owns its fault before preparing a response, even if response logging is blocked.
    /// </summary>
    [Fact]
    public void TransientError_ConcurrentRequests_ConsumeFaultOnce()
    {
        using GatedWriter log = new();
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments
        {
            IsEnabledTransientError = true,
            Number = 40613,
            Log = log
        });
        Task<TDSMessageCollection> first = Task.Factory.StartNew(
            () => server.OnSQLBatchRequest(server.OpenSession(), Batch()),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        TDSMessageCollection second = null;
        Exception secondError = null;
        Thread secondThread = new(() =>
        {
            try
            {
                second = server.OnSQLBatchRequest(server.OpenSession(), Batch());
            }
            catch (Exception ex)
            {
                secondError = ex;
            }
        }) { IsBackground = true };
        try
        {
            Assert.True(log.Entered.Wait(TimeSpan.FromSeconds(30)), "First response was not prepared.");
            secondThread.Start();
            // Logging serializes writers. Wait until request two reaches that lock, after
            // choosing its response, before allowing request one to finish its response.
            Assert.True(SpinWait.SpinUntil(
                () => (secondThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(30)), "Second response did not reach the logging gate.");
        }
        finally
        {
            log.Release.Set();
            Assert.True(first.Wait(TimeSpan.FromSeconds(30)), "First response did not finish.");
            if ((secondThread.ThreadState & ThreadState.Unstarted) == 0)
            {
                Assert.True(secondThread.Join(TimeSpan.FromSeconds(30)), "Second response did not finish.");
            }
        }

        Assert.Null(secondError);
        Assert.NotNull(second);
        Assert.DoesNotContain(second.SelectMany(message => message), token => token is TDSErrorToken);
        Assert.Single(first.Result.SelectMany(message => message).OfType<TDSErrorToken>());
    }

    /// <summary>
    /// Rearming an exhausted fault budget applies the new error only to the next request.
    /// </summary>
    [Fact]
    public void TransientError_Reconfigure_StartsNewFaultBudget()
    {
        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments
        {
            IsEnabledTransientError = true,
            Number = 40613
        });
        Assert.Equal((uint)40613, ErrorNumber());
        server.SetErrorBehavior(true, 42108);
        Assert.Equal((uint)42108, ErrorNumber());
        Assert.DoesNotContain(server.OnSQLBatchRequest(server.OpenSession(), Batch())
            .SelectMany(message => message), token => token is TDSErrorToken);

        uint ErrorNumber() => Assert.Single(server.OnSQLBatchRequest(server.OpenSession(), Batch())
            .SelectMany(message => message).OfType<TDSErrorToken>()).Number;
    }

    /// <summary>
    /// A blocked transient delay owns its quota, so the next request is not delayed too.
    /// Cancelling the server releases the first request without generating a late response.
    /// </summary>
    [Fact]
    public void TransientDelay_ConcurrentRequests_ReserveDelayBeforeWaiting()
    {
        using TransientDelayTdsServer server = new(new TransientDelayTdsServerArguments
        {
            IsEnabledTransientDelay = true,
            DelayDuration = Timeout.InfiniteTimeSpan
        });
        using ManualResetEventSlim started = new();
        Thread worker = null;
        Task<TDSMessageCollection> first = Task.Factory.StartNew(() =>
        {
            worker = Thread.CurrentThread;
            started.Set();
            return server.OnSQLBatchRequest(server.OpenSession(), Batch());
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<TDSMessageCollection> second = null;
        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(30)));
            Assert.True(SpinWait.SpinUntil(
                () => (worker!.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(30)), "First request did not enter the delay.");
            second = Task.Factory.StartNew(
                () => server.OnSQLBatchRequest(server.OpenSession(), Batch()),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(second.Wait(TimeSpan.FromSeconds(30)), "Second request also consumed the one-shot delay.");
            Assert.NotEmpty(second.Result);
        }
        finally
        {
            server.Dispose();
            Assert.True(first.Wait(TimeSpan.FromSeconds(30)), "Disposal did not release the delayed request.");
            if (second != null)
            {
                Assert.True(second.Wait(TimeSpan.FromSeconds(30)));
            }
        }
        Assert.Empty(first.Result);
    }

    /// <summary>
    /// Disposal drains both active protocol sessions and clients that never send pre-login data.
    /// Repeated lifetimes expose registration/close races within one process.
    /// </summary>
    [Fact]
    [Trait("category", "flaky")]
    [Trait("category", "probation")]
    public void Dispose_DrainsConnectionsAcrossRepeatedServerLifetimes()
    {
        // Arrange
        for (int iteration = 0; iteration < 20; iteration++)
        {
            using WorkerTrackingServer server = new();
            server.Start();
            using SqlConnection connection = new(new SqlConnectionStringBuilder
            {
                DataSource = $"127.0.0.1,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false
            }.ConnectionString);
            connection.Open();
            using TcpClient idle = new();
            idle.Connect("127.0.0.1", server.EndPoint.Port);

            // Act
            server.Dispose();

            // Assert
            Assert.Equal(0, server.ActiveSessions);
        }
    }

    /// <summary>
    /// Unexpected protocol-handler failures must fail the test even when no log writer is configured.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Dispose_ReportsUnexpectedWorkerFailure(bool failOnClose, bool authenticationFailure)
    {
        // Arrange
        using ThrowingServer server = new(failOnClose, authenticationFailure);
        server.Start();
        using TcpClient peer = new();
        peer.Connect("127.0.0.1", server.EndPoint.Port);
        using TDSStream transport = new(peer.GetStream(), false) { PacketSize = 4096 };

        // Act
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(transport);
        Assert.True(server.FailureTriggered.Wait(TimeSpan.FromSeconds(30)),
            "The protocol worker did not reach the injected failure.");
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());

        // Assert
        Assert.Contains(error.Flatten().InnerExceptions,
            exception => exception.Message == "Intentional protocol failure." &&
                (authenticationFailure ? exception is AuthenticationException : exception is InvalidOperationException));
        if (failOnClose)
        {
            Assert.Contains(error.Flatten().InnerExceptions,
                exception => exception.Message == "Intentional session cleanup failure.");
        }
    }

    /// <summary>
    /// Constructs a valid request without requiring socket scheduling for a fault-budget test.
    /// </summary>
    private static TDSMessage Batch() => new(TDSMessageType.SQLBatch, new TDSSQLBatchToken { Text = "SELECT 1" });

    private sealed class WorkerTrackingServer : TdsServer
    {
        private int _activeSessions;
        private readonly TaskCompletionSource<bool> _sessionClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ActiveSessions => Volatile.Read(ref _activeSessions);
        public Task SessionClosed => _sessionClosed.Task;
        public bool IsThreadPoolWorker { get; private set; }

        public WorkerTrackingServer()
        {
        }

        public WorkerTrackingServer(TdsServerArguments arguments) : base(arguments)
        {
        }

        public override ITDSServerSession OpenSession()
        {
            Interlocked.Increment(ref _activeSessions);
            return base.OpenSession();
        }

        public override void CloseSession(ITDSServerSession session)
        {
            Interlocked.Decrement(ref _activeSessions);
            base.CloseSession(session);
            _sessionClosed.TrySetResult(true);
        }

        protected override TDSMessageCollection OnPreLoginRequestCore(ITDSServerSession session, TDSMessage request)
        {
            IsThreadPoolWorker = Thread.CurrentThread.IsThreadPoolThread;
            return base.OnPreLoginRequestCore(session, request);
        }
    }

    private sealed class ThrowingServer : TdsServer
    {
        private readonly bool _failOnClose;
        private readonly bool _authenticationFailure;
        private readonly TaskCompletionSource<bool> _failureTriggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task FailureTriggered => _failureTriggered.Task;

        public ThrowingServer(bool failOnClose, bool authenticationFailure)
            : base(new TdsServerArguments { ExpectTlsHandshakeFailure = true })
        {
            _failOnClose = failOnClose;
            _authenticationFailure = authenticationFailure;
        }

        protected override TDSMessageCollection OnPreLoginRequestCore(ITDSServerSession session, TDSMessage request)
        {
            _failureTriggered.TrySetResult(true);
            throw (_authenticationFailure
                ? new AuthenticationException("Intentional protocol failure.")
                : new InvalidOperationException("Intentional protocol failure."));
        }

        public override void CloseSession(ITDSServerSession session)
        {
            base.CloseSession(session);
            if (_failOnClose)
            {
                throw new InvalidOperationException("Intentional session cleanup failure.");
            }
        }
    }

    /// <summary>
    /// Handler I/O failures must not be mistaken for transport disconnects, even during shutdown.
    /// </summary>
    [Fact]
    public void Dispose_HandlerIOException_ReportsWorkerFailure()
    {
        // Arrange
        IOException failure = new("Intentional handler I/O failure.");
        using IoFailureServer server = new(failure);
        server.Start();
        using TcpClient peer = new();
        peer.Connect("127.0.0.1", server.EndPoint.Port);
        using TDSStream transport = new(peer.GetStream(), false) { PacketSize = 4096 };

        // Act
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(transport);
        Assert.True(server.FailureTriggered.Wait(TimeSpan.FromSeconds(30)),
            "The protocol worker did not reach the injected failure.");
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());

        // Assert
        Assert.Contains(failure, error.Flatten().InnerExceptions);
    }

    /// <summary>
    /// Socket exceptions raised by handlers are failures, not evidence that the peer disconnected.
    /// </summary>
    [Fact]
    public void Dispose_HandlerSocketException_ReportsWorkerFailure()
    {
        // Arrange
        SocketException failure = new((int)SocketError.ConnectionReset);
        using IoFailureServer server = new(failure);
        server.Start();
        using TcpClient peer = new();
        peer.Connect("127.0.0.1", server.EndPoint.Port);
        using TDSStream transport = new(peer.GetStream(), false) { PacketSize = 4096 };

        // Act
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(transport);
        Assert.True(server.FailureTriggered.Wait(TimeSpan.FromSeconds(30)));
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());

        // Assert
        Assert.Contains(failure, error.Flatten().InnerExceptions);
    }

    /// <summary>
    /// Protocol logging and a subsequent diagnostic logging failure must both survive teardown.
    /// </summary>
    [Fact]
    public void Dispose_LogIOException_ReportsOriginalAndDiagnosticFailures()
    {
        // Arrange
        using FailingWriter log = new();
        using TdsServer server = new(new TdsServerArguments { Log = log });
        server.Start();
        using TcpClient peer = new();
        peer.Connect("127.0.0.1", server.EndPoint.Port);
        using TDSStream transport = new(peer.GetStream(), false) { PacketSize = 4096 };

        // Act
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(transport);
        Assert.True(log.FailureTriggered.Wait(TimeSpan.FromSeconds(30)));
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());

        // Assert
        Assert.Contains(log.ProtocolFailure, error.Flatten().InnerExceptions);
        Assert.Contains(log.DiagnosticFailure, error.Flatten().InnerExceptions);
    }

    /// <summary>
    /// A handler may reset its own socket without joining itself or disconnecting another active peer.
    /// </summary>
    [Fact]
    public void AbortConnection_SelectedPreLogin_ResetsOnlySelectedPeer()
    {
        // Arrange
        using AbortFirstServer server = new();
        server.Start();
        using TcpClient first = new();
        first.Connect("127.0.0.1", server.EndPoint.Port);
        using TcpClient second = new();
        second.Connect("127.0.0.1", server.EndPoint.Port);
        Assert.True(SpinWait.SpinUntil(() => server.ActiveSessions == 2, TimeSpan.FromSeconds(30)));
        using NetworkStream firstRaw = first.GetStream();
        using NetworkStream secondRaw = second.GetStream();
        firstRaw.ReadTimeout = secondRaw.ReadTimeout = 5000;
        using TDSStream firstTransport = new(firstRaw, true) { PacketSize = 4096 };
        using TDSStream secondTransport = new(secondRaw, true) { PacketSize = 4096 };

        // Act
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(firstTransport);
        Assert.True(server.FirstClosed.Wait(TimeSpan.FromSeconds(30)), "Abort did not finish the selected worker.");
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(secondTransport);

        // Assert
        int firstByte = 0;
        Exception disconnect = Record.Exception(() => firstByte = firstRaw.ReadByte());
        if (disconnect == null)
        {
            Assert.Equal(-1, firstByte);
        }
        else
        {
            IOException reset = Assert.IsType<IOException>(disconnect);
            SocketException socketError = Assert.IsType<SocketException>(reset.InnerException);
            Assert.Equal(SocketError.ConnectionReset, socketError.SocketErrorCode);
        }
        Assert.NotEqual(-1, secondRaw.ReadByte());
        Assert.NotEqual(server.FirstSessionId, server.SecondSessionId);
        Assert.Equal(1, server.ActiveSessions);
        server.Dispose();
        Assert.Equal(0, server.ActiveSessions);
    }

    /// <summary>
    /// Deliberate transport shutdown must not hide a handler failure raised after resetting the socket.
    /// </summary>
    [Fact]
    public void AbortConnection_HandlerFailsAfterAbort_ReportsFailure()
    {
        // Arrange
        using ThrowingAfterAbortServer server = new();
        server.Start();
        using TcpClient peer = new();
        peer.Connect("127.0.0.1", server.EndPoint.Port);
        using TDSStream transport = new(peer.GetStream(), false) { PacketSize = 4096 };

        // Act
        new TDSMessage(TDSMessageType.PreLogin,
            new TDSPreLoginToken(new Version(11, 0, 0, 0), TDSPreLoginTokenEncryptionType.Off)).Deflate(transport);
        Assert.True(server.FirstClosed.Wait(TimeSpan.FromSeconds(30)));
        AggregateException error = Assert.Throws<AggregateException>(() => server.Dispose());

        // Assert
        Assert.Contains(server.Failure, error.Flatten().InnerExceptions);
        Assert.Equal(0, server.ActiveSessions);
    }

    /// <summary>Resets the first pre-login session while observing normal session cleanup.</summary>
    private class AbortFirstServer : TdsServer
    {
        private int _activeSessions;
        private int _requests;
        private readonly TaskCompletionSource<bool> _firstClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ActiveSessions => Volatile.Read(ref _activeSessions);
        public Task FirstClosed => _firstClosed.Task;
        public uint FirstSessionId { get; private set; }
        public uint SecondSessionId { get; private set; }

        public override ITDSServerSession OpenSession()
        {
            Interlocked.Increment(ref _activeSessions);
            return base.OpenSession();
        }

        public override void CloseSession(ITDSServerSession session)
        {
            base.CloseSession(session);
            Interlocked.Decrement(ref _activeSessions);
            if (session.SessionID == FirstSessionId)
            {
                _firstClosed.TrySetResult(true);
            }
        }

        protected override TDSMessageCollection OnPreLoginRequestCore(ITDSServerSession session, TDSMessage request)
        {
            if (Interlocked.Increment(ref _requests) == 1)
            {
                FirstSessionId = session.SessionID;
                AbortConnection(session);
                return new TDSMessageCollection();
            }
            SecondSessionId = session.SessionID;
            return base.OnPreLoginRequestCore(session, request);
        }
    }

    /// <summary>Raises an unexpected I/O failure after a deliberate transport abort.</summary>
    private sealed class ThrowingAfterAbortServer : AbortFirstServer
    {
        public IOException Failure { get; } = new("Intentional failure after transport abort.");

        protected override TDSMessageCollection OnPreLoginRequestCore(ITDSServerSession session, TDSMessage request)
        {
            base.OnPreLoginRequestCore(session, request);
            throw Failure;
        }
    }

    private sealed class IoFailureServer : TdsServer
    {
        private readonly Exception _failure;
        private readonly TaskCompletionSource<bool> _failureTriggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task FailureTriggered => _failureTriggered.Task;

        public IoFailureServer(Exception failure)
            : base(new TdsServerArguments { ExpectTlsHandshakeFailure = true })
            => _failure = failure;

        protected override TDSMessageCollection OnPreLoginRequestCore(ITDSServerSession session, TDSMessage request)
        {
            _failureTriggered.TrySetResult(true);
            throw _failure;
        }
    }

    private sealed class FailingWriter : TextWriter
    {
        private readonly TaskCompletionSource<bool> _failureTriggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task FailureTriggered => _failureTriggered.Task;
        public IOException ProtocolFailure { get; } = new("Intentional protocol log failure.");
        public IOException DiagnosticFailure { get; } = new("Intentional diagnostic log failure.");
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value)
        {
            _failureTriggered.TrySetResult(true);
            throw ProtocolFailure;
        }

        public override void WriteLine(string format, params object[] arg)
        {
            if (format.Contains(ProtocolFailure.Message))
            {
                throw DiagnosticFailure;
            }
        }
    }

    private sealed class GatedWriter : TextWriter
    {
        private int _writes;
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value)
        {
            if (Interlocked.Increment(ref _writes) == 1)
            {
                Entered.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Response logging was not released.");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Entered.Dispose();
                Release.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
