// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Row;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Regression coverage for https://github.com/dotnet/SqlClient/issues/4321.
///
/// A T-SQL error that is re-raised from a CATCH block (<c>BEGIN TRY ... END TRY
/// BEGIN CATCH THROW END CATCH</c>) arrives on the wire <em>after</em> the DONE token
/// that terminates the current result set:
///
/// <code>
/// COLMETADATA
/// DONE  (DONE_MORE | DONE_COUNT, Select, 0)   &lt;- terminates the (empty) result set
/// DONE  (DONE_MORE)                           &lt;- END TRY
/// ERROR (8134 "Divide by zero...")            &lt;- the THROW
/// DONE  (DONE_ERROR)                          &lt;- final
/// </code>
///
/// whereas a bare <c>SELECT 1/0</c> sends the ERROR token <em>before</em> any DONE token.
/// <c>SqlDataReader.TryHasMoreRows</c> used to stop consuming ERROR/INFO tokens once any
/// DONE token had been seen, so the trailing ERROR was never surfaced and the caller saw
/// an empty result set instead of a <see cref="SqlException"/>.
///
/// These tests replay the exact token sequences against the in-process simulated TDS
/// server so the regression is caught without a live SQL Server.
/// </summary>
// Serializes execution with the other SimulatedServerTests classes.
[Collection(SimulatedServerTestCollection.Name)]
public class DataReaderTrailingErrorTests : IDisposable
{
    private const uint DivideByZeroErrorNumber = 8134;
    private const string DivideByZeroMessage = "Divide by zero error encountered.";

    private readonly TdsServerFixture _fixture;
    private readonly TdsServer _server;
    private readonly string _connectionString;

    public DataReaderTrailingErrorTests()
    {
        _fixture = new TdsServerFixture();
        _server = _fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    public void Dispose()
    {
        _server.OnSQLBatchCompleted = null;
        _fixture.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Token-stream builders
    // ──────────────────────────────────────────────────────────────────────────

    private static TDSColMetadataToken SingleIntColumnMetadata()
    {
        TDSColMetadataToken metadata = new();
        TDSColumnData column = new()
        {
            DataType = TDSDataType.IntN,
            DataTypeSpecific = (byte)4
        };
        column.Flags.Updatable = TDSColumnDataUpdatableFlag.ReadOnly;
        column.Flags.IsNullable = true;
        column.Flags.IsComputed = true;
        metadata.Columns.Add(column);
        return metadata;
    }

    private static TDSRowToken IntRow(TDSColMetadataToken metadata, int value)
    {
        TDSRowToken row = new(metadata);
        row.Data.Add(value);
        return row;
    }

    private static TDSErrorToken DivideByZeroError() =>
        new(DivideByZeroErrorNumber, 1, 16, DivideByZeroMessage, "simulated", string.Empty, 2);

    /// <summary>
    /// Replaces the simulated server's canned batch response with <paramref name="tokens"/>.
    /// </summary>
    private void RespondWith(params TDSPacketToken[] tokens)
    {
        _server.OnSQLBatchCompleted = response =>
        {
            response.Clear();
            foreach (TDSPacketToken token in tokens)
            {
                response.Add(token);
            }
        };
    }

    /// <summary>
    /// The TRY/CATCH + THROW shape: an empty result set whose DONE token is followed by
    /// an ERROR token.
    /// </summary>
    private void RespondWithTrailingError()
    {
        RespondWith(
            SingleIntColumnMetadata(),
            new TDSDoneToken(TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 0),
            new TDSDoneToken(TDSDoneTokenStatusType.More, TDSDoneTokenCommandType.Done, 0),
            DivideByZeroError(),
            new TDSDoneToken(TDSDoneTokenStatusType.Error, TDSDoneTokenCommandType.Done, 0));
    }

    /// <summary>
    /// The bare <c>SELECT 1/0</c> shape: the ERROR token precedes every DONE token.
    /// </summary>
    private void RespondWithLeadingError()
    {
        RespondWith(
            SingleIntColumnMetadata(),
            DivideByZeroError(),
            new TDSDoneToken(TDSDoneTokenStatusType.Error, TDSDoneTokenCommandType.Select, 0));
    }

    private void RespondWithEmptyResultSet()
    {
        RespondWith(
            SingleIntColumnMetadata(),
            new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 0));
    }

    private static SqlCommand CreateCommand(SqlConnection connection) =>
        new("SELECT 1", connection);

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: the issue repro - trailing ERROR must surface (sync + async)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_ErrorAfterResultSetDone_ThrowsSqlException()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        RespondWithTrailingError();

        SqlException ex = Assert.ThrowsAny<SqlException>(() =>
        {
            using SqlCommand command = CreateCommand(connection);
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
            }
        });

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
        Assert.Contains(DivideByZeroMessage, ex.Message);
    }

    [Fact]
    public async Task ReadAsync_ErrorAfterResultSetDone_ThrowsSqlException()
    {
        using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        RespondWithTrailingError();

        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(async () =>
        {
            using SqlCommand command = CreateCommand(connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
            }
        });

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
        Assert.Contains(DivideByZeroMessage, ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2: the pre-existing working case must keep working (sync + async)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_ErrorBeforeDone_StillThrowsSqlException()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        RespondWithLeadingError();

        SqlException ex = Assert.ThrowsAny<SqlException>(() =>
        {
            using SqlCommand command = CreateCommand(connection);
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
            }
        });

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    [Fact]
    public async Task ReadAsync_ErrorBeforeDone_StillThrowsSqlException()
    {
        using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        RespondWithLeadingError();

        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(async () =>
        {
            using SqlCommand command = CreateCommand(connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
            }
        });

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 3: a genuinely empty result set must stay empty and must not throw
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_EmptyResultSetWithoutError_ReturnsNoRowsAndDoesNotThrow()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        RespondWithEmptyResultSet();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = command.ExecuteReader();

        int rows = 0;
        while (reader.Read())
        {
            rows++;
        }

        Assert.Equal(0, rows);
        Assert.False(reader.NextResult());
    }

    [Fact]
    public async Task ReadAsync_EmptyResultSetWithoutError_ReturnsNoRowsAndDoesNotThrow()
    {
        using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        RespondWithEmptyResultSet();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        int rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
        }

        Assert.Equal(0, rows);
        Assert.False(await reader.NextResultAsync());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 4: rows are still delivered before a trailing error is raised
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_RowsThenTrailingError_DeliversRowsThenThrows()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        TDSColMetadataToken metadata = SingleIntColumnMetadata();
        RespondWith(
            metadata,
            IntRow(metadata, 42),
            new TDSDoneToken(TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1),
            DivideByZeroError(),
            new TDSDoneToken(TDSDoneTokenStatusType.Error, TDSDoneTokenCommandType.Done, 0));

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(42, reader.GetInt32(0));

        SqlException ex = Assert.ThrowsAny<SqlException>(() => reader.Read());
        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 5: trailing informational messages stay informational
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_TrailingInfoMessage_IsRaisedAsInfoNotError()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        List<string> infoMessages = new();
        connection.InfoMessage += (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                infoMessages.Add(error.Message);
            }
        };

        TDSColMetadataToken metadata = SingleIntColumnMetadata();
        RespondWith(
            metadata,
            IntRow(metadata, 7),
            new TDSDoneToken(TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1),
            new TDSInfoToken(50000, 1, 0, "informational only", "simulated", string.Empty, 1),
            new TDSDoneToken(TDSDoneTokenStatusType.Final, TDSDoneTokenCommandType.Done, 0));

        int rows = 0;
        using (SqlCommand command = CreateCommand(connection))
        using (SqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows++;
            }
        }

        Assert.Equal(1, rows);
        Assert.Contains("informational only", infoMessages);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 6: multiple result sets are unaffected (sync + async)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NextResult_MultipleResultSets_AreUnchanged()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        TDSColMetadataToken first = SingleIntColumnMetadata();
        TDSColMetadataToken second = SingleIntColumnMetadata();
        RespondWith(
            first,
            IntRow(first, 1),
            new TDSDoneToken(TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1),
            second,
            IntRow(second, 2),
            new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1));

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = command.ExecuteReader();

        List<int> values = new();
        do
        {
            while (reader.Read())
            {
                values.Add(reader.GetInt32(0));
            }
        }
        while (reader.NextResult());

        Assert.Equal(new[] { 1, 2 }, values);
    }

    [Fact]
    public async Task NextResultAsync_MultipleResultSets_AreUnchanged()
    {
        using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();

        TDSColMetadataToken first = SingleIntColumnMetadata();
        TDSColMetadataToken second = SingleIntColumnMetadata();
        RespondWith(
            first,
            IntRow(first, 1),
            new TDSDoneToken(TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1),
            second,
            IntRow(second, 2),
            new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1));

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        List<int> values = new();
        do
        {
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetInt32(0));
            }
        }
        while (await reader.NextResultAsync());

        Assert.Equal(new[] { 1, 2 }, values);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 7: the error still surfaces when the caller drains via NextResult()
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NextResult_ErrorAfterResultSetDone_ThrowsSqlException()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        RespondWithTrailingError();

        SqlException ex = Assert.ThrowsAny<SqlException>(() =>
        {
            using SqlCommand command = CreateCommand(connection);
            using SqlDataReader reader = command.ExecuteReader();
            do
            {
                while (reader.Read())
                {
                }
            }
            while (reader.NextResult());
        });

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }
}
