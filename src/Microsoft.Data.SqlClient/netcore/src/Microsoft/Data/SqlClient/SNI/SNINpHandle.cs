// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// Named Pipe connection handle
    /// </summary>
    internal sealed class SNINpHandle : SNIPhysicalHandle
    {
        internal const string DefaultPipePath = @"sql\query"; // e.g. \\HOSTNAME\pipe\sql\query

        private readonly string _targetServer;
        private readonly object _sendSync;

        private Stream _stream;
        private NamedPipeClientStream _pipeStream;
        private SslOverTdsStream _sslOverTdsStream;

        private SslStream _sslStream;
        private SNIAsyncCallback _receiveCallback;
        private SNIAsyncCallback _sendCallback;

        private bool _validateCert = true;
        private readonly uint _status = TdsEnums.SNI_UNINITIALIZED;
        private int _bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE;
        private readonly Guid _connectionId = Guid.NewGuid();

        public SNINpHandle(string serverName, string pipeName, long timerExpire)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.ctor | SNI | INFO | SCOPE | Entering Scope {0}");
            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ctor | SNI | INFO | Connection Id {0}, Setting server name = {1}, pipe name = {2}", _connectionId, serverName, pipeName);
            try
            {
                _sendSync = new object();
                _targetServer = serverName;

                try
                {
                    _pipeStream = new NamedPipeClientStream(
                        serverName,
                        pipeName,
                        PipeDirection.InOut,
                        PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                    bool isInfiniteTimeOut = long.MaxValue == timerExpire;
                    if (isInfiniteTimeOut)
                    {
                        _pipeStream.Connect(Timeout.Infinite);
                    }
                    else
                    {
                        TimeSpan ts = DateTime.FromFileTime(timerExpire) - DateTime.Now;
                        ts = ts.Ticks < 0 ? TimeSpan.FromTicks(0) : ts;

                        _pipeStream.Connect((int)ts.TotalMilliseconds);
                    }
                }
                catch (TimeoutException te)
                {
                    SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.ConnOpenFailedError, te);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ctor | SNI | ERR | Connection Id {0}, Connection Timed out. Error Code 1 Exception = {1}", _connectionId, te?.Message);
                    return;
                }
                catch (IOException ioe)
                {
                    SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.ConnOpenFailedError, ioe);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ctor | SNI | ERR | Connection Id {0}, IO Exception occurred. Error Code 1 Exception = {1}", _connectionId, ioe?.Message);
                    return;
                }

                if (!_pipeStream.IsConnected || !_pipeStream.CanWrite || !_pipeStream.CanRead)
                {
                    SNICommon.ReportSNIError(SNIProviders.NP_PROV, 0, SNICommon.ConnOpenFailedError, Strings.SNI_ERROR_40);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ctor | SNI | ERR | Connection Id {0}, Pipe Stream not operational. Error Code 1 Exception = {1}", _connectionId, Strings.SNI_ERROR_1);
                    return;
                }

                _sslOverTdsStream = new SslOverTdsStream(_pipeStream, _connectionId);
                _sslStream = new SNISslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate));

                _stream = _pipeStream;
                _status = TdsEnums.SNI_SUCCESS;
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        public override uint Status
        {
            get
            {
                return _status;
            }
        }

        public override int ProtocolVersion
        {
            get
            {
                try
                {
                    return (int)_sslStream.SslProtocol;
                }
                catch
                {
                    return base.ProtocolVersion;
                }
            }
        }

        public override uint CheckConnection()
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.CheckConnection | SNI | INFO | SCOPE | Entering Scope {0} ");
            try
            {
                if (!_stream.CanWrite || !_stream.CanRead)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.CheckConnection | SNI | ERR | Connection Id {0}, Cannot write or read to/from the stream", _connectionId);
                    return TdsEnums.SNI_ERROR;
                }
                else
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.CheckConnection | SNI | INFO | Connection Id {0}, Can read and write to/from stream.", _connectionId);
                    return TdsEnums.SNI_SUCCESS;
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override void Dispose()
        {
            lock (this)
            {
                if (_sslOverTdsStream != null)
                {
                    _sslOverTdsStream.Dispose();
                    _sslOverTdsStream = null;
                }

                if (_sslStream != null)
                {
                    _sslStream.Dispose();
                    _sslStream = null;
                }

                if (_pipeStream != null)
                {
                    _pipeStream.Dispose();
                    _pipeStream = null;
                }

                //Release any references held by _stream.
                _stream = null;
                SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Dispose | SNI | INFO | Connection Id {0}, All streams disposed and references cleared.", _connectionId);
            }
        }

        public override uint Receive(out SNIPacket packet, int timeout)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.Receive | SNI | INFO | SCOPE | Entering Scope {0} ");
            try
            {
                SNIPacket errorPacket;
                lock (this)
                {
                    packet = null;
                    try
                    {
                        packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
                        packet.ReadFromStream(_stream);
                        SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Receive | SNI | INFO | Connection Id {0}, Rented and read packet, dataLeft {1}", _connectionId, packet?.DataLeft);

                        if (packet.Length == 0)
                        {
                            errorPacket = packet;
                            packet = null;
                            var e = new Win32Exception();
                            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Receive | SNI | ERR | Connection Id {0}, Packet length found 0, Win32 exception raised: {1}", _connectionId, e?.Message);
                            return ReportErrorAndReleasePacket(errorPacket, (uint)e.NativeErrorCode, 0, e.Message);
                        }
                    }
                    catch (ObjectDisposedException ode)
                    {
                        errorPacket = packet;
                        packet = null;
                        SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Receive | SNI | ERR | Connection Id {0}, ObjectDisposedException occurred: {1}.", _connectionId, ode?.Message);
                        return ReportErrorAndReleasePacket(errorPacket, ode);
                    }
                    catch (IOException ioe)
                    {
                        errorPacket = packet;
                        packet = null;
                        SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Receive | SNI | ERR | Connection Id {0}, IOException occurred: {1}.", _connectionId, ioe?.Message);
                        return ReportErrorAndReleasePacket(errorPacket, ioe);
                    }
                    return TdsEnums.SNI_SUCCESS;
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override uint ReceiveAsync(ref SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.ReceiveAsync | SNI | INFO | SCOPE | Entering Scope {0} ");
            try
            {
                SNIPacket errorPacket;
                packet = RentPacket(headerSize: 0, dataSize: _bufferSize);

                try
                {
                    packet.ReadFromStreamAsync(_stream, _receiveCallback);
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ReceiveAsync | SNI | INFO | Connection Id {0}, Rented and read packet asynchronously, dataLeft {1}", _connectionId, packet?.DataLeft);
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                catch (ObjectDisposedException ode)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ReceiveAsync | SNI | ERR | Connection Id {0}, ObjectDisposedException occurred: {1}.", _connectionId, ode?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ode);
                }
                catch (IOException ioe)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ReceiveAsync | SNI | ERR | Connection Id {0}, IOException occurred: {1}.", _connectionId, ioe?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ioe);
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override uint Send(SNIPacket packet)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.Send | SNI | INFO | SCOPE | Entering Scope {0} ");
            try
            {
                bool releaseLock = false;
                try
                {
                    // is the packet is marked out out-of-band (attention packets only) it must be
                    // sent immediately even if a send of receive operation is already in progress
                    // because out of band packets are used to cancel ongoing operations
                    // so try to take the lock if possible but continue even if it can't be taken
                    if (packet.IsOutOfBand)
                    {
                        Monitor.TryEnter(this, ref releaseLock);
                    }
                    else
                    {
                        Monitor.Enter(this);
                        releaseLock = true;
                    }

                    // this lock ensures that two packets are not being written to the transport at the same time
                    // so that sending a standard and an out-of-band packet are both written atomically no data is
                    // interleaved
                    lock (_sendSync)
                    {
                        try
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Send | SNI | INFO | Connection Id {0}, Packet writing to stream, dataLeft {1}", _connectionId, packet?.DataLeft);
                            packet.WriteToStream(_stream);
                            return TdsEnums.SNI_SUCCESS;
                        }
                        catch (ObjectDisposedException ode)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Send | SNI | ERR | Connection Id {0}, ObjectDisposedException occurred: {1}.", _connectionId, ode?.Message);
                            return ReportErrorAndReleasePacket(packet, ode);
                        }
                        catch (IOException ioe)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.Send | SNI | ERR | Connection Id {0}, IOException occurred: {1}.", _connectionId, ioe?.Message);
                            return ReportErrorAndReleasePacket(packet, ioe);
                        }
                    }
                }
                finally
                {
                    if (releaseLock)
                    {
                        Monitor.Exit(this);
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override uint SendAsync(SNIPacket packet, SNIAsyncCallback callback = null)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.SendAsync | SNI | INFO | SCOPE | Entering Scope {0}");
            try
            {
                SNIAsyncCallback cb = callback ?? _sendCallback;
                SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.SendAsync | SNI | INFO | Connection Id {0}, Packet writing to stream, dataLeft {1}", _connectionId, packet?.DataLeft);
                packet.WriteToStreamAsync(_stream, cb, SNIProviders.NP_PROV);
                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        public override uint EnableSsl(uint options)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.EnableSsl | SNI | INFO | SCOPE | Entering Scope {0}");
            try
            {
                _validateCert = (options & TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE) != 0;
                try
                {

                    _sslStream.AuthenticateAsClient(_targetServer);
                    _sslOverTdsStream.FinishHandshake();
                }
                catch (AuthenticationException aue)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.EnableSsl | SNI | ERR | Connection Id {0}, AuthenticationException message = {1}.", ConnectionId, aue.Message);
                    return SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, aue);
                }
                catch (InvalidOperationException ioe)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.EnableSsl | SNI | ERR | Connection Id {0}, InvalidOperationException message = {1}.", ConnectionId, ioe.Message);
                    return SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, ioe);
                }
                _stream = _sslStream;
                return TdsEnums.SNI_SUCCESS;
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        public override void DisableSsl()
        {
            _sslStream.Dispose();
            _sslStream = null;
            _sslOverTdsStream.Dispose();
            _sslOverTdsStream = null;

            _stream = _pipeStream;
        }

        /// <summary>
        /// Validate server certificate
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="cert">X.509 certificate</param>
        /// <param name="chain">X.509 chain</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>true if valid</returns>
        private bool ValidateServerCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("SNINpHandle.ValidateServerCertificate | SNI | INFO | SCOPE | Entering Scope {0}");
            try
            {
                if (!_validateCert)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ValidateServerCertificate | SNI | INFO | Connection Id {0}, Certificate validation not requested.", ConnectionId);
                    return true;
                }

                SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ValidateServerCertificate | SNI | INFO | Connection Id {0}, Proceeding to SSL certificate validation.", ConnectionId);
                return SNICommon.ValidateSslServerCertificate(_targetServer, cert, policyErrors);
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public override void SetBufferSize(int bufferSize)
        {
            _bufferSize = bufferSize;
        }

        private uint ReportErrorAndReleasePacket(SNIPacket packet, Exception sniException)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ReportErrorAndReleasePacket | SNI | INFO | Connection Id {0}, Packet returned, error occurred: {1} ", ConnectionId, sniException.Message);
            return SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, sniException);
        }

        private uint ReportErrorAndReleasePacket(SNIPacket packet, uint nativeError, uint sniError, string errorMessage)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            SqlClientEventSource.Log.TrySNITraceEvent("SNINpHandle.ReportErrorAndReleasePacket | SNI | INFO | Connection Id {0}, Packet returned, error occurred: {1} ", ConnectionId, errorMessage);
            return SNICommon.ReportSNIError(SNIProviders.NP_PROV, nativeError, sniError, errorMessage);
        }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _pipeStream.Dispose();
            _pipeStream = null;
        }
#endif
    }
}
