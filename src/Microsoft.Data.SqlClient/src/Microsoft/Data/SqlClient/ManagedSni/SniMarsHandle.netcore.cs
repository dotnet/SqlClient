// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// MARS handle
    /// </summary>
    internal sealed class SniMarsHandle : SniHandle
    {
        private const uint ACK_THRESHOLD = 2;

        private readonly SniMarsConnection _connection;
        private readonly uint _status = TdsEnums.SNI_UNINITIALIZED;
        private readonly Queue<SniPacket> _receivedPacketQueue = new Queue<SniPacket>();
        private readonly Queue<SniPacket> _sendPacketQueue = new Queue<SniPacket>();
        private readonly object _callbackObject;
        private readonly Guid _connectionId;
        private readonly ushort _sessionId;
        private readonly ManualResetEventSlim _packetEvent = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _ackEvent = new ManualResetEventSlim(false);
        private readonly SNISMUXHeader _currentHeader = new SNISMUXHeader();
        private readonly SniAsyncCallback _handleSendCompleteCallback;

        private uint _sendHighwater = 4;
        private int _asyncReceives = 0;
        private uint _receiveHighwater = 4;
        private uint _receiveHighwaterLastAck = 4;
        private uint _sequenceNumber;
        private SniError _connectionError;

        /// <summary>
        /// Connection ID
        /// </summary>
        public override Guid ConnectionId => _connectionId;

        /// <summary>
        /// Handle status
        /// </summary>
        public override uint Status => _status;

        public override int ReserveHeaderSize => SNISMUXHeader.HEADER_LENGTH;

        public override int ProtocolVersion => _connection.ProtocolVersion;

        /// <summary>
        /// Dispose object
        /// </summary>
        public override void Dispose()
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                try
                {
                    SendControlPacket(SniSmuxFlags.SMUX_FIN);
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Sent SMUX_FIN packet to terminate session.", args0: ConnectionId);
                }
                catch (Exception e)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.ERR, "MARS Session Id {0}, Internal exception error = {1}, Member Name={2}", args0: ConnectionId, args1: e?.Message, args2: e?.GetType()?.Name);
                    SniCommon.ReportSNIError(SniProviders.SMUX_PROV, SniCommon.InternalExceptionError, e);
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection">MARS connection</param>
        /// <param name="sessionId">MARS session ID</param>
        /// <param name="callbackObject">Callback object</param>
        /// <param name="async">true if connection is asynchronous</param>
        public SniMarsHandle(SniMarsConnection connection, ushort sessionId, object callbackObject, bool async)
        {
            _sessionId = sessionId;
            _connection = connection;
            _connectionId = connection.ConnectionId;
            _callbackObject = callbackObject;
            _handleSendCompleteCallback = HandleSendComplete;
            SendControlPacket(SniSmuxFlags.SMUX_SYN);
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Sent SMUX_SYN packet to start a new session, session Id {1}", args0: ConnectionId, args1: _sessionId);
            _status = TdsEnums.SNI_SUCCESS;
        }

        /// <summary>
        /// Send control packet
        /// </summary>
        /// <param name="flags">SMUX header flags</param>
        private void SendControlPacket(SniSmuxFlags flags)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                SniPacket packet = RentPacket(headerSize: SNISMUXHeader.HEADER_LENGTH, dataSize: 0);
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Packet rented {1}, packet dataLeft {2}", args0: ConnectionId, args1: packet?._id, args2: packet?.DataLeft);
#endif
                lock (this)
                {
                    SetupSMUXHeader(0, flags);
                    _currentHeader.Write(packet.GetHeaderBuffer(SNISMUXHeader.HEADER_LENGTH));
                    packet.SetHeaderActive();
                }

                _connection.Send(packet);
                ReturnPacket(packet);
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Packet returned {1}, packet dataLeft {2}", args0: ConnectionId, args1: packet?._id, args2: packet?.DataLeft);
                ;
#endif
            }
        }

        private void SetupSMUXHeader(int length, SniSmuxFlags flags)
        {
            Debug.Assert(Monitor.IsEntered(this), "must take lock on self before updating smux header");

            _currentHeader.SMID = 83;
            _currentHeader.flags = (byte)flags;
            _currentHeader.sessionId = _sessionId;
            _currentHeader.length = (uint)SNISMUXHeader.HEADER_LENGTH + (uint)length;
            _currentHeader.sequenceNumber = ((flags == SniSmuxFlags.SMUX_FIN) || (flags == SniSmuxFlags.SMUX_ACK)) ? _sequenceNumber - 1 : _sequenceNumber++;
            _currentHeader.highwater = _receiveHighwater;
            _receiveHighwaterLastAck = _currentHeader.highwater;
        }

        /// <summary>
        /// Generate a packet with SMUX header
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>The packet with the SMUx header set.</returns>
        private SniPacket SetPacketSMUXHeader(SniPacket packet)
        {
            Debug.Assert(packet.ReservedHeaderSize == SNISMUXHeader.HEADER_LENGTH, "mars handle attempting to smux packet without smux reservation");

            SetupSMUXHeader(packet.Length, SniSmuxFlags.SMUX_DATA);
            _currentHeader.Write(packet.GetHeaderBuffer(SNISMUXHeader.HEADER_LENGTH));
            packet.SetHeaderActive();
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Setting SMUX_DATA header in current header for packet {1}", args0: ConnectionId, args1: packet?._id);
#endif
            return packet;
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint Send(SniPacket packet)
        {
            Debug.Assert(packet.ReservedHeaderSize == SNISMUXHeader.HEADER_LENGTH, "mars handle attempting to send muxed packet without smux reservation in Send");
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                while (true)
                {
                    lock (this)
                    {
                        if (_sequenceNumber < _sendHighwater)
                        {
                            break;
                        }
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, Waiting for Acknowledgment event.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                    _ackEvent.Wait();

                    lock (this)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sendPacketQueue count found {1}, Acknowledgment event Reset", args0: ConnectionId, args1: _sendPacketQueue?.Count);
                        _ackEvent.Reset();
                    }
                }

                SniPacket muxedPacket = null;
                lock (this)
                {
                    muxedPacket = SetPacketSMUXHeader(packet);
                }
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, SMUX Packet is going to be sent.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                return _connection.Send(muxedPacket);
            }
        }

        /// <summary>
        /// Send packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        private uint InternalSendAsync(SniPacket packet)
        {
            Debug.Assert(packet.ReservedHeaderSize == SNISMUXHeader.HEADER_LENGTH, "mars handle attempting to send muxed packet without smux reservation in InternalSendAsync");
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                lock (this)
                {
                    if (_sequenceNumber >= _sendHighwater)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, SNI Queue is full", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                        return TdsEnums.SNI_QUEUE_FULL;
                    }

                    SniPacket muxedPacket = SetPacketSMUXHeader(packet);
                    muxedPacket.SetAsyncIOCompletionCallback(_handleSendCompleteCallback);
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, Sending packet", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                    return _connection.SendAsync(muxedPacket);
                }
            }
        }

        /// <summary>
        /// Send pending packets
        /// </summary>
        /// <returns>SNI error code</returns>
        private uint SendPendingPackets()
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                SniPacket packet = null;

                while (true)
                {
                    lock (this)
                    {
                        if (_sequenceNumber < _sendHighwater)
                        {
                            if (_sendPacketQueue.Count != 0)
                            {
                                packet = _sendPacketQueue.Peek();
                                uint result = InternalSendAsync(packet);

                                if (result != TdsEnums.SNI_SUCCESS && result != TdsEnums.SNI_SUCCESS_IO_PENDING)
                                {
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.ERR, "MARS Session Id {0}, InternalSendAsync result is not SNI_SUCCESS and is not SNI_SUCCESS_IO_PENDING", args0: ConnectionId);
                                    return result;
                                }

                                _sendPacketQueue.Dequeue();
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sendPacketQueue dequeued, count {1}", args0: ConnectionId, args1: _sendPacketQueue?.Count);
                                continue;
                            }
                            else
                            {
                                _ackEvent.Set();
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sendPacketQueue count found {1}, acknowledgment set", args0: ConnectionId, args1: _sendPacketQueue?.Count);
                            }
                        }

                        break;
                    }
                }

                return TdsEnums.SNI_SUCCESS;
            }
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint SendAsync(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                packet.SetAsyncIOCompletionCallback(_handleSendCompleteCallback);
                lock (this)
                {
                    _sendPacketQueue.Enqueue(packet);
                }

                SendPendingPackets();
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sendPacketQueue enqueued, count {1}", args0: ConnectionId, args1: _sendPacketQueue?.Count);

                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint ReceiveAsync(ref SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                lock (_receivedPacketQueue)
                {
                    int queueCount = _receivedPacketQueue.Count;

                    if (_connectionError != null)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.ERR, "MARS Session Id {0}, _asyncReceives {1}, _receiveHighwater {2}, _sendHighwater {3}, _receiveHighwaterLastAck {4}, _connectionError {5}", args0: ConnectionId, args1: _asyncReceives, args2: _receiveHighwater, args3: _sendHighwater, args4: _receiveHighwaterLastAck, args5: _connectionError);
                        return SniCommon.ReportSNIError(_connectionError);
                    }

                    if (queueCount == 0)
                    {
                        _asyncReceives++;
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, queueCount 0, _asyncReceives {1}, _receiveHighwater {2}, _sendHighwater {3}, _receiveHighwaterLastAck {4}", args0: ConnectionId, args1: _asyncReceives, args2: _receiveHighwater, args3: _sendHighwater, args4: _receiveHighwaterLastAck);

                        return TdsEnums.SNI_SUCCESS_IO_PENDING;
                    }

                    packet = _receivedPacketQueue.Dequeue();

                    if (queueCount == 1)
                    {
#if DEBUG
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, packet dequeued {1}, packet Owner {2}, packet refCount {3}, received Packet Queue count {4}", args0: ConnectionId, args1: packet?._id, args2: packet?._owner, args3: packet?._refCount, args4: _receivedPacketQueue?.Count);
#endif
                        _packetEvent.Reset();
                    }
                }

                lock (this)
                {
                    _receiveHighwater++;
                }

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _asyncReceives {1}, _receiveHighwater {2}, _sendHighwater {3}, _receiveHighwaterLastAck {4}, queueCount {5}", args0: ConnectionId, args1: _asyncReceives, args2: _receiveHighwater, args3: _sendHighwater, args4: _receiveHighwaterLastAck, args5: _receivedPacketQueue?.Count);
                SendAckIfNecessary();
                return TdsEnums.SNI_SUCCESS;
            }
        }

        /// <summary>
        /// Handle receive error
        /// </summary>
        public void HandleReceiveError(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                // SNIMarsHandle should only receive calls to this function from the SNIMarsConnection aggregator class
                // which should handle ownership of the packet because the individual mars handles are not aware of
                // each other and cannot know if they are the last one in the list and that it is safe to return the packet

                lock (_receivedPacketQueue)
                {
                    _connectionError = SniLoadHandle.SingletonInstance.LastError;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.ERR, "MARS Session Id {0}, _connectionError to be handled: {1}", args0: ConnectionId, args1: _connectionError);
                    _packetEvent.Set();
                }

                ((TdsParserStateObject)_callbackObject).ReadAsyncCallback(PacketHandle.FromManagedPacket(packet), 1);
            }
        }

        /// <summary>
        /// Handle send completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="sniErrorCode">SNI error code</param>
        public void HandleSendComplete(SniPacket packet, uint sniErrorCode)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                lock (this)
                {
                    Debug.Assert(_callbackObject != null);

                    ((TdsParserStateObject)_callbackObject).WriteAsyncCallback(PacketHandle.FromManagedPacket(packet), sniErrorCode);
                }
                _connection.ReturnPacket(packet);
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Returned Packet: {1}", args0: ConnectionId, args1: packet?._id);
#endif
            }
        }

        /// <summary>
        /// Handle SMUX acknowledgment
        /// </summary>
        /// <param name="highwater">Send highwater mark</param>
        public void HandleAck(uint highwater)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                lock (this)
                {
                    if (_sendHighwater != highwater)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, Setting _sendHighwater {1} to highwater {2} and send pending packets.", args0: ConnectionId, args1: _sendHighwater, args2: highwater);
                        _sendHighwater = highwater;
                        SendPendingPackets();
                    }
                }
            }
        }

        /// <summary>
        /// Handle receive completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="header">SMUX header</param>
        public void HandleReceiveComplete(SniPacket packet, SNISMUXHeader header)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                lock (this)
                {
                    if (_sendHighwater != header.highwater)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, header.highwater {1}, _sendHighwater {2}, Handle Ack with header.highwater", args0: ConnectionId, args1: header?.highwater, args2: _sendHighwater);
                        HandleAck(header.highwater);
                    }

                    lock (_receivedPacketQueue)
                    {
                        if (_asyncReceives == 0)
                        {
                            _receivedPacketQueue.Enqueue(packet);
                            _packetEvent.Set();
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, _receivedPacketQueue count {3}, packet event set", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater, args3: _receivedPacketQueue?.Count);
                            return;
                        }

                        _asyncReceives--;
                        Debug.Assert(_callbackObject != null);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, _asyncReceives {3}", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater, args3: _asyncReceives);

                        ((TdsParserStateObject)_callbackObject).ReadAsyncCallback(PacketHandle.FromManagedPacket(packet), 0);
                    }

                    _connection.ReturnPacket(packet);
                }

                lock (this)
                {
                    _receiveHighwater++;
                }
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _asyncReceives {1}, _receiveHighwater {2}, _sendHighwater {3}, _receiveHighwaterLastAck {4}", args0: ConnectionId, args1: _asyncReceives, args2: _receiveHighwater, args3: _sendHighwater, args4: _receiveHighwaterLastAck);
                SendAckIfNecessary();
            }
        }

        /// <summary>
        /// Send ACK if we've hit highwater threshold
        /// </summary>
        private void SendAckIfNecessary()
        {
            uint receiveHighwater;
            uint receiveHighwaterLastAck;

            lock (this)
            {
                receiveHighwater = _receiveHighwater;
                receiveHighwaterLastAck = _receiveHighwaterLastAck;
            }

            if (receiveHighwater - receiveHighwaterLastAck > ACK_THRESHOLD)
            {
                SendControlPacket(SniSmuxFlags.SMUX_ACK);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _asyncReceives {1}, _receiveHighwater {2}, _sendHighwater {3}, _receiveHighwaterLastAck {4} Sending acknowledgment ACK_THRESHOLD {5}", args0: ConnectionId, args1: _asyncReceives, args2: _receiveHighwater, args3: _sendHighwater, args4: _receiveHighwaterLastAck, args5: ACK_THRESHOLD);
            }
        }

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds</param>
        /// <returns>SNI error code</returns>
        public override uint Receive(out SniPacket packet, int timeoutInMilliseconds)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsHandle)))
            {
                packet = null;
                int queueCount;
                uint result = TdsEnums.SNI_SUCCESS_IO_PENDING;

                while (true)
                {
                    lock (_receivedPacketQueue)
                    {
                        if (_connectionError != null)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.ERR, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, _connectionError found: {3}.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater, args3: _connectionError);
                            return SniCommon.ReportSNIError(_connectionError);
                        }

                        queueCount = _receivedPacketQueue.Count;
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, W_receivedPacketQueue count {3}.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater, args3: queueCount);

                        if (queueCount > 0)
                        {
                            packet = _receivedPacketQueue.Dequeue();

                            if (queueCount == 1)
                            {
                                _packetEvent.Reset();
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, packet event reset, _receivedPacketQueue count 1.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                            }

                            result = TdsEnums.SNI_SUCCESS;
                        }
                    }

                    if (result == TdsEnums.SNI_SUCCESS)
                    {
                        lock (this)
                        {
                            _receiveHighwater++;
                        }

                        SendAckIfNecessary();
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, returning with result {3}.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater, args3: result);
                        return result;
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, Waiting for packet event.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                    if (!_packetEvent.Wait(timeoutInMilliseconds))
                    {
                        SniLoadHandle.SingletonInstance.LastError = new SniError(SniProviders.SMUX_PROV, 0, SniCommon.ConnTimeoutError, Strings.SNI_ERROR_11);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsHandle), EventType.INFO, "MARS Session Id {0}, _sequenceNumber {1}, _sendHighwater {2}, _packetEvent wait timed out.", args0: ConnectionId, args1: _sequenceNumber, args2: _sendHighwater);
                        return TdsEnums.SNI_WAIT_TIMEOUT;
                    }
                }
            }
        }

        /// <summary>
        /// Check SNI handle connection
        /// </summary>
        /// <returns>SNI error status</returns>
        public override uint CheckConnection()
        {
            return _connection.CheckConnection();
        }

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        public override void SetAsyncCallbacks(SniAsyncCallback receiveCallback, SniAsyncCallback sendCallback)
        {
        }

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public override void SetBufferSize(int bufferSize)
        {
        }

        public override uint EnableSsl(uint options) => _connection.EnableSsl(options);

        public override void DisableSsl() => _connection.DisableSsl();

        public override SniPacket RentPacket(int headerSize, int dataSize) => _connection.RentPacket(headerSize, dataSize);

        public override void ReturnPacket(SniPacket packet) => _connection.ReturnPacket(packet);


#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _connection.KillConnection();
        }
#endif
    }
}
