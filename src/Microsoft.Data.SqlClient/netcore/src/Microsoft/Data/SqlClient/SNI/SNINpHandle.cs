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
        private const int MAX_PIPE_INSTANCES = 255;

        private readonly string _targetServer;
        private readonly object _callbackObject;
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

        public SNINpHandle(string serverName, string pipeName, long timerExpire, object callbackObject)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.SNINpHandle |SNI|INFO|SCOPE> Constructor");
            SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.SNINpHandle |SNI|INFO> Constructor. server name = {0}, pipe name = {1}", serverName, pipeName);
            try
            {
                _sendSync = new object();
                _targetServer = serverName;
                _callbackObject = callbackObject;

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
                        _pipeStream.Connect(System.Threading.Timeout.Infinite);
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
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.SNINpHandle |SNI|ERR> Timed out. Exception = {0}", te.Message);
                    return;
                }
                catch (IOException ioe)
                {
                    SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.ConnOpenFailedError, ioe);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.SNINpHandle |SNI|ERR> IOException = {0}", ioe.Message);
                    return;
                }

                if (!_pipeStream.IsConnected || !_pipeStream.CanWrite || !_pipeStream.CanRead)
                {
                    SNICommon.ReportSNIError(SNIProviders.NP_PROV, 0, SNICommon.ConnOpenFailedError, string.Empty);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.SNINpHandle |SNI|ERR> Pipe stream is not connected or cannot write or read to/from it.");
                    return;
                }

                _sslOverTdsStream = new SslOverTdsStream(_pipeStream);
                _sslStream = new SslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.CheckConnection |SNI|INFO>");
            try
            {
                if (!_stream.CanWrite || !_stream.CanRead)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.CheckConnection |SNI|ERR> cannot write or read to/from the stream");
                    return TdsEnums.SNI_ERROR;
                }
                else
                {
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
            }
        }

        public override uint Receive(out SNIPacket packet, int timeout)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.Receive |SNI|INFO>");
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

                        if (packet.Length == 0)
                        {
                            errorPacket = packet;
                            packet = null;
                            var e = new Win32Exception();
                            SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.Receive |SNI|ERR> packet length is 0.");
                            return ReportErrorAndReleasePacket(errorPacket, (uint)e.NativeErrorCode, 0, e.Message);
                        }
                    }
                    catch (ObjectDisposedException ode)
                    {
                        errorPacket = packet;
                        packet = null;
                        SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.Receive |SNI|ERR> ObjectDisposedException message = {0}.", ode.Message);
                        return ReportErrorAndReleasePacket(errorPacket, ode);
                    }
                    catch (IOException ioe)
                    {
                        errorPacket = packet;
                        packet = null;
                        SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.Receive |SNI|ERR> IOException message = {0}.", ioe.Message);
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.ReceiveAsync |SNI|SCOPE>");
            try
            {
                SNIPacket errorPacket;
                packet = RentPacket(headerSize: 0, dataSize: _bufferSize);

                try
                {
                    packet.ReadFromStreamAsync(_stream, _receiveCallback);
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                catch (ObjectDisposedException ode)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.ReceiveAsync |SNI|ERR> ObjectDisposedException message = {0}.", ode.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ode);
                }
                catch (IOException ioe)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.ReceiveAsync |SNI|ERR> IOException message = {0}.", ioe.Message);
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.Send |SNI|SCOPE>");
            try
            {
                bool releaseLock = false;
                try
                {
                    // is the packet is marked out out-of-band (attention packets only) it must be
                    // sent immediately even if a send of recieve operation is already in progress
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
                            packet.WriteToStream(_stream);
                            return TdsEnums.SNI_SUCCESS;
                        }
                        catch (ObjectDisposedException ode)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.Send |SNI|ERR> ObjectDisposedException message = {0}.", ode.Message);
                            return ReportErrorAndReleasePacket(packet, ode);
                        }
                        catch (IOException ioe)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.Send |SNI|ERR> IOException message = {0}.", ioe.Message);

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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.SendAsync |SNI|SCOPE>");
            try
            {
                SNIAsyncCallback cb = callback ?? _sendCallback;
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.EnableSsl |SNI|SCOPE>");
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
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.EnableSsl |SNI|ERR> AuthenticationException message = {0}.", aue.Message);
                    return SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, aue);
                }
                catch (InvalidOperationException ioe)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent("<sc.SNI.SNINpHandle.EnableSsl |SNI|ERR>InvalidOperationException message = {0}.", ioe.Message);
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
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent("<sc.SNI.SNINpHandle.ValidateServerCertificate |SNI|SCOPE>");
            try
            {
                if (!_validateCert)
                {
                    return true;
                }

                return SNICommon.ValidateSslServerCertificate(_targetServer, sender, cert, chain, policyErrors);
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
            return SNICommon.ReportSNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, sniException);
        }

        private uint ReportErrorAndReleasePacket(SNIPacket packet, uint nativeError, uint sniError, string errorMessage)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
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
