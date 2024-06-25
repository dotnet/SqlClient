// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers;
using Microsoft.Data.SqlClientX.Handlers.TransportCreation;
using Moq;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers.TransportCreation
{
    public class NamedPipeTransportCreationHandlerTests
    {
        [Fact]
        public async Task Handle_NamedPipe_Throws()
        {
            // Arrange
            var handler = new NamedPipeTransportCreationHandler();
            var parameters = new ConnectionHandlerContext { DataSource = new DataSource(DataSource.Protocol.NP) };

            // Act
            Func<Task<Stream>> action = () => handler.Handle(parameters, false, default).AsTask();
            
            // Assert
            await Assert.ThrowsAsync<NotImplementedException>(action);
        }

        [Theory]
        [InlineData(DataSource.Protocol.None)]
        [InlineData(DataSource.Protocol.TCP)]
        [InlineData(DataSource.Protocol.Admin)]
        internal async Task Handle_ProtocolFromParams_Passes(DataSource.Protocol protocol)
        {
            // Arrange
            var handler2 = new Mock<ReturningHandler<ConnectionHandlerContext, Stream>>();
            handler2.Setup(h => h.Handle(It.IsAny<ConnectionHandlerContext>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<Stream>());
                
            var handler1 = new NamedPipeTransportCreationHandler { NextHandler = handler2.Object };

            var context = new ConnectionHandlerContext { DataSource = new DataSource(protocol) };
            
            // Act
            _ = await handler1.Handle(context, false, default);
            
            // Assert
            handler2.Verify(h => h.Handle(context, false, default), Times.Once);
        }
    }
}
