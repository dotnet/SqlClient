// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using static Microsoft.Data.SqlClient.SNINativeMethodWrapper;

namespace Microsoft.Data.SqlClient
{
    internal static class SNINativeManagedWrapperX64
    {
        private const string SNI = "Microsoft.Data.SqlClient.SNI.x64.dll";

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIAddProviderWrapper")]
        internal static extern uint SNIAddProvider(SNIHandle pConn, ProviderEnum ProvNum, [In] ref uint pInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIAddProviderWrapper")]
        internal static extern uint SNIAddProviderWrapper(SNIHandle pConn, ProviderEnum ProvNum, [In] ref SNICTAIPProviderInfo pInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIAddProviderWrapper")]
        internal static extern uint SNIAddProviderWrapper(SNIHandle pConn, ProviderEnum ProvNum, [In] ref AuthProviderInfo pInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNICheckConnectionWrapper")]
        internal static extern uint SNICheckConnection([In] SNIHandle pConn);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNICloseWrapper")]
        internal static extern uint SNIClose(IntPtr pConn);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SNIGetLastError(out SNI_Error pErrorStruct);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SNIPacketRelease(IntPtr pPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIPacketResetWrapper")]
        internal static extern void SNIPacketReset([In] SNIHandle pConn, IOType IOType, SNIPacket pPacket, ConsumerNumber ConsNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIQueryInfo(QTypes QType, ref uint pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIQueryInfo(QTypes QType, ref IntPtr pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIReadAsyncWrapper")]
        internal static extern uint SNIReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIRemoveProviderWrapper")]
        internal static extern uint SNIRemoveProvider(SNIHandle pConn, ProviderEnum ProvNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNISecInitPackage(ref uint pcbMaxToken);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNISetInfoWrapper")]
        internal static extern uint SNISetInfo(SNIHandle pConn, QTypes QType, [In] ref uint pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNITerminate();

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIWaitForSSLHandshakeToCompleteWrapper")]
        internal static extern uint SNIWaitForSSLHandshakeToComplete([In] SNIHandle pConn, int dwMilliseconds, out uint pProtocolVersion);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UnmanagedIsTokenRestricted([In] IntPtr token, [MarshalAs(UnmanagedType.Bool)] out bool isRestricted);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetSniMaxComposedSpnLength();

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, out Guid pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, [MarshalAs(UnmanagedType.Bool)] out bool pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, ref IntPtr pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, out ushort portNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern uint SNIGetPeerAddrStrWrapper([In] SNIHandle pConn, int bufferSize, StringBuilder addrBuffer, out uint addrLen);        

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, out ProviderEnum provNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIInitialize")]
        internal static extern uint SNIInitialize([In] IntPtr pmo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIOpenSyncExWrapper(ref SNI_CLIENT_CONSUMER_INFO pClientConsumerInfo, out IntPtr ppConn);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIOpenWrapper(
            [In] ref Sni_Consumer_Info pConsumerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string szConnect,
            [In] SNIHandle pConn,
            out IntPtr ppConn,
            [MarshalAs(UnmanagedType.Bool)] bool fSync,
            [In] ref SNI_DNSCache_Info pDNSCachedInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SNIPacketAllocateWrapper([In] SafeHandle pConn, IOType IOType);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIPacketGetDataWrapper([In] IntPtr packet, [In, Out] byte[] readBuffer, uint readBufferLength, out uint dataSize);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe void SNIPacketSetData(SNIPacket pPacket, [In] byte* pbBuf, uint cbBuf);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNISecGenClientContextWrapper")]
        internal static extern unsafe uint SNISecGenClientContextWrapper(
            [In] SNIHandle pConn,
            [In, Out] byte[] pIn,
            uint cbIn,
            [In, Out] byte[] pOut,
            [In] ref uint pcbOut,
            [MarshalAsAttribute(UnmanagedType.Bool)] out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszUserName,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszPassword);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIWriteAsyncWrapper(SNIHandle pConn, [In] SNIPacket pPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIWriteSyncOverAsync(SNIHandle pConn, [In] SNIPacket pPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SNIClientCertificateFallbackWrapper(IntPtr pCallbackContext);
        
        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool RegisterTraceProviderWrapper(int eventKeyword);
        
        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void UnregisterTraceProviderWrapper();
    }
}
