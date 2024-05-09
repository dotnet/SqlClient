using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal class TdsPreLoginHandler : OutgoingPacketHandler
    {
        private TdsWriteStream _writeStream;

        internal LucidTdsEnums.FeatureExtension RequestedFeatures { get; private set; }

        protected override byte PacketHeaderType => TdsEnums.MT_PRELOGIN;

        public TdsPreLoginHandler(TdsWriteStream writeStream) 
        {
            _writeStream = writeStream;
            _writeStream.PacketHeaderType = PacketHeaderType;
        }
        
        public async ValueTask Send(
            bool isAsync, 
            CancellationToken ct)
        {
            // 5 bytes for each option (1 byte length, 2 byte offset, 2 byte payload length)
            int preloginOptionsCount = 7;
            int offset = 36; // 7 * 5 + 1 add 1 to start after the first 40 bytes
            // The payload is the bytes for all the options and the maximum length of the payload
            byte[] payload = new byte[preloginOptionsCount * 5 + TdsConstants.MAX_PRELOGIN_PAYLOAD_LENGTH];
            int payLoadIndex = 0;

            for (int option = 0; option < preloginOptionsCount; option++)
            {
                int optionDataSize = 0;

                // Fill in the option
                await _writeStream.WriteByteAsync((byte)option, isAsync, ct).ConfigureAwait(false);

                // Fill in the offset of the option data
                await _writeStream.WriteByteAsync((byte)((offset & 0xff00) >> 8), isAsync, ct).ConfigureAwait(false); // send upper order byte
                await _writeStream.WriteByteAsync((byte)(offset & 0x00ff), isAsync, ct).ConfigureAwait(false); // send lower order byte

                switch (option)
                {
                    case 0:
                        Version.TryParse("6.0.0.0", out Version systemDataVersion);

                        // Major and minor
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Major & 0xff);
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Minor & 0xff);

                        // Build (Big Endian)
                        payload[payLoadIndex++] = (byte)((systemDataVersion.Build & 0xff00) >> 8);
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Build & 0xff);

                        // Sub-build (Little Endian)
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Revision & 0xff);
                        payload[payLoadIndex++] = (byte)((systemDataVersion.Revision & 0xff00) >> 8);
                        offset += 6;
                        optionDataSize = 6;
                        break;

                    case 1:

                        // Assume that the encryption is off
                        payload[payLoadIndex] = (byte)0;

                        payLoadIndex += 1;
                        offset += 1;
                        optionDataSize = 1;
                        break;

                    case 2:
                        int i = 0;
                        // Assume we dont need to send the instance name.
                        byte[] instanceName = new byte[1];
                        while (instanceName[i] != 0)
                        {
                            payload[payLoadIndex] = instanceName[i];
                            payLoadIndex++;
                            i++;
                        }

                        payload[payLoadIndex] = 0; // null terminate
                        payLoadIndex++;
                        i++;

                        offset += i;
                        optionDataSize = i;
                        break;

                    case (int)PreLoginOptions.THREADID:
                        int threadID = 1234; // Hard code some thread on the client side.

                        payload[payLoadIndex++] = (byte)((0xff000000 & threadID) >> 24);
                        payload[payLoadIndex++] = (byte)((0x00ff0000 & threadID) >> 16);
                        payload[payLoadIndex++] = (byte)((0x0000ff00 & threadID) >> 8);
                        payload[payLoadIndex++] = (byte)(0x000000ff & threadID);
                        offset += 4;
                        optionDataSize = 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payload[payLoadIndex++] = (byte)(0); // Turn off MARS
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        Guid connectionId = Guid.NewGuid();
                        connectionId.TryWriteBytes(payload.AsSpan(payLoadIndex, TdsConstants.GUID_SIZE)); // 16 is the size of a GUID
                        payLoadIndex += TdsConstants.GUID_SIZE;
                        offset += TdsConstants.GUID_SIZE;
                        optionDataSize = TdsConstants.GUID_SIZE;

                        Guid activityId = Guid.NewGuid();
                        uint sequence = 123;
                        activityId.TryWriteBytes(payload.AsSpan(payLoadIndex, 16)); // 16 is the size of a GUID
                        payLoadIndex += TdsConstants.GUID_SIZE;
                        payload[payLoadIndex++] = (byte)(0x000000ff & sequence);
                        payload[payLoadIndex++] = (byte)((0x0000ff00 & sequence) >> 8);
                        payload[payLoadIndex++] = (byte)((0x00ff0000 & sequence) >> 16);
                        payload[payLoadIndex++] = (byte)((0xff000000 & sequence) >> 24);
                        int actIdSize = TdsConstants.GUID_SIZE + sizeof(uint);
                        offset += actIdSize;
                        optionDataSize += actIdSize;
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payload[payLoadIndex++] = 0x01;
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in SendPreLoginHandshake");
                        break;
                }

                // Write data length
                await _writeStream.WriteByteAsync((byte)((optionDataSize & 0xff00) >> 8), isAsync, ct).ConfigureAwait(false);
                await _writeStream.WriteByteAsync((byte)(optionDataSize & 0x00ff), isAsync, ct).ConfigureAwait(false);
            }
            await _writeStream.WriteByteAsync((byte)255, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteArrayAsync(isAsync, payload[..payLoadIndex], ct).ConfigureAwait(false);
            await _writeStream.FlushAsync(ct, isAsync, hardFlush: true).ConfigureAwait(false);
        }
    }
}
