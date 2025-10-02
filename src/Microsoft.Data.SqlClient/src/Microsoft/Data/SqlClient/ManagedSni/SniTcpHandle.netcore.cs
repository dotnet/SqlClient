// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// TCP connection handle
    /// </summary>
    internal sealed class SniTcpHandle : SniPhysicalHandle
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
        private SniAsyncCallback _receiveCallback;
        private SniAsyncCallback _sendCallback;

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
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, All streams disposed.", args0: _connectionId);
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

        public override SslProtocols ProtocolVersion
        {
            get
            {
                try
                {
                    return _sslStream.SslProtocol;
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
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="parallel">Parallel executions</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <param name="cachedFQDN">Key for DNS Cache</param>
        /// <param name="pendingDNSInfo">Used for DNS Cache</param>
        /// <param name="tlsFirst">Support TDS8.0</param>
        /// <param name="hostNameInCertificate">Host Name in Certificate</param>
        /// <param name="serverCertificateFilename">Used for the path to the Server Certificate</param>
        public SniTcpHandle(
            string serverName,
            int port,
            TimeoutTimer timeout,
            bool parallel,
            SqlConnectionIPAddressPreference ipPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            bool tlsFirst,
            string hostNameInCertificate,
            string serverCertificateFilename)
        {
            using (TrySNIEventScope.Create(nameof(SniTcpHandle)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Setting server name = {1}", args0: _connectionId, args1: serverName);

                _targetServer = serverName;
                _tlsFirst = tlsFirst;
                _hostNameInCertificate = hostNameInCertificate;
                _serverCertificateFilename = serverCertificateFilename;
                _sendSync = new object();

                SQLDNSInfo cachedDNSInfo;
                bool hasCachedDNSInfo = SQLFallbackDNSCache.Instance.GetDNSInfo(cachedFQDN, out cachedDNSInfo);

                try
                {
                    bool reportError = true;

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Connecting to serverName {1} and port {2}", args0: _connectionId, args1: serverName, args2: port);
                    // We will always first try to connect with serverName as before and let DNS resolve the serverName.
                    // If DNS resolution fails, we will try with IPs in the DNS cache if they exist. We try with cached IPs based on IPAddressPreference.
                    // Exceptions will be thrown to the caller and be handled as before.
                    try
                    {
                        if (parallel)
                        {
                            _socket = TryConnectParallel(serverName, port, timeout, ref reportError, cachedFQDN, ref pendingDNSInfo);
                        }
                        else
                        {
                            _socket = Connect(serverName, port, timeout, ipPreference, cachedFQDN, ref pendingDNSInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (timeout.IsExpired)
                        {
                            throw;
                        }
                        // Retry with cached IP address
                        if (ex is SocketException || ex is ArgumentException || ex is AggregateException)
                        {
                            if (hasCachedDNSInfo == false)
                            {
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Cached DNS Info not found, exception occurred thrown: {1}", args0: _connectionId, args1: ex?.Message);
                                throw;
                            }
                            else
                            {
                                int portRetry = string.IsNullOrEmpty(cachedDNSInfo.Port) ? port : int.Parse(cachedDNSInfo.Port);
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Retrying with cached DNS IP Address {1} and port {2}", args0: _connectionId, args1: cachedDNSInfo.AddrIPv4, args2: cachedDNSInfo.Port);

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
                                        _socket = TryConnectParallel(firstCachedIP, portRetry, timeout, ref reportError, cachedFQDN, ref pendingDNSInfo);
                                    }
                                    else
                                    {
                                        _socket = Connect(firstCachedIP, portRetry, timeout, ipPreference, cachedFQDN, ref pendingDNSInfo);
                                    }
                                }
                                catch (Exception exRetry)
                                {
                                    if (timeout.IsExpired)
                                    {
                                        throw;
                                    }
                                    if (exRetry is SocketException || exRetry is ArgumentNullException
                                        || exRetry is ArgumentException || exRetry is ArgumentOutOfRangeException || exRetry is AggregateException)
                                    {
                                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Retrying exception {1}", args0: _connectionId, args1: exRetry?.Message);
                                        if (parallel)
                                        {
                                            _socket = TryConnectParallel(secondCachedIP, portRetry, timeout, ref reportError, cachedFQDN, ref pendingDNSInfo);
                                        }
                                        else
                                        {
                                            _socket = Connect(secondCachedIP, portRetry, timeout, ipPreference, cachedFQDN, ref pendingDNSInfo);
                                        }
                                    }
                                    else
                                    {
                                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Retry failed, exception occurred: {1}", args0: _connectionId, args1: exRetry?.Message);
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
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0} could not be opened, exception occurred: {1}", args0: _connectionId, args1: Strings.SNI_ERROR_40);
                            ReportTcpSNIError(0, SniCommon.ConnOpenFailedError, Strings.SNI_ERROR_40);
                        }
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0} Socket could not be opened.", args0: _connectionId);
                        return;
                    }

                    _socket.NoDelay = true;
                    _tcpStream = new SniNetworkStream(_socket, true);

                    Stream stream = _tcpStream;
                    if (!_tlsFirst)
                    {
                        _sslOverTdsStream = new SslOverTdsStream(_tcpStream, _connectionId);
                        stream = _sslOverTdsStream;
                    }
                    _sslStream = new SniSslStream(stream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate));
                }
                catch (SocketException se)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0} Socket exception occurred: Error Code {1}, Message {2}", args0: _connectionId, args1: se?.SocketErrorCode, args2: se?.Message);
                    ReportTcpSNIError(se);
                    return;
                }
                catch (Exception e)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0} Exception occurred: {1}", args0: _connectionId, args1: e?.Message);
                    ReportTcpSNIError(e);
                    return;
                }

                _stream = _tcpStream;
                _status = TdsEnums.SNI_SUCCESS;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0} Socket opened successfully, TCP stream ready.", args0: _connectionId);
            }
        }

        // Connect to server with hostName and port in parellel mode.
        // The IP information will be collected temporarily as the pendingDNSInfo but is not stored in the DNS cache at this point.
        // Only write to the DNS cache when we receive IsSupported flag as true in the Feature Ext Ack from server.
        private Socket TryConnectParallel(string hostName, int port, TimeoutTimer timeout, ref bool callerReportError, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo)
        {
            using (TrySNIEventScope.Create(nameof(SniTcpHandle)))
            {
                Socket availableSocket = null;
                bool isInfiniteTimeOut = timeout.IsInfinite;

                IPAddress[] serverAddresses = isInfiniteTimeOut
                        ? SniCommon.GetDnsIpAddresses(hostName)
                        : SniCommon.GetDnsIpAddresses(hostName, timeout);

                if (serverAddresses.Length > MaxParallelIpAddresses)
                {
                    // Fail if above 64 to match legacy behavior
                    callerReportError = false;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0} serverAddresses.Length {1} Exception: {2}", args0: _connectionId, args1: serverAddresses.Length, args2: Strings.SNI_ERROR_47);
                    ReportTcpSNIError(0, SniCommon.MultiSubnetFailoverWithMoreThan64IPs, Strings.SNI_ERROR_47);
                    return availableSocket;
                }

                availableSocket = ParallelConnect(serverAddresses, port, timeout, cachedFQDN, ref pendingDNSInfo);

                return availableSocket;
            }
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
        private static Socket Connect(string serverName, int port, TimeoutTimer timeout, SqlConnectionIPAddressPreference ipPreference, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo)
        {
            using (TrySNIEventScope.Create(nameof(SniTcpHandle)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "IP preference : {0}", Enum.GetName(typeof(SqlConnectionIPAddressPreference), ipPreference));
                bool isInfiniteTimeout = timeout.IsInfinite;

                IEnumerable<IPAddress> ipAddresses = GetHostAddressesSortedByPreference(serverName, ipPreference);

                SocketException lastSocketException = null;

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

                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
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
                                                                      socketException.SocketErrorCode == SocketError.WouldBlock)
                        {
                            // https://github.com/dotnet/SqlClient/issues/826#issuecomment-736224118
                            // Socket.Select is used because it supports timeouts, while Socket.Connect does not

                            List<Socket> checkReadLst;
                            List<Socket> checkWriteLst;
                            List<Socket> checkErrorLst;

                            // Repeating Socket.Select several times if our timeout is greater
                            // than int.MaxValue microseconds because of 
                            // https://github.com/dotnet/SqlClient/pull/1029#issuecomment-875364044
                            // which states that Socket.Select can't handle timeouts greater than int.MaxValue microseconds
                            do
                            {
                                if (timeout.IsExpired)
                                {
                                    throw new Win32Exception(
                                        TdsEnums.SNI_WAIT_TIMEOUT,
                                        StringsHelper.GetString(Strings.SQL_ConnectTimeout));
                                }

                                int socketSelectTimeout =
                                    checked((int)(Math.Min(timeout.MillisecondsRemainingInt, int.MaxValue / 1000) * 1000));

                                checkReadLst = new List<Socket>(1) { socket };
                                checkWriteLst = new List<Socket>(1) { socket };
                                checkErrorLst = new List<Socket>(1) { socket };

                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                                    "Determining the status of the socket during the remaining timeout of {0} microseconds.",
                                    socketSelectTimeout);

                                Socket.Select(checkReadLst, checkWriteLst, checkErrorLst, socketSelectTimeout);
                                // nothing selected means timeout
                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                                    "Socket.Select results: checkReadLst.Count: {0}, checkWriteLst.Count: {1}, checkErrorLst.Count: {2}",
                                    checkReadLst.Count, checkWriteLst.Count, checkErrorLst.Count);
                            } while (checkReadLst.Count == 0 && checkWriteLst.Count == 0 && checkErrorLst.Count == 0);

                            // workaround: false positive socket.Connected on linux: https://github.com/dotnet/runtime/issues/55538
                            isConnected = socket.Connected && checkErrorLst.Count == 0;
                            if (!isConnected)
                            {
                                // Retrieve the socket error code
                                int socketErrorCode = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                                SocketError socketError = (SocketError)socketErrorCode;

                                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR,
                                "Socket connection failed. SocketError: {0} ({1})", socketError, socketErrorCode);

                                lastSocketException = new SocketException(socketErrorCode);
                            }
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
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                                "Connected to socket: {0}", socket.RemoteEndPoint);
                            return socket;
                        }
                    }
                    catch (SocketException e)
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent(
                            "{0}.{1}{2}THIS EXCEPTION IS BEING SWALLOWED: {3}",
                            nameof(SniTcpHandle), nameof(Connect), EventType.ERR, e);
                        lastSocketException = e;
                    }
                    finally
                    {
                        if (!isSocketSelected)
                        {
                            socket?.Dispose();
                        }
                    }
                }

                if (lastSocketException != null)
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        "{0}.{1}{2}Last Socket Exception: {3}",
                        nameof(SniTcpHandle), nameof(Connect), EventType.ERR, lastSocketException);
                    throw lastSocketException;
                }

                return null;
            }
        }

        private static Socket ParallelConnect(IPAddress[] serverAddresses, int port, TimeoutTimer timeout, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo)
        {
            using (TrySNIEventScope.Create(nameof(SniTcpHandle)))
            {
                if (serverAddresses == null)
                {
                    throw new ArgumentNullException(nameof(serverAddresses));
                }
                if (serverAddresses.Length == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(serverAddresses));
                }

                Dictionary<Socket, IPAddress> sockets = new(serverAddresses.Length);
                Socket connectedSocket = null;

                foreach (IPAddress address in serverAddresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        Blocking = false
                    };
                    sockets.Add(socket, address);

                    // enable keep-alive on socket
                    SetKeepAliveValues(ref socket);

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                        "Connecting to IP address {0} and port {1} using {2} address family. Is infinite timeout: {3}",
                        address,
                        port,
                        address.AddressFamily,
                        timeout.IsInfinite);

                    try // catching SocketException with SocketErrorCode == WouldBlock to run Socket.Select
                    {
                        socket.Connect(address, port);
                        throw SQL.SocketDidNotThrow();
                    }
                    catch (SocketException socketException) when (socketException.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                }

                // The code between here and Socket.Select is all for setting up the Socket.Select method.
                // https://github.com/dotnet/SqlClient/issues/826#issuecomment-736224118
                // Socket.Select is used because it supports timeouts, while Socket.Connect does not
                // Socket.Select also allows us to wait for multiple sockets using a single thread
                // Socket.Select will return as soon any any socket in the list meets read/write/error

                List<Socket> socketsInFlight = new List<Socket>(sockets.Count);
                socketsInFlight.AddRange(sockets.Keys);
                int socketSelectTimeout;
                List<Socket> checkReadLst = new(socketsInFlight.Count);
                List<Socket> checkWriteLst = new(socketsInFlight.Count);
                List<Socket> checkErrorLst = new(socketsInFlight.Count);
                SocketException lastError = null;

                // Each time the loop repeats, we will either end with a connected socket or an errored socket
                // A connected socket results in all other sockets getting disposed and returning that socket
                // An errored socket results in that socket being removed from socketsInFlight and repeating the loop
                while (connectedSocket == null && socketsInFlight.Count > 0 && !timeout.IsExpired)
                {
                    try
                    {
                        // Repeating Socket.Select several times if our timeout is greater
                        // than int.MaxValue microseconds because of 
                        // https://github.com/dotnet/SqlClient/pull/1029#issuecomment-875364044
                        // which states that Socket.Select can't handle timeouts greater than int.MaxValue microseconds
                        do
                        { // timeout loop for when select timed out but no sockets have changed state
                          // Socket.Select timeout is in microseconds and can only handle up to int.MaxValue
                          // This means our connection timeout can be greater than the max select timeout
                            socketSelectTimeout =
                                checked((int)(Math.Min(timeout.MillisecondsRemainingInt, int.MaxValue / 1000) * 1000));

                            checkReadLst.Clear();
                            checkReadLst.AddRange(socketsInFlight);
                            checkWriteLst.Clear();
                            checkWriteLst.AddRange(socketsInFlight);
                            checkErrorLst.Clear();
                            checkErrorLst.AddRange(socketsInFlight);

                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                                "Watching pending sockets during the remaining timeout of {0} microseconds.",
                                socketSelectTimeout);

                            // Socket.Select will return as soon as any socket is readable, writable, or errored
                            Socket.Select(checkReadLst, checkWriteLst, checkErrorLst, socketSelectTimeout);
                            // nothing selected means select timed out
                        } while (checkReadLst.Count == 0 && checkWriteLst.Count == 0 && checkErrorLst.Count == 0 && !timeout.IsExpired);
                        foreach (Socket socket in checkErrorLst)
                        {
                            // Retrieve the socket error code
                            int socketErrorCode = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                            SocketError socketError = (SocketError)socketErrorCode;

                            // Log any failed sockets
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                            "Socket connection failed for {0}. SocketError: {1} ({2})",
                            sockets[socket], socketError, socketErrorCode);

                            lastError = new SocketException(socketErrorCode);
                        }

                    }
                    catch (SocketException e)
                    {
                        // Socket.Select can throw if one of the sockets has issues. That socket will be in checkErrorLst.
                        // Log the error and let that socket be removed from socketsInFlight below.
                        SqlClientEventSource.Log.TryAdvancedTraceEvent(
                            "{0}.{1}{2}THIS EXCEPTION IS BEING SWALLOWED: {3}", nameof(SniTcpHandle), nameof(ParallelConnect), EventType.ERR, e);
                        lastError = e;
                    }

                    if (timeout.IsExpired)
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent(
                            "{0}.{1}{2}ParallelConnect timeout expired.", nameof(SniTcpHandle), nameof(ParallelConnect), EventType.INFO);
                        // We will throw below after cleanup
                        break;
                    }

                    // As the client, we only care about the write list
                    foreach (Socket s in checkWriteLst)
                    {
                        // workaround: false positive socket.Connected on linux: https://github.com/dotnet/runtime/issues/55538
                        if (!checkErrorLst.Contains(s) && s.Connected)
                        {
                            connectedSocket = s;
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO,
                                "Connected to endpoint: {0}", connectedSocket.RemoteEndPoint);
                            connectedSocket.Blocking = true;
                            string iPv4String = null;
                            string iPv6String = null;
                            if (connectedSocket.AddressFamily == AddressFamily.InterNetwork)
                            {
                                iPv4String = ((IPEndPoint)connectedSocket.RemoteEndPoint).Address.ToString();
                            }
                            else
                            {
                                iPv6String = ((IPEndPoint)connectedSocket.RemoteEndPoint).Address.ToString();
                            }
                            pendingDNSInfo = new SQLDNSInfo(cachedFQDN, iPv4String, iPv6String, port.ToString());
                            break;
                        }
                    }

                    if (connectedSocket == null)
                    {
                        // Remove any remaining (unsuccessful) sockets from socketsInFlight so that we
                        // loop again on any remaining socketsInFlight

                        foreach (Socket socket in checkErrorLst)
                        {
                            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                                "{0}.{1}{2}Failed to connect to endpoint: {3}. Error: {4}", nameof(SniTcpHandle),
                                nameof(ParallelConnect), EventType.INFO, sockets[socket], lastError);
                            socketsInFlight.Remove(socket);
                        }
                        // Read/write lists could contain sockets that indicated Connected == false above
                        checkWriteLst.AddRange(checkReadLst);
                        foreach (Socket socket in checkWriteLst)
                        {
                            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                                "{0}.{1}{2}Failed to connect to endpoint: {3}. Error: {4}", nameof(SniTcpHandle),
                                nameof(ParallelConnect), EventType.INFO, sockets[socket], lastError);
                            socketsInFlight.Remove(socket);
                        }
                    }
                }

                // Dispose unused sockets
                foreach (Socket socket in sockets.Keys)
                {
                    if (socket != connectedSocket)
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent(
                            "{0}.{1}{2}Disposing non-selected socket for endpoint: {3}", nameof(SniTcpHandle),
                            nameof(ParallelConnect), EventType.INFO, sockets[socket]);
                        socket?.Dispose();
                    }
                }

                if (connectedSocket == null)
                {
                    if (timeout.IsExpired)
                    {
                        throw new Win32Exception(
                            TdsEnums.SNI_WAIT_TIMEOUT,
                            StringsHelper.GetString(Strings.SQL_ConnectTimeout));
                    }

                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        "{0}.{1}{2} No socket connections succeeded. Last error: {3}",
                        nameof(SniTcpHandle), nameof(ParallelConnect), EventType.ERR, lastError);

                    if (lastError != null)
                    {
                        throw lastError;
                    }
                }

                return connectedSocket;
            }
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public override uint EnableSsl(uint options)
        {
            using (TrySNIEventScope.Create(nameof(SniHandle)))
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
                        _sslStream.AuthenticateAsClient(_targetServer, null, s_supportedProtocols, false);
                    }
                    if (_sslOverTdsStream is not null)
                    {
                        _sslOverTdsStream.FinishHandshake();
                    }
                }
                catch (AuthenticationException aue)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Authentication exception occurred: {1}", args0: _connectionId, args1: aue?.Message);
                    return ReportTcpSNIError(aue, SniError.CertificateValidationErrorCode);
                }
                catch (InvalidOperationException ioe)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Invalid Operation Exception occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                    return ReportTcpSNIError(ioe);
                }

                _stream = _sslStream;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, SSL enabled successfully.", args0: _connectionId);
                return TdsEnums.SNI_SUCCESS;
            }
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public override void DisableSsl()
        {
            using (TrySNIEventScope.Create(nameof(SniTcpHandle)))
            {
                _sslStream.Dispose();
                _sslStream = null;
                _sslOverTdsStream?.Dispose();
                _sslOverTdsStream = null;
                _stream = _tcpStream;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, SSL Disabled. Communication will continue on TCP Stream.", args0: _connectionId);
            }
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
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Certificate will not be validated.", args0: _connectionId);
                return true;
            }

            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Certificate will be validated for Target Server name", args0: _connectionId);
            return SniCommon.ValidateSslServerCertificate(_connectionId, _targetServer, _hostNameInCertificate, serverCertificate, _serverCertificateFilename, policyErrors);
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
        public override uint Send(SniPacket packet)
        {
            bool releaseLock = false;
            try
            {
                // is the packet is marked out out-of-band (attention packets only) it must be
                // sent immediately even if a send or receive operation is already in progress
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
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Data sent to stream synchronously", args0: _connectionId);
                        return TdsEnums.SNI_SUCCESS;
                    }
                    catch (ObjectDisposedException ode)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}", args0: _connectionId, args1: ode?.Message);
                        return ReportTcpSNIError(ode);
                    }
                    catch (SocketException se)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, SocketException occurred: {1}", args0: _connectionId, args1: se?.Message);
                        return ReportTcpSNIError(se);
                    }
                    catch (IOException ioe)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, IOException occurred: {1}", args0: _connectionId, args1: ioe?.Message);
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
        /// Receives a packet synchronously.
        /// </summary>
        /// <param name="packet">The received SNI packet.</param>
        /// <param name="timeoutInMilliseconds">
        /// Timeout in milliseconds:
        /// - If greater than 0, sets the socket's receive timeout to the specified value.
        /// - If equal to -1, represents an infinite timeout (socket timeout is set to 0).
        /// - If less than -1 or equal to 0, results in a timeout error.
        /// </param>
        /// <returns>SNI error code indicating the result of the operation.</returns>
        public override uint Receive(out SniPacket packet, int timeoutInMilliseconds)
        {
            SniPacket errorPacket;
            lock (this)
            {
                packet = null;

                if (_socket == null)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Socket is null.", args0: _connectionId);
                    return ReportTcpSNIError(0, SniCommon.ConnOpenFailedError, Strings.SNI_ERROR_10);
                }

                try
                {
                    if (timeoutInMilliseconds > 0)
                    {
                        _socket.ReceiveTimeout = timeoutInMilliseconds;
                    }
                    else if (timeoutInMilliseconds == -1)
                    {
                        _socket.ReceiveTimeout = Timeout.Infinite;
                    }
                    else
                    {
                        // otherwise it is timeout for 0 or less than -1
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Error 258, Timeout error occurred.", args0: _connectionId);
                        ReportTcpSNIError(0, SniCommon.ConnTimeoutError, Strings.SNI_ERROR_11);
                        return TdsEnums.SNI_WAIT_TIMEOUT;
                    }

                    packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
                    packet.ReadFromStream(_stream);

                    if (packet.Length == 0)
                    {
                        errorPacket = packet;
                        packet = null;
                        var e = new Win32Exception();
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Win32 exception occurred: {1}", args0: _connectionId, args1: e?.Message);
                        return ReportErrorAndReleasePacket(errorPacket, e.NativeErrorCode, 0, e.Message);
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Data read from stream synchronously", args0: _connectionId);
                    return TdsEnums.SNI_SUCCESS;
                }
                catch (ObjectDisposedException ode)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, ObjectDisposedException occurred: {1}", args0: _connectionId, args1: ode?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, ode);
                }
                catch (SocketException se)
                {
                    errorPacket = packet;
                    packet = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, Socket exception occurred: {1}", args0: _connectionId, args1: se?.Message);
                    return ReportErrorAndReleasePacket(errorPacket, se);
                }
                catch (IOException ioe)
                {
                    errorPacket = packet;
                    packet = null;
                    uint errorCode = ReportErrorAndReleasePacket(errorPacket, ioe);
                    if (ioe.InnerException is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut)
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, IO exception occurred with Wait Timeout (error 258): {1}", args0: _connectionId, args1: ioe?.Message);
                        errorCode = TdsEnums.SNI_WAIT_TIMEOUT;
                    }

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.ERR, "Connection Id {0}, IO exception occurred: {1}", args0: _connectionId, args1: ioe?.Message);
                    return errorCode;
                }
                finally
                {
                    const int resetTimeout = Timeout.Infinite;

                    try
                    {
                        // Reset the socket timeout to Timeout.Infinite after
                        // the receive operation is done to avoid blocking the
                        // thread in case of a timeout error.
                        _socket.ReceiveTimeout = resetTimeout;

                    }
                    catch (SocketException ex)
                    {
                        // We sometimes see setting the ReceiveTimeout fail
                        // on macOS. There's isn't much we can do about it
                        // though, so just log and move on.
                        SqlClientEventSource.Log.TrySNITraceEvent(
                            nameof(SniTcpHandle),
                            EventType.ERR,
                            "Connection Id {0}, Failed to reset socket " +
                            "receive timeout to {1}: {2}",
                            _connectionId,
                            resetTimeout,
                            ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        public override void SetAsyncCallbacks(SniAsyncCallback receiveCallback, SniAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint SendAsync(SniPacket packet)
        {
            using (TrySNIEventScope.Create(nameof(SniTcpHandle)))
            {
                packet.WriteToStreamAsync(_stream, _sendCallback, SniProviders.TCP_PROV);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Data sent to stream asynchronously", args0: _connectionId);
                return TdsEnums.SNI_SUCCESS_IO_PENDING;
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override uint ReceiveAsync(ref SniPacket packet)
        {
            SniPacket errorPacket;
            packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
            packet.SetAsyncIOCompletionCallback(_receiveCallback);
            try
            {
                packet.ReadFromStreamAsync(_stream);
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Data received from stream asynchronously", args0: _connectionId);
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
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Socket not usable.", args0: _connectionId);
                    return TdsEnums.SNI_ERROR;
                }
            }
            catch (SocketException se)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Socket Exception occurred: {1}", args0: _connectionId, args1: se?.Message);
                return ReportTcpSNIError(se);
            }
            catch (ObjectDisposedException ode)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, ObjectDisposedException occurred: {1}", args0: _connectionId, args1: ode?.Message);
                return ReportTcpSNIError(ode);
            }

            return TdsEnums.SNI_SUCCESS;
        }

        private uint ReportTcpSNIError(Exception sniException, int nativeErrorCode = 0)
        {
            _status = TdsEnums.SNI_ERROR;
            return SniCommon.ReportSNIError(SniProviders.TCP_PROV, SniCommon.InternalExceptionError, sniException, nativeErrorCode);
        }

        private uint ReportTcpSNIError(int nativeError, uint sniError, string errorMessage)
        {
            _status = TdsEnums.SNI_ERROR;
            return SniCommon.ReportSNIError(SniProviders.TCP_PROV, nativeError, sniError, errorMessage);
        }

        private uint ReportErrorAndReleasePacket(SniPacket packet, Exception sniException)
        {
            if (packet != null)
            {
                ReturnPacket(packet);
            }
            return ReportTcpSNIError(sniException);
        }

        private uint ReportErrorAndReleasePacket(SniPacket packet, int nativeError, uint sniError, string errorMessage)
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
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 1);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
        }
    }
}

#endif
