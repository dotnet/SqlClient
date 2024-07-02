// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.TransportCreationSubHandlers
{
    /// <summary>
    /// Handler for connecting via TCP.
    /// </summary>
    internal sealed class TcpTransportCreationHandler : IReturningHandler<ConnectionHandlerContext, Stream>
    {
        private const int KeepAliveIntervalSeconds = 1;
        private const int KeepAliveTimeSeconds = 30;

        #if NET8_0_OR_GREATER
        private static readonly TimeSpan DefaultPollTimeout = TimeSpan.FromSeconds(30);
        #else
        private const int DefaultPollTimeout = 30 * 1_000_000; // 30 seconds as microseconds
        #endif

        /// <inheritdoc />
        public async ValueTask<Stream> Handle(ConnectionHandlerContext parameters, bool isAsync, CancellationToken ct)
        {
            // This handler cannot process if the protocol does not contain TCP
            if (parameters.DataSource.ResolvedProtocol is not (DataSource.Protocol.Admin or DataSource.Protocol.TCP or DataSource.Protocol.None))
            {
                return null;
            }

            ct.ThrowIfCancellationRequested();

            // DNS lookup
            IPAddress[] ipAddresses = isAsync
                ? await Dns.GetHostAddressesAsync(parameters.DataSource.ServerName, ct).ConfigureAwait(false)
                : Dns.GetHostAddresses(parameters.DataSource.ServerName);
            if (ipAddresses is null || ipAddresses.Length == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            // If there is an IP version preference, apply it
            switch (parameters.ConnectionString.IPAddressPreference)
            {
                case SqlConnectionIPAddressPreference.IPv4First:
                    Array.Sort(ipAddresses, IpAddressVersionComparer.InstanceV4);
                    break;

                case SqlConnectionIPAddressPreference.IPv6First:
                    Array.Sort(ipAddresses, IpAddressVersionComparer.InstanceV6);
                    break;
            }

            // Attempt to connect to one of the matching IP addresses
            // @TODO: Handle opening in parallel
            Socket socket = null;
            var socketOpenExceptions = new List<Exception>();

            int portToUse = parameters.DataSource.ResolvedPort < 0
                ? parameters.DataSource.Port
                : parameters.DataSource.ResolvedPort;
            var ipEndPoint = new IPEndPoint(IPAddress.None, portToUse); // Allocate once
            foreach (IPAddress ipAddress in ipAddresses)
            {
                ipEndPoint.Address = ipAddress;
                try
                {
                    socket = await OpenSocket(ipEndPoint, isAsync, ct).ConfigureAwait(false);
                    break;
                }
                catch(Exception e)
                {
                    socketOpenExceptions.Add(e);
                }
            }

            // If no socket succeeded, throw
            if (socket is null)
            {
                // If there are any socket exceptions in the collected exceptions, throw the first
                // one. If there are not, collect all exceptions and throw them as an aggregate.
                foreach (Exception exception in socketOpenExceptions)
                {
                    if (exception is SocketException)
                    {
                        throw exception;
                    }
                }

                throw new AggregateException(socketOpenExceptions);
            }

            // Create the stream for the socket
            return new NetworkStream(socket, ownsSocket: true);
        }

        private async ValueTask<Socket> OpenSocket(IPEndPoint ipEndPoint, bool isAsync, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { Blocking = false };

            // Enable keep-alive
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveTimeSeconds);

            try
            {
                if (isAsync)
                {
                    await socket.ConnectAsync(ipEndPoint, ct).ConfigureAwait(false);
                }
                else
                {
                    OpenSocketSync(socket, ipEndPoint, ct);
                }
            }
            catch (Exception)
            {
                socket.Dispose();
                throw;
            }

            // Connection is established
            socket.Blocking = true;
            socket.NoDelay = true;

            return socket;
        }

        private void OpenSocketSync(Socket socket, IPEndPoint ipEndPoint, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Note: Although it seems logical to dispose the socket (ie, cancel connecting) if
                //   the cancellation token fires, we don't need to do that here. Since the socket
                //   is set to be non-blocking, once we call connect we'll throw and move onto
                //   polling where we continuously check the cancellation token.
                socket.Connect(ipEndPoint);
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.WouldBlock)
            {
                // Because the socket is configured to be non-blocking, any operation that would
                // block will throw an exception indicating it would block. Since opening a TCP
                // connection will always block, we expect to get an exception for it, and will
                // ignore it. This allows us to immediately return from connect and poll it,
                // allowing us to observe timeouts and cancellation.
            }
            
            try
            {
                // Poll the socket until it is open. If the cancellation token fires, we will
                // dispose of the socket, effectively cancelling the polling.
                using (ct.Register(socket.Dispose))
                {
                    if (!socket.Poll(DefaultPollTimeout, SelectMode.SelectWrite))
                    {
                        throw new TimeoutException();
                    }
                }
            }
            catch (SocketException se) when (se.ErrorCode is (int)SocketError.Interrupted)
            {
                // We get a special exception if the cancellation token timed out. If the
                // cancellation token triggered it, use that exception instead.
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
