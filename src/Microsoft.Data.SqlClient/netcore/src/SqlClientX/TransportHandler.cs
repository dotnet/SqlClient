using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;

namespace Microsoft.Data.SqlClient.SqlClientX
{
    internal class TransportHandler
    {
        public TransportHandler() { }


    }

    internal class TcpHandler : TransportHandler
    {
        private string _hostname;
        private int _port;

        public TcpHandler(string hostname, int port = 1433) {
            this._hostname = hostname;
            this._port = port;
        }

        public async ValueTask<NetworkStream> Connect(bool isAsync, CancellationToken ct)
        {
            IEnumerable<IPAddress> ipAddresses = await Dns.GetHostAddressesAsync(_hostname);
            IPAddress ipToConnect = ipAddresses.First((ipaddr) => ipaddr.AddressFamily == AddressFamily.InterNetwork);
            
            Socket socket = new Socket(ipToConnect.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false // We want to block until the connection is established
            };

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 1);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);

            try
            {
                // Now we have a TCP connection to the server.
                socket.Connect(ipToConnect, _port);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock)
                    throw;
            }

            var write = new List<Socket> { socket };
            var error = new List<Socket> { socket };
            Socket.Select(null, write, error, 30000000); // Wait for 30 seconds 
            if (write.Count > 0)
            {
                //Console.WriteLine("WE have a socket");
                //Console.WriteLine(socket.Connected);
                bool connected = socket.Connected;
                if (!connected)
                    Console.WriteLine("Socket not conencted ");
                // Connection established
                socket.Blocking = true;
                socket.NoDelay = true;
            }
            else
            {
                throw new Exception("Connection failed");
            }
            //socket.NoDelay = true;

            return new NetworkStream(socket, true);


        }

    }
}
