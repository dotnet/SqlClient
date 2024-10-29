using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
#if NETFRAMEWORK
    using PacketHandle = IntPtr;
#endif
    partial class TdsParserStateObject
    {
        private Packet __partialPacket;
        internal Packet _partialPacket => __partialPacket;

        public void ProcessSniPacket(PacketHandle packet, uint error, bool usePartialPacket = false)
        {
            if (error != 0)
            {
                if ((_parser.State == TdsParserState.Closed) || (_parser.State == TdsParserState.Broken))
                {
                    // Do nothing with callback if closed or broken and error not 0 - callback can occur
                    // after connection has been closed.  PROBLEM IN NETLIB - DESIGN FLAW.
                    return;
                }

                AddError(_parser.ProcessSNIError(this));
                AssertValidState();
            }
            else
            {
                uint dataSize = 0;
                bool usedPartialPacket = false;
                uint getDataError = 0;

                if (usePartialPacket && _snapshot == null && _partialPacket != null && _partialPacket.IsComplete)
                {
                    //Debug.Assert(_snapshot == null, "_snapshot must be null when processing partial packet instead of network read");
                    //Debug.Assert(_partialPacket != null, "_partialPacket must not be null when usePartialPacket is true");
                    //Debug.Assert(_partialPacket.IsComplete, "_partialPacket.IsComplete must be true to use it in place of a real read");
                    SetBuffer(_partialPacket.Buffer, 0, _partialPacket.CurrentLength);
                    ClearPartialPacket();
                    getDataError = TdsEnums.SNI_SUCCESS;
                    usedPartialPacket = true;
                }
                else
                {
                    getDataError = GetSniPacket(packet, ref dataSize);
                }

                if (getDataError == TdsEnums.SNI_SUCCESS)
                {
                    if (_inBuff.Length < dataSize)
                    {
                        Debug.Assert(true, "Unexpected dataSize on Read");
                        throw SQL.InvalidInternalPacketSize(StringsHelper.GetString(Strings.SqlMisc_InvalidArraySizeMessage));
                    }

                    if (!usedPartialPacket)
                    {
                        _lastSuccessfulIOTimer._value = DateTime.UtcNow.Ticks;

                        SetBuffer(_inBuff, 0, (int)dataSize);
                    }

                    bool recurse;
                    bool appended = false;
                    do
                    {
                        MultiplexPackets(
                            _inBuff, _inBytesUsed, _inBytesRead,
                            _partialPacket,
                            out int newDataOffset,
                            out int newDataLength,
                            out Packet remainderPacket,
                            out bool consumeInputDirectly,
                            out bool consumePartialPacket,
                            out bool remainderPacketProduced,
                            out recurse
                        );
                        bool bufferIsPartialCompleted = false;

                        // if a partial packet was reconstructed it must be handled first
                        if (consumePartialPacket)
                        {
                            if (_snapshot != null)
                            {
                                _snapshot.AppendPacketData(_partialPacket.Buffer, _partialPacket.CurrentLength);
                                SetBuffer(new byte[_inBuff.Length], 0, 0);
                                appended = true;
                            }
                            else
                            {
                                SetBuffer(_partialPacket.Buffer, 0, _partialPacket.CurrentLength);
                                
                            }
                            bufferIsPartialCompleted = true;
                            ClearPartialPacket();
                        }

                        // if the remaining data can be processed directly it must be second
                        if (consumeInputDirectly)
                        {
                            // if some data was taken from the new packet adjust the counters
                            if (dataSize != newDataLength || 0 != newDataOffset)
                            {
                                SetBuffer(_inBuff, newDataOffset, newDataLength);
                            }

                            if (_snapshot != null)
                            {
                                _snapshot.AppendPacketData(_inBuff, _inBytesRead);
                                SetBuffer(new byte[_inBuff.Length], 0, 0);
                                appended = true;
                            }
                            else
                            {
                                SetBuffer(_inBuff, 0, _inBytesRead);
                            }
                            bufferIsPartialCompleted = true;
                        }
                        else
                        {
                            // whatever is in the input buffer should not be directly consumed
                            // and is contained in the partial or remainder packets so make sure
                            // we don't process it
                            if (!bufferIsPartialCompleted)
                            {
                                SetBuffer(_inBuff, 0, 0);
                            }
                        }

                        // if there is a remainder it must be last
                        if (remainderPacketProduced)
                        {
                            SetPartialPacket(remainderPacket);
                            if (!bufferIsPartialCompleted)
                            {
                                // we are keeping the partial packet buffer so replace it with a new one
                                // unless we have already set the buffer to the partial packet buffer
                                SetBuffer(new byte[_inBuff.Length], 0, 0);
                            }
                        }

                    } while (recurse && _snapshot != null);

                    if (_snapshot != null)
                    {
                        if (_snapshotStatus != SnapshotStatus.NotActive && appended)
                        {
                            _snapshot.MoveNext();
#if DEBUG
                            // multiple packets can be appended by demuxing but we should only move 
                            // forward by a single packet so we can no longer assert that we are on
                            // the last packet at this time
                            //_snapshot.AssertCurrent();
#endif
                        }
                    }

                    SniReadStatisticsAndTracing();
                    SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParser.ReadNetworkPacketAsyncCallback | INFO | ADV | State Object Id {0}, Packet read. In Buffer {1}, In Bytes Read: {2}", ObjectID, _inBuff, (ushort)_inBytesRead);

                    AssertValidState();
                }
                else
                {
                    throw SQL.ParsingError(ParsingErrorState.ProcessSniPacketFailed);
                }
            }
        }

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
                partialPacket.Dispose();
            }
        }

        private static void MultiplexPackets(
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
                        bytesConsumed += headerBytesAvailable;
                        data = data.Slice(headerBytesAvailable);
                    }
                    if (partialPacket.HasHeader)
                    {
                        partialPacket.DataLength = Packet.GetDataLengthFromHeader(partialPacket);
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

                        ReadOnlySpan<byte> payloadSource = data.Slice(0, payloadBytesAvailable);
                        Span<byte> payloadTarget = partialPacket.Buffer.AsSpan(partialPacket.CurrentLength, payloadBytesAvailable);
                        payloadSource.CopyTo(payloadTarget);

                        partialPacket.CurrentLength = partialPacket.CurrentLength + payloadBytesAvailable;
                        bytesConsumed += payloadBytesAvailable;
                        data = data.Slice(payloadBytesAvailable);
                    }
                    else if (partialPacket.CurrentLength > partialPacket.RequiredLength)
                    {
                        // the partial packet contains a complete packet of data and then and also contains
                        // data from a following packet

                        // the TDS spec requires that all packets be of the defined packet size apart from
                        // the last packet of a response. This means that is is not possible to have more than
                        // 2 packet fragments in a single buffer like this:
                        //  - first packet caused the partial
                        //  - second packet is the one we have just unpacked
                        //  - third packet is the extra data we have found

                        // we must throw an exception because we have encountered an invalid tds stream
                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
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
                        // some data has been taken from the buffer, put into the partial
                        // packet buffer and we have data left so move the data we have
                        // left to the start of the buffer so we can pass the buffer back
                        // as zero based to the caller avoiding offset calculations everywhere
                        Buffer.BlockCopy(
                            dataBuffer, dataOffset + bytesConsumed, // from
                            dataBuffer, dataOffset, // to
                            dataLength - bytesConsumed // for
                        );
#if DEBUG
                        // for debugging purposes fill the removed data area with an easily
                        // recognisable pattern so we can see if it is misused
                        Span<byte> removed = dataBuffer.AsSpan(dataOffset + (dataLength - bytesConsumed), (dataOffset + bytesConsumed));
                        removed.Fill(0xFF);
#endif

                        // then recreate the data span so that we're looking at the data
                        // that has been moved
                        data = dataBuffer.AsSpan(dataOffset, dataLength - bytesConsumed);
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
                    }
                    else // implied: current length > required length
                    {
                        //// more data than required so need to split it out but we can't do that
                        //// here so we need to tell the caller to take the remainer packet and then
                        //// call this function again

                        int remainderLength = data.Length - (TdsEnums.HEADER_LEN + packetDataLength);
                        remainderPacket = new Packet
                        {
                            Buffer = new byte[dataBuffer.Length],
                            CurrentLength = remainderLength,
                        };

                        ReadOnlySpan<byte> remainderSource = data.Slice(TdsEnums.HEADER_LEN + packetDataLength);
                        Span<byte> remainderTarget = remainderPacket.Buffer.AsSpan(0, remainderLength);
                        remainderSource.CopyTo(remainderTarget);

                        newDataLength = TdsEnums.HEADER_LEN + packetDataLength;
                        consumeInputDirectly = true;
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
                }
            }
#if DEBUG
            //// the Window field is unused by the spec so it can be used as a marker
            ////  to identify reconstructed packets while debugging
            //if (remainderPacket != null && remainderPacket.HasHeader)
            //{
            //    remainderPacket.Buffer[7] = 0xF;
            //}
#endif

            if (consumePartialPacket && consumeInputDirectly)
            {
                throw new InvalidOperationException($"MultiplexPackets cannot return both {nameof(consumePartialPacket)} and {nameof(consumeInputDirectly)}");
            }
        }
    }
}
