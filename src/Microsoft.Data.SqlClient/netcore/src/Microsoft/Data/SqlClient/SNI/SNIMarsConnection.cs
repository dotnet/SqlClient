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
        private readonly Guid _connectionId;
        private readonly Dictionary<int, SNIMarsHandle> _sessions;
        private SNIHandle _lowerHandle;
        private ushort _nextSessionId;

        /// <summary>
        /// Connection ID
        /// </summary>
        public Guid ConnectionId => _connectionId;

        public int ProtocolVersion => _lowerHandle.ProtocolVersion;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lowerHandle">Lower handle</param>
        public SNIMarsConnection(SNIHandle lowerHandle)
        {
            _connectionId = Guid.NewGuid();
            _sessions = new Dictionary<int, SNIMarsHandle>();
            _state = State.Header;
            _headerCount = 0;
            _headerBytes = new byte[SNISMUXHeader.HEADER_LENGTH];
            _header = new SNISMUXHeader();
            _nextSessionId = 0;
            _lowerHandle = lowerHandle;
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "Created MARS Session Id {0}", args0: ConnectionId);
            _lowerHandle.SetAsyncCallbacks(HandleReceiveComplete, HandleSendComplete);
        }

        public SNIMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            lock (this)
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
                lock (this)
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
                lock (this)
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

                lock (this)
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
                lock (this)
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
            Debug.Assert(Monitor.IsEntered(this), "HandleReceiveError was called without being locked.");
            if (!Monitor.IsEntered(this))
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

        private enum State : uint
        {
            Header = 1,
            Payload = 2,
            Dispatch = 3
        }

        private enum LoopState : uint
        {
            Run,
            Recieve,
            Finish,
            Error
        }


        // the following variables are used only inside HandleRecieveComplete
        // all access to these variables must be performed under lock(this) because
        // RecieveAsync can immediately return a new packet causing reentrant behaviour 
        // without the lock.
        private State _state;

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
                    lock (this)
                    {
                        HandleReceiveError(packet);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                        return;
                    }
                }

                LoopState loopState = LoopState.Run;
                lock (this)
                {
                    while (loopState == LoopState.Run)
                    {
                        switch (_state)
                        {
                            case State.Header:
                                int taken = packet.TakeData(_headerBytes, _headerCount, SNISMUXHeader.HEADER_LENGTH - _headerCount);
                                _headerCount += taken;
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, took {1} header bytes", args0: _lowerHandle?.ConnectionId, args1: packet.DataLeft,args2: taken);
                                if (_headerCount == SNISMUXHeader.HEADER_LENGTH)
                                {
                                    _header.Read(_headerBytes);
                                    _payloadLength = (int)_header.length;
                                    _payloadCount = 0;
                                    _partial = RentPacket(headerSize: 0, dataSize: _payloadLength);
                                    _state = State.Payload;
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, header complete, _payloadLength {1}", args0: _lowerHandle?.ConnectionId, args1: _payloadLength);
                                    goto case State.Payload;
                                }
                                else
                                {
                                    loopState = LoopState.Recieve;
                                }
                                break;

                            case State.Payload:
                                if (packet.DataLeft == _payloadLength && _partial == null)
                                {
                                    // if the data in the packet being processed is exactly and only the data that is going to sent
                                    // on to the parser then don't copy it to a new packet just forward the current packet once we've
                                    // fiddled the data pointer so that it skips the header data when
                                    _partial = packet;
                                    packet = null;
                                    _partial.SetDataToRemainingContents();
                                    _state = State.Dispatch;
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, forwarding packet contents", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                    goto case State.Dispatch;
                                }
                                else
                                {
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, reconstructing packet contents", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                    int wanted = _payloadLength - _payloadCount;
                                    int transferred = SNIPacket.TransferData(packet, _partial, wanted);
                                    _payloadCount += transferred;
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, took {1} payload bytes", args0: _lowerHandle?.ConnectionId, args1: transferred);

                                    if (_payloadCount == _payloadLength)
                                    {
                                        // payload is complete so dispatch the current packet
                                        _state = State.Dispatch;
                                        goto case State.Dispatch;
                                    }
                                    else if (packet.DataLeft == 0)
                                    {
                                        // no more data in this packet so wait for a new one
                                        loopState = LoopState.Recieve;
                                    }
                                    else
                                    {
                                        // start the loop again and decode the next packet in the input
                                        _headerCount = 0;
                                        _state = State.Header;
                                    }
                                }

                                break;

                            case State.Dispatch:
                                if (_sessions.TryGetValue(_header.sessionId, out SNIMarsHandle session))
                                {
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, Current Session assigned to Session Id {1}", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                    switch ((SNISMUXFlags)_header.flags)
                                    {
                                        case SNISMUXFlags.SMUX_DATA:
                                            session.HandleReceiveComplete(_partial, _header);
                                            // do not return the _partial packet, the receiver is responsible for returning the 
                                            // packet once it has been used because it can take sync and async paths from here
                                            _partial = null;
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "SMUX_DATA | MARS Session Id {0}, Current Session {1} completed receiving Data", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                            break;

                                        case SNISMUXFlags.SMUX_ACK:
                                            ReturnPacket(_partial);
                                            _partial = null;
                                            try
                                            {
                                                session.HandleAck(_header.highwater);
                                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "SMUX_ACK | MARS Session Id {0}, Current Session {1} handled ack", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                            }
                                            catch (Exception e)
                                            {
                                                SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, SNICommon.InternalExceptionError, e);
                                            }
                                            break;

                                        case SNISMUXFlags.SMUX_FIN:
                                            ReturnPacket(_partial);
                                            _partial = null;
                                            _sessions.Remove(_header.sessionId);
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "SMUX_FIN | MARS Session Id {0}, SMUX_FIN flag received, Current Header Session Id {1} removed", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                            break;

                                        default:
                                            Debug.Fail("unknown smux packet flag");
                                            break;
                                    }

                                    // partial packet has been fully reconstructed and dispatched so change to header state 
                                    // to be ready to decode another smux header
                                    _headerCount = 0;
                                    _state = State.Header;

                                    if (packet==null || packet.DataLeft == 0)
                                    {
                                        if (packet != null)
                                        {
                                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, run out of data , queuing receive", args0: _lowerHandle?.ConnectionId, args1: _header?.sessionId);
                                        }
                                        // no more data in this packet or the packet has been forwarded so exit 
                                        // the loop and wait for a new packet to be recieved
                                        loopState = LoopState.Recieve;
                                    }
                                }
                                else
                                {
                                    SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.SMUX_PROV, 0, SNICommon.InvalidParameterError, string.Empty);
                                    HandleReceiveError(packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "Current Header Session Id {0} not found, MARS Session Id {1} will be destroyed, New SNI error created: {2}", args0: _header?.sessionId, args1: _lowerHandle?.ConnectionId, args2: sniErrorCode);
                                    packet = null;
                                    _lowerHandle.Dispose();
                                    _lowerHandle = null;
                                    loopState = LoopState.Error;
                                }
                                break;
                        }
                    }
                }

                if (loopState == LoopState.Recieve)
                {
                    if (packet != null)
                    {
                        Debug.Assert(packet.DataLeft == 0, "loop exit with data remaining");
                        ReturnPacket(packet);
                        packet = null;
                    }

                    if (ReceiveAsync(ref packet) == TdsEnums.SNI_SUCCESS_IO_PENDING)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.INFO, "MARS Session Id {0}, SMUX DATA Header SNI Packet received with code {1}", args0: ConnectionId, args1: TdsEnums.SNI_SUCCESS_IO_PENDING);
                        packet = null;
                    }
                    else
                    {
                        HandleReceiveError(packet);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                    }
                }
            }
        }

    }
}
