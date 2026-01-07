// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SplitPacketTest : IDisposable
    {
        private int _port = -1;
        private int _splitPacketSize = 1;
        private string _baseConnString;
        private TcpListener _listener;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public SplitPacketTest()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            DataSourceBuilder dataSourceBuilder = new DataSourceBuilder(builder.DataSource);

            Task.Factory.StartNew(() => { SetupProxy(dataSourceBuilder.ServerName, dataSourceBuilder.Port ?? 1433, _cts.Token); });

            for (int i = 0; i < 10 && _port == -1; i++)
            {
                Thread.Sleep(500);
            }

            if (_port == -1)
            {
                throw new InvalidOperationException("Proxy local port not defined!");
            }

            builder.DataSource = "tcp:127.0.0.1," + _port;
            _baseConnString = builder.ConnectionString;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsLocalHost), nameof(DataTestUtility.IsNotNamedInstance))]
        public void OneByteSplitTest()
        {
            _splitPacketSize = 1;
            OpenConnection();
            Assert.True(true);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsLocalHost), nameof(DataTestUtility.IsNotNamedInstance))]
        public void AlmostFullHeaderTest()
        {
            _splitPacketSize = 7;
            OpenConnection();
            Assert.True(true);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsLocalHost), nameof(DataTestUtility.IsNotNamedInstance))]
        public void FullHeaderTest()
        {
            _splitPacketSize = 8;
            OpenConnection();
            Assert.True(true);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsLocalHost), nameof(DataTestUtility.IsNotNamedInstance))]
        public void HeaderPlusOneTest()
        {
            _splitPacketSize = 9;
            OpenConnection();
            Assert.True(true);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsLocalHost), nameof(DataTestUtility.IsNotNamedInstance))]
        public void MARSSplitTest()
        {
            _splitPacketSize = 1;
            OpenMarsConnection("select * from Orders");
            Assert.True(true);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsLocalHost), nameof(DataTestUtility.IsNotNamedInstance))]
        public void MARSReplicateTest()
        {
            _splitPacketSize = 1;
            OpenMarsConnection("select REPLICATE('A', 10000)");
            Assert.True(true);
        }

        private void OpenMarsConnection(string cmdText)
        {
            using (SqlConnection conn = new SqlConnection((new SqlConnectionStringBuilder(_baseConnString) { MultipleActiveResultSets = true }).ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd1 = new SqlCommand(cmdText, conn))
                using (SqlCommand cmd2 = new SqlCommand(cmdText, conn))
                using (SqlCommand cmd3 = new SqlCommand(cmdText, conn))
                using (SqlCommand cmd4 = new SqlCommand(cmdText, conn))
                {
                    cmd1.ExecuteReader();
                    cmd2.ExecuteReader();
                    cmd3.ExecuteReader();
                    cmd4.ExecuteReader();
                }
                conn.Close();
            }
        }

        private void OpenConnection()
        {
            using (SqlConnection conn = new SqlConnection(_baseConnString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("select * from Orders", conn))
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    Assert.True(reader.HasRows, "Split packet query did not return any rows!");
                }
                conn.Close();
            }
        }

        private void SetupProxy(string actualHost, int actualPort, CancellationToken cancellationToken)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var client = _listener.AcceptTcpClientAsync().GetAwaiter().GetResult();

            var sqlClient = new TcpClient();
            sqlClient.ConnectAsync(actualHost, actualPort).Wait(cancellationToken);

            Task.Factory.StartNew(() => { ForwardToSql(client, sqlClient, cancellationToken); }, cancellationToken);
            Task.Factory.StartNew(() => { ForwardToClient(client, sqlClient, cancellationToken); }, cancellationToken);
        }

        private void ForwardToSql(TcpClient ourClient, TcpClient sqlClient, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = ourClient.GetStream().Read(buffer, 0, buffer.Length);

                sqlClient.GetStream().Write(buffer, 0, bytesRead);
            }
        }

        private void ForwardToClient(TcpClient ourClient, TcpClient sqlClient, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = new byte[_splitPacketSize];
                int bytesRead = sqlClient.GetStream().Read(buffer, 0, buffer.Length);

                ourClient.GetStream().Write(buffer, 0, bytesRead);

                buffer = new byte[1024];
                bytesRead = sqlClient.GetStream().Read(buffer, 0, buffer.Length);

                ourClient.GetStream().Write(buffer, 0, bytesRead);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                _listener?.Server.Dispose();
#if NETFRAMEWORK
                _listener?.Stop();
#else
                _listener?.Dispose();
#endif
            }
        }
    }
}
