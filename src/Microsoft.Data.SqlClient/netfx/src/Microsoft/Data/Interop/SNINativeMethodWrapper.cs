// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Interop.Windows.Sni;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient
{
    internal static class SNINativeMethodWrapper
    {
        private static ISniNativeMethods NativeMethodsX64 = new SniNativeMethodsX64();
        private static ISniNativeMethods NativeMethodsX86 = new SniNativeMethodsX86();

        private static int s_sniMaxComposedSpnLength = -1;
        private static readonly System.Runtime.InteropServices.Architecture s_architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

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

        static AppDomain GetDefaultAppDomainInternal()
        {
            return AppDomain.CurrentDomain;
        }

        internal static _AppDomain GetDefaultAppDomain()
        {
            return GetDefaultAppDomainInternal();
        }

        [ResourceExposure(ResourceScope.Process)] // SxS: there is no way to set scope = Instance, using Process which is wider
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal unsafe static byte[] GetData()
        {
            int size;
            IntPtr ptr = (IntPtr)(SqlDependencyProcessDispatcherStorage.NativeGetData(out size));
            byte[] result = null;

            if (ptr != IntPtr.Zero)
            {
                result = new byte[size];
                Marshal.Copy(ptr, result, 0, size);
            }

            return result;
        }

        [ResourceExposure(ResourceScope.Process)] // SxS: there is no way to set scope = Instance, using Process which is wider
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal unsafe static void SetData(Byte[] data)
        {
            //cli::pin_ptr<System::Byte> pin_dispatcher = &data[0];
            fixed (byte* pin_dispatcher = &data[0])
            {
                SqlDependencyProcessDispatcherStorage.NativeSetData(pin_dispatcher, data.Length);
            }
        }

        #region DLL Imports
        internal static uint SNIAddProvider(SNIHandle pConn, Provider ProvNum, [In] ref uint pInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIAddProvider(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniAddProvider(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniAddProvider(pConn, ProvNum, ref pInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIAddProviderWrapper(SNIHandle pConn, Provider ProvNum, [In] ref AuthProviderInfo pInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniAddProvider(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniAddProvider(pConn, ProvNum, ref pInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNICheckConnection([In] SNIHandle pConn)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNICheckConnection(pConn);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniCheckConnection(pConn);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniCheckConnection(pConn);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIClose(IntPtr pConn)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIClose(pConn);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniClose(pConn);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniClose(pConn);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static void SNIGetLastError(out SniError pErrorStruct)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIGetLastError(out pErrorStruct);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    NativeMethodsX64.SniGetLastError(out pErrorStruct);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    NativeMethodsX86.SniGetLastError(out pErrorStruct);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static void SNIPacketRelease(IntPtr pPacket)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIPacketRelease(pPacket);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    NativeMethodsX64.SniPacketRelease(pPacket);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    NativeMethodsX86.SniPacketRelease(pPacket);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static void SNIPacketReset([In] SNIHandle pConn, IoType IOType, SNIPacket pPacket, ConsumerNumber ConsNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIPacketReset(pConn, IOType, pPacket, ConsNum);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    NativeMethodsX64.SniPacketReset(pConn, IOType, pPacket, ConsNum);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    NativeMethodsX86.SniPacketReset(pConn, IOType, pPacket, ConsNum);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIQueryInfo(QueryType QType, ref uint pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniQueryInfo(QType, ref pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIQueryInfo(QueryType QType, ref IntPtr pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniQueryInfo(QType, ref pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIReadAsync(pConn, ref ppNewPacket);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniReadAsync(pConn, ref ppNewPacket);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniReadAsync(pConn, ref ppNewPacket);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIRemoveProvider(SNIHandle pConn, Provider ProvNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIRemoveProvider(pConn, ProvNum);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniRemoveProvider(pConn, ProvNum);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniRemoveProvider(pConn, ProvNum);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNISecInitPackage(ref uint pcbMaxToken)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNISecInitPackage(ref pcbMaxToken);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniSecInitPackage(ref pcbMaxToken);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniSecInitPackage(ref pcbMaxToken);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNISetInfo(SNIHandle pConn, QueryType QType, [In] ref uint pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNISetInfo(pConn, QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniSetInfo(pConn, QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniSetInfo(pConn, QType, ref pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNITerminate()
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNITerminate();
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniTerminate();
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniTerminate();
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIWaitForSSLHandshakeToComplete([In] SNIHandle pConn, int dwMilliseconds, out uint pProtocolVersion)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIWaitForSSLHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniWaitForSslHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniWaitForSslHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint UnmanagedIsTokenRestricted([In] IntPtr token, [MarshalAs(UnmanagedType.Bool)] out bool isRestricted)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.UnmanagedIsTokenRestricted(token, out isRestricted);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniIsTokenRestricted(token, out isRestricted);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniIsTokenRestricted(token, out isRestricted);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint GetSniMaxComposedSpnLength()
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.GetSniMaxComposedSpnLength();
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniGetMaxComposedSpnLength();
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniGetMaxComposedSpnLength();
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, out Guid pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniGetInfoWrapper(pConn, QType, out pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, [MarshalAs(UnmanagedType.Bool)] out bool pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniGetInfoWrapper(pConn, QType, out pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, out ushort portNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out portNum);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniGetInfoWrapper(pConn, QType, out portNum);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniGetInfoWrapper(pConn, QType, out portNum);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetPeerAddrStrWrapper([In] SNIHandle pConn, int bufferSize, StringBuilder addrBuffer, out uint addrLen)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLen);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLen);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLen);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, QueryType QType, out Provider provNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out provNum);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniGetInfoWrapper(pConn, QType, out provNum);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniGetInfoWrapper(pConn, QType, out provNum);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIInitialize([In] IntPtr pmo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIInitialize(pmo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniInitialize(pmo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniInitialize(pmo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIOpenSyncExWrapper(ref SniClientConsumerInfo pClientConsumerInfo, out IntPtr ppConn)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIOpenWrapper(
            [In] ref SniConsumerInfo pConsumerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string szConnect,
            [In] SNIHandle pConn,
            out IntPtr ppConn,
            [MarshalAs(UnmanagedType.Bool)] bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            [In] ref SniDnsCacheInfo pDNSCachedInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIOpenWrapper(ref pConsumerInfo, szConnect, pConn, out ppConn, fSync, ipPreference, ref pDNSCachedInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniOpenWrapper(ref pConsumerInfo, szConnect, pConn, out ppConn, fSync, ipPreference, ref pDNSCachedInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniOpenWrapper(ref pConsumerInfo, szConnect, pConn, out ppConn, fSync, ipPreference, ref pDNSCachedInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static IntPtr SNIPacketAllocateWrapper([In] SafeHandle pConn, IoType IOType)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIPacketAllocateWrapper(pConn, IOType);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniPacketAllocateWrapper(pConn, IOType);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniPacketAllocateWrapper(pConn, IOType);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIPacketGetDataWrapper([In] IntPtr packet, [In, Out] byte[] readBuffer, uint readBufferLength, out uint dataSize)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static unsafe void SNIPacketSetData(SNIPacket pPacket, [In] byte* pbBuf, uint cbBuf)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIPacketSetData(pPacket, pbBuf, cbBuf);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    NativeMethodsX64.SniPacketSetData(pPacket, pbBuf, cbBuf);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    NativeMethodsX86.SniPacketSetData(pPacket, pbBuf, cbBuf);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static unsafe uint SNISecGenClientContextWrapper(
            [In] SNIHandle pConn,
            [In, Out] ReadOnlySpan<byte> pIn,
            [In, Out] byte[] pOut,
            [In] ref uint pcbOut,
            [MarshalAsAttribute(UnmanagedType.Bool)] out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszUserName,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszPassword)
        {
            fixed (byte* pInPtr = pIn)
            {
                switch (s_architecture)
                {
                    case System.Runtime.InteropServices.Architecture.Arm64:
                        return SNINativeManagedWrapperARM64.SNISecGenClientContextWrapper(pConn, pInPtr, (uint)pIn.Length, pOut, ref pcbOut, out pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
                    case System.Runtime.InteropServices.Architecture.X64:
                        return NativeMethodsX64.SniSecGenClientContextWrapper(pConn, pInPtr, (uint)pIn.Length, pOut, ref pcbOut, out pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
                    case System.Runtime.InteropServices.Architecture.X86:
                        return NativeMethodsX86.SniSecGenClientContextWrapper(pConn, pInPtr, (uint)pIn.Length, pOut, ref pcbOut, out pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
                    default:
                        throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
                }
            }
        }

        private static uint SNIWriteAsyncWrapper(SNIHandle pConn, [In] SNIPacket pPacket)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIWriteAsyncWrapper(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniWriteAsyncWrapper(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniWriteAsyncWrapper(pConn, pPacket);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIWriteSyncOverAsync(SNIHandle pConn, [In] SNIPacket pPacket)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIWriteSyncOverAsync(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniWriteSyncOverAsync(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniWriteSyncOverAsync(pConn, pPacket);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }
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
            uint ERROR_SUCCESS = 0;
            uint connIPLen = 0;

            int bufferSize = SniIP6AddrStringBufferLength;
            StringBuilder addrBuffer = new StringBuilder(bufferSize);

            ret = SNIGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out connIPLen);
            Debug.Assert(ret == ERROR_SUCCESS, "SNIGetPeerAddrStrWrapper fail");

            connIPStr = addrBuffer.ToString(0, Convert.ToInt32(connIPLen));

            return ret;
        }

        internal static uint SNIInitialize()
        {
            return SNIInitialize(IntPtr.Zero);
        }

        internal static IntPtr SNIServerEnumOpen()
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIServerEnumOpen();
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniServerEnumOpen();
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniServerEnumOpen();
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }
        internal static int SNIServerEnumRead([In] IntPtr packet, [In, Out] char[] readbuffer, int bufferLength, out bool more)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIServerEnumRead(packet, readbuffer, bufferLength, out more);
                case System.Runtime.InteropServices.Architecture.X64:
                    return NativeMethodsX64.SniServerEnumRead(packet, readbuffer, bufferLength, out more);
                case System.Runtime.InteropServices.Architecture.X86:
                    return NativeMethodsX86.SniServerEnumRead(packet, readbuffer, bufferLength, out more);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static void SNIServerEnumClose([In] IntPtr packet)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIServerEnumClose(packet);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    NativeMethodsX64.SniServerEnumClose(packet);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    NativeMethodsX86.SniServerEnumClose(packet);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
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
            Int32 transparentNetworkResolutionStateNo,
            Int32 totalTimeout,
            Boolean isAzureSqlServerEndpoint,
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

                clientConsumerInfo.isAzureSqlServerEndpoint = ADP.IsAzureSqlServerEndpoint(constring);

                switch (transparentNetworkResolutionStateNo)
                {
                    case (0):
                        clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.DisabledMode;
                        break;
                    case (1):
                        clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.SequentialMode;
                        break;
                    case (2):
                        clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.ParallelMode;
                        break;
                };
                clientConsumerInfo.totalTimeout = totalTimeout;

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

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal static uint SNIAddProvider(SNIHandle pConn,
                                            Provider providerEnum,
                                            AuthProviderInfo authInfo)
        {
            UInt32 ret;
            uint ERROR_SUCCESS = 0;

            Debug.Assert(authInfo.clientCertificateCallback == null, "CTAIP support has been removed");

            ret = SNIAddProviderWrapper(pConn, providerEnum, ref authInfo);

            if (ret == ERROR_SUCCESS)
            {
                // added a provider, need to requery for sync over async support
                ret = SNIGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, out bool _);
                Debug.Assert(ret == ERROR_SUCCESS, "SNIGetInfo cannot fail with this QType");
            }

            return ret;
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

        //[ResourceExposure(ResourceScope::None)]
        //
        // Notes on SecureString: Writing out security sensitive information to managed buffer should be avoid as these can be moved
        //    around by GC. There are two set of information which falls into this category: passwords and new changed password which
        //    are passed in as SecureString by a user. Writing out clear passwords information is delayed until this layer to ensure that
        //    the information is written out to buffer which is pinned in this method already. This also ensures that processing a clear password
        //    is done right before it is written out to SNI_Packet where gets encrypted properly. 
        //    TdsParserStaticMethods.EncryptPassword operation is also done here to minimize the time the clear password is held in memory. Any changes
        //    to loose encryption algorithm is changed it should be done in both in this method as well as TdsParserStaticMethods.EncryptPassword.
        //  Up to current release, it is also guaranteed that both password and new change password will fit into a single login packet whose size is fixed to 4096
        //        So, there is no splitting logic is needed.
        internal static void SNIPacketSetData(SNIPacket packet,
                                      Byte[] data,
                                      Int32 length,
                                      SecureString[] passwords,            // pointer to the passwords which need to be written out to SNI Packet
                                      Int32[] passwordOffsets    // Offset into data buffer where the password to be written out to
                                      )
        {
            Debug.Assert(passwords == null || (passwordOffsets != null && passwords.Length == passwordOffsets.Length), "The number of passwords does not match the number of password offsets");

            bool mustRelease = false;
            bool mustClearBuffer = false;
            IntPtr clearPassword = IntPtr.Zero;

            // provides a guaranteed finally block – without this it isn’t guaranteed – non interruptable by fatal exceptions
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                unsafe
                {

                    fixed (byte* pin_data = &data[0])
                    { }
                    if (passwords != null)
                    {
                        // Process SecureString
                        for (int i = 0; i < passwords.Length; ++i)
                        {
                            // SecureString is used
                            if (passwords[i] != null)
                            {
                                // provides a guaranteed finally block – without this it isn’t guaranteed – non interruptable by fatal exceptions
                                RuntimeHelpers.PrepareConstrainedRegions();
                                try
                                {
                                    // ==========================================================================
                                    //  Get the clear text of secure string without converting it to String type
                                    // ==========================================================================
                                    clearPassword = Marshal.SecureStringToCoTaskMemUnicode(passwords[i]);

                                    // ==========================================================================================================================
                                    //  Losely encrypt the clear text - The encryption algorithm should exactly match the TdsParserStaticMethods.EncryptPassword
                                    // ==========================================================================================================================

                                    unsafe
                                    {

                                        char* pwChar = (char*)clearPassword.ToPointer();
                                        byte* pByte = (byte*)(clearPassword.ToPointer());




                                        int s;
                                        byte bLo;
                                        byte bHi;
                                        int passwordsLength = passwords[i].Length;
                                        for (int j = 0; j < passwordsLength; ++j)
                                        {
                                            s = (int)*pwChar;
                                            bLo = (byte)(s & 0xff);
                                            bHi = (byte)((s >> 8) & 0xff);
                                            *(pByte++) = (Byte)((((bLo & 0x0f) << 4) | (bLo >> 4)) ^ 0xa5);
                                            *(pByte++) = (Byte)((((bHi & 0x0f) << 4) | (bHi >> 4)) ^ 0xa5);
                                            ++pwChar;
                                        }

                                        // ===========================================================
                                        //  Write out the losely encrypted passwords to data buffer
                                        // ===========================================================
                                        mustClearBuffer = true;
                                        Marshal.Copy(clearPassword, data, passwordOffsets[i], passwordsLength * 2);
                                    }
                                }
                                finally
                                {
                                    // Make sure that we clear the security sensitive information
                                    if (clearPassword != IntPtr.Zero)
                                    {
                                        Marshal.ZeroFreeCoTaskMemUnicode(clearPassword);
                                    }
                                }
                            }
                        }
                    }

                    packet.DangerousAddRef(ref mustRelease);
                    Debug.Assert(mustRelease, "AddRef Failed!");

                    fixed (byte* pin_data = &data[0])
                    {
                        SNIPacketSetData(packet, pin_data, (uint)length);
                    }
                }
            }
            finally
            {
                if (mustRelease)
                {
                    packet.DangerousRelease();
                }

                // Make sure that we clear the security sensitive information
                // data->Initialize() is not safe to call under CER
                if (mustClearBuffer)
                {
                    for (int i = 0; i < data.Length; ++i)
                    {
                        data[i] = 0;
                    }
                }
            }
        }

        internal static unsafe uint SNISecGenClientContext(SNIHandle pConnectionObject, ReadOnlySpan<byte> inBuff, byte[] OutBuff, ref uint sendLength, byte[] serverUserName)
        {
            fixed (byte* pin_serverUserName = &serverUserName[0])
            {
                return SNISecGenClientContextWrapper(
                    pConnectionObject,
                    inBuff,
                    OutBuff,
                    ref sendLength,
                    out bool _,
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
