// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Interop.Windows.Sni
{
    internal sealed class SniNativeMethodsX64 : ISniNativeMethods
    {
        private const string DllName = "Microsoft.Data.SqlClient.SNI.x64.dll";

        #region Interface Implementation

        public uint SniAddProvider(SNIHandle pConn, Provider provider, ref AuthProviderInfo pInfo) =>
            SNIAddProviderWrapper(pConn, provider, ref pInfo);

        public uint SniAddProvider(SNIHandle pConn, Provider provider, ref uint pInfo) =>
            SNIAddProviderWrapper(pConn, provider, ref pInfo);

        public uint SniCheckConnection(SNIHandle pConn) =>
            SNICheckConnectionWrapper(pConn);

        public uint SniClose(IntPtr pConn) =>
            SNICloseWrapper(pConn);

        public uint SniGetMaxComposedSpnLength() =>
            GetSniMaxComposedSpnLength();

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out bool pbQueryInfo) =>
            SNIGetInfoWrapper(pConn, queryType, out pbQueryInfo);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Guid pbQueryInfo) =>
            SNIGetInfoWrapper(pConn, queryType, out pbQueryInfo);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Provider provider) =>
            SNIGetInfoWrapper(pConn, queryType, out provider);

        public uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out ushort portNumber) =>
            SNIGetInfoWrapper(pConn, queryType, out portNumber);

        public void SniGetLastError(out SniError pLastError) =>
            SNIGetLastError(out pLastError);

        public uint SniGetPeerAddrStrWrapper(
            SNIHandle pConn,
            int bufferSize,
            StringBuilder addrBuffer,
            out uint addrLength) =>
            SNIGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLength);

        public uint SniInitialize(IntPtr pmo) =>
            SNIInitialize(pmo);

        public uint SniIsTokenRestricted(IntPtr token, out bool isRestricted) =>
            UnmanagedIsTokenRestricted(token, out isRestricted);

        public uint SniOpenSyncExWrapper(ref SniClientConsumerInfo pClientConsumerInfo, out IntPtr ppConn) =>
            SNIOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);

        public uint SniOpenWrapper(
            ref SniConsumerInfo pConsumerInfo,
            string connect,
            SNIHandle pConn,
            out IntPtr ppConn,
            bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            ref SniDnsCacheInfo pDnsCacheInfo) =>
            SNIOpenWrapper(ref pConsumerInfo, connect, pConn, out ppConn, fSync, ipPreference, ref pDnsCacheInfo);

        public IntPtr SniPacketAllocateWrapper(SafeHandle pConn, IoType ioType) =>
            SNIPacketAllocateWrapper(pConn, ioType);

        public uint SniPacketGetDataWrapper(
            IntPtr packet,
            byte[] readBuffer,
            uint readBufferLength,
            out uint dataSize) =>
            SNIPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);

        public void SniPacketReset(SNIHandle pConn, IoType ioType, SNIPacket pPacket, ConsumerNumber consumer) =>
            SNIPacketResetWrapper(pConn, ioType, pPacket, consumer);

        public void SniPacketRelease(IntPtr pPacket) =>
            SNIPacketRelease(pPacket);

        public unsafe void SniPacketSetData(SNIPacket pPacket, byte* pbBuffer, uint cbBuffer) =>
            SNIPacketSetData(pPacket, pbBuffer, cbBuffer);

        public uint SniQueryInfo(QueryType queryType, ref IntPtr pbQueryInfo) =>
            SNIQueryInfo(queryType, ref pbQueryInfo);

        public uint SniQueryInfo(QueryType queryType, ref uint pbQueryInfo) =>
            SNIQueryInfo(queryType, ref pbQueryInfo);

        public uint SniReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket) =>
            SNIReadAsyncWrapper(pConn, ref ppNewPacket);

        public uint SniReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout) =>
            SNIReadSyncOverAsync(pConn, ref ppNewPacket, timeout);

        public uint SniRemoveProvider(SNIHandle pConn, Provider provider) =>
            SNIRemoveProviderWrapper(pConn, provider);

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
            SNISecGenClientContextWrapper(
                pConn,
                pIn,
                cbIn,
                pOut,
                ref pcbOut,
                out pfDone,
                szServerInfo,
                cbServerInfo,
                pwszUserName,
                pwszPassword);

        public uint SniSecInitPackage(ref uint pcbMaxToken) =>
            SNISecInitPackage(ref pcbMaxToken);

        public void SniServerEnumClose(IntPtr packet) =>
            SNIServerEnumCloseWrapper(packet);

        public IntPtr SniServerEnumOpen() =>
            SNIServerEnumOpenWrapper();

        public int SniServerEnumRead(IntPtr packet, char[] readBuffer, int bufferLength, out bool more) =>
            SNIServerEnumReadWrapper(packet, readBuffer, bufferLength, out more);

        public uint SniSetInfo(SNIHandle pConn, QueryType queryType, ref uint pbQueryInfo) =>
            SNISetInfoWrapper(pConn, queryType, ref pbQueryInfo);

        public uint SniTerminate() =>
            SNITerminate();

        public uint SniWaitForSslHandshakeToComplete(SNIHandle pConn, int dwMilliseconds, out uint pProtocolVersion) =>
            SNIWaitForSSLHandshakeToCompleteWrapper(pConn, dwMilliseconds, out pProtocolVersion);

        public uint SniWriteAsyncWrapper(SNIHandle pConn, SNIPacket pPacket) =>
            SNIWriteAsyncWrapper(pConn, pPacket);

        public uint SniWriteSyncOverAsync(SNIHandle pConn, SNIPacket pPacket) =>
            SNIWriteSyncOverAsync(pConn, pPacket);

        #endregion

        #region DllImports

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetSniMaxComposedSpnLength();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIAddProviderWrapper(
            SNIHandle pConn,
            Provider provider,
            [In] ref AuthProviderInfo pInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIAddProviderWrapper(SNIHandle pConn, Provider provider, [In] ref uint pInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNICheckConnectionWrapper([In] SNIHandle pConn);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNICloseWrapper(IntPtr pConn);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper(
            [In] SNIHandle pConn,
            QueryType queryType,
            [MarshalAs(UnmanagedType.Bool)] out bool pbQInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType queryType, out Guid pbQueryInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType queryType, out Provider provider);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType queryType, out ushort portNum);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SNIGetLastError(out SniError pErrorStruct);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern uint SNIGetPeerAddrStrWrapper(
            [In] SNIHandle pConn,
            int bufferSize,
            StringBuilder addrBuffer,
            out uint addrLength);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIInitialize([In] IntPtr pmo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIOpenSyncExWrapper(
            ref SniClientConsumerInfo pClientConsumerInfo,
            out IntPtr ppConn);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIOpenWrapper(
            [In] ref SniConsumerInfo pConsumerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string szConnect,
            [In] SNIHandle pConn,
            out IntPtr ppConn,
            [MarshalAs(UnmanagedType.Bool)] bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            [In] ref SniDnsCacheInfo pDnsCacheInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SNIPacketAllocateWrapper([In] SafeHandle pConn, IoType ioType);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIPacketGetDataWrapper(
            [In] IntPtr packet,
            [In, Out] byte[] readBuffer,
            uint readBufferLength,
            out uint dataSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SNIPacketRelease(IntPtr pPacket);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SNIPacketResetWrapper(
            [In] SNIHandle pConn,
            IoType ioType,
            SNIPacket pPacket,
            ConsumerNumber consumer);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void SNIPacketSetData(SNIPacket pPacket, [In] byte* pbBuf, uint cbBuf);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIQueryInfo(QueryType queryType, ref IntPtr pbQueryInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIQueryInfo(QueryType queryType, ref uint pbQueryInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIReadAsyncWrapper(SNIHandle pConn, ref IntPtr ppNewPacket);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIRemoveProviderWrapper(SNIHandle pConn, Provider provider);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe uint SNISecGenClientContextWrapper(
            [In] SNIHandle pConn,
            [In, Out] byte* pIn,
            uint cbIn,
            [In, Out] byte* pOut,
            [In] ref uint pcbOut,
            [MarshalAs(UnmanagedType.Bool)] out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string pwszUserName,
            [MarshalAs(UnmanagedType.LPWStr)] string pwszPassword);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNISecInitPackage(ref uint pcbMaxToken);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SNIServerEnumCloseWrapper([In] IntPtr packet);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SNIServerEnumOpenWrapper();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int SNIServerEnumReadWrapper(
            [In] IntPtr packet,
            [In, Out][MarshalAs(UnmanagedType.LPArray)] char[] readBuffer,
            [In] int bufferLength,
            [MarshalAs(UnmanagedType.Bool)] out bool more);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNISetInfoWrapper(SNIHandle pConn, QueryType queryType, [In] ref uint pbQueryInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNITerminate();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIWaitForSSLHandshakeToCompleteWrapper(
            [In] SNIHandle pConn,
            int dwMilliseconds,
            out uint pProtocolVersion);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIWriteAsyncWrapper(SNIHandle pConn, [In] SNIPacket pPacket);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIWriteSyncOverAsync(SNIHandle pConn, [In] SNIPacket pPacket);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint UnmanagedIsTokenRestricted(
            [In] IntPtr token,
            [MarshalAs(UnmanagedType.Bool)] out bool isRestricted);

        #endregion
    }
}

#endif
