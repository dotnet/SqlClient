// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Interop.Windows.Sni;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal class TdsParserStateObjectNative : TdsParserStateObject
    {
        private readonly WritePacketCache _writePacketCache = new WritePacketCache(); // Store write packets that are ready to be re-used

        private readonly Dictionary<IntPtr, SNIPacket> _pendingWritePackets = new Dictionary<IntPtr, SNIPacket>(); // Stores write packets that have been sent to SNI, but have not yet finished writing (i.e. we are waiting for SNI's callback)

        internal TdsParserStateObjectNative(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
            : base(parser, physicalConnection.Handle, async)
        {
        }

        public TdsParserStateObjectNative(TdsParser parser)
            : base(parser)
        {
        }

        ////////////////
        // Properties //
        ////////////////

        internal override uint Status => _sessionHandle != null ? _sessionHandle.Status : TdsEnums.SNI_UNINITIALIZED;

        internal override SessionHandle SessionHandle => SessionHandle.FromNativeHandle(_sessionHandle);

        protected override PacketHandle EmptyReadPacket => PacketHandle.FromNativePointer(default);

        internal override Guid? SessionId => default;

        protected override uint SniPacketGetData(PacketHandle packet, byte[] _inBuff, ref uint dataSize)
        {
            Debug.Assert(packet.Type == PacketHandle.NativePointerType, "unexpected packet type when requiring NativePointer");
            return SniNativeWrapper.SniPacketGetData(packet.NativePointer, _inBuff, ref dataSize);
        }

        protected override bool CheckPacket(PacketHandle packet, TaskCompletionSource<object> source)
        {
            Debug.Assert(packet.Type == PacketHandle.NativePointerType, "unexpected packet type when requiring NativePointer");
            IntPtr ptr = packet.NativePointer;
            return IntPtr.Zero == ptr || IntPtr.Zero != ptr && source != null;
        }

        protected override void RemovePacketFromPendingList(PacketHandle ptr)
        {
            Debug.Assert(ptr.Type == PacketHandle.NativePointerType, "unexpected packet type when requiring NativePointer");
            IntPtr pointer = ptr.NativePointer;

            lock (_writePacketLockObject)
            {
                if (_pendingWritePackets.TryGetValue(pointer, out SNIPacket recoveredPacket))
                {
                    _pendingWritePackets.Remove(pointer);
                    _writePacketCache.Add(recoveredPacket);
                }
#if DEBUG
                else
                {
                    Debug.Fail("Removing a packet from the pending list that was never added to it");
                }
#endif
            }
        }

        internal override bool IsFailedHandle() => _sessionHandle.Status != TdsEnums.SNI_SUCCESS;

        internal override bool IsPacketEmpty(PacketHandle readPacket)
        {
            Debug.Assert(readPacket.Type == PacketHandle.NativePointerType || readPacket.Type == 0, "unexpected packet type when requiring NativePointer");
            return IntPtr.Zero == readPacket.NativePointer;
        }

        internal override void ReleasePacket(PacketHandle syncReadPacket)
        {
            Debug.Assert(syncReadPacket.Type == PacketHandle.NativePointerType, "unexpected packet type when requiring NativePointer");
            SniNativeWrapper.SniPacketRelease(syncReadPacket.NativePointer);
        }

        internal override PacketHandle ReadAsync(SessionHandle handle, out uint error)
        {
            IntPtr readPacketPtr = IntPtr.Zero;
            error = SniNativeWrapper.SniReadAsync(handle.NativeHandle, ref readPacketPtr);
            return PacketHandle.FromNativePointer(readPacketPtr);
        }

        internal override PacketHandle ReadSyncOverAsync(int timeoutRemaining, out uint error)
        {
            SNIHandle handle = Handle ?? throw ADP.ClosedConnectionError();
            IntPtr readPacketPtr = IntPtr.Zero;
            error = SniNativeWrapper.SniReadSyncOverAsync(handle, ref readPacketPtr, timeoutRemaining);
            return PacketHandle.FromNativePointer(readPacketPtr);
        }

        internal override uint WritePacket(PacketHandle packet, bool sync)
        {
            Debug.Assert(packet.Type == PacketHandle.NativePacketType, "unexpected packet type when requiring NativePacket");
            return SniNativeWrapper.SniWritePacket(Handle, packet.NativePacket, sync);
        }

        internal override PacketHandle AddPacketToPendingList(PacketHandle packetToAdd)
        {
            Debug.Assert(packetToAdd.Type == PacketHandle.NativePacketType, "unexpected packet type when requiring NativePacket");
            SNIPacket packet = packetToAdd.NativePacket;
            Debug.Assert(packet == _sniPacket, "Adding a packet other than the current packet to the pending list");
            _sniPacket = null;
            IntPtr pointer = packet.DangerousGetHandle();

            lock (_writePacketLockObject)
            {
                _pendingWritePackets.Add(pointer, packet);
            }

            return PacketHandle.FromNativePointer(pointer);
        }

        internal override bool IsValidPacket(PacketHandle packetPointer)
        {
            Debug.Assert(packetPointer.Type == PacketHandle.NativePointerType || packetPointer.Type == PacketHandle.NativePacketType, "unexpected packet type when requiring NativePointer");

            return (packetPointer.Type == PacketHandle.NativePointerType && packetPointer.NativePointer != IntPtr.Zero)
                || (packetPointer.Type == PacketHandle.NativePacketType && packetPointer.NativePacket != null);
        }

        internal override PacketHandle GetResetWritePacket(int dataSize)
        {
            if (_sniPacket != null)
            {
                SniNativeWrapper.SniPacketReset(Handle, IoType.WRITE, _sniPacket, ConsumerNumber.SNI_Consumer_SNI);
            }
            else
            {
                lock (_writePacketLockObject)
                {
                    _sniPacket = _writePacketCache.Take(Handle);
                }
            }
            return PacketHandle.FromNativePacket(_sniPacket);
        }

        internal override void ClearAllWritePackets()
        {
            if (_sniPacket != null)
            {
                _sniPacket.Dispose();
                _sniPacket = null;
            }
            lock (_writePacketLockObject)
            {
                Debug.Assert(_pendingWritePackets.Count == 0 && _asyncWriteCount == 0, "Should not clear all write packets if there are packets pending");
                _writePacketCache.Clear();
            }
        }

        internal override uint SniGetConnectionId(ref Guid clientConnectionId)
            => SniNativeWrapper.SniGetConnectionId(Handle, ref clientConnectionId);

        internal override uint DisableSsl()
            => SniNativeWrapper.SniRemoveProvider(Handle, Provider.SSL_PROV);

        internal override uint EnableMars(ref uint info)
            => SniNativeWrapper.SniAddProvider(Handle, Provider.SMUX_PROV, ref info);

        internal override uint PostReadAsyncForMars(TdsParserStateObject physicalStateObject)
        {
            // HACK HACK HACK - for Async only
            // Have to post read to initialize MARS - will get callback on this when connection goes
            // down or is closed.

            PacketHandle temp = default;
            uint error = TdsEnums.SNI_SUCCESS;

#if NETFRAMEWORK
            RuntimeHelpers.PrepareConstrainedRegions();
#endif
            try
            { }
            finally
            {
                IncrementPendingCallbacks();
                SessionHandle handle = SessionHandle;
                // we do not need to consider partial packets when making this read because we
                // expect this read to pend. a partial packet should not exist at setup of the
                // parser
                Debug.Assert(physicalStateObject.PartialPacket == null);
                temp = ReadAsync(handle, out error);

                Debug.Assert(temp.Type == PacketHandle.NativePointerType, "unexpected packet type when requiring NativePointer");

                if (temp.NativePointer != IntPtr.Zero)
                {
                    // Be sure to release packet, otherwise it will be leaked by native.
                    ReleasePacket(temp);
                }
            }

            Debug.Assert(IntPtr.Zero == temp.NativePointer, "unexpected syncReadPacket without corresponding SNIPacketRelease");
            return error;
        }

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize)
            => SniNativeWrapper.SniSetInfo(Handle, QueryType.SNI_QUERY_CONN_BUFSIZE, ref unsignedPacketSize);

        internal override uint WaitForSSLHandShakeToComplete(out uint protocolVersion)
        {
            return SniNativeWrapper.SniWaitForSslHandshakeToComplete(Handle, GetTimeoutRemaining(), out protocolVersion);
        }

        internal override SniErrorDetails GetErrorDetails()
        {
            SniNativeWrapper.SniGetLastError(out SniError sniError);

            return new SniErrorDetails(sniError.errorMessage, sniError.nativeError, sniError.sniError, (int)sniError.provider, sniError.lineNumber, sniError.function);
        }

        internal override void DisposePacketCache()
        {
            lock (_writePacketLockObject)
            {
#if NETFRAMEWORK
                RuntimeHelpers.PrepareConstrainedRegions();
#endif
                try
                { }
                finally
                {
                    _writePacketCache.Dispose();
                    // Do not set _writePacketCache to null, just in case a WriteAsyncCallback completes after this point
                }
            }
        }

        internal override SspiContextProvider CreateSspiContextProvider() => new NativeSspiContextProvider();
    }
}
