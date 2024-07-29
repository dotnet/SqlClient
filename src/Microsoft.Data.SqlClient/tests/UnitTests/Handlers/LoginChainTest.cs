// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers.Connection;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers
{
    /// <summary>
    ///  Place holder for stitching all the handlers for login and testing them
    ///  without the orchestrator.
    /// </summary>
    public sealed class LoginChainTest
    {

        /// <summary>
        ///  Datasource to use for connection test. This may be changed locally to test against a different server.
        ///  TODO: Migrate to config when we enable CI.
        /// </summary>
        private string _dataSource = "tcp:localhost,1444";

        [Fact]
        public async void TestConnectivity()
        {
            DataSourceParsingHandler dspHandler = new();
            TransportCreationHandler tcHandler = new();
            PreloginHandler plHandler = new();
            ConnectionHandlerContext chc = new();
            SqlConnectionStringBuilder csb = new()
            {
                DataSource = _dataSource,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = true,
                UserID = "sa",
                Password = "HappyPass1234",// Environment.GetEnvironmentVariable("PASSWORD"),
                ConnectRetryCount = 0,
            };

            SqlConnectionString scs = new(csb.ConnectionString);
            chc.ConnectionString = scs;

            // MDS connectivity 
            using (SqlConnection connection = new SqlConnection(csb.ConnectionString))
                connection.Open();

            var serverInfo = new ServerInfo(scs);
            serverInfo.SetDerivedNames(null, serverInfo.UserServerName);
            chc.ServerInfo = serverInfo;
            dspHandler.NextHandler = tcHandler;
            tcHandler.NextHandler = plHandler;
            LoginHandler loginHandler = new();
            plHandler.NextHandler = loginHandler;
            await dspHandler.Handle(chc, true, default);
        }
    }
}
