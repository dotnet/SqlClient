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

        internal bool ExpectTlsHandshakeFailure { get; set; }

        public TDSServerEndPointConnection(ITDSServer server, TcpClient connection)
            : base(server, connection)
        {
        }

        public override void PrepareForProcessingData(Stream rawStream)
        {
            // Create a server TDS parser
            Debug.Assert(_parser == null, "PrepareForProcessingData should not be called twice");
            _parser = new TDSServerParser(Server, Session, rawStream)
            {
                ExpectTlsHandshakeFailure = ExpectTlsHandshakeFailure
            };
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
        /// Signals the processor loop to stop.
        /// </summary>
        private volatile bool _stopRequested;

        internal Exception ProcessingException { get; private set; }

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
        /// Initialization constructor
        /// </summary>
        public ServerEndPointConnection(ITDSServer server, TcpClient connection)
        {
            // Save server
            Server = server;

            // Save TCP connection
            Connection = connection;

            // Note: no artificial socket receive timeout is configured.  A blocked
            // read is unblocked deterministically when the socket is closed during
            // Dispose().  A short receive timeout here previously aborted parsing
            // mid-message under CI CPU load, which was a source of test flakiness.

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
            // Socket reads and simulated delays block. They must not starve the client
            // opens and continuations sharing the process-wide thread pool.
            ProcessorTask = Task.Factory.StartNew(RunConnectionHandler,
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
            // Signal the processor loop to stop.
            _stopRequested = true;

            // Close the socket first.  This unblocks any synchronous read or poll
            // the processor task may be waiting on, causing it to exit its loop.
            Connection?.Close();

            // Deterministically wait for the processor task to finish so that no
            // background work (socket I/O, counter mutation) outlives Dispose().
            // A bounded wait guards against an unexpected hang.
            if (ProcessorTask != null && !ProcessorTask.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("TDS connection processor did not stop during Dispose.");
            }
            if (ProcessingException != null)
            {
                throw new InvalidOperationException("TDS connection processor failed.", ProcessingException);
            }
        }

        /// <summary>
        /// Worker thread that processes the TDS packet stream for this connection.
        /// </summary>
        private void RunConnectionHandler()
        {
            TcpClient connection = Connection;

            try
            {
                // Get network stream
                Socket socket = connection.Client;
                NetworkStream rawStream = connection.GetStream();
                PrepareForProcessingData(rawStream);

                // Process the packet sequence until the peer disconnects or the
                // server requests a stop.
                while (!_stopRequested && connection.Connected)
                {
                    // Check incoming buffer
                    if (rawStream.DataAvailable)
                    {
                        ProcessData(rawStream);
                    }
                    else if (socket.Poll(100_000 /* 100 ms */, SelectMode.SelectRead)
                        && !rawStream.DataAvailable)
                    {
                        // Poll reports readable with no data available when the
                        // peer has closed the connection.
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException ||
                (_stopRequested && ex is ObjectDisposedException))
            {
                // A socket close during Dispose is an expected part of teardown, so only log
                // when a stop was not requested. This keeps real failures visible in CI logs
                // without the teardown noise.
                if (!_stopRequested)
                {
                    Log(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                ProcessingException = ex;
                Log(ex.ToString());
            }
            finally
            {
                connection.Close();
                try
                {
                    Server.CloseSession(Session);
                }
                catch (Exception ex)
                {
                    ProcessingException = ProcessingException == null
                        ? ex
                        : new AggregateException(ProcessingException, ex);
                }
                OnConnectionClosed?.Invoke(this, EventArgs.Empty);
            }
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
