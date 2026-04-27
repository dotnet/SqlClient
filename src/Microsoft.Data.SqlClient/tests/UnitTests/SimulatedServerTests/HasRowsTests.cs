// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Row;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.Tests.UnitTests.SimulatedServerTests;

public sealed class HasRowsTests : IDisposable
{
    // The query engine used by the server.
    private readonly InfoQueryEngine _engine;

    // The TDS server we will connect to.
    private readonly TdsServer _server;

    // The connection to the server; always open post-construction.
    private readonly SqlConnection _connection;
    
    // The list of INFO message text received.
    private readonly List<string> _infoText = new();

    // Construct to setup the server and connection.
    public HasRowsTests(ITestOutputHelper output)
    {
        // Use our log writer to capture TDS Server logs to the xUnit output.
        _engine = new(new() { Log = new LogWriter(output) });

        // Start the TDS server.
        _server = new(_engine);
        _server.Start();

        // Use the server's endpoint to build a connection string.
        var connStr = new SqlConnectionStringBuilder()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
        }.ConnectionString;

        // Create the connection.
        _connection = new SqlConnection(connStr);

        // Add a handler for INFO messages to capture them.
        _connection.InfoMessage += new(
            (object sender, SqlInfoMessageEventArgs imevent) =>
            {
                // The informational messages are exposed as Errors for some
                // reason.  Capture them in order.
                for (int i = 0; i < imevent.Errors.Count; i++)
                {
                    _infoText.Add(imevent.Errors[i].Message);
                }
            });

        // Open the connection.
        _connection.Open();
    }

    // Dispose of resources.
    public void Dispose()
    {
        _connection.Dispose();
        _server.Dispose();
    }

    // Verify that HasRows is not set when we only receive INFO tokens.
    [Fact]
    public void OnlyInfo()
    {
        using SqlCommand command = new(
            // Use command text that isn't recognized by the query engine.  This
            // should elicit a response that includes 2 INFO tokens and no row
            // results.
            //
            // See QueryEngine.CreateQueryResponse()'s else block.
            //
            "select 'Hello, World!'",
            _connection);
        using SqlDataReader reader = command.ExecuteReader();

        // We should not have detected any rows.
        Assert.False(reader.HasRows);

        // Verify that we received the expected 2 INFO messages.
        Assert.Equal(2, _infoText.Count);
        Assert.Equal("select 'Hello, World!'", _infoText[0]);
        Assert.Equal(
            "Received query is not recognized by the query engine. Please " +
            "ask a very specific question.",
            _infoText[1]);

        // Confirm that we really didn't get any rows.
        Assert.False(reader.Read());
    }

    // Verify that HasRows is true when a variable number of INFO tokens is
    // included at the start of the response stream to a SQL batch that returns
    // rows.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void InfoAndRows_Start(ushort infoCount)
    {
        // Configure the engine to specify the desired number and placement of
        // INFO tokens with its response.
        _engine.InfoCount = infoCount;
        _engine.InfoPlacement = InfoPlacement.Start;

        using SqlCommand command = new(
            // Use command text that is intercepted by our engine.
            InfoQueryEngine.CommandText,
            _connection);
        using SqlDataReader reader = command.ExecuteReader();

        // We should have read past the INFO tokens and determined that there
        // are row results.
        Assert.True(reader.HasRows);

        // Verify that we received the expected INFO messages.
        Assert.Equal(infoCount, (ushort)_infoText.Count);
        for (ushort i = 0; i < infoCount; i++)
        {
            Assert.Equal($"{InfoQueryEngine.InfoPreamble}{i}", _infoText[i]);
        }
        _infoText.Clear();

        // Verify that we can read the single row.
        Assert.True(reader.Read());

        // HasRows never gets reset.
        Assert.True(reader.HasRows);

        // Verify the row data.
        Assert.Equal(InfoQueryEngine.RowData, reader.GetString(0));
        
        // No further rows.
        Assert.False(reader.Read());

        // HasRows never gets reset.
        Assert.True(reader.HasRows);

        // No further INFO tokens were found.
        Assert.Empty(_infoText);
    }

    // Verify that HasRows is true when a variable number of INFO tokens is
    // included in the middle of the response stream to a SQL batch that returns
    // rows.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void InfoAndRows_Middle(ushort infoCount)
    {
        // Configure the engine to specify the desired number and placement of
        // INFO tokens with its response.
        _engine.InfoCount = infoCount;
        _engine.InfoPlacement = InfoPlacement.Middle;

        using SqlCommand command = new(
            // Use command text that is intercepted by our engine.
            InfoQueryEngine.CommandText,
            _connection);
        using SqlDataReader reader = command.ExecuteReader();

        // TODO: HasRows should be true here, regardless of the number of INFO
        // tokens.
        bool hasRowsExpected = infoCount <= 1;
        Assert.Equal(hasRowsExpected, reader.HasRows);

        // Starting a reader consumes the column metadata and then stops, so
        // we haven't encountered the INFO tokens yet.
        Assert.Empty(_infoText);

        // Verify that we can read the single row.
        Assert.True(reader.Read());

        // TODO: HasRows should still be true - it never gets cleared.
        Assert.Equal(hasRowsExpected, reader.HasRows);

        // Reading into the first row reads past the INFO tokens, so we should
        // have them all accumulated now.
        Assert.Equal(infoCount, (ushort)_infoText.Count);
        for (ushort i = 0; i < infoCount; i++)
        {
            Assert.Equal($"{InfoQueryEngine.InfoPreamble}{i}", _infoText[i]);
        }
        _infoText.Clear();

        // Verify the row data.
        Assert.Equal(InfoQueryEngine.RowData, reader.GetString(0));

        // No further rows.
        Assert.False(reader.Read());

        // TODO: HasRows should still be true - it never gets cleared.
        Assert.Equal(hasRowsExpected, reader.HasRows);

        // No further INFO tokens were found.
        Assert.Empty(_infoText);
    }

    // Verify that HasRows is true when a variable number of INFO tokens is
    // included at the end of the response stream to a SQL batch that returns
    // rows.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void InfoAndRows_End(ushort infoCount)
    {
        // Configure the engine to specify the desired number and placement of
        // INFO tokens with its response.
        _engine.InfoCount = infoCount;
        _engine.InfoPlacement = InfoPlacement.End;

        using SqlCommand command = new(
            // Use command text that is intercepted by our engine.
            InfoQueryEngine.CommandText,
            _connection);
        using SqlDataReader reader = command.ExecuteReader();

        // We should have read the column metadata and determined that there
        // are row results.
        Assert.True(reader.HasRows);

        // Starting a reader consumes the column metadata and then stops, so
        // we haven't encountered the INFO tokens yet.
        Assert.Empty(_infoText);

        // Verify that we can read the single row.
        Assert.True(reader.Read());

        // HasRows never gets reset
        Assert.True(reader.HasRows);

        // Still no INFO tokens.
        Assert.Empty(_infoText);

        // Verify the row data.
        Assert.Equal(InfoQueryEngine.RowData, reader.GetString(0));

        // No further rows.
        Assert.False(reader.Read());
        Assert.True(reader.HasRows);
        
        // Verify that we received the expected INFO messages.
        Assert.Equal(infoCount, (ushort)_infoText.Count);
        for (ushort i = 0; i < infoCount; i++)
        {
            Assert.Equal($"{InfoQueryEngine.InfoPreamble}{i}", _infoText[i]);
        }
    }
}

// A writer compatible with TdsUtilities.Log() that pumps accumulated log
// messages to an xUnit output helper.
internal sealed class LogWriter : StringWriter
{
    private readonly ITestOutputHelper _output;

    // Disable emission if TEST_SUPPRESS_LOGGING is defined.
    private readonly bool _suppressLogging =
        Environment.GetEnvironmentVariable("TEST_SUPPRESS_LOGGING") is not null;

    public LogWriter(ITestOutputHelper output)
    {
        _output = output;
    }

    // The TDSUtilities.Log() method calls Flush() after each operation, so we
    // can use that to emit the accumulated messages here.
    public override void Flush()
    {
        // Get the accumulated buffer.
        var builder = GetStringBuilder();

        // Trim trailing whitespace, since _output always appends a newline.
        var text = builder.ToString().TrimEnd();

        // Emit if there's anything worthwhile.
        if (text.Length > 0)
        {
            // Suppress emission if requested, but still do everything else.
            if (!_suppressLogging)
            {
                _output.WriteLine(text);
            }
            base.Flush();
        }
        
        // Clear the buffer for the next accumulation.
        builder.Clear();
    }
}

// Indicates where in the response stream to place the INFO tokens.
public enum InfoPlacement
{
    // Place the INFO tokens before the column metadata and row data.
    Start,

    // Place the INFO tokens between the column metadata and row data.
    Middle,

    // Place the INFO tokens after the row data.
    End
}

// A query engine that can include INFO tokens in its response.
internal sealed class InfoQueryEngine : QueryEngine
{
    // The query text that this engine recognizes and will trigger the
    // inclusion of InfoCount INFO tokens in the response.
    internal const string CommandText = "select Foo from Bar";

    // The row data that this engine will return for the recognized query.
    internal const string RowData = "Foo Value";

    // The preamble for all INFO message text.  The 0-based index of the INFO
    // token will be appended to this to form the complete message text.
    internal const string InfoPreamble = "Info message ";

    // The number of INFO tokens to include in the response.
    internal ushort InfoCount { get; set; } = 0;

    // Determines where to place the INFO tokens in the response stream.
    internal InfoPlacement InfoPlacement { get; set; } = InfoPlacement.Start;

    // Construct with server arguments.
    internal InfoQueryEngine(TdsServerArguments arguments)
    : base(arguments)
    {
    }

    // Override to provide our INFO token response.
    //
    // Calls the base implementation for unrecognized commands.
    //
    protected override TDSMessageCollection CreateQueryResponse(
        ITDSServerSession session,
        TDSSQLBatchToken batchRequest)
    {
        // Defer to the base implementation for unrecognized commands.
        if (batchRequest.Text != CommandText)
        {
            return base.CreateQueryResponse(session, batchRequest);
        }

        // Build a response with the desired number of INFO tokens and then
        // one row result.
        TDSMessage response = new(TDSMessageType.Response);

        // Helper to add INFO tokens at the desired placement.
        var addInfoTokens = new Action(() =>
        {
            for (ushort i = 0; i < InfoCount; i++)
            {
                // Choose an error code outside the reserved range.
                TDSInfoToken token = new(30000u + i, 0, 0, $"{InfoPreamble}{i}");
                response.Add(token);
                TDSUtilities.Log(Log, "INFO Response", token);
            }
        });

        // Add INFO tokens at the start if desired.
        if (InfoPlacement == InfoPlacement.Start)
        {
            addInfoTokens();
        }

        // Add the column metadata.
        TDSColumnData column = new()
        {
            DataType = TDSDataType.NVarChar,
            // Magic foo copied from QueryEngine.
            DataTypeSpecific = new TDSShilohVarCharColumnSpecific(
                256, new TDSColumnDataCollation(13632521, 52))
        };
        column.Flags.Updatable = TDSColumnDataUpdatableFlag.ReadOnly;

        TDSColMetadataToken metadataToken = new();
        metadataToken.Columns.Add(column);
        response.Add(metadataToken);

        TDSUtilities.Log(Log, "INFO Response", metadataToken);

        // Add INFO tokens in the middle if desired.
        if (InfoPlacement == InfoPlacement.Middle)
        {
            addInfoTokens();
        }

        // Add the row result data.
        TDSRowToken rowToken = new(metadataToken);
        rowToken.Data.Add(RowData);
        response.Add(rowToken);
        TDSUtilities.Log(Log, "INFO Response", rowToken);

        // Add INFO tokens at the end if desired.
        if (InfoPlacement == InfoPlacement.End)
        {
            addInfoTokens();
        }

        // Add the done token.
        TDSDoneToken doneToken = new(
            TDSDoneTokenStatusType.Final |
            TDSDoneTokenStatusType.Count,
            TDSDoneTokenCommandType.Select,
            1);
        response.Add(doneToken);
        TDSUtilities.Log(Log, "INFO Response", doneToken);

        return new(response);
    }
}
