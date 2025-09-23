// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Interop.Windows.Sni;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient
{
    internal class TdsParserStateObjectNative : TdsParserStateObject
    {
        // protocol versions from native sni
        [Flags]
        private enum NativeProtocols
        {
            SP_PROT_SSL2_SERVER = 0x00000004,
            SP_PROT_SSL2_CLIENT = 0x00000008,
            SP_PROT_SSL3_SERVER = 0x00000010,
            SP_PROT_SSL3_CLIENT = 0x00000020,
            SP_PROT_TLS1_0_SERVER = 0x00000040,
            SP_PROT_TLS1_0_CLIENT = 0x00000080,
            SP_PROT_TLS1_1_SERVER = 0x00000100,
            SP_PROT_TLS1_1_CLIENT = 0x00000200,
            SP_PROT_TLS1_2_SERVER = 0x00000400,
            SP_PROT_TLS1_2_CLIENT = 0x00000800,
            SP_PROT_TLS1_3_SERVER = 0x00001000,
            SP_PROT_TLS1_3_CLIENT = 0x00002000,
            SP_PROT_NONE = 0x0
        }

        private SNIHandle _sessionHandle = null;              // the SNI handle we're to work on

        private SNIPacket _sniPacket = null;                // Will have to re-vamp this for MARS
        internal SNIPacket _sniAsyncAttnPacket = null;                // Packet to use to send Attn
        private readonly WritePacketCache _writePacketCache = new WritePacketCache(); // Store write packets that are ready to be re-used

        private GCHandle _gcHandle;                                    // keeps this object alive until we're closed.

        private readonly Dictionary<IntPtr, SNIPacket> _pendingWritePackets = new Dictionary<IntPtr, SNIPacket>(); // Stores write packets that have been sent to SNI, but have not yet finished writing (i.e. we are waiting for SNI's callback)

        internal TdsParserStateObjectNative(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
            : base(parser, physicalConnection, async)
        {
        }

        internal TdsParserStateObjectNative(TdsParser parser)
            : base(parser)
        {
        }

        #region Properties

        internal SNIHandle Handle => _sessionHandle;

        internal override uint Status => _sessionHandle != null ? _sessionHandle.Status : TdsEnums.SNI_UNINITIALIZED;

        internal override SessionHandle SessionHandle => SessionHandle.FromNativeHandle(_sessionHandle);

        protected override PacketHandle EmptyReadPacket => PacketHandle.FromNativePointer(default);

        internal override Guid? SessionId => default;

        #endregion

        protected override void CreateSessionHandle(TdsParserStateObject physicalConnection, bool async)
        {
            Debug.Assert(physicalConnection is TdsParserStateObjectNative, "Expected a stateObject of type " + this.GetType());
            TdsParserStateObjectNative nativeSNIObject = physicalConnection as TdsParserStateObjectNative;
            ConsumerInfo myInfo = CreateConsumerInfo(async);

            SQLDNSInfo cachedDNSInfo;
            bool ret = SQLFallbackDNSCache.Instance.GetDNSInfo(_parser.FQDNforDNSCache, out cachedDNSInfo);

            _sessionHandle = new SNIHandle(myInfo, nativeSNIObject.Handle, _parser.Connection.ConnectionOptions.IPAddressPreference, cachedDNSInfo);
        }

        // Retrieve the IP and port number from native SNI for TCP protocol. The IP information is stored temporarily in the
        // pendingSQLDNSObject but not in the DNS Cache at this point. We only add items to the DNS Cache after we receive the
        // IsSupported flag as true in the feature ext ack from server.
        internal override void AssignPendingDNSInfo(string userProtocol, string DNSCacheKey, ref SQLDNSInfo pendingDNSInfo)
        {
            uint result;
            ushort portFromSNI = 0;
            string IPStringFromSNI = string.Empty;
            IPAddress IPFromSNI;
            _parser.isTcpProtocol = false;
            Provider providerNumber = Provider.INVALID_PROV;

            if (string.IsNullOrEmpty(userProtocol))
            {

                result = SniNativeWrapper.SniGetProviderNumber(Handle, ref providerNumber);
                Debug.Assert(result == TdsEnums.SNI_SUCCESS, "Unexpected failure state upon calling SniGetProviderNumber");
                _parser.isTcpProtocol = (providerNumber == Provider.TCP_PROV);
            }
            else if (userProtocol == TdsEnums.TCP)
            {
                _parser.isTcpProtocol = true;
            }

            // serverInfo.UserProtocol could be empty
            if (_parser.isTcpProtocol)
            {
                result = SniNativeWrapper.SniGetConnectionPort(Handle, ref portFromSNI);
                Debug.Assert(result == TdsEnums.SNI_SUCCESS, "Unexpected failure state upon calling SniGetConnectionPort");

                result = SniNativeWrapper.SniGetConnectionIpString(Handle, ref IPStringFromSNI);
                Debug.Assert(result == TdsEnums.SNI_SUCCESS, "Unexpected failure state upon calling SniGetConnectionIPString");

                pendingDNSInfo = new SQLDNSInfo(DNSCacheKey, null, null, portFromSNI.ToString());

                if (IPAddress.TryParse(IPStringFromSNI, out IPFromSNI))
                {
                    if (System.Net.Sockets.AddressFamily.InterNetwork == IPFromSNI.AddressFamily)
                    {
                        pendingDNSInfo.AddrIPv4 = IPStringFromSNI;
                    }
                    else if (System.Net.Sockets.AddressFamily.InterNetworkV6 == IPFromSNI.AddressFamily)
                    {
                        pendingDNSInfo.AddrIPv6 = IPStringFromSNI;
                    }
                }
            }
            else
            {
                pendingDNSInfo = null;
            }
        }

        private ConsumerInfo CreateConsumerInfo(bool async)
        {
            ConsumerInfo myInfo = new ConsumerInfo();

            Debug.Assert(_outBuff.Length == _inBuff.Length, "Unexpected unequal buffers.");

            myInfo.defaultBufferSize = _outBuff.Length; // Obtain packet size from outBuff size.

            if (async)
            {
                myInfo.readDelegate = SNILoadHandle.SingletonInstance.ReadAsyncCallbackDispatcher;
                myInfo.writeDelegate = SNILoadHandle.SingletonInstance.WriteAsyncCallbackDispatcher;
                _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
                myInfo.key = (IntPtr)_gcHandle;
            }
            return myInfo;
        }

        internal override void CreatePhysicalSNIHandle(
            string serverName,
            TimeoutTimer timeout,
            out byte[] instanceName,
            out ManagedSni.ResolvedServerSpn resolvedSpn,
            bool flushCache,
            bool async,
            bool fParallel,
            TransparentNetworkResolutionState transparentNetworkResolutionState,
            int totalTimeout,
            SqlConnectionIPAddressPreference iPAddressPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            string serverSPN,
            bool isIntegratedSecurity,
            bool tlsFirst,
            string hostNameInCertificate,
            string serverCertificateFilename)
        {
            if (isIntegratedSecurity)
            {
                // now allocate proper length of buffer
                if (!string.IsNullOrEmpty(serverSPN))
                {
                    // Native SNI requires the Unicode encoding and any other encoding like UTF8 breaks the code.
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.Connect|SEC> Server SPN `{0}` from the connection string is used.", serverSPN);
                }
                else
                {
                    // Empty signifies to interop layer that SPN needs to be generated
                    serverSPN = string.Empty;
                }
            }

            ConsumerInfo myInfo = CreateConsumerInfo(async);

            // serverName : serverInfo.ExtendedServerName
            // may not use this serverName as key

            SQLDNSInfo cachedDNSInfo;
            bool ret = SQLFallbackDNSCache.Instance.GetDNSInfo(cachedFQDN, out cachedDNSInfo);

            _sessionHandle = new SNIHandle(myInfo, serverName, ref serverSPN, timeout.MillisecondsRemainingInt, out instanceName,
                flushCache, !async, fParallel, iPAddressPreference, cachedDNSInfo, hostNameInCertificate);
            resolvedSpn = new(serverSPN.TrimEnd());
        }

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

        public void ReadAsyncCallback(IntPtr key, IntPtr packet, uint error) => ReadAsyncCallback(key, packet, error);

        public void WriteAsyncCallback(IntPtr key, IntPtr packet, uint sniError) => WriteAsyncCallback(key, packet, sniError);

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

        internal override void Dispose()
        {
            SafeHandle packetHandle = _sniPacket;
            SafeHandle sessionHandle = _sessionHandle;
            SafeHandle asyncAttnPacket = _sniAsyncAttnPacket;

            _sniPacket = null;
            _sessionHandle = null;
            _sniAsyncAttnPacket = null;

            DisposeCounters();

            if (sessionHandle != null || packetHandle != null)
            {
                packetHandle?.Dispose();
                asyncAttnPacket?.Dispose();

                if (sessionHandle != null)
                {
                    sessionHandle.Dispose();
                    DecrementPendingCallbacks(true); // Will dispose of GC handle.
                }
            }

            DisposePacketCache();
        }

        protected override void FreeGcHandle(int remaining, bool release)
        {
            if ((0 == remaining || release) && _gcHandle.IsAllocated)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParserStateObject.DecrementPendingCallbacks|ADV> {0}, FREEING HANDLE!", ObjectID);
                _gcHandle.Free();
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

        internal override uint CheckConnection()
        {
            SNIHandle handle = Handle;
            return handle == null ? TdsEnums.SNI_SUCCESS : SniNativeWrapper.SniCheckConnection(handle);
        }

        internal override PacketHandle ReadAsync(SessionHandle handle, out uint error)
        {
            Debug.Assert(handle.Type == SessionHandle.NativeHandleType, "unexpected handle type when requiring NativePointer");
            IntPtr readPacketPtr = IntPtr.Zero;
            error = SniNativeWrapper.SniReadAsync(handle.NativeHandle, ref readPacketPtr);
            return PacketHandle.FromNativePointer(readPacketPtr);
        }

        internal override PacketHandle ReadSyncOverAsync(int timeoutRemaining, out uint error)
        {
            SNIHandle handle = Handle ?? throw ADP.ClosedConnectionError();
            IntPtr readPacketPtr = IntPtr.Zero;
            error = SniNativeWrapper.SniReadSyncOverAsync(handle, ref readPacketPtr, GetTimeoutRemaining());
            return PacketHandle.FromNativePointer(readPacketPtr);
        }

        internal override PacketHandle CreateAndSetAttentionPacket()
        {
            SNIPacket attnPacket = new SNIPacket(Handle);
            _sniAsyncAttnPacket = attnPacket;
            SniNativeWrapper.SniPacketSetData(attnPacket, SQL.AttentionHeader, TdsEnums.HEADER_LEN);
            return PacketHandle.FromNativePacket(attnPacket);
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

        internal override void SetPacketData(PacketHandle packet, byte[] buffer, int bytesUsed)
        {
            Debug.Assert(packet.Type == PacketHandle.NativePacketType, "unexpected packet type when requiring NativePacket");
            SniNativeWrapper.SniPacketSetData(packet.NativePacket, buffer, bytesUsed);
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

            Debug.Assert(IntPtr.Zero == temp.NativePointer, "unexpected syncReadPacket without corresponding SNIPacketRelease");
            return error;
        }

        internal override uint EnableSsl(ref uint info, bool tlsFirst, string serverCertificateFilename)
        {
            AuthProviderInfo authInfo = new AuthProviderInfo();
            authInfo.flags = info;
            authInfo.tlsFirst = tlsFirst;
            authInfo.serverCertFileName = serverCertificateFilename;

            // Add SSL (Encryption) SNI provider.
            return SniNativeWrapper.SniAddProvider(Handle, Provider.SSL_PROV, ref authInfo);
        }

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize)
            => SniNativeWrapper.SniSetInfo(Handle, QueryType.SNI_QUERY_CONN_BUFSIZE, ref unsignedPacketSize);

        internal override uint WaitForSSLHandShakeToComplete(out SslProtocols protocolVersion)
        {
            uint returnValue = SniNativeWrapper.SniWaitForSslHandshakeToComplete(Handle, GetTimeoutRemaining(), out uint nativeProtocolVersion);
            var nativeProtocol = (NativeProtocols)nativeProtocolVersion;

#pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
            if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_2_CLIENT) || nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_2_SERVER))
            {
                protocolVersion = SslProtocols.Tls12;
            }
            else if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_3_CLIENT) || nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_3_SERVER))
            {
                /* The SslProtocols.Tls13 is supported by netcoreapp3.1 and later */
                protocolVersion = SslProtocols.Tls13;
            }
            else if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_1_CLIENT) || nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_1_SERVER))
            {
                protocolVersion = SslProtocols.Tls11;
            }
            else if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_0_CLIENT) || nativeProtocol.HasFlag(NativeProtocols.SP_PROT_TLS1_0_SERVER))
            {
                protocolVersion = SslProtocols.Tls;
            }
            else if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_SSL3_CLIENT) || nativeProtocol.HasFlag(NativeProtocols.SP_PROT_SSL3_SERVER))
            {
                // SSL 2.0 and 3.0 are only referenced to log a warning, not explicitly used for connections
#pragma warning disable CS0618, CA5397
                protocolVersion = SslProtocols.Ssl3;
            }
            else if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_SSL2_CLIENT) || nativeProtocol.HasFlag(NativeProtocols.SP_PROT_SSL2_SERVER))
            {
                protocolVersion = SslProtocols.Ssl2;
#pragma warning restore CS0618, CA5397
            }
            else //if (nativeProtocol.HasFlag(NativeProtocols.SP_PROT_NONE))
            {
                protocolVersion = SslProtocols.None;
            }
#pragma warning restore CA5398 // Avoid hardcoded SslProtocols values 
            return returnValue;
        }

        internal override SniErrorDetails GetErrorDetails()
        {
            SniNativeWrapper.SniGetLastError(out SniError sniError);

            return new SniErrorDetails(sniError.errorMessage, sniError.nativeError, sniError.sniError,
                (int)sniError.provider, sniError.lineNumber, sniError.function);
        }

        internal override void DisposePacketCache()
        {
            lock (_writePacketLockObject)
            {
                _writePacketCache.Dispose();
                // Do not set _writePacketCache to null, just in case a WriteAsyncCallback completes after this point
            }
        }

        internal override SspiContextProvider CreateSspiContextProvider() => new NativeSspiContextProvider();

        private sealed class WritePacketCache : IDisposable
        {
            private bool _disposed;
            private Stack<SNIPacket> _packets;

            public WritePacketCache()
            {
                _disposed = false;
                _packets = new Stack<SNIPacket>();
            }

            public SNIPacket Take(SNIHandle sniHandle)
            {
                SNIPacket packet;
                if (_packets.Count > 0)
                {
                    // Success - reset the packet
                    packet = _packets.Pop();
                    SniNativeWrapper.SniPacketReset(sniHandle, IoType.WRITE, packet, ConsumerNumber.SNI_Consumer_SNI);
                }
                else
                {
                    // Failed to take a packet - create a new one
                    packet = new SNIPacket(sniHandle);
                }
                return packet;
            }

            public void Add(SNIPacket packet)
            {
                if (!_disposed)
                {
                    _packets.Push(packet);
                }
                else
                {
                    // If we're disposed, then get rid of any packets added to us
                    packet.Dispose();
                }
            }

            public void Clear()
            {
                while (_packets.Count > 0)
                {
                    _packets.Pop().Dispose();
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    Clear();
                }
            }
        }
    }
}
