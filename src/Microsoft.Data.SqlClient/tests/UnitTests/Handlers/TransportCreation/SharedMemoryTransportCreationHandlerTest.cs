// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers.Connection;
using Microsoft.Data.SqlClientX.Handlers.Connection.TransportCreation;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers.TransportCreation
{
    public class SharedMemoryTransportCreationHandlerTest
    {
        [Fact]
        internal async Task Handle_SharedMemory_ThrowsNotImplemented()
        {
            // Arrange
            var handler = new SharedMemoryTransportCreationHandler();
            var context = new ConnectionHandlerContext
            {
                DataSource = new DataSource { Protocol = DataSourceProtocol.SharedMemory }
            };
            
            // Act
            Func<Task<Stream?>> action = () => handler.Handle(context, false, default).AsTask();
            
            // Assert
            await Assert.ThrowsAsync<NotImplementedException>(action);
        }
        
        [Theory]
        [InlineData(DataSourceProtocol.NamedPipe)]
        [InlineData(DataSourceProtocol.NotSpecified)]
        [InlineData(DataSourceProtocol.Tcp)]
        [InlineData(DataSourceProtocol.Admin)]
        internal async Task Handle_ProtocolFromParams_Passes(DataSourceProtocol protocol)
        {
            // Arrange
            var handler1 = new SharedMemoryTransportCreationHandler();
            var context = new ConnectionHandlerContext { DataSource = new DataSource { Protocol = protocol } };
            
            // Act
            var result = await handler1.Handle(context, false, default);
            
            // Assert
            Assert.Null(result);
        }
    }
}
