// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// TCP connection handle
    /// </summary>
    internal sealed class SNITCPHandle : SNIPhysicalHandle
    {
        private readonly string _targetServer;
        private readonly object _sendSync;
        private readonly Socket _socket;
        private NetworkStream _tcpStream;
        private readonly string _hostNameInCertificate;
        private readonly string _serverCertificateFilename;
        private readonly bool _tlsFirst;

        private Stream _stream;
        private SslStream _sslStream;
        private SslOverTdsStream _sslOverTdsStream;
        private SNIAsyncCallback _receiveCallback;
        private SNIAsyncCallback _sendCallback;

        private bool _validateCert = true;
        private int _bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE;
        private uint _status = TdsEnums.SNI_UNINITIALIZED;
        private Guid _connectionId = Guid.NewGuid();

        private const int MaxParallelIpAddresses = 64;

        /// <summary>
        /// Dispose object
        /// </summary>
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

                if (_tcpStream != null)
                {
                    _tcpStream.Dispose();
                    _tcpStream = null;
                }

                //Release any references held by _stream.
                _stream = null;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, All streams disposed.", args0: _connectionId);
            }
        }

        /// <summary>
        /// Connection ID
        /// </summary>
        public override Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        /// <summary>
        /// Connection status
        /// </summary>
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

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serverName">Server name</param>
        /// <param name="port">TCP port number</param>
        /// <param name="timerExpire">Connection timer expiration</param>
        /// <param name="parallel">Parallel executions</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <param name="cachedFQDN">Key for DNS Cache</param>
        /// <param name="pendingDNSInfo">Used for DNS Cache</param>
        /// <param name="tlsFirst">Support TDS8.0</param>
        /// <param name="hostNameInCertificate">Host Name in Certificate</param>
        /// <param name="serverCertificateFilename">Used for the path to the Server Certificate</param>
        public SNITCPHandle(
            string serverName,
            int port,
            long timerExpire,
            bool parallel,
            SqlConnectionIPAddressPreference ipPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            bool tlsFirst,
            string hostNameInCertificate,
            string serverCertificateFilename)
        {
            using (TrySNIEventScope.Create(nameof(SNITCPHandle)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Setting server name = {1}", args0: _connectionId, args1: serverName);

                _targetServer = serverName;
                _tlsFirst = tlsFirst;
                _hostNameInCertificate = hostNameInCertificate;
                _serverCertificateFilename = serverCertificateFilename;
                _sendSync = new object();

                SQLDNSInfo cachedDNSInfo;
                bool hasCachedDNSInfo = SQLFallbackDNSCache.Instance.GetDNSInfo(cachedFQDN, out cachedDNSInfo);

                try
                {
                    TimeSpan ts = default(TimeSpan);

                    // In case the Timeout is Infinite, we will receive the max value of Int64 as the tick count
                    // The infinite Timeout is a function of ConnectionString Timeout=0
                    bool isInfiniteTimeOut = long.MaxValue == timerExpire;
                    if (!isInfiniteTimeOut)
                    {
                        ts = DateTime.FromFileTime(timerExpire) - DateTime.Now;
                        ts = ts.Ticks < 0 ? TimeSpan.FromTicks(0) : ts;
                    }

                    bool reportError = true;

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Connecting to serverName {1} and port {2}", args0: _connectionId, args1: serverName, args2: port);
                    // We will always first try to connect with serverName as before and let DNS resolve the serverName.
                    // If DNS resolution fails, we will try with IPs in the DNS cache if they exist. We try with cached IPs based on IPAddressPreference.
                    // Exceptions will be thrown to the caller and be handled as before.
                    try
                    {
                        if (parallel)
                        {
                            _socket = TryConnectParallel(serverName, port, ts, isInfiniteTimeOut, ref reportError, cachedFQDN, ref pendingDNSInfo);
                        }
                        else
                        {
                            _socket = Connect(serverName, port, ts, isInfiniteTimeOut, ipPreference, cachedFQDN, ref pendingDNSInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Retry with cached IP address
                        if (ex is SocketException || ex is ArgumentException || ex is AggregateException)
                        {
                            if (hasCachedDNSInfo == false)
                            {
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Cached DNS Info not found, exception occurred thrown: {1}", args0: _connectionId, args1: ex?.Message);
                                throw;
                            }
                            else
                            {
                                int portRetry = string.IsNullOrEmpty(cachedDNSInfo.Port) ? port : int.Parse(cachedDNSInfo.Port);
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Retrying with cached DNS IP Address {1} and port {2}", args0: _connectionId, args1: cachedDNSInfo.AddrIPv4, args2: cachedDNSInfo.Port);

                                string firstCachedIP;
                                string secondCachedIP;

                                if (SqlConnectionIPAddressPreference.IPv6First == ipPreference)
                                {
                                    firstCachedIP = cachedDNSInfo.AddrIPv6;
                                    secondCachedIP = cachedDNSInfo.AddrIPv4;
                                }
                                else
                                {
                                    firstCachedIP = cachedDNSInfo.AddrIPv4;
                                    secondCachedIP = cachedDNSInfo.AddrIPv6;
                                }

                                try
                                {
                                    if (parallel)
                                    {
                                        _socket = TryConnectParallel(firstCachedIP, portRetry, ts, isInfiniteTimeOut, ref reportError, cachedFQDN, ref pendingDNSInfo);
                                    }
                                    else
                                    {
                                        _socket = Connect(firstCachedIP, portRetry, ts, isInfiniteTimeOut, ipPreference, cachedFQDN, ref pendingDNSInfo);
                                    }
                                }
                                catch (Exception exRetry)
                                {
                                    if (exRetry is SocketException || exRetry is ArgumentNullException
                                        || exRetry is ArgumentException || exRetry is ArgumentOutOfRangeException || exRetry is AggregateException)
                                    {
                                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Retrying exception {1}", args0: _connectionId, args1: exRetry?.Message);
                                        if (parallel)
                                        {
                                            _socket = TryConnectParallel(secondCachedIP, portRetry, ts, isInfiniteTimeOut, ref reportError, cachedFQDN, ref pendingDNSInfo);
                                        }
                                        else
                                        {
                                            _socket = Connect(secondCachedIP, portRetry, ts, isInfiniteTimeOut, ipPreference, cachedFQDN, ref pendingDNSInfo);
                                        }
                                    }
                                    else
                                    {
                                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Retry failed, exception occurred: {1}", args0: _connectionId, args1: exRetry?.Message);
                                        throw;
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }

                    if (_socket == null || !_socket.Connected)
                    {
                        if (_socket != null)
                        {
                            _socket.Dispose();
                            _socket = null;
                        }

                        if (reportError)
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0} could not be opened, exception occurred: {1}", args0: _connectionId, args1: Strings.SNI_ERROR_40);
                            ReportTcpSNIError(0, SNICommon.ConnOpenFailedError, Strings.SNI_ERROR_40);
                        }
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0} Socket could not be opened.", args0: _connectionId);
                        return;
                    }

                    _socket.NoDelay = true;
                    _tcpStream = new SNINetworkStream(_socket, true);

                    Stream stream = _tcpStream;
                    if (!_tlsFirst)
                    {
                        _sslOverTdsStream = new SslOverTdsStream(_tcpStream, _connectionId);
                        stream = _sslOverTdsStream;
                    }
                    _sslStream = new SNISslStream(stream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate));
                }
                catch (SocketException se)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0} Socket exception occurred: Error Code {1}, Message {2}", args0: _connectionId, args1: se?.SocketErrorCode, args2: se?.Message);
                    ReportTcpSNIError(se);
                    return;
                }
                catch (Exception e)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0} Exception occurred: {1}", args0: _connectionId, args1: e?.Message);
                    ReportTcpSNIError(e);
                    return;
                }

                _stream = _tcpStream;
                _status = TdsEnums.SNI_SUCCESS;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0} Socket opened successfully, TCP stream ready.", args0: _connectionId);
            }
        }

        // Connect to server with hostName and port in parellel mode.
        // The IP information will be collected temporarily as the pendingDNSInfo but is not stored in the DNS cache at this point.
        // Only write to the DNS cache when we receive IsSupported flag as true in the Feature Ext Ack from server.
        private Socket TryConnectParallel(string hostName, int port, TimeSpan ts, bool isInfiniteTimeOut, ref bool callerReportError, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo)
        {
            Socket availableSocket = null;
            Task<Socket> connectTask;

            IPAddress[] serverAddresses = SNICommon.GetDnsIpAddresses(hostName);

            if (serverAddresses.Length > MaxParallelIpAddresses)
            {
                // Fail if above 64 to match legacy behavior
                callerReportError = false;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0} serverAddresses.Length {1} Exception: {2}", args0: _connectionId, args1: serverAddresses.Length, args2: Strings.SNI_ERROR_47);
                ReportTcpSNIError(0, SNICommon.MultiSubnetFailoverWithMoreThan64IPs, Strings.SNI_ERROR_47);
                return availableSocket;
            }

            string IPv4String = null;
            string IPv6String = null;

            foreach (IPAddress ipAddress in serverAddresses)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPv4String = ipAddress.ToString();
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    IPv6String = ipAddress.ToString();
                }
            }

            if (IPv4String != null || IPv6String != null)
            {
                pendingDNSInfo = new SQLDNSInfo(cachedFQDN, IPv4String, IPv6String, port.ToString());
            }

            connectTask = ParallelConnectAsync(serverAddresses, port);

            if (!(isInfiniteTimeOut ? connectTask.Wait(-1) : connectTask.Wait(ts)))
            {
                callerReportError = false;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0} Connection timed out, Exception: {1}", args0: _connectionId, args1: Strings.SNI_ERROR_40);
                ReportTcpSNIError(0, SNICommon.ConnOpenFailedError, Strings.SNI_ERROR_40);
                return availableSocket;
            }

            availableSocket = connectTask.Result;
            return availableSocket;
        }
        
        /// <summary>
        /// Returns array of IP addresses for the given server name, sorted according to the given preference.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when ipPreference is not supported</exception>
        private static IEnumerable<IPAddress> GetHostAddressesSortedByPreference(string serverName, SqlConnectionIPAddressPreference ipPreference)
        {
            IPAddress[] ipAddresses = Dns.GetHostAddresses(serverName);
            AddressFamily? prioritiesFamily = ipPreference switch
            {
                SqlConnectionIPAddressPreference.IPv4First => AddressFamily.InterNetwork,
                SqlConnectionIPAddressPreference.IPv6First => AddressFamily.InterNetworkV6,
                SqlConnectionIPAddressPreference.UsePlatformDefault => null,
                _ => throw ADP.NotSupportedEnumerationValue(typeof(SqlConnectionIPAddressPreference), ipPreference.ToString(), nameof(GetHostAddressesSortedByPreference))
            };

            // Return addresses of the preferred family first
            if (prioritiesFamily != null)
            {
                foreach (IPAddress ipAddress in ipAddresses)
                {
                    if (ipAddress.AddressFamily == prioritiesFamily)
                    {
                        yield return ipAddress;
                    }
                }
            }

            // Return addresses of the other family
            foreach (IPAddress ipAddress in ipAddresses)
            {
                if (ipAddress.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                {
                    if (prioritiesFamily == null || ipAddress.AddressFamily != prioritiesFamily)
                    {
                        yield return ipAddress;
                    }
                }
            }
        }
        
        // Connect to server with hostName and port.
        // The IP information will be collected temporarily as the pendingDNSInfo but is not stored in the DNS cache at this point.
        // Only write to the DNS cache when we receive IsSupported flag as true in the Feature Ext Ack from server.
        private static Socket Connect(string serverName, int port, TimeSpan timeout, bool isInfiniteTimeout, SqlConnectionIPAddressPreference ipPreference, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo)
        {
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "IP preference : {0}", Enum.GetName(typeof(SqlConnectionIPAddressPreference), ipPreference));

            Stopwatch timeTaken = Stopwatch.StartNew();

            IEnumerable<IPAddress> ipAddresses = GetHostAddressesSortedByPreference(serverName, ipPreference);

            foreach (IPAddress ipAddress in ipAddresses)
            {
                bool isSocketSelected = false;
                Socket socket = null;

                try
                {
                    socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        Blocking = isInfiniteTimeout
                    };

                    // enable keep-alive on socket
                    SetKeepAliveValues(ref socket);

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO,
                        "Connecting to IP address {0} and port {1} using {2} address family. Is infinite timeout: {3}",
                        ipAddress,
                        port,
                        ipAddress.AddressFamily,
                        isInfiniteTimeout);
                
                    bool isConnected;
                    try // catching SocketException with SocketErrorCode == WouldBlock to run Socket.Select
                    {
                        socket.Connect(ipAddress, port);
                        if (!isInfiniteTimeout)
                        {
                            throw SQL.SocketDidNotThrow();
                        }
                        
                        isConnected = true;
                    }
                    catch (SocketException socketException) when (!isInfiniteTimeout &&
                                                                  socketException.SocketErrorCode ==
                                                                  SocketError.WouldBlock)
                    {
                        // https://github.com/dotnet/SqlClient/issues/826#issuecomment-736224118
                        // Socket.Select is used because it supports timeouts, while Socket.Connect does not

                        List<Socket> checkReadLst; List<Socket> checkWriteLst; List<Socket> checkErrorLst;

                        // Repeating Socket.Select several times if our timeout is greater
                        // than int.MaxValue microseconds because of 
                        // https://github.com/dotnet/SqlClient/pull/1029#issuecomment-875364044
                        // which states that Socket.Select can't handle timeouts greater than int.MaxValue microseconds
                        do
                        {
                            TimeSpan timeLeft = timeout - timeTaken.Elapsed;

                            if (timeLeft <= TimeSpan.Zero)
                                return null;

                            int socketSelectTimeout =
                                checked((int)(Math.Min(timeLeft.TotalMilliseconds, int.MaxValue / 1000) * 1000));

                            checkReadLst = new List<Socket>(1) { socket };
                            checkWriteLst = new List<Socket>(1) { socket };
                            checkErrorLst = new List<Socket>(1) { socket };

                            Socket.Select(checkReadLst, checkWriteLst, checkErrorLst, socketSelectTimeout);
                            // nothing selected means timeout
                        } while (checkReadLst.Count == 0 && checkWriteLst.Count == 0 && checkErrorLst.Count == 0);

                        // workaround: false positive socket.Connected on linux: https://github.com/dotnet/runtime/issues/55538
                        isConnected = socket.Connected && checkErrorLst.Count == 0;
                    }

                    if (isConnected)
                    {
                        socket.Blocking = true;
                        string iPv4String = null;
                        string iPv6String = null;
                        if (socket.AddressFamily == AddressFamily.InterNetwork)
                        {
                            iPv4String = ipAddress.ToString();
                        }
                        else
                        {
                            iPv6String = ipAddress.ToString();
                        }
                        pendingDNSInfo = new SQLDNSInfo(cachedFQDN, iPv4String, iPv6String, port.ToString());
                        isSocketSelected = true;
                        return socket;
                    }
                }
                catch (SocketException e)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "THIS EXCEPTION IS BEING SWALLOWED: {0}", args0: e?.Message);
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"{nameof(SNITCPHandle)}.{nameof(Connect)}{EventType.ERR}THIS EXCEPTION IS BEING SWALLOWED: {e}");
                }
                finally
                {
                    if (!isSocketSelected)
                        socket?.Dispose();
                }
            }

            return null;
        }

        private static Task<Socket> ParallelConnectAsync(IPAddress[] serverAddresses, int port)
        {
            if (serverAddresses == null)
            {
                throw new ArgumentNullException(nameof(serverAddresses));
            }
            if (serverAddresses.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverAddresses));
            }

            var sockets = new List<Socket>(serverAddresses.Length);
            var connectTasks = new List<Task>(serverAddresses.Length);
            var tcs = new TaskCompletionSource<Socket>();
            var lastError = new StrongBox<Exception>();
            var pendingCompleteCount = new StrongBox<int>(serverAddresses.Length);

            foreach (IPAddress address in serverAddresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sockets.Add(socket);

                // Start all connection tasks now, to prevent possible race conditions with
                // calling ConnectAsync on disposed sockets.
                try
                {
                    connectTasks.Add(socket.ConnectAsync(address, port));
                }
                catch (Exception e)
                {
                    connectTasks.Add(Task.FromException(e));
                }
            }

            for (int i = 0; i < sockets.Count; i++)
            {
                ParallelConnectHelper(sockets[i], connectTasks[i], tcs, pendingCompleteCount, lastError, sockets);
            }

            return tcs.Task;
        }

        private static async void ParallelConnectHelper(
            Socket socket,
            Task connectTask,
            TaskCompletionSource<Socket> tcs,
            StrongBox<int> pendingCompleteCount,
            StrongBox<Exception> lastError,
            List<Socket> sockets)
        {
            bool success = false;
            try
            {
                // Try to connect.  If we're successful, store this task into the result task.
                await connectTask.ConfigureAwait(false);
                success = tcs.TrySetResult(socket);
                if (success)
                {
                    // Whichever connection completes the return task is responsible for disposing
                    // all of the sockets (except for whichever one is stored into the result task).
                    // This ensures that only one thread will attempt to dispose of a socket.
                    // This is also the closest thing we have to canceling connect attempts.
                    foreach (Socket otherSocket in sockets)
                    {
                        if (otherSocket != socket)
                        {
                            otherSocket.Dispose();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Store an exception to be published if no connection succeeds
                Interlocked.Exchange(ref lastError.Value, e);
            }
            finally
            {
                // If we didn't successfully transition the result task to completed,
                // then someone else did and they would have cleaned up, so there's nothing
                // more to do.  Otherwise, no one completed it yet or we failed; either way,
                // see if we're the last outstanding connection, and if we are, try to complete
                // the task, and if we're successful, it's our responsibility to dispose all of the sockets.
                if (!success && Interlocked.Decrement(ref pendingCompleteCount.Value) == 0)
                {
                    if (lastError.Value != null)
                    {
                        tcs.TrySetException(lastError.Value);
                    }
                    else
                    {
                        tcs.TrySetCanceled();
                    }

                    foreach (Socket s in sockets)
                    {
                        s.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public override uint EnableSsl(uint options)
        {
            using (TrySNIEventScope.Create(nameof(SNIHandle)))
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
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Authentication exception occurred: {1}", args0: _connectionId, args1: aue?.Message);
                    return ReportTcpSNIError(aue, SNIError.CertificateValidationErrorCode);
                }
                catch (InvalidOperationException ioe)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Invalid Operation Exception occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                    return ReportTcpSNIError(ioe);
                }

                _stream = _sslStream;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, SSL enabled successfully.", args0: _connectionId);
                return TdsEnums.SNI_SUCCESS;
            }
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public override void DisableSsl()
        {
            _sslStream.Dispose();
            _sslStream = null;
            _sslOverTdsStream?.Dispose();
            _sslOverTdsStream = null;
            _stream = _tcpStream;
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, SSL Disabled. Communication will continue on TCP Stream.", args0: _connectionId);
        }

        /// <summary>
        /// Validate server certificate callback
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="serverCertificate">X.509 certificate provided from the server</param>
        /// <param name="chain">X.509 chain</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>True if certificate is valid</returns>
        private bool ValidateServerCertificate(object sender, X509Certificate serverCertificate, X509Chain chain, SslPolicyErrors policyErrors)
        {
            if (!_validateCert)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: _connectionId);
                return true;
            }
            
            string serverNameToValidate;
            if (!string.IsNullOrEmpty(_hostNameInCertificate))
            {
                serverNameToValidate = _hostNameInCertificate;
            }
            else
            {
                serverNameToValidate = _targetServer;
            }

            if (!string.IsNullOrEmpty(_serverCertificateFilename))
            {
                X509Certificate clientCertificate = null;
                try
                {
                    clientCertificate = new X509Certificate(_serverCertificateFilename);
                    return SNICommon.ValidateSslServerCertificate(clientCertificate, serverCertificate, policyErrors);
                }
                catch (Exception e)
                {
                    // if this fails, then fall back to the HostNameInCertificate or TargetServer validation.
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, IOException occurred: {1}", args0: _connectionId, args1: e.Message);
                }
            }

            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: _connectionId);
            return SNICommon.ValidateSslServerCertificate(serverNameToValidate, serverCertificate, policyErrors);
        }

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public override void SetBufferSize(int bufferSize)
        {
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint Send(SNIPacket packet)
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
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Data sent to stream synchronously", args0: _connectionId);
                        return TdsEnums.SNI_SUCCESS;
                    }
                    catch (ObjectDisposedException ode)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}", args0: _connectionId, args1: ode?.Message);
                        return ReportTcpSNIError(ode);
                    }
                    catch (SocketException se)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, SocketException occurred: {1}", args0: _connectionId, args1: se?.Message);
                        return ReportTcpSNIError(se);
                    }
                    catch (IOException ioe)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, IOException occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                        return ReportTcpSNIError(ioe);
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

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds</param>
        /// <returns>SNI error code</returns>
        public override uint Receive(out SNIPacket packet, int timeoutInMilliseconds)
        {
            SNIPacket errorPacket;
            lock (this)
            {
                packet = null;
                try
                {
                    if (timeoutInMilliseconds > 0)
                    {
                        _socket.ReceiveTimeout = timeoutInMilliseconds;
                    }
                    else if (timeoutInMilliseconds == -1)
                    {
                        // SqlClient internally represents infinite timeout by -1, and for TcpClient this is translated to a timeout of 0
                        _socket.ReceiveTimeout = 0;
                    }
                    else
                    {
                        // otherwise it is timeout for 0 or less than -1
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Error 258, Timeout error occurred.", args0: _connectionId);
                        ReportTcpSNIError(0, SNICommon.ConnTimeoutError, Strings.SNI_ERROR_11);
                        return TdsEnums.SNI_WAIT_TIMEOUT;
                    }

                    packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
                    packet.ReadFromStream(_stream);

                    if (packet.Length == 0)
                    {
                        errorPacket = packet;
                        packet = null;
                        var e = new Win32Exception();
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Win32 exception occurred: {1}", args0: _connectionId, args1: e?.Message);
                        return ReportErrorAndReleasePacket(errorPacket, (uint)e.NativeErrorCode, 0, e.Message);
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Data read from stream synchronously", args0: _connectionId);
                    return TdsEnums.SNI_SUCCESS;
                }
                catch (ObjectDisposedException ode)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}", args0: _connectionId, args1: ode?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ode);
                }
                catch (SocketException se)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, Socket exception occurred: {1}", args0: _connectionId, args1: se?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, se);
                }
                catch (IOException ioe)
                {
                    errorPacket = packet;
                    packet = null;
                    uint errorCode = ReportErrorAndReleasePacket(errorPacket, ioe);
                    if (ioe.InnerException is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, IO exception occurred with Wait Timeout (error 258): {1}", args0: _connectionId, args1: ioe?.Message);
                        errorCode = TdsEnums.SNI_WAIT_TIMEOUT;
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.ERR, "Connection Id {0}, IO exception occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                    return errorCode;
                }
                finally
                {
                    _socket.ReceiveTimeout = 0;
                }
            }
        }

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        public override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint SendAsync(SNIPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SNITCPHandle)))
            {
                packet.WriteToStreamAsync(_stream, _sendCallback, SNIProviders.TCP_PROV);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Data sent to stream asynchronously", args0: _connectionId);
                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint ReceiveAsync(ref SNIPacket packet)
        {
            SNIPacket errorPacket;
            packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
            packet.SetAsyncIOCompletionCallback(_receiveCallback);
            try
            {
                packet.ReadFromStreamAsync(_stream);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Data received from stream asynchronously", args0: _connectionId);
                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
            catch (Exception e) when (e is ObjectDisposedException || e is SocketException || e is IOException)
            {
                errorPacket = packet;
                packet = null;
                return ReportErrorAndReleasePacket(errorPacket, e);
            }
        }

        /// <summary>
        /// Check SNI handle connection
        /// </summary>
        /// <returns>SNI error status</returns>
        public override uint CheckConnection()
        {
            try
            {
                // _socket.Poll method with argument SelectMode.SelectRead returns
                //      True : if Listen has been called and a connection is pending, or
                //      True : if data is available for reading, or
                //      True : if the connection has been closed, reset, or terminated, i.e no active connection.
                //      False : otherwise.
                // _socket.Available property returns the number of bytes of data available to read.
                //
                // Since _socket.Connected alone doesn't guarantee if the connection is still active, we use it in
                // combination with _socket.Poll method and _socket.Available == 0 check. When both of them
                // return true we can safely determine that the connection is no longer active.
                if (!_socket.Connected || (_socket.Poll(100, SelectMode.SelectRead) && _socket.Available == 0))
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Socket not usable.", args0: _connectionId);
                    return TdsEnums.SNI_ERROR;
                }
            }
            catch (SocketException se)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, Socket Exception occurred: {1}", args0: _connectionId, args1: se?.Message);
                return ReportTcpSNIError(se);
            }
            catch (ObjectDisposedException ode)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNITCPHandle), EventType.INFO, "Connection Id {0}, ObjectDisposedException occurred: {1}", args0: _connectionId, args1: ode?.Message);
                return ReportTcpSNIError(ode);
            }

            return TdsEnums.SNI_SUCCESS;
        }

        private uint ReportTcpSNIError(Exception sniException, uint nativeErrorCode = 0)
        {
            _status = TdsEnums.SNI_ERROR;
            return SNICommon.ReportSNIError(SNIProviders.TCP_PROV, SNICommon.InternalExceptionError, sniException, nativeErrorCode);
        }

        private uint ReportTcpSNIError(uint nativeError, uint sniError, string errorMessage)
        {
            _status = TdsEnums.SNI_ERROR;
            return SNICommon.ReportSNIError(SNIProviders.TCP_PROV, nativeError, sniError, errorMessage);
        }

        private uint ReportErrorAndReleasePacket(SNIPacket packet, Exception sniException)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            return ReportTcpSNIError(sniException);
        }

        private uint ReportErrorAndReleasePacket(SNIPacket packet, uint nativeError, uint sniError, string errorMessage)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            return ReportTcpSNIError(nativeError, sniError, errorMessage);
        }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
#endif

        internal static void SetKeepAliveValues(ref Socket socket)
        {
#if NETCOREAPP
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 1);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
#endif
        }
    }
}
