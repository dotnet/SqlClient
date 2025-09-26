// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Interop.Windows.Sni;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Utilities;

#if NETFRAMEWORK
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using Interop.Windows;
#endif

namespace Microsoft.Data.SqlClient
{
    internal static class SniNativeWrapper
    {
        #region Member Variables
        
        private const int SniIpv6AddrStringBufferLength = 48;
        
        #if NET
        private const int SniOpenTimeOut = -1;
        #endif
        
        #if NETFRAMEWORK
        private static readonly ISniNativeMethods s_nativeMethods = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => new SniNativeMethodsArm64(),
            Architecture.X64 => new SniNativeMethodsX64(),
            Architecture.X86 => new SniNativeMethodsX86(),
            _ => new SniNativeMethodsNotSupported(RuntimeInformation.ProcessArchitecture)
        };
        #else
        private static readonly SniNativeMethods s_nativeMethods = new SniNativeMethods();
        #endif
        
        private static int s_sniMaxComposedSpnLength = -1;
        
        #endregion
        
        internal static int SniMaxComposedSpnLength
        {
            get
            {
                if (s_sniMaxComposedSpnLength == -1)
                {
                    s_sniMaxComposedSpnLength = checked((int)s_nativeMethods.SniGetMaxComposedSpnLength());
                }
                return s_sniMaxComposedSpnLength;
            }
        }

        #region Public Methods
        
        internal static uint SniAddProvider(SNIHandle pConn, Provider provNum, ref AuthProviderInfo pInfo) =>
            s_nativeMethods.SniAddProvider(pConn, provNum, ref pInfo);
        
        #if NETFRAMEWORK
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal static uint SniAddProvider(SNIHandle pConn,
            Provider providerEnum,
            AuthProviderInfo authInfo)
        {
            Debug.Assert(authInfo.clientCertificateCallback == null, "CTAIP support has been removed");

            uint ret = SniAddProvider(pConn, providerEnum, ref authInfo);
            if (ret == SystemErrors.ERROR_SUCCESS)
            {
                // added a provider, need to requery for sync over async support
                ret = s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, out bool _);
                Debug.Assert(ret == SystemErrors.ERROR_SUCCESS, "SNIGetInfo cannot fail with this QType");
            }

            return ret;
        }
        #endif
        
        internal static uint SniAddProvider(SNIHandle pConn, Provider provNum, ref uint pInfo) =>
            s_nativeMethods.SniAddProvider(pConn, provNum, ref pInfo);
        
        internal static uint SniCheckConnection(SNIHandle pConn) =>
            s_nativeMethods.SniCheckConnection(pConn);
        
        internal static uint SniClose(IntPtr pConn) =>
            s_nativeMethods.SniClose(pConn);
        
        internal static uint SniGetConnectionId(SNIHandle pConn, ref Guid connId) =>
            s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_CONNID, out connId);
        
        internal static uint SniGetConnectionIpString(SNIHandle pConn, ref string connIpStr)
        {
            StringBuilder addrBuffer = new StringBuilder(SniIpv6AddrStringBufferLength);

            uint ret = s_nativeMethods.SniGetPeerAddrStrWrapper(
                pConn,
                SniIpv6AddrStringBufferLength,
                addrBuffer,
                out uint connIpLen);

            connIpStr = addrBuffer.ToString(0, Convert.ToInt32(connIpLen));

            return ret;
        }
        
        internal static uint SniGetConnectionPort(SNIHandle pConn, ref ushort portNum) =>
            s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_PEERPORT, out portNum);
        
        internal static void SniGetLastError(out SniError pErrorStruct) =>
            s_nativeMethods.SniGetLastError(out pErrorStruct);
        
        internal static uint SniGetProviderNumber(SNIHandle pConn, ref Provider provNum) =>
            s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_PROVIDERNUM, out provNum);

        internal static uint SniInitialize() =>
            s_nativeMethods.SniInitialize(IntPtr.Zero);

        internal static uint SniIsTokenRestricted(IntPtr token, out bool isRestricted)
        {
            uint result = s_nativeMethods.SniIsTokenRestricted(token, out isRestricted);
            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(unchecked((int)result));
            }

            return result;
        }
        
        internal static uint SniOpenMarsSession(
            ConsumerInfo consumerInfo,
            SNIHandle parent,
            ref IntPtr pConn,
            bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            SQLDNSInfo cachedDnsInfo)
        {
            // initialize consumer info for MARS
            SniConsumerInfo nativeConsumerInfo = new SniConsumerInfo();
            MarshalConsumerInfo(consumerInfo, ref nativeConsumerInfo);

            SniDnsCacheInfo nativeCachedDnsInfo = new SniDnsCacheInfo()
            {
                wszCachedFQDN = cachedDnsInfo?.FQDN,
                wszCachedTcpIPv4 = cachedDnsInfo?.AddrIPv4,
                wszCachedTcpIPv6 = cachedDnsInfo?.AddrIPv6,
                wszCachedTcpPort = cachedDnsInfo?.Port,
            };

            return s_nativeMethods.SniOpenWrapper(
                pConsumerInfo: ref nativeConsumerInfo,
                connect: "session:",
                pConn: parent,
                ppConn: out pConn,
                fSync,
                ipPreference,
                pDnsCacheInfo: ref nativeCachedDnsInfo);
        }

        internal static unsafe uint SniOpenSyncEx(
            ConsumerInfo consumerInfo,
            string connString,
            ref IntPtr pConn,
            ref string spn,
            byte[] instanceName,
            bool fOverrideCache,
            bool fSync,
            int timeout,
            bool fParallel,
            
            #if NETFRAMEWORK
            int transparentNetworkResolutionStateNo,
            int totalTimeout,
            #endif
            
            SqlConnectionIPAddressPreference ipPreference,
            SQLDNSInfo cachedDnsInfo,
            string hostNameInCertificate)
        {
            fixed (byte* pInstanceName = instanceName)
            {
                SniClientConsumerInfo clientConsumerInfo = new SniClientConsumerInfo();

                // initialize client ConsumerInfo part first
                MarshalConsumerInfo(consumerInfo, ref clientConsumerInfo.ConsumerInfo);

                clientConsumerInfo.wszConnectionString = connString;
                clientConsumerInfo.HostNameInCertificate = hostNameInCertificate;
                clientConsumerInfo.networkLibrary = Prefix.UNKNOWN_PREFIX;
                clientConsumerInfo.szInstanceName = pInstanceName;
                clientConsumerInfo.cchInstanceName = (uint)instanceName.Length;
                clientConsumerInfo.fOverrideLastConnectCache = fOverrideCache;
                clientConsumerInfo.fSynchronousConnection = fSync;
                clientConsumerInfo.timeout = timeout;
                clientConsumerInfo.fParallel = fParallel;

                #if NETFRAMEWORK
                switch (transparentNetworkResolutionStateNo)
                {
                    case 0:
                        clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.DisabledMode;
                        break;
                    case 1:
                        clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.SequentialMode;
                        break;
                    case 2:
                        clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.ParallelMode;
                        break;
                };
                clientConsumerInfo.totalTimeout = totalTimeout;
                #else
                clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.DisabledMode;
                clientConsumerInfo.totalTimeout = SniOpenTimeOut;
                #endif

                clientConsumerInfo.isAzureSqlServerEndpoint = ADP.IsAzureSqlServerEndpoint(connString);

                clientConsumerInfo.ipAddressPreference = ipPreference;
                clientConsumerInfo.DNSCacheInfo.wszCachedFQDN = cachedDnsInfo?.FQDN;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv4 = cachedDnsInfo?.AddrIPv4;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv6 = cachedDnsInfo?.AddrIPv6;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpPort = cachedDnsInfo?.Port;

                if (spn is not null)
                {
                    if (spn.Length == 0)
                    {
                        // An empty string implies we need to find the SPN so we supply a buffer for the max size
                        var array = ArrayPool<byte>.Shared.Rent(SniMaxComposedSpnLength);
                        array.AsSpan().Clear();

                        try
                        {
                            fixed (byte* pin_spnBuffer = array)
                            {
                                clientConsumerInfo.szSPN = pin_spnBuffer;
                                clientConsumerInfo.cchSPN = (uint)SniMaxComposedSpnLength;

                                var result = s_nativeMethods.SniOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                                if (result == 0)
                                {
                                    spn = Encoding.Unicode.GetString(array).TrimEnd('\0');
                                }

                                return result;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(array);
                        }
                    }
                    else
                    {
                        // We have a value of the SPN, so we marshal that and send it to the native layer
                        var writer = ObjectPools.BufferWriter.Rent();

                        try
                        {
                            // Native SNI requires the Unicode encoding and any other encoding like UTF8 breaks the code.
                            Encoding.Unicode.GetBytes(spn, writer);
                            Trace.Assert(writer.WrittenCount <= SniMaxComposedSpnLength,
                                "Length of the provided SPN exceeded the buffer size.");

                            fixed (byte* pin_spnBuffer = writer.WrittenSpan)
                            {
                                clientConsumerInfo.szSPN = pin_spnBuffer;
                                clientConsumerInfo.cchSPN = (uint)writer.WrittenCount;
                                return s_nativeMethods.SniOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                            }
                        }
                        finally
                        {
                            ObjectPools.BufferWriter.Return(writer);
                        }
                    }
                }

                // Otherwise leave szSPN null (SQL Auth)
                return s_nativeMethods.SniOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
            }
        }

        internal static void SniPacketAllocate(SafeHandle pConn, IoType ioType, ref IntPtr pPacket) =>
            pPacket = s_nativeMethods.SniPacketAllocateWrapper(pConn, ioType);
        
        internal static uint SniPacketGetData(IntPtr packet, byte[] readBuffer, ref uint dataSize) =>
            s_nativeMethods.SniPacketGetDataWrapper(packet, readBuffer, (uint)readBuffer.Length, out dataSize);
        
        internal static void SniPacketRelease(IntPtr pPacket) =>
            s_nativeMethods.SniPacketRelease(pPacket);
        
        internal static unsafe void SniPacketSetData(SNIPacket packet, byte[] data, int length)
        {
            fixed (byte* pData = data)
            {
                s_nativeMethods.SniPacketSetData(packet, pData, (uint)length);
            }
        }
        
        internal static void SniPacketReset(SNIHandle pConn, IoType ioType, SNIPacket pPacket, ConsumerNumber consNum) =>
            s_nativeMethods.SniPacketReset(pConn, ioType, pPacket, consNum);
        
        internal static uint SniQueryInfo(QueryType qType, ref uint pbQInfo) =>
            s_nativeMethods.SniQueryInfo(qType, ref pbQInfo);
        
        internal static uint SniQueryInfo(QueryType qType, ref IntPtr pbQInfo) =>
            s_nativeMethods.SniQueryInfo(qType, ref pbQInfo);
        
        internal static uint SniReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket) =>
            s_nativeMethods.SniReadAsync(pConn, ref ppNewPacket);
        
        internal static uint SniReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout) =>
            s_nativeMethods.SniReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
        
        internal static uint SniRemoveProvider(SNIHandle pConn, Provider provNum) =>
            s_nativeMethods.SniRemoveProvider(pConn, provNum);
        
        internal static unsafe uint SniSecGenClientContext(
            SNIHandle pConnectionObject,
            ReadOnlySpan<byte> inBuff,
            Span<byte> outBuff,
            ref uint sendLength,
            string serverUserName)
        {
            var serverWriter = ObjectPools.BufferWriter.Rent();

            try
            {
                Encoding.Unicode.GetBytes(serverUserName, serverWriter);

                fixed (byte* pInBuff = inBuff)
                fixed (byte* pOutBuff = outBuff)
                fixed (byte* pServerInfo = serverWriter.WrittenSpan)
                {
                    return s_nativeMethods.SniSecGenClientContextWrapper(
                        pConn: pConnectionObject,
                        pIn: pInBuff,
                        cbIn: (uint)inBuff.Length,
                        pOut: pOutBuff,
                        pcbOut: ref sendLength,
                        pfDone: out _,
                        szServerInfo: pServerInfo,
                        cbServerInfo: (uint)serverWriter.WrittenSpan.Length,
                        pwszUserName: null,
                        pwszPassword: null);
                }
            }
            finally
            {
                ObjectPools.BufferWriter.Return(serverWriter);
            }
        }
        
        internal static uint SniSecInitPackage(ref uint pcbMaxToken) =>
            s_nativeMethods.SniSecInitPackage(ref pcbMaxToken);
        
        internal static void SniServerEnumClose(IntPtr packet) =>
            s_nativeMethods.SniServerEnumClose(packet);
        
        internal static IntPtr SniServerEnumOpen() =>
            s_nativeMethods.SniServerEnumOpen();
        
        internal static int SniServerEnumRead(IntPtr packet, char[] readBuffer, int bufferLength, out bool more) =>
            s_nativeMethods.SniServerEnumRead(packet, readBuffer, bufferLength, out more);
        
        internal static uint SniSetInfo(SNIHandle pConn, QueryType qType, ref uint pbQInfo) =>
            s_nativeMethods.SniSetInfo(pConn, qType, ref pbQInfo);
        
        internal static uint SniTerminate() =>
            s_nativeMethods.SniTerminate();

        internal static uint SniWaitForSslHandshakeToComplete(
            SNIHandle pConn,
            int dwMilliseconds,
            out System.Security.Authentication.SslProtocols pProtocolVersion)
        {
            uint returnValue = s_nativeMethods.SniWaitForSslHandshakeToComplete(pConn, dwMilliseconds, out SniSslProtocols nativeProtocolVersion);

#pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
            if ((nativeProtocolVersion & SniSslProtocols.SP_PROT_TLS1_2) != 0)
            {
                pProtocolVersion = System.Security.Authentication.SslProtocols.Tls12;
            }
            else if ((nativeProtocolVersion & SniSslProtocols.SP_PROT_TLS1_3) != 0)
            {
#if NET
                pProtocolVersion = System.Security.Authentication.SslProtocols.Tls13;
#else
                // Only .NET Core supports SslProtocols.Tls13
                pProtocolVersion = (System.Security.Authentication.SslProtocols)0x3000;
#endif
            }
            else if ((nativeProtocolVersion & SniSslProtocols.SP_PROT_TLS1_1) != 0)
            {
#if NET8_0_OR_GREATER
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
                pProtocolVersion = System.Security.Authentication.SslProtocols.Tls11;
            }
            else if ((nativeProtocolVersion & SniSslProtocols.SP_PROT_TLS1_0) != 0)
            {
                pProtocolVersion = System.Security.Authentication.SslProtocols.Tls;
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
            }
            else if ((nativeProtocolVersion & SniSslProtocols.SP_PROT_SSL3) != 0)
            {
                // SSL 2.0 and 3.0 are only referenced to log a warning, not explicitly used for connections
#pragma warning disable CS0618, CA5397
                pProtocolVersion = System.Security.Authentication.SslProtocols.Ssl3;
            }
            else if ((nativeProtocolVersion & SniSslProtocols.SP_PROT_SSL2) != 0)
            {
                pProtocolVersion = System.Security.Authentication.SslProtocols.Ssl2;
#pragma warning restore CS0618, CA5397
            }
            else
            {
                pProtocolVersion = System.Security.Authentication.SslProtocols.None;
            }
#pragma warning restore CA5398 // Avoid hardcoded SslProtocols values
            return returnValue;
        }

        internal static uint SniWritePacket(SNIHandle pConn, SNIPacket packet, bool sync) =>
            sync
                ? s_nativeMethods.SniWriteSyncOverAsync(pConn, packet)
                : s_nativeMethods.SniWriteAsyncWrapper(pConn, packet);
        
#endregion

        #region Private Methods

        private static void MarshalConsumerInfo(ConsumerInfo consumerInfo, ref SniConsumerInfo nativeConsumerInfo)
        {
            nativeConsumerInfo.DefaultUserDataLength = consumerInfo.defaultBufferSize;
            nativeConsumerInfo.fnReadComp = consumerInfo.readDelegate is not null
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.readDelegate)
                : IntPtr.Zero;
            nativeConsumerInfo.fnWriteComp = consumerInfo.writeDelegate is not null
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.writeDelegate)
                : IntPtr.Zero;
            nativeConsumerInfo.ConsumerKey = consumerInfo.key;
        }
        
        #endregion
    }
}
