// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Reads <c>nvarchar(max)</c> values whose PLP chunks and length fields fall at different places relative
/// to TDS packet boundaries, with packet multiplexing on.
/// <para>
/// An async read that pends inside a PLP value keeps the packets it has read in a snapshot. When more data
/// arrives it either replays the snapshot from the start of the row, or continues from a continue point:
/// the parser state captured at a packet boundary where no field is half-read. While continuing it skips
/// the value bytes it already copied, using a running count of value bytes per snapshot packet. Each test
/// sends a layout that breaks one part of that bookkeeping.
/// </para>
/// <para>
/// The server uses 512-byte packets and sends one row of two <c>nvarchar(max)</c> columns, a padding
/// column and the value. It sends the packets of the value one at a time with a pause between them, so the
/// read pends at every packet boundary inside the value. None of the value's packets has header packet
/// ID 0, which has a separate defect.
/// </para>
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class PlpContinueTests
{
    private const string CommandText = "SELECT pad, value";
    private const int PacketSize = 512;
    private const int PacketPayloadSize = PacketSize - 8;
    private const int ChunkLengthSize = 4;

    /// <summary>
    /// The 1-based index of the packet the value's data starts in.
    /// </summary>
    private const int ValuePacket = 10;

    /// <summary>
    /// Length of the value in bytes: it spans several packets.
    /// </summary>
    private const int ValueSize = 3000;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The layouts of the three async tests, for the sync guard.
    /// </summary>
    public static TheoryData<int, int> Layouts => new()
    {
        { 8, ValueSize },
        { 408, 600 },
        { 16, 1000 },
    };

    /// <summary>
    /// Ensures an async read returns the value when the value's 8-byte PLP length spans two packets.
    /// The read used to capture a continue point at that boundary with half of the length read, and then
    /// read the length from the wrong bytes.
    /// </summary>
    [Fact]
    public async Task ReadAsync_PlpLengthSpansPackets_ReturnsValue()
    {
        // Arrange
        // The value's data starts 8 bytes into its packet, so its 8-byte total length, followed by the
        // 4-byte chunk length, starts 4 bytes before the end of the previous packet. One chunk.
        PlpLayout layout = new(valueOffset: 8, chunkSize: ValueSize);

        // Act
        string value = await ReadAsync(layout);

        // Assert
        Assert.Equal(layout.Text, value);
    }

    /// <summary>
    /// Ensures an async read returns the value when it continues several packets after its continue point.
    /// The read used to resume at the newest packet, skipping a chunk length and part of a chunk that no
    /// count covers.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ContinueAfterSeveralPackets_ReturnsValue()
    {
        // Arrange
        // The first chunk ends exactly at the end of the value's second packet, which becomes the continue
        // point. No later packet boundary falls between chunks, so every later resume comes after packets
        // that hold chunk lengths and partly read chunks.
        PlpLayout layout = new(valueOffset: 408, chunkSize: 600);

        // Act
        string value = await ReadAsync(layout);

        // Assert
        Assert.Equal(layout.Text, value);
    }

    /// <summary>
    /// Ensures an async read returns the value when a chunk completes two packets after the previous one.
    /// The running count of the packet in between stayed 0, so the count of the next packet dropped the
    /// bytes before it and the continue resumed too early in the value.
    /// </summary>
    [Fact]
    public async Task ReadAsync_PacketWithoutCompletedChunk_ReturnsValue()
    {
        // Arrange
        // 1000-byte chunks: the first completes in the value's third packet, the second in its fifth, and
        // no chunk completes in the fourth.
        PlpLayout layout = new(valueOffset: 16, chunkSize: 1000);

        // Act
        string value = await ReadAsync(layout);

        // Assert
        Assert.Equal(layout.Text, value);
    }

    /// <summary>
    /// Ensures a sync read of each layout returns the value. The sync path never pends inside a value, so
    /// it guards the test's response layouts rather than the continue logic.
    /// </summary>
    [Theory]
    [MemberData(nameof(Layouts))]
    public void Read_Layout_ReturnsValue(int valueOffset, int chunkSize)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseCompatibilityProcessSni = false,
            UseCompatibilityAsyncBehaviour = false,
        };
        PlpLayout layout = new(valueOffset, chunkSize);
        using TdsServer server = CreateServer(layout);
        server.Start();
        using SqlConnection connection = CreateConnection(server);
        connection.Open();
        using SqlCommand command = new(CommandText, connection);

        // Act
        using SqlDataReader reader = command.ExecuteReader();
        bool hasRow = reader.Read();

        // Assert
        Assert.True(hasRow);
        Assert.Equal(layout.Text, reader.GetString(1));
    }

    /// <summary>
    /// Reads the row of <paramref name="layout"/> asynchronously with packet multiplexing on.
    /// </summary>
    /// <param name="layout">The layout the server sends.</param>
    /// <returns>The value of the second column.</returns>
    private static async Task<string> ReadAsync(PlpLayout layout)
    {
        // Packet multiplexing on: the async read continues from snapshots instead of restarting.
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseCompatibilityProcessSni = false,
            UseCompatibilityAsyncBehaviour = false,
        };
        using TdsServer server = CreateServer(layout);
        server.Start();
        using SqlConnection connection = CreateConnection(server);
        await connection.OpenAsync();
        using SqlCommand command = new(CommandText, connection);

        // A wrong offset can also make the read spin forever, so the read is bounded.
        Task<string> read = ReadValueAsync(command);
        Task completed = await Task.WhenAny(read, Task.Delay(ReadBudget));
        Assert.True(completed == read, $"The read did not complete within {ReadBudget.TotalSeconds:N0}s.");
        return await read;
    }

    /// <summary>
    /// Reads the row asynchronously and returns its second column.
    /// </summary>
    /// <param name="command">The command returning the row.</param>
    /// <returns>The value of the second column.</returns>
    private static async Task<string> ReadValueAsync(SqlCommand command)
    {
        using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return reader.GetString(1);
    }

    /// <summary>
    /// Creates a server with 512-byte packets that answers <see cref="CommandText"/> with one row.
    /// </summary>
    /// <param name="layout">The layout of the value.</param>
    /// <returns>The server to start and dispose within the test.</returns>
    private static TdsServer CreateServer(PlpLayout layout)
    {
        TdsServerArguments arguments = new() { PacketSize = PacketSize };
        return new TdsServer(new PacedRowQueryEngine(arguments, layout), arguments);
    }

    /// <summary>
    /// Creates an unopened connection to <paramref name="server"/>.
    /// </summary>
    /// <param name="server">The running server for this test.</param>
    /// <returns>The connection to open and dispose within the test.</returns>
    private static SqlConnection CreateConnection(TdsServer server)
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectTimeout = 5,
            PacketSize = PacketSize,
        };
        return new SqlConnection(builder.ConnectionString);
    }

    /// <summary>
    /// A value of <see cref="ValueSize"/> bytes sent in chunks of one size, starting at an offset of
    /// packet <see cref="ValuePacket"/>.
    /// </summary>
    private sealed class PlpLayout
    {
        /// <summary>
        /// Lays out the value.
        /// </summary>
        /// <param name="valueOffset">Where the value's data starts in packet <see cref="ValuePacket"/>.</param>
        /// <param name="chunkSize">The size in bytes of each chunk; the last one may be shorter.</param>
        public PlpLayout(int valueOffset, int chunkSize)
        {
            ValueOffset = valueOffset;
            List<int> chunks = new();
            for (int offset = 0; offset < ValueSize; offset += chunkSize)
            {
                chunks.Add(Math.Min(chunkSize, ValueSize - offset));
            }
            Chunks = chunks;

            // Characters differ by position, so a shifted or repeated part is detected.
            StringBuilder builder = new(ValueSize / 2);
            for (int i = 0; i < ValueSize / 2; i++)
            {
                builder.Append((char)('A' + (i % 26)));
            }
            Text = builder.ToString();
        }

        /// <summary>
        /// Where the value's data starts in packet <see cref="ValuePacket"/>.
        /// </summary>
        public int ValueOffset { get; }

        /// <summary>
        /// The sizes in bytes of the value's PLP chunks.
        /// </summary>
        public IReadOnlyList<int> Chunks { get; }

        /// <summary>
        /// The value.
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    /// Answers <see cref="CommandText"/> with the paced row and its DONE token.
    /// </summary>
    private sealed class PacedRowQueryEngine : QueryEngine
    {
        private readonly PlpLayout _layout;

        internal PacedRowQueryEngine(TdsServerArguments arguments, PlpLayout layout)
            : base(arguments)
        {
            _layout = layout;
        }

        /// <inheritdoc/>
        protected override TDSMessageCollection CreateQueryResponse(ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            if (batchRequest.Text != CommandText)
            {
                return base.CreateQueryResponse(session, batchRequest);
            }

            TDSDoneToken done = new(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1);
            return new TDSMessageCollection(new TDSMessage(TDSMessageType.Response, new PacedRowToken(_layout), done));
        }
    }

    /// <summary>
    /// Writes the column metadata and the row as raw bytes, then sends the packets that carry the second
    /// column one at a time.
    /// </summary>
    private sealed class PacedRowToken : TDSPacketToken
    {
        /// <summary>
        /// Pause between the packets of the value, so the client reads each one before the next arrives.
        /// </summary>
        private static readonly TimeSpan PacketPause = TimeSpan.FromMilliseconds(50);

        private readonly PlpLayout _layout;

        internal PacedRowToken(PlpLayout layout)
        {
            _layout = layout;
        }

        /// <inheritdoc/>
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Deflate(Stream destination)
        {
            byte[] valueBytes = Encoding.Unicode.GetBytes(_layout.Text);

            // Everything before the value's data: metadata, the row token, the padding column, and the
            // value's total length and first chunk length.
            MemoryStream head = new();
            WriteColumnMetadata(head);
            head.WriteByte(0xD1);
            int valueStart = ((ValuePacket - 1) * PacketPayloadSize) + _layout.ValueOffset;
            int padLength = valueStart - (int)head.Length - (8 + ChunkLengthSize + ChunkLengthSize) - (8 + ChunkLengthSize);
            Assert.True(padLength >= 0 && padLength % 2 == 0, $"Invalid padding length {padLength}.");
            WritePlp(head, new byte[padLength]);
            WriteULong(head, (ulong)valueBytes.Length);
            WriteUInt(head, (uint)_layout.Chunks[0]);
            Assert.Equal(valueStart, head.Length);
            destination.Write(head.GetBuffer(), 0, (int)head.Length);

            // The rest of the value: chunk data, the following chunk lengths, and the terminator.
            MemoryStream body = new();
            int offset = 0;
            for (int i = 0; i < _layout.Chunks.Count; i++)
            {
                if (i > 0)
                {
                    WriteUInt(body, (uint)_layout.Chunks[i]);
                }
                body.Write(valueBytes, offset, _layout.Chunks[i]);
                offset += _layout.Chunks[i];
            }
            WriteUInt(body, 0);

            // One packet at a time. The message stream sends a full packet only when the next byte is
            // written, so each piece fills the current packet and the pause holds back sending it until the
            // next piece.
            byte[] bodyBytes = body.ToArray();
            int written = 0;
            int toPacketEnd = PacketPayloadSize - _layout.ValueOffset;
            while (written < bodyBytes.Length)
            {
                int count = Math.Min(toPacketEnd, bodyBytes.Length - written);
                destination.Write(bodyBytes, written, count);
                written += count;
                toPacketEnd = PacketPayloadSize;
                Thread.Sleep(PacketPause);
            }
        }

        /// <summary>
        /// Writes COLMETADATA for two <c>nvarchar(max)</c> columns named <c>pad</c> and <c>value</c>.
        /// </summary>
        /// <param name="destination">The stream to write to.</param>
        private static void WriteColumnMetadata(Stream destination)
        {
            destination.WriteByte(0x81);
            WriteUShort(destination, 2);
            WriteNVarCharMaxColumn(destination, "pad");
            WriteNVarCharMaxColumn(destination, "value");
        }

        /// <summary>
        /// Writes the metadata of one nullable <c>nvarchar(max)</c> column.
        /// </summary>
        /// <param name="destination">The stream to write to.</param>
        /// <param name="name">The column name.</param>
        private static void WriteNVarCharMaxColumn(Stream destination, string name)
        {
            WriteUInt(destination, 0); // user type
            WriteUShort(destination, 0x0009); // flags: nullable, updatable unknown
            destination.WriteByte(0xE7); // NVARCHAR
            WriteUShort(destination, 0xFFFF); // max
            destination.Write(new byte[] { 0x09, 0x04, 0xD0, 0x00, 0x34 }, 0, 5); // collation
            destination.WriteByte((byte)name.Length);
            byte[] nameBytes = Encoding.Unicode.GetBytes(name);
            destination.Write(nameBytes, 0, nameBytes.Length);
        }

        /// <summary>
        /// Writes a PLP value as one chunk followed by the terminator.
        /// </summary>
        /// <param name="destination">The stream to write to.</param>
        /// <param name="data">The value.</param>
        private static void WritePlp(Stream destination, byte[] data)
        {
            WriteULong(destination, (ulong)data.Length);
            WriteUInt(destination, (uint)data.Length);
            destination.Write(data, 0, data.Length);
            WriteUInt(destination, 0);
        }

        private static void WriteUShort(Stream destination, ushort value)
        {
            destination.WriteByte((byte)value);
            destination.WriteByte((byte)(value >> 8));
        }

        private static void WriteUInt(Stream destination, uint value)
        {
            WriteUShort(destination, (ushort)value);
            WriteUShort(destination, (ushort)(value >> 16));
        }

        private static void WriteULong(Stream destination, ulong value)
        {
            WriteUInt(destination, (uint)value);
            WriteUInt(destination, (uint)(value >> 32));
        }
    }
}
