using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    partial class TdsParserStateObject
    {
        private Packet __partialPacket;
        private Packet _partialPacket => __partialPacket;

        private void SetPartialPacket(Packet packet/*, [CallerMemberName] string caller = null*/)
        {
            if (__partialPacket != null && packet != null)
            {
                throw new InvalidOperationException("partial packet cannot be non-null when setting to non=null");
            }
            __partialPacket = packet;
        }

        private void ClearPartialPacket(/*[CallerMemberName] string caller = null*/)
        {
            Packet partialPacket = __partialPacket;
            __partialPacket = null;
            if (partialPacket != null)
            {
                ReadOnlySpan<byte> header = partialPacket.GetHeaderSpan();
                int packetId = Packet.GetIDFromHeader(header);
                bool isEOM = Packet.GetIsEOMFromHeader(header);
                partialPacket.Dispose();
            }
        }

        public static void MultiplexPackets(
            byte[] dataBuffer, int dataOffset, int dataLength,
            Packet partialPacket,
            out int newDataOffset,
            out int newDataLength,
            out Packet remainderPacket,
            out bool consumeInputDirectly,
            out bool consumePartialPacket,
            out bool consumeRemainderPacket,
            out bool recurse
        )
        {
            ReadOnlySpan<byte> data = dataBuffer.AsSpan(dataOffset, dataLength);
            remainderPacket = null;
            consumeInputDirectly = false;
            consumePartialPacket = false;
            consumeRemainderPacket = false;
            recurse = false;

            newDataLength = dataLength;
            newDataOffset = dataOffset;

            int bytesConsumed = 0;

            if (partialPacket != null)
            {
                if (!partialPacket.HasDataLength)
                {
                    // we need to get enough bytes to read the packet header
                    int headeBytesNeeded = Math.Max(0, TdsEnums.HEADER_LEN - partialPacket.CurrentLength);
                    if (headeBytesNeeded > 0)
                    {
                        int headerBytesAvailable = Math.Min(data.Length, headeBytesNeeded);
                        Span<byte> headerTarget = partialPacket.Buffer.AsSpan(partialPacket.CurrentLength, headerBytesAvailable);
                        ReadOnlySpan<byte> headerSource = data.Slice(0, headerBytesAvailable);
                        headerSource.CopyTo(headerTarget);
                        partialPacket.CurrentLength = partialPacket.CurrentLength + headerBytesAvailable;
                        data = data.Slice(headerBytesAvailable);
                        bytesConsumed += headerBytesAvailable;
                    }
                    if (partialPacket.HasHeader)
                    {
                        partialPacket.DataLength = Packet.GetDataLengthFromHeader(partialPacket);
                        //if (partialPacket.DataLength > dataBuffer.Length)
                        //{
                        //    Debugger.Break();
                        //}
                    }
                }

                if (partialPacket.HasDataLength)
                {
                    if (partialPacket.CurrentLength < partialPacket.RequiredLength)
                    {
                        // the packet length is known so take as much data as possible from the incoming
                        // data to try and complete the packet
                        int payloadBytesNeeded = partialPacket.DataLength - (partialPacket.CurrentLength - TdsEnums.HEADER_LEN);
                        int payloadBytesAvailable = Math.Min(data.Length, payloadBytesNeeded);
                        Span<byte> payloadTarget = partialPacket.Buffer.AsSpan(partialPacket.CurrentLength, payloadBytesAvailable);
                        ReadOnlySpan<byte> payloadSource = data.Slice(0, payloadBytesAvailable);
                        payloadSource.CopyTo(payloadTarget);
                        partialPacket.CurrentLength = partialPacket.CurrentLength + payloadBytesAvailable;
                        bytesConsumed += payloadBytesAvailable;
                        data = data.Slice(payloadBytesAvailable);
                    }
                    else if (partialPacket.CurrentLength > partialPacket.RequiredLength)
                    {
                        // the packet contains an entire packet and more data after that so we need
                        // to extract the following data into a new packet with a new buffer and return
                        // it as the remainer packet

                        int remainderLength = partialPacket.CurrentLength - partialPacket.RequiredLength;
                        remainderPacket = new Packet
                        {
                            Buffer = new byte[dataBuffer.Length],
                            CurrentLength = remainderLength,
                        };
                        Buffer.BlockCopy(
                            partialPacket.Buffer, partialPacket.RequiredLength, // from
                            remainderPacket.Buffer, 0, // to
                            remainderPacket.CurrentLength // for
                        );
                        partialPacket.CurrentLength = partialPacket.CurrentLength - remainderPacket.CurrentLength;
                        consumeRemainderPacket = true;

                        if (remainderPacket.HasHeader)
                        {
                            remainderPacket.DataLength = Packet.GetDataLengthFromHeader(remainderPacket);
                            if (remainderPacket.HasDataLength && remainderPacket.CurrentLength >= remainderPacket.RequiredLength)
                            {
                                recurse = true;
                            }
                        }
                    }

                    if (partialPacket.CurrentLength == partialPacket.RequiredLength)
                    {
                        // partial packet has been completed
                        consumePartialPacket = true;
                    }
                }

                if (bytesConsumed > 0)
                {
                    if (data.Length > 0)
                    {
                        //if (data[0] == 120)
                        //{
                        //    var d = Vizualize(dataBuffer, dataOffset, dataLength);
                        //    Debugger.Break();
                        //}

                        // some data has been taken from the buffer, put into the partial
                        // packet buffer and we have data left so move the data we have
                        // left to the start of the buffer so we can pass the buffer back
                        // as zero based to the caller avoiding offset calculations everywhere
                        Buffer.BlockCopy(
                            dataBuffer, dataOffset + bytesConsumed, // from
                            dataBuffer, dataOffset, // to
                            dataLength - bytesConsumed // for
                        );

                        //// for debugging purposes fill the removed data area with an easily
                        //// recognisable pattern so we can see if it is misused
                        //Span<byte> removed = dataBuffer.AsSpan(dataOffset + (dataLength - bytesConsumed), (dataOffset + bytesConsumed));
                        //removed.Fill(0xFF);

                        // then recreate the data span so that we're looking at the data
                        // that has been moved
                        data = dataBuffer.AsSpan(dataOffset, dataLength - bytesConsumed);
                        //if (data[0] == 120)
                        //{
                        //    Debugger.Break();
                        //}
                    }

                    newDataLength = dataLength - bytesConsumed;
                }
            }

            if (data.Length > 0 && !consumeRemainderPacket)
            {
                if (data.Length >= TdsEnums.HEADER_LEN)
                {
                    // we have enough bytes to read the packet header and see how
                    // much data we are expecting it to contain
                    int packetDataLength = Packet.GetDataLengthFromHeader(data);
                    //if (packetDataLength > dataBuffer.Length)
                    //{
                    //    Debugger.Break();
                    //}
                    if (data.Length == TdsEnums.HEADER_LEN + packetDataLength)
                    {
                        if (!consumePartialPacket)
                        {
                            // we can tell the caller that they should directly consume the data in
                            // the input buffer, this is the happy path
                            consumeInputDirectly = true;
                        }
                        else
                        {
                            // we took some data from the input to reconstruct the partial packet
                            // so we can't tell the caller to directly consume the packet in the
                            // input buffer, we need to construct a new remainder packet and then
                            // tell them to consume it
                            remainderPacket = new Packet
                            {
                                Buffer = dataBuffer,
                                DataLength = packetDataLength,
                                CurrentLength = data.Length
                            };
                            consumeRemainderPacket = true;
                            //Debug.Assert(remainderPacket.HasHeader); // precondition of entering this block
                            //Debug.Assert(remainderPacket.HasDataLength); // must have been set at construction
                            if (remainderPacket.CurrentLength >= remainderPacket.RequiredLength)
                            {
                                // the remainder packet contains more data than the packet so we need
                                // to tell the caller to recurse into this function again once they have
                                // consumed the first packet
                                recurse = true;
                            }
                        }
                    }
                    else if (data.Length < TdsEnums.HEADER_LEN + packetDataLength)
                    {
                        // another partial packet so produce one and tell the caller that they need
                        // consume it.
                        remainderPacket = new Packet
                        {
                            Buffer = dataBuffer,
                            DataLength = packetDataLength,
                            CurrentLength = data.Length
                        };
                        consumeRemainderPacket = true;
                        Debug.Assert(remainderPacket.HasHeader); // precondition of entering this block
                        if (remainderPacket.HasDataLength && remainderPacket.CurrentLength >= remainderPacket.RequiredLength)
                        {
                            // the remainder packet contains more data than the packet so we need
                            // to tell the caller to recurse into this function again once they have
                            // consumed the first packet
                            recurse = true;
                        }
                    }
                    else // implied: current length > required length
                    {
                        // more data than required so need to split it out but we can't do that
                        // here so we need to tell the caller to take the remainer packet and then
                        // call this function again
                        remainderPacket = new Packet
                        {
                            Buffer = dataBuffer,
                            DataLength = packetDataLength,
                            CurrentLength = data.Length
                        };
                        consumeRemainderPacket = true;
                        recurse = true;
                    }
                }
                else
                {
                    // we don't have enough information to read the header
                    if (!consumePartialPacket)
                    {
                        // we can tell the caller that they should directly consume the data in
                        // the input buffer, this is the happy path
                        consumeInputDirectly = true;
                    }
                    else
                    {
                        // we took some data from the input to reconstruct the partial packet
                        // so we can't tell the caller to directly consume the packet in the
                        // input buffer, we need to construct a new remainder packet and then
                        // tell them to consume it
                        remainderPacket = new Packet
                        {
                            Buffer = dataBuffer,
                            CurrentLength = data.Length
                        };
                        consumeRemainderPacket = true;
                        if (remainderPacket.HasHeader)
                        {
                            remainderPacket.DataLength = Packet.GetDataLengthFromHeader(remainderPacket.GetHeaderSpan());
                        }
                        if (remainderPacket.HasDataLength && remainderPacket.CurrentLength >= remainderPacket.RequiredLength)
                        {
                            // the remainder packet contains more data than the packet so we need
                            // to tell the caller to recurse into this function again once they have
                            // consumed the first packet
                            recurse = true;
                        }
                    }
                }
            }

            if (remainderPacket != null && remainderPacket.HasHeader)
            {
                remainderPacket.Buffer[7] = 0xF;
            }

            if (consumePartialPacket && consumeInputDirectly)
            {
                throw new InvalidOperationException($"AppendData cannot return both {nameof(consumePartialPacket)} and {nameof(consumeInputDirectly)}");
            }
        }
    }
}
