// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
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
    internal static class SniNativeWrapper
    {
        #region Member Variables
        
        private const int SniIpv6AddrStringBufferLength = 48;
        private const int SniOpenTimeOut = -1;
        
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
        
        internal static uint SniAddProvider(SNIHandle pConn, Provider ProvNum, [In] ref AuthProviderInfo pInfo) =>
            s_nativeMethods.SniAddProvider(pConn, ProvNum, ref pInfo);
        
        #if NETFRAMEWORK
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal static uint SniAddProvider(SNIHandle pConn,
            Provider providerEnum,
            AuthProviderInfo authInfo)
        {
            UInt32 ret;
            uint ERROR_SUCCESS = 0;

            Debug.Assert(authInfo.clientCertificateCallback == null, "CTAIP support has been removed");

            ret = SniAddProvider(pConn, providerEnum, ref authInfo);

            if (ret == ERROR_SUCCESS)
            {
                // added a provider, need to requery for sync over async support
                ret = s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, out bool _);
                Debug.Assert(ret == ERROR_SUCCESS, "SNIGetInfo cannot fail with this QType");
            }

            return ret;
        }
        #endif
        
        internal static uint SniAddProvider(SNIHandle pConn, Provider ProvNum, [In] ref uint pInfo) =>
            s_nativeMethods.SniAddProvider(pConn, ProvNum, ref pInfo);
        
        internal static uint SniCheckConnection([In] SNIHandle pConn) =>
            s_nativeMethods.SniCheckConnection(pConn);
        
        internal static uint SniClose(IntPtr pConn) =>
            s_nativeMethods.SniClose(pConn);
        
        internal static uint SniGetConnectionId(SNIHandle pConn, ref Guid connId) =>
            s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_CONNID, out connId);
        
        internal static uint SniGetConnectionIPString(SNIHandle pConn, ref string connIPStr)
        {
            UInt32 ret;
            uint connIPLen = 0;

            int bufferSize = SniIpv6AddrStringBufferLength;
            StringBuilder addrBuffer = new StringBuilder(bufferSize);

            ret = s_nativeMethods.SniGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out connIPLen);

            connIPStr = addrBuffer.ToString(0, Convert.ToInt32(connIPLen));

            return ret;
        }
        
        internal static uint SniGetConnectionPort(SNIHandle pConn, ref ushort portNum)
        {
            return s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_PEERPORT, out portNum);
        }
        
        internal static void SniGetLastError(out SniError pErrorStruct) =>
            s_nativeMethods.SniGetLastError(out pErrorStruct);
        
        internal static uint SniGetProviderNumber(SNIHandle pConn, ref Provider provNum)
        {
            return s_nativeMethods.SniGetInfoWrapper(pConn, QueryType.SNI_QUERY_CONN_PROVIDERNUM, out provNum);
        }

        internal static uint SniInitialize() =>
            s_nativeMethods.SniInitialize(IntPtr.Zero);
        
        internal static uint SniIsTokenRestricted([In] IntPtr token, [MarshalAs(UnmanagedType.Bool)] out bool isRestricted) =>
            s_nativeMethods.SniIsTokenRestricted(token, out isRestricted);
        
        internal static unsafe uint SniOpenMarsSession(ConsumerInfo consumerInfo, SNIHandle parent, ref IntPtr pConn, bool fSync, SqlConnectionIPAddressPreference ipPreference, SQLDNSInfo cachedDNSInfo)
        {
            // initialize consumer info for MARS
            SniConsumerInfo native_consumerInfo = new SniConsumerInfo();
            MarshalConsumerInfo(consumerInfo, ref native_consumerInfo);

            SniDnsCacheInfo native_cachedDNSInfo = new SniDnsCacheInfo();
            native_cachedDNSInfo.wszCachedFQDN = cachedDNSInfo?.FQDN;
            native_cachedDNSInfo.wszCachedTcpIPv4 = cachedDNSInfo?.AddrIPv4;
            native_cachedDNSInfo.wszCachedTcpIPv6 = cachedDNSInfo?.AddrIPv6;
            native_cachedDNSInfo.wszCachedTcpPort = cachedDNSInfo?.Port;

            return s_nativeMethods.SniOpenWrapper(ref native_consumerInfo, "session:", parent, out pConn, fSync, ipPreference, ref native_cachedDNSInfo);
        }

        internal static unsafe uint SniOpenSyncEx(
            ConsumerInfo consumerInfo,
            string constring,
            ref IntPtr pConn,
            ref string spn,
            byte[] instanceName,
            bool fOverrideCache,
            bool fSync,
            int timeout,
            bool fParallel,
            
            #if NETFRAMEWORK
            Int32 transparentNetworkResolutionStateNo,
            Int32 totalTimeout,
            #endif
            
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

                #if NETFRAMEWORK
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
                #else
                clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.DisabledMode;
                clientConsumerInfo.totalTimeout = SniOpenTimeOut;
                #endif

                clientConsumerInfo.isAzureSqlServerEndpoint = ADP.IsAzureSqlServerEndpoint(constring);

                clientConsumerInfo.ipAddressPreference = ipPreference;
                clientConsumerInfo.DNSCacheInfo.wszCachedFQDN = cachedDNSInfo?.FQDN;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv4 = cachedDNSInfo?.AddrIPv4;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv6 = cachedDNSInfo?.AddrIPv6;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpPort = cachedDNSInfo?.Port;

                if (spn != null)
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
                        var writer = SqlObjectPools.BufferWriter.Rent();

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
                            SqlObjectPools.BufferWriter.Return(writer);
                        }
                    }
                }
                else
                {
                    // else leave szSPN null (SQL Auth)
                    return s_nativeMethods.SniOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                }
            }
        }

        internal static void SniPacketAllocate(SafeHandle pConn, IoType IOType, ref IntPtr pPacket) =>
            pPacket = s_nativeMethods.SniPacketAllocateWrapper(pConn, IOType);
        
        internal static unsafe uint SniPacketGetData(IntPtr packet, byte[] readBuffer, ref uint dataSize) =>
            s_nativeMethods.SniPacketGetDataWrapper(packet, readBuffer, (uint)readBuffer.Length, out dataSize);
        
        internal static void SniPacketRelease(IntPtr pPacket) =>
            s_nativeMethods.SniPacketRelease(pPacket);
        
        internal static unsafe void SniPacketSetData(SNIPacket packet, byte[] data, int length)
        {
            fixed (byte* pin_data = &data[0])
            {
                s_nativeMethods.SniPacketSetData(packet, pin_data, (uint)length);
            }
        }
        
        #if NETFRAMEWORK
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
        internal static void SniPacketSetData(SNIPacket packet,
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

                    SniPacketSetData(packet, data, length);
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
        #endif
        
        internal static void SniPacketReset([In] SNIHandle pConn, IoType IOType, SNIPacket pPacket, ConsumerNumber ConsNum) =>
            s_nativeMethods.SniPacketReset(pConn, IOType, pPacket, ConsNum);
        
        internal static uint SniQueryInfo(QueryType QType, ref uint pbQInfo) =>
            s_nativeMethods.SniQueryInfo(QType, ref pbQInfo);
        
        internal static uint SniQueryInfo(QueryType QType, ref IntPtr pbQInfo) =>
            s_nativeMethods.SniQueryInfo(QType, ref pbQInfo);
        
        internal static uint SniReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket) =>
            s_nativeMethods.SniReadAsync(pConn, ref ppNewPacket);
        
        internal static uint SniReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout) =>
            s_nativeMethods.SniReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
        
        internal static uint SniRemoveProvider(SNIHandle pConn, Provider ProvNum) =>
            s_nativeMethods.SniRemoveProvider(pConn, ProvNum);
        
        internal static unsafe uint SniSecGenClientContext(
            SNIHandle pConnectionObject,
            ReadOnlySpan<byte> inBuff,
            Span<byte> outBuff,
            ref uint sendLength,
            string serverUserName)
        {
            var serverWriter = SqlObjectPools.BufferWriter.Rent();

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
                SqlObjectPools.BufferWriter.Return(serverWriter);
            }
        }
        
        internal static uint SniSecInitPackage(ref uint pcbMaxToken) =>
            s_nativeMethods.SniSecInitPackage(ref pcbMaxToken);
        
        internal static void SniServerEnumClose([In] IntPtr packet) =>
            s_nativeMethods.SniServerEnumClose(packet);
        
        internal static IntPtr SniServerEnumOpen() =>
            s_nativeMethods.SniServerEnumOpen();
        
        internal static int SniServerEnumRead(
            [In] IntPtr packet,
            [In] [MarshalAs(UnmanagedType.LPArray)] char[] readBuffer,
            [In] int bufferLength,
            [MarshalAs(UnmanagedType.Bool)] out bool more) =>
            s_nativeMethods.SniServerEnumRead(packet, readBuffer, bufferLength, out more);
        
        internal static uint SniSetInfo(SNIHandle pConn, QueryType QType, [In] ref uint pbQInfo) =>
            s_nativeMethods.SniSetInfo(pConn, QType, ref pbQInfo);
        
        internal static uint SniTerminate() =>
            s_nativeMethods.SniTerminate();
        
        internal static uint SniWaitForSslHandshakeToComplete([In] SNIHandle pConn, int dwMilliseconds, out uint pProtocolVersion) =>
            s_nativeMethods.SniWaitForSslHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
        
        internal static uint SniWritePacket(SNIHandle pConn, SNIPacket packet, bool sync)
        {
            if (sync)
            {
                return s_nativeMethods.SniWriteSyncOverAsync(pConn, packet);
            }
            else
            {
                return s_nativeMethods.SniWriteAsyncWrapper(pConn, packet);
            }
        }
        
        #endregion

        #region Private Methods

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
        
        #endregion
        
        
        #if NETFRAMEWORK
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
        #endif
    }
}

namespace Microsoft.Data
{
    internal static class Win32NativeMethods
    {
        internal static bool IsTokenRestrictedWrapper(IntPtr token)
        {
            bool isRestricted;
            uint result = SniNativeWrapper.SniIsTokenRestricted(token, out isRestricted);

            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(unchecked((int)result));
            }

            return isRestricted;
        }
    }
}
