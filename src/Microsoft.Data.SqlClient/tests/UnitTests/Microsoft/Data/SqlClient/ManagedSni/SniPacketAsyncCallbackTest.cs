// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    public sealed class SniPacketAsyncCallbackTest
    {
        [Fact]
        public async Task ReadFromStreamAsync_WhenAsyncIoCallbackThrows_CreatesUnobservedTaskException()
        {
            // Arrange


            try
            {
                // Act



                // Assert
            }
            finally
            {
                // Cleanup
                // - Unregister unobserved exception handler
                TaskScheduler.UnobservedTaskException -= handleUnobservedException;
            }


            // Assert

            Guid exceptionId = Guid.NewGuid();
            Exception? unobservedException = null;

            EventHandler<UnobservedTaskExceptionEventArgs> handleUnobservedException =
                (_, args) =>
                {
                    if (args.Exception.InnerExceptions.OfType<ObservableException>().Any(e => e.Identifier == exceptionId))
                    {
                        unobservedException = args.Exception;
                        args.SetObserved();
                    }
                };

            TaskScheduler.UnobservedTaskException += handleUnobservedException;

            try
            {
                await RunReadWithThrowingCallbackAsync(exceptionId);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Assert.NotNull(unobservedException);
            }
            finally
            {

            }
        }

        private static async Task RunReadWithThrowingCallbackAsync(Guid exceptionId)
        {
            TaskCompletionSource<object?> callbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            SniPacket packet = new(new TestSniHandle(), id: 1);
            packet.Allocate(headerLength: 0, dataLength: 1);
            packet.SetAsyncIOCompletionCallback((_, _) =>
            {
                callbackStarted.SetResult(null);
                throw new ObservableException(exceptionId);
            });

            using MemoryStream emptyStream = new();
            packet.ReadFromStreamAsync(emptyStream);

            await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            packet.Release();
            GC.SuppressFinalize(packet);
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
