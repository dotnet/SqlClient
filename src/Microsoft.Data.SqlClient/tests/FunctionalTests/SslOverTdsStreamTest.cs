// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public static class SslOverTdsStreamTest
    {
        public static TheoryData<int, int, int> PacketSizes
        {
            get
            {
                const int EncapsulatedPacketCount = 4;
                const int PassThroughPacketCount = 5;

                TheoryData<int, int, int> data = new TheoryData<int, int, int>();

                data.Add(EncapsulatedPacketCount, PassThroughPacketCount, 0);
                data.Add(EncapsulatedPacketCount, PassThroughPacketCount, 2);
                data.Add(EncapsulatedPacketCount, PassThroughPacketCount, 128);
                data.Add(EncapsulatedPacketCount, PassThroughPacketCount, 2048);
                data.Add(EncapsulatedPacketCount, PassThroughPacketCount, 8192);

                return data;
            }
        }


        [Theory]
        [MemberData(nameof(PacketSizes))]
        public static void SyncTest(int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength)
        {
            byte[] input;
            byte[] output;
            SetupArrays(encapsulatedPacketCount + passthroughPacketCount, out input, out output);

            byte[] buffer = WritePackets(encapsulatedPacketCount, passthroughPacketCount,
                (Stream stream, int index) =>
                {
                    stream.Write(input, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE * index, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE);
                }
            );

            ReadPackets(buffer, encapsulatedPacketCount, passthroughPacketCount, maxPacketReadLength, output,
                (Stream stream, byte[] bytes, int offset, int count) =>
                {
                    return stream.Read(bytes, offset, count);
                }
            );

            Validate(input, output);
        }

        [Theory]
        [MemberData(nameof(PacketSizes))]
        public static void AsyncTest(int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength)
        {
            byte[] input;
            byte[] output;
            SetupArrays(encapsulatedPacketCount + passthroughPacketCount, out input, out output);
            byte[] buffer = WritePackets(encapsulatedPacketCount, passthroughPacketCount,
                async (Stream stream, int index) =>
                {
                    await stream.WriteAsync(input, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE * index, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE);
                }
            );

            ReadPackets(buffer, encapsulatedPacketCount, passthroughPacketCount, maxPacketReadLength, output,
                async (Stream stream, byte[] bytes, int offset, int count) =>
                {
                    return await stream.ReadAsync(bytes, offset, count);
                }
            );

            Validate(input, output);
        }

        [Theory]
        [MemberData(nameof(PacketSizes))]
        public static void SyncCoreTest(int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength)
        {
            byte[] input;
            byte[] output;
            SetupArrays(encapsulatedPacketCount + passthroughPacketCount, out input, out output);

            byte[] buffer = WritePackets(encapsulatedPacketCount, passthroughPacketCount,
                (Stream stream, int index) =>
                {
                    stream.Write(input.AsSpan(TdsEnums.DEFAULT_LOGIN_PACKET_SIZE * index, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE));
                }
            );

            ReadPackets(buffer, encapsulatedPacketCount, passthroughPacketCount, maxPacketReadLength, output,
                (Stream stream, byte[] bytes, int offset, int count) =>
                {
                    return stream.Read(bytes.AsSpan(offset, count));
                }
            );

            Validate(input, output);
        }

        [Theory]
        [MemberData(nameof(PacketSizes))]
        public static void AsyncCoreTest(int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength)
        {
            byte[] input;
            byte[] output;
            SetupArrays(encapsulatedPacketCount + passthroughPacketCount, out input, out output);

            byte[] buffer = WritePackets(encapsulatedPacketCount, passthroughPacketCount,
                async (Stream stream, int index) =>
                {
                    await stream.WriteAsync(
                        new ReadOnlyMemory<byte>(input, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE * index, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE)
                    );
                }
            );

            ReadPackets(buffer, encapsulatedPacketCount, passthroughPacketCount, maxPacketReadLength, output,
                async (Stream stream, byte[] bytes, int offset, int count) =>
                {
                    return await stream.ReadAsync(
                        new Memory<byte>(bytes, offset, count)
                    );
                }
            );

            Validate(input, output);
        }


        private static void ReadPackets(byte[] buffer, int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength, byte[] output, Func<Stream, byte[], int, int, Task<int>> action)
        {
            using (LimitedMemoryStream stream = new LimitedMemoryStream(buffer, maxPacketReadLength))
            using (Stream tdsStream = CreateSslOverTdsStream(stream))
            {
                int offset = 0;
                byte[] bytes = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
                for (int index = 0; index < encapsulatedPacketCount; index++)
                {
                    Array.Clear(bytes, 0, bytes.Length);
                    int packetBytes = ReadPacket(tdsStream, action, bytes).GetAwaiter().GetResult();
                    Array.Copy(bytes, 0, output, offset, packetBytes);
                    offset += packetBytes;
                }
                InvokeFinishHandshake(tdsStream);
                for (int index = 0; index < passthroughPacketCount; index++)
                {
                    Array.Clear(bytes, 0, bytes.Length);
                    int packetBytes = ReadPacket(tdsStream, action, bytes).GetAwaiter().GetResult();
                    Array.Copy(bytes, 0, output, offset, packetBytes);
                    offset += packetBytes;
                }
            }
        }

        private static void InvokeFinishHandshake(Stream stream)
        {
            MethodInfo method = stream.GetType().GetMethod("FinishHandshake", BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(stream, null);
        }

        private static Stream CreateSslOverTdsStream(Stream stream)
        {
            Type type = typeof(SqlClientFactory).Assembly.GetType("Microsoft.Data.SqlClient.SNI.SslOverTdsStream");
            ConstructorInfo ctor = type.GetConstructor(new Type[] { typeof(Stream) });
            Stream instance = (Stream)ctor.Invoke(new object[] { stream });
            return instance;
        }

        private static void ReadPackets(byte[] buffer, int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength, byte[] output, Func<Stream, byte[], int, int, int> action)
        {
            using (LimitedMemoryStream stream = new LimitedMemoryStream(buffer, maxPacketReadLength))
            using (Stream tdsStream = CreateSslOverTdsStream(stream))
            {
                int offset = 0;
                byte[] bytes = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
                for (int index = 0; index < encapsulatedPacketCount; index++)
                {
                    Array.Clear(bytes, 0, bytes.Length);
                    int packetBytes = ReadPacket(tdsStream, action, bytes);
                    Array.Copy(bytes, 0, output, offset, packetBytes);
                    offset += packetBytes;
                }
                InvokeFinishHandshake(tdsStream);
                for (int index = 0; index < passthroughPacketCount; index++)
                {
                    Array.Clear(bytes, 0, bytes.Length);
                    int packetBytes = ReadPacket(tdsStream, action, bytes);
                    Array.Copy(bytes, 0, output, offset, packetBytes);
                    offset += packetBytes;
                }
            }
        }

        private static int ReadPacket(Stream tdsStream, Func<Stream, byte[], int, int, int> action, byte[] output)
        {
            int readCount;
            int offset = 0;
            byte[] bytes = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
            do
            {
                readCount = action(tdsStream, bytes, offset, bytes.Length - offset);
                if (readCount > 0)
                {
                    offset += readCount;
                }
            }
            while (readCount > 0 && offset < bytes.Length);
            Array.Copy(bytes, 0, output, 0, offset);
            return offset;
        }

        private static async Task<int> ReadPacket(Stream tdsStream, Func<Stream, byte[], int, int, Task<int>> action, byte[] output)
        {
            int readCount;
            int offset = 0;
            byte[] bytes = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
            do
            {
                readCount = await action(tdsStream, bytes, offset, bytes.Length - offset);
                if (readCount > 0)
                {
                    offset += readCount;
                }
            }
            while (readCount > 0 && offset < bytes.Length);
            Array.Copy(bytes, 0, output, 0, offset);
            return offset;
        }

        private static byte[] WritePackets(int encapsulatedPacketCount, int passthroughPacketCount, Action<Stream, int> action)
        {
            byte[] buffer = null;
            using (LimitedMemoryStream stream = new LimitedMemoryStream())
            {
                using (Stream tdsStream = CreateSslOverTdsStream(stream))
                {
                    for (int index = 0; index < encapsulatedPacketCount; index++)
                    {
                        action(tdsStream, index);
                    }
                    InvokeFinishHandshake(tdsStream);//tdsStream.FinishHandshake();
                    for (int index = 0; index < passthroughPacketCount; index++)
                    {
                        action(tdsStream, encapsulatedPacketCount + index);
                    }
                }
                buffer = stream.ToArray();
            }
            return buffer;
        }

        private static void SetupArrays(int packetCount, out byte[] input, out byte[] output)
        {
            byte[] pattern = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            input = new byte[packetCount * TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
            output = new byte[input.Length];
            for (int index = 0; index < packetCount; index++)
            {
                int position = 0;
                while (position < TdsEnums.DEFAULT_LOGIN_PACKET_SIZE)
                {
                    int copyCount = Math.Min(pattern.Length, TdsEnums.DEFAULT_LOGIN_PACKET_SIZE - position);
                    Array.Copy(pattern, 0, input, (TdsEnums.DEFAULT_LOGIN_PACKET_SIZE * index) + position, copyCount);
                    position += copyCount;
                }
            }
        }

        private static void Validate(byte[] input, byte[] output)
        {
            Assert.True(input.AsSpan().SequenceEqual(output.AsSpan()));
        }

        internal static class TdsEnums
        {
            public const int DEFAULT_LOGIN_PACKET_SIZE = 4096;
        }
    }

    [DebuggerStepThrough]
    public sealed partial class LimitedMemoryStream : MemoryStream
    {
        private readonly int _readLimit;
        private readonly int _delay;

        public LimitedMemoryStream(int readLimit = 0, int delay = 0)
        {
            _readLimit = readLimit;
            _delay = delay;
        }

        public LimitedMemoryStream(byte[] buffer, int readLimit = 0, int delay = 0)
            : base(buffer)
        {
            _readLimit = readLimit;
            _delay = delay;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_readLimit > 0)
            {
                return base.Read(buffer, offset, Math.Min(_readLimit, count));
            }
            else
            {
                return base.Read(buffer, offset, count);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_delay > 0)
            {
                await Task.Delay(_delay, cancellationToken);
            }
            if (_readLimit > 0)
            {
                return await base.ReadAsync(buffer, offset, Math.Min(_readLimit, count), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
        }
        public override int Read(Span<byte> destination)
        {
            if (_readLimit > 0)
            {
                return base.Read(destination.Slice(0, Math.Min(_readLimit, destination.Length)));
            }
            else
            {
                return base.Read(destination);
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            if (_readLimit > 0)
            {
                return base.ReadAsync(destination.Slice(0, Math.Min(_readLimit, destination.Length)), cancellationToken);
            }
            else
            {
                return base.ReadAsync(destination, cancellationToken);
            }
        }
    }
}
