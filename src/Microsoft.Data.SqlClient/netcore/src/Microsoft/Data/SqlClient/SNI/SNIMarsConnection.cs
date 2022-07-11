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
        private const string s_className = nameof(SNIMarsConnection);

        private readonly Guid _connectionId = Guid.NewGuid();
        private readonly Dictionary<int, SNIMarsHandle> _sessions = new Dictionary<int, SNIMarsHandle>();
        private readonly byte[] _headerBytes = new byte[SNISMUXHeader.HEADER_LENGTH];
        private readonly SNISMUXHeader _currentHeader = new SNISMUXHeader();
        private SNIHandle _lowerHandle;
        private ushort _nextSessionId = 0;
        private int _currentHeaderByteCount = 0;
        private int _dataBytesLeft = 0;
        private SNIPacket _currentPacket;

        /// <summary>
        /// Connection ID
        /// </summary>
        public Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        public int ProtocolVersion => _lowerHandle.ProtocolVersion;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lowerHandle">Lower handle</param>
        public SNIMarsConnection(SNIHandle lowerHandle)
        {
            _lowerHandle = lowerHandle;
            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "Created MARS Session Id {0}", args0: ConnectionId);
            _lowerHandle.SetAsyncCallbacks(HandleReceiveComplete, HandleSendComplete);
        }

        public SNIMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            lock (this)
            {
                ushort sessionId = _nextSessionId++;
                SNIMarsHandle handle = new SNIMarsHandle(this, sessionId, callbackObject, async);
                _sessions.Add(sessionId, handle);
                SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, SNI MARS Handle Id {1}, created new MARS Session {2}", args0: ConnectionId, args1: handle?.ConnectionId, args2: sessionId);
                return handle;
            }
        }

        /// <summary>
        /// Start receiving
        /// </summary>
        /// <returns></returns>
        public uint StartReceive()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                SNIPacket packet = null;

                if (ReceiveAsync(ref packet) == TdsEnums.SNI_SUCCESS_IO_PENDING)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, Success IO pending.", args0: ConnectionId);
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, Connection not usable.", args0: ConnectionId);
                return SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, 0, SNICommon.ConnNotUsableError, Strings.SNI_ERROR_19);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public uint Send(SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                lock (this)
                {
                    return _lowerHandle.Send(packet);
                }
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
        /// <returns>SNI error code</returns>
        public uint SendAsync(SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                lock (this)
                {
                    return _lowerHandle.SendAsync(packet);
                }
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
        public uint ReceiveAsync(ref SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                if (packet != null)
                {
                    ReturnPacket(packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, Packet {1} returned", args0: ConnectionId, args1: packet?._id);
#endif
                    packet = null;
                }

                lock (this)
                {
                    var response = _lowerHandle.ReceiveAsync(ref packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, Received new packet {1}", args0: ConnectionId, args1: packet?._id);
#endif
                    return response;
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
        public uint CheckConnection()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                lock (this)
                {
                    return _lowerHandle.CheckConnection();
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Process a receive error
        /// </summary>
        public void HandleReceiveError(SNIPacket packet)
        {
            Debug.Assert(Monitor.IsEntered(this), "HandleReceiveError was called without being locked.");
            foreach (SNIMarsHandle handle in _sessions.Values)
            {
                if (packet.HasAsyncIOCompletionCallback)
                {
                    handle.HandleReceiveError(packet);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, Packet {1} has Completion Callback", args0: ConnectionId, args1: packet?._id);
                }
                else
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, Packet {1} does not have Completion Callback, error not handled.", args0: ConnectionId, args1: packet?._id);
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
            packet.InvokeAsyncIOCompletionCallback(sniErrorCode);
        }

        /// <summary>
        /// Process a receive completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="sniErrorCode">SNI error code</param>
        public void HandleReceiveComplete(SNIPacket packet, uint sniErrorCode)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                SNISMUXHeader currentHeader = null;
                SNIPacket currentPacket = null;
                SNIMarsHandle currentSession = null;

                if (sniErrorCode != TdsEnums.SNI_SUCCESS)
                {
                    lock (this)
                    {
                        HandleReceiveError(packet);
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                        return;
                    }
                }

                while (true)
                {
                    lock (this)
                    {
                        if (_currentHeaderByteCount != SNISMUXHeader.HEADER_LENGTH)
                        {
                            currentHeader = null;
                            currentPacket = null;
                            currentSession = null;

                            while (_currentHeaderByteCount != SNISMUXHeader.HEADER_LENGTH)
                            {
                                int bytesTaken = packet.TakeData(_headerBytes, _currentHeaderByteCount, SNISMUXHeader.HEADER_LENGTH - _currentHeaderByteCount);
                                _currentHeaderByteCount += bytesTaken;

                                if (bytesTaken == 0)
                                {
                                    sniErrorCode = ReceiveAsync(ref packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, Non-SMUX Header SNI Packet received with code {1}", args0: ConnectionId, args1: sniErrorCode);

                                    if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                    {
                                        return;
                                    }

                                    HandleReceiveError(packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                                    return;
                                }
                            }

                            _currentHeader.Read(_headerBytes);
                            _dataBytesLeft = (int)_currentHeader.length;
                            _currentPacket = _lowerHandle.RentPacket(headerSize: 0, dataSize: (int)_currentHeader.length);
#if DEBUG
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, _dataBytesLeft {1}, _currentPacket {2}, Reading data of length: _currentHeader.length {3}", args0: _lowerHandle?.ConnectionId, args1: _dataBytesLeft, args2: currentPacket?._id, args3: _currentHeader?.length);
#endif
                        }

                        currentHeader = _currentHeader;
                        currentPacket = _currentPacket;

                        if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_DATA)
                        {
                            if (_dataBytesLeft > 0)
                            {
                                int length = packet.TakeData(_currentPacket, _dataBytesLeft);
                                _dataBytesLeft -= length;

                                if (_dataBytesLeft > 0)
                                {
                                    sniErrorCode = ReceiveAsync(ref packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, SMUX DATA Header SNI Packet received with code {1}, _dataBytesLeft {2}", args0: ConnectionId, args1: sniErrorCode, args2: _dataBytesLeft);

                                    if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                    {
                                        return;
                                    }

                                    HandleReceiveError(packet);
                                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, Handled receive error code: {1}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                                    return;
                                }
                            }
                        }

                        _currentHeaderByteCount = 0;

                        if (!_sessions.ContainsKey(_currentHeader.sessionId))
                        {
                            SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.SMUX_PROV, 0, SNICommon.InvalidParameterError, Strings.SNI_ERROR_5);
                            HandleReceiveError(packet);
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Current Header Session Id {0} not found, MARS Session Id {1} will be destroyed, New SNI error created: {2}", args0: _currentHeader?.sessionId, args1: _lowerHandle?.ConnectionId, args2: sniErrorCode);
                            _lowerHandle.Dispose();
                            _lowerHandle = null;
                            return;
                        }

                        if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_FIN)
                        {
                            _sessions.Remove(_currentHeader.sessionId);
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "SMUX_FIN | MARS Session Id {0}, SMUX_FIN flag received, Current Header Session Id {1} removed", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                        }
                        else
                        {
                            currentSession = _sessions[_currentHeader.sessionId];
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "MARS Session Id {0}, Current Session assigned to Session Id {1}", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                        }
                    }

                    if (currentHeader.flags == (byte)SNISMUXFlags.SMUX_DATA)
                    {
                        currentSession.HandleReceiveComplete(currentPacket, currentHeader);
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "SMUX_DATA | MARS Session Id {0}, Current Session {1} completed receiving Data", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                    }

                    if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_ACK)
                    {
                        try
                        {
                            currentSession.HandleAck(currentHeader.highwater);
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "SMUX_ACK | MARS Session Id {0}, Current Session {1} handled ack", args0: _lowerHandle?.ConnectionId, args1: _currentHeader?.sessionId);
                        }
                        catch (Exception e)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "SMUX_ACK | MARS Session Id {0}, Exception occurred: {2}", args0: _currentHeader?.sessionId, args1: e?.Message);
                            SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, SNICommon.InternalExceptionError, e);
                        }
#if DEBUG
                        Debug.Assert(_currentPacket == currentPacket, "current and _current are not the same");
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "SMUX_ACK | MARS Session Id {0}, Current Packet {1} returned", args0: _lowerHandle?.ConnectionId, args1: currentPacket?._id);
#endif
                        ReturnPacket(currentPacket);
                        currentPacket = null;
                        _currentPacket = null;
                    }

                    lock (this)
                    {
                        if (packet.DataLeft == 0)
                        {
                            sniErrorCode = ReceiveAsync(ref packet);

                            if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                            {
                                return;
                            }

                            HandleReceiveError(packet);
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "MARS Session Id {0}, packet.DataLeft 0, SNI error {2}", args0: _lowerHandle?.ConnectionId, args1: sniErrorCode);
                            return;
                        }
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public uint EnableSsl(uint options)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                return _lowerHandle.EnableSsl(options);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public void DisableSsl()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                _lowerHandle.DisableSsl();
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public SNIPacket RentPacket(int headerSize, int dataSize)
        {
            return _lowerHandle.RentPacket(headerSize, dataSize);
        }

        public void ReturnPacket(SNIPacket packet)
        {
            _lowerHandle.ReturnPacket(packet);
        }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public void KillConnection()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                _lowerHandle.KillConnection();
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }
#endif
    }
}
