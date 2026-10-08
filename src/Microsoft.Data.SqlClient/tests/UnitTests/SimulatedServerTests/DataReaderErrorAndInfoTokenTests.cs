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
/// Covers how <see cref="SqlDataReader"/> surfaces ERROR and INFO tokens depending on
/// <em>where</em> they sit in the TDS token stream: before the result set's DONE token, after it,
/// or behind a later result set's COLMETADATA token.
///
/// The motivating defect is https://github.com/dotnet/SqlClient/issues/4321. A T-SQL error
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
/// <c>SqlDataReader.TryHasMoreRows</c> used to stop consuming ERROR/INFO tokens once any DONE
/// token had been seen, so the trailing ERROR was never surfaced and the caller saw an empty
/// result set instead of a <see cref="SqlException"/>.
///
/// Because the fix widens which trailing tokens are consumed, this class deliberately covers more
/// than the trailing-error repro alone. It also pins the behavior the fix must <em>not</em>
/// disturb: leading errors, legitimately empty result sets, informational messages keeping their
/// severity, multi-result-set iteration, and the COLMETADATA barrier that stops a later result
/// set's ERROR or INFO token from being pulled forward into the current one.
///
/// Every test replays an exact token sequence against the in-process simulated TDS server, so the
/// regressions are caught in CI without a live SQL Server. Each test runs twice, once for the sync
/// read path and once for the async one, because they drain the token stream through different
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
    /// <returns>The reader, which the caller owns and must dispose.</returns>
    private static async Task<SqlDataReader> ExecuteReader(SqlCommand command, bool async)
    {
        if (async)
        {
            return await command.ExecuteReaderAsync();
        }

        return command.ExecuteReader();
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
    /// empty query, used to prove the fix does not manufacture errors where none were sent.
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
    /// trailing-token path the fix now consumes.
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
    // Test 1: the issue repro - a trailing ERROR must surface
    // --------------------------------------------------------------------------

    /// <summary>
    /// The core regression for issue #4321: when the ERROR token follows the DONE token that
    /// closed the result set, the reader must still raise it. Before the fix the token was dropped
    /// and the caller saw an empty result set instead.
    ///
    /// The result set is empty, so no row can be returned and the <em>first</em> read is the one
    /// that reaches the trailing ERROR token - the caller makes no "extra" call to observe it.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_ErrorAfterResultSetDone_ThrowsSqlException(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithTrailingError();

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // Only the read is expected to throw; execution must have succeeded.
        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(() => Read(reader, async));

        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
        Assert.Contains(DivideByZeroMessage, ex.Message);
    }

    /// <summary>
    /// A trailing class-16 ERROR must reach InfoMessage instead of throwing when
    /// FireInfoMessageEventOnUserErrors is enabled. Checks delivery during Read, before
    /// NextResult or disposal can drain the stream and hide a missed event.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_TrailingError_WithInfoMessageEnabled_RaisesEvent(bool async)
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

        // The empty result's first read must deliver the ERROR as an event, not throw.
        Assert.False(await Read(reader, async));
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
    /// Guards the shape that always worked - the ERROR token ahead of every DONE token, as a bare
    /// failing statement sends it - so the fix for issue #4321 is proven additive rather than a
    /// relocation of error handling onto the trailing-token path.
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
    // Test 4: rows are still delivered before a trailing error is raised
    // --------------------------------------------------------------------------

    /// <summary>
    /// Ensures the fix for issue #4321 does not discard data already sent: the row preceding a
    /// trailing ERROR token must be handed to the caller first, and the exception raised only by
    /// the next read - which is the same read that would have reported the end of the result set.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_RowsThenTrailingError_DeliversRowsThenThrows(bool async)
    {
        using SqlConnection connection = new(_connectionString);
        await Open(connection, async);
        RespondWithRowsThenTrailingError(42);

        using SqlCommand command = CreateCommand(connection);
        using SqlDataReader reader = await ExecuteReader(command, async);

        // The row that was already produced is delivered normally.
        Assert.True(await Read(reader, async));
        Assert.Equal(42, reader.GetInt32(0));

        // The read that would otherwise have returned false raises the error instead.
        SqlException ex = await Assert.ThrowsAnyAsync<SqlException>(() => Read(reader, async));
        Assert.Equal((int)DivideByZeroErrorNumber, ex.Number);
    }

    // --------------------------------------------------------------------------
    // Test 5: trailing informational messages stay informational
    // --------------------------------------------------------------------------

    /// <summary>
    /// Severity guard for issue #4321: an INFO token trailing the result set travels the same
    /// path the fix now consumes, so it must still be delivered through
    /// <see cref="SqlConnection.InfoMessage"/> and must not be promoted into an exception.
    /// </summary>
    /// <param name="async"><see langword="true"/> to exercise the async read path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_TrailingInfoMessage_IsRaisedAsInfoNotError(bool async)
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
        }

        Assert.Contains(InfoText, infoMessages);
    }

    // --------------------------------------------------------------------------
    // Test 6: multiple result sets are unaffected
    // --------------------------------------------------------------------------

    /// <summary>
    /// Ensures ordinary multi-result-set iteration is untouched by the fix for issue #4321: each
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

    // --------------------------------------------------------------------------
    // Test 7: the error also surfaces when the caller drains via NextResult()
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
