// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Interop.Windows.Sni;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient
{
    internal static partial class SNINativeMethodWrapper
    {
        private const string SNI = "Microsoft.Data.SqlClient.SNI.dll";
        
        private static readonly ISniNativeMethods NativeMethods = new SniNativeMethods();
        
        private static int s_sniMaxComposedSpnLength = -1;

        private const int SniOpenTimeOut = -1; // infinite

        internal const int SniIP6AddrStringBufferLength = 48; // from SNI layer

        internal static int SniMaxComposedSpnLength
        {
            get
            {
                if (s_sniMaxComposedSpnLength == -1)
                {
                    s_sniMaxComposedSpnLength = checked((int)GetSniMaxComposedSpnLength());
                }
                return s_sniMaxComposedSpnLength;
            }
        }

        #region DLL Imports
        
        internal static uint SNIAddProvider(SNIHandle pConn, Provider ProvNum, [In] ref uint pInfo) =>
            NativeMethods.SniAddProvider(pConn, ProvNum, ref pInfo);
        
        internal static uint SNIAddProvider(SNIHandle pConn, Provider ProvNum, [In] ref AuthProviderInfo pInfo) =>
            NativeMethods.SniAddProvider(pConn, ProvNum, ref pInfo);
        
        internal static uint SNICheckConnection([In] SNIHandle pConn) =>
            NativeMethods.SniCheckConnection(pConn);
        
        internal static uint SNIClose(IntPtr pConn) =>
            NativeMethods.SniClose(pConn);
        
        internal static void SNIGetLastError(out SniError pErrorStruct) =>
            NativeMethods.SniGetLastError(out pErrorStruct);
        
        internal static void SNIPacketRelease(IntPtr pPacket) =>
            NativeMethods.SniPacketRelease(pPacket);
        
        internal static void SNIPacketReset([In] SNIHandle pConn, IoType IOType, SNIPacket pPacket, ConsumerNumber ConsNum) =>
            NativeMethods.SniPacketReset(pConn, IOType, pPacket, ConsNum);
        
        internal static uint SNIQueryInfo(QueryType QType, ref uint pbQInfo) =>
            NativeMethods.SniQueryInfo(QType, ref pbQInfo);
        
        internal static uint SNIQueryInfo(QueryType QType, ref IntPtr pbQInfo) =>
            NativeMethods.SniQueryInfo(QType, ref pbQInfo);
        
        internal static uint SNIReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket) =>
            NativeMethods.SniReadAsync(pConn, ref ppNewPacket);
        
        internal static uint SNIReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout) =>
            NativeMethods.SniReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
        
        internal static uint SNIRemoveProvider(SNIHandle pConn, Provider ProvNum) =>
            NativeMethods.SniRemoveProvider(pConn, ProvNum);
        
        internal static uint SNISecInitPackage(ref uint pcbMaxToken) =>
            NativeMethods.SniSecInitPackage(ref pcbMaxToken);
        
        internal static uint SNISetInfo(SNIHandle pConn, QueryType QType, [In] ref uint pbQInfo) =>
            NativeMethods.SniSetInfo(pConn, QType, ref pbQInfo);
        
        internal static uint SNITerminate() =>
            NativeMethods.SniTerminate();
        
        internal static uint SNIWaitForSSLHandshakeToComplete([In] SNIHandle pConn, int dwMilliseconds, out uint pProtocolVersion) =>
            NativeMethods.SniWaitForSslHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
        
        internal static uint UnmanagedIsTokenRestricted([In] IntPtr token, [MarshalAs(UnmanagedType.Bool)] out bool isRestricted) =>
            NativeMethods.SniIsTokenRestricted(token, out isRestricted);
        
        private static uint GetSniMaxComposedSpnLength() =>
            NativeMethods.SniGetMaxComposedSpnLength();
        
        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, out Guid pbQInfo) =>
            NativeMethods.SniGetInfoWrapper(pConn, QType, out pbQInfo);
        
        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, out ushort portNum) =>
            NativeMethods.SniGetInfoWrapper(pConn, QType, out portNum);
        
        private static uint SNIGetPeerAddrStrWrapper([In] SNIHandle pConn, int bufferSize, StringBuilder addrBuffer, out uint addrLen) =>
            NativeMethods.SniGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLen);
        
        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, out Provider provNum) =>
            NativeMethods.SniGetInfoWrapper(pConn, QType, out provNum);
        
        private static uint SNIInitialize([In] IntPtr pmo) =>
            NativeMethods.SniInitialize(pmo);
        
        private static uint SNIOpenSyncExWrapper(ref SniClientConsumerInfo pClientConsumerInfo, out IntPtr ppConn) =>
            NativeMethods.SniOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);

        private static uint SNIOpenWrapper(
            [In] ref SniConsumerInfo pConsumerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string szConnect,
            [In] SNIHandle pConn,
            out IntPtr ppConn,
            [MarshalAs(UnmanagedType.Bool)] bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            [In] ref SniDnsCacheInfo pDNSCachedInfo) =>
            NativeMethods.SniOpenWrapper(
                ref pConsumerInfo,
                szConnect,
                pConn,
                out ppConn,
                fSync,
                ipPreference,
                ref pDNSCachedInfo);
        
        private static IntPtr SNIPacketAllocateWrapper([In] SafeHandle pConn, IoType IOType) =>
            NativeMethods.SniPacketAllocateWrapper(pConn, IOType);
        
        private static uint SNIPacketGetDataWrapper([In] IntPtr packet, [In, Out] byte[] readBuffer, uint readBufferLength, out uint dataSize) =>
            NativeMethods.SniPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);
        
        private static unsafe void SNIPacketSetData(SNIPacket pPacket, [In] byte* pbBuf, uint cbBuf) =>
            NativeMethods.SniPacketSetData(pPacket, pbBuf, cbBuf);

        private static unsafe uint SNISecGenClientContextWrapper(
            [In] SNIHandle pConn,
            [In, Out] byte* pIn,
            uint cbIn,
            [In, Out] byte[] pOut,
            [In] ref uint pcbOut,
            [MarshalAsAttribute(UnmanagedType.Bool)] out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszUserName,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszPassword) =>
            NativeMethods.SniSecGenClientContextWrapper(pConn, pIn, cbIn, pOut, ref pcbOut, out pfDone, szServerInfo,
                cbServerInfo, pwszUserName, pwszPassword);
        
        private static uint SNIWriteAsyncWrapper(SNIHandle pConn, [In] SNIPacket pPacket) =>
            NativeMethods.SniWriteAsyncWrapper(pConn, pPacket);
        
        private static uint SNIWriteSyncOverAsync(SNIHandle pConn, [In] SNIPacket pPacket) =>
            NativeMethods.SniWriteSyncOverAsync(pConn, pPacket);
        
        internal static IntPtr SNIServerEnumOpen() =>
            NativeMethods.SniServerEnumOpen();
        
        internal static void SNIServerEnumClose([In] IntPtr packet) =>
            NativeMethods.SniServerEnumClose(packet);

        internal static int SNIServerEnumRead([In] IntPtr packet,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            char[] readBuffer,
            [In] int bufferLength,
            [MarshalAs(UnmanagedType.Bool)] out bool more) =>
            NativeMethods.SniServerEnumRead(packet, readBuffer, bufferLength, out more);
        
        #endregion

        internal static uint SniGetConnectionId(SNIHandle pConn, ref Guid connId)
        {
            return SNIGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_CONNID, out connId);
        }

        internal static uint SniGetProviderNumber(SNIHandle pConn, ref Provider provNum)
        {
            return SNIGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_PROVIDERNUM, out provNum);
        }

        internal static uint SniGetConnectionPort(SNIHandle pConn, ref ushort portNum)
        {
            return SNIGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_PEERPORT, out portNum);
        }

        internal static uint SniGetConnectionIPString(SNIHandle pConn, ref string connIPStr)
        {
            UInt32 ret;
            uint connIPLen = 0;

            int bufferSize = SniIP6AddrStringBufferLength;
            StringBuilder addrBuffer = new StringBuilder(bufferSize);

            ret = SNIGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out connIPLen);

            connIPStr = addrBuffer.ToString(0, Convert.ToInt32(connIPLen));

            return ret;
        }

        internal static uint SNIInitialize()
        {
            return SNIInitialize(IntPtr.Zero);
        }

        internal static unsafe uint SNIOpenMarsSession(ConsumerInfo consumerInfo, SNIHandle parent, ref IntPtr pConn, bool fSync, SqlConnectionIPAddressPreference ipPreference, SQLDNSInfo cachedDNSInfo)
        {
            // initialize consumer info for MARS
            SniConsumerInfo native_consumerInfo = new SniConsumerInfo();
            MarshalConsumerInfo(consumerInfo, ref native_consumerInfo);

            SniDnsCacheInfo native_cachedDNSInfo = new SniDnsCacheInfo();
            native_cachedDNSInfo.wszCachedFQDN = cachedDNSInfo?.FQDN;
            native_cachedDNSInfo.wszCachedTcpIPv4 = cachedDNSInfo?.AddrIPv4;
            native_cachedDNSInfo.wszCachedTcpIPv6 = cachedDNSInfo?.AddrIPv6;
            native_cachedDNSInfo.wszCachedTcpPort = cachedDNSInfo?.Port;

            return SNIOpenWrapper(ref native_consumerInfo, "session:", parent, out pConn, fSync, ipPreference, ref native_cachedDNSInfo);
        }

        internal static unsafe uint SNIOpenSyncEx(
            ConsumerInfo consumerInfo,
            string constring,
            ref IntPtr pConn,
            byte[] spnBuffer,
            byte[] instanceName,
            bool fOverrideCache,
            bool fSync,
            int timeout,
            bool fParallel,
            SqlConnectionIPAddressPreference ipPreference,
            SQLDNSInfo cachedDNSInfo,
            string hostNameInCertificate)
        {

            fixed (byte* pin_instanceName = &instanceName[0])
            {
                SniClientConsumerInfo clientConsumerInfo = new SniClientConsumerInfo();

                // initialize client ConsumerInfo part first
                MarshalConsumerInfo(consumerInfo, ref clientConsumerInfo.ConsumerInfo);

                clientConsumerInfo.wszConnectionString = constring;
                clientConsumerInfo.HostNameInCertificate = hostNameInCertificate;
                clientConsumerInfo.networkLibrary = Prefix.UNKNOWN_PREFIX;
                clientConsumerInfo.szInstanceName = pin_instanceName;
                clientConsumerInfo.cchInstanceName = (uint)instanceName.Length;
                clientConsumerInfo.fOverrideLastConnectCache = fOverrideCache;
                clientConsumerInfo.fSynchronousConnection = fSync;
                clientConsumerInfo.timeout = timeout;
                clientConsumerInfo.fParallel = fParallel;

                clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.DisabledMode;
                clientConsumerInfo.totalTimeout = SniOpenTimeOut;
                clientConsumerInfo.isAzureSqlServerEndpoint = ADP.IsAzureSqlServerEndpoint(constring);

                clientConsumerInfo.ipAddressPreference = ipPreference;
                clientConsumerInfo.DNSCacheInfo.wszCachedFQDN = cachedDNSInfo?.FQDN;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv4 = cachedDNSInfo?.AddrIPv4;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv6 = cachedDNSInfo?.AddrIPv6;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpPort = cachedDNSInfo?.Port;

                if (spnBuffer != null)
                {
                    fixed (byte* pin_spnBuffer = &spnBuffer[0])
                    {
                        clientConsumerInfo.szSPN = pin_spnBuffer;
                        clientConsumerInfo.cchSPN = (uint)spnBuffer.Length;
                        return SNIOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                    }
                }
                else
                {
                    // else leave szSPN null (SQL Auth)
                    return SNIOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                }
            }
        }

        internal static void SNIPacketAllocate(SafeHandle pConn, IoType IOType, ref IntPtr pPacket)
        {
            pPacket = SNIPacketAllocateWrapper(pConn, IOType);
        }

        internal static unsafe uint SNIPacketGetData(IntPtr packet, byte[] readBuffer, ref uint dataSize)
        {
            return SNIPacketGetDataWrapper(packet, readBuffer, (uint)readBuffer.Length, out dataSize);
        }

        internal static unsafe void SNIPacketSetData(SNIPacket packet, byte[] data, int length)
        {
            fixed (byte* pin_data = &data[0])
            {
                SNIPacketSetData(packet, pin_data, (uint)length);
            }
        }

        internal static unsafe uint SNISecGenClientContext(SNIHandle pConnectionObject, ReadOnlySpan<byte> inBuff, byte[] OutBuff, ref uint sendLength, byte[] serverUserName)
        {
            fixed (byte* pin_serverUserName = &serverUserName[0])
            fixed (byte* pInBuff = inBuff)
            {
                return SNISecGenClientContextWrapper(
                    pConnectionObject,
                    pInBuff,
                    (uint)inBuff.Length,
                    OutBuff,
                    ref sendLength,
                    out _,
                    pin_serverUserName,
                    (uint)serverUserName.Length,
                    null,
                    null);
            }
        }

        internal static uint SNIWritePacket(SNIHandle pConn, SNIPacket packet, bool sync)
        {
            if (sync)
            {
                return SNIWriteSyncOverAsync(pConn, packet);
            }
            else
            {
                return SNIWriteAsyncWrapper(pConn, packet);
            }
        }

        private static void MarshalConsumerInfo(ConsumerInfo consumerInfo, ref SniConsumerInfo native_consumerInfo)
        {
            native_consumerInfo.DefaultUserDataLength = consumerInfo.defaultBufferSize;
            native_consumerInfo.fnReadComp = consumerInfo.readDelegate != null
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.readDelegate)
                : IntPtr.Zero;
            native_consumerInfo.fnWriteComp = consumerInfo.writeDelegate != null
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.writeDelegate)
                : IntPtr.Zero;
            native_consumerInfo.ConsumerKey = consumerInfo.key;
        }
    }
}

namespace Microsoft.Data
{
    internal static partial class SafeNativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr HModule, [MarshalAs(UnmanagedType.LPStr), In] string funcName);
    }
}

namespace Microsoft.Data
{
    internal static class Win32NativeMethods
    {
        internal static bool IsTokenRestrictedWrapper(IntPtr token)
        {
            bool isRestricted;
            uint result = SNINativeMethodWrapper.UnmanagedIsTokenRestricted(token, out isRestricted);

            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(unchecked((int)result));
            }

            return isRestricted;
        }
    }
}
