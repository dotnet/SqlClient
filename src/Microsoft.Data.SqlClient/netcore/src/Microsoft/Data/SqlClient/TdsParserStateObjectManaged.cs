// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.SNI
{
    internal class TdsParserStateObjectManaged : TdsParserStateObject
    {
        private SNIMarsConnection _marsConnection;
        private SNIHandle _sessionHandle;
        private SspiClientContextStatus _sspiClientContextStatus;

        public TdsParserStateObjectManaged(TdsParser parser) : base(parser) { }

        internal TdsParserStateObjectManaged(TdsParser parser, TdsParserStateObject physicalConnection, bool async) :
            base(parser, physicalConnection, async)
        { }

        internal SNIHandle Handle => _sessionHandle;

        internal override uint Status => _sessionHandle != null ? _sessionHandle.Status : TdsEnums.SNI_UNINITIALIZED;

        internal override SessionHandle SessionHandle => SessionHandle.FromManagedSession(_sessionHandle);

        protected override bool CheckPacket(PacketHandle packet, TaskCompletionSource<object> source)
        {
            SNIPacket p = packet.ManagedPacket;
            return p.IsInvalid || source != null;
        }

        protected override void CreateSessionHandle(TdsParserStateObject physicalConnection, bool async)
        {
            Debug.Assert(physicalConnection is TdsParserStateObjectManaged, "Expected a stateObject of type " + this.GetType());
            TdsParserStateObjectManaged managedSNIObject = physicalConnection as TdsParserStateObjectManaged;

            _sessionHandle = managedSNIObject.CreateMarsSession(this, async);
        }

        internal SNIMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            return _marsConnection.CreateMarsSession(callbackObject, async);
        }

        protected override uint SNIPacketGetData(PacketHandle packet, byte[] _inBuff, ref uint dataSize)
            => SNIProxy.GetInstance().PacketGetData(packet.ManagedPacket, _inBuff, ref dataSize);

        internal override void CreatePhysicalSNIHandle(string serverName, bool ignoreSniOpenTimeout, long timerExpire, out byte[] instanceName, ref byte[] spnBuffer, bool flushCache, bool async, bool parallel, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo, bool isIntegratedSecurity)
        {
            _sessionHandle = SNIProxy.GetInstance().CreateConnectionHandle(this, serverName, ignoreSniOpenTimeout, timerExpire, out instanceName, ref spnBuffer, flushCache, async, parallel, isIntegratedSecurity, cachedFQDN, ref pendingDNSInfo);
            if (_sessionHandle == null)
            {
                _parser.ProcessSNIError(this);
            }
            else if (async)
            {
                // Create call backs and allocate to the session handle
                _sessionHandle.SetAsyncCallbacks(ReadAsyncCallback, WriteAsyncCallback);
            }
        }

        // The assignment will be happened right after we resolve DNS in managed SNI layer
        internal override void AssignPendingDNSInfo(string userProtocol, string DNSCacheKey, ref SQLDNSInfo pendingDNSInfo)
        {
            // No-op
        }

        internal void ReadAsyncCallback(SNIPacket packet, uint error)
        {
            ReadAsyncCallback(IntPtr.Zero, PacketHandle.FromManagedPacket(packet), error);
            _sessionHandle.ReturnPacket(packet);
        }

        internal void WriteAsyncCallback(SNIPacket packet, uint sniError)
        {
            WriteAsyncCallback(IntPtr.Zero, PacketHandle.FromManagedPacket(packet), sniError);
            _sessionHandle.ReturnPacket(packet);
        }

        protected override void RemovePacketFromPendingList(PacketHandle packet)
        {
            // No-Op
        }

        internal override void Dispose()
        {
            SNIHandle sessionHandle = _sessionHandle;

            _sessionHandle = null;
            _marsConnection = null;

            DisposeCounters();

            if (null != sessionHandle)
            {
                sessionHandle.Dispose();
                DecrementPendingCallbacks(true); // Will dispose of GC handle.
            }
        }

        internal override void DisposePacketCache()
        {
            // No - op
        }

        protected override void FreeGcHandle(int remaining, bool release)
        {
            // No - op
        }

        internal override bool IsFailedHandle() => _sessionHandle.Status != TdsEnums.SNI_SUCCESS;

        internal override PacketHandle ReadSyncOverAsync(int timeoutRemaining, out uint error)
        {
            SNIHandle handle = Handle;
            if (handle == null)
            {
                throw ADP.ClosedConnectionError();
            }
            error = SNIProxy.GetInstance().ReadSyncOverAsync(handle, out SNIPacket packet, timeoutRemaining);
            return PacketHandle.FromManagedPacket(packet);
        }

        protected override PacketHandle EmptyReadPacket => PacketHandle.FromManagedPacket(null);

        internal override bool IsPacketEmpty(PacketHandle packet) => packet.ManagedPacket == null;

        internal override void ReleasePacket(PacketHandle syncReadPacket)
        {
            SNIPacket packet = syncReadPacket.ManagedPacket;
            if (packet != null)
            {
                SNIHandle handle = Handle;
                handle.ReturnPacket(packet);
            }
        }

        internal override uint CheckConnection()
        {
            SNIHandle handle = Handle;
            return handle == null ? TdsEnums.SNI_SUCCESS : SNIProxy.GetInstance().CheckConnection(handle);
        }

        internal override PacketHandle ReadAsync(SessionHandle handle, out uint error)
        {
            error = SNIProxy.GetInstance().ReadAsync(handle.ManagedHandle, out SNIPacket packet);
            return PacketHandle.FromManagedPacket(packet);
        }

        internal override PacketHandle CreateAndSetAttentionPacket()
        {
            PacketHandle packetHandle = GetResetWritePacket(TdsEnums.HEADER_LEN);
#if DEBUG
            Debug.Assert(packetHandle.ManagedPacket.IsActive, "rental packet is not active a serious pooling error may have occurred");
#endif
            SetPacketData(packetHandle, SQL.AttentionHeader, TdsEnums.HEADER_LEN);
            packetHandle.ManagedPacket.IsOutOfBand = true;
            return packetHandle;
        }

        internal override uint WritePacket(PacketHandle packet, bool sync) =>
            SNIProxy.GetInstance().WritePacket(Handle, packet.ManagedPacket, sync);

        // No- Op in managed SNI
        internal override PacketHandle AddPacketToPendingList(PacketHandle packet) => packet;

        internal override bool IsValidPacket(PacketHandle packet)
        {
            Debug.Assert(packet.Type == PacketHandle.ManagedPacketType, "unexpected packet type when requiring ManagedPacket");
            return (
                packet.Type == PacketHandle.ManagedPacketType &&
                packet.ManagedPacket != null &&
                !packet.ManagedPacket.IsInvalid
             );
        }

        internal override PacketHandle GetResetWritePacket(int dataSize)
        {
            SNIHandle handle = Handle;
            SNIPacket packet = handle.RentPacket(headerSize: handle.ReserveHeaderSize, dataSize: dataSize);
#if DEBUG
            Debug.Assert(packet.IsActive, "packet is not active, a serious pooling error may have occurred");
#endif
            Debug.Assert(packet.ReservedHeaderSize == handle.ReserveHeaderSize, "failed to reserve header");
            return PacketHandle.FromManagedPacket(packet);
        }

        internal override void ClearAllWritePackets()
        {
            Debug.Assert(_asyncWriteCount == 0, "Should not clear all write packets if there are packets pending");
        }

        internal override void SetPacketData(PacketHandle packet, byte[] buffer, int bytesUsed) => SNIProxy.GetInstance().PacketSetData(packet.ManagedPacket, buffer, bytesUsed);

        internal override uint SniGetConnectionId(ref Guid clientConnectionId) => SNIProxy.GetInstance().GetConnectionId(Handle, ref clientConnectionId);

        internal override uint DisableSsl() => SNIProxy.GetInstance().DisableSsl(Handle);

        internal override uint EnableMars(ref uint info)
        {
            _marsConnection = new SNIMarsConnection(Handle);
            if (_marsConnection.StartReceive() == TdsEnums.SNI_SUCCESS_IO_PENDING)
            {
                return TdsEnums.SNI_SUCCESS;
            }

            return TdsEnums.SNI_ERROR;
        }

        internal override uint EnableSsl(ref uint info) => SNIProxy.GetInstance().EnableSsl(Handle, info);

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize) => SNIProxy.GetInstance().SetConnectionBufferSize(Handle, unsignedPacketSize);

        internal override uint GenerateSspiClientContext(byte[] receivedBuff, uint receivedLength, ref byte[] sendBuff, ref uint sendLength, byte[] _sniSpnBuffer)
        {
            if (_sspiClientContextStatus == null)
            {
                _sspiClientContextStatus = new SspiClientContextStatus();
            }
            
            SNIProxy.GetInstance().GenSspiClientContext(_sspiClientContextStatus, receivedBuff, ref sendBuff, _sniSpnBuffer);
            sendLength = (uint)(sendBuff != null ? sendBuff.Length : 0);
            return 0;
        }

        internal override uint WaitForSSLHandShakeToComplete(out int protocolVersion)
        {
            protocolVersion = Handle.ProtocolVersion;
            return 0;
        }
    }
}
