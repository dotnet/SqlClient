// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Unit tests for the <see cref="SniPacket"/> class.
    /// </summary>
    public sealed class SniPacketTests
    {
        /// <summary>
        /// Tests the behavior of the <see cref="SniPacket"/> ReadFromStreamAsync method when the
        /// asynchronous IO callback throws an exception. Ensures that such exceptions result in an
        /// unobserved task exception being captured by the event handler for unobserved task
        /// exceptions.
        /// </summary>
        // @TODO: This test is a repro scenario for GH#3720, it's assertions should be inverted
        //    when the broken scenario has been fixed.
        [Fact]
        public async Task ReadFromStreamAsync_WhenAsyncIoCallbackThrows_CreatesUnobservedTaskException()
        {
            // Arrange
            // - Set up for unobserved exception handling
            using ObservableExceptionHelper exceptionHelper = new ObservableExceptionHelper();

            // - Set up task completion source to signal end of async IO completion callback
            TaskCompletionSource callbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // - Set up packet around the test handle, wire up callback
            TestSniHandle testHandle = new TestSniHandle();
            SniPacket testPacket = new SniPacket(testHandle, id: 1);

            testPacket.Allocate(headerLength: 0, dataLength: 1);
            testPacket.SetAsyncIOCompletionCallback((_, _) =>
            {
                // Throw our special test exception, then before finishing complete the TCS so
                // we know the exception was thrown.
                try
                {
                    throw exceptionHelper.TestException;
                }
                finally
                {
                    callbackStarted.SetResult();
                }
            });

            // Act
            // - Read some bytes from the test asynchronously. This will cause the async IO
            //   completion callback to fire, which will throw our test exception.
            using MemoryStream emptyStream = new();
            testPacket.ReadFromStreamAsync(emptyStream);

            // - Wait for async IO to complete (should be more or less instantaneous)
            await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // - Force GC and wait for unhandled exception to be raised
            Exception? unobservedException = await exceptionHelper.Wait(TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotNull(unobservedException);
        }

        private sealed class TestSniHandle : SniHandle
        {
            public override Guid ConnectionId { get; } = Guid.NewGuid();

            public override uint Status => TdsEnums.SNI_SUCCESS;

            public override SslProtocols ProtocolVersion => SslProtocols.None;

            public override uint CheckConnection() => TdsEnums.SNI_SUCCESS;

            public override void DisableSsl()
            {
            }

            public override void Dispose()
            {
            }

            public override uint EnableSsl(uint options) => TdsEnums.SNI_SUCCESS;

            public override SniPacket RentPacket(int headerSize, int dataSize)
            {
                SniPacket packet = new(this, id: 1);
                packet.Allocate(headerSize, dataSize);
                return packet;
            }

            public override uint Receive(out SniPacket packet, int timeoutInMilliseconds)
            {
                throw new NotSupportedException();
            }

            public override uint ReceiveAsync(ref SniPacket packet)
            {
                throw new NotSupportedException();
            }

            public override void ReturnPacket(SniPacket packet)
            {
                packet.Release();
                GC.SuppressFinalize(packet);
            }

            public override uint Send(SniPacket packet)
            {
                throw new NotSupportedException();
            }

            public override uint SendAsync(SniPacket packet)
            {
                throw new NotSupportedException();
            }

            public override void SetAsyncCallbacks(SniAsyncCallback receiveCallback, SniAsyncCallback sendCallback)
            {
            }

            public override void SetBufferSize(int bufferSize)
            {
            }

            #if DEBUG
            public override void KillConnection()
            {
            }
            #endif
        }
    }
}

#endif
