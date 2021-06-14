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
            Debug.Assert(physicalConnection is TdsParserStateObjectManaged, "Expected a stateObject of type " + GetType());
            TdsParserStateObjectManaged managedSNIObject = physicalConnection as TdsParserStateObjectManaged;
            _sessionHandle = managedSNIObject.CreateMarsSession(this, async);
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreateSessionHandle | Info | State Object Id {0}, Session Id {1}", _objectID, _sessionHandle?.ConnectionId);
        }

        internal SNIMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreateMarsSession | Info | State Object Id {0}, Session Id {1}, Async = {2}", _objectID, _sessionHandle?.ConnectionId, async);
            return _marsConnection.CreateMarsSession(callbackObject, async);
        }

        /// <summary>
        /// Copies data in SNIPacket to given byte array parameter
        /// </summary>
        /// <param name="packet">SNIPacket object containing data packets</param>
        /// <param name="inBuff">Destination byte array where data packets are copied to</param>
        /// <param name="dataSize">Length of data packets</param>
        /// <returns>SNI error status</returns>
        protected override uint SNIPacketGetData(PacketHandle packet, byte[] inBuff, ref uint dataSize)
        {
            int dataSizeInt = 0;
            packet.ManagedPacket.GetData(inBuff, ref dataSizeInt);
            dataSize = (uint)dataSizeInt;
            return TdsEnums.SNI_SUCCESS;
        }

        internal override void CreatePhysicalSNIHandle(string serverName, bool ignoreSniOpenTimeout, long timerExpire, out byte[] instanceName, ref byte[][] spnBuffer, bool flushCache, bool async, bool parallel, 
                                           SqlConnectionIPAddressPreference iPAddressPreference, string cachedFQDN, ref SQLDNSInfo pendingDNSInfo, bool isIntegratedSecurity)
        {
            _sessionHandle = SNIProxy.CreateConnectionHandle(serverName, ignoreSniOpenTimeout, timerExpire, out instanceName, ref spnBuffer, flushCache, async, parallel, isIntegratedSecurity, 
                                                        iPAddressPreference, cachedFQDN, ref pendingDNSInfo);
            if (_sessionHandle == null)
            {
                _parser.ProcessSNIError(this);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreatePhysicalSNIHandle | Info | State Object Id {0}, Session Id {1}, ServerName {2}, Async = {3}", _objectID, _sessionHandle?.ConnectionId, serverName, async);
                if (async)
                {
                    // Create call backs and allocate to the session handle
                    _sessionHandle.SetAsyncCallbacks(ReadAsyncCallback, WriteAsyncCallback);
                }
            }
        }

        // The assignment will be happened right after we resolve DNS in managed SNI layer
        internal override void AssignPendingDNSInfo(string userProtocol, string DNSCacheKey, ref SQLDNSInfo pendingDNSInfo)
        {
            // No-op
        }

        internal void ReadAsyncCallback(SNIPacket packet, uint error)
        {
            SNIHandle sessionHandle = _sessionHandle;
            if (sessionHandle != null)
            {
                ReadAsyncCallback(IntPtr.Zero, PacketHandle.FromManagedPacket(packet), error);
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReadAsyncCallback | Info | State Object Id {0}, Session Id {1}, Error code returned {2}", _objectID, _sessionHandle?.ConnectionId, error);
#if DEBUG
                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.ReadAsyncCallback | TRC | State Object Id {0}, Session Id {1}, Packet Id = {2}, Error code returned {3}", _objectID, _sessionHandle?.ConnectionId, packet?._id, error);
#endif
                sessionHandle?.ReturnPacket(packet);
            }
            else
            {
                // clear the packet and drop it to GC because we no longer know how to return it to the correct owner
                // this can only happen if a packet is in-flight when the _sessionHandle is cleared
                packet.Release();
            }
        }

        internal void WriteAsyncCallback(SNIPacket packet, uint sniError)
        {
            SNIHandle sessionHandle = _sessionHandle;
            if (sessionHandle != null)
            {
                WriteAsyncCallback(IntPtr.Zero, PacketHandle.FromManagedPacket(packet), sniError);
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.WriteAsyncCallback | Info | State Object Id {0}, Session Id {1}, Error code returned {2}", _objectID, _sessionHandle?.ConnectionId, sniError);
#if DEBUG
                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.WriteAsyncCallback | TRC | State Object Id {0}, Session Id {1}, Packet Id = {2}, Error code returned {3}", _objectID, _sessionHandle?.ConnectionId, packet?._id, sniError);
#endif
                sessionHandle?.ReturnPacket(packet);
            }
            else
            {
                // clear the packet and drop it to GC because we no longer know how to return it to the correct owner
                // this can only happen if a packet is in-flight when the _sessionHandle is cleared
                packet.Release();
            }
        }

        protected override void RemovePacketFromPendingList(PacketHandle packet)
        {
            // No-Op
        }

        internal override void Dispose()
        {
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.Dispose | Info | State Object Id {0}, Session Id {1}, Disposing session Handle and counters.", _objectID, _sessionHandle?.ConnectionId);
            SNIHandle sessionHandle = _sessionHandle;

            _sessionHandle = null;
            _marsConnection = null;

            DisposeCounters();

            if (null != sessionHandle)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.Dispose | Info | State Object Id {0}, Session Id {1}, sessionHandle is available, disposing session.", _objectID, _sessionHandle?.ConnectionId);
                sessionHandle.Dispose();
                DecrementPendingCallbacks(true); // Will dispose of GC handle.
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.Dispose | Info | State Object Id {0}, sessionHandle not available, could not dispose session.", _objectID);
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

            error = handle.Receive(out SNIPacket packet, timeoutRemaining);
            
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReadSyncOverAsync | Info | State Object Id {0}, Session Id {1}", _objectID, _sessionHandle?.ConnectionId);
#if DEBUG
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.ReadSyncOverAsync | TRC | State Object Id {0}, Session Id {1}, Packet {2} received, Packet owner Id {3}, Packet dataLeft {4}", _objectID, _sessionHandle?.ConnectionId, packet?._id, packet?._owner.ConnectionId, packet?.DataLeft);
#endif
            return PacketHandle.FromManagedPacket(packet);
        }

        protected override PacketHandle EmptyReadPacket => PacketHandle.FromManagedPacket(null);

        internal override bool IsPacketEmpty(PacketHandle packet) => packet.ManagedPacket == null;

        internal override void ReleasePacket(PacketHandle syncReadPacket)
        {
            SNIPacket packet = syncReadPacket.ManagedPacket;
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReleasePacket | Info | State Object Id {0}, Session Id {1}, Packet DataLeft {2}", _objectID, _sessionHandle?.ConnectionId, packet?.DataLeft);
#if DEBUG
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.ReleasePacket | TRC | State Object Id {0}, Session Id {1}, Packet {2} will be released, Packet Owner Id {3}, Packet dataLeft {4}", _objectID, _sessionHandle?.ConnectionId, packet?._id, packet?._owner.ConnectionId, packet?.DataLeft);
#endif
            if (packet != null)
            {
                SNIHandle handle = Handle;
                handle.ReturnPacket(packet);
            }
        }

        internal override uint CheckConnection()
        {
            SNIHandle handle = Handle;
            return handle == null ? TdsEnums.SNI_SUCCESS : handle.CheckConnection();
        }

        internal override PacketHandle ReadAsync(SessionHandle handle, out uint error)
        {
            SNIPacket packet = null;
            error = handle.ManagedHandle.ReceiveAsync(ref packet);

            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReadAsync | Info | State Object Id {0}, Session Id {1}, Packet DataLeft {2}", _objectID, _sessionHandle?.ConnectionId, packet?.DataLeft);
            return PacketHandle.FromManagedPacket(packet);
        }

        internal override PacketHandle CreateAndSetAttentionPacket()
        {
            PacketHandle packetHandle = GetResetWritePacket(TdsEnums.HEADER_LEN);
#if DEBUG
            Debug.Assert(packetHandle.ManagedPacket.IsActive, "rental packet is not active a serious pooling error may have occurred");
#endif
            SetPacketData(packetHandle, SQL.AttentionHeader, TdsEnums.HEADER_LEN);
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreateAndSetAttentionPacket | Info | State Object Id {0}, Session Id {1}", _objectID, _sessionHandle?.ConnectionId);

            packetHandle.ManagedPacket.IsOutOfBand = true;
            return packetHandle;
        }

        internal override uint WritePacket(PacketHandle packetHandle, bool sync)
        {
            uint result;
            SNIHandle handle = Handle;
            SNIPacket packet = packetHandle.ManagedPacket;
            if (sync)
            {
                result = handle.Send(packet);
                handle.ReturnPacket(packet);
            }
            else
            {
                result = handle.SendAsync(packet);
            }

            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.WritePacket | Info | Session Id {0}, SendAsync Result {1}", handle?.ConnectionId, result);
            return result;
        }

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

        internal override void SetPacketData(PacketHandle packet, byte[] buffer, int bytesUsed)
        {
            packet.ManagedPacket.AppendData(buffer, bytesUsed);
        }

        internal override uint SniGetConnectionId(ref Guid clientConnectionId)
        {
            clientConnectionId = Handle.ConnectionId;
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.GetConnectionId | Info | Session Id {0}", clientConnectionId);
            return TdsEnums.SNI_SUCCESS;
        }

        internal override uint DisableSsl()
        {
            SNIHandle handle = Handle;
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.DisableSsl | Info | Session Id {0}", handle?.ConnectionId);
            handle.DisableSsl();
            return TdsEnums.SNI_SUCCESS;
        }

        internal override uint EnableMars(ref uint info)
        {
            _marsConnection = new SNIMarsConnection(Handle);
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableMars | Info | State Object Id {0}, Session Id {1}", _objectID, _sessionHandle?.ConnectionId);

            if (_marsConnection.StartReceive() == TdsEnums.SNI_SUCCESS_IO_PENDING)
            {
                return TdsEnums.SNI_SUCCESS;
            }

            return TdsEnums.SNI_ERROR;
        }

        internal override uint EnableSsl(ref uint info)
        {
            SNIHandle handle = Handle;
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableSsl | Info | Session Id {0}", handle?.ConnectionId);
                return handle.EnableSsl(info);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableSsl | Err | Session Id {0}, SNI Handshake failed with exception: {1}", handle?.ConnectionId, e?.Message);
                return SNICommon.ReportSNIError(SNIProviders.SSL_PROV, SNICommon.HandshakeFailureError, e);
            }
        }

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize)
        {
            Handle.SetBufferSize((int)unsignedPacketSize);
            return TdsEnums.SNI_SUCCESS;
        }

        internal override uint GenerateSspiClientContext(byte[] receivedBuff, uint receivedLength, ref byte[] sendBuff, ref uint sendLength, byte[][] _sniSpnBuffer)
        {
            if (_sspiClientContextStatus == null)
            {
                _sspiClientContextStatus = new SspiClientContextStatus();
            }

            SNIProxy.GenSspiClientContext(_sspiClientContextStatus, receivedBuff, ref sendBuff, _sniSpnBuffer);
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.GenerateSspiClientContext | Info | Session Id {0}", _sessionHandle?.ConnectionId);
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
