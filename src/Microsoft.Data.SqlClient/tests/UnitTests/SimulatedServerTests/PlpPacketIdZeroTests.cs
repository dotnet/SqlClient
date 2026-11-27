// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
/// Reads an <c>nvarchar(max)</c> value whose first PLP chunk ends in a TDS packet with header packet ID 0
/// (https://github.com/dotnet/SqlClient/issues/4925).
/// <para>
/// The packet ID is one byte, so every 256th packet of a large response has ID 0. With packet
/// multiplexing on, an async read that pends inside a PLP value records, per snapshot packet, how many
/// bytes of the value it copied, and a later continue resumes at the offset those counts add up to. When
/// a replay restarts the value from its beginning, the count of a packet seen in an earlier replay must be
/// reset rather than added to. The reader decides whether a packet is new by comparing its ID with the
/// last one it saw, and that "last seen" ID started at 0, so a packet with ID 0 kept its count from the
/// earlier replay: the continue then resumed at too large an offset, and the value came back corrupted.
/// </para>
/// <para>
/// The server uses 512-byte packets and sends one row of two <c>nvarchar(max)</c> columns, laid out so the
/// read takes this path, packet by packet with a pause between packets:
/// <list type="bullet">
///   <item>The value starts in packet 255. Its first chunk ends inside packet 256, which has ID 0.</item>
///   <item>Its second chunk ends exactly at the end of packet 257, the first point where the read can
///   capture a continue point, so the value is replayed from its beginning twice before that and the
///   first chunk is counted on packet 256 in both replays.</item>
///   <item>Every later chunk fills exactly one packet, and no length field spans two packets.</item>
/// </list>
/// </para>
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class PlpPacketIdZeroTests
{
    private const string CommandText = "SELECT pad, value";
    private const int PacketSize = 512;
    private const int PacketPayloadSize = PacketSize - 8;
    private const int ChunkLengthSize = 4;

    /// <summary>
    /// The 1-based index of the first packet with header ID 0: packet IDs of a message start at 1.
    /// </summary>
    private const int PacketWithIdZero = 256;

    /// <summary>
    /// Where the value's first chunk ends in the packet with ID 0.
    /// </summary>
    private const int FirstChunkEnd = 200;

    /// <summary>
    /// Number of one-packet chunks after the second chunk.
    /// </summary>
    private const int PacketChunks = 3;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Where the value's data starts in the packet before the one with ID 0. The value's length fields
    /// before it stay in that packet.
    /// </summary>
    public static TheoryData<int> ValueOffsets => new() { 16, 200, 480 };

    /// <summary>
    /// Ensures an async read returns the stored value when its first chunk ends in a packet with ID 0
    /// and the read replays and continues the value.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValueOffsets))]
    public async Task ReadAsync_FirstChunkEndsInPacketWithIdZero_ReturnsValue(int valueOffset)
    {
        // Arrange
        // Packet multiplexing on: the async read continues from snapshots instead of restarting.
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseCompatibilityProcessSni = false,
            UseCompatibilityAsyncBehaviour = false,
        };
        PacedValue value = new(valueOffset);
        using TdsServer server = CreateServer(value);
        server.Start();
        using SqlConnection connection = CreateConnection(server);
        await connection.OpenAsync();
        using SqlCommand command = new(CommandText, connection);

        // Act
        // A wrong offset can also make the read spin forever, so the read is bounded.
        Task<string> read = ReadValueAsync(command);
        Task completed = await Task.WhenAny(read, Task.Delay(ReadBudget));

        // Assert
        Assert.True(completed == read, $"The read did not complete within {ReadBudget.TotalSeconds:N0}s.");
        Assert.Equal(value.Text, await read);
    }

    /// <summary>
    /// Ensures a sync read of the same response returns the stored value. The sync path never replays a
    /// partially read value, so it guards the test's response layout rather than the defect.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValueOffsets))]
    public void Read_FirstChunkEndsInPacketWithIdZero_ReturnsValue(int valueOffset)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseCompatibilityProcessSni = false,
            UseCompatibilityAsyncBehaviour = false,
        };
        PacedValue value = new(valueOffset);
        using TdsServer server = CreateServer(value);
        server.Start();
        using SqlConnection connection = CreateConnection(server);
        connection.Open();
        using SqlCommand command = new(CommandText, connection);

        // Act
        using SqlDataReader reader = command.ExecuteReader();
        bool hasRow = reader.Read();

        // Assert
        Assert.True(hasRow);
        Assert.Equal(value.Text, reader.GetString(1));
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
    /// <param name="value">The value of the second column and its layout.</param>
    /// <returns>The server to start and dispose within the test.</returns>
    private static TdsServer CreateServer(PacedValue value)
    {
        TdsServerArguments arguments = new() { PacketSize = PacketSize };
        return new TdsServer(new PacedRowQueryEngine(arguments, value), arguments);
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
    /// The value of the second column and its PLP chunk sizes, laid out as described on the test class.
    /// </summary>
    private sealed class PacedValue
    {
        /// <summary>
        /// Lays out a value whose data starts at <paramref name="valueOffset"/> in packet 255.
        /// </summary>
        /// <param name="valueOffset">Where the value's data starts in packet 255.</param>
        public PacedValue(int valueOffset)
        {
            ValueOffset = valueOffset;
            int[] chunks = new int[2 + PacketChunks];
            chunks[0] = PacketPayloadSize - valueOffset + FirstChunkEnd;
            chunks[1] = (2 * PacketPayloadSize) - FirstChunkEnd - ChunkLengthSize;
            for (int i = 2; i < chunks.Length; i++)
            {
                chunks[i] = PacketPayloadSize - ChunkLengthSize;
            }
            Chunks = chunks;

            int length = 0;
            foreach (int chunk in chunks)
            {
                length += chunk;
            }

            // Characters differ by position, so a shifted or repeated part is detected.
            StringBuilder builder = new(length / 2);
            for (int i = 0; i < length / 2; i++)
            {
                builder.Append((char)('A' + (i % 26)));
            }
            Text = builder.ToString();
        }

        /// <summary>
        /// Where the value's data starts in packet 255.
        /// </summary>
        public int ValueOffset { get; }

        /// <summary>
        /// The sizes in bytes of the value's PLP chunks.
        /// </summary>
        public int[] Chunks { get; }

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
        private readonly PacedValue _value;

        internal PacedRowQueryEngine(TdsServerArguments arguments, PacedValue value)
            : base(arguments)
        {
            _value = value;
        }

        /// <inheritdoc/>
        protected override TDSMessageCollection CreateQueryResponse(ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            if (batchRequest.Text != CommandText)
            {
                return base.CreateQueryResponse(session, batchRequest);
            }

            TDSDoneToken done = new(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1);
            return new TDSMessageCollection(new TDSMessage(TDSMessageType.Response, new PacedRowToken(_value), done));
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

        private readonly PacedValue _value;

        internal PacedRowToken(PacedValue value)
        {
            _value = value;
        }

        /// <inheritdoc/>
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Deflate(Stream destination)
        {
            byte[] valueBytes = Encoding.Unicode.GetBytes(_value.Text);

            // Everything before the value's data: metadata, the row token, the padding column, and the
            // value's total length and first chunk length.
            MemoryStream head = new();
            WriteColumnMetadata(head);
            head.WriteByte(0xD1);
            int valueStart = ((PacketWithIdZero - 2) * PacketPayloadSize) + _value.ValueOffset;
            int padLength = valueStart - (int)head.Length - (8 + ChunkLengthSize + ChunkLengthSize) - (8 + ChunkLengthSize);
            Assert.True(padLength >= 0 && padLength % 2 == 0, $"Invalid padding length {padLength}.");
            WritePlp(head, new byte[padLength]);
            WriteULong(head, (ulong)valueBytes.Length);
            WriteUInt(head, (uint)_value.Chunks[0]);
            Assert.Equal(valueStart, head.Length);
            destination.Write(head.GetBuffer(), 0, (int)head.Length);

            // The rest of the value: chunk data, the following chunk lengths, and the terminator.
            MemoryStream body = new();
            int offset = 0;
            for (int i = 0; i < _value.Chunks.Length; i++)
            {
                if (i > 0)
                {
                    WriteUInt(body, (uint)_value.Chunks[i]);
                }
                body.Write(valueBytes, offset, _value.Chunks[i]);
                offset += _value.Chunks[i];
            }
            WriteUInt(body, 0);

            // One packet at a time. The message stream sends a full packet only when the next byte is
            // written, so each piece fills the current packet and the pause holds back sending it until the
            // next piece.
            byte[] bodyBytes = body.ToArray();
            int written = 0;
            int toPacketEnd = PacketPayloadSize - _value.ValueOffset;
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
