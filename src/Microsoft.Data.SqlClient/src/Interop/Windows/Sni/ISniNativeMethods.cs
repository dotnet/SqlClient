// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Interop.Windows.Sni
{
    internal interface ISniNativeMethods
    {
        uint SniAddProvider(SNIHandle pConn, Provider provider, ref AuthProviderInfo pInfo);

        uint SniAddProvider(SNIHandle pConn, Provider provider, ref uint pInfo);

        uint SniCheckConnection(SNIHandle pConn);

        uint SniClose(IntPtr pConn);

        uint SniGetMaxComposedSpnLength();

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out bool pbQueryInfo);

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Guid pbQueryInfo);

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Provider provider);

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out ushort portNumber);

        void SniGetLastError(out SniError pLastError);

        uint SniGetPeerAddrStrWrapper(SNIHandle pConn, int bufferSize, StringBuilder addrBuffer, out uint addrLength);

        uint SniInitialize(IntPtr pmo);

        uint SniIsTokenRestricted(IntPtr token, out bool isRestricted);

        uint SniOpenSyncExWrapper(ref SniClientConsumerInfo pClientConsumerInfo, out IntPtr ppConn);

        uint SniOpenWrapper(
            ref SniConsumerInfo pConsumerInfo,
            string connect,
            SNIHandle pConn,
            out IntPtr ppConn,
            bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            ref SniDnsCacheInfo pDnsCacheInfo);

        IntPtr SniPacketAllocateWrapper(SafeHandle pConn, IoType ioType);

        uint SniPacketGetDataWrapper(IntPtr packet, byte[] readBuffer, uint readBufferLength, out uint dataSize);

        void SniPacketReset(SNIHandle pConn, IoType ioType, SNIPacket pPacket, ConsumerNumber consumer);

        void SniPacketRelease(IntPtr pPacket);

        unsafe void SniPacketSetData(SNIPacket pPacket, byte* pbBuffer, uint cbBuffer);

        uint SniQueryInfo(QueryType queryType, ref IntPtr pbQueryInfo);

        uint SniQueryInfo(QueryType queryType, ref uint pbQueryInfo);

        uint SniReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket);

        uint SniReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout);

        uint SniRemoveProvider(SNIHandle pConn, Provider provider);

        unsafe uint SniSecGenClientContextWrapper(
            SNIHandle pConn,
            byte* pIn,
            uint cbIn,
            byte* pOut,
            ref uint pcbOut,
            out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            string pwszUserName,
            string pwszPassword);

        uint SniSecInitPackage(ref uint pcbMaxToken);

        void SniServerEnumClose(IntPtr packet);

        IntPtr SniServerEnumOpen();

        int SniServerEnumRead(IntPtr packet, char[] readBuffer, int bufferLength, out bool more);

        uint SniSetInfo(SNIHandle pConn, QueryType queryType, ref uint pbQueryInfo);

        uint SniTerminate();

        uint SniWaitForSslHandshakeToComplete(SNIHandle pConn, int dwMilliseconds, out SniSslProtocols pProtocolVersion);

        uint SniWriteAsyncWrapper(SNIHandle pConn, SNIPacket pPacket);

        uint SniWriteSyncOverAsync(SNIHandle pConn, SNIPacket pPacket);
    }
}

#endif
