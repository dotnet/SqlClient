using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class MultiplexerTests
    {
        public static IEnumerable<object[]> IsAsync() { yield return new object[] { false }; yield return new object[] { true }; }

        [Theory, MemberData(nameof(IsAsync))]
        public static void PassThroughSinglePacket(bool isAsync)
        {
            int dataSize = 20;
            var a = CreatePacket(dataSize, 0xF);
            List<PacketData> input = new List<PacketData> { a };
            List<PacketData> expected = new List<PacketData> { a };

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void PassThroughMultiplePacket(bool isAsync)
        {
            int dataSize = 40;
            List<PacketData> input = CreatePackets(dataSize, 5, 6, 7, 8);
            List<PacketData> expected = input;

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void PassThroughMultiplePacketWithShortEnd(bool isAsync)
        {
            int dataSize = 40;
            List<PacketData> input = CreatePackets((dataSize, 20), 5, 6, 7, 8);
            List<PacketData> expected = input;

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void ReconstructSinglePacket(bool isAsync)
        {
            int dataSize = 4;
            var a = CreatePacket(dataSize, 0xF);
            List<PacketData> input = SplitPacket(a, 1);
            List<PacketData> expected = new List<PacketData> { a };

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void Reconstruct2Packets_Part_PartFull(bool isAsync)
        {
            int dataSize = 4;
            var expected = CreatePackets(dataSize, 0xAA, 0xBB);

            var input = SplitPackets(dataSize, expected,
                6, // partial first packet
                (6 + 6), // end of packet 0, start of packet 1
                6 // end of packet 1
            );

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void Reconstruct2Packets_Full_FullPart_Part(bool isAsync)
        {
            int dataSize = 30;
            var expected = new List<PacketData>
            {
                CreatePacket(30, 5),
                CreatePacket(10, 6),
                CreatePacket(30, 7)
            };

            var input = SplitPackets(38, expected,
                (8 + 30), // full
                (8 + 10) + (8 + 12), // full, part next 
                18 // part end
            );

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void ReconstructMultiplePacketSequence(bool isAsync)
        {
            int dataSize = 40;
            List<PacketData> expected = CreatePackets(dataSize, 5, 6, 7, 8);
            List<PacketData> input = SplitPackets(dataSize, expected,
                (8 + 40),
                (8 + 23),
                (17) + (8 + 23),
                (17) + (8 + 23),
                (17)
            );

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void ReconstructMultiplePacketSequenceWithShortEnd(bool isAsync)
        {
            int dataSize = 40;
            List<PacketData> expected = CreatePackets((dataSize, 20), 5, 6, 7, 8);
            List<PacketData> input = SplitPackets(dataSize, expected,
                (8 + 40),
                (8 + 23),
                (17) + (8 + 23),
                (17) + (8 + 20)
            );

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Theory, MemberData(nameof(IsAsync))]
        public static void Reconstruct3Packets_PartPartPart(bool isAsync)
        {
            int dataSize = 62;

            var expected = new List<PacketData>
            {
                CreatePacket(26, 5),
                CreatePacket(10, 6),
                CreatePacket(10, 7)
            };

            var input = SplitPackets(70, expected,
                (8 + 26) + (8 + 10) + (8 + 10) // = 70: full, full, part
            );

            var output = MultiplexPacketList(isAsync, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Fact]
        public static void TrailingPartialPacketInSnapshotNotDuplicated()
        {
            int dataSize = 120;

            var expected = new List<PacketData>
            {
                CreatePacket(120, 5),
                CreatePacket(90, 6),
                CreatePacket(13, 7),
            };

            var input = SplitPackets(120, expected,
                (8 + 120),
                (8 + 90) + (8 + 13)
            );

            Assert.Equal(SumPacketLengths(expected), SumPacketLengths(input));

            var output = MultiplexPacketList(true, dataSize, input);

            ComparePacketLists(dataSize, expected, output);
        }

        [Fact]
        public static void BetweenAsyncAttentionPacket()
        {
            int dataSize = 120;
            var normalPacket = CreatePacket(120, 5);
            var attentionPacket = CreatePacket(13, 6);
            var input = new List<PacketData> { normalPacket, attentionPacket };

            var stateObject = new TdsParserStateObject(input, TdsEnums.HEADER_LEN + dataSize, true);

            for (int index = 0; index < input.Count; index++)
            {
                stateObject.Current = input[index];
                stateObject.ProcessSniPacket(default, 0, usePartialPacket: false);
            }

            // attention packet should be in the current buffer because the snapshot is not active
            Assert.NotNull(stateObject._inBuff);
            Assert.Equal(21, stateObject._inBytesRead);
            Assert.Equal(0, stateObject._inBytesUsed);

            // attention packet should be in the snapshot as well
            Assert.NotNull(stateObject._snapshot);
            Assert.NotNull(stateObject._snapshot.List);
            Assert.Equal(2, stateObject._snapshot.List.Count); 
        }


        private static List<PacketData> MultiplexPacketList(bool isAsync, int dataSize, List<PacketData> input)
        {
            var stateObject = new TdsParserStateObject(input, TdsEnums.HEADER_LEN + dataSize, isAsync);
            var output = new List<PacketData>();

            for (int index = 0; index < input.Count; index++)
            {
                stateObject.Current = input[index];

                stateObject.ProcessSniPacket(default, 0, usePartialPacket: false);

                if (stateObject._inBytesRead > 0)
                {
                    if (
                        stateObject._inBytesRead < TdsEnums.HEADER_LEN
                        ||
                        stateObject._inBytesRead != (TdsEnums.HEADER_LEN + Packet.GetDataLengthFromHeader(stateObject._inBuff.AsSpan(0, TdsEnums.HEADER_LEN)))
                    )
                    {
                        Assert.Fail("incomplete packet exposed after call to ProcessSniPacket");
                    }
                    if (!isAsync)
                    {
                        output.Add(PacketData.Copy(stateObject._inBuff, stateObject._inBytesUsed, stateObject._inBytesRead));
                    }
                }
            }

            if (!isAsync)
            {
                while (stateObject._partialPacket != null)
                {
                    stateObject.Current = default;

                    stateObject.ProcessSniPacket(default, 0, usePartialPacket: true);

                    if (stateObject._inBytesRead > 0)
                    {
                        if (
                            stateObject._inBytesRead < TdsEnums.HEADER_LEN
                            ||
                            stateObject._inBytesRead != (TdsEnums.HEADER_LEN + Packet.GetDataLengthFromHeader(stateObject._inBuff.AsSpan(0, TdsEnums.HEADER_LEN)))
                        )
                        {
                            Assert.Fail("incomplete packet exposed after call to ProcessSniPacket with usePartialPacket");
                        }
                        if (!isAsync)
                        {
                            output.Add(PacketData.Copy(stateObject._inBuff, stateObject._inBytesUsed, stateObject._inBytesRead));
                        }
                    }
                }

            }
            else
            {
                output = stateObject._snapshot.List;
            }

            return output;
        }

        private static void ComparePacketLists(int dataSize, List<PacketData> expected, List<PacketData> output)
        {
            Assert.NotNull(expected);
            Assert.NotNull(output);
            Assert.Equal(expected.Count, output.Count);

            for (int index = 0; index < expected.Count; index++)
            {
                var a = expected[index];
                var b = output[index];

                var compare = a.AsSpan().SequenceCompareTo(b.AsSpan());

                if (compare != 0)
                {
                    Assert.Fail($"expected data does not match output data at packet index {index}");
                }
            }
        }

        public static PacketData CreatePacket(int dataSize, byte dataValue, int startOffset = 0, int endPadding = 0)
        {
            byte[] buffer = new byte[startOffset + TdsEnums.HEADER_LEN + dataSize + endPadding];
            Span<byte> packet = buffer.AsSpan(startOffset, TdsEnums.HEADER_LEN + dataSize);
            WritePacket(packet, dataSize, dataValue, 1);
            return new PacketData(buffer, startOffset, buffer.Length - endPadding);
        }

        public static List<PacketData> CreatePackets(DataSize sizes, params byte[] dataValues)
        {
            int count = dataValues.Length;
            List<PacketData> list = new List<PacketData>(count);

            for (byte index = 0; index < count; index++)
            {
                int dataSize = sizes.GetSize(index == dataValues.Length - 1);
                int packetSize = TdsEnums.HEADER_LEN + dataSize;
                byte[] array = new byte[packetSize];
                WritePacket(array, dataSize, dataValues[index], index);
                list.Add(new PacketData(array, 0, packetSize));
            }

            return list;
        }

        private static void WritePacket(Span<byte> buffer, int dataSize, byte dataValue, byte id)
        {
            Span<byte> header = buffer.Slice(0, TdsEnums.HEADER_LEN);
            header[0] = 4; // Type, 4 - Raw Data
            header[1] = 0; // Status, 0 - normal message
            BinaryPrimitives.TryWriteInt16BigEndian(header.Slice(TdsEnums.HEADER_LEN_FIELD_OFFSET, 2), (short)(TdsEnums.HEADER_LEN + dataSize)); // total length
            BinaryPrimitives.TryWriteInt16BigEndian(header.Slice(TdsEnums.SPID_OFFSET, 2), short.MaxValue); // SPID 
            header[TdsEnums.HEADER_LEN_FIELD_OFFSET + 4] = id; // PacketID
            header[TdsEnums.HEADER_LEN_FIELD_OFFSET + 5] = 0; // Window

            Span<byte> data = buffer.Slice(TdsEnums.HEADER_LEN, dataSize);
            data.Fill(dataValue);
        }

        public static List<PacketData> SplitPacket(PacketData packet, int length)
        {
            List<PacketData> list = new List<PacketData>(2);
            while (packet.Length > length)
            {
                list.Add(new PacketData(packet.Array, packet.Start, length));
                packet = new PacketData(packet.Array, packet.Start + length, packet.Length - length);
            }
            if (packet.Length > 0)
            {
                list.Add(packet);
            }
            return list;
        }

        public static List<PacketData> SplitPackets(int dataSize, List<PacketData> packets, params int[] lengths)
        {
            List<PacketData> list = new List<PacketData>(lengths.Length);
            int packetSize = TdsEnums.HEADER_LEN + dataSize;
            byte[][] arrays = new byte[lengths.Length][];
            for (int index = 0; index < lengths.Length; index++)
            {
                if (lengths[index] > packetSize)
                {
                    throw new ArgumentOutOfRangeException($"segment size of an individual part cannot exceed the packet buffer size of the state object, max packet size: {packetSize}, supplied length: {lengths[index]}, at index: {index}");
                }
                arrays[index] = new byte[lengths[index]];
            }

            int targetOffset = 0;
            int targetIndex = 0;

            int sourceOffset = 0;
            int sourceIndex = 0;

            do
            {
                Span<byte> targetSpan = Span<byte>.Empty;
                if (targetOffset < arrays[targetIndex].Length)
                {
                    targetSpan = arrays[targetIndex].AsSpan(targetOffset);
                }
                else
                {
                    targetIndex += 1;
                    targetOffset = 0;
                    continue;
                }

                Span<byte> sourceSpan = Span<byte>.Empty;
                if (sourceOffset < packets[sourceIndex].Length)
                {
                    sourceSpan = packets[sourceIndex].AsSpan(sourceOffset);
                }
                else
                {
                    sourceIndex += 1;
                    sourceOffset = 0;
                    continue;
                }

                int copy = Math.Min(targetSpan.Length, sourceSpan.Length);
                if (copy > 0)
                {
                    targetOffset += copy;
                    sourceOffset += copy;
                    sourceSpan.Slice(0, copy).CopyTo(targetSpan.Slice(0, copy));
                }
            } while (sourceIndex < packets.Count && targetIndex < arrays.Length);

            foreach (var array in arrays)
            {
                list.Add(new PacketData(array, 0, array.Length));
            }

            return list;
        }

        public static int PacketSizeFromDataSize(int dataSize) => TdsEnums.HEADER_LEN + dataSize;

        public static int DataSizeFromPacketSize(int packetSize) => packetSize - TdsEnums.HEADER_LEN;

        public static int SumPacketLengths(List<PacketData> list)
        {
            int total = 0;
            for (int index = 0; index < list.Count; index++)
            {
                total += list[index].Length;
            }
            return total;
        }
    }


    [DebuggerDisplay("{ToDebugString(),nq}")]
    public readonly struct PacketData
    {
        public readonly byte[] Array;
        public readonly int Start;
        public readonly int Length;

        public PacketData(byte[] array, int start, int length)
        {
            Array = array;
            Start = start;
            Length = length;
        }

        public Span<byte> AsSpan()
        {
            return Array == null ? Span<byte>.Empty : Array.AsSpan(Start, Length);
        }

        public Span<byte> AsSpan(int start)
        {
            Span<byte> span = AsSpan();
            return span.Slice(start);
        }

        public static PacketData Copy(byte[] array, int start, int length)
        {
            byte[] newArray = null;
            if (array != null)
            {
                newArray = new byte[array.Length];
                Buffer.BlockCopy(array, start, newArray, start, length);
            }
            return new PacketData(newArray, start, length);
        }

        [ExcludeFromCodeCoverage]
        public string ToDebugString()
        {
            StringBuilder buffer = new StringBuilder(128);
            buffer.Append(Length);

            if (Array != null && Array.Length > 0)
            {
                if (Array.Length != Length)
                {
                    buffer.AppendFormat(" (arr: {0})", Array.Length);
                }
                buffer.Append(": {");
                buffer.AppendFormat("{0:D2}", Array[0]);

                int max = Math.Min(32, Array.Length);
                for (int index = 1; index < max; index++)
                {
                    buffer.Append(',');
                    buffer.AppendFormat("{0:D2}", Array[index]);
                }
                if (Length > max)
                {
                    buffer.Append(" ...");
                }
                buffer.Append('}');
            }
            return buffer.ToString();
        }
    }

    [DebuggerStepThrough]
    public struct DataSize
    {
        public DataSize(int commonSize)
        {
            CommonSize = commonSize;
            LastSize = commonSize;
        }
        public DataSize(int commonSize, int lastSize)
        {
            CommonSize = commonSize;
            LastSize = lastSize;
        }

        public int LastSize { get; set; }
        public int CommonSize { get; set; }

        public int GetSize(bool isLast)
        {
            if (isLast)
            {
                return LastSize;
            }
            else
            {
                return CommonSize;
            }
        }

        public static implicit operator DataSize(int commonSize)
        {
            return new DataSize( commonSize,  commonSize );
        }

        public static implicit operator DataSize((int commonSize, int lastSize) values)
        {
            return new DataSize( values.commonSize, values.lastSize );
        }
    }
}
