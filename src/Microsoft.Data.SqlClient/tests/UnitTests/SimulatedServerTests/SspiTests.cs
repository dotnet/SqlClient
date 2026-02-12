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

            using SqlConnection connection = new(connStr)
            {
                SspiContextProvider = new ChallengeResponseSspiContextProvider(),
            };

            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);
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

            using SqlConnection connection = new(connStr)
            {
                SspiContextProvider = new ChallengeResponseSspiContextProvider(),
            };

            await connection.OpenAsync();
            Assert.Equal(ConnectionState.Open, connection.State);
        }

        /// <summary>
        /// A custom <see cref="SspiContextProvider"/> that performs a two-step
        /// challenge-response handshake:
        ///   Step 1 (empty incoming): writes an initial client token.
        ///   Step 2 (incoming = server challenge): validates the challenge and writes a client response.
        /// </summary>
        private sealed class ChallengeResponseSspiContextProvider : SspiContextProvider
        {
            protected override bool GenerateContext(
                ReadOnlySpan<byte> incomingBlob,
                IBufferWriter<byte> outgoingBlobWriter,
                SspiAuthenticationParameters authParams)
            {
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
