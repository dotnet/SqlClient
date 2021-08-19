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
    /// SNI MARS connection. Multiple MARS streams will be overlaid on this connection.
    /// </summary>
    internal class SNIMarsConnection
    {
        private readonly object _sync;
        private readonly Guid _connectionId;
        private readonly Dictionary<int, SNIMarsHandle> _sessions;
        private SNIHandle _lowerHandle;
        private ushort _nextSessionId;

        /// <summary>
        /// Connection ID
        /// </summary>
        public Guid ConnectionId => _connectionId;

        public int ProtocolVersion => _lowerHandle.ProtocolVersion;

        public object DemuxerSync => _sync;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lowerHandle">Lower handle</param>
        public SNIMarsConnection(SNIHandle lowerHandle)
        {
            _sync = new object();
            _connectionId = Guid.NewGuid();
            _sessions = new Dictionary<int, SNIMarsHandle>();
            _demuxState = DemuxState.Header;
            _headerCount = 0;
            _headerBytes = new byte[SNISMUXHeader.HEADER_LENGTH];
            _nextSessionId = 0;
            _lowerHandle = lowerHandle;
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "Created MARS Session Id {0}", args0: ConnectionId);
            _lowerHandle.SetAsyncCallbacks(HandleReceiveComplete, HandleSendComplete);
        }

        public SNIMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            lock (DemuxerSync)
            {
                ushort sessionId = _nextSessionId++;
                SNIMarsHandle handle = new SNIMarsHandle(this, sessionId, callbackObject, async);
                _sessions.Add(sessionId, handle);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, SNI MARS Handle Id {1}, created new MARS Session {2}", args0: ConnectionId, args1: handle?.ConnectionId, args2: sessionId);
                return handle;
            }
        }

        /// <summary>
        /// Start receiving
        /// </summary>
        /// <returns></returns>
        public uint StartReceive()
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                SNIPacket packet = null;

                if (ReceiveAsync(ref packet) == TdsEnums.SNI_SUCCESS_IO_PENDING)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, Success IO pending.", args0: ConnectionId);
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Connection not usable.", args0: ConnectionId);
                return SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, 0, SNICommon.ConnNotUsableError, Strings.SNI_ERROR_19);
            }
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public uint Send(SNIPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                lock (DemuxerSync)
                {
                    return _lowerHandle.Send(packet);
                }
            }
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        public uint SendAsync(SNIPacket packet, SNIAsyncCallback callback)
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                lock (DemuxerSync)
                {
                    return _lowerHandle.SendAsync(packet, callback);
                }
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public uint ReceiveAsync(ref SNIPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                if (packet != null)
                {
                    ReturnPacket(packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, Packet {1} returned", args0: ConnectionId, args1: packet?._id);
#endif
                    packet = null;
                }

                lock (DemuxerSync)
                {
                    var response = _lowerHandle.ReceiveAsync(ref packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, Received new packet {1}", args0: ConnectionId, args1: packet?._id);
#endif
                    return response;
                }
            }
        }

        /// <summary>
        /// Check SNI handle connection
        /// </summary>
        /// <returns>SNI error status</returns>
        public uint CheckConnection()
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                lock (DemuxerSync)
                {
                    return _lowerHandle.CheckConnection();
                }
            }
        }

        /// <summary>
        /// Process a receive error
        /// </summary>
        public void HandleReceiveError(SNIPacket packet)
        {
            Debug.Assert(Monitor.IsEntered(DemuxerSync), "HandleReceiveError was called without demuxer lock being taken.");
            if (!Monitor.IsEntered(DemuxerSync))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, function was called without being locked.", args0: ConnectionId);
            }
            foreach (SNIMarsHandle handle in _sessions.Values)
            {
                if (packet.HasCompletionCallback)
                {
                    handle.HandleReceiveError(packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Packet {1} has Completion Callback", args0: ConnectionId, args1: packet?._id);
                }
                else
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Packet {1} does not have Completion Callback, error not handled.", args0: ConnectionId, args1: packet?._id);
#endif
                }
            }
            Debug.Assert(!packet.IsInvalid, "packet was returned by MarsConnection child, child sessions should not release the packet");
            ReturnPacket(packet);
        }

        /// <summary>
        /// Process a send completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="sniErrorCode">SNI error code</param>
        public void HandleSendComplete(SNIPacket packet, uint sniErrorCode)
        {
            packet.InvokeCompletionCallback(sniErrorCode);
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public uint EnableSsl(uint options)
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                return _lowerHandle.EnableSsl(options);
            }
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public void DisableSsl()
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                _lowerHandle.DisableSsl();
            }
        }

        public SNIPacket RentPacket(int headerSize, int dataSize) => _lowerHandle.RentPacket(headerSize, dataSize);

        public void ReturnPacket(SNIPacket packet) => _lowerHandle.ReturnPacket(packet);

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public void KillConnection()
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                _lowerHandle.KillConnection();
            }
        }
#endif

        private enum DemuxState : uint
        {
            Header = 1,
            Payload = 2,
            Dispatch = 3
        }

        private enum State : uint
        {
            Demux,
            HandleAck,
            HandleData,
            Receive,
            Finish,
            Error
        }


        // the following variables are used only inside HandleRecieveComplete
        // all access to these variables must be performed under lock(DemuxerSync) because
        // RecieveAsync can immediately return a new packet causing reentrant behaviour 
        // without the lock.
        private DemuxState _demuxState;

        private byte[] _headerBytes;
        private int _headerCount;
        private SNISMUXHeader _header;

        private int _payloadLength;
        private int _payloadCount;

        private SNIPacket _partial;

        public void HandleReceiveComplete(SNIPacket packet, uint sniErrorCode)
        {
            using (TrySNIEventScope.Create(nameof(SNIMarsConnection)))
            {
                if (sniErrorCode != TdsEnums.SNI_SUCCESS)
                {
                    lock (DemuxerSync)
                    {
                        HandleReceiveError(packet);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                        return;
                    }
                }

                State state = State.Demux;
                State nextState = State.Demux;
                
                SNISMUXHeader handleHeader = default;
                SNIMarsHandle handleSession = null;
                SNIPacket handlePacket = null;

                while (state != State.Error && state != State.Finish)
                {
                    switch (state)
                    {
                        case State.Demux:
                            lock (DemuxerSync)
                            {
                                switch (_demuxState)
                                {
                                    case DemuxState.Header:
                                        int taken = packet.TakeData(_headerBytes, _headerCount, SNISMUXHeader.HEADER_LENGTH - _headerCount);
                                        _headerCount += taken;
                                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, took {1} header bytes", args0: _lowerHandle?.ConnectionId, args1: packet.DataLeft, args2: taken);
                                        if (_headerCount == SNISMUXHeader.HEADER_LENGTH)
                                        {
                                            _header.Read(_headerBytes);
                                            _payloadLength = (int)_header.Length;
                                            _payloadCount = 0;
                                            _demuxState = DemuxState.Payload;
                                            state = State.Demux;
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, header complete, _payloadLength {1}", args0: _lowerHandle?.ConnectionId, args1: _payloadLength);
                                            goto case DemuxState.Payload;
                                        }
                                        else
                                        {
                                            state = State.Receive;
                                        }
                                        break;

                                    case DemuxState.Payload:
                                        if (packet.DataLeft == _payloadLength && _partial == null)
                                        {
                                            // if the data in the packet being processed is exactly and only the data that is going to sent
                                            // on to the parser then don't copy it to a new packet just forward the current packet once we've
                                            // fiddled the data pointer so that it skips the header data
                                            _partial = packet;
                                            packet = null;
                                            _partial.SetDataToRemainingContents();
                                            _demuxState = DemuxState.Dispatch;
                                            state = State.Demux;
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, forwarding packet contents", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                                            goto case DemuxState.Dispatch;
                                        }
                                        else
                                        {
                                            if (_partial == null)
                                            {
                                                _partial = RentPacket(headerSize: 0, dataSize: _payloadLength);
                                            }
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, reconstructing packet contents", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                                            int wanted = _payloadLength - _payloadCount;
                                            int transferred = SNIPacket.TransferData(packet, _partial, wanted);
                                            _payloadCount += transferred;
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, took {1} payload bytes", args0: _lowerHandle?.ConnectionId, args1: transferred);

                                            if (_payloadCount == _payloadLength)
                                            {
                                                // payload is complete so dispatch the current packet
                                                _demuxState = DemuxState.Dispatch;
                                                state = State.Receive;
                                                goto case DemuxState.Dispatch;
                                            }
                                            else if (packet.DataLeft == 0)
                                            {
                                                // no more data in the delivered packet so wait for a new one
                                                _demuxState = DemuxState.Payload;
                                                state = State.Receive;
                                            }
                                            else
                                            {
                                                // data left in the delivered packet so start the demux loop 
                                                // again and decode the next packet in the input
                                                _headerCount = 0;
                                                _demuxState = DemuxState.Header;
                                                state = State.Demux;
                                            }
                                        }

                                        break;

                                    case DemuxState.Dispatch:
                                        if (_sessions.TryGetValue(_header.SessionId, out SNIMarsHandle session))
                                        {
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, Current Session assigned to Session Id {1}", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                                            switch ((SNISMUXFlags)_header.Flags)
                                            {
                                                case SNISMUXFlags.SMUX_DATA:
                                                    handleSession = session;
                                                    session = null;
                                                    handleHeader = _header.Clone();
                                                    handlePacket = _partial;
                                                    _partial = null;
                                                    // move to the state for sending the data to the mars handle and setup
                                                    // the state that should be moved to after that operation has succeeded
                                                    state = State.HandleData;
                                                    if (packet != null && packet.DataLeft > 0)
                                                    {
                                                        nextState = State.Demux;
                                                    }
                                                    else
                                                    {
                                                        nextState = State.Receive;
                                                    }
                                                    break;

                                                case SNISMUXFlags.SMUX_ACK:
                                                    handleSession = session;
                                                    session = null;
                                                    handleHeader = _header.Clone();
                                                    ReturnPacket(_partial);
                                                    _partial = null;
                                                    // move to the state for sending the data to the mars handle and setup
                                                    // the state that should be moved to after that operation has succeeded
                                                    state = State.HandleAck;
                                                    if (packet != null && packet.DataLeft > 0)
                                                    {
                                                        nextState = State.Demux;
                                                    }
                                                    else
                                                    {
                                                        nextState = State.Receive;
                                                    }
                                                    break;

                                                case SNISMUXFlags.SMUX_FIN:
                                                    ReturnPacket(_partial);
                                                    _partial = null;
                                                    _sessions.Remove(_header.SessionId);
                                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "SMUX_FIN | MARS Session Id {0}, SMUX_FIN flag received, Current Header Session Id {1} removed", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                                                    break;

                                                default:
                                                    Debug.Fail("unknown smux packet flag");
                                                    break;
                                            }

                                            // a full packet has been decoded and queued for sending by setting the state or the 
                                            // handle it was sent to no longer exists and the handle has been dropped. Now reset the
                                            // demuxer state ready to recode another packet
                                            _header.Clear();
                                            _headerCount = 0;
                                            _demuxState = DemuxState.Header;

                                            // if the state is set to demux more data and there is no data left then change
                                            // the state to request more data
                                            if (state == State.Demux && (packet == null || packet.DataLeft == 0))
                                            {
                                                if (packet != null)
                                                {
                                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, run out of data , queuing receive", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                                                }
                                                state = State.Receive;
                                            }

                                        }
                                        else
                                        {
                                            SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.SMUX_PROV, 0, SNICommon.InvalidParameterError, string.Empty);
                                            HandleReceiveError(packet);
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "Current Header Session Id {0} not found, MARS Session Id {1} will be destroyed, New SNI error created: {2}", args0: _header.SessionId, args1: _lowerHandle?.ConnectionId, args2: sniErrorCode);
                                            packet = null;
                                            _lowerHandle.Dispose();
                                            _lowerHandle = null;
                                            state = State.Error;
                                        }
                                        break;
                                }
                            }
                            break;

                        case State.HandleAck:
                            Debug.Assert(handleSession != null, "dispatching ack to null SNIMarsHandle");
                            Debug.Assert(!Monitor.IsEntered(DemuxerSync), "do not dispatch ack to session handle while holding the demuxer lock");
                            try
                            {
                                handleSession.HandleAck(handleHeader.Highwater);
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "SMUX_ACK | MARS Session Id {0}, Current Session {1} handled ack", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                            }
                            catch (Exception e)
                            {
                                SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, SNICommon.InternalExceptionError, e);
                            }
                            finally
                            {
                                handleHeader = default;
                                handleSession = null;
                            }
                            state = nextState;
                            nextState = State.Finish;
                            break;

                        case State.HandleData:
                            Debug.Assert(handleSession != null, "dispatching data to null SNIMarsHandle");
                            Debug.Assert(handlePacket != null, "dispatching null data to SNIMarsHandle");
                            Debug.Assert(!Monitor.IsEntered(DemuxerSync), "do not dispatch data to session handle while holding the demuxer lock");
                            // do not ReturnPacket(handlePacket) the receiver is responsible for returning the packet 
                            // once it has been used because it can take sync and async paths from to the receiver and 
                            // only the reciever can make the decision on when it is completed and can be returned
                            try
                            {
                                handleSession.HandleReceiveComplete(handlePacket, handleHeader);
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "SMUX_DATA | MARS Session Id {0}, Current Session {1} completed receiving Data", args0: _lowerHandle?.ConnectionId, args1: _header.SessionId);
                            }
                            finally
                            {
                                handleHeader = default;
                                handleSession = null;
                                handlePacket = null;
                            }
                            state = nextState;
                            nextState = State.Finish;
                            break;

                        case State.Receive:
                            if (packet != null)
                            {
                                Debug.Assert(packet.DataLeft == 0, "loop exit with data remaining");
                                ReturnPacket(packet);
                                packet = null;
                            }

                            lock (DemuxerSync)
                            {
                                uint receiveResult = ReceiveAsync(ref packet);
                                if (receiveResult == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                {
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, SMUX DATA Header SNI Packet received with code {1}", args0: ConnectionId, args1: receiveResult);
                                    packet = null;
                                }
                                else
                                {
                                    HandleReceiveError(packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: receiveResult);
                                }
                            }
                            state = State.Finish;
                            break;
                    }
                }

            }
        }

    }
}
