// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers;
using Moq;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers
{
    public class ReturningHandlerTests
    {
        [Fact]
        public async Task HandleNext_WithNextHandler_CallsNextHandler()
        {
            // Arrange
            var handler2 = new Mock<ReturningHandler<string, int>>();
            handler2.Setup(h => h.Handle(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<int>(123));

            var handler1 = new PassThroughReturningHandler { NextHandler = handler2.Object, };
            
            // Act
            var result = await handler1.Handle("foo", false, default);

            // Assert
            Assert.Equal(123, result);
            handler2.Verify(h => h.Handle("foo", false, default), Times.Once);
        }

        [Fact]
        public async Task HandleNext_WithoutNextHandler_Throws()
        {
            // Arrange
            var handler1 = new PassThroughReturningHandler();

            // Act
            Func<Task<int>> action = () => handler1.Handle("foo", false, default).AsTask();

            // Assert
            await Assert.ThrowsAsync<NoSuitableHandlerFoundException>(action);
        }

        private class PassThroughReturningHandler : ReturningHandler<string, int>
        {
            public override ValueTask<int> Handle(string parameters, bool isAsync, CancellationToken ct) =>
                HandleNext(parameters,isAsync, ct);
        }
    }
}
