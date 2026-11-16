// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies that Connect Timeout bounds the TLS handshake of a new connection on managed SNI
/// (GitHub issues #4834 and #5045). A <see cref="TlsHandshakeStallingTdsServer"/> stands in for a
/// server whose network path stalls right after the TCP connect: it never answers the client's TLS
/// ClientHello. Without a bound, the open blocks until the operating system gives up on the
/// socket (about 15 minutes on Linux by default), and with pooling every other open on the
/// pool waits behind it. The open must instead fail like native SNI does: with a wait timeout
/// once the whole Connect Timeout has elapsed, including when MultiSubnetFailover splits the
/// timeout into per-attempt slices.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class TlsHandshakeTimeoutTests
{
    /// <summary>
    /// Connect Timeout used by every connection in this class, in seconds.
    /// </summary>
    private const int ConnectTimeoutSeconds = 2;

    /// <summary>
    /// Lower bound for a timed-out open. The login loop may give up slightly before the timer
    /// expires, but an open that ends much earlier ran the handshake on a single attempt's slice
    /// of the timeout instead of retrying within the full Connect Timeout.
    /// </summary>
    private static readonly TimeSpan MinimumTimeoutElapsed = TimeSpan.FromSeconds(ConnectTimeoutSeconds * 0.75);

    /// <summary>
    /// Upper bound for an open to fail. Generous, so it only separates "honors Connect Timeout"
    /// from "blocks until the operating system gives up on the socket".
    /// </summary>
    private static readonly TimeSpan OpenBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A synchronous open whose TLS handshake (negotiated after PRELOGIN) gets no answer must
    /// fail with a wait timeout once Connect Timeout elapses, with and without the
    /// per-attempt timeout slices of MultiSubnetFailover.
    /// </summary>
    /// <param name="multiSubnetFailover">The MultiSubnetFailover connection option.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Open_ServerStallsTlsHandshake_ThrowsTimeoutAtConnectTimeout(bool multiSubnetFailover)
    {
        // Arrange
        using TlsHandshakeStallingTdsServer server = new(new TdsServerArguments { Encryption = TDSPreLoginTokenEncryptionType.On });
        server.Start();
        using SqlConnection connection = new(ConnectionString(server, SqlConnectionEncryptOption.Mandatory, multiSubnetFailover));

        // Act
        OpenOutcome outcome = RunOpen(() => Task.Run(connection.Open));

        // Assert
        AssertTimedOutAtConnectTimeout(server, outcome);
    }

    /// <summary>
    /// The asynchronous open must honor Connect Timeout during the TLS handshake too, since a
    /// pooled OpenAsync that blocks here holds up every other open queued on the pool.
    /// </summary>
    [Fact]
    public void OpenAsync_ServerStallsTlsHandshake_ThrowsTimeoutAtConnectTimeout()
    {
        // Arrange
        using TlsHandshakeStallingTdsServer server = new(new TdsServerArguments { Encryption = TDSPreLoginTokenEncryptionType.On });
        server.Start();
        using SqlConnection connection = new(ConnectionString(server, SqlConnectionEncryptOption.Mandatory, multiSubnetFailover: false));

        // Act
        OpenOutcome outcome = RunOpen(() => Task.Run(() => connection.OpenAsync()));

        // Assert
        AssertTimedOutAtConnectTimeout(server, outcome);
    }

    /// <summary>
    /// With strict encryption (TDS 8) the TLS handshake is the first exchange on the socket and
    /// runs from a different parser path; a stall there must also fail at Connect Timeout.
    /// </summary>
    [Fact]
    public void Open_StrictEncryptionServerStallsTlsHandshake_ThrowsTimeoutAtConnectTimeout()
    {
        // Arrange
        using TlsHandshakeStallingTdsServer server = new(new TdsServerArguments { TlsFirst = true });
        server.Start();
        using SqlConnection connection = new(ConnectionString(server, SqlConnectionEncryptOption.Strict, multiSubnetFailover: false));

        // Act
        OpenOutcome outcome = RunOpen(() => Task.Run(connection.Open));

        // Assert
        AssertTimedOutAtConnectTimeout(server, outcome);
    }

    /// <summary>
    /// Builds a non-pooled connection string pointing at the server.
    /// </summary>
    /// <param name="server">The server to connect to.</param>
    /// <param name="encrypt">The encryption mode.</param>
    /// <param name="multiSubnetFailover">The MultiSubnetFailover connection option.</param>
    /// <returns>The connection string.</returns>
    private static string ConnectionString(TdsServer server, SqlConnectionEncryptOption encrypt, bool multiSubnetFailover) =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = encrypt,
            ConnectTimeout = ConnectTimeoutSeconds,
            ConnectRetryCount = 0,
            MultiSubnetFailover = multiSubnetFailover,
            Pooling = false,
        }.ConnectionString;

    /// <summary>
    /// Runs an open with managed networking forced on (it is always on outside Windows) and
    /// waits up to <see cref="OpenBudget"/> for it to complete. The switch is held for the whole
    /// open because every login attempt creates a new transport and reads it again.
    /// </summary>
    /// <param name="start">Starts the open on a worker thread.</param>
    /// <returns>The open, whether it completed within the budget, and how long it ran.</returns>
    private static OpenOutcome RunOpen(Func<Task> start)
    {
        using LocalAppContextSwitchesHelper switches = new() { UseManagedNetworking = true };
        Stopwatch elapsed = Stopwatch.StartNew();
        Task open = start();
        bool settled;
        try
        {
            settled = open.Wait(OpenBudget);
        }
        catch (AggregateException)
        {
            settled = true;
        }
        return new OpenOutcome(open, settled, elapsed.Elapsed);
    }

    /// <summary>
    /// Asserts that the client reached the TLS handshake and that the open then failed with a
    /// wait timeout after Connect Timeout elapsed, within the budget.
    /// </summary>
    /// <param name="server">The stalling server.</param>
    /// <param name="outcome">The outcome of the open under test.</param>
    private static void AssertTimedOutAtConnectTimeout(TlsHandshakeStallingTdsServer server, OpenOutcome outcome)
    {
        Assert.True(server.HandshakeStalled.IsCompleted, "The connection never reached the TLS handshake.");
        Assert.True(
            outcome.Settled,
            $"The open did not fail within {OpenBudget.TotalSeconds:N0}s although Connect Timeout is " +
            $"{ConnectTimeoutSeconds}s: the TLS handshake ignores Connect Timeout.");
        SqlException exception = Assert.IsType<SqlException>(outcome.Open.Exception?.InnerException);
        Assert.Equal((int)TdsEnums.SNI_WAIT_TIMEOUT, exception.Number);
        Assert.True(
            outcome.Elapsed >= MinimumTimeoutElapsed,
            $"The open failed after {outcome.Elapsed.TotalMilliseconds:N0}ms, before Connect Timeout " +
            $"({ConnectTimeoutSeconds}s) elapsed: {exception.Message}");
    }

    /// <summary>
    /// The result of <see cref="RunOpen"/>.
    /// </summary>
    /// <param name="Open">The open task.</param>
    /// <param name="Settled">Whether the open completed within <see cref="OpenBudget"/>.</param>
    /// <param name="Elapsed">How long the open ran, or the budget if it did not complete.</param>
    private sealed record OpenOutcome(Task Open, bool Settled, TimeSpan Elapsed);
}

#endif
