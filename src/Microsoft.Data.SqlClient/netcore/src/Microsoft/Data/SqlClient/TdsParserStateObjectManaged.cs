// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

// @TODO: If this is in the manages SNI namespace, it should be in the managed SNI folder
namespace Microsoft.Data.SqlClient.ManagedSni
{
    internal sealed class TdsParserStateObjectManaged : TdsParserStateObject
    {
        private SniMarsConnection? _marsConnection;
        private SniHandle? _sessionHandle;

        public TdsParserStateObjectManaged(TdsParser parser) : base(parser) { }

        internal TdsParserStateObjectManaged(TdsParser parser, TdsParserStateObject physicalConnection, bool async) :
            base(parser, physicalConnection, async)
        { }

        internal override uint Status => _sessionHandle != null ? _sessionHandle.Status : TdsEnums.SNI_UNINITIALIZED;

        internal override SessionHandle SessionHandle => SessionHandle.FromManagedSession(_sessionHandle);

        protected override bool CheckPacket(PacketHandle packet, TaskCompletionSource<object> source)
        {
            SniPacket p = packet.ManagedPacket;
            return p.IsInvalid || source != null;
        }

        protected override void CreateSessionHandle(TdsParserStateObject physicalConnection, bool async)
        {
            Debug.Assert(physicalConnection is TdsParserStateObjectManaged, "Expected a stateObject of type " + GetType());
            if (physicalConnection is TdsParserStateObjectManaged managedSNIObject)
            {
                _sessionHandle = managedSNIObject.CreateMarsSession(this, async);
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreateSessionHandle | Info | State Object Id {0}, Session Id {1}", _objectID, _sessionHandle?.ConnectionId);
            }
            else
            {
                throw ADP.IncorrectPhysicalConnectionType();
            }
        }

        internal SniMarsHandle CreateMarsSession(object callbackObject, bool async)
        {
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreateMarsSession | Info | State Object Id {0}, Session Id {1}, Async = {2}", _objectID, _sessionHandle?.ConnectionId, async);
            if (_marsConnection is null)
            {
                ThrowClosedConnection();
            }
            return _marsConnection.CreateMarsSession(callbackObject, async);
        }

        /// <summary>
        /// Copies data in SNIPacket to given byte array parameter
        /// </summary>
        /// <param name="packet">SNIPacket object containing data packets</param>
        /// <param name="inBuff">Destination byte array where data packets are copied to</param>
        /// <param name="dataSize">Length of data packets</param>
        /// <returns>SNI error status</returns>
        protected override uint SniPacketGetData(PacketHandle packet, byte[] inBuff, ref uint dataSize)
        {
            int dataSizeInt = 0;
            packet.ManagedPacket.GetData(inBuff, ref dataSizeInt);
            dataSize = (uint)dataSizeInt;
            return TdsEnums.SNI_SUCCESS;
        }

        internal override void CreatePhysicalSNIHandle(
            string serverName,
            TimeoutTimer timeout,
            out byte[] instanceName,
            ref string[] spns,
            bool flushCache,
            bool async,
            bool parallel,
            SqlConnectionIPAddressPreference iPAddressPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            string serverSPN,
            bool isIntegratedSecurity,
            bool tlsFirst,
            string hostNameInCertificate,
            string serverCertificateFilename)
        {
            SniHandle? sessionHandle = SniProxy.CreateConnectionHandle(serverName, timeout, out instanceName, ref spns, serverSPN,
                flushCache, async, parallel, isIntegratedSecurity, iPAddressPreference, cachedFQDN, ref pendingDNSInfo, tlsFirst,
                hostNameInCertificate, serverCertificateFilename);

            if (sessionHandle is not null)
            {
                _sessionHandle = sessionHandle;
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.CreatePhysicalSNIHandle | Info | State Object Id {0}, Session Id {1}, ServerName {2}, Async = {3}", _objectID, sessionHandle.ConnectionId, serverName, async);
                if (async)
                {
                    // Create call backs and allocate to the session handle
                    sessionHandle.SetAsyncCallbacks(ReadAsyncCallback, WriteAsyncCallback);
                }
            }
            else
            {
                _parser.ProcessSNIError(this);
            }
        }

        // The assignment will be happened right after we resolve DNS in managed SNI layer
        internal override void AssignPendingDNSInfo(string userProtocol, string DNSCacheKey, ref SQLDNSInfo pendingDNSInfo)
        {
            // No-op
        }

        internal void ReadAsyncCallback(SniPacket packet, uint error)
        {
            SniHandle? sessionHandle = _sessionHandle;
            if (sessionHandle is not null)
            {
                ReadAsyncCallback(IntPtr.Zero, PacketHandle.FromManagedPacket(packet), error);
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReadAsyncCallback | Info | State Object Id {0}, Session Id {1}, Error code returned {2}", _objectID, sessionHandle.ConnectionId, error);
#if DEBUG
                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.ReadAsyncCallback | TRC | State Object Id {0}, Session Id {1}, Packet Id = {2}, Error code returned {3}", _objectID, sessionHandle.ConnectionId, packet?._id, error);
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

        internal void WriteAsyncCallback(SniPacket packet, uint sniError)
        {
            SniHandle? sessionHandle = _sessionHandle;
            if (sessionHandle is not null)
            {
                WriteAsyncCallback(IntPtr.Zero, PacketHandle.FromManagedPacket(packet), sniError);
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.WriteAsyncCallback | Info | State Object Id {0}, Session Id {1}, Error code returned {2}", _objectID, sessionHandle.ConnectionId, sniError);
#if DEBUG
                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.WriteAsyncCallback | TRC | State Object Id {0}, Session Id {1}, Packet Id = {2}, Error code returned {3}", _objectID, sessionHandle.ConnectionId, packet?._id, sniError);
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
            SniHandle? sessionHandle = Interlocked.Exchange(ref _sessionHandle, null);
            if (sessionHandle is not null)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.Dispose | Info | State Object Id {0}, Session Id {1}, Disposing session Handle and counters.", _objectID, sessionHandle.ConnectionId);

                _marsConnection = null;

                DisposeCounters();

                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.Dispose | Info | State Object Id {0}, Session Id {1}, sessionHandle is available, disposing session.", _objectID, sessionHandle.ConnectionId);
                try
                {
                    sessionHandle.Dispose();
                }
                finally
                {
                    DecrementPendingCallbacks(true); // Will dispose of GC handle.
                }
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

        internal override bool IsFailedHandle()
        {
            SniHandle? sessionHandle = _sessionHandle;
            if (sessionHandle is not null)
            {
                return sessionHandle.Status != TdsEnums.SNI_SUCCESS;
            }
            return true;
        }


        internal override PacketHandle ReadSyncOverAsync(int timeoutRemaining, out uint error)
        {
            SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();

            error = sessionHandle.Receive(out SniPacket packet, timeoutRemaining);

            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReadSyncOverAsync | Info | State Object Id {0}, Session Id {1}", _objectID, sessionHandle.ConnectionId);
#if DEBUG
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.ReadSyncOverAsync | TRC | State Object Id {0}, Session Id {1}, Packet {2} received, Packet owner Id {3}, Packet dataLeft {4}", _objectID, sessionHandle.ConnectionId, packet?._id, packet?._owner.ConnectionId, packet?.DataLeft);
#endif
            return PacketHandle.FromManagedPacket(packet);
        }

        protected override PacketHandle EmptyReadPacket => PacketHandle.FromManagedPacket(null);

        internal override Guid? SessionId => _sessionHandle?.ConnectionId;

        internal override bool IsPacketEmpty(PacketHandle packet) => packet.ManagedPacket == null;

        internal override void ReleasePacket(PacketHandle syncReadPacket)
        {
            SniPacket packet = syncReadPacket.ManagedPacket;
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.ReleasePacket | Info | State Object Id {0}, Session Id {1}, Packet DataLeft {2}", _objectID, _sessionHandle?.ConnectionId, packet?.DataLeft);
#if DEBUG
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObjectManaged.ReleasePacket | TRC | State Object Id {0}, Session Id {1}, Packet {2} will be released, Packet Owner Id {3}, Packet dataLeft {4}", _objectID, _sessionHandle?.ConnectionId, packet?._id, packet?._owner.ConnectionId, packet?.DataLeft);
#endif
            if (packet is not null)
            {
                SniHandle? sessionHandle = _sessionHandle;
                if (sessionHandle is not null)
                {
                    sessionHandle.ReturnPacket(packet);
                }
                else
                {
                    // clear the packet and drop it to GC because we no longer know how to return it to the correct owner
                    // this can only happen if a packet is in-flight when the _sessionHandle is cleared
                    packet.Release();
                }
            }
        }

        internal override uint CheckConnection()
        {
            SniHandle? handle = GetSessionSNIHandleHandleOrThrow();
            return handle is null ? TdsEnums.SNI_SUCCESS : handle.CheckConnection();
        }

        internal override PacketHandle ReadAsync(SessionHandle handle, out uint error)
        {
            SniPacket? packet = null;
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
            uint result = TdsEnums.SNI_UNINITIALIZED;
            SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();
            SniPacket? packet = packetHandle.ManagedPacket;

            if (sync)
            {
                result = sessionHandle.Send(packet);
                sessionHandle.ReturnPacket(packet);
            }
            else
            {
                result = sessionHandle.SendAsync(packet);
            }

            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.WritePacket | Info | Session Id {0}, SendAsync Result {1}", sessionHandle.ConnectionId, result);
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
            SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();
            SniPacket packet = sessionHandle.RentPacket(headerSize: sessionHandle.ReserveHeaderSize, dataSize: dataSize);
#if DEBUG
            Debug.Assert(packet.IsActive, "packet is not active, a serious pooling error may have occurred");
#endif
            Debug.Assert(packet.ReservedHeaderSize == sessionHandle.ReserveHeaderSize, "failed to reserve header");
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
            clientConnectionId = GetSessionSNIHandleHandleOrThrow().ConnectionId;
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.GetConnectionId | Info | Session Id {0}", clientConnectionId);
            return TdsEnums.SNI_SUCCESS;
        }

        internal override uint DisableSsl()
        {
            SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.DisableSsl | Info | Session Id {0}", sessionHandle.ConnectionId);
            sessionHandle.DisableSsl();
            return TdsEnums.SNI_SUCCESS;
        }

        internal override uint EnableMars(ref uint info)
        {
            SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();
            _marsConnection = new SniMarsConnection(sessionHandle);
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableMars | Info | State Object Id {0}, Session Id {1}", _objectID, sessionHandle.ConnectionId);

            if (_marsConnection.StartReceive() == TdsEnums.SNI_SUCCESS_IO_PENDING)
            {
                return TdsEnums.SNI_SUCCESS;
            }

            return TdsEnums.SNI_ERROR;
        }

        internal override uint PostReadAsyncForMars(TdsParserStateObject physicalStateObject) => TdsEnums.SNI_SUCCESS_IO_PENDING;

        internal override uint EnableSsl(ref uint info, bool tlsFirst, string serverCertificateFilename)
        {
            SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableSsl | Info | Session Id {0}", sessionHandle.ConnectionId);
                return sessionHandle.EnableSsl(info);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.EnableSsl | Err | Session Id {0}, SNI Handshake failed with exception: {1}", sessionHandle.ConnectionId, e.Message);
                return SniCommon.ReportSNIError(SniProviders.SSL_PROV, SniCommon.HandshakeFailureError, e);
            }
        }

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize)
        {
            GetSessionSNIHandleHandleOrThrow().SetBufferSize((int)unsignedPacketSize);
            return TdsEnums.SNI_SUCCESS;
        }

        internal override uint WaitForSSLHandShakeToComplete(out int protocolVersion)
        {
            protocolVersion = GetSessionSNIHandleHandleOrThrow().ProtocolVersion;
            return 0;
        }

        internal override SniErrorDetails GetErrorDetails()
        {
            SniError sniError = SniProxy.Instance.GetLastError();

            return new SniErrorDetails(sniError.errorMessage, sniError.nativeError, sniError.sniError,
                (int)sniError.provider, sniError.lineNumber, sniError.function,
                sniError.exception);
        }

        private SniHandle GetSessionSNIHandleHandleOrThrow()
        {
            SniHandle? sessionHandle = _sessionHandle;
            if (sessionHandle is null)
            {
                ThrowClosedConnection();
            }
            return sessionHandle;
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)] // this forces the exception throwing code not to be inlined for performance
        private void ThrowClosedConnection() => throw ADP.ClosedConnectionError();

        internal override SspiContextProvider CreateSspiContextProvider()
            => new NegotiateSspiContextProvider();
    }
}
