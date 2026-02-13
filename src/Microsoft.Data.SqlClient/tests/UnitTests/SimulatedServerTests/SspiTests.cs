// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SSPI;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    public class SspiTests
    {
        private static readonly byte[] s_clientInitialToken = Encoding.UTF8.GetBytes("ClientInitial");
        private static readonly byte[] s_serverChallengeToken = Encoding.UTF8.GetBytes("ServerChallenge");
        private static readonly byte[] s_clientResponseToken = Encoding.UTF8.GetBytes("ClientResponse");

        /// <summary>
        /// Verifies that a custom <see cref="SspiContextProvider"/> can complete a full
        /// login handshake against the simulated TDS server using a two-step
        /// challenge-response protocol:
        ///   1. Client sends initial token → Server validates and responds with a challenge
        ///   2. Client validates challenge, sends response → Server validates and completes login
        /// </summary>
        [Fact]
        public void CustomSspiProviderCompletesLogin()
        {
            using var server = new ChallengeResponseSspiTdsServer(new TdsServerArguments());
            server.Start();

            var connStr = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                IntegratedSecurity = true,
            }.ConnectionString;

            var provider = new ChallengeResponseSspiContextProvider();
            using SqlConnection connection = new(connStr)
            {
                SspiContextProvider = provider,
            };

            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.True(provider.GenerateContextCallCount >= 2, $"Expected at least 2 GenerateContext calls (initial + challenge-response) but got {provider.GenerateContextCallCount}");
        }

        /// <summary>
        /// Verifies that a custom <see cref="SspiContextProvider"/> can complete a full
        /// login handshake asynchronously.
        /// </summary>
        [Fact]
        public async Task CustomSspiProviderCompletesLoginAsync()
        {
            using var server = new ChallengeResponseSspiTdsServer(new TdsServerArguments());
            server.Start();

            var connStr = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                IntegratedSecurity = true,
            }.ConnectionString;

            var provider = new ChallengeResponseSspiContextProvider();
            using SqlConnection connection = new(connStr)
            {
                SspiContextProvider = provider,
            };

            await connection.OpenAsync();
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.True(provider.GenerateContextCallCount >= 2, $"Expected at least 2 GenerateContext calls (initial + challenge-response) but got {provider.GenerateContextCallCount}");
        }

        /// <summary>
        /// Verifies that connections using different <see cref="SspiContextProvider"/> instances
        /// are placed in separate connection pools (since the provider is part of the pool key).
        /// </summary>
        [Fact]
        public void DifferentSspiProviders_UseSeparateConnectionPools()
        {
            using var server = new ChallengeResponseSspiTdsServer(new TdsServerArguments());
            server.Start();

            var connStr = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                IntegratedSecurity = true,
            }.ConnectionString;

            var providerA = new ChallengeResponseSspiContextProvider();
            var providerB = new ChallengeResponseSspiContextProvider();

            // Open and close a connection with provider A
            using (SqlConnection connectionA = new(connStr) { SspiContextProvider = providerA })
            {
                connectionA.Open();
                Assert.Equal(ConnectionState.Open, connectionA.State);
            }

            // Open a connection with provider B — it should not reuse provider A's pooled connection
            using (SqlConnection connectionB = new(connStr) { SspiContextProvider = providerB })
            {
                connectionB.Open();
                Assert.Equal(ConnectionState.Open, connectionB.State);
            }

            // Both providers should have been called (not just the first one)
            // If pooling incorrectly shared connections, the server would see fewer logins
            Assert.True(server.PreLoginCount >= 2,
                $"Expected at least 2 pre-login requests (separate pools) but got {server.PreLoginCount}");

            // Verify each provider completed the two-step SSPI handshake
            Assert.True(providerA.GenerateContextCallCount >= 2, $"Expected at least 2 GenerateContext calls (initial + challenge-response) but got {providerA.GenerateContextCallCount}");
            Assert.True(providerB.GenerateContextCallCount >= 2, $"Expected at least 2 GenerateContext calls (initial + challenge-response) but got {providerB.GenerateContextCallCount}");
        }

        /// <summary>
        /// Verifies that the same <see cref="SspiContextProvider"/> instance can be reused
        /// across multiple sequential connection open/close cycles.
        /// </summary>
        [Fact]
        public void SameSspiProvider_CanBeReusedAcrossMultipleConnections()
        {
            using var server = new ChallengeResponseSspiTdsServer(new TdsServerArguments());
            server.Start();

            var connStr = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                IntegratedSecurity = true,
            }.ConnectionString;

            var provider = new ChallengeResponseSspiContextProvider();

            for (int i = 0; i < 3; i++)
            {
                using SqlConnection connection = new(connStr) { SspiContextProvider = provider };
                connection.Open();
                Assert.Equal(ConnectionState.Open, connection.State);
            }

            // The first login triggers 2 GenerateContext calls (initial + challenge-response).
            // Subsequent connections may reuse pooled connections, so the total should be at least 2.
            Assert.True(provider.GenerateContextCallCount >= 2,
                $"Expected at least 2 GenerateContext calls but got {provider.GenerateContextCallCount}");
        }

        /// <summary>
        /// Verifies that setting <see cref="SqlConnection.SspiContextProvider"/> throws
        /// <see cref="InvalidOperationException"/> when the connection is open.
        /// </summary>
        [Fact]
        public void SspiContextProvider_CannotBeSetWhileConnectionIsOpen()
        {
            using var server = new ChallengeResponseSspiTdsServer(new TdsServerArguments());
            server.Start();

            var connStr = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                IntegratedSecurity = true,
            }.ConnectionString;

            var provider = new ChallengeResponseSspiContextProvider();
            using SqlConnection connection = new(connStr)
            {
                SspiContextProvider = provider,
            };

            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => connection.SspiContextProvider = new ChallengeResponseSspiContextProvider());
            Assert.Contains("SspiContextProvider", ex.Message);
        }

        /// <summary>
        /// A custom <see cref="SspiContextProvider"/> that performs a two-step
        /// challenge-response handshake:
        ///   Step 1 (empty incoming): writes an initial client token.
        ///   Step 2 (incoming = server challenge): validates the challenge and writes a client response.
        /// </summary>
        private sealed class ChallengeResponseSspiContextProvider : SspiContextProvider
        {
            /// <summary>
            /// Tracks how many times <see cref="GenerateContext"/> has been called.
            /// For a successful two-step handshake this should be 2 per login.
            /// </summary>
            public int GenerateContextCallCount { get; private set; }

            protected override bool GenerateContext(
                ReadOnlySpan<byte> incomingBlob,
                IBufferWriter<byte> outgoingBlobWriter,
                SspiAuthenticationParameters authParams)
            {
                GenerateContextCallCount++;

                if (incomingBlob.IsEmpty)
                {
                    // First call: no server challenge yet, send initial client token
                    WriteBytes(outgoingBlobWriter, s_clientInitialToken);
                    return true;
                }
                else
                {
                    // Second call: validate server challenge and send client response
                    byte[] incoming = incomingBlob.ToArray();
                    Assert.True(
                        incoming.SequenceEqual(s_serverChallengeToken),
                        $"Expected server challenge token but received '{Encoding.UTF8.GetString(incoming)}'");

                    WriteBytes(outgoingBlobWriter, s_clientResponseToken);
                    return true;
                }
            }

            private static void WriteBytes(IBufferWriter<byte> writer, byte[] data)
            {
                var span = writer.GetSpan(data.Length);
                data.CopyTo(span);
                writer.Advance(data.Length);
            }
        }

        /// <summary>
        /// A TDS server that implements a two-step challenge-response SSPI handshake:
        ///   Step 1 (Login7 SSPI): Expects the initial client token, responds with a server challenge.
        ///   Step 2 (SSPI message): Expects the client response, completes authentication.
        /// </summary>
        private sealed class ChallengeResponseSspiTdsServer : GenericTdsServer<TdsServerArguments>
        {
            public ChallengeResponseSspiTdsServer(TdsServerArguments arguments)
                : base(arguments)
            {
            }

            protected override TDSMessageCollection ContinueSSPIAuthentication(
                ITDSServerSession session, byte[] payload)
            {
                if (payload != null && payload.SequenceEqual(s_clientInitialToken))
                {
                    // Step 1: Received initial client token — respond with server challenge
                    TDSSSPIToken sspiToken = new(s_serverChallengeToken);
                    return new TDSMessageCollection(
                        new TDSMessage(TDSMessageType.Response, sspiToken));
                }
                else if (payload != null && payload.SequenceEqual(s_clientResponseToken))
                {
                    // Step 2: Received client response — complete authentication
                    session.SQLUserID = null;
                    return OnAuthenticationCompleted(session);
                }
                else
                {
                    // Unexpected payload — return an error
                    string received = payload != null ? Encoding.UTF8.GetString(payload) : "(null)";
                    TDSErrorToken errorToken = new(
                        12345, 1, 15,
                        $"Unexpected SSPI payload: '{received}'",
                        Arguments.ServerName);
                    TDSDoneToken doneToken = new(
                        TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Error);
                    return new TDSMessageCollection(
                        new TDSMessage(TDSMessageType.Response, errorToken, doneToken));
                }
            }
        }
    }
}
