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
    /// <summary>
    /// Rejects tokens requiring uninitialized state during both connection-opening variants.
    /// </summary>
    [Theory]
    [InlineData(TDSTokenType.Row, false)]
    [InlineData(TDSTokenType.Row, true)]
    [InlineData(TDSTokenType.NBCRow, false)]
    [InlineData(TDSTokenType.NBCRow, true)]
    [InlineData(TDSTokenType.AlternativeRow, false)]
    [InlineData(TDSTokenType.AlternativeRow, true)]
    [InlineData(TDSTokenType.SSPI, false)]
    [InlineData(TDSTokenType.SSPI, true)]
    public async Task LoginWithoutRequiredState_ThrowsParsingError(
        TDSTokenType tokenType, bool useAsync)
    {
        using TdsServerFixture fixture = new();
        fixture.TdsServer.OnAuthenticationResponseCompleted = response =>
        {
            response.Clear();
            response.Add(CreateToken(tokenType));
        };
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Open(connection, useAsync));

        AssertParsingError(error);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(1, fixture.TdsServer.PreLoginCount);
    }

    /// <summary>
    /// Repeated parser failures during login or command processing cannot occupy the sole pool
    /// slot or leave a reusable connection, with either pool and either execution variant.
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ParserFailure_DoesNotPoisonPool(
        bool duringLogin, bool usePoolV2, bool useAsync)
    {
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

        for (int attempt = 0; attempt < 2; attempt++)
        {
            using SqlConnection failed = new(builder.ConnectionString);
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                {
                    await Open(failed, useAsync);
                    using SqlCommand command = new("SELECT 1", failed);
                    if (useAsync)
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        command.ExecuteNonQuery();
                    }
                });
            AssertParsingError(error);
            Assert.Equal(ConnectionState.Closed, failed.State);
        }

        fixture.TdsServer.OnAuthenticationResponseCompleted = null;
        fixture.TdsServer.OnSQLBatchCompleted = null;
        using SqlConnection valid = new(builder.ConnectionString);
        await Open(valid, useAsync);
        Assert.Equal(ConnectionState.Open, valid.State);
        Assert.Equal(3, fixture.TdsServer.PreLoginCount);
    }

    /// <summary>
    /// Missing row state is rejected during command processing, including the early return
    /// used by readers as well as the non-query row-skipping path.
    /// </summary>
    [Theory]
    [InlineData(TDSTokenType.Row, false, false)]
    [InlineData(TDSTokenType.Row, false, true)]
    [InlineData(TDSTokenType.Row, true, false)]
    [InlineData(TDSTokenType.Row, true, true)]
    [InlineData(TDSTokenType.NBCRow, false, false)]
    [InlineData(TDSTokenType.NBCRow, false, true)]
    [InlineData(TDSTokenType.NBCRow, true, false)]
    [InlineData(TDSTokenType.NBCRow, true, true)]
    [InlineData(TDSTokenType.AlternativeRow, false, false)]
    [InlineData(TDSTokenType.AlternativeRow, false, true)]
    [InlineData(TDSTokenType.AlternativeRow, true, false)]
    [InlineData(TDSTokenType.AlternativeRow, true, true)]
    public async Task BatchWithoutRequiredState_ThrowsParsingError(
        TDSTokenType tokenType, bool useReader, bool useAsync)
    {
        using TdsServerFixture fixture = new();
        fixture.TdsServer.OnSQLBatchCompleted = response =>
        {
            response.Clear();
            response.Add(CreateToken(tokenType));
            response.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final));
        };
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);
        await Open(connection, useAsync);
        using SqlCommand command = new("SELECT 1", connection);

        SqlDataReader? reader = null;
        Exception? operationError = await Record.ExceptionAsync(
            async () =>
            {
                if (useReader)
                {
                    reader = useAsync
                        ? await command.ExecuteReaderAsync()
                        : command.ExecuteReader();
                    if (useAsync)
                    {
                        await reader.ReadAsync();
                    }
                    else
                    {
                        reader.Read();
                    }
                }
                else if (useAsync)
                {
                    await command.ExecuteNonQueryAsync();
                }
                else
                {
                    command.ExecuteNonQuery();
                }
            });

        ConnectionState stateAfterOperation = connection.State;
        Exception? disposeError = Record.Exception(() => reader?.Dispose());

        AssertParsingError(Assert.IsType<InvalidOperationException>(operationError));
        Assert.Equal(ConnectionState.Closed, stateAfterOperation);
        Assert.Null(disposeError);
    }

    /// <summary>
    /// Alternate rows must resolve their own metadata, not fall back to a primary result's
    /// metadata. Consuming and skipping rows must fail before disposal, which is checked
    /// separately so cleanup cannot substitute for the operation's exception.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    public async Task AlternateRowWithoutMatchingMetadata_ThrowsParsingError(
        bool includeAlternateMetadata, bool useReader, bool useAsync, bool readAllRows)
    {
        using TdsServerFixture fixture = new();
        fixture.TdsServer.OnSQLBatchCompleted = response =>
            SetResult(response, includeAlternateMetadata, alternateId: 2);
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);
        await Open(connection, useAsync);
        using SqlCommand command = new("SELECT 1", connection);

        SqlDataReader? reader = null;
        Exception? operationError = await Record.ExceptionAsync(
            async () =>
            {
                if (useReader)
                {
                    reader = useAsync
                        ? await command.ExecuteReaderAsync()
                        : command.ExecuteReader();
                    Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
                    Assert.Equal(1, reader.GetInt32(0));
                    if (readAllRows)
                    {
                        Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
                        Assert.True(reader.IsDBNull(0));
                    }
                    if (useAsync)
                    {
                        await reader.NextResultAsync();
                    }
                    else
                    {
                        reader.NextResult();
                    }
                }
                else if (useAsync)
                {
                    await command.ExecuteNonQueryAsync();
                }
                else
                {
                    command.ExecuteNonQuery();
                }
            });

        ConnectionState stateAfterOperation = connection.State;
        Exception? disposeError = Record.Exception(() => reader?.Dispose());

        AssertParsingError(Assert.IsType<InvalidOperationException>(operationError));
        Assert.Equal(ConnectionState.Closed, stateAfterOperation);
        Assert.Null(disposeError);
    }

    /// <summary>
    /// Valid primary and alternate metadata still support consuming and skipping results.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ValidMetadata_PreservesResultProcessing(bool useReader, bool useAsync)
    {
        using TdsServerFixture fixture = new();
        fixture.TdsServer.OnSQLBatchCompleted = response =>
            SetResult(response, includeAlternateMetadata: true, alternateId: 1);
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);
        await Open(connection, useAsync);
        using SqlCommand command = new("SELECT 1", connection);

        if (useReader)
        {
            using SqlDataReader reader = useAsync
                ? await command.ExecuteReaderAsync()
                : command.ExecuteReader();
            Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.True(useAsync ? await reader.NextResultAsync() : reader.NextResult());
            Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.Equal(42, reader.GetInt32(0));
            Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.False(useAsync ? await reader.NextResultAsync() : reader.NextResult());
        }
        else if (useAsync)
        {
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            command.ExecuteNonQuery();
        }

        Assert.Equal(ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// Splits an alternate-row identifier across TDS packets to verify that continuation
    /// preserves both successful metadata lookup and controlled rejection.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AlternateRowIdAcrossPackets_PreservesValidation(bool knownId, bool useAsync)
    {
        using TdsServerFixture fixture = new();
        using SqlConnection connection = new(CreateBuilder(fixture).ConnectionString);
        await Open(connection, useAsync);
        fixture.TdsServer.OnSQLBatchCompleted = response =>
        {
            SetResult(response, includeAlternateMetadata: true,
                alternateId: knownId ? (ushort)1 : (ushort)2);
            using MemoryStream prefix = new();
            for (int index = 0; index < response.Count - 2; index++)
            {
                response[index].Deflate(prefix);
            }

            TDSInfoToken padding = new(0, 0, 0, string.Empty);
            using MemoryStream emptyPadding = new();
            padding.Deflate(emptyPadding);
            int targetLength = connection.PacketSize - TdsEnums.HEADER_LEN - 2;
            int textBytes = targetLength - (int)prefix.Length - (int)emptyPadding.Length;
            Assert.True(textBytes >= 0);
            Assert.Equal(0, textBytes % 2);
            padding.Message = new string('x', textBytes / 2);
            padding.Deflate(prefix);
            Assert.Equal(targetLength, prefix.Length);
            response.Insert(response.Count - 2, padding);
        };
        using SqlCommand command = new("SELECT 1", connection);

        async Task Execute()
        {
            if (useAsync)
            {
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                command.ExecuteNonQuery();
            }
        }

        if (knownId)
        {
            await Execute();
            Assert.Equal(ConnectionState.Open, connection.State);
        }
        else
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                Execute);
            AssertParsingError(error);
            Assert.Equal(ConnectionState.Closed, connection.State);
        }
    }

    /// <summary>
    /// Builds a bounded, unpooled connection to the test's ephemeral server.
    /// </summary>
    private static SqlConnectionStringBuilder CreateBuilder(TdsServerFixture fixture) => new()
    {
        DataSource = $"localhost,{fixture.TdsServer.EndPoint.Port}",
        Encrypt = SqlConnectionEncryptOption.Optional,
        Pooling = false,
        ConnectTimeout = 5,
        ConnectRetryCount = 0
    };

    /// <summary>
    /// Exercises the selected public connection-opening entry point.
    /// </summary>
    private static async Task Open(SqlConnection connection, bool useAsync)
    {
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
    }

    /// <summary>
    /// Requires the existing corrupted-stream failure rather than an incidental CLR exception.
    /// </summary>
    private static void AssertParsingError(InvalidOperationException error) =>
        Assert.Contains("Error state: 18", error.Message);

    /// <summary>
    /// Writes a little-endian identifier without flushing the surrounding TDS message.
    /// </summary>
    private static void WriteUInt16(Stream destination, ushort value)
    {
        destination.WriteByte((byte)value);
        destination.WriteByte((byte)(value >> 8));
    }

    /// <summary>
    /// Creates a token without sending the state that would normally precede it.
    /// </summary>
    private static TDSPacketToken CreateToken(TDSTokenType tokenType) => tokenType switch
    {
        TDSTokenType.Row => new TDSRowToken(new TDSColMetadataToken()),
        TDSTokenType.NBCRow => new TDSNBCRowToken(new TDSColMetadataToken()),
        TDSTokenType.AlternativeRow => new AlternateRowToken(1),
        TDSTokenType.SSPI => new TDSSSPIToken(Array.Empty<byte>()),
        _ => throw new ArgumentOutOfRangeException(nameof(tokenType))
    };

    /// <summary>
    /// Writes a primary result followed by an alternate row with optional metadata.
    /// </summary>
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

    /// <summary>
    /// Serializes one integer alternate column using metadata identifier one.
    /// </summary>
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

    /// <summary>
    /// Serializes one alternate row referring to the supplied metadata identifier.
    /// </summary>
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
