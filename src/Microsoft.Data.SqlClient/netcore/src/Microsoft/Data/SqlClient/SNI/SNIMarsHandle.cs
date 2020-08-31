// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// MARS handle
    /// </summary>
    internal sealed class SNIMarsHandle : SNIHandle
    {
        private const uint ACK_THRESHOLD = 2;

        private readonly SNIMarsConnection _connection;
        private readonly uint _status = TdsEnums.SNI_UNINITIALIZED;
        private readonly Queue<SNIPacket> _receivedPacketQueue = new Queue<SNIPacket>();
        private readonly Queue<SNIMarsQueuedPacket> _sendPacketQueue = new Queue<SNIMarsQueuedPacket>();
        private readonly object _callbackObject;
        private readonly Guid _connectionId = Guid.NewGuid();
        private readonly ushort _sessionId;
        private readonly ManualResetEventSlim _packetEvent = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _ackEvent = new ManualResetEventSlim(false);
        private readonly SNISMUXHeader _currentHeader = new SNISMUXHeader();
        private readonly SNIAsyncCallback _handleSendCompleteCallback;

        private uint _sendHighwater = 4;
        private int _asyncReceives = 0;
        private uint _receiveHighwater = 4;
        private uint _receiveHighwaterLastAck = 4;
        private uint _sequenceNumber;
        private SNIError _connectionError;

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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.Dispose |SNI|INFO|SCOPE>");
            try
            {
                SendControlPacket(SNISMUXFlags.SMUX_FIN);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsHandle.Dispose |SNI|ERR> internal exception error = {0}, Member Name={1}", e.Message, e.GetType().Name);
                SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, SNICommon.InternalExceptionError, e);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection">MARS connection</param>
        /// <param name="sessionId">MARS session ID</param>
        /// <param name="callbackObject">Callback object</param>
        /// <param name="async">true if connection is asynchronous</param>
        public SNIMarsHandle(SNIMarsConnection connection, ushort sessionId, object callbackObject, bool async)
        {
            _sessionId = sessionId;
            _connection = connection;
            _callbackObject = callbackObject;
            _handleSendCompleteCallback = HandleSendComplete;
            SendControlPacket(SNISMUXFlags.SMUX_SYN);
            _status = TdsEnums.SNI_SUCCESS;
        }

        /// <summary>
        /// Send control packet
        /// </summary>
        /// <param name="flags">SMUX header flags</param>
        private void SendControlPacket(SNISMUXFlags flags)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.SendControlPacket |SNI|INFO|SCOPE>");
            try
            {
                SNIPacket packet = RentPacket(headerSize: SNISMUXHeader.HEADER_LENGTH, dataSize: 0);

                lock (this)
                {
                    SetupSMUXHeader(0, flags);
                    _currentHeader.Write(packet.GetHeaderBuffer(SNISMUXHeader.HEADER_LENGTH));
                    packet.SetHeaderActive();
                }

                _connection.Send(packet);
                ReturnPacket(packet);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        private void SetupSMUXHeader(int length, SNISMUXFlags flags)
        {
            Debug.Assert(Monitor.IsEntered(this), "must take lock on self before updating mux header");

            _currentHeader.SMID = 83;
            _currentHeader.flags = (byte)flags;
            _currentHeader.sessionId = _sessionId;
            _currentHeader.length = (uint)SNISMUXHeader.HEADER_LENGTH + (uint)length;
            _currentHeader.sequenceNumber = ((flags == SNISMUXFlags.SMUX_FIN) || (flags == SNISMUXFlags.SMUX_ACK)) ? _sequenceNumber - 1 : _sequenceNumber++;
            _currentHeader.highwater = _receiveHighwater;
            _receiveHighwaterLastAck = _currentHeader.highwater;
        }

        /// <summary>
        /// Generate a packet with SMUX header
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>The packet with the SMUx header set.</returns>
        private SNIPacket SetPacketSMUXHeader(SNIPacket packet)
        {
            Debug.Assert(packet.ReservedHeaderSize == SNISMUXHeader.HEADER_LENGTH, "mars handle attempting to mux packet without mux reservation");

            SetupSMUXHeader(packet.Length, SNISMUXFlags.SMUX_DATA);
            _currentHeader.Write(packet.GetHeaderBuffer(SNISMUXHeader.HEADER_LENGTH));
            packet.SetHeaderActive();

            return packet;
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint Send(SNIPacket packet)
        {
            Debug.Assert(packet.ReservedHeaderSize == SNISMUXHeader.HEADER_LENGTH, "mars handle attempting to send muxed packet without mux reservation in Send");
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.Send |SNI|INFO|SCOPE>");
            try
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

                    _ackEvent.Wait();

                    lock (this)
                    {
                        _ackEvent.Reset();
                    }
                }

                SNIPacket muxedPacket = null;
                lock (this)
                {
                    muxedPacket = SetPacketSMUXHeader(packet);
                }
                return _connection.Send(muxedPacket);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Send packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        private uint InternalSendAsync(SNIPacket packet, SNIAsyncCallback callback)
        {
            Debug.Assert(packet.ReservedHeaderSize == SNISMUXHeader.HEADER_LENGTH, "mars handle attempting to send muxed packet without mux reservation in InternalSendAsync");
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.InternalSendAsync |SNI|INFO|SCOPE>");
            try
            {
                lock (this)
                {
                    if (_sequenceNumber >= _sendHighwater)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsHandle.InternalSendAsync |SNI|INFO|> SNI Queue is full");
                        return TdsEnums.SNI_QUEUE_FULL;
                    }

                    SNIPacket muxedPacket = SetPacketSMUXHeader(packet);
                    muxedPacket.SetCompletionCallback(callback ?? HandleSendComplete);
                    return _connection.SendAsync(muxedPacket, callback);
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Send pending packets
        /// </summary>
        /// <returns>SNI error code</returns>
        private uint SendPendingPackets()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.SendPendingPackets |SNI|INFO|SCOPE>");
            SNIMarsQueuedPacket packet = null;
            try
            {
                while (true)
                {
                    lock (this)
                    {
                        if (_sequenceNumber < _sendHighwater)
                        {
                            if (_sendPacketQueue.Count != 0)
                            {
                                packet = _sendPacketQueue.Peek();
                                uint result = InternalSendAsync(packet.Packet, packet.Callback);

                                if (result != TdsEnums.SNI_SUCCESS && result != TdsEnums.SNI_SUCCESS_IO_PENDING)
                                {
                                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsHandle.SendPendingPackets |SNI|ERR> InternalSendAsync result is not SNI_SUCCESS and is not SNI_SUCCESS_IO_PENDING");
                                    return result;
                                }

                                _sendPacketQueue.Dequeue();
                                continue;
                            }
                            else
                            {
                                _ackEvent.Set();
                            }
                        }

                        break;
                    }
                }

                return TdsEnums.SNI_SUCCESS;
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        public override uint SendAsync(SNIPacket packet, SNIAsyncCallback callback = null)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.SendAsync |SNI|INFO|SCOPE>");
            try
            {
                lock (this)
                {
                    _sendPacketQueue.Enqueue(new SNIMarsQueuedPacket(packet, callback ?? _handleSendCompleteCallback));
                }

                SendPendingPackets();
                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint ReceiveAsync(ref SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.SendAsync |SNI|INFO|SCOPE>");
            try
            {
                lock (_receivedPacketQueue)
                {
                    int queueCount = _receivedPacketQueue.Count;

                    if (_connectionError != null)
                    {
                        return SNICommon.ReportSNIError(_connectionError);
                    }

                    if (queueCount == 0)
                    {
                        _asyncReceives++;
                        return TdsEnums.SNI_SUCCESS_IO_PENDING;
                    }

                    packet = _receivedPacketQueue.Dequeue();

                    if (queueCount == 1)
                    {
                        _packetEvent.Reset();
                    }
                }

                lock (this)
                {
                    _receiveHighwater++;
                }

                SendAckIfNecessary();
                return TdsEnums.SNI_SUCCESS;
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Handle receive error
        /// </summary>
        public void HandleReceiveError(SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.HandleReceiveError |SNI|INFO|SCOPE>");
            try
            {
                // SNIMarsHandle should only receive calls to this function from the SNIMarsConnection aggregator class
                // which should handle ownership of the packet because the individual mars handles are not aware of 
                // each other and cannot know if they are the last one in the list and that it is safe to return the packet

                lock (_receivedPacketQueue)
                {
                    _connectionError = SNILoadHandle.SingletonInstance.LastError;
                    _packetEvent.Set();
                }

                ((TdsParserStateObject)_callbackObject).ReadAsyncCallback(PacketHandle.FromManagedPacket(packet), 1);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Handle send completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="sniErrorCode">SNI error code</param>
        public void HandleSendComplete(SNIPacket packet, uint sniErrorCode)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.HandleSendComplete |SNI|INFO|SCOPE>");
            try
            {
                lock (this)
                {
                    Debug.Assert(_callbackObject != null);

                    ((TdsParserStateObject)_callbackObject).WriteAsyncCallback(PacketHandle.FromManagedPacket(packet), sniErrorCode);
                }
                _connection.ReturnPacket(packet);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Handle SMUX acknowledgement
        /// </summary>
        /// <param name="highwater">Send highwater mark</param>
        public void HandleAck(uint highwater)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.HandleAck |SNI|INFO|SCOPE>");
            try
            {
                lock (this)
                {
                    if (_sendHighwater != highwater)
                    {
                        _sendHighwater = highwater;
                        SendPendingPackets();
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Handle receive completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="header">SMUX header</param>
        public void HandleReceiveComplete(SNIPacket packet, SNISMUXHeader header)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.HandleReceiveComplete |SNI|INFO|SCOPE>");
            try
            {
                lock (this)
                {
                    if (_sendHighwater != header.highwater)
                    {
                        HandleAck(header.highwater);
                    }

                    lock (_receivedPacketQueue)
                    {
                        if (_asyncReceives == 0)
                        {
                            _receivedPacketQueue.Enqueue(packet);
                            _packetEvent.Set();
                            return;
                        }

                        _asyncReceives--;
                        Debug.Assert(_callbackObject != null);

                        ((TdsParserStateObject)_callbackObject).ReadAsyncCallback(PacketHandle.FromManagedPacket(packet), 0);
                    }

                    _connection.ReturnPacket(packet);
                }

                lock (this)
                {
                    _receiveHighwater++;
                }

                SendAckIfNecessary();
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
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
                SendControlPacket(SNISMUXFlags.SMUX_ACK);
            }
        }

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds</param>
        /// <returns>SNI error code</returns>
        public override uint Receive(out SNIPacket packet, int timeoutInMilliseconds)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsHandle.Receive |SNI|INFO|SCOPE>");
            try
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
                            return SNICommon.ReportSNIError(_connectionError);
                        }

                        queueCount = _receivedPacketQueue.Count;

                        if (queueCount > 0)
                        {
                            packet = _receivedPacketQueue.Dequeue();

                            if (queueCount == 1)
                            {
                                _packetEvent.Reset();
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
                        return result;
                    }

                    if (!_packetEvent.Wait(timeoutInMilliseconds))
                    {
                        SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.SMUX_PROV, 0, SNICommon.ConnTimeoutError, string.Empty);
                        return TdsEnums.SNI_WAIT_TIMEOUT;
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
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
        public override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
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

        public override SNIPacket RentPacket(int headerSize, int dataSize) => _connection.RentPacket(headerSize, dataSize);

        public override void ReturnPacket(SNIPacket packet) => _connection.ReturnPacket(packet);


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
