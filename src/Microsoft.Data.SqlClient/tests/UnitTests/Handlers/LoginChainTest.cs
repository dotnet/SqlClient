// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        private string _dataSource = "tcp:localhost,1433";

        // TODO: This test needs to be enabled conditionally. This can be used to test the handlers E2E.
        // For now, uncomment the [Fact] attribute to run the test.
        //[Fact]
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
                Password = Environment.GetEnvironmentVariable("PASSWORD"),
                ConnectRetryCount = 0,
            };

            SqlConnectionString scs = new(csb.ConnectionString);
            chc.ConnectionString = scs;

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
