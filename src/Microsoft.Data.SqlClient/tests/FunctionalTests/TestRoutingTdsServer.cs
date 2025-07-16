// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;

namespace Microsoft.Data.SqlClient.Tests
{
    internal class TestRoutingTdsServer : RoutingTDSServer, IDisposable
    {
        private const int DefaultConnectionTimeout = 5;

        private TDSServerEndPoint _endpoint = null;

        private SqlConnectionStringBuilder _connectionStringBuilder;

        public TestRoutingTdsServer(RoutingTDSServerArguments args) : base(args) { }

        public override IPEndPoint Endpoint => _endpoint.ServerEndPoint;

        public static TestRoutingTdsServer StartTestServer(IPEndPoint destinationEndpoint, bool enableFedAuth = false, bool enableLog = false, int connectionTimeout = DefaultConnectionTimeout, bool excludeEncryption = false, [CallerMemberName] string methodName = "")
        {
            RoutingTDSServerArguments args = new RoutingTDSServerArguments()
            {
                Log = enableLog ? Console.Out : null,
                RoutingTCPHost = destinationEndpoint.Address.ToString() == IPAddress.Any.ToString() ? IPAddress.Loopback.ToString() : destinationEndpoint.Address.ToString(),
                RoutingTCPPort = (ushort)destinationEndpoint.Port,
            };

            if (enableFedAuth)
            {
                args.FedAuthRequiredPreLoginOption = SqlServer.TDS.PreLogin.TdsPreLoginFedAuthRequiredOption.FedAuthRequired;
            }
            if (excludeEncryption)
            {
                args.Encryption = SqlServer.TDS.PreLogin.TDSPreLoginTokenEncryptionType.None;
            }

            TestRoutingTdsServer server = new TestRoutingTdsServer(args);
            server._endpoint = new TDSServerEndPoint(server) { ServerEndPoint = new IPEndPoint(IPAddress.Any, 0) };
            server._endpoint.EndpointName = methodName;
            // The server EventLog should be enabled as it logs the exceptions.
            server._endpoint.EventLog = enableLog ? Console.Out : null;
            server._endpoint.Start();

            int port = server._endpoint.ServerEndPoint.Port;
            server._connectionStringBuilder = excludeEncryption
                // Allow encryption to be set when encryption is to be excluded from pre-login response.
                ? new SqlConnectionStringBuilder() { DataSource = "localhost," + port, ConnectTimeout = connectionTimeout, Encrypt = SqlConnectionEncryptOption.Mandatory }
                : new SqlConnectionStringBuilder() { DataSource = "localhost," + port, ConnectTimeout = connectionTimeout, Encrypt = SqlConnectionEncryptOption.Optional };
            server.ConnectionString = server._connectionStringBuilder.ConnectionString;
            return server;
        }

        public void Dispose() => _endpoint?.Stop();
    }
}
