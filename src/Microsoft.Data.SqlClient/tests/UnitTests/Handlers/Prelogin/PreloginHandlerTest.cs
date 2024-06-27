// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers;
using Microsoft.Data.SqlClientX.Handlers.Connection;
using Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers;
using Microsoft.Data.SqlClientX.Handlers.TransportCreation;
using Microsoft.Data.SqlClientX.IO;
using Moq;
using Xunit;
using static Microsoft.Data.SqlClientX.Handlers.Connection.PreloginHandler;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers.Prelogin
{
    public class PreloginHandlerTest
    {
        [Theory]
        [InlineData(true, "Encrypt=Strict")]
        [InlineData(false, "Encrypt=Strict")]
        [InlineData(true, "Encrypt=Optional")]
        [InlineData(false, "Encrypt=Optional")]
        public async void Handler_StreamInitialization(bool isAsync, string connectionString)
        {
            var tds8Encrypt = SqlConnectionEncryptOption.Strict;

            ConnectionHandlerContext connectionContext = new();

            SqlConnectionString connectionOptions = new(connectionString);
            connectionContext.ConnectionString = connectionOptions;
            
            var mockStream = new Mock<Stream>();
            mockStream.SetupGet(x => x.CanRead).Returns(true);
            mockStream.SetupGet(x => x.CanWrite).Returns(true);
            connectionContext.ConnectionStream = mockStream.Object;
            
            var mockTlsAuthenticator = new Mock<TlsAuthenticator>();
            mockTlsAuthenticator.Setup(mockTlsAuthenticator => mockTlsAuthenticator.AuthenticateClientInternal(It.IsAny<PreloginHandlerContext>(), It.IsAny<SslClientAuthenticationOptions>(), isAsync, default)).Returns(ValueTask.CompletedTask);
            
            TlsAuthenticator authenticator = mockTlsAuthenticator.Object;
            connectionContext.DataSource = DataSource.ParseServerName("tcp:localhost,1433");
            
            Mock<IHandler<PreloginHandlerContext>> mockHandler = new();
            mockHandler.Setup(mockHandler => mockHandler.Handle(It.IsAny<PreloginHandlerContext>(), isAsync, default)).Returns(ValueTask.CompletedTask);

            Mock<PreloginSubHandlerBuilder> mockPreloginSubHandlerBuilder = new();
            mockPreloginSubHandlerBuilder.Setup(preloginSubHandlerBuilder => preloginSubHandlerBuilder.CreateChain(It.IsAny<PreloginHandlerContext>(), It.IsAny<TlsAuthenticator>())).
                Returns(
                    mockHandler.Object
                );

            PreloginHandler handler = new(authenticator, mockPreloginSubHandlerBuilder.Object);

            await handler.Handle(connectionContext, isAsync, default);

            Assert.NotNull(connectionContext.SslStream);

            if (connectionOptions.Encrypt == SqlConnectionEncryptOption.Strict)
            {
                Assert.Null(connectionContext.SslOverTdsStream);
            }
            else
            {
                Assert.NotNull(connectionContext.SslOverTdsStream);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async void Tds8Handler_Tds8TlsHandleTest(bool isAsync)
        {

            var mockTlsAuthenticator = new Mock<TlsAuthenticator>();
            SslClientAuthenticationOptions? capturedOptions = null;
            mockTlsAuthenticator.Setup(mockTlsAuthenticator => mockTlsAuthenticator.AuthenticateClientInternal(It.IsAny<PreloginHandlerContext>(), It.IsAny<SslClientAuthenticationOptions>(), isAsync, default))
                .Callback((PreloginHandlerContext context, SslClientAuthenticationOptions options, bool isAsync, CancellationToken ct) => capturedOptions = options).Returns(ValueTask.CompletedTask);

            
            string connectionString = "Encrypt=Strict";
            SqlConnectionString connectionOptions = new SqlConnectionString(connectionString);
            ConnectionHandlerContext connectionContext = new ConnectionHandlerContext();
            connectionContext.DataSource = DataSource.ParseServerName("tcp:localhost,1433");
            connectionContext.ConnectionString = connectionOptions;
            PreloginHandlerContext context = new(connectionContext);
            Tds8TlsHandler tds8Handler = new(mockTlsAuthenticator.Object)
            {
                NextHandler = null
            };

            await tds8Handler.Handle(context, isAsync, default);

            Assert.Equal(1, capturedOptions?.ApplicationProtocols?.Count);
            Assert.Equal(TdsEnums.TDS8_Protocol, capturedOptions?.ApplicationProtocols?[0].ToString());
            Assert.Equal(EncryptionOptions.NOT_SUP, context.InternalEncryptionOption);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public async void Tds74Handler_Tds74TlsHandleTest(bool isAsync, bool serverSupportsEncryption)
        {

            var mockTlsAuthenticator = new Mock<TlsAuthenticator>();
            SslClientAuthenticationOptions? capturedOptions = null;
            mockTlsAuthenticator.Setup(mockTlsAuthenticator => mockTlsAuthenticator.AuthenticateClientInternal(It.IsAny<PreloginHandlerContext>(), It.IsAny<SslClientAuthenticationOptions>(), isAsync, default))
                .Callback((PreloginHandlerContext context, SslClientAuthenticationOptions options, bool isAsync, CancellationToken ct) => capturedOptions = options).Returns(ValueTask.CompletedTask);

            MemoryStream stream = new MemoryStream();
            TdsStream tdsStream = new(new TdsWriteStream(stream), new TdsReadStream(stream));

            string connectionString = "Encrypt=Optional";
            SqlConnectionString connectionOptions = new SqlConnectionString(connectionString);

            ConnectionHandlerContext connectionContext = new ConnectionHandlerContext();
            connectionContext.ServerInfo = new ServerInfo(connectionOptions);
            connectionContext.DataSource = DataSource.ParseServerName("tcp:localhost,1433");
            connectionContext.ConnectionString = connectionOptions;
            connectionContext.TdsStream = tdsStream;

            PreloginHandlerContext context = new(connectionContext);

            // This causes a successful execution.
            context.ServerSupportsEncryption = serverSupportsEncryption;
            context.InternalEncryptionOption = EncryptionOptions.LOGIN;

            Tds74TlsHandler tds74Handler = new(mockTlsAuthenticator.Object)
            {
                NextHandler = null
            };
            if (!serverSupportsEncryption)
            {
                await Assert.ThrowsAsync<SqlException>(testCode: async () => await tds74Handler.Handle(context, isAsync, default));
            }
            else
            {
                await tds74Handler.Handle(context, isAsync, default);
                Assert.Equal(SslProtocols.None, capturedOptions?.EnabledSslProtocols);
                Assert.Equal(EncryptionOptions.LOGIN, context.InternalEncryptionOption);
            }
        }

        [Fact]
        public void TestE2E()
        {
            DataSourceParsingHandler dspHandler = new DataSourceParsingHandler();
            TransportCreationHandler tcHandler = new TransportCreationHandler();
            PreloginHandler plHandler = new PreloginHandler();
            ConnectionHandlerContext chc = new ConnectionHandlerContext();
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            csb.DataSource = "tcp:localhost,1444";
            csb.Encrypt = SqlConnectionEncryptOption.Mandatory;

            csb.TrustServerCertificate = true;

            SqlConnectionString scs = new SqlConnectionString(csb.ConnectionString);
            chc.ConnectionString = scs;
            var serverInfo = new ServerInfo(scs);
            serverInfo.SetDerivedNames(null, serverInfo.UserServerName);
            chc.ServerInfo = serverInfo;
            dspHandler.NextHandler = tcHandler;
            tcHandler.NextHandler = plHandler;
            dspHandler.Handle(chc, false, default).GetAwaiter().GetResult();
        }

        [Fact]
        public void CheckEndianness()
        {
            int offset = 0;
            byte[] payload = new byte[4];
            uint sequence = 1234;
            payload[offset++] = (byte)(0x000000ff & sequence);
            payload[offset++] = (byte)((0x0000ff00 & sequence) >> 8);
            payload[offset++] = (byte)((0x00ff0000 & sequence) >> 16);
            payload[offset++] = (byte)((0xff000000 & sequence) >> 24);

            byte[] payload2 = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(payload2, sequence);
            Assert.Equal(payload, payload2);
        }
    }
}
