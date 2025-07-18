// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConnectionReadOnlyRoutingTests
    {
        [Fact]
        public void NonRoutedConnection()
        {
            using TdsServer server = new TdsServer();
            server.Start();
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder() { DataSource = $"localhost,{server.EndPoint.Port}", ApplicationIntent = ApplicationIntent.ReadOnly };
            using SqlConnection connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
        }

        [Fact]
        public async Task NonRoutedAsyncConnection()
        {
            using TdsServer server = new TdsServer();
            server.Start();
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder() { DataSource = $"localhost,{server.EndPoint.Port}", ApplicationIntent = ApplicationIntent.ReadOnly };
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
            using TdsServer innerServer = new TdsServer();
            innerServer.Start();
            IPEndPoint lastEndpoint = innerServer.EndPoint;
            Stack<RoutingTdsServer> routingLayers = new(layers + 1);
            string lastConnectionString = (new SqlConnectionStringBuilder() { DataSource = $"localhost,{lastEndpoint.Port}" }).ConnectionString;

            try
            {
                for (int i = 0; i < layers; i++)
                {
                    RoutingTdsServer router = new RoutingTdsServer(
                        new RoutingTdsServerArguments()
                    {
                        RoutingTCPHost = lastEndpoint.Address.ToString(),
                        RoutingTCPPort = (ushort)lastEndpoint.Port,
                    });
                    router.Start();
                    routingLayers.Push(router);
                    lastEndpoint = router.EndPoint;
                    lastConnectionString = (new SqlConnectionStringBuilder() { DataSource = $"localhost,{lastEndpoint.Port}" }).ConnectionString;
                }

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(lastConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
                using SqlConnection connection = new SqlConnection(builder.ConnectionString);
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
        [InlineData(2)]
        [InlineData(9)]
        [InlineData(11)] // The driver rejects more than 10 redirects (11 layers of redirecting servers)
        public async Task RecursivelyRoutedAsyncConnection(int layers)
        {
            using TdsServer innerServer = new TdsServer();
            innerServer.Start();
            IPEndPoint lastEndpoint = innerServer.EndPoint;
            Stack<RoutingTdsServer> routingLayers = new(layers + 1);
            string lastConnectionString = (new SqlConnectionStringBuilder() { DataSource = $"localhost,{lastEndpoint.Port}" }).ConnectionString;

            try
            {
                for (int i = 0; i < layers; i++)
                {
                    RoutingTdsServer router = new RoutingTdsServer(
                        new RoutingTdsServerArguments()
                        {
                            RoutingTCPHost = lastEndpoint.Address.ToString(),
                            RoutingTCPPort = (ushort)lastEndpoint.Port,
                        });
                    router.Start();
                    routingLayers.Push(router);
                    lastEndpoint = router.EndPoint;
                    lastConnectionString = (new SqlConnectionStringBuilder() { DataSource = $"localhost,{lastEndpoint.Port}" }).ConnectionString;
                }

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(lastConnectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
                using SqlConnection connection = new SqlConnection(builder.ConnectionString);
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

        [ConditionalTheory(typeof(TestUtility), nameof(TestUtility.IsNotArmProcess))]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TransientFaultAtRoutedLocationTest(uint errorCode)
        {
            // Arrange
            using TransientFaultTdsServer server = new TransientFaultTdsServer(
                new TransientFaultTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = server.EndPoint.Address.ToString(),
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder() {
                DataSource = $"localhost,{server.EndPoint.Port}",
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 30,
                ConnectRetryInterval = 1
            };
            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                // Act
                connection.Open();
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, router.PreLoginCount);
            Assert.Equal(2, server.PreLoginCount);
        }
    }
}
