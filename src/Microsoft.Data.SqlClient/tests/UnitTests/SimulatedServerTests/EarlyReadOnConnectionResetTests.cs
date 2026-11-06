// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if DEBUG
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// When SQL Server refuses to reset a pooled connection it answers the first request after
/// checkout with error tokens and then ends the connection abortively, which discards anything
/// the client has not read yet. The driver must therefore have a read pending before such a
/// request is sent; otherwise the caller gets a transport error instead of the server's error.
/// These tests stand in for a cold (still compiling) reader by holding back any read posted on
/// demand until the server has answered and aborted the connection.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class EarlyReadOnConnectionResetTests
{
    private static readonly TimeSpan s_abortWait = TimeSpan.FromSeconds(30);

    [Fact]
    public void ExecuteScalar_ResetRefusedAndReadHeldBack_ReportsServerError()
    {
        // Arrange
        using ResetRefusingTdsServer server = new();
        server.Start();
        string connectionString = PooledConnectionString(server);
        ReturnConnectionToPool(connectionString);

        // Act
        SqlException exception;
        using (ReadsHeldBackUntilAbort(server))
        using (SqlConnection connection = new(connectionString))
        {
            connection.Open();
            using SqlCommand command = new("SELECT 1", connection);
            exception = Assert.Throws<SqlException>(() => command.ExecuteScalar());
        }

        // Assert
        AssertServerErrorReported(server, exception);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ResetRefusedAndReadHeldBack_ReportsServerError()
    {
        // Arrange
        using ResetRefusingTdsServer server = new();
        server.Start();
        string connectionString = PooledConnectionString(server);
        ReturnConnectionToPool(connectionString);

        // Act
        SqlException exception;
        using (ReadsHeldBackUntilAbort(server))
        using (SqlConnection connection = new(connectionString))
        {
            await connection.OpenAsync();
            using SqlCommand command = new("SELECT 1", connection);
            exception = await Assert.ThrowsAsync<SqlException>(
                () => command.ExecuteScalarAsync());
        }

        // Assert
        AssertServerErrorReported(server, exception);
    }

    [Fact]
    public void ExecuteScalar_PooledReuse_ReturnsResult()
    {
        // Arrange: a server that accepts resets; the early read must not disturb a normal reuse.
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        string connectionString = PooledConnectionString(server);
        ReturnConnectionToPool(connectionString);

        // Act
        object? first;
        object? second;
        using (SqlConnection connection = new(connectionString))
        {
            connection.Open();
            using SqlCommand command = new("SELECT 1", connection);
            first = command.ExecuteScalar();
            second = command.ExecuteScalar();
        }

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public async Task ExecuteScalarAsync_PooledReuse_ReturnsResult()
    {
        // Arrange
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        string connectionString = PooledConnectionString(server);
        ReturnConnectionToPool(connectionString);

        // Act
        object? first;
        object? second;
        using (SqlConnection connection = new(connectionString))
        {
            await connection.OpenAsync();
            using SqlCommand command = new("SELECT 1", connection);
            first = await command.ExecuteScalarAsync();
            second = await command.ExecuteScalarAsync();
        }

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    private static string PooledConnectionString<T>(GenericTdsServer<T> server)
        where T : TdsServerArguments
    {
        return new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = true,
            MaxPoolSize = 1,
            ConnectTimeout = 30,
            CommandTimeout = 30,
            ApplicationName = Guid.NewGuid().ToString()
        }.ConnectionString;
    }

    // Opens, uses and closes a connection so that the next checkout reuses it with the
    // connection-reset flag pending on its first request.
    private static void ReturnConnectionToPool(string connectionString)
    {
        using SqlConnection connection = new(connectionString);
        connection.Open();
        using SqlCommand command = new("SELECT 1", connection);
        Assert.Equal(1, command.ExecuteScalar());
    }

    // Any read the driver posts on demand waits until the server has refused the reset and
    // aborted the connection, so its response can only be seen by a read posted ahead of the
    // request. Restores the seam on dispose.
    private static IDisposable ReadsHeldBackUntilAbort(ResetRefusingTdsServer server)
    {
        ManualResetEventSlim aborted = new();
        server.ConnectionAborted = aborted.Set;
        TdsParserStateObject.s_beforeReadPost = () => Assert.True(
            aborted.Wait(s_abortWait),
            "The server did not abort the connection before the read was posted.");
        return new SeamReset(aborted);
    }

    private static void AssertServerErrorReported(
        ResetRefusingTdsServer server,
        SqlException exception)
    {
        Assert.Equal(1, server.RefusedResets);
        Assert.Equal(4021, exception.Number);
        Assert.Contains(exception.Errors.Cast<SqlError>(), error => error.Number == 596);
    }

    private sealed class SeamReset : IDisposable
    {
        private readonly ManualResetEventSlim _aborted;

        public SeamReset(ManualResetEventSlim aborted)
        {
            _aborted = aborted;
        }

        public void Dispose()
        {
            TdsParserStateObject.s_beforeReadPost = null;
            _aborted.Dispose();
        }
    }
}
#endif
