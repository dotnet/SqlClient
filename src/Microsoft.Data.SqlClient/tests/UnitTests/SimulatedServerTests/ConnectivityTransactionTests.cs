// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.TransactionManager;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>Validates the real transaction wire boundary needed for #4696 instead of substituting a Login7 delay.</summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ConnectivityTransactionTests
{
    private readonly ITestOutputHelper _output;
    public ConnectivityTransactionTests(ITestOutputHelper output) => _output = output;

    /// <summary>Releasing the actual post-BEGIN gate allows ordinary scope and physical-open completion.</summary>
    [Fact]
    public Task OpenAsync_AmbientTransaction_PostBeginGate_HealthyControl_Completes() => GatedEnlistment(false);

    /// <summary>Known defect #4696: actual rollback holds the connection monitor against login's proven parser lock.</summary>
    [Fact]
    [Trait("category", "failing")]
    public Task OpenAsync_AmbientTransaction_CancelledAfterBegin_ScopeAndPhysicalWorkComplete() => GatedEnlistment(true);

    /// <summary>Before BEGIN, public cancellation unwinds the scope even while physical login remains held.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAsync_AmbientTransaction_CancelledBeforeEnlistment_DisposesScope(bool enlist)
    {
        using var server = new GatedConnectionTdsServer(TdsConnectionPhase.Login7);
        server.Start();
        using Process child = ConnectivityProcess.Start(enlist ? "enlist-before-cancel" : "enlist-disabled-cancel", server.EndPoint.Port);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        try
        {
            Assert.Equal("CALL_RETURNED", await Read(child));
            Task arrival = server.WaitForArrivalsAsync(1);
            Assert.Same(arrival, await Task.WhenAny(arrival, Task.Delay(TimeSpan.FromSeconds(15))));
            await arrival;
            child.StandardInput.WriteLine("CANCEL");
            Assert.Equal("PUBLIC_CANCELLED", await Read(child));
            Assert.Equal("SCOPE_COMPLETE", await Read(child));
            server.Release();
            child.StandardInput.WriteLine("FINISH");
            Assert.Equal("SCOPE_AND_PHYSICAL_COMPLETE", await Read(child));
            Assert.True(child.WaitForExit(30_000), "Pre-enlistment child did not exit.");
            Assert.Equal(0, child.ExitCode);
            Assert.Equal(string.Empty, await errors);
            Assert.Equal(1, server.ArrivalCount);
        }
        finally
        {
            server.Release();
            ConnectivityProcess.Stop(child);
            _output.WriteLine(await errors);
        }
    }

    private async Task GatedEnlistment(bool cancel)
    {
        using var server = new GatedTransactionTdsServer();
        server.Start();
        using Process child = ConnectivityProcess.Start(cancel ? "enlist-cancel" : "enlist-control", server.EndPoint.Port);
        Task<string> errors = child.StandardError.ReadToEndAsync();
        try
        {
            Assert.Equal("CALL_RETURNED", await Read(child));
            Assert.Same(server.BeginArrived, await Task.WhenAny(server.BeginArrived, Task.Delay(TimeSpan.FromSeconds(15))));
            await server.BeginArrived;
            Assert.Equal(1, server.BeginCount);
            server.ReleaseBegin();
            Assert.StartsWith("ENLISTED_PARSER_HELD ", await Read(child));
            child.StandardInput.WriteLine(cancel ? "CANCEL" : "FINISH");
            if (cancel)
            {
                Assert.Equal("PUBLIC_CANCELLED", await Read(child));
                Assert.Contains(await Read(child), new[] { "ROLLBACK_OWNS_CONNECTION", "ROLLBACK_RELEASED_CONNECTION" });
                child.StandardInput.WriteLine("FINISH");
            }
            string completion = await Read(child);
            Assert.Equal("SCOPE_AND_PHYSICAL_COMPLETE", completion);
            Assert.True(child.WaitForExit(30_000), "Enlistment child did not exit.");
            Assert.Equal(0, child.ExitCode);
            Assert.Equal(string.Empty, await errors);
            Assert.Equal(cancel ? 0 : 1, server.CommitCount);
        }
        finally
        {
            server.ReleaseBegin();
            ConnectivityProcess.Stop(child);
            _output.WriteLine(await errors);
        }
    }

    private async Task<string> Read(Process child)
    {
        string line = await ConnectivityProcess.ReadAsync(child, TimeSpan.FromSeconds(30));
        _output.WriteLine(line);
        return line;
    }

    /// <summary>Local transaction requests preserve descriptor and operation, with strict truncation handling.</summary>
    [Theory]
    [InlineData(TDSTransactionManagerRequest.Begin)]
    [InlineData(TDSTransactionManagerRequest.Commit)]
    [InlineData(TDSTransactionManagerRequest.Rollback)]
    public void TransactionManager_Request_RoundTripsAndRejectsTruncation(TDSTransactionManagerRequest request)
    {
        var original = new TDSTransactionManagerToken { Request = request, Descriptor = 42, IsolationLevel = 2 };
        using var stream = new MemoryStream();
        original.Deflate(stream);
        stream.Position = 0;
        var parsed = new TDSTransactionManagerToken();
        Assert.True(parsed.Inflate(stream));
        Assert.Equal(original.Request, parsed.Request);
        Assert.Equal(original.Descriptor, parsed.Descriptor);
        Assert.Equal(original.OutstandingRequests, parsed.OutstandingRequests);
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;
        Assert.Throws<EndOfStreamException>(() => parsed.Inflate(stream));
    }

    /// <summary>Both APIs genuinely enlist via BEGIN and finish with COMMIT or ROLLBACK, with and without pooling.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Open_AmbientTransaction_UsesActualBeginAndCompletion(bool async, bool pooling, bool complete)
    {
        using var server = new GatedTransactionTdsServer();
        server.ReleaseBegin();
        server.Start();
        using var connection = new SqlConnection(ConnectionString(server, pooling));
        try
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
            {
                if (async)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                Assert.Equal(1, server.BeginCount);
                if (complete)
                {
                    scope.Complete();
                }
            }
            Assert.Equal(complete ? 1 : 0, server.CommitCount);
            Assert.Equal(complete ? 0 : 1, server.RollbackCount);
        }
        finally
        {
            connection.Close();
            SqlConnection.ClearPool(connection);
        }
    }

    /// <summary>The gate observes transaction BEGIN only after login; releasing it lets the actual ambient open finish.</summary>
    [Fact]
    public async Task OpenAsync_AmbientTransaction_HeldBegin_CompletesAfterRelease()
    {
        using var server = new GatedTransactionTdsServer();
        server.Start();
        using var connection = new SqlConnection(ConnectionString(server, false));
        using var scope = new TransactionScope(TransactionScopeOption.RequiresNew,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled);
        Task open = connection.OpenAsync();
        try
        {
            Assert.Same(server.BeginArrived, await Task.WhenAny(server.BeginArrived, Task.Delay(TimeSpan.FromSeconds(30))));
            await server.BeginArrived;
            Assert.False(open.IsCompleted);
            server.ReleaseBegin();
            Assert.Same(open, await Task.WhenAny(open, Task.Delay(TimeSpan.FromSeconds(30))));
            await open;
            scope.Complete();
        }
        finally
        {
            server.ReleaseBegin();
            await open;
        }
    }

    /// <summary>Uses only a fake local peer; ambient enlistment remains explicitly enabled.</summary>
    internal static string ConnectionString(GatedTransactionTdsServer server, bool pooling) => new SqlConnectionStringBuilder
    {
        DataSource = $"127.0.0.1,{server.EndPoint.Port}",
        Pooling = pooling,
        Enlist = true,
        Encrypt = SqlConnectionEncryptOption.Optional,
        ConnectRetryCount = 0,
        ConnectTimeout = 30
    }.ConnectionString;
}
