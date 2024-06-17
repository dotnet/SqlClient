// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers.Connection;

namespace Microsoft.Data.SqlClientX.Handlers.TransportCreation
{
    internal class TransportCreationHandler : IHandler<ConnectionHandlerContext>
    {
        #if NET8_0_OR_GREATER
        private static readonly TimeSpan DefaultPollTimeout = TimeSpan.FromSeconds(30);
        #else
        private const int DefaultPollTimeout = 30 * 100000; // 30 seconds as microseconds
        #endif

        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            if (context.DataSource is null)
            {
                context.Error = new ArgumentNullException(nameof(context));
                return;
            }

            try
            {
                // @TODO: Build CoR for handling the different protocols in order
                if (context.DataSource.ResolvedProtocol is DataSource.Protocol.TCP)
                {
                    context.ConnectionStream = isAsync
                        ? await HandleTcpRequest(context, isAsync, ct).ConfigureAwait(false)
                        : HandleTcpRequest(context, isAsync, ct).Result;
                }
            }
            catch (Exception e)
            {
                context.Error = e;
            }


            if (NextHandler is not null)
            {
                if (isAsync)
                {
                    await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
                }
                else
                {
                    NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
        }

        private ValueTask<Stream> HandleNamedPipeRequest()
        {
            throw new NotImplementedException();
        }

        private ValueTask<Stream> HandleSharedMemoryRequest()
        {
            throw new NotImplementedException();
        }

        private async ValueTask<Stream> HandleTcpRequest(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // DNS lookup
            var ipAddresses = isAsync
                ? await Dns.GetHostAddressesAsync(context.DataSource.ServerName, ct).ConfigureAwait(false)
                : Dns.GetHostAddresses(context.DataSource.ServerName);
            if (ipAddresses.Length == 0)
            {
                throw new Exception("Hostname did not resolve");
            }

            // If there is an IP version preference, apply it
            switch (context.IpAddressPreference)
            {
                case SqlConnectionIPAddressPreference.IPv4First:
                    Array.Sort(ipAddresses, IpAddressVersionSorter.InstanceV4);
                    break;

                case SqlConnectionIPAddressPreference.IPv6First:
                    Array.Sort(ipAddresses, IpAddressVersionSorter.InstanceV6);
                    break;

                case SqlConnectionIPAddressPreference.UsePlatformDefault:
                default:
                    // Not sorting necessary
                    break;
            }

            // Attempt to connect to one of the matching IP addresses
            // @TODO: Handle opening in parallel
            Socket socket = null;
            var socketOpenExceptions = new List<Exception>();

            var ipEndpoint = new IPEndPoint(IPAddress.None, context.DataSource.Port); // Allocate once
            foreach (var ipAddress in ipAddresses)
            {
                ipEndpoint.Address = ipAddress;
                try
                {
                    socket = await OpenSocketAsync(ipEndpoint, ct).ConfigureAwait(false);
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
                throw socketOpenExceptions.OfType<SocketException>().FirstOrDefault()
                      ?? (Exception)new AggregateException(socketOpenExceptions);
            }

            // Create the stream for the socket
            return new NetworkStream(socket);
        }

        private async ValueTask<Socket> OpenSocketAsync(IPEndPoint ipEndpoint, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false,
                NoDelay = true,
            };

            // Enable keep-alive
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 1);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);

            // Open the socket
            try
            {
                #if NET6_0_OR_GREATER
                await socket.ConnectAsync(ipEndpoint, ct).ConfigureAwait(false);
                #else
                await new TaskFactory(ct).FromAsync(socket.BeginConnect, socket.EndConnect, ipEndpoint, null)
                    .ConfigureAwait(false);
                #endif
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode is not SocketError.WouldBlock)
                {
                    throw;
                }
            }

            // Verify the socket is open
            if (!socket.Poll(DefaultPollTimeout, SelectMode.SelectWrite))
            {
                throw new Exception("Connection not ready for writing");
            }

            // Connection is established
            socket.Blocking = true;
            socket.NoDelay = true;

            return socket;
        }
    }
}
