// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Server;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies that a streamed table-valued parameter whose row source throws while the RPC is
/// being written is terminated with an attention, so the connection remains usable. This
/// includes failures in packets whose one-byte TDS packet number has wrapped back to 1.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class StreamedTvpFailureTests
{
    private const string FailureMessage = "Row source failed.";

    /// <summary>
    /// Numbers of fully sent packets before the row source throws. A count of 256 or 512 means
    /// the packet being filled has the wrapped TDS packet number 1.
    /// </summary>
    public static TheoryData<int> PacketsSentBeforeFailure => new() { 10, 255, 256, 257, 512 };

    /// <summary>
    /// A synchronous execution whose TVP row source throws after packets were sent must send
    /// one attention and leave the connection usable for the next command.
    /// </summary>
    /// <param name="packetsSent">Number of packets sent before the row source throws.</param>
    [Theory]
    [MemberData(nameof(PacketsSentBeforeFailure))]
    public void ExecuteNonQuery_TvpRowSourceThrowsAfterPacketsSent_SendsAttention(int packetsSent)
    {
        // Arrange
        using AttentionCountingTdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        FailingRowSource rows = new(connection, packetsSent);
        using SqlCommand command = CreateTvpCommand(connection, rows);

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => command.ExecuteNonQuery());

        // Assert
        AssertFailureHandled(server, connection, rows, packetsSent, exception);
    }

    /// <summary>
    /// An asynchronous execution whose TVP row source throws after packets were sent must send
    /// one attention and leave the connection usable for the next command.
    /// </summary>
    /// <param name="packetsSent">Number of packets sent before the row source throws.</param>
    [Theory]
    [MemberData(nameof(PacketsSentBeforeFailure))]
    public async Task ExecuteNonQueryAsync_TvpRowSourceThrowsAfterPacketsSent_SendsAttention(
        int packetsSent)
    {
        // Arrange
        using AttentionCountingTdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        FailingRowSource rows = new(connection, packetsSent);
        using SqlCommand command = CreateTvpCommand(connection, rows);

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => command.ExecuteNonQueryAsync());

        // Assert
        AssertFailureHandled(server, connection, rows, packetsSent, exception);
    }

    /// <summary>
    /// Verifies the original error surfaced, the failure happened in the intended packet, the
    /// server received one attention, and a follow-up command succeeds on the same connection.
    /// </summary>
    private static void AssertFailureHandled(
        AttentionCountingTdsServer server,
        SqlConnection connection,
        FailingRowSource rows,
        int packetsSent,
        InvalidOperationException exception)
    {
        // The row source's own exception is surfaced to the caller.
        Assert.Equal(FailureMessage, exception.Message);

        // The failure happened while filling the intended packet, including the wrapped header.
        Assert.Equal((uint)packetsSent, rows.PacketCountAtFailure);
        Assert.Equal(unchecked((byte)(packetsSent + 1)), rows.PacketNumberAtFailure);

        // The partially sent RPC was terminated with exactly one attention.
        Assert.Equal(1, server.AttentionCount);

        // The connection is still usable and the next request is not parsed as RPC data.
        Assert.Equal(ConnectionState.Open, connection.State);
        using SqlCommand followUp = new("SELECT 1", connection);
        Assert.Equal(1, followUp.ExecuteScalar());
        Assert.Equal(1, server.AttentionCount);
    }

    /// <summary>Creates a command that streams <paramref name="rows"/> as a TVP.</summary>
    private static SqlCommand CreateTvpCommand(
        SqlConnection connection,
        IEnumerable<SqlDataRecord> rows)
    {
        SqlCommand command = new("SELECT COUNT(*) FROM @Rows", connection);
        command.Parameters.Add(new SqlParameter("@Rows", SqlDbType.Structured)
        {
            TypeName = "dbo.StreamedTvp",
            Value = rows
        });
        return command;
    }

    /// <summary>Opens a non-pooled connection to <paramref name="server"/>.</summary>
    private static SqlConnection OpenConnection(TdsServer server)
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectTimeout = 5
        };
        SqlConnection connection = new(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Yields rows until the connection has sent the requested number of packets for the
    /// current request, then throws, recording the packet counters at that point.
    /// </summary>
    private sealed class FailingRowSource : IEnumerable<SqlDataRecord>
    {
        private static readonly SqlMetaData[] s_metaData =
        {
            new("Id", SqlDbType.Int),
            new("Flag", SqlDbType.Bit)
        };

        private readonly SqlConnection _connection;
        private readonly int _packetsSent;

        internal FailingRowSource(SqlConnection connection, int packetsSent)
        {
            _connection = connection;
            _packetsSent = packetsSent;
        }

        /// <summary>Non-wrapping number of packets sent when the row source threw.</summary>
        internal uint PacketCountAtFailure { get; private set; }

        /// <summary>
        /// TDS header packet number of the packet being filled when the row source threw.
        /// </summary>
        internal byte PacketNumberAtFailure { get; private set; }

        public IEnumerator<SqlDataRecord> GetEnumerator()
        {
            TdsParserStateObject stateObj =
                ((SqlConnectionInternal)_connection.InnerConnection).Parser._physicalStateObj;

            for (int row = 0; ; row++)
            {
                if (stateObj._outputPacketCount >= _packetsSent)
                {
                    PacketCountAtFailure = stateObj._outputPacketCount;
                    PacketNumberAtFailure = stateObj._outputPacketNumber;
                    throw new InvalidOperationException(FailureMessage);
                }

                SqlDataRecord record = new(s_metaData);
                record.SetInt32(0, row);
                record.SetBoolean(1, row % 2 == 0);
                yield return record;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    /// <summary>A simulated server that counts the attention messages it receives.</summary>
    private sealed class AttentionCountingTdsServer : TdsServer
    {
        private int _attentionCount;

        internal AttentionCountingTdsServer(TdsServerArguments arguments) : base(arguments)
        {
        }

        /// <summary>Number of attention messages received.</summary>
        internal int AttentionCount => Volatile.Read(ref _attentionCount);

        public override TDSMessageCollection OnAttention(
            ITDSServerSession session,
            TDSMessage message)
        {
            Interlocked.Increment(ref _attentionCount);
            return base.OnAttention(session, message);
        }
    }
}
