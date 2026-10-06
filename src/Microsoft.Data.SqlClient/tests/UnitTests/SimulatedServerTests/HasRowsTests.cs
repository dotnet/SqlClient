// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Order;
using Microsoft.SqlServer.TDS.Row;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Guards against issue #3018 using INFO-only responses and INFO tokens around metadata and rows,
/// without requiring a SQL Server instance.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class HasRowsTests
{
    /// <summary>Response shapes used to verify INFO processing and row detection.</summary>
    public enum InfoPlacement
    {
        BeforeMetadata,
        AfterMetadata,
        AfterOrder,
        AfterRows,
        InfoOnly
    }

    private const ushort AltMetadataId = 1;
    private static readonly int[] s_infoCounts = { 0, 1, 2, 3, 10 };

    /// <summary>Combines INFO counts with each placement around metadata and rows.</summary>
    /// <returns>Strongly typed INFO counts and placements for each result-shape test.</returns>
    public static TheoryData<int, InfoPlacement> InfoTokenCases()
    {
        TheoryData<int, InfoPlacement> cases = new();
        foreach (int infoCount in s_infoCounts)
        {
            foreach (InfoPlacement placement in new[]
            {
                InfoPlacement.BeforeMetadata,
                InfoPlacement.AfterMetadata,
                InfoPlacement.AfterOrder,
                InfoPlacement.AfterRows
            })
            {
                cases.Add(infoCount, placement);
            }
        }
        return cases;
    }

    /// <summary>Covers zero, one, and multiple INFO tokens without column metadata.</summary>
    /// <returns>Strongly typed INFO counts for metadata-free responses.</returns>
    public static TheoryData<int> InfoOnlyCases()
    {
        TheoryData<int> cases = new();
        foreach (int infoCount in s_infoCounts)
        {
            cases.Add(infoCount);
        }
        return cases;
    }

    /// <summary>Checks that INFO tokens neither hide nor consume the first regular row.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public void HasRows_WithPopulatedResult_ReturnsTrue(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: true, useAlternateMetadata: false);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        connection.Open();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = command.ExecuteReader();

        // Assert
        bool hasRowsBeforeRead = reader.HasRows;
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(hasRowsBeforeRead);
        Assert.True(reader.HasRows);
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(reader.Read());
        Assert.True(reader.HasRows);
        Assert.Equal(infoCount, infoMessages.Count);
        Assert.False(reader.NextResult());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Async counterpart: checks that INFO tokens neither hide nor consume the first regular row.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public async Task HasRows_WithPopulatedResult_ReturnsTrueAsync(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: true, useAlternateMetadata: false);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        await connection.OpenAsync();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        // Assert
        bool hasRowsBeforeRead = reader.HasRows;
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(hasRowsBeforeRead);
        Assert.True(reader.HasRows);
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(await reader.ReadAsync());
        Assert.True(reader.HasRows);
        Assert.Equal(infoCount, infoMessages.Count);
        Assert.False(await reader.NextResultAsync());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Checks that an empty result stays empty while all INFO messages are delivered.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public void HasRows_WithEmptyResult_ReturnsFalse(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: false, useAlternateMetadata: false);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        connection.Open();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = command.ExecuteReader();

        // Assert
        Assert.False(reader.HasRows);
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.False(reader.Read());
        Assert.False(reader.HasRows);
        Assert.False(reader.Read());
        Assert.False(reader.HasRows);
        Assert.Equal(infoCount, infoMessages.Count);
        Assert.False(reader.NextResult());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Async counterpart: checks that an empty result stays empty while all INFO messages are delivered.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public async Task HasRows_WithEmptyResult_ReturnsFalseAsync(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: false, useAlternateMetadata: false);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        await connection.OpenAsync();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        // Assert
        Assert.False(reader.HasRows);
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.False(await reader.ReadAsync());
        Assert.False(reader.HasRows);
        Assert.False(await reader.ReadAsync());
        Assert.False(reader.HasRows);
        Assert.Equal(infoCount, infoMessages.Count);
        Assert.False(await reader.NextResultAsync());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Checks separate regular and aggregate rows, result navigation, and deferred INFO delivery.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public void HasRows_WithRegularAndAlternateRows_PreservesBothResults(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: true, useAlternateMetadata: true);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        connection.Open();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = command.ExecuteReader();

        // Assert the regular result, including its first row and deferred messages.
        bool hasRowsBeforeRead = reader.HasRows;
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(hasRowsBeforeRead);
        Assert.True(reader.HasRows);
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(reader.Read());
        Assert.True(reader.HasRows);
        Assert.Equal(placement == InfoPlacement.AfterRows ? 0 : infoCount, infoMessages.Count);

        // Act: advance to the alternate result.
        Assert.True(reader.NextResult());

        // Assert the aggregate row is a separate result and is not consumed by HasRows.
        Assert.True(reader.HasRows);
        Assert.Equal("sum", reader.GetName(0));
        Assert.True(reader.Read());
        Assert.Equal(42, reader.GetInt32(0));
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(reader.Read());
        Assert.True(reader.HasRows);
        Assert.False(reader.NextResult());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Async counterpart: checks separate regular and aggregate rows, result navigation, and deferred INFO delivery.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public async Task HasRows_WithRegularAndAlternateRows_PreservesBothResultsAsync(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: true, useAlternateMetadata: true);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        await connection.OpenAsync();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        // Assert the regular result, including its first row and deferred messages.
        bool hasRowsBeforeRead = reader.HasRows;
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(hasRowsBeforeRead);
        Assert.True(reader.HasRows);
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(await reader.ReadAsync());
        Assert.True(reader.HasRows);
        Assert.Equal(placement == InfoPlacement.AfterRows ? 0 : infoCount, infoMessages.Count);

        // Act: advance to the alternate result.
        Assert.True(await reader.NextResultAsync());

        // Assert the aggregate row is a separate result and is not consumed by HasRows.
        Assert.True(reader.HasRows);
        Assert.Equal("sum", reader.GetName(0));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(42, reader.GetInt32(0));
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(await reader.ReadAsync());
        Assert.True(reader.HasRows);
        Assert.False(await reader.NextResultAsync());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Checks the transition from an empty regular result to an aggregate row without losing INFO.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public void HasRows_WithAlternateRowOnly_PreservesResultTransition(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: false, useAlternateMetadata: true);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        connection.Open();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = command.ExecuteReader();

        // Assert alternate metadata does not make the empty regular result appear populated.
        Assert.False(reader.HasRows);
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.False(reader.Read());
        Assert.False(reader.HasRows);
        Assert.False(reader.Read());
        Assert.False(reader.HasRows);
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }

        // Act: advance past the empty regular result.
        Assert.True(reader.NextResult());

        // Assert the aggregate row and messages survive the result transition.
        Assert.True(reader.HasRows);
        Assert.Equal("sum", reader.GetName(0));
        Assert.True(reader.Read());
        Assert.Equal(42, reader.GetInt32(0));
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(reader.Read());
        Assert.True(reader.HasRows);
        Assert.False(reader.NextResult());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Async counterpart: checks the transition from an empty regular result to an aggregate row without losing INFO.</summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public async Task HasRows_WithAlternateRowOnly_PreservesResultTransitionAsync(int infoCount, InfoPlacement placement)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, placement, returnsRow: false, useAlternateMetadata: true);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        await connection.OpenAsync();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        // Assert alternate metadata does not make the empty regular result appear populated.
        Assert.False(reader.HasRows);
        Assert.Equal(placement == InfoPlacement.BeforeMetadata ? infoCount : 0, infoMessages.Count);
        Assert.False(await reader.ReadAsync());
        Assert.False(reader.HasRows);
        Assert.False(await reader.ReadAsync());
        Assert.False(reader.HasRows);
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }

        // Act: advance past the empty regular result.
        Assert.True(await reader.NextResultAsync());

        // Assert the aggregate row and messages survive the result transition.
        Assert.True(reader.HasRows);
        Assert.Equal("sum", reader.GetName(0));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(42, reader.GetInt32(0));
        if (placement == InfoPlacement.AfterRows)
        {
            Assert.Empty(infoMessages);
        }
        Assert.False(await reader.ReadAsync());
        Assert.True(reader.HasRows);
        Assert.False(await reader.NextResultAsync());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Checks metadata-free INFO responses report no rows or columns and deliver every message.</summary>
    [Theory]
    [MemberData(nameof(InfoOnlyCases))]
    public void HasRows_WithInfoOnly_ReturnsFalse(int infoCount)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, InfoPlacement.InfoOnly, returnsRow: false, useAlternateMetadata: false);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        connection.Open();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = command.ExecuteReader();

        // Assert
        Assert.False(reader.HasRows);
        Assert.Equal(0, reader.FieldCount);
        Assert.Equal(infoCount, infoMessages.Count);
        Assert.False(reader.Read());
        Assert.False(reader.HasRows);
        Assert.False(reader.Read());
        Assert.False(reader.HasRows);
        Assert.False(reader.NextResult());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>Async counterpart: checks metadata-free INFO responses report no rows or columns and deliver every message.</summary>
    [Theory]
    [MemberData(nameof(InfoOnlyCases))]
    public async Task HasRows_WithInfoOnly_ReturnsFalseAsync(int infoCount)
    {
        // Arrange: using declarations dispose the reader, command, connection, then server.
        using TdsServer server = CreateServer(infoCount, InfoPlacement.InfoOnly, returnsRow: false, useAlternateMetadata: false);
        server.Start();
        List<string> infoMessages = new();
        using SqlConnection connection = CreateConnection(server, infoMessages);
        await connection.OpenAsync();
        infoMessages.Clear();
        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };

        // Act
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        // Assert
        Assert.False(reader.HasRows);
        Assert.Equal(0, reader.FieldCount);
        Assert.Equal(infoCount, infoMessages.Count);
        Assert.False(await reader.ReadAsync());
        Assert.False(reader.HasRows);
        Assert.False(await reader.ReadAsync());
        Assert.False(reader.HasRows);
        Assert.False(await reader.NextResultAsync());
        AssertInfoMessages(infoCount, infoMessages);
    }

    /// <summary>
    /// Buffered INFO tokens retain HasRows=false for closed or broken parsers,
    /// without introducing an exception or looping on an unconsumed token.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Metadata_WithBufferedInfoAndUnavailableParser_ReturnsNoRows(bool broken, bool useAlternateMetadata)
    {
        // Arrange
        using SqlCommand command = new();
        using SqlDataReader reader = new(command, CommandBehavior.Default);
        TdsParser parser = new(false, false)
        {
            State = broken ? TdsParserState.Broken : TdsParserState.Closed
        };
        TdsParserStateObject state = parser._physicalStateObj;
        reader.Bind(state);
        state.SniContext = SniContext.Snix_Read;
        state.SetBuffer(new[] { TdsEnums.SQLINFO }, 0, 1);
        state._inBytesPacket = 1;
        _SqlMetaDataSet metadata = new(1, null);

        // Act
        TdsOperationStatus result = useAlternateMetadata
            ? reader.TrySetAltMetaDataSet(metadata, true)
            : reader.TrySetMetaData(metadata, false);

        // Assert
        Assert.Equal(TdsOperationStatus.Done, result);
        Assert.False(reader.HasRows);
        Assert.Equal(0, state._inBytesUsed);
        Assert.Equal(1, state._inBytesPacket);
        Assert.False(state._accumulateInfoEvents);
        Assert.Null(state._pendingInfoEvents);
    }

    /// <summary>Creates an unstarted server with the requested wire response; the caller owns its lifetime.</summary>
    /// <param name="infoCount">Number of messages in the response.</param>
    /// <param name="placement">Location of INFO tokens relative to metadata and rows.</param>
    /// <param name="returnsRow">Whether the regular result has a row.</param>
    /// <param name="useAlternateMetadata">Whether an aggregate result follows the regular result.</param>
    /// <returns>The server to start and dispose within the test.</returns>
    private static TdsServer CreateServer(int infoCount, InfoPlacement placement, bool returnsRow, bool useAlternateMetadata)
    {
        TdsServerArguments arguments = new();
        InfoQueryEngine engine = new(arguments, infoCount, placement, returnsRow, useAlternateMetadata);
        return new TdsServer(engine, arguments);
    }

    /// <summary>Creates an unopened connection and captures INFO messages in delivery order.</summary>
    /// <param name="server">The running server for this test.</param>
    /// <param name="infoMessages">The destination for captured messages.</param>
    /// <returns>The connection to open and dispose within the test.</returns>
    private static SqlConnection CreateConnection(TdsServer server, List<string> infoMessages)
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectTimeout = 5
        };
        SqlConnection connection = new(builder.ConnectionString);
        connection.InfoMessage += (_, args) =>
        {
            foreach (SqlError error in args.Errors)
            {
                infoMessages.Add(error.Message);
            }
        };
        return connection;
    }

    /// <summary>Verifies every INFO message was delivered once and in wire order.</summary>
    /// <param name="infoCount">Expected number of messages.</param>
    /// <param name="infoMessages">Messages captured during the query.</param>
    private static void AssertInfoMessages(int infoCount, List<string> infoMessages)
    {
        Assert.Equal(infoCount, infoMessages.Count);
        for (int i = 0; i < infoCount; i++)
        {
            Assert.Equal($"Info message {i}", infoMessages[i]);
        }
    }

    /// <summary>
    /// Inserts INFO tokens into the existing SELECT 1 response and optionally
    /// removes its row or adds an alternate result set.
    /// </summary>
    private sealed class InfoQueryEngine : QueryEngine
    {
        internal const string CommandText = "SELECT 1";
        private readonly int _infoCount;
        private readonly InfoPlacement _placement;
        private readonly bool _returnsRow;
        private readonly bool _useAlternateMetadata;

        internal InfoQueryEngine(TdsServerArguments arguments, int infoCount, InfoPlacement placement, bool returnsRow, bool useAlternateMetadata)
            : base(arguments)
        {
            _infoCount = infoCount;
            _placement = placement;
            _returnsRow = returnsRow;
            _useAlternateMetadata = useAlternateMetadata;
        }

        /// <summary>
        /// Places messages around metadata and rows, or omits metadata, keeping ALTROW
        /// separate from the regular result set.
        /// </summary>
        /// <param name="session">The connected test session.</param>
        /// <param name="batchRequest">The batch to execute.</param>
        /// <returns>A response with the requested INFO tokens and row presence.</returns>
        protected override TDSMessageCollection CreateQueryResponse(
            ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            TDSMessageCollection response = base.CreateQueryResponse(session, batchRequest);
            if (batchRequest.Text == CommandText)
            {
                if (!_returnsRow)
                {
                    response[0].RemoveAll(token => token is TDSRowToken);
                    foreach (TDSPacketToken token in response[0])
                    {
                        if (token is TDSDoneToken done)
                        {
                            done.RowCount = 0;
                        }
                    }
                }

                if (_placement == InfoPlacement.InfoOnly)
                {
                    response[0].RemoveAll(token => token is TDSColMetadataToken);
                }

                if (_useAlternateMetadata)
                {
                    TDSColMetadataToken metadata = Assert.IsType<TDSColMetadataToken>(response[0][0]);
                    response[0].Insert(1, new AltMetadataToken(metadata.Columns[0]));
                    response[0].Insert(response[0].Count - 1, new AltRowToken(metadata));
                }

                int insertAt;
                switch (_placement)
                {
                    case InfoPlacement.BeforeMetadata:
                    case InfoPlacement.InfoOnly:
                        insertAt = 0;
                        break;
                    case InfoPlacement.AfterMetadata:
                        insertAt = _useAlternateMetadata ? 2 : 1;
                        break;
                    case InfoPlacement.AfterOrder:
                        insertAt = _useAlternateMetadata ? 2 : 1;
                        response[0].Insert(insertAt++, new TDSOrderToken(1));
                        break;
                    case InfoPlacement.AfterRows:
                        insertAt = response[0].Count - 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_placement));
                }
                for (int i = 0; i < _infoCount; i++)
                {
                    response[0].Insert(insertAt + i, new TDSInfoToken(30000, 0, 0, $"Info message {i}"));
                }
            }

            return response;
        }
    }

    /// <summary>
    /// Writes a little-endian token field without flushing the TDS response.
    /// Disposing a BinaryWriter on the response stream would end the message early.
    /// </summary>
    /// <param name="destination">The server response stream.</param>
    /// <param name="value">The unsigned 16-bit field value.</param>
    private static void WriteUInt16(Stream destination, ushort value)
    {
        destination.WriteByte((byte)value);
        destination.WriteByte((byte)(value >> 8));
    }

    /// <summary>
    /// Writes the deprecated ALTMETADATA format for one aggregate column.
    /// The test server has no built-in serializer for this token.
    /// </summary>
    private sealed class AltMetadataToken : TDSPacketToken
    {
        private readonly TDSColumnData _column;

        internal AltMetadataToken(TDSColumnData column) => _column = column;

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        /// <summary>Serializes the aggregate header followed by standard column metadata.</summary>
        /// <param name="destination">The server response stream.</param>
        public override void Deflate(Stream destination)
        {
            destination.WriteByte(TdsEnums.SQLALTMETADATA);
            WriteUInt16(destination, 1); // Column count, not byte length.
            WriteUInt16(destination, AltMetadataId);
            destination.WriteByte(0); // No COMPUTE BY columns.
            destination.WriteByte(TdsEnums.AOPSUM);
            WriteUInt16(destination, 1); // Source column ordinal.
            _column.Deflate(destination);
        }
    }

    /// <summary>
    /// Writes an ALTROW tied to the aggregate metadata, reusing standard value serialization.
    /// </summary>
    private sealed class AltRowToken : TDSRowToken
    {
        internal AltRowToken(TDSColMetadataToken metadata) : base(metadata) => Data.Add(42);

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        /// <summary>Serializes an alternate row without consuming the regular result's row.</summary>
        /// <param name="destination">The server response stream.</param>
        public override void Deflate(Stream destination)
        {
            destination.WriteByte((byte)TDSTokenType.AlternativeRow);
            WriteUInt16(destination, AltMetadataId);
            DeflateColumn(destination, Metadata.Columns[0], Data[0]);
        }
    }
}
