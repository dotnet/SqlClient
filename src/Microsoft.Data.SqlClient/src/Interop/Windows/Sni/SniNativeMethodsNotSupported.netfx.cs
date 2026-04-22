// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK && _WINDOWS

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Interop.Windows.Sni
{
    internal sealed class SniNativeMethodsNotSupported : ISniNativeMethods
    {
        private readonly string _architecture;
        
        public SniNativeMethodsNotSupported(Architecture architecture)
        {
            _architecture = architecture.ToString();
        }
        
        public uint SniAddProvider(SNIHandle pConn, Provider provider, ref AuthProviderInfo pInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniAddProvider(SNIHandle pConn, Provider provider, ref uint pInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniCheckConnection(SNIHandle pConn) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniClose(IntPtr pConn) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniGetMaxComposedSpnLength() =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out bool pbQueryInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Guid pbQueryInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Provider provider) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out ushort portNumber) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public void SniGetLastError(out SniError pLastError) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniGetPeerAddrStrWrapper(
            SNIHandle pConn,
            int bufferSize,
            StringBuilder addrBuffer,
            out uint addrLength) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniInitialize(IntPtr pmo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniIsTokenRestricted(IntPtr token, out bool isRestricted) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniOpenSyncExWrapper(ref SniClientConsumerInfo pClientConsumerInfo, out IntPtr ppConn) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniOpenWrapper(
            ref SniConsumerInfo pConsumerInfo,
            string connect,
            SNIHandle pConn,
            out IntPtr ppConn,
            bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            ref SniDnsCacheInfo pDnsCacheInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public IntPtr SniPacketAllocateWrapper(SafeHandle pConn, IoType ioType) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniPacketGetDataWrapper(
            IntPtr packet,
            byte[] readBuffer,
            uint readBufferLength,
            out uint dataSize) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public void SniPacketReset(SNIHandle pConn, IoType ioType, SNIPacket pPacket, ConsumerNumber consumer) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public void SniPacketRelease(IntPtr pPacket) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public unsafe void SniPacketSetData(SNIPacket pPacket, byte* pbBuffer, uint cbBuffer) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniQueryInfo(QueryType queryType, ref IntPtr pbQueryInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniQueryInfo(QueryType queryType, ref uint pbQueryInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniRemoveProvider(SNIHandle pConn, Provider provider) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public unsafe uint SniSecGenClientContextWrapper(
            SNIHandle pConn,
            byte* pIn,
            uint cbIn,
            byte* pOut,
            ref uint pcbOut,
            out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            string pwszUserName,
            string pwszPassword) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniSecInitPackage(ref uint pcbMaxToken) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public void SniServerEnumClose(IntPtr packet) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public IntPtr SniServerEnumOpen() =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public int SniServerEnumRead(IntPtr packet, char[] readBuffer, int bufferLength, out bool more) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniSetInfo(SNIHandle pConn, QueryType queryType, ref uint pbQueryInfo) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniTerminate() =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniWaitForSslHandshakeToComplete(SNIHandle pConn, int dwMilliseconds, out SniSslProtocols pProtocolVersion) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniWriteAsyncWrapper(SNIHandle pConn, SNIPacket pPacket) =>
            throw ADP.SNIPlatformNotSupported(_architecture);

        public uint SniWriteSyncOverAsync(SNIHandle pConn, SNIPacket pPacket) =>
            throw ADP.SNIPlatformNotSupported(_architecture);
    }
}

#endif
