// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClientX.IO;
using Microsoft.Data.SqlClient.UnitTests.IO.TdsHelpers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    public partial class TdsReadStreamTest
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadSinglePacketWithoutSplit(bool isAsync)
        {

            int negotiatedPacketSize = 200;
            TdsMessage message = TdsReadStreamTest.PrepareTdsMessage(negotiatedPacketSize, 100);
            SplittableStream splitStream = SplittableStream.FromMessage(message);

            using TdsReadStream stream = new(splitStream);
            byte[] readBuffer = new byte[100];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, message.Payload.Length) : stream.Read(readBuffer, 0, message.Payload.Length);

            Assert.Equal(message.Payload.Length, readCount);
            Assert.Equal(message.Payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadSingleByte(bool isAsync)
        {
            int negotiatedPacketSize = 200;
            TdsMessage message = TdsReadStreamTest.PrepareTdsMessage(negotiatedPacketSize, 100);
            SplittableStream splitStream = SplittableStream.FromMessage(message);

            using TdsReadStream stream = new(splitStream);
            byte[] readBuffer = new byte[100];

            for (int i = 0; i < message.Payload.Length; i++)
            {
                byte readByte = await stream.ReadByteAsync(isAsync, default);
                Assert.Equal(message.Payload[i], readByte);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadSkipBytes(bool isAsync)
        {
            int negotiatedPacketSize = 200;

            TdsMessage message = TdsReadStreamTest.PrepareTdsMessage(negotiatedPacketSize, 100);
            SplittableStream splitStream = SplittableStream.FromMessage(message);

            using TdsReadStream stream = new(splitStream);
            stream.SetPacketSize(negotiatedPacketSize);

            byte[] readBuffer = new byte[100];

            int skipCount = new Random().Next() % 50;
            await stream.SkipReadBytesAsync(skipCount, isAsync, default);
            for (int i = skipCount; i < message.Payload.Length; i++)
            {
                byte readByte = await stream.ReadByteAsync(isAsync, default);
                Assert.Equal(message.Payload[i], readByte);
            }
            Assert.Equal(message.Spid, stream.Spid);
        }


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadPacketSplit(bool isAsync)
        {

            int negotiatedPacketSize = 200;
            TdsMessage message = TdsReadStreamTest.PrepareTdsMessage(negotiatedPacketSize, 100);

            int splitSize = 4;
            byte[] messageBytes = message.GetBytes();
            SplittableStream splitStream = new(messageBytes, splitSize);


            using TdsReadStream stream = new TdsReadStream(splitStream);
            byte[] readBuffer = new byte[100];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, 2) :
                                stream.Read(readBuffer, 0, 2);

            Assert.Equal(2, readCount);
            Assert.Equal(message.Payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }



        /// <summary>
        /// This test splits the packet sending so that the partial header is 
        /// received in one underlying stream read by the TdsReadStream.
        /// </summary>
        /// <param name="isAsync"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadPacketSplitWithPartialHeader(bool isAsync)
        {

            int negotiatedPacketSize = 200;
            TdsMessage message = TdsReadStreamTest.PrepareTdsMessage(negotiatedPacketSize, 500);

            SplittableStream splitStream = SplittableStream.FromMessage(message, negotiatedPacketSize + 3);


            using TdsReadStream stream = new(splitStream);
            byte[] readBuffer = new byte[message.Payload.Length];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, message.Payload.Length) :
                                stream.Read(readBuffer, 0, message.Payload.Length);


            Assert.Equal(readBuffer.Length, readCount);

            Assert.Equal(message.Payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }

        /// <summary>
        /// This test splits the packet sending so that the header is 
        /// received in one underlying stream read by the TdsReadStream.
        /// </summary>
        /// <param name="isAsync"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadPacketSplitWithFullHeaderAndPartialPayload(bool isAsync)
        {

            int negotiatedPacketSize = 200;
            TdsMessage message = TdsReadStreamTest.PrepareTdsMessage(negotiatedPacketSize, 500);

            SplittableStream splitStream = SplittableStream.FromMessage(message, negotiatedPacketSize + TdsEnums.HEADER_LEN + 10);

            using TdsReadStream stream = new (splitStream);
            byte[] readBuffer = new byte[message.Payload.Length];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, message.Payload.Length) :
                                stream.Read(readBuffer, 0, message.Payload.Length);

            Assert.Equal(readBuffer.Length, readCount);
            Assert.Equal(message.Payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }

        private static TdsMessage PrepareTdsMessage(int negotiatedPacketSize, int payloadSize)
        {
            byte[] payload = new byte[payloadSize];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }
            byte messageType = TdsEnums.MT_PRELOGIN;
            int spid = TdsUtils.GenerateSpid();
            return new TdsMessage(negotiatedPacketSize, payload, messageType, spid);
        }

        internal static TdsMessage PrepareTdsMessage(int negotiatedPacketSize, byte[] payload, byte messageType)
        {
            int spid = TdsUtils.GenerateSpid();
            return new TdsMessage(negotiatedPacketSize, payload, messageType, spid);
        }
    }
}
