// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConnectionReadOnlyRoutingTests
    {
        [Fact]
        public void NonRoutedConnection()
        {
            using TestTdsServer server = TestTdsServer.StartTestServer();
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(server.ConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
            using SqlConnection connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
        }

        [Fact]
        public async Task NonRoutedAsyncConnection()
        {
            using TestTdsServer server = TestTdsServer.StartTestServer();
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(server.ConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
            using SqlConnection connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
        }

        [Fact]
        public void RoutedConnection()
            => RecursivelyRoutedConnection(1);

        [Fact]
        public async Task RoutedAsyncConnection()
            => await RecursivelyRoutedAsyncConnection(1);

        [Theory]
        [InlineData(2)]
        [InlineData(9)]
        [InlineData(11)] // The driver rejects more than 10 redirects (11 layers of redirecting servers)
        public void RecursivelyRoutedConnection(int layers)
        {
            TestTdsServer innerServer = TestTdsServer.StartTestServer();
            IPEndPoint lastEndpoint = innerServer.Endpoint;
            Stack<GenericTDSServer> routingLayers = new(layers + 1);
            string lastConnectionString = innerServer.ConnectionString;

            try
            {
                routingLayers.Push(innerServer);
                for (int i = 0; i < layers; i++)
                {
                    TestRoutingTdsServer router = TestRoutingTdsServer.StartTestServer(lastEndpoint);

                    routingLayers.Push(router);
                    lastEndpoint = router.Endpoint;
                    lastConnectionString = router.ConnectionString;
                }

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(lastConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
                using SqlConnection connection = new SqlConnection(builder.ConnectionString);
                connection.Open();
            }
            finally
            {
                while (routingLayers.Count > 0)
                {
                    GenericTDSServer layer = routingLayers.Pop();

                    if (layer is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                }
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(9)]
        [InlineData(11)] // The driver rejects more than 10 redirects (11 layers of redirecting servers)
        public async Task RecursivelyRoutedAsyncConnection(int layers)
        {
            TestTdsServer innerServer = TestTdsServer.StartTestServer();
            IPEndPoint lastEndpoint = innerServer.Endpoint;
            Stack<GenericTDSServer> routingLayers = new(layers + 1);
            string lastConnectionString = innerServer.ConnectionString;

            try
            {
                routingLayers.Push(innerServer);
                for (int i = 0; i < layers; i++)
                {
                    TestRoutingTdsServer router = TestRoutingTdsServer.StartTestServer(lastEndpoint);

                    routingLayers.Push(router);
                    lastEndpoint = router.Endpoint;
                    lastConnectionString = router.ConnectionString;
                }

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(lastConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
                using SqlConnection connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
            }
            finally
            {
                while (routingLayers.Count > 0)
                {
                    GenericTDSServer layer = routingLayers.Pop();

                    if (layer is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                }
            }
        }

        [Fact]
        public void ConnectionRoutingLimit()
        {
            SqlException sqlEx = Assert.Throws<SqlException>(() => RecursivelyRoutedConnection(12)); // This will fail on the 11th redirect

            Assert.Contains("Too many redirections have occurred.", sqlEx.Message, StringComparison.InvariantCultureIgnoreCase);
        }

        [Fact]
        public async Task AsyncConnectionRoutingLimit()
        {
            SqlException sqlEx = await Assert.ThrowsAsync<SqlException>(() => RecursivelyRoutedAsyncConnection(12)); // This will fail on the 11th redirect

            Assert.Contains("Too many redirections have occurred.", sqlEx.Message, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
