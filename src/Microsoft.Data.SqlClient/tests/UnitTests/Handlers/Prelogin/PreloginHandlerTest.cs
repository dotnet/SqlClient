// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClient.UnitTests.IO;
using Microsoft.Data.SqlClient.UnitTests.IO.TdsHelpers;
using Microsoft.Data.SqlClientX.Handlers;
using Microsoft.Data.SqlClientX.Handlers.Connection;
using Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers;
using Microsoft.Data.SqlClientX.IO;
using Moq;
using Xunit;
using static Microsoft.Data.SqlClientX.Handlers.Connection.PreLoginHandler;

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

            PreLoginHandler handler = new(authenticator, mockPreloginSubHandlerBuilder.Object);

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

            MemoryStream stream = new();
            TdsStream tdsStream = new(new TdsWriteStream(stream), new TdsReadStream(stream));

            string connectionString = "Encrypt=Optional";
            SqlConnectionString connectionOptions = new(connectionString);

            ConnectionHandlerContext connectionContext = new()
            {
                ServerInfo = new ServerInfo(connectionOptions),
                DataSource = DataSource.ParseServerName("tcp:localhost,1433"),
                ConnectionString = connectionOptions,
                TdsStream = tdsStream
            };

            PreloginHandlerContext context = new(connectionContext)
            {
                // This causes a successful execution.
                ServerSupportsEncryption = serverSupportsEncryption,
                InternalEncryptionOption = EncryptionOptions.LOGIN
            };

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async void PreloginPacketHandler_HandlePackets(bool isAsync)
        {

            byte[] preloginResponse = new byte[]
                {
                    0, 0, 36, 0, 6,
                    1, 0, 42, 0, 1,
                    2, 0, 43, 0, 1,
                    3, 0, 44, 0, 0,
                    4, 0, 44, 0, 1,
                    5, 0, 45, 0, 0,
                    6, 0, 45, 0, 1,
                    255,
                    12, 0, 20, 229, 0, 0, (byte)EncryptionOptions.REQ, 0, 0, 1
                };
            TdsMessage tdsMessage = TdsReadStreamTest.PrepareTdsMessage(100, preloginResponse, TdsEnums.MT_PRELOGIN);
            SplittableStream splitStream = new(tdsMessage.GetBytes());
            TdsStream tdsStream = new(new TdsWriteStream(splitStream), new TdsReadStream(splitStream));

            string connectionString = "Encrypt=Optional";
            ConnectionHandlerContext connectionContext = new()
            {
                TdsStream = tdsStream,
                ConnectionString = new SqlConnectionString(connectionString)
            };

            PreloginHandlerContext context = new(connectionContext)
            {
                
            };
            PreloginPacketHandler preloginPacketHandler = new()
            {
                NextHandler = null
            };
            await preloginPacketHandler.Handle(context, isAsync, default).ConfigureAwait(false);
        }
    }
}
