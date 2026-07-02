// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
/// wrapped in a bounded wait: a deadlock manifests as the wait timing out,
/// which fails the test. After the fix, the close completes promptly.
/// </para>
///
/// <para>
/// <b>Expected state:</b> these tests are expected to FAIL (time out) against
/// the current, unfixed close path and PASS once the drain-before-close fix is
/// implemented.
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

    [Fact]
    public void CloseConnection_WithPendingAsyncRead_DoesNotDeadlock()
    {
        RunPendingAsyncReadCloseScenario(disposeInsteadOfClose: false);
    }

    [Fact]
    public void DisposeConnection_WithPendingAsyncRead_DoesNotDeadlock()
    {
        RunPendingAsyncReadCloseScenario(disposeInsteadOfClose: true);
    }

    private static void RunPendingAsyncReadCloseScenario(bool disposeInsteadOfClose)
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
            if (!disposeInsteadOfClose)
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
}
