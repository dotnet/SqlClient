using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.Tests
{
    public static partial class SslOverTdsStreamTest
    {
        static partial void SyncCoreTest(int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength)
        {
            (byte[] input, byte[] output) = SetupArrays(encapsulatedPacketCount + passthroughPacketCount);

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

        static partial void AsyncCoreTest(int encapsulatedPacketCount, int passthroughPacketCount, int maxPacketReadLength)
        {
            (byte[] input, byte[] output) = SetupArrays(encapsulatedPacketCount + passthroughPacketCount);

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
    }

    public sealed partial class LimitedMemoryStream : MemoryStream
    {
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
