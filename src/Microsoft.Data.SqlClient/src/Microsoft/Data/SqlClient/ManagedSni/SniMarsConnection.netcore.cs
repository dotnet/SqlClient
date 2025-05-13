// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SNI MARS connection. Multiple MARS streams will be overlaid on this connection.
    /// </summary>
    internal class SniMarsConnection
    {
        private readonly Guid _connectionId;
        private readonly Dictionary<int, SniMarsHandle> _sessions;
        private readonly byte[] _headerBytes;
        private readonly SniSmuxHeader _currentHeader;
        private readonly object _sync;
        private SniHandle _lowerHandle;
        private ushort _nextSessionId;
        private int _currentHeaderByteCount;
        private int _dataBytesLeft;
        private SniPacket _currentPacket;

        /// <summary>
        /// Connection ID
        /// </summary>
        public Guid ConnectionId => _connectionId;

        public int ProtocolVersion => _lowerHandle.ProtocolVersion;

        internal object DemuxerSync => _sync;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lowerHandle">Lower handle</param>
        public SniMarsConnection(SniHandle lowerHandle)
        {
            _sync = new object();
            _connectionId = Guid.NewGuid();
            _sessions = new Dictionary<int, SniMarsHandle>();
            _headerBytes = new byte[SniSmuxHeader.HEADER_LENGTH];
            _currentHeader = new SniSmuxHeader();
            _nextSessionId = 0;
            _currentHeaderByteCount = 0;
            _dataBytesLeft = 0;
            _lowerHandle = lowerHandle;
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "Created MARS Session Id {0}", args0: ConnectionId);
            _lowerHandle.SetAsyncCallbacks(HandleReceiveComplete, HandleSendComplete);
        }

        public SniMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            lock (DemuxerSync)
            {
                ushort sessionId = _nextSessionId++;
                SniMarsHandle handle = new SniMarsHandle(this, sessionId, callbackObject, async);
                _sessions.Add(sessionId, handle);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, SNI MARS Handle Id {1}, created new MARS Session {2}", args0: ConnectionId, args1: handle?.ConnectionId, args2: sessionId);
                return handle;
            }
        }

        /// <summary>
        /// Start receiving
        /// </summary>
        /// <returns></returns>
        public uint StartReceive()
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                SniPacket packet = null;

                if (ReceiveAsync(ref packet) == TdsEnums.SNI_SUCCESS_IO_PENDING)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, Success IO pending.", args0: ConnectionId);
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, Connection not usable.", args0: ConnectionId);
                return SniCommon.ReportSNIError(SniProviders.SMUX_PROV, 0, SniCommon.ConnNotUsableError, Strings.SNI_ERROR_19);
            }
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public uint Send(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
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
        /// <returns>SNI error code</returns>
        public uint SendAsync(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                lock (DemuxerSync)
                {
                    return _lowerHandle.SendAsync(packet);
                }
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public uint ReceiveAsync(ref SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                if (packet != null)
                {
                    ReturnPacket(packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, Packet {1} returned", args0: ConnectionId, args1: packet?._id);
#endif
                    packet = null;
                }

                lock (DemuxerSync)
                {
                    var response = _lowerHandle.ReceiveAsync(ref packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, Received new packet {1}", args0: ConnectionId, args1: packet?._id);
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
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
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
        public void HandleReceiveError(SniPacket packet)
        {
            Debug.Assert(Monitor.IsEntered(this), "HandleReceiveError was called without being locked.");
            foreach (SniMarsHandle handle in _sessions.Values)
            {
                if (packet.HasAsyncIOCompletionCallback)
                {
                    handle.HandleReceiveError(packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, Packet {1} has Completion Callback", args0: ConnectionId, args1: packet?._id);
                }
                else
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, Packet {1} does not have Completion Callback, error not handled.", args0: ConnectionId, args1: packet?._id);
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
        public void HandleSendComplete(SniPacket packet, uint sniErrorCode)
        {
            packet.InvokeAsyncIOCompletionCallback(sniErrorCode);
        }

        /// <summary>
        /// Process a receive completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="sniErrorCode">SNI error code</param>
        public void HandleReceiveComplete(SniPacket packet, uint sniErrorCode)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                SniSmuxHeader currentHeader = null;
                SniPacket currentPacket = null;
                SniMarsHandle currentSession = null;

                if (sniErrorCode != TdsEnums.SNI_SUCCESS)
                {
                    lock (DemuxerSync)
                    {
                        HandleReceiveError(packet);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                        return;
                    }
                }

                while (true)
                {
                    lock (DemuxerSync)
                    {
                        if (_currentHeaderByteCount != SniSmuxHeader.HEADER_LENGTH)
                        {
                            currentHeader = null;
                            currentPacket = null;
                            currentSession = null;

                            while (_currentHeaderByteCount != SniSmuxHeader.HEADER_LENGTH)
                            {
                                int bytesTaken = packet.TakeData(_headerBytes, _currentHeaderByteCount, SniSmuxHeader.HEADER_LENGTH - _currentHeaderByteCount);
                                _currentHeaderByteCount += bytesTaken;

                                if (bytesTaken == 0)
                                {
                                    sniErrorCode = ReceiveAsync(ref packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, Non-SMUX Header SNI Packet received with code {1}", args0: ConnectionId, args1: sniErrorCode);

                                    if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                    {
                                        return;
                                    }

                                    HandleReceiveError(packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                                    return;
                                }
                            }

                            _currentHeader.Read(_headerBytes);
                            _dataBytesLeft = (int)_currentHeader.length;
                            _currentPacket = _lowerHandle.RentPacket(headerSize: 0, dataSize: (int)_currentHeader.length);
#if DEBUG
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, _dataBytesLeft {1}, _currentPacket {2}, Reading data of length: _currentHeader.length {3}", args0: _lowerHandle?.ConnectionId, args1: _dataBytesLeft, args2: currentPacket?._id, args3: _currentHeader?.length);
#endif
                        }

                        currentHeader = _currentHeader;
                        currentPacket = _currentPacket;

                        if (_currentHeader.flags == (byte)SniSmuxFlags.SMUX_DATA)
                        {
                            if (_dataBytesLeft > 0)
                            {
                                int length = packet.TakeData(_currentPacket, _dataBytesLeft);
                                _dataBytesLeft -= length;

                                if (_dataBytesLeft > 0)
                                {
                                    sniErrorCode = ReceiveAsync(ref packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, SMUX DATA Header SNI Packet received with code {1}, _dataBytesLeft {2}", args0: ConnectionId, args1: sniErrorCode, args2: _dataBytesLeft);

                                    if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                    {
                                        return;
                                    }

                                    HandleReceiveError(packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                                    return;
                                }
                            }
                        }

                        _currentHeaderByteCount = 0;

                        if (!_sessions.ContainsKey(_currentHeader.sessionId))
                        {
                            SniLoadHandle.SingletonInstance.LastError = new SniError(SniProviders.SMUX_PROV, 0, SniCommon.InvalidParameterError, Strings.SNI_ERROR_5);
                            HandleReceiveError(packet);
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "Current Header Session Id {0} not found, MARS Session Id {1} will be destroyed, New SNI error created: {2}", args0: _currentHeader?.sessionId, args1: _lowerHandle?.ConnectionId, args2: sniErrorCode);
                            _lowerHandle.Dispose();
                            _lowerHandle = null;
                            return;
                        }

                        if (_currentHeader.flags == (byte)SniSmuxFlags.SMUX_FIN)
                        {
                            _sessions.Remove(_currentHeader.sessionId);
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "SMUX_FIN | MARS Session Id {0}, SMUX_FIN flag received, Current Header Session Id {1} removed", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                        }
                        else
                        {
                            currentSession = _sessions[_currentHeader.sessionId];
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "MARS Session Id {0}, Current Session assigned to Session Id {1}", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                        }
                    }

                    if (currentHeader.flags == (byte)SniSmuxFlags.SMUX_DATA)
                    {
                        currentSession.HandleReceiveComplete(currentPacket, currentHeader);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "SMUX_DATA | MARS Session Id {0}, Current Session {1} completed receiving Data", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                    }

                    if (_currentHeader.flags == (byte)SniSmuxFlags.SMUX_ACK)
                    {
                        try
                        {
                            currentSession.HandleAck(currentHeader.highwater);
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "SMUX_ACK | MARS Session Id {0}, Current Session {1} handled ack", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                        }
                        catch (Exception e)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "SMUX_ACK | MARS Session Id {0}, Exception occurred: {2}", args0: _currentHeader?.sessionId, args1: e?.Message);
                            SniCommon.ReportSNIError(SniProviders.SMUX_PROV, SniCommon.InternalExceptionError, e);
                        }
#if DEBUG
                        Debug.Assert(_currentPacket == currentPacket, "current and _current are not the same");
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.INFO, "SMUX_ACK | MARS Session Id {0}, Current Packet {1} returned", args0: _lowerHandle?.ConnectionId, args1: currentPacket?._id);
#endif
                        ReturnPacket(currentPacket);
                        currentPacket = null;
                        _currentPacket = null;
                    }

                    lock (DemuxerSync)
                    {
                        if (packet.DataLeft == 0)
                        {
                            sniErrorCode = ReceiveAsync(ref packet);

                            if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                            {
                                return;
                            }

                            HandleReceiveError(packet);
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniMarsConnection), EventType.ERR, "MARS Session Id {0}, packet.DataLeft 0, SNI error {2}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public uint EnableSsl(uint options)
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                return _lowerHandle.EnableSsl(options);
            }
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public void DisableSsl()
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                _lowerHandle.DisableSsl();
            }
        }

        public SniPacket RentPacket(int headerSize, int dataSize)
        {
            return _lowerHandle.RentPacket(headerSize, dataSize);
        }

        public void ReturnPacket(SniPacket packet)
        {
            _lowerHandle.ReturnPacket(packet);
        }

        #if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public void KillConnection()
        {
            using (TrySNIEventScope.Create(nameof(SniMarsConnection)))
            {
                _lowerHandle.KillConnection();
            }
        }
        #endif
    }
}

#endif
