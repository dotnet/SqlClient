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
            _lowerHandle.SetAsyncCallbacks(HandleReceiveComplete, HandleSendComplete);
        }

        public SNIMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            lock (this)
            {
                ushort sessionId = _nextSessionId++;
                SNIMarsHandle handle = new SNIMarsHandle(this, sessionId, callbackObject, async);
                _sessions.Add(sessionId, handle);
                return handle;
            }
        }

        /// <summary>
        /// Start receiving
        /// </summary>
        /// <returns></returns>
        public uint StartReceive()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.StartReceive |SNI|INFO|SCOPE> StartReceive");
            try
            {
                SNIPacket packet = null;

                if (ReceiveAsync(ref packet) == TdsEnums.SNI_SUCCESS_IO_PENDING)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsConnection.StartReceive |SNI|INFO|Trace> Success IO pending.");
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsConnection.StartReceive |SNI|ERR> Connection not useable.");
                return SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, 0, SNICommon.ConnNotUsableError, string.Empty);
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.Send |SNI|INFO|SCOPE> Send");
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
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        public uint SendAsync(SNIPacket packet, SNIAsyncCallback callback)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.SendAsync |SNI|INFO|SCOPE> SendAsync");
            try
            {
                lock (this)
                {
                    return _lowerHandle.SendAsync(packet, callback);
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.SendAsync |SNI|INFO|SCOPE> SendAsync");
            try
            {
                if (packet != null)
                {
                    ReturnPacket(packet);
                    packet = null;
                }

                lock (this)
                {
                    return _lowerHandle.ReceiveAsync(ref packet);
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.CheckConnection |SNI|INFO|SCOPE>");
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
            if (!Monitor.IsEntered(this))
            {
                SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsConnection.HandleReceiveError |SNI|ERR> HandleReceiveError was called without being locked.");
            }
            foreach (SNIMarsHandle handle in _sessions.Values)
            {
                if (packet.HasCompletionCallback)
                {
                    handle.HandleReceiveError(packet);
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
        /// Process a receive completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="sniErrorCode">SNI error code</param>
        public void HandleReceiveComplete(SNIPacket packet, uint sniErrorCode)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.HandleReceiveComplete |SNI|INFO|SCOPE>");
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
                        SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsConnection.HandleReceiveComplete |SNI|ERR> not successful.");
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

                                    if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                    {
                                        SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNIMarsConnection.HandleReceiveComplete |SNI|ERR> not successful.");
                                        return;
                                    }

                                    HandleReceiveError(packet);
                                    return;
                                }
                            }

                            _currentHeader.Read(_headerBytes);

                            _dataBytesLeft = (int)_currentHeader.length;
                            _currentPacket = _lowerHandle.RentPacket(headerSize: 0, dataSize: (int)_currentHeader.length);
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

                                    if (sniErrorCode == TdsEnums.SNI_SUCCESS_IO_PENDING)
                                    {
                                        return;
                                    }

                                    HandleReceiveError(packet);
                                    return;
                                }
                            }
                        }

                        _currentHeaderByteCount = 0;

                        if (!_sessions.ContainsKey(_currentHeader.sessionId))
                        {
                            SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.SMUX_PROV, 0, SNICommon.InvalidParameterError, string.Empty);
                            HandleReceiveError(packet);
                            _lowerHandle.Dispose();
                            _lowerHandle = null;
                            return;
                        }

                        if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_FIN)
                        {
                            _sessions.Remove(_currentHeader.sessionId);
                        }
                        else
                        {
                            currentSession = _sessions[_currentHeader.sessionId];
                        }
                    }

                    if (currentHeader.flags == (byte)SNISMUXFlags.SMUX_DATA)
                    {
                        currentSession.HandleReceiveComplete(currentPacket, currentHeader);
                    }

                    if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_ACK)
                    {
                        try
                        {
                            currentSession.HandleAck(currentHeader.highwater);
                        }
                        catch (Exception e)
                        {
                            SNICommon.ReportSNIError(SNIProviders.SMUX_PROV, SNICommon.InternalExceptionError, e);
                        }

                        Debug.Assert(_currentPacket == currentPacket, "current and _current are not the same");
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.EnableSsl |SNI|INFO|SCOPE>");
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.EnableSsl |SNI|INFO|SCOPE>");
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNIMarsConnection.KillConnection |SNI|INFO|SCOPE>");
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
