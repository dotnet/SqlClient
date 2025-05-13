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
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// Named Pipe connection handle
    /// </summary>
    internal sealed class SniNpHandle : SniPhysicalHandle
    {
        internal const string DefaultPipePath = @"sql\query"; // e.g. \\HOSTNAME\pipe\sql\query
        // private const int MAX_PIPE_INSTANCES = 255; // TODO: Investigate pipe instance limit.

        private readonly string _targetServer;
        private readonly object _sendSync;
        private readonly string _hostNameInCertificate;
        private readonly string _serverCertificateFilename;
        private readonly bool _tlsFirst;
        private Stream _stream;
        private NamedPipeClientStream _pipeStream;
        private SslOverTdsStream _sslOverTdsStream;

        private SslStream _sslStream;
        private SniAsyncCallback _receiveCallback;
        private SniAsyncCallback _sendCallback;

        private bool _validateCert = true;
        private readonly uint _status = TdsEnums.SNI_UNINITIALIZED;
        private int _bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE;
        private readonly Guid _connectionId = Guid.NewGuid();

        public SniNpHandle(string serverName, string pipeName, TimeoutTimer timeout, bool tlsFirst, string hostNameInCertificate, string serverCertificateFilename)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Setting server name = {1}, pipe name = {2}", args0: _connectionId, args1: serverName, args2: pipeName);

                _sendSync = new object();
                _targetServer = serverName;
                _tlsFirst = tlsFirst;
                _hostNameInCertificate = hostNameInCertificate;
                _serverCertificateFilename = serverCertificateFilename;
                try
                {
                    _pipeStream = new NamedPipeClientStream(
                        serverName,
                        pipeName,
                        PipeDirection.InOut,
                        PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                    if (timeout.IsInfinite)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO,
                                                                  "Connection Id {0}, Setting server name = {1}, pipe name = {2}. Connecting with infinite timeout.",
                                                                  args0: _connectionId,
                                                                  args1: serverName,
                                                                  args2: pipeName);
                        _pipeStream.Connect(Timeout.Infinite);
                    }
                    else
                    {
                        int timeoutMilliseconds = timeout.MillisecondsRemainingInt;
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO,
                                                                  "Connection Id {0}, Setting server name = {1}, pipe name = {2}. Connecting within the {3} specified milliseconds.",
                                                                  args0: _connectionId,
                                                                  args1: serverName,
                                                                  args2: pipeName,
                                                                  args3: timeoutMilliseconds);
                        _pipeStream.Connect(timeoutMilliseconds);
                    }
                }
                catch (TimeoutException te)
                {
                    SniCommon.ReportSNIError(SniProviders.NP_PROV, SniCommon.ConnOpenFailedError, te);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, Connection Timed out. Error Code 1 Exception = {1}", args0: _connectionId, args1: te?.Message);
                    return;
                }
                catch (IOException ioe)
                {
                    SniCommon.ReportSNIError(SniProviders.NP_PROV, SniCommon.ConnOpenFailedError, ioe);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, IO Exception occurred. Error Code 1 Exception = {1}", args0: _connectionId, args1: ioe?.Message);
                    return;
                }

                if (!_pipeStream.IsConnected || !_pipeStream.CanWrite || !_pipeStream.CanRead)
                {
                    SniCommon.ReportSNIError(SniProviders.NP_PROV, 0, SniCommon.ConnOpenFailedError, Strings.SNI_ERROR_40);
                    _status = TdsEnums.SNI_ERROR;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, Pipe Stream not operational. Error Code 1 Exception = {1}", args0: _connectionId, args1: Strings.SNI_ERROR_1);
                    return;
                }

                Stream stream = _pipeStream;

                if (!_tlsFirst)
                {
                    _sslOverTdsStream = new SslOverTdsStream(_pipeStream, _connectionId);
                    stream = _sslOverTdsStream;
                }
                _sslStream = new SniSslStream(stream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate));

                _stream = _pipeStream;
                _status = TdsEnums.SNI_SUCCESS;
            }
        }

        public override Guid ConnectionId => _connectionId;

        public override uint Status => _status;

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
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {
                if (!_stream.CanWrite || !_stream.CanRead)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, Cannot write or read to/from the stream", args0: _connectionId);
                    return TdsEnums.SNI_ERROR;
                }
                else
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Can read and write to/from stream.", args0: _connectionId);
                    return TdsEnums.SNI_SUCCESS;
                }
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
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, All streams disposed and references cleared.", args0: _connectionId);
            }
        }

        public override uint Receive(out SniPacket packet, int timeout)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {
                SniPacket errorPacket;
                lock (this)
                {
                    packet = null;
                    try
                    {
                        packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
                        packet.ReadFromStream(_stream);
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Rented and read packet, dataLeft {1}", args0: _connectionId, args1: packet?.DataLeft);

                        if (packet.Length == 0)
                        {
                            errorPacket = packet;
                            packet = null;
                            var e = new Win32Exception();
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, Packet length found 0, Win32 exception raised: {1}", args0: _connectionId, args1: e?.Message);
                            return ReportErrorAndReleasePacket(errorPacket, (uint)e.NativeErrorCode, 0, e.Message);
                        }
                    }
                    catch (ObjectDisposedException ode)
                    {
                        errorPacket = packet;
                        packet = null;
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}.", args0: _connectionId, args1: ode?.Message);
                        return ReportErrorAndReleasePacket(errorPacket, ode);
                    }
                    catch (IOException ioe)
                    {
                        errorPacket = packet;
                        packet = null;
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, IOException occurred: {1}.", args0: _connectionId, args1: ioe?.Message);
                        return ReportErrorAndReleasePacket(errorPacket, ioe);
                    }
                    return TdsEnums.SNI_SUCCESS;
                }
            }
        }

        public override uint ReceiveAsync(ref SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {
                SniPacket errorPacket;
                packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
                packet.SetAsyncIOCompletionCallback(_receiveCallback);
                try
                {
                    packet.ReadFromStreamAsync(_stream);
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Rented and read packet asynchronously, dataLeft {1}", args0: _connectionId, args1: packet?.DataLeft);
                    return TdsEnums.SNI_SUCCESS_IO_PENDING;
                }
                catch (ObjectDisposedException ode)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}.", args0: _connectionId, args1: ode?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ode);
                }
                catch (IOException ioe)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, IOException occurred: {1}.", args0: _connectionId, args1: ioe?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ioe);
                }
            }
        }

        public override uint Send(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
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
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Packet writing to stream, dataLeft {1}", args0: _connectionId, args1: packet?.DataLeft);
                            packet.WriteToStream(_stream);
                            return TdsEnums.SNI_SUCCESS;
                        }
                        catch (ObjectDisposedException ode)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}.", args0: _connectionId, args1: ode?.Message);
                            return ReportErrorAndReleasePacket(packet, ode);
                        }
                        catch (IOException ioe)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, IOException occurred: {1}.", args0: _connectionId, args1: ioe?.Message);
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
        }

        public override uint SendAsync(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Packet writing to stream, dataLeft {1}", args0: _connectionId, args1: packet?.DataLeft);
                packet.WriteToStreamAsync(_stream, _sendCallback, SniProviders.NP_PROV);
                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
        }

        public override void SetAsyncCallbacks(SniAsyncCallback receiveCallback, SniAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        public override uint EnableSsl(uint options)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {
                _validateCert = (options & TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE) != 0;
                try
                {
                    if (_tlsFirst)
                    {
                        AuthenticateAsClient(_sslStream, _targetServer, null);
                    }
                    else
                    {
                        // TODO: Resolve whether to send _serverNameIndication or _targetServer. _serverNameIndication currently results in error. Why?
                        _sslStream.AuthenticateAsClient(_targetServer, null, s_supportedProtocols, false);
                    }
                    if (_sslOverTdsStream is not null)
                    {
                        _sslOverTdsStream.FinishHandshake();
                    }
                }
                catch (AuthenticationException aue)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, AuthenticationException message = {1}.", args0: ConnectionId, args1: aue?.Message);
                    return SniCommon.ReportSNIError(SniProviders.NP_PROV, SniCommon.InternalExceptionError, aue);
                }
                catch (InvalidOperationException ioe)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.ERR, "Connection Id {0}, InvalidOperationException message = {1}.", args0: ConnectionId, args1: ioe?.Message);
                    return SniCommon.ReportSNIError(SniProviders.NP_PROV, SniCommon.InternalExceptionError, ioe);
                }
                _stream = _sslStream;
                return TdsEnums.SNI_SUCCESS;
            }
        }

        public override void DisableSsl()
        {
            _sslStream.Dispose();
            _sslStream = null;
            _sslOverTdsStream?.Dispose();
            _sslOverTdsStream = null;

            _stream = _pipeStream;
        }

        /// <summary>
        /// Validate server certificate
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="serverCertificate">X.509 certificate</param>
        /// <param name="chain">X.509 chain</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>true if valid</returns>
        private bool ValidateServerCertificate(object sender, X509Certificate serverCertificate, X509Chain chain, SslPolicyErrors policyErrors)
        {
            using (TrySNIEventScope.Create(nameof(SniNpHandle)))
            {			
                if (!_validateCert)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Certificate validation not requested.", args0: ConnectionId);
                    return true;
                }

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Proceeding to SSL certificate validation.", args0: ConnectionId);
                return SniCommon.ValidateSslServerCertificate(_connectionId, _targetServer, _hostNameInCertificate, serverCertificate, _serverCertificateFilename, policyErrors);
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

        private uint ReportErrorAndReleasePacket(SniPacket packet, Exception sniException)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Packet returned, error occurred: {1}", args0: ConnectionId, args1: sniException?.Message);
            return SniCommon.ReportSNIError(SniProviders.NP_PROV, SniCommon.InternalExceptionError, sniException);
        }

        private uint ReportErrorAndReleasePacket(SniPacket packet, uint nativeError, uint sniError, string errorMessage)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniNpHandle), EventType.INFO, "Connection Id {0}, Packet returned, error occurred: {1}", args0: ConnectionId, args1: errorMessage);
            return SniCommon.ReportSNIError(SniProviders.NP_PROV, nativeError, sniError, errorMessage);
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
