// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Row;
using Microsoft.SqlServer.TDS.SSPI;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Ensures server tokens cannot consume missing parser state and that rejected connections
/// are not reused, without changing valid login or result processing.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ParserPrerequisiteTests
{
    /// <summary>Open rejects tokens whose metadata or authentication state is missing.</summary>
    [Theory]
    [InlineData(TDSTokenType.Row)]
    [InlineData(TDSTokenType.NBCRow)]
    [InlineData(TDSTokenType.AlternativeRow)]
    [InlineData(TDSTokenType.SSPI)]
    public void Open_WithoutRequiredState_ThrowsParsingError(TDSTokenType tokenType)
    {
        // Arrange
        using TdsServerFixture fixture = CreateInvalidLoginFixture(tokenType);
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);

        // Act
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(connection.Open);

        // Assert
        AssertRejectedConnection(error, connection);
        Assert.Equal(1, fixture.TdsServer.PreLoginCount);
    }

    /// <summary>OpenAsync rejects tokens missing metadata or authentication state.</summary>
    [Theory]
    [InlineData(TDSTokenType.Row)]
    [InlineData(TDSTokenType.NBCRow)]
    [InlineData(TDSTokenType.AlternativeRow)]
    [InlineData(TDSTokenType.SSPI)]
    public async Task OpenAsync_WithoutRequiredState_ThrowsParsingError(TDSTokenType tokenType)
    {
        // Arrange
        using TdsServerFixture fixture = CreateInvalidLoginFixture(tokenType);
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);

        // Act
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => connection.OpenAsync());

        // Assert
        AssertRejectedConnection(error, connection);
        Assert.Equal(1, fixture.TdsServer.PreLoginCount);
    }

    /// <summary>Repeated sync parser failures release the sole slot in either pool.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ParserFailure_DoesNotPoisonPool(bool duringLogin, bool usePoolV2) =>
        AssertPoolRecovery(duringLogin, usePoolV2,
            connection =>
            {
                connection.Open();
                return Task.CompletedTask;
            },
            command => Task.FromResult(command.ExecuteNonQuery()));

    /// <summary>Repeated async parser failures release the sole slot in either pool.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ParserFailureAsync_DoesNotPoisonPool(bool duringLogin, bool usePoolV2) =>
        AssertPoolRecovery(duringLogin, usePoolV2,
            connection => connection.OpenAsync(), command => command.ExecuteNonQueryAsync());

    /// <summary>Non-query row skipping rejects a batch without required row metadata.</summary>
    [Theory]
    [InlineData(TDSTokenType.Row)]
    [InlineData(TDSTokenType.NBCRow)]
    [InlineData(TDSTokenType.AlternativeRow)]
    public void ExecuteNonQuery_WithoutRequiredState_ThrowsParsingError(TDSTokenType tokenType)
    {
        // Arrange
        using BatchTestContext context = new(response => SetInvalidBatch(response, tokenType));
        context.Connection.Open();

        // Act
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => context.Command.ExecuteNonQuery());

        // Assert
        AssertRejectedConnection(error, context.Connection);
    }

    /// <summary>Asynchronous non-query row skipping rejects missing row metadata.</summary>
    [Theory]
    [InlineData(TDSTokenType.Row)]
    [InlineData(TDSTokenType.NBCRow)]
    [InlineData(TDSTokenType.AlternativeRow)]
    public async Task ExecuteNonQueryAsync_WithoutRequiredState_ThrowsParsingError(
        TDSTokenType tokenType)
    {
        // Arrange
        using BatchTestContext context = new(response => SetInvalidBatch(response, tokenType));
        await context.Connection.OpenAsync();

        // Act
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Command.ExecuteNonQueryAsync());

        // Assert
        AssertRejectedConnection(error, context.Connection);
    }

    /// <summary>Reader consumption rejects missing row metadata before disposal.</summary>
    [Theory]
    [InlineData(TDSTokenType.Row)]
    [InlineData(TDSTokenType.NBCRow)]
    [InlineData(TDSTokenType.AlternativeRow)]
    public void ExecuteReader_WithoutRequiredState_ThrowsParsingError(TDSTokenType tokenType)
    {
        // Arrange
        using BatchTestContext context = new(response => SetInvalidBatch(response, tokenType));
        context.Connection.Open();
        SqlDataReader? reader = null;

        // Act
        Exception? error = Record.Exception(() =>
        {
            reader = context.Command.ExecuteReader();
            reader.Read();
        });

        // Assert / Cleanup
        AssertRejectedReader(error, context.Connection, reader);
    }

    /// <summary>Async reader consumption rejects missing row metadata before disposal.</summary>
    [Theory]
    [InlineData(TDSTokenType.Row)]
    [InlineData(TDSTokenType.NBCRow)]
    [InlineData(TDSTokenType.AlternativeRow)]
    public async Task ExecuteReaderAsync_WithoutRequiredState_ThrowsParsingError(
        TDSTokenType tokenType)
    {
        // Arrange
        using BatchTestContext context = new(response => SetInvalidBatch(response, tokenType));
        await context.Connection.OpenAsync();
        SqlDataReader? reader = null;

        // Act
        Exception? error = await Record.ExceptionAsync(async () =>
        {
            reader = await context.Command.ExecuteReaderAsync();
            await reader.ReadAsync();
        });

        // Assert / Cleanup
        AssertRejectedReader(error, context.Connection, reader);
    }

    /// <summary>Non-query execution rejects alternate rows without matching metadata.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecuteNonQuery_AlternateRowWithoutMatchingMetadata_ThrowsParsingError(
        bool includeAlternateMetadata)
    {
        // Arrange
        using BatchTestContext context = new(response =>
            SetResult(response, includeAlternateMetadata, alternateId: 2));
        context.Connection.Open();

        // Act
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => context.Command.ExecuteNonQuery());

        // Assert
        AssertRejectedConnection(error, context.Connection);
    }

    /// <summary>Async non-query execution rejects unmatched alternate-row metadata.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteNonQueryAsync_AlternateRowWithoutMatchingMetadata_ThrowsParsingError(
        bool includeAlternateMetadata)
    {
        // Arrange
        using BatchTestContext context = new(response =>
            SetResult(response, includeAlternateMetadata, alternateId: 2));
        await context.Connection.OpenAsync();

        // Act
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Command.ExecuteNonQueryAsync());

        // Assert
        AssertRejectedConnection(error, context.Connection);
    }

    /// <summary>Result transitions reject unmatched alternate rows before disposal.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NextResult_AlternateRowWithoutMatchingMetadata_ThrowsParsingError(
        bool includeAlternateMetadata, bool readAllRows)
    {
        // Arrange
        using BatchTestContext context = new(response =>
            SetResult(response, includeAlternateMetadata, alternateId: 2));
        context.Connection.Open();
        SqlDataReader? reader = null;

        // Act
        Exception? error = Record.Exception(() =>
        {
            reader = context.Command.ExecuteReader();
            ReadPrimaryRows(reader, readAllRows);
            reader.NextResult();
        });

        // Assert / Cleanup
        AssertRejectedReader(error, context.Connection, reader);
    }

    /// <summary>Async transitions reject unmatched alternate rows before disposal.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NextResultAsync_AlternateRowWithoutMatchingMetadata_ThrowsParsingError(
        bool includeAlternateMetadata, bool readAllRows)
    {
        // Arrange
        using BatchTestContext context = new(response =>
            SetResult(response, includeAlternateMetadata, alternateId: 2));
        await context.Connection.OpenAsync();
        SqlDataReader? reader = null;

        // Act
        Exception? error = await Record.ExceptionAsync(async () =>
        {
            reader = await context.Command.ExecuteReaderAsync();
            await ReadPrimaryRowsAsync(reader, readAllRows);
            await reader.NextResultAsync();
        });

        // Assert / Cleanup
        AssertRejectedReader(error, context.Connection, reader);
    }

    /// <summary>Non-query execution skips valid primary and alternate results.</summary>
    [Fact]
    public void ExecuteNonQuery_ValidMetadata_PreservesConnection()
    {
        // Arrange
        using BatchTestContext context = new(SetValidResult);
        context.Connection.Open();

        // Act
        context.Command.ExecuteNonQuery();

        // Assert
        Assert.Equal(ConnectionState.Open, context.Connection.State);
    }

    /// <summary>Asynchronous non-query execution skips valid results without rejection.</summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_ValidMetadata_PreservesConnection()
    {
        // Arrange
        using BatchTestContext context = new(SetValidResult);
        await context.Connection.OpenAsync();

        // Act
        await context.Command.ExecuteNonQueryAsync();

        // Assert
        Assert.Equal(ConnectionState.Open, context.Connection.State);
    }

    /// <summary>Result transitions skip a null row and consume the alternate result.</summary>
    [Fact]
    public void NextResult_ValidMetadata_PreservesResultProcessing()
    {
        // Arrange
        using BatchTestContext context = new(SetValidResult);
        context.Connection.Open();
        using (SqlDataReader reader = context.Command.ExecuteReader())
        {
            ReadPrimaryRows(reader, readAllRows: false);

            // Act
            bool hasAlternateResult = reader.NextResult();

            // Assert
            Assert.True(hasAlternateResult);
            Assert.True(reader.Read());
            Assert.Equal(42, reader.GetInt32(0));
            Assert.False(reader.Read());
            Assert.False(reader.NextResult());
        }

        // Cleanup
        Assert.Equal(ConnectionState.Open, context.Connection.State);
    }

    /// <summary>Async transitions skip a null row and consume the alternate result.</summary>
    [Fact]
    public async Task NextResultAsync_ValidMetadata_PreservesResultProcessing()
    {
        // Arrange
        using BatchTestContext context = new(SetValidResult);
        await context.Connection.OpenAsync();
        using (SqlDataReader reader = await context.Command.ExecuteReaderAsync())
        {
            await ReadPrimaryRowsAsync(reader, readAllRows: false);

            // Act
            bool hasAlternateResult = await reader.NextResultAsync();

            // Assert
            Assert.True(hasAlternateResult);
            Assert.True(await reader.ReadAsync());
            Assert.Equal(42, reader.GetInt32(0));
            Assert.False(await reader.ReadAsync());
            Assert.False(await reader.NextResultAsync());
        }

        // Cleanup
        Assert.Equal(ConnectionState.Open, context.Connection.State);
    }

    /// <summary>Packet continuation preserves lookup of a known alternate-row identifier.</summary>
    [Fact]
    public void ExecuteNonQuery_KnownAlternateRowIdAcrossPackets_PreservesConnection()
    {
        // Arrange
        using BatchTestContext context = new();
        context.Connection.Open();
        context.SetPacketBoundaryResponse(alternateId: 1);

        // Act
        context.Command.ExecuteNonQuery();

        // Assert
        Assert.Equal(ConnectionState.Open, context.Connection.State);
    }

    /// <summary>Asynchronous packet continuation preserves lookup of a known identifier.</summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_KnownAlternateRowIdAcrossPackets_PreservesConnection()
    {
        // Arrange
        using BatchTestContext context = new();
        await context.Connection.OpenAsync();
        context.SetPacketBoundaryResponse(alternateId: 1);

        // Act
        await context.Command.ExecuteNonQueryAsync();

        // Assert
        Assert.Equal(ConnectionState.Open, context.Connection.State);
    }

    /// <summary>Packet continuation rejects an unknown alternate-row identifier.</summary>
    [Fact]
    public void ExecuteNonQuery_UnknownAlternateRowIdAcrossPackets_ThrowsParsingError()
    {
        // Arrange
        using BatchTestContext context = new();
        context.Connection.Open();
        context.SetPacketBoundaryResponse(alternateId: 2);

        // Act
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => context.Command.ExecuteNonQuery());

        // Assert
        AssertRejectedConnection(error, context.Connection);
    }

    /// <summary>Asynchronous packet continuation rejects an unknown identifier.</summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_UnknownAlternateRowIdAcrossPackets_ThrowsParsingError()
    {
        // Arrange
        using BatchTestContext context = new();
        await context.Connection.OpenAsync();
        context.SetPacketBoundaryResponse(alternateId: 2);

        // Act
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Command.ExecuteNonQueryAsync());

        // Assert
        AssertRejectedConnection(error, context.Connection);
    }

    /// <summary>Starts a server that sends a token missing its state during login.</summary>
    /// <param name="tokenType">The token to send without metadata or a provider.</param>
    /// <returns>A running fixture owned by the caller.</returns>
    private static TdsServerFixture CreateInvalidLoginFixture(TDSTokenType tokenType)
    {
        TdsServerFixture fixture = new();
        fixture.TdsServer.OnAuthenticationResponseCompleted = response =>
        {
            response.Clear();
            response.Add(CreateToken(tokenType));
        };
        return fixture;
    }

    /// <summary>Checks rejection and recovery using the supplied public entry points.</summary>
    /// <param name="duringLogin">Whether to inject the malformed token during login.</param>
    /// <param name="usePoolV2">The pool implementation to exercise.</param>
    /// <param name="open">The synchronous or asynchronous connection-opening operation.</param>
    /// <param name="execute">The corresponding non-query operation.</param>
    /// <returns>A task that completes after a fresh physical connection is verified.</returns>
    private static async Task AssertPoolRecovery(bool duringLogin, bool usePoolV2,
        Func<SqlConnection, Task> open, Func<SqlCommand, Task> execute)
    {
        // Arrange
        using ConnectionPoolVersionScope version = new(usePoolV2);
        using TdsServerFixture fixture = new();
        SqlConnectionStringBuilder builder = CreateBuilder(fixture);
        builder.Pooling = true;
        builder.MaxPoolSize = 1;
        builder.PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock;
        using SimulatedServerPoolScope pool = new(builder);
        void SetInvalidResponse(TDSMessage response)
        {
            response.Clear();
            response.Add(CreateToken(TDSTokenType.Row));
        }
        if (duringLogin)
        {
            fixture.TdsServer.OnAuthenticationResponseCompleted = SetInvalidResponse;
        }
        else
        {
            fixture.TdsServer.OnSQLBatchCompleted = SetInvalidResponse;
        }

        // Act / Assert: repeated failures must release the sole pool slot.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using SqlConnection failed = new(builder.ConnectionString);
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                {
                    await open(failed);
                    using SqlCommand command = new("SELECT 1", failed);
                    await execute(command);
                });
            AssertRejectedConnection(error, failed);
        }

        // Cleanup / Assert: recovery requires a fresh physical login.
        fixture.TdsServer.OnAuthenticationResponseCompleted = null;
        fixture.TdsServer.OnSQLBatchCompleted = null;
        using SqlConnection valid = new(builder.ConnectionString);
        await open(valid);
        Assert.Equal(ConnectionState.Open, valid.State);
        Assert.Equal(3, fixture.TdsServer.PreLoginCount);
    }

    /// <summary>Checks that the corrupted-stream error closes the connection.</summary>
    /// <param name="error">The operation's exception, captured before cleanup.</param>
    /// <param name="connection">The connection that consumed the malformed response.</param>
    private static void AssertRejectedConnection(
        InvalidOperationException error, SqlConnection connection)
    {
        Assert.Contains("Error state: 18", error.Message);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>Checks rejection and connection state independently of reader cleanup.</summary>
    /// <param name="error">The exception captured while opening or consuming the reader.</param>
    /// <param name="connection">The connection whose state must be closed before disposal.</param>
    /// <param name="reader">The reader to dispose, if execution returned one.</param>
    private static void AssertRejectedReader(
        Exception? error, SqlConnection connection, SqlDataReader? reader)
    {
        ConnectionState stateAfterOperation = connection.State;
        Exception? disposeError = Record.Exception(() => reader?.Dispose());
        AssertRejectedConnection(Assert.IsType<InvalidOperationException>(error), connection);
        Assert.Equal(ConnectionState.Closed, stateAfterOperation);
        Assert.Null(disposeError);
    }

    /// <summary>Consumes the primary value and optionally the null row.</summary>
    /// <param name="reader">The reader positioned before the primary rows.</param>
    /// <param name="readAllRows">Whether to consume the null row rather than skip it.</param>
    private static void ReadPrimaryRows(SqlDataReader reader, bool readAllRows)
    {
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        if (readAllRows)
        {
            Assert.True(reader.Read());
            Assert.True(reader.IsDBNull(0));
        }
    }

    /// <summary>Asynchronously consumes the primary value and optionally the null row.</summary>
    /// <param name="reader">The reader positioned before the primary rows.</param>
    /// <param name="readAllRows">Whether to consume the null row rather than skip it.</param>
    /// <returns>A task that completes after the selected rows are checked.</returns>
    private static async Task ReadPrimaryRowsAsync(SqlDataReader reader, bool readAllRows)
    {
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        if (readAllRows)
        {
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.IsDBNull(0));
        }
    }

    /// <summary>Owns the repeated server, connection and command setup for batch tests.</summary>
    private sealed class BatchTestContext : IDisposable
    {
        private readonly TdsServerFixture _fixture = new();

        /// <summary>Creates an unopened connection and installs the batch response.</summary>
        /// <param name="response">The override, or null for the normal response.</param>
        public BatchTestContext(Action<TDSMessage>? response = null)
        {
            _fixture.TdsServer.OnSQLBatchCompleted = response;
            Connection = new SqlConnection(CreateBuilder(_fixture).ConnectionString);
            Command = new SqlCommand("SELECT 1", Connection);
        }

        public SqlConnection Connection { get; }
        public SqlCommand Command { get; }

        /// <summary>Installs a response whose alternate-row identifier spans two packets.</summary>
        /// <param name="alternateId">The identifier to split across packets.</param>
        public void SetPacketBoundaryResponse(ushort alternateId) =>
            _fixture.TdsServer.OnSQLBatchCompleted = response =>
                SetPacketBoundaryResult(response, Connection.PacketSize, alternateId);

        /// <summary>Disposes the command, connection and server in ownership order.</summary>
        public void Dispose()
        {
            try
            {
                Command.Dispose();
            }
            finally
            {
                try
                {
                    Connection.Dispose();
                }
                finally
                {
                    _fixture.Dispose();
                }
            }
        }
    }

    /// <summary>Builds a bounded, unpooled connection to the test's ephemeral server.</summary>
    /// <param name="fixture">The server fixture supplying the ephemeral port.</param>
    /// <returns>The connection settings used by the parser tests.</returns>
    private static SqlConnectionStringBuilder CreateBuilder(TdsServerFixture fixture) => new()
    {
        DataSource = $"localhost,{fixture.TdsServer.EndPoint.Port}",
        Encrypt = SqlConnectionEncryptOption.Optional,
        Pooling = false,
        ConnectTimeout = 5,
        ConnectRetryCount = 0
    };

    /// <summary>Writes a little-endian identifier without flushing the TDS message.</summary>
    /// <param name="destination">The stream receiving the identifier.</param>
    /// <param name="value">The identifier to serialize.</param>
    private static void WriteUInt16(Stream destination, ushort value)
    {
        destination.WriteByte((byte)value);
        destination.WriteByte((byte)(value >> 8));
    }

    /// <summary>Creates a token without sending the state that would normally precede it.</summary>
    /// <param name="tokenType">The token whose prerequisite state is missing.</param>
    /// <returns>The malformed response token.</returns>
    private static TDSPacketToken CreateToken(TDSTokenType tokenType) => tokenType switch
    {
        TDSTokenType.Row => new TDSRowToken(new TDSColMetadataToken()),
        TDSTokenType.NBCRow => new TDSNBCRowToken(new TDSColMetadataToken()),
        TDSTokenType.AlternativeRow => new AlternateRowToken(1),
        TDSTokenType.SSPI => new TDSSSPIToken(Array.Empty<byte>()),
        _ => throw new ArgumentOutOfRangeException(nameof(tokenType))
    };

    /// <summary>Writes a token lacking its prerequisite state followed by a final DONE.</summary>
    /// <param name="response">The response to replace.</param>
    /// <param name="tokenType">The token to send without its required state.</param>
    private static void SetInvalidBatch(TDSMessage response, TDSTokenType tokenType)
    {
        response.Clear();
        response.Add(CreateToken(tokenType));
        response.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final));
    }

    /// <summary>Writes ordinary and null rows followed by a matching alternate result.</summary>
    /// <param name="response">The response to replace with the valid result stream.</param>
    private static void SetValidResult(TDSMessage response) =>
        SetResult(response, includeAlternateMetadata: true, alternateId: 1);

    /// <summary>Writes primary rows and an alternate row with optional metadata.</summary>
    /// <param name="response">The response to replace.</param>
    /// <param name="includeAlternateMetadata">Whether to declare metadata identifier one.</param>
    /// <param name="alternateId">The identifier requested by the alternate row.</param>
    private static void SetResult(
        TDSMessage response, bool includeAlternateMetadata, ushort alternateId)
    {
        response.Clear();
        TDSColMetadataToken metadata = new();
        metadata.Columns.Add(new TDSColumnData { DataType = TDSDataType.Int4, Name = "value" });
        response.Add(metadata);
        if (includeAlternateMetadata)
        {
            response.Add(new AlternateMetadataToken());
        }

        TDSRowToken row = new(metadata);
        row.Data.Add(1);
        response.Add(row);
        TDSNBCRowToken nullRow = new(metadata);
        nullRow.Data.Add(null);
        response.Add(nullRow);
        response.Add(new AlternateRowToken(alternateId));
        response.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final));
    }

    /// <summary>Pads a result so the alternate-row identifier spans two TDS packets.</summary>
    /// <param name="response">The batch response to replace with the padded result.</param>
    /// <param name="packetSize">The negotiated packet size, including its header.</param>
    /// <param name="alternateId">The metadata identifier requested by the alternate row.</param>
    private static void SetPacketBoundaryResult(
        TDSMessage response, int packetSize, ushort alternateId)
    {
        SetResult(response, includeAlternateMetadata: true, alternateId);
        using MemoryStream prefix = new();
        for (int index = 0; index < response.Count - 2; index++)
        {
            response[index].Deflate(prefix);
        }

        TDSInfoToken padding = new(0, 0, 0, string.Empty);
        using MemoryStream emptyPadding = new();
        padding.Deflate(emptyPadding);
        // Leave the ALTROW token and first identifier byte in this packet, and the next byte
        // in the following packet.
        int targetLength = packetSize - TdsEnums.HEADER_LEN - 2;
        int textBytes = targetLength - (int)prefix.Length - (int)emptyPadding.Length;
        Assert.True(textBytes >= 0);
        Assert.Equal(0, textBytes % 2);
        padding.Message = new string('x', textBytes / 2);
        padding.Deflate(prefix);
        Assert.Equal(targetLength, prefix.Length);
        response.Insert(response.Count - 2, padding);
    }

    /// <summary>Serializes one integer alternate column using metadata identifier one.</summary>
    private sealed class AlternateMetadataToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            destination.WriteByte(TdsEnums.SQLALTMETADATA);
            WriteUInt16(destination, 1);
            WriteUInt16(destination, 1);
            destination.WriteByte(0);
            destination.WriteByte(TdsEnums.AOPSUM);
            WriteUInt16(destination, 0);
            new TDSColumnData { DataType = TDSDataType.Int4, Name = "sum" }.Deflate(destination);
        }
    }

    /// <summary>Serializes an alternate row with the supplied metadata identifier.</summary>
    private sealed class AlternateRowToken(ushort id) : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            destination.WriteByte(TdsEnums.SQLALTROW);
            WriteUInt16(destination, id);
            TDSUtilities.WriteUInt(destination, 42);
        }
    }
}
