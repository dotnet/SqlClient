// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    partial class TdsParserStateObject
    {
        private Packet _partialPacket;
        internal Packet PartialPacket => _partialPacket;

        public void ProcessSniPacket(PacketHandle packet, uint error)
        {
            if (LocalAppContextSwitches.UseCompatibilityProcessSni)
            {
                ProcessSniPacketCompat(packet, error);
                return;
            }

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
                
                if (PartialPacketContainsCompletePacket())
                {
                    Packet partialPacket = _partialPacket;
                    SetBuffer(partialPacket.Buffer, 0, partialPacket.CurrentLength);
                    ClearPartialPacket();
                    getDataError = TdsEnums.SNI_SUCCESS;
                    usedPartialPacket = true;
                }
                else
                {
                    if (_inBytesRead != 0)
                    {
                        SetBuffer(new byte[_inBuff.Length], 0, 0);
                    }
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

                    bool recurse = false;
                    bool appended = false;
                    do
                    {
                        if (recurse && appended)
                        {
                            SetBuffer(new byte[_inBuff.Length], 0, 0);
                            appended = false;
                        }
                        MultiplexPackets(
                            _inBuff, _inBytesUsed, _inBytesRead,
                            PartialPacket,
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
                                _snapshot.AppendPacketData(PartialPacket.Buffer, PartialPacket.CurrentLength);
                                SetBuffer(new byte[_inBuff.Length], 0, 0);
                                appended = true;
                            }
                            else
                            {
                                SetBuffer(PartialPacket.Buffer, 0, PartialPacket.CurrentLength);
                                
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
                                // if we SetBuffer here to clear the packet buffer we will break the attention handling which relies
                                // on the attention containing packet remaining in the active buffer even if we're appending to the
                                // snapshot so we will have to use the appended variable to prevent the same buffer being added again
                                //// SetBuffer(new byte[_inBuff.Length], 0, 0);
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

        private void SetPartialPacket(Packet packet)
        {
            if (_partialPacket != null && packet != null)
            {
                throw new InvalidOperationException("partial packet cannot be non-null when setting to non=null");
            }
            _partialPacket = packet;
        }

        private void ClearPartialPacket()
        {
            Packet partialPacket = _partialPacket;
            _partialPacket = null;
#if DEBUG
            if (partialPacket != null)
            {
                partialPacket.Dispose();
            }
#endif 
        }

        // this check is used in two places that must be identical so it is
        // extracted into a method, do not inline this method
        internal bool PartialPacketContainsCompletePacket()
        {
            Packet partialPacket = _partialPacket;
            return partialPacket != null && partialPacket.ContainsCompletePacket;
        }

        private static void MultiplexPackets(
            byte[] dataBuffer, int dataOffset, int dataLength,
            Packet partialPacket,
            out int newDataOffset,
            out int newDataLength,
            out Packet remainderPacket,
            out bool consumeInputDirectly,
            out bool consumePartialPacket,
            out bool createdRemainderPacket,
            out bool recurse
        )
        {
            Debug.Assert(dataBuffer != null);

            ReadOnlySpan<byte> data = dataBuffer.AsSpan(dataOffset, dataLength);
            remainderPacket = null;
            consumeInputDirectly = false;
            consumePartialPacket = false;
            createdRemainderPacket = false;
            recurse = false;

            newDataLength = dataLength;
            newDataOffset = dataOffset;

            int bytesConsumed = 0;

            if (partialPacket != null)
            {
                if (!partialPacket.HasDataLength)
                {
                    // we need to get enough bytes to read the packet header
                    int headerBytesNeeded = Math.Max(0, TdsEnums.HEADER_LEN - partialPacket.CurrentLength);
                    if (headerBytesNeeded > 0)
                    {
                        int headerBytesAvailable = Math.Min(data.Length, headerBytesNeeded);

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
                        // the partial packet contains a complete packet of data and also contains
                        // data from a following packet

                        // the TDS spec requires that all packets be of the defined packet size apart from
                        // the last packet of a response. This means that is should not possible to have more than
                        // 2 packet fragments in a single buffer like this:
                        //  - first packet caused the partial
                        //  - second packet is the one we have just unpacked
                        //  - third packet is the extra data we have found
                        // however, due to the timing of cancellation it is possible that a response token stream
                        // has ended before an attention message response is sent leaving us with a short final
                        // packet and an additional short cancel packet following it

                        // this should only happen when the caller is trying to consume the partial packet
                        // and does not have new input data

                        int remainderLength = partialPacket.CurrentLength - partialPacket.RequiredLength;

                        partialPacket.CurrentLength = partialPacket.RequiredLength;

                        remainderPacket = new Packet
                        {
                            Buffer = new byte[dataBuffer.Length],
                            CurrentLength = remainderLength,
                        };
                        remainderPacket.SetCreatedBy(1);

                        ReadOnlySpan<byte> remainderSource = partialPacket.Buffer.AsSpan(TdsEnums.HEADER_LEN + partialPacket.DataLength, remainderLength);
                        Span<byte> remainderTarget = remainderPacket.Buffer.AsSpan(0, remainderLength);
                        remainderSource.CopyTo(remainderTarget);

                        createdRemainderPacket = true;

                        recurse = SetupRemainderPacket(remainderPacket);
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
                        // some data has been taken from the buffer and put into the partial
                        // packet buffer. We have data left so move the data we have to the
                        // start of the buffer so we can pass the buffer back as zero based
                        // to the caller avoiding offset calculations in the rest of this method
                        Buffer.BlockCopy(
                            dataBuffer, dataOffset + bytesConsumed, // from
                            dataBuffer, dataOffset, // to
                            dataLength - bytesConsumed // for
                        );
#if DEBUG
                        // for debugging purposes fill the removed data area with an easily
                        // recognisable pattern so we can see if it is misused
                        Span<byte> removed = dataBuffer.AsSpan(dataOffset + (dataLength - bytesConsumed), bytesConsumed);
                        removed.Fill(0xFF);
#endif

                        // then recreate the data span so that we're looking at the data
                        // that has been moved
                        data = dataBuffer.AsSpan(dataOffset, dataLength - bytesConsumed);
                    }

                    newDataLength = dataLength - bytesConsumed;
                }
            }

            // partial packet handling should not make decisions about consuming the input buffer
            Debug.Assert(!consumeInputDirectly);
            // partial packet handling may only create a remainder packet when it is trying to consume the partial packet and has no incoming data
            Debug.Assert(!createdRemainderPacket || data.Length == 0);

            if (data.Length > 0)
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
                                CurrentLength = data.Length
                            };
                            remainderPacket.SetCreatedBy(2);
                            createdRemainderPacket = true;
                            recurse = SetupRemainderPacket(remainderPacket);
                        }
                    }
                    else if (data.Length < TdsEnums.HEADER_LEN + packetDataLength)
                    {
                        // an incomplete packet so create a remainder packet to pass back
                        remainderPacket = new Packet
                        {
                            Buffer = dataBuffer,
                            DataLength = packetDataLength,
                            CurrentLength = data.Length
                        };
                        remainderPacket.SetCreatedBy(3);
                        createdRemainderPacket = true;
                        recurse = SetupRemainderPacket(remainderPacket);
                    }
                    else // implied: current length > required length
                    {
                        // more data than required so need to split it out, but we can't do that
                        // here so we need to tell the caller to take the remainder packet and then
                        // call this function again
                        if (consumePartialPacket)
                        {
                            // we are already telling the caller to consume the partial packet so we
                            // can't tell them it to also consume the data in the buffer directly
                            // so create a remainder packet and pass it back.
                            remainderPacket = new Packet
                            {
                                Buffer = new byte[dataBuffer.Length],
                                CurrentLength = data.Length
                            };
                            remainderPacket.SetCreatedBy(4);
                            ReadOnlySpan<byte> remainderSource = data;
                            Span<byte> remainderTarget = remainderPacket.Buffer.AsSpan(0, remainderPacket.CurrentLength);
                            remainderSource.CopyTo(remainderTarget);

                            createdRemainderPacket = true;

                            recurse = SetupRemainderPacket(remainderPacket);
                        }
                        else
                        {
                            newDataLength = TdsEnums.HEADER_LEN + packetDataLength;
                            int remainderLength = data.Length - (TdsEnums.HEADER_LEN + packetDataLength);
                            remainderPacket = new Packet
                            {
                                Buffer = new byte[dataBuffer.Length],
                                CurrentLength = remainderLength
                            };
                            remainderPacket.SetCreatedBy(5);

                            ReadOnlySpan<byte> remainderSource = data.Slice(TdsEnums.HEADER_LEN + packetDataLength);
                            Span<byte> remainderTarget = remainderPacket.Buffer.AsSpan(0, remainderLength);
                            remainderSource.CopyTo(remainderTarget);
#if DEBUG
                            // for debugging purposes fill the removed data area with an easily
                            // recognisable pattern so we can see if it is misused
                            Span<byte> removed = dataBuffer.AsSpan(TdsEnums.HEADER_LEN + packetDataLength, remainderLength);
                            removed.Fill(0xFF);
#endif
                            createdRemainderPacket = true;
                            recurse = SetupRemainderPacket(remainderPacket);

                            consumeInputDirectly = true;
                        }
                    }
                }
                else
                {
                    // either:
                    // 1) we took some data from the input to reconstruct the partial packet
                    // 2) there was less than a single packet header of data received
                    // in both cases we can't tell the caller to directly consume the packet
                    // in the input buffer, we need to construct a new remainder packet with
                    // the incomplete data and let the caller deal with it
                    remainderPacket = new Packet
                    {
                        Buffer = dataBuffer,
                        CurrentLength = data.Length
                    };
                    remainderPacket.SetCreatedBy(6);
                    createdRemainderPacket = true;
                    recurse = SetupRemainderPacket(remainderPacket);
                }
            }

            if (consumePartialPacket && consumeInputDirectly)
            {
                throw new InvalidOperationException($"MultiplexPackets cannot return both {nameof(consumePartialPacket)} and {nameof(consumeInputDirectly)}");
            }
        }

        private static bool SetupRemainderPacket(Packet packet)
        {
            Debug.Assert(packet != null);
            bool containsFullPacket = false;
            if (packet.HasHeader)
            {
                packet.DataLength = Packet.GetDataLengthFromHeader(packet);
                if (packet.HasDataLength && packet.CurrentLength >= packet.RequiredLength)
                {
                    containsFullPacket = true;
                }
            }

            return containsFullPacket;
        }


        public void ProcessSniPacketCompat(PacketHandle packet, uint error)
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
                
                #if NETFRAMEWORK
                uint getDataError = SniNativeWrapper.SniPacketGetData(packet, _inBuff, ref dataSize);
                #else
                uint getDataError = SniPacketGetData(packet, _inBuff, ref dataSize);
                #endif

                if (getDataError == TdsEnums.SNI_SUCCESS)
                {
                    if (_inBuff.Length < dataSize)
                    {
                        Debug.Assert(true, "Unexpected dataSize on Read");
                        throw SQL.InvalidInternalPacketSize(StringsHelper.GetString(Strings.SqlMisc_InvalidArraySizeMessage));
                    }

                    _lastSuccessfulIOTimer._value = DateTime.UtcNow.Ticks;
                    _inBytesRead = (int)dataSize;
                    _inBytesUsed = 0;

                    if (_snapshot != null)
                    {
                        _snapshot.AppendPacketData(_inBuff, _inBytesRead);
                        if (_snapshotStatus != SnapshotStatus.NotActive)
                        {
                            _snapshot.MoveNext();
#if DEBUG
                            _snapshot.AssertCurrent();
#endif
                        }
                    }

                    SniReadStatisticsAndTracing();
                    SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParser.ReadNetworkPacketAsyncCallback | INFO | ADV | State Object Id {0}, Packet read. In Buffer: {1}, In Bytes Read: {2}", ObjectID, _inBuff, _inBytesRead);

                    AssertValidState();
                }
                else
                {
                    throw SQL.ParsingError(ParsingErrorState.ProcessSniPacketFailed);
                }
            }
        }
    }
}
