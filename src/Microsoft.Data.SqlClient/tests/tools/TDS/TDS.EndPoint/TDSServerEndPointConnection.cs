// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SqlServer.TDS.EndPoint
{
    /// <summary>
    /// A delegate for client connection termination
    /// </summary>
    public delegate void ConnectionClosedEventHandler(object sender, EventArgs e);

    /// <summary>
    /// Connection to a single client that handles TDS data
    /// </summary>
    public class TDSServerEndPointConnection : ServerEndPointConnection
    {
        private TDSServerParser _parser;

        public TDSServerEndPointConnection(ITDSServer server, TcpClient connection)
            : base(server, connection)
        {
        }

        public override void PrepareForProcessingData(Stream rawStream)
        {
            // Create a server TDS parser
            Debug.Assert(_parser == null, "PrepareForProcessingData should not be called twice");
            _parser = new TDSServerParser(Server, Session, rawStream);
        }

        public override void ProcessData(Stream rawStream)
        {
            _parser.Run();
        }
    }

    /// <summary>
    /// Connection to a single client
    /// </summary>
    public abstract class ServerEndPointConnection : IDisposable
    {
        /// <summary>
        /// Worker thread
        /// </summary>
        protected Task ProcessorTask { get; set; }

        /// <summary>
        /// Gets/Sets the event log for the proxy server
        /// </summary>
        public TextWriter EventLog { get; set; }

        /// <summary>
        /// TDS Server to which this connection is established
        /// </summary>
        public ITDSServer Server { get; protected set; }

        /// <summary>
        /// TDS Server session that is assigned to this physical connection
        /// </summary>
        public ITDSServerSession Session { get; protected set; }

        /// <summary>
        /// Event that is fired when connection is closed
        /// </summary>
        public event ConnectionClosedEventHandler OnConnectionClosed;

        /// <summary>
        /// Connection itself
        /// </summary>
        protected TcpClient Connection { get; set; }

        /// <summary>
        /// Cancellation token source for managing cancellation of the processing thread
        /// </summary>
        private CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initialization constructor
        /// </summary>
        public ServerEndPointConnection(ITDSServer server, TcpClient connection)
        {
            // Save server
            Server = server;

            // Save TCP connection
            Connection = connection;

            // Configure timeouts
            Connection.ReceiveTimeout = 1000;

            // Create a new TDS server session
            Session = server.OpenSession();

            // Check if local end-point is recognized
            if (Connection.Client.LocalEndPoint is IPEndPoint)
            {
                // Cast to IP end-point
                IPEndPoint endPoint = Connection.Client.LocalEndPoint as IPEndPoint;

                // Update TDS session
                Session.ServerEndPointInfo = new TDSEndPointInfo(endPoint.Address, endPoint.Port, TDSEndPointTransportType.TCP);
            }

            // Check if remote end-point is recognized
            if (Connection.Client.RemoteEndPoint is IPEndPoint)
            {
                // Cast to IP end-point
                IPEndPoint endPoint = Connection.Client.RemoteEndPoint as IPEndPoint;

                // Update server context
                Session.ClientEndPointInfo = new TDSEndPointInfo(endPoint.Address, endPoint.Port, TDSEndPointTransportType.TCP);
            }
        }

        /// <summary>
        /// Start the connection 
        /// </summary>
        internal void Start()
        {
            // Prepare and start a thread
            ProcessorTask = RunConnectionHandler(CancellationTokenSource.Token);
        }

        /// <summary>
        /// Stop the connection
        /// </summary>
        internal void Stop()
        {
            CancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Called when the data processing thread is first started
        /// </summary>
        public abstract void PrepareForProcessingData(Stream rawStream);

        /// <summary>
        /// Called every time there is new data available
        /// </summary>
        public abstract void ProcessData(Stream rawStream);

        public void Dispose()
        {
            Stop();

            if (Connection != null)
            {
                Connection.Dispose();
            }

            CancellationTokenSource.Dispose();
        }

        /// <summary>
        /// Worker thread
        /// </summary>
        private async Task RunConnectionHandler(CancellationToken cancellationToken)
        {
            try
            {
                // Get network stream
                NetworkStream rawStream = Connection.GetStream();
                PrepareForProcessingData(rawStream);

                // Process the packet sequence
                while (Connection.Connected && !cancellationToken.IsCancellationRequested)
                {
                    // Check incoming buffer
                    if (rawStream.DataAvailable)
                    {
                        ProcessData(rawStream);
                    }
                    else
                    {
                        // Poll the socket for data
                        if (Connection.Client.Poll(100, SelectMode.SelectRead) && !rawStream.DataAvailable)
                        {
                            break;
                        }

                        // Sleep a bit to reduce load on CPU
                        await Task.Delay(10);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Log(ex.ToString());
            }

            try
            {
                // Disconnect the client
                Connection.Close();
            }
            catch (Exception)
            {
                // Do nothing there
            }

            // Notify subscribers that this connection is closed
            if (OnConnectionClosed != null)
            {
                OnConnectionClosed(this, null);
            }

            return;
        }

        /// <summary>
        /// Write a string to the log
        /// </summary>
        internal void Log(string text, params object[] args)
        {
            if (EventLog != null)
            {
                EventLog.WriteLine(text, args);
            }
        }
    }
}
