// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
    private readonly InfoQueryEngine _engine;
    private readonly TdsServer _server;
    private readonly SqlConnection _connection;
    private readonly List<string> _infoMessagesReceived = new();

    public HasRowsTests(ITestOutputHelper output)
    {
        _engine = new(new(){ Log = new LogWriter(output) });

        _server = new(_engine);
        _server.Start();

        var connStr = new SqlConnectionStringBuilder()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
        }.ConnectionString;

        _connection = new SqlConnection(connStr);

        _connection.InfoMessage += new(
            (object sender, SqlInfoMessageEventArgs imevent) =>
            {
                // The informational messages are exposed as Errors for some
                // reason.  Capture them in order.
                for (int i = 0; i < imevent.Errors.Count; i++)
                {
                    _infoMessagesReceived.Add(imevent.Errors[i].Message);
                }
            });
        
        _connection.Open();
    }

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
            "select 'Hello, World!'",
            _connection);
        using SqlDataReader reader = command.ExecuteReader();

        // We should not have detected any rows.
        Assert.False(reader.HasRows);

        // Verify that we received the expected 2 INFO messages with the
        // expected text.
        Assert.Equal(2, _infoMessagesReceived.Count);
        Assert.Equal("select 'Hello, World!'", _infoMessagesReceived[0]);
        Assert.Equal(
            "Received query is not recognized by the query engine. Please " +
            "ask a very specific question.",
            _infoMessagesReceived[1]);

        // Confirm that we really didn't get any rows.
        Assert.False(reader.Read());
    }

    // Verify that HasRows is true when more than one INFO token is included
    // in the response to a SQL batch that returns rows.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void InfoAndRows(ushort infoCount)
    {
        // Configure the engine to include the desired number of INFO tokens
        // with its response.
        _engine.InfoCount = infoCount;

        using SqlCommand command = new(
            // Use command text that is intercepted by the InfoQueryEngine.
            InfoQueryEngine.InfoCommandText,
            _connection);
        using SqlDataReader reader = command.ExecuteReader();

        // We should have read past the INFO tokens and determined that there
        // are row results.
        Assert.True(reader.HasRows);
        
        // Verify that we received the expected number of INFO messages with
        // the expected text.
        Assert.Equal(infoCount, (ushort)_infoMessagesReceived.Count);
        for (ushort i = 0; i < infoCount; i++)
        {
            Assert.Equal($"Info message {i}", _infoMessagesReceived[i]);
        }

        // Verify that we can read the single row.
        Assert.True(reader.Read());
        Assert.Equal("Foo Value", reader.GetString(0));
        Assert.False(reader.Read());
    }
}

// A writer compatible with TdsUtilities.Log() that pumps accumulated log
// messages to an xUnit output helper.
internal sealed class LogWriter : StringWriter
{
    private readonly ITestOutputHelper _output;

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
            _output.WriteLine(text);
            base.Flush();
        }
        
        // Clear the buffer for the next accumulation.
        builder.Clear();
    }
}

// A query engine that can include INFO tokens in its response.
internal sealed class InfoQueryEngine : QueryEngine
{
    // The query text that this engine recognizes and will trigger the
    // inclusion of InfoCount INFO tokens in the response.
    internal const string InfoCommandText = "select Foo from Bar";

    // The number of INFO tokens to include in the response.
    internal ushort InfoCount { get; set; } = 0;

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
        if (batchRequest.Text != InfoCommandText)
        {
            return base.CreateQueryResponse(session, batchRequest);
        }

        // Build a response with the desired number of INFO tokens and then
        // one row result.
        TDSMessage response = new(TDSMessageType.Response);

        // Add the INFO tokens first.
        for (ushort i = 0; i < InfoCount; i++)
        {
            // Choose an error code outside the reserved range.
            TDSInfoToken token = new(30000u + i, 0, 0, $"Info message {i}");
            response.Add(token);
            TDSUtilities.Log(Log, "INFO Response", token);
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

        // Add the row result data.
        TDSRowToken rowToken = new(metadataToken);
        rowToken.Data.Add("Foo Value");
        response.Add(rowToken);
        TDSUtilities.Log(Log, "INFO Response", rowToken);

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
