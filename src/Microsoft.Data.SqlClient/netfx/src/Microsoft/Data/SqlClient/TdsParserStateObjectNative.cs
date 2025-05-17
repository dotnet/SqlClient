// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Interop.Windows.Sni;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal class TdsParserStateObjectNative : TdsParserStateObject
    {
        internal TdsParserStateObjectNative(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
            : base(parser, physicalConnection.Handle, async)
        {
        }

        internal TdsParserStateObjectNative(TdsParser parser)
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

        internal override uint SniGetConnectionId(ref Guid clientConnectionId)
            => SniNativeWrapper.SniGetConnectionId(Handle, ref clientConnectionId);

        internal override uint DisableSsl()
            => SniNativeWrapper.SniRemoveProvider(Handle, Provider.SSL_PROV);

        internal override uint EnableMars(ref uint info)
            => SniNativeWrapper.SniAddProvider(Handle, Provider.SMUX_PROV, ref info);

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize)
            => SniNativeWrapper.SniSetInfo(Handle, QueryType.SNI_QUERY_CONN_BUFSIZE, ref unsignedPacketSize);

        internal override SspiContextProvider CreateSspiContextProvider() => new NativeSspiContextProvider();
    }
}
