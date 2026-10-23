// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Covers managed TCP and named-pipe receive completion during physical connection teardown.
    /// </summary>
    public sealed class SniReceiveTests
    {
        /// <summary>
        /// Closing the connection invalidates an async reader's buffered-data estimate.
        /// Its next network read must report closure rather than fail a debug assertion.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadNetworkPacket_AfterClose_ReportsClosedConnection(bool broken)
        {
            TdsParser parser = new(MARS: true, fAsynchronous: true);
            TdsParserStateObjectManaged stateObject = new(parser);
            try
            {
                parser.State = broken ? TdsParserState.Broken : TdsParserState.Closed;
#if DEBUG
                stateObject._shouldHaveEnoughData = true;
#endif
                Assert.Throws<InvalidOperationException>(() => stateObject.TryReadNetworkPacket());
            }
            finally
            {
                stateObject.Dispose();
                parser._physicalStateObj.Dispose();
            }
        }

        /// <summary>
        /// A receive started after disposal must report an SNI error, not throw into its caller.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Receive_AfterDispose_ReturnsError(bool namedPipe, bool async)
        {
            using LocalConnection connection = new(namedPipe);
            connection.Handle.Dispose();
            SniPacket? packet = null;
            try
            {
                uint result = async
                    ? connection.Handle.ReceiveAsync(ref packet)
                    : connection.Handle.Receive(out packet, 1000);

                Assert.Equal(TdsEnums.SNI_ERROR, result);
                Assert.Null(packet);
                Assert.Equal(namedPipe ? SniProviders.NP_PROV : SniProviders.TCP_PROV, SniLoadHandle.LastError.provider);
                Assert.IsType<ObjectDisposedException>(SniLoadHandle.LastError.exception);
            }
            finally
            {
                if (packet is not null && !packet.IsInvalid)
                {
                    connection.Handle.ReturnPacket(packet);
                }
            }
        }

        /// <summary>
        /// Re-arming a successful MARS receive after disposal must finish error handling
        /// without throwing, including when the SMUX header or payload spans receives.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void MarsReceiveComplete_AfterDispose_HandlesReceiveError(bool namedPipe, bool partialPayload)
        {
            using LocalConnection connection = new(namedPipe);
            SniMarsConnection mars = new(connection.Handle);
            SniPacket packet = connection.Handle.RentPacket(0, SniSmuxHeader.HEADER_LENGTH);
            packet.SetAsyncIOCompletionCallback(mars.HandleReceiveComplete);
            if (partialPayload)
            {
                byte[] headerBytes = new byte[SniSmuxHeader.HEADER_LENGTH];
                new SniSmuxHeader
                {
                    SMID = 83,
                    flags = (byte)SniSmuxFlags.SMUX_DATA,
                    length = SniSmuxHeader.HEADER_LENGTH + 1,
                }.Write(headerBytes);
                packet.AppendData(headerBytes, headerBytes.Length);
            }

            connection.Handle.Dispose();
            try
            {
                mars.HandleReceiveComplete(packet, TdsEnums.SNI_SUCCESS);
                Assert.IsType<ObjectDisposedException>(SniLoadHandle.LastError.exception);
                Assert.True(packet.IsInvalid);
            }
            finally
            {
                if (!packet.IsInvalid)
                {
                    connection.Handle.ReturnPacket(packet);
                }
            }
        }

        /// <summary>
        /// A successful transport callback may run after disposal but must not fault when
        /// the MARS demultiplexer tries to receive the rest of its header.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MarsReceiveCallback_DisposesBeforeRearm_CompletesWithoutException(bool namedPipe)
        {
            using LocalConnection connection = new(namedPipe);
            SniMarsConnection mars = new(connection.Handle);
            TaskCompletionSource<SniError> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Handle.SetAsyncCallbacks((packet, error) =>
            {
                // Force the race's ordering, rather than depending on thread scheduling and GC.
                try
                {
                    Assert.Equal(TdsEnums.SNI_SUCCESS, error);
                    connection.Handle.Dispose();
                    mars.HandleReceiveComplete(packet, error);
                    completion.SetResult(SniLoadHandle.LastError);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }, null);

            Assert.Equal(TdsEnums.SNI_SUCCESS_IO_PENDING, mars.StartReceive());
            await connection.Peer.WriteAsync(new byte[] { 83 });
            SniError receiveError = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<ObjectDisposedException>(receiveError.exception);
        }

        /// <summary>
        /// Healthy sync and async receives must retain their data and packet ownership semantics.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Receive_Connected_ReturnsData(bool namedPipe, bool async)
        {
            using LocalConnection connection = new(namedPipe);
            TaskCompletionSource<(SniPacket Packet, uint Error)> completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Handle.SetAsyncCallbacks(
                (received, error) => completion.SetResult((received, error)), null);
            SniPacket? packet = null;
            Task write = connection.Peer.WriteAsync(new byte[] { 42 }).AsTask();
            try
            {
                uint result = async
                    ? connection.Handle.ReceiveAsync(ref packet)
                    : connection.Handle.Receive(out packet, 1000);
                if (async)
                {
                    Assert.Equal(TdsEnums.SNI_SUCCESS_IO_PENDING, result);
                    (packet, result) = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }

                Assert.Equal(TdsEnums.SNI_SUCCESS, result);
                Assert.NotNull(packet);
                byte[] data = new byte[1];
                Assert.Equal(1, packet.TakeData(data, 0, data.Length));
                Assert.Equal(42, data[0]);
                await write.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                if (packet is not null && !packet.IsInvalid)
                {
                    connection.Handle.ReturnPacket(packet);
                }
            }
        }

        /// <summary>
        /// Disposal must not wait for a pending MARS receive, and the resulting error callback
        /// must be able to acquire the demultiplexer lock and release its packet.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MarsReceive_PendingRead_DisposeCompletesAndReportsError(bool namedPipe)
        {
            using LocalConnection connection = new(namedPipe);
            SniMarsConnection mars = new(connection.Handle);
            TaskCompletionSource<uint> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Handle.SetAsyncCallbacks((packet, error) =>
            {
                try
                {
                    mars.HandleReceiveComplete(packet, error);
                    Assert.True(packet.IsInvalid);
                    completion.SetResult(error);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }, null);

            Assert.Equal(TdsEnums.SNI_SUCCESS_IO_PENDING, mars.StartReceive());
            Assert.False(completion.Task.IsCompleted);
            await Task.Run(() =>
            {
                lock (mars.DemuxerSync)
                {
                    connection.Handle.Dispose();
                }
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(TdsEnums.SNI_ERROR, await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        /// <summary>
        /// An immediate re-arm failure keeps the consumed packet's callback and ownership
        /// intact until MARS broadcasts the error; a new packet must not escape on initial failure.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MarsReceive_AfterDispose_PreservesErrorPacket(bool namedPipe)
        {
            using LocalConnection connection = new(namedPipe);
            SniMarsConnection mars = new(connection.Handle);
            SniPacket previousPacket = connection.Handle.RentPacket(0, 1);
            previousPacket.SetAsyncIOCompletionCallback(mars.HandleReceiveComplete);
            SniPacket? packet = previousPacket;
            connection.Handle.Dispose();
            try
            {
                Assert.Equal(TdsEnums.SNI_ERROR, mars.ReceiveAsync(ref packet));
                Assert.Same(previousPacket, packet);
                Assert.False(previousPacket.IsInvalid);
                Assert.True(previousPacket.HasAsyncIOCompletionCallback);
            }
            finally
            {
                if (!previousPacket.IsInvalid)
                {
                    connection.Handle.ReturnPacket(previousPacket);
                }
            }

            Assert.Equal(TdsEnums.SNI_ERROR, mars.StartReceive());
        }

        /// <summary>
        /// Re-arm failure after a complete ACK must reach every pending parser callback
        /// exactly once, including a task cleared during closure, without calling an idle session.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MarsReceiveComplete_AfterDispose_NotifiesAllSessions(bool namedPipe)
        {
            using LocalConnection connection = new(namedPipe);
            SniMarsConnection mars = new(connection.Handle);
            TdsParser parser = new(MARS: true, fAsynchronous: true);
            TdsParserStateObjectManaged[] callbacks =
            {
                new(parser),
                new(parser),
                new(parser),
                new(parser),
            };
            SniPacket? packet = null;
            try
            {
                SniMarsHandle[] sessions = new SniMarsHandle[callbacks.Length];
                for (int i = 0; i < callbacks.Length; i++)
                {
                    callbacks[i].TimeoutTime = long.MaxValue;
                    if (i > 0)
                    {
                        callbacks[i]._networkPacketTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        callbacks[i].IncrementPendingCallbacks();
                    }
                    byte[] syn = new byte[SniSmuxHeader.HEADER_LENGTH];
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
                    // Consume the synchronous SYN write even when the pipe has no buffer space.
                    Task readSyn = connection.Peer.ReadExactlyAsync(syn, timeout.Token).AsTask();
                    using CancellationTokenRegistration abort = timeout.Token.Register(connection.Peer.Dispose);
                    sessions[i] = mars.CreateMarsSession(callbacks[i], async: true);
                    await readSyn;
                    SniSmuxHeader synHeader = new();
                    synHeader.Read(syn);
                    Assert.Equal((byte)SniSmuxFlags.SMUX_SYN, synHeader.flags);
                    Assert.Equal((ushort)i, synHeader.sessionId);
                    if (i > 0)
                    {
                        SniPacket? pending = null;
                        Assert.Equal(TdsEnums.SNI_SUCCESS_IO_PENDING, sessions[i].ReceiveAsync(ref pending));
                        Assert.Null(pending);
                    }
                }

                byte[] header = new byte[SniSmuxHeader.HEADER_LENGTH];
                new SniSmuxHeader
                {
                    SMID = 83,
                    flags = (byte)SniSmuxFlags.SMUX_ACK,
                    length = SniSmuxHeader.HEADER_LENGTH,
                    highwater = 4,
                }.Write(header);
                packet = connection.Handle.RentPacket(0, header.Length);
                packet.SetAsyncIOCompletionCallback(mars.HandleReceiveComplete);
                packet.AppendData(header, header.Length);

                parser.State = TdsParserState.Broken;
                // Model a pending read whose task was already completed and cleared by teardown.
                callbacks[1]._networkPacketTaskSource.SetCanceled();
                await Assert.ThrowsAsync<TaskCanceledException>(() => callbacks[1]._networkPacketTaskSource.Task);
                callbacks[1]._networkPacketTaskSource = null;
                connection.Handle.Dispose();
                mars.HandleReceiveComplete(packet, TdsEnums.SNI_SUCCESS);
                SniError receiveError = SniLoadHandle.LastError;
                Assert.IsType<ObjectDisposedException>(receiveError.exception);
                Assert.True(packet.IsInvalid);

                packet = connection.Handle.RentPacket(0, header.Length);
                packet.SetAsyncIOCompletionCallback(mars.HandleReceiveComplete);
                lock (mars.DemuxerSync)
                {
                    mars.HandleReceiveError(packet);
                }
                Assert.True(packet.IsInvalid);

                for (int i = 0; i < sessions.Length; i++)
                {
                    if (i > 1)
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(() =>
                            callbacks[i]._networkPacketTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(5)));
                    }
                    else
                    {
                        Assert.Null(callbacks[i]._networkPacketTaskSource);
                    }
                    Assert.Equal(TdsEnums.SNI_ERROR, sessions[i].Receive(out SniPacket? received, 1000));
                    Assert.Null(received);
                    Assert.Same(receiveError, SniLoadHandle.LastError);
                    Assert.Equal(0, callbacks[i].DecrementPendingCallbacks(release: false));
                    Assert.Equal(TdsEnums.SNI_ERROR, sessions[i].ReceiveAsync(ref received));
                    Assert.Null(received);
                    Assert.Same(receiveError, SniLoadHandle.LastError);
                }
            }
            finally
            {
                if (packet is not null && !packet.IsInvalid)
                {
                    connection.Handle.ReturnPacket(packet);
                }
                foreach (TdsParserStateObjectManaged callback in callbacks)
                {
                    callback.Dispose();
                }
                parser._physicalStateObj.Dispose();
            }
        }

        /// <summary>
        /// Successful re-arms recycle consumed packets without clearing the new packet's
        /// callback or data, including after the physical packet pool starts reusing objects.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MarsReceive_Connected_RecyclesConsumedPackets(bool namedPipe)
        {
            using LocalConnection connection = new(namedPipe);
            SniMarsConnection mars = new(connection.Handle);
            SniPacket? packet = null;
            try
            {
                for (byte value = 1; value <= 3; value++)
                {
                    TaskCompletionSource<(SniPacket Packet, uint Error)> completion =
                        new(TaskCreationOptions.RunContinuationsAsynchronously);
                    connection.Handle.SetAsyncCallbacks(
                        (received, error) => completion.SetResult((received, error)), null);
                    SniPacket? previousPacket = packet;
                    Assert.Equal(TdsEnums.SNI_SUCCESS_IO_PENDING, mars.ReceiveAsync(ref packet));
                    Assert.NotNull(packet);
                    Assert.True(previousPacket is null || ReferenceEquals(previousPacket, packet) || previousPacket.IsInvalid);
                    Assert.True(packet.HasAsyncIOCompletionCallback);
                    await connection.Peer.WriteAsync(new byte[] { value });
                    (SniPacket received, uint error) = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Same(packet, received);
                    Assert.Equal(TdsEnums.SNI_SUCCESS, error);
                    byte[] data = new byte[1];
                    Assert.Equal(1, packet.TakeData(data, 0, data.Length));
                    Assert.Equal(value, data[0]);
                }
            }
            finally
            {
                if (packet is not null && !packet.IsInvalid)
                {
                    connection.Handle.ReturnPacket(packet);
                }
            }
        }

        /// <summary>
        /// Establishes a real local transport without SQL Server and owns both ends.
        /// </summary>
        private sealed class LocalConnection : IDisposable
        {
            public SniHandle Handle { get; }

            public Stream Peer { get; }

            public LocalConnection(bool namedPipe)
            {
                if (namedPipe)
                {
                    string pipeName = $"SqlClient-{Guid.NewGuid():N}";
                    NamedPipeServerStream server = new(
                        pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    Peer = server;
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
                    Task accept = server.WaitForConnectionAsync(timeout.Token);
                    Handle = new SniNpHandle(
                        ".", pipeName, TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                        tlsFirst: false, hostNameInCertificate: null, serverCertificateFilename: null);
                    accept.GetAwaiter().GetResult();
                }
                else
                {
                    using TcpListener listener = new(IPAddress.Loopback, 0);
                    listener.Start();
                    SQLDNSInfo? pendingDnsInfo = null;
                    Handle = new SniTcpHandle(
                        IPAddress.Loopback.ToString(),
                        ((IPEndPoint)listener.LocalEndpoint).Port,
                        TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                        parallel: false,
                        SqlConnectionIPAddressPreference.IPv4First,
                        cachedFQDN: nameof(SniReceiveTests),
                        ref pendingDnsInfo,
                        tlsFirst: false,
                        hostNameInCertificate: null,
                        serverCertificateFilename: null);
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
                    Peer = new NetworkStream(
                        listener.AcceptSocketAsync(timeout.Token).AsTask().GetAwaiter().GetResult(), ownsSocket: true);
                }
                Assert.Equal(TdsEnums.SNI_SUCCESS, Handle.Status);
            }

            /// <summary>
            /// Closes the client handle and its local server stream.
            /// </summary>
            public void Dispose()
            {
                Handle.Dispose();
                Peer.Dispose();
            }
        }
    }
}

#endif
