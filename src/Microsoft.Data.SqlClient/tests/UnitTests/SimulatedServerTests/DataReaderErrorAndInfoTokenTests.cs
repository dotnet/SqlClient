// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
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
/// Covers how <see cref="SqlDataReader"/> surfaces ERROR and INFO tokens depending on
/// <em>where</em> they sit in the TDS token stream: before the result set's DONE token, after it,
/// or behind a later result set's COLMETADATA token.
///
/// The motivating report is https://github.com/dotnet/SqlClient/issues/4321. A T-SQL error
/// re-raised from a CATCH block (<c>BEGIN TRY ... END TRY BEGIN CATCH THROW END CATCH</c>) arrives
/// on the wire <em>after</em> the DONE token that terminates the current result set:
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
/// Read ends the current result set; NextResult processes errors that follow its DONE token.
/// Closing instead discards pending nonfatal errors. This preserves the result boundary observed
/// with Microsoft ODBC and in the Microsoft JDBC direct, forward-only result implementation.
///
/// Guards cover leading errors, empty results, message severity and ordering, explicit result
/// advancement, and cleanup. SingleResult's existing discard behavior is tested separately;
/// it must not be used when the caller needs to observe errors across the entire response.
///
/// Every test replays an exact token sequence against the in-process simulated TDS server, so the
/// regressions are caught in CI without a live SQL Server. Tests cover both sync and async
/// read paths, because they drain the token stream through different
/// internal machinery.
/// </summary>
// Serializes execution with the other SimulatedServerTests classes.
[Collection(SimulatedServerTestCollection.Name)]
public class DataReaderErrorAndInfoTokenTests : IDisposable
{
    private const uint DivideByZeroErrorNumber = 8134;
    private const string DivideByZeroMessage = "Divide by zero error encountered.";

    private readonly TdsServerFixture _fixture;
    private readonly TdsServer _server;
    private readonly string _connectionString;

    /// <summary>
    /// Starts a dedicated in-process simulated TDS server and builds a connection string pointing
    /// at it. Pooling is disabled so each test gets a fresh physical connection and cannot inherit
    /// another test's token stream.
    /// </summary>
    public DataReaderErrorAndInfoTokenTests()
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

    /// <summary>
    /// Detaches the batch-response handler before shutting the simulated server down, so a
    /// handler installed by one test cannot outlive it.
    /// </summary>
    public void Dispose()
    {
        _server.OnSQLBatchCompleted = null;
        _fixture.Dispose();
    }

    // --------------------------------------------------------------------------
    // Sync/async shims
    //
    // Every test is a [Theory] over a bool "async" flag so the sync and async reader paths are
    // exercised by one test body instead of two near-identical copies. These shims are the only
    // place the branch appears.
    // --------------------------------------------------------------------------

    /// <summary>
    /// Opens <paramref name="connection"/> on the sync or async path.
    /// </summary>
    /// <param name="connection">The connection to open. Side effect: it is left open.</param>
    /// <param name="async">
    /// <see langword="true"/> to call <see cref="SqlConnection.OpenAsync()"/>;
    /// <see langword="false"/> to call <see cref="SqlConnection.Open"/>.
    /// </param>
    /// <returns>A task that completes once the connection is open.</returns>
    private static async Task Open(SqlConnection connection, bool async)
    {
        if (async)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
    }

    /// <summary>
    /// Executes <paramref name="command"/> on the sync or async path.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="async">
    /// <see langword="true"/> to call <see cref="SqlCommand.ExecuteReaderAsync()"/>;
    /// <see langword="false"/> to call <see cref="SqlCommand.ExecuteReader()"/>.
    /// </param>
    /// <param name="behavior">The result-consumption behavior for the command.</param>
    /// <returns>The reader, which the caller owns and must dispose.</returns>
    private static async Task<SqlDataReader> ExecuteReader(
        SqlCommand command, bool async, CommandBehavior behavior = CommandBehavior.Default)
    {
        if (async)
        {
            return await command.ExecuteReaderAsync(behavior);
        }

        return command.ExecuteReader(behavior);
    }

    /// <summary>
    /// Advances <paramref name="reader"/> by one row on the sync or async path.
    /// </summary>
    /// <param name="reader">
    /// The reader to advance. Side effect: the reader's position moves.
    /// </param>
    /// <param name="async">
    /// <see langword="true"/> to call <see cref="SqlDataReader.ReadAsync()"/>;
    /// <see langword="false"/> to call <see cref="SqlDataReader.Read"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a row was read; otherwise <see langword="false"/>.
    /// </returns>
    private static async Task<bool> Read(SqlDataReader reader, bool async)
    {
        if (async)
        {
            return await reader.ReadAsync();
        }

        return reader.Read();
    }

    /// <summary>
    /// Advances <paramref name="reader"/> to the next result set on the sync or async path.
    /// </summary>
    /// <param name="reader">
    /// The reader to advance. Side effect: the reader's position moves.
    /// </param>
    /// <param name="async">
    /// <see langword="true"/> to call <see cref="SqlDataReader.NextResultAsync()"/>;
    /// <see langword="false"/> to call <see cref="SqlDataReader.NextResult"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if another result set exists; otherwise <see langword="false"/>.
    /// </returns>
    private static async Task<bool> NextResult(SqlDataReader reader, bool async)
    {
        if (async)
        {
            return await reader.NextResultAsync();
        }

        return reader.NextResult();
    }

    // --------------------------------------------------------------------------
    // Token-stream builders
    // --------------------------------------------------------------------------

    /// <summary>
    /// Builds COLMETADATA describing a single nullable, read-only integer column - the minimum
    /// shape a result set needs for the reader to expose rows.
    /// </summary>
    /// <returns>Metadata to emit, and to bind that result set's rows against.</returns>
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

    /// <summary>
    /// Builds a single-column ROW token carrying <paramref name="value"/>.
    /// </summary>
    /// <param name="metadata">
    /// The COLMETADATA the row is bound to. Each result set must be given its own metadata
    /// instance, otherwise the rows of one set are encoded against another set's layout.
    /// </param>
    /// <param name="value">The integer the row's only column holds.</param>
    /// <returns>A ROW token ready to be appended to a simulated response.</returns>
    private static TDSRowToken IntRow(TDSColMetadataToken metadata, int value)
    {
        TDSRowToken row = new(metadata);
        row.Data.Add(value);
        return row;
    }

    /// <summary>
    /// Builds the ERROR token used throughout these tests: server error 8134 ("Divide by zero"),
    /// severity 16, matching what SQL Server sends for the batches in issue #4321.
    /// </summary>
    /// <returns>
    /// An ERROR token that normally becomes a <see cref="SqlException"/>, or an
    /// <see cref="SqlConnection.InfoMessage"/> when user-error forwarding is enabled.
    /// </returns>
    private static TDSErrorToken DivideByZeroError() =>
        new(DivideByZeroErrorNumber, 1, 16, DivideByZeroMessage, "simulated", string.Empty, 2);

    /// <summary>
    /// Replaces the simulated server's canned batch response with <paramref name="tokens"/>.
    /// </summary>
    /// <remarks>
    /// Side effect: installs a handler on the shared simulated server, which
    /// <see cref="Dispose"/> clears. Must be called only after the connection is open, so that
    /// the login exchange still receives the server's normal responses.
    /// </remarks>
    /// <param name="tokens">The exact token stream the next batch should return, in order.</param>
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
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 0),
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

    /// <summary>
    /// Emits an empty result set whose DONE token is followed by nothing at all - a legitimately
    /// empty query, used to prove advancement does not manufacture errors where none were sent.
    /// </summary>
    private void RespondWithEmptyResultSet()
    {
        RespondWith(
            SingleIntColumnMetadata(),
            new TDSDoneToken(
                TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 0));
    }

    /// <summary>
    /// Emits one row followed by the result set's DONE token and then an ERROR token, modelling
    /// a batch that produced data before failing.
    /// </summary>
    /// <param name="value">The integer carried by the single row that precedes the error.</param>
    private void RespondWithRowsThenTrailingError(int value)
    {
        TDSColMetadataToken metadata = SingleIntColumnMetadata();
        RespondWith(
            metadata,
            IntRow(metadata, value),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1),
            DivideByZeroError(),
            new TDSDoneToken(TDSDoneTokenStatusType.Error, TDSDoneTokenCommandType.Done, 0));
    }

    /// <summary>
    /// Emits one row followed by the result set's DONE token and then an INFO token - the
    /// severity counterpart of <see cref="RespondWithRowsThenTrailingError"/>, travelling the same
    /// result boundary.
    /// </summary>
    /// <param name="value">The integer carried by the single row that precedes the message.</param>
    /// <param name="message">The informational text the INFO token carries.</param>
    private void RespondWithRowsThenTrailingInfoMessage(int value, string message)
    {
        TDSColMetadataToken metadata = SingleIntColumnMetadata();
        RespondWith(
            metadata,
            IntRow(metadata, value),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1),
            new TDSInfoToken(50000, 1, 0, message, "simulated", string.Empty, 1),
            new TDSDoneToken(TDSDoneTokenStatusType.Final, TDSDoneTokenCommandType.Done, 0));
    }

    /// <summary>
    /// Emits two well-formed result sets, each with its own COLMETADATA and a single row.
    /// </summary>
    private void RespondWithTwoResultSets()
    {
        TDSColMetadataToken first = SingleIntColumnMetadata();
        TDSColMetadataToken second = SingleIntColumnMetadata();
        RespondWith(
            first,
            IntRow(first, 1),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1),
            second,
            IntRow(second, 2),
            new TDSDoneToken(
                TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1));
    }

    /// <summary>
    /// Builds the "later result set carries its own metadata" shape:
    ///
    /// <code>
    /// COLMETADATA(rs1) ROW(1) DONE(More|Count)
    /// COLMETADATA(rs2) ROW(2) ERROR(8134) DONE(Error)
    /// </code>
    ///
    /// <c>TryHasMoreRows</c> consumes DONE/ERROR/INFO tokens but deliberately stops at
    /// COLMETADATA, so rs2's ERROR sits behind that barrier and is unreachable while the
    /// caller is still draining rs1.
    /// </summary>
    private void RespondWithErrorInSecondResultSet()
    {
        TDSColMetadataToken first = SingleIntColumnMetadata();
        TDSColMetadataToken second = SingleIntColumnMetadata();
        RespondWith(
            first,
            IntRow(first, 1),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1),
            second,
            IntRow(second, 2),
            DivideByZeroError(),
            new TDSDoneToken(TDSDoneTokenStatusType.Error, TDSDoneTokenCommandType.Done, 0));
    }

    /// <summary>
    /// Places an INFO token behind the second result set's COLMETADATA:
    ///
    /// <code>
    /// COLMETADATA(rs1) ROW(1) DONE(More|Count)
    /// COLMETADATA(rs2) INFO("...") ROW(2) DONE(Final|Count)
    /// </code>
    /// </summary>
    private void RespondWithInfoMessageInSecondResultSet()
    {
        TDSColMetadataToken first = SingleIntColumnMetadata();
        TDSColMetadataToken second = SingleIntColumnMetadata();
        RespondWith(
            first,
            IntRow(first, 1),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1),
            second,
            new TDSInfoToken(50000, 1, 0, DeferredInfoMessage, "simulated", string.Empty, 1),
            IntRow(second, 2),
            new TDSDoneToken(
                TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1));
    }

    /// <summary>
    /// Creates the command used to trigger a batch. Its text is irrelevant: the simulated server
    /// answers every batch with whatever <see cref="RespondWith"/> last installed.
    /// </summary>
    /// <param name="connection">The open connection to issue the batch on.</param>
    /// <returns>A command whose execution replays the configured token stream.</returns>
    private static SqlCommand CreateCommand(SqlConnection connection) =>
        new("SELECT 1", connection);

    // --------------------------------------------------------------------------
    // Test 1: the issue repro - a trailing ERROR belongs to result advancement
    // --------------------------------------------------------------------------

    /// <summary>
    /// The empty result in issue #4321 ends before the ERROR token. Read reports no rows;
    /// explicitly advancing reports the error, even though no second result set exists.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_AfterEmptyResult_ThrowsTrailingError(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithTrailingError();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        Assert.False(await Read(reader, async));
        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(
            () => NextResult(reader, async));

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
        Assert.Contains(DivideByZeroMessage, ex.Message);
    }

    /// <summary>
    /// A trailing class-16 ERROR must reach InfoMessage instead of throwing when
    /// FireInfoMessageEventOnUserErrors is enabled. Checks that delivery is deferred until
    /// NextResult, rather than pulled forward into Read or lost during disposal.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_TrailingError_WithInfoMessageEnabled_RaisesEvent(bool async)
    {
        using SqlConnection connection = new(_connectionString)
        {
            FireInfoMessageEventOnUserErrors = true
        };
        await Open(connection, async);

        List<SqlInfoMessageEventArgs> messages = new();
        connection.InfoMessage += (_, e) => messages.Add(e);
        RespondWithTrailingError();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);
        Assert.Empty(messages);

        Assert.False(await Read(reader, async));
        Assert.Empty(messages);
        Assert.False(await NextResult(reader, async));
        SqlInfoMessageEventArgs message = Assert.Single(messages);
        Assert.Single(message.Errors);
        SqlError error = message.Errors[0];
        Assert.Equal((int)DivideByZeroErrorNumber, error.Number);
        Assert.Equal((byte)16, error.Class);
        Assert.Equal(DivideByZeroMessage, error.Message);

        Assert.False(await NextResult(reader, async));
        Assert.Single(messages);
    }

    // --------------------------------------------------------------------------
    // Test 2: the pre-existing working case must keep working
    // --------------------------------------------------------------------------

    /// <summary>
    /// An ERROR before the result's DONE still throws during Read. Deferring trailing errors
    /// must not defer errors encountered inside the current result set.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_ErrorBeforeDone_StillThrowsSqlException(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithLeadingError();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(() => Read(reader, async));

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // --------------------------------------------------------------------------
    // Test 3: a genuinely empty result set must stay empty and must not throw
    // --------------------------------------------------------------------------

    /// <summary>
    /// The false-positive guard for issue #4321: a result set terminated by a DONE token with no
    /// error behind it must report zero rows, raise nothing, and report no further result sets.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_EmptyResultSetWithoutError_ReturnsNoRowsAndDoesNotThrow(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithEmptyResultSet();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // No rows, no error, no further result sets.
        Assert.False(await Read(reader, async));
        Assert.False(await NextResult(reader, async));
    }

    // --------------------------------------------------------------------------
    // Test 4: rows end normally before result advancement reports a trailing error
    // --------------------------------------------------------------------------

    /// <summary>
    /// A completed result's row is returned normally. Read then reports the end of that result,
    /// and NextResult reports the ERROR following its DONE token.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_AfterRows_ThrowsTrailingError(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithRowsThenTrailingError(42);

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // The row that was already produced is delivered normally.
        Assert.True(await Read(reader, async));
        Assert.Equal(42, reader.GetInt32(0));

        Assert.False(await Read(reader, async));
        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(
            () => NextResult(reader, async));
        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // --------------------------------------------------------------------------
    // Test 5: trailing informational messages stay informational
    // --------------------------------------------------------------------------

    /// <summary>
    /// An INFO following DONE is deferred until NextResult and remains informational.
    /// Assertions before disposal distinguish explicit advancement from cleanup delivery.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_TrailingInfoMessage_IsDeferredAndRaisedAsInfo(bool async)
    {
        const string InfoText = "informational only";

        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);

        List<string> infoMessages = new();
        connection.InfoMessage += (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                infoMessages.Add(error.Message);
            }
        };

        RespondWithRowsThenTrailingInfoMessage(7, InfoText);

        using (SqlCommand command = CreateCommand(connection))
        using (SqlDataReader reader = await ExecuteReader(command, async))
        {
            // Exactly one row, then a clean end of result set - the INFO token must not throw
            // and must not be mistaken for a row.
            Assert.True(await Read(reader, async));
            Assert.Equal(7, reader.GetInt32(0));
            Assert.False(await Read(reader, async));
            Assert.Empty(infoMessages);
            Assert.False(await NextResult(reader, async));
            Assert.Equal(InfoText, Assert.Single(infoMessages));
        }

        Assert.Contains(InfoText, infoMessages);
    }

    // --------------------------------------------------------------------------
    // Test 6: multiple result sets are unaffected
    // --------------------------------------------------------------------------

    /// <summary>
    /// Ensures ordinary multi-result-set iteration retains its boundaries: each
    /// set yields exactly its own row, and the reader reports the end of the batch afterwards.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_MultipleResultSets_AreUnchanged(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithTwoResultSets();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // Result set 1 holds exactly one row, carrying 1.
        Assert.True(await Read(reader, async));
        Assert.Equal(1, reader.GetInt32(0));
        Assert.False(await Read(reader, async));

        // Result set 2 holds exactly one row, carrying 2.
        Assert.True(await NextResult(reader, async));
        Assert.True(await Read(reader, async));
        Assert.Equal(2, reader.GetInt32(0));
        Assert.False(await Read(reader, async));

        // ... and the batch ends there.
        Assert.False(await NextResult(reader, async));
    }

    /// <summary>
    /// An empty result is not the end of the response. Advancing must still expose a later
    /// result's row and report that result's trailing error on a subsequent NextResult call.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise async result advancement.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_EmptyFirstResult_ThenRowsAndTrailingError(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        TDSColMetadataToken second = SingleIntColumnMetadata();
        RespondWith(
            SingleIntColumnMetadata(),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 0),
            second,
            IntRow(second, 2),
            new TDSDoneToken(
                TDSDoneTokenStatusType.More | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1),
            DivideByZeroError(),
            new TDSDoneToken(TDSDoneTokenStatusType.Error, TDSDoneTokenCommandType.Done, 0));

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);
        Assert.False(await Read(reader, async));
        Assert.True(await NextResult(reader, async));
        Assert.True(await Read(reader, async));
        Assert.Equal(2, reader.GetInt32(0));
        Assert.False(await Read(reader, async));
        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(
            () => NextResult(reader, async));
        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // --------------------------------------------------------------------------
    // Test 7: advancing directly also reports trailing errors
    // --------------------------------------------------------------------------

    /// <summary>
    /// Covers the alternative drain style for the issue #4321 stream: a caller that never reads a
    /// row and advances straight to the next result set must also receive the trailing error,
    /// because advancing drains the remainder of the current set.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_ErrorAfterResultSetDone_ThrowsSqlException(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithTrailingError();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(
            () => NextResult(reader, async));

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    /// <summary>
    /// Closing early discards pending class-16 errors rather than introducing cleanup
    /// exceptions. Tests both Close and Dispose without first advancing or exhausting rows.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise async execution and reading.</param>
    /// <param name="dispose">
    /// <see langword="true"/> to dispose instead of explicitly closing.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CloseOrDispose_AfterOneRow_DiscardsTrailingError(bool async, bool dispose)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithRowsThenTrailingError(42);

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);
        Assert.True(await Read(reader, async));
        Assert.Equal(42, reader.GetInt32(0));

        if (dispose)
        {
            reader.Dispose();
        }
        else
        {
            reader.Close();
        }

        Assert.True(reader.IsClosed);
        RespondWithEmptyResultSet();
        using SqlDataReader nextReader = await ExecuteReader(command, async);
        Assert.False(await Read(nextReader, async));
        Assert.False(await NextResult(nextReader, async));
    }

    /// <summary>
    /// SingleResult retains its existing clean-drain behavior. It is not a replacement for
    /// default result advancement when the application must observe trailing errors.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise async result advancement.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextResult_SingleResult_DiscardsTrailingError(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithTrailingError();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(
            command, async, CommandBehavior.SingleResult);

        Assert.False(await Read(reader, async));
        Assert.False(await NextResult(reader, async));
    }

    // --------------------------------------------------------------------------
    // Test 8: the COLMETADATA barrier - an error belonging to a *later* result
    // set must not be pulled forward into the current one
    // --------------------------------------------------------------------------

    /// <summary>
    /// Barrier guard: an error belonging to a later result set must not be pulled forward into the
    /// current one. Draining result set 1 yields only its own row and must not throw, because the
    /// trailing-token loop stops at result set 2's COLMETADATA; the error must then surface while
    /// the caller reads result set 2.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_ErrorInSecondResultSet_DoesNotSurfaceWhileDrainingFirst(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithErrorInSecondResultSet();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // Result set 1 yields exactly its own row and must NOT throw: result set 2's
        // ERROR token is behind the COLMETADATA barrier.
        Assert.True(await Read(reader, async));
        Assert.Equal(1, reader.GetInt32(0));
        Assert.False(await Read(reader, async));

        // Advancing crosses the barrier and exposes result set 2's row...
        Assert.True(await NextResult(reader, async));
        Assert.True(await Read(reader, async));
        Assert.Equal(2, reader.GetInt32(0));

        // ... and only then is the error raised, by the read that reaches it.
        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(() => Read(reader, async));
        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // --------------------------------------------------------------------------
    // Test 9: InfoMessage ordering across result sets - a message belonging to a
    // later result set must not fire early
    // --------------------------------------------------------------------------

    private const string DeferredInfoMessage = "belongs to the second result set";

    /// <summary>
    /// Ordering guard for <see cref="SqlConnection.InfoMessage"/>: a message belonging to a later
    /// result set must not be raised early. The assertions are interleaved with the reader calls,
    /// so they pin precisely when the event fires: the collected messages must still be empty at
    /// the moment result set 1 ends, and must contain the message once the reader has advanced.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InfoMessage_BelongingToLaterResultSet_DoesNotFireWhileReadingEarlierOne(
        bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);

        // Every informational message is appended here as it arrives, so the position of an
        // assertion within the call sequence below is what establishes the firing point.
        List<string> messages = new();
        connection.InfoMessage += (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                messages.Add(error.Message);
            }
        };

        RespondWithInfoMessageInSecondResultSet();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // Result set 1 holds exactly one row, carrying 1.
        Assert.True(await Read(reader, async));
        Assert.Equal(1, reader.GetInt32(0));

        // The INFO token sits behind result set 2's COLMETADATA, so the read that ends result
        // set 1 must not reach it: nothing has been raised at this point in the sequence.
        Assert.False(await Read(reader, async));
        Assert.Empty(messages);

        // Advancing consumes result set 2's COLMETADATA and stops there, so the INFO token
        // that sits behind it has still not been reached.
        Assert.True(await NextResult(reader, async));
        Assert.Empty(messages);

        // The read that reaches result set 2's row passes over the INFO token on the way, and
        // that is the call which delivers the message.
        Assert.True(await Read(reader, async));
        Assert.Single(messages);
        Assert.Contains(DeferredInfoMessage, messages[0]);
        Assert.Equal(2, reader.GetInt32(0));

        // Result set 2 holds only that one row, and the batch ends there.
        Assert.False(await Read(reader, async));
        Assert.False(await NextResult(reader, async));

        // The message was raised once, as information, and never as an exception.
        Assert.Single(messages);
    }
}
