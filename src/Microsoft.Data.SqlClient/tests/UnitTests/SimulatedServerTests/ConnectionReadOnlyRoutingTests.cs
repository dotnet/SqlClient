// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.ScenarioTests
{
    [Collection("SimulatedServerTests")]
    public class ConnectionReadOnlyRoutingTests
    {
        [Fact]
        public void NonRoutedConnection()
        {
            using TdsServer server = new();
            server.Start();
            SqlConnectionStringBuilder builder = new() {
                DataSource = $"localhost,{server.EndPoint.Port}",
                ApplicationIntent = ApplicationIntent.ReadOnly,
                Encrypt = SqlConnectionEncryptOption.Optional
            };
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
        }

        [Fact]
        public async Task NonRoutedAsyncConnection()
        {
            using TdsServer server = new();
            server.Start();
            SqlConnectionStringBuilder builder = new() {
                DataSource = $"localhost,{server.EndPoint.Port}",
                ApplicationIntent = ApplicationIntent.ReadOnly,
                Encrypt = SqlConnectionEncryptOption.Optional
             };
            using SqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
        }

        [Fact]
        public void RoutedConnection()
            => RecursivelyRoutedConnection(1);

        [Fact]
        public async Task RoutedAsyncConnection()
            => await RecursivelyRoutedAsyncConnection(1);

        [Theory]
        [InlineData(11)] // 11 layers of routing should succeed, 12 should fail
        public void RecursivelyRoutedConnection(int layers)
        {
            using TdsServer innerServer = new();
            innerServer.Start();
            IPEndPoint lastEndpoint = innerServer.EndPoint;
            Stack<RoutingTdsServer> routingLayers = new(layers + 1);
            string lastConnectionString = (new SqlConnectionStringBuilder() { DataSource = $"localhost,{lastEndpoint.Port}" }).ConnectionString;

            try
            {
                for (int i = 0; i < layers; i++)
                {
                    RoutingTdsServer router = new(
                        new RoutingTdsServerArguments()
                    {
                        RoutingTCPHost = "localhost",
                        RoutingTCPPort = (ushort)lastEndpoint.Port,
                    });
                    router.Start();
                    routingLayers.Push(router);
                    lastEndpoint = router.EndPoint;
                    lastConnectionString = (new SqlConnectionStringBuilder() { 
                        DataSource = $"localhost,{lastEndpoint.Port}",
                        ApplicationIntent = ApplicationIntent.ReadOnly,
                        Encrypt = false
                    }).ConnectionString;
                }

                SqlConnectionStringBuilder builder = new(lastConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
                using SqlConnection connection = new(builder.ConnectionString);
                connection.Open();
            }
            finally
            {
                while (routingLayers.Count > 0)
                {
                    routingLayers.Pop().Dispose();
                }
            }
        }

        [Theory]
        [InlineData(11)] // 11 layers of routing should succeed, 12 should fail
        public async Task RecursivelyRoutedAsyncConnection(int layers)
        {
            using TdsServer innerServer = new();
            innerServer.Start();
            IPEndPoint lastEndpoint = innerServer.EndPoint;
            Stack<RoutingTdsServer> routingLayers = new(layers + 1);
            string lastConnectionString = (new SqlConnectionStringBuilder() { DataSource = $"localhost,{lastEndpoint.Port}" }).ConnectionString;

            try
            {
                for (int i = 0; i < layers; i++)
                {
                    RoutingTdsServer router = new(
                        new RoutingTdsServerArguments()
                        {
                            RoutingTCPHost = "localhost",
                            RoutingTCPPort = (ushort)lastEndpoint.Port,
                        });
                    router.Start();
                    routingLayers.Push(router);
                    lastEndpoint = router.EndPoint;
                    lastConnectionString = (new SqlConnectionStringBuilder() { 
                        DataSource = $"localhost,{lastEndpoint.Port}", 
                        ApplicationIntent = ApplicationIntent.ReadOnly,
                        Encrypt = false
                    }).ConnectionString;
                }

                SqlConnectionStringBuilder builder = new(lastConnectionString) {
                    ApplicationIntent = ApplicationIntent.ReadOnly,
                    Encrypt = false
                };
                using SqlConnection connection = new(builder.ConnectionString);
                await connection.OpenAsync();
            }
            finally
            {
                while (routingLayers.Count > 0)
                {
                    routingLayers.Pop().Dispose();
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
