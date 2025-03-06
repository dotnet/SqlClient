// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    internal class TestTdsServer : GenericTDSServer, IDisposable
    {
        private const int DefaultConnectionTimeout = 5;

        private TDSServerEndPoint _endpoint = null;

        private SqlConnectionStringBuilder _connectionStringBuilder;

        public TestTdsServer(TDSServerArguments args) : base(args) { }

        public TestTdsServer(QueryEngine engine, TDSServerArguments args) : base(args)
        {
            Engine = engine;
        }

        public static TestTdsServer StartServerWithQueryEngine(QueryEngine engine, bool enableFedAuth = false, bool enableLog = false,
            int connectionTimeout = DefaultConnectionTimeout, [CallerMemberName] string methodName = "",
            X509Certificate2 encryptionCertificate = null, SslProtocols encryptionProtocols = SslProtocols.Tls12, TDSPreLoginTokenEncryptionType encryptionType = TDSPreLoginTokenEncryptionType.NotSupported)
        {
            TDSServerArguments args = new TDSServerArguments()
            {
                Log = enableLog ? Console.Out : null,
            };

            if (enableFedAuth)
            {
                args.FedAuthRequiredPreLoginOption = SqlServer.TDS.PreLogin.TdsPreLoginFedAuthRequiredOption.FedAuthRequired;
            }

            args.EncryptionCertificate = encryptionCertificate;
            args.EncryptionProtocols = encryptionProtocols;
            args.Encryption = encryptionType;

            TestTdsServer server = engine == null ? new TestTdsServer(args) : new TestTdsServer(engine, args);

            server._endpoint = new TDSServerEndPoint(server) { ServerEndPoint = new IPEndPoint(IPAddress.Any, 0) };
            server._endpoint.EndpointName = methodName;
            // The server EventLog should be enabled as it logs the exceptions.
            server._endpoint.EventLog = enableLog ? Console.Out : null;
            server._endpoint.Start();

            int port = server._endpoint.ServerEndPoint.Port;

            server._connectionStringBuilder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + port,
                ConnectTimeout = connectionTimeout,
            };

            if (encryptionType == TDSPreLoginTokenEncryptionType.Off || 
                encryptionType == TDSPreLoginTokenEncryptionType.None || 
                encryptionType == TDSPreLoginTokenEncryptionType.NotSupported)
            {
                server._connectionStringBuilder.Encrypt = SqlConnectionEncryptOption.Optional;
            }
            else
            {
                server._connectionStringBuilder.Encrypt = SqlConnectionEncryptOption.Mandatory;
            }

            server.ConnectionString = server._connectionStringBuilder.ConnectionString;
            return server;
        }

        public static TestTdsServer StartTestServer(bool enableFedAuth = false, bool enableLog = false,
            int connectionTimeout = DefaultConnectionTimeout, [CallerMemberName] string methodName = "",
            X509Certificate2 encryptionCertificate = null, SslProtocols encryptionProtocols = SslProtocols.Tls12, TDSPreLoginTokenEncryptionType encryptionType = TDSPreLoginTokenEncryptionType.NotSupported)
        {
            return StartServerWithQueryEngine(null, enableFedAuth, enableLog, connectionTimeout, methodName, encryptionCertificate, encryptionProtocols, encryptionType);
        }

        public void Dispose() => _endpoint?.Stop();

        public string ConnectionString { get; private set; }
    }
}
