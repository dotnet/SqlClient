// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers;
using Moq;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers
{
    public class ReturningHandlerChainTests
    {
        public enum ExpectedBehavior
        {
            ThrowsAggregate,
            ThrowsException,
            ThrowsNoSuitable,
            Returns,
        }

        [Fact]
        public async Task Handle_InvalidExceptionHandling_Throws()
        {
            // Arrange
            var chain = new ReturningHandlerChain<string, string?>(
                Array.Empty<IReturningHandler<string, string?>>(),
                (ReturningHandlerChainExceptionBehavior)123);

            // Act
            Func<Task> action = () => chain.Handle("foo", false, default).AsTask();

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(action);
        }

        public static TheoryData<string, ExpectedBehavior, int> Handle_Halt_TestCases
        {
            get => new TheoryData<string, ExpectedBehavior, int>
            {
                { "TT", ExpectedBehavior.ThrowsException,  1 },
                { "TP", ExpectedBehavior.ThrowsException,  1 },
                { "TR", ExpectedBehavior.ThrowsException,  1 },
                { "PT", ExpectedBehavior.ThrowsException,  2 },
                { "PP", ExpectedBehavior.ThrowsNoSuitable, 2 },
                { "PR", ExpectedBehavior.Returns,          2 },
                { "RT", ExpectedBehavior.Returns,          1 },
                { "RP", ExpectedBehavior.Returns,          1 },
                { "RR", ExpectedBehavior.Returns,          1 },
            };
        }

        [Theory]
        [MemberData(nameof(Handle_Halt_TestCases))]
        public async Task Handle_Halt(string mockHandlersString, ExpectedBehavior expectedBehavior, int expectedCalledHandlers)
        {
            // Arrange
            var mockHandlers = GetMockHandlers(mockHandlersString);
            var chain = GetTestChain(mockHandlers, ReturningHandlerChainExceptionBehavior.Halt);

            // Act / Assert
            await AssertBehavior(chain, expectedBehavior);
            AssertHandlersCalled(mockHandlers, expectedCalledHandlers);
        }

        public static TheoryData<string, ExpectedBehavior, int> Handle_ThrowCollected_TestCases
        {
            get => new TheoryData<string, ExpectedBehavior, int>
            {
                { "TTT", ExpectedBehavior.ThrowsAggregate,  3 },
                { "TTP", ExpectedBehavior.ThrowsAggregate,  3 },
                { "TTR", ExpectedBehavior.Returns,          3 },
                { "TPT", ExpectedBehavior.ThrowsAggregate,  3 },
                { "TPP", ExpectedBehavior.ThrowsException,  3 },
                { "TPR", ExpectedBehavior.Returns,          3 },
                { "TRT", ExpectedBehavior.Returns,          2 },
                { "TRP", ExpectedBehavior.Returns,          2 },
                { "TRR", ExpectedBehavior.Returns,          2 },
                { "PTT", ExpectedBehavior.ThrowsAggregate,  3 },
                { "PTP", ExpectedBehavior.ThrowsException,  3 },
                { "PTR", ExpectedBehavior.Returns,          3 },
                { "PPT", ExpectedBehavior.ThrowsException,  3 },
                { "PPP", ExpectedBehavior.ThrowsNoSuitable, 3 },
                { "PPR", ExpectedBehavior.Returns,          3 },
                { "PRT", ExpectedBehavior.Returns,          2 },
                { "PRP", ExpectedBehavior.Returns,          2 },
                { "PRR", ExpectedBehavior.Returns,          2 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
                { "RTT", ExpectedBehavior.Returns,          1 },
            };
        }

        [Theory]
        [MemberData(nameof(Handle_ThrowCollected_TestCases))]
        public async Task Handle_ThrowCollected(string mockHandlersString, ExpectedBehavior expectedBehavior, int expectedCalledHandlers)
        {
            // Arrange
            var mockHandlers = GetMockHandlers(mockHandlersString);
            var chain = GetTestChain(mockHandlers, ReturningHandlerChainExceptionBehavior.ThrowCollected);

            // Act / Assert
            await AssertBehavior(chain, expectedBehavior);
            AssertHandlersCalled(mockHandlers, expectedCalledHandlers);
        }

        public static TheoryData<string, ExpectedBehavior, int> Handle_ThrowFirstLast_TestCases
        {
            get => new TheoryData<string, ExpectedBehavior, int>
            {
                // Multiple throws is handled in Handle_ThrowFirstOrLast_ThrowsFirstOrLast
                { "TP", ExpectedBehavior.ThrowsException,  2 },
                { "TR", ExpectedBehavior.Returns,          2 },
                { "PT", ExpectedBehavior.ThrowsException,  2 },
                { "PP", ExpectedBehavior.ThrowsNoSuitable, 2 },
                { "PR", ExpectedBehavior.Returns,          2 },
                { "RT", ExpectedBehavior.Returns,          1 },
                { "RP", ExpectedBehavior.Returns,          1 },
                { "RR", ExpectedBehavior.Returns,          1 },
            };
        }

        [Theory]
        [MemberData(nameof(Handle_ThrowFirstLast_TestCases))]
        internal async Task Handle_ThrowFirst(string mockHandlersString, ExpectedBehavior expectedBehavior, int expectedCalledHandlers)
        {
            // Arrange
            var mockHandlers = GetMockHandlers(mockHandlersString);
            var chain = GetTestChain(mockHandlers, ReturningHandlerChainExceptionBehavior.ThrowFirst);

            // Act / Assert
            await AssertBehavior(chain, expectedBehavior);
            AssertHandlersCalled(mockHandlers, expectedCalledHandlers);
        }

        [Theory]
        [MemberData(nameof(Handle_ThrowFirstLast_TestCases))]
        internal async Task Handle_ThrowLast(string mockHandlersString, ExpectedBehavior expectedBehavior, int expectedCalledHandlers)
        {
            // Arrange
            var mockHandlers = GetMockHandlers(mockHandlersString);
            var chain = GetTestChain(mockHandlers, ReturningHandlerChainExceptionBehavior.ThrowLast);

            // Act / Assert
            await AssertBehavior(chain, expectedBehavior);
            AssertHandlersCalled(mockHandlers, expectedCalledHandlers);
        }

        [Theory]
        [InlineData(ReturningHandlerChainExceptionBehavior.ThrowFirst)]
        [InlineData(ReturningHandlerChainExceptionBehavior.ThrowLast)]
        internal async Task Handle_ThrowFirstOrLast_ThrowsFirstOrLast(ReturningHandlerChainExceptionBehavior exceptionBehavior)
        {
            // Arrange
            var exception1 = new InvalidOperationException();
            var exception2 = new InvalidOperationException();

            var mockThrowingHandler = new Mock<IReturningHandler<string, string?>>();
            mockThrowingHandler.SetupSequence(h => h.Handle(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception1)
                .ThrowsAsync(exception2);

            var handlers = new[] { mockThrowingHandler.Object, mockThrowingHandler.Object };
            var chain = new ReturningHandlerChain<string, string?>(handlers, exceptionBehavior);

            // Act
            Func<Task<string?>> action = () => chain.Handle("foo", false, default).AsTask();

            // Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
            switch (exceptionBehavior)
            {
                case ReturningHandlerChainExceptionBehavior.ThrowFirst:
                    Assert.Same(exception1, exception);
                    break;

                case ReturningHandlerChainExceptionBehavior.ThrowLast:
                    Assert.Same(exception2, exception);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(exceptionBehavior));
            }
        }

        private async ValueTask AssertBehavior(ReturningHandlerChain<string, string?> chain, ExpectedBehavior behavior)
        {
            // Act
            Func<Task<string?>> action = () => chain.Handle("foo", false, default).AsTask();

            // / Assert Returns/Throws
            switch (behavior)
            {
                case ExpectedBehavior.Returns:
                    var result = await action();
                    Assert.Equal("bar", result);
                    break;

                case ExpectedBehavior.ThrowsAggregate:
                    var ae = await Assert.ThrowsAsync<AggregateException>(action);
                    Assert.All(ae.InnerExceptions, ie => Assert.IsType<InvalidOperationException>(ie));
                    break;

                case ExpectedBehavior.ThrowsException:
                    await Assert.ThrowsAsync<InvalidOperationException>(action);
                    break;

                case ExpectedBehavior.ThrowsNoSuitable:
                    await Assert.ThrowsAsync<NoSuitableHandlerFoundException>(action);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(behavior));
            }
        }

        private static void AssertHandlersCalled(Mock<IReturningHandler<string, string?>>[] mockHandlers, int expectedCalled)
        {
            for (int i = 0; i < mockHandlers.Length; i++)
            {
                if (i < expectedCalled)
                {
                    mockHandlers[i].Verify(h => h.Handle("foo", false, default), Times.Once);
                }
                else
                {
                    mockHandlers[i].Verify(
                        h => h.Handle(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                        Times.Never);
                }
            }
        }

        private static Mock<IReturningHandler<string, string?>>[] GetMockHandlers(string definition)
        {
            var mockHandlers = new Mock<IReturningHandler<string, string?>>[definition.Length];
            for (int i = 0; i < definition.Length; i++)
            {
                Mock<IReturningHandler<string, string?>> mockHandler;
                switch (definition[i])
                {
                    case 'T':
                        // Handler that throws
                        mockHandler = new Mock<IReturningHandler<string, string?>>();
                        mockHandler.Setup(m => m.Handle(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new InvalidOperationException());
                        break;

                    case 'P':
                        // Handler that passes (returns null)
                        mockHandler = new Mock<IReturningHandler<string, string?>>();
                        mockHandler.Setup(m => m.Handle(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync((string?)null);
                        break;

                    case 'R':
                        // Handler that returns (a result)
                        mockHandler = new Mock<IReturningHandler<string, string?>>();
                        mockHandler.Setup(m => m.Handle(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync("bar");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(definition));
                }

                mockHandlers[i] = mockHandler;
            }

            return mockHandlers;
        }

        private static ReturningHandlerChain<string, string?> GetTestChain(
            Mock<IReturningHandler<string, string?>>[] mockHandlers,
            ReturningHandlerChainExceptionBehavior exceptionBehavior)
        {
            var handlers = mockHandlers.Select(mh => mh.Object).ToArray();
            return new ReturningHandlerChain<string, string?>(handlers, exceptionBehavior);
        }
    }
}
