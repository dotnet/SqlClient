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
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public HasRowsTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Covers the single-token boundary, multiple tokens, and empty results with
    /// both synchronous and asynchronous readers, including alternate metadata.
    /// </summary>
    /// <returns>INFO count, placement, async flag, regular row presence, and alternate metadata flag.</returns>
    public static IEnumerable<object[]> InfoTokenCases()
    {
        foreach (int infoCount in new[] { 0, 1, 2, 3, 10 })
        {
            foreach (InfoPlacement placement in Enum.GetValues(typeof(InfoPlacement)))
            {
                foreach (bool useAsync in new[] { false, true })
                {
                    if (placement == InfoPlacement.InfoOnly)
                    {
                        yield return new object[] { infoCount, placement, useAsync, false, false };
                    }
                    else
                    {
                        foreach (bool useAlternateMetadata in new[] { false, true })
                        {
                            yield return new object[] { infoCount, placement, useAsync, false, useAlternateMetadata };
                            yield return new object[] { infoCount, placement, useAsync, true, useAlternateMetadata };
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Verifies INFO tokens do not affect HasRows or change deferred message delivery.
    /// </summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public async Task HasRows_WithInfoTokens_ReflectsRowPresence(
        int infoCount, InfoPlacement placement, bool useAsync, bool returnsRow, bool useAlternateMetadata)
    {
        TdsServerArguments arguments = new();
        InfoQueryEngine engine = new(arguments, infoCount, placement, returnsRow, useAlternateMetadata);
        using TdsServer server = new(engine, arguments);
        server.Start();

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectTimeout = 5
        };
        using SqlConnection connection = new(builder.ConnectionString);
        List<string> infoMessages = new();
        connection.InfoMessage += (_, args) =>
        {
            foreach (SqlError error in args.Errors)
            {
                infoMessages.Add(error.Message);
            }
        };

        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
        infoMessages.Clear();

        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };
        using SqlDataReader reader = useAsync
            ? await command.ExecuteReaderAsync()
            : command.ExecuteReader();

        bool hasRowsBeforeRead = reader.HasRows;
        bool messagesBeforeMetadata = placement == InfoPlacement.BeforeMetadata || placement == InfoPlacement.InfoOnly;
        Assert.Equal(messagesBeforeMetadata ? infoCount : 0, infoMessages.Count);
        if (placement == InfoPlacement.InfoOnly)
        {
            Assert.Equal(0, reader.FieldCount);
        }
        bool read = useAsync ? await reader.ReadAsync() : reader.Read();
        Assert.Equal(returnsRow, read);
        if (read)
        {
            Assert.Equal(1, reader.GetInt32(0));
            if (placement == InfoPlacement.AfterRows)
            {
                Assert.Empty(infoMessages);
            }
        }
        bool hasRowsAfterRead = reader.HasRows;
        Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
        if (placement == InfoPlacement.AfterRows && useAlternateMetadata)
        {
            Assert.Empty(infoMessages);
        }
        else if (!useAlternateMetadata || returnsRow)
        {
            Assert.Equal(infoCount, infoMessages.Count);
        }

        _output.WriteLine(
            $"INFO count={infoCount}, placement={placement}, async={useAsync}, returns row={returnsRow}, alternate metadata={useAlternateMetadata}: " +
            $"HasRows before Read={hasRowsBeforeRead}, Read={read}, " +
            $"HasRows after Read={hasRowsAfterRead}, INFO received={infoMessages.Count}");

        Assert.Equal(returnsRow, hasRowsBeforeRead);
        Assert.Equal(returnsRow, hasRowsAfterRead);
        Assert.Equal(returnsRow, reader.HasRows);
        if (useAlternateMetadata)
        {
            Assert.True(useAsync ? await reader.NextResultAsync() : reader.NextResult());
            Assert.True(reader.HasRows);
            Assert.Equal("sum", reader.GetName(0));
            Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.Equal(42, reader.GetInt32(0));
            if (placement == InfoPlacement.AfterRows)
            {
                Assert.Empty(infoMessages);
            }
            Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.True(reader.HasRows);
        }
        Assert.False(useAsync ? await reader.NextResultAsync() : reader.NextResult());
        Assert.Equal(infoCount, infoMessages.Count);
        for (int i = 0; i < infoCount; i++)
        {
            Assert.Equal($"Info message {i}", infoMessages[i]);
        }
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

        TdsOperationStatus result = useAlternateMetadata
            ? reader.TrySetAltMetaDataSet(metadata, true)
            : reader.TrySetMetaData(metadata, false);

        Assert.Equal(TdsOperationStatus.Done, result);
        Assert.False(reader.HasRows);
        Assert.Equal(0, state._inBytesUsed);
        Assert.Equal(1, state._inBytesPacket);
        Assert.False(state._accumulateInfoEvents);
        Assert.Null(state._pendingInfoEvents);
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
