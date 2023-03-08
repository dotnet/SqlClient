// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient
{
    internal static class SNINativeMethodWrapper
    {
        private static int s_sniMaxComposedSpnLength = -1;
        private static readonly System.Runtime.InteropServices.Architecture s_architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

        private const int SniOpenTimeOut = -1; // infinite

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void SqlAsyncCallbackDelegate(IntPtr m_ConsKey, IntPtr pPacket, uint dwError);

        internal delegate IntPtr SqlClientCertificateDelegate(IntPtr pCallbackContext);

        internal const int ConnTerminatedError = 2;
        internal const int InvalidParameterError = 5;
        internal const int ProtocolNotSupportedError = 8;
        internal const int ConnTimeoutError = 11;
        internal const int ConnNotUsableError = 19;
        internal const int InvalidConnStringError = 25;
        internal const int HandshakeFailureError = 31;
        internal const int InternalExceptionError = 35;
        internal const int ConnOpenFailedError = 40;
        internal const int ErrorSpnLookup = 44;
        internal const int LocalDBErrorCode = 50;
        internal const int MultiSubnetFailoverWithMoreThan64IPs = 47;
        internal const int MultiSubnetFailoverWithInstanceSpecified = 48;
        internal const int MultiSubnetFailoverWithNonTcpProtocol = 49;
        internal const int MaxErrorValue = 50157;
        internal const int LocalDBNoInstanceName = 51;
        internal const int LocalDBNoInstallation = 52;
        internal const int LocalDBInvalidConfig = 53;
        internal const int LocalDBNoSqlUserInstanceDllPath = 54;
        internal const int LocalDBInvalidSqlUserInstanceDllPath = 55;
        internal const int LocalDBFailedToLoadDll = 56;
        internal const int LocalDBBadRuntime = 57;
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

        unsafe internal class SqlDependencyProcessDispatcherStorage
        {

            static void* data;

            static int size;
            static volatile int thelock; // Int used for a spin-lock.

            public static void* NativeGetData(out int passedSize)
            {
                passedSize = size;
                return data;
            }

            internal static bool NativeSetData(void* passedData, int passedSize)
            {
                bool success = false;

                while (0 != Interlocked.CompareExchange(ref thelock, 1, 0))
                { // Spin until we have the lock.
                    Thread.Sleep(50); // Sleep with short-timeout to prevent starvation.
                }
                Trace.Assert(1 == thelock); // Now that we have the lock, lock should be equal to 1.

                if (null == data)
                {
                    data = Marshal.AllocHGlobal(passedSize).ToPointer();

                    Trace.Assert(null != data);

                    System.Buffer.MemoryCopy(passedData, data, passedSize, passedSize);

                    Trace.Assert(0 == size); // Size should still be zero at this point.
                    size = passedSize;
                    success = true;
                }

                int result = Interlocked.CompareExchange(ref thelock, 0, 1);
                Trace.Assert(1 == result); // The release of the lock should have been successful.  

                return success;
            }
        }

        internal enum SniSpecialErrors : uint
        {
            LocalDBErrorCode = SNINativeMethodWrapper.LocalDBErrorCode,

            // multi-subnet-failover specific error codes
            MultiSubnetFailoverWithMoreThan64IPs = SNINativeMethodWrapper.MultiSubnetFailoverWithMoreThan64IPs,
            MultiSubnetFailoverWithInstanceSpecified = SNINativeMethodWrapper.MultiSubnetFailoverWithInstanceSpecified,
            MultiSubnetFailoverWithNonTcpProtocol = SNINativeMethodWrapper.MultiSubnetFailoverWithNonTcpProtocol,

            // max error code value
            MaxErrorValue = SNINativeMethodWrapper.MaxErrorValue,
        }

        #region Structs\Enums
        [StructLayout(LayoutKind.Sequential)]
        internal struct ConsumerInfo
        {
            internal int defaultBufferSize;
            internal SqlAsyncCallbackDelegate readDelegate;
            internal SqlAsyncCallbackDelegate writeDelegate;
            internal IntPtr key;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AuthProviderInfo
        {
            public uint flags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool tlsFirst;
            public object certContext;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string certId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool certHash;
            public object clientCertificateCallbackContext;
            public SqlClientCertificateDelegate clientCertificateCallback;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string serverCertFileName;
        };

        internal struct CTAIPProviderInfo
        {
            internal byte[] originalNetworkAddress;
            internal Boolean fromDataSecurityProxy;
        };

        struct SNIAuthProviderInfoWrapper
        {
            internal object pDelegateContext;
            internal SqlClientCertificateDelegate pSqlClientCertificateDelegate;
        };

        internal struct SNICTAIPProviderInfo
        {
            internal SNIHandle pConn;
            internal byte prgbAddress;
            internal ulong cbAddress;
            internal bool fFromDataSecurityProxy;
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct CredHandle
        {
            internal IntPtr dwLower;
            internal IntPtr dwUpper;
        };

        internal enum ContextAttribute
        {
            // sspi.h
            SECPKG_ATTR_SIZES = 0,
            SECPKG_ATTR_NAMES = 1,
            SECPKG_ATTR_LIFESPAN = 2,
            SECPKG_ATTR_DCE_INFO = 3,
            SECPKG_ATTR_STREAM_SIZES = 4,
            SECPKG_ATTR_AUTHORITY = 6,
            SECPKG_ATTR_PACKAGE_INFO = 10,
            SECPKG_ATTR_NEGOTIATION_INFO = 12,
            SECPKG_ATTR_UNIQUE_BINDINGS = 25,
            SECPKG_ATTR_ENDPOINT_BINDINGS = 26,
            SECPKG_ATTR_CLIENT_SPECIFIED_TARGET = 27,
            SECPKG_ATTR_APPLICATION_PROTOCOL = 35,

            // minschannel.h
            SECPKG_ATTR_REMOTE_CERT_CONTEXT = 0x53,    // returns PCCERT_CONTEXT
            SECPKG_ATTR_LOCAL_CERT_CONTEXT = 0x54,     // returns PCCERT_CONTEXT
            SECPKG_ATTR_ROOT_STORE = 0x55,             // returns HCERTCONTEXT to the root store
            SECPKG_ATTR_ISSUER_LIST_EX = 0x59,         // returns SecPkgContext_IssuerListInfoEx
            SECPKG_ATTR_CONNECTION_INFO = 0x5A,        // returns SecPkgContext_ConnectionInfo
            SECPKG_ATTR_UI_INFO = 0x68, // sets SEcPkgContext_UiInfo  
        }

        internal enum ConsumerNumber
        {
            SNI_Consumer_SNI,
            SNI_Consumer_SSB,
            SNI_Consumer_PacketIsReleased,
            SNI_Consumer_Invalid,
        }

        internal enum IOType
        {
            READ,
            WRITE,
        }

        internal enum PrefixEnum
        {
            UNKNOWN_PREFIX,
            SM_PREFIX,
            TCP_PREFIX,
            NP_PREFIX,
            VIA_PREFIX,
            INVALID_PREFIX,
        }

        internal enum ProviderEnum
        {
            HTTP_PROV,
            NP_PROV,
            SESSION_PROV,
            SIGN_PROV,
            SM_PROV,
            SMUX_PROV,
            SSL_PROV,
            TCP_PROV,
            VIA_PROV,
            CTAIP_PROV,
            MAX_PROVS,
            INVALID_PROV,
        }

        internal enum QTypes
        {
            SNI_QUERY_CONN_INFO,
            SNI_QUERY_CONN_BUFSIZE,
            SNI_QUERY_CONN_KEY,
            SNI_QUERY_CLIENT_ENCRYPT_POSSIBLE,
            SNI_QUERY_SERVER_ENCRYPT_POSSIBLE,
            SNI_QUERY_CERTIFICATE,
            SNI_QUERY_LOCALDB_HMODULE,
            SNI_QUERY_CONN_ENCRYPT,
            SNI_QUERY_CONN_PROVIDERNUM,
            SNI_QUERY_CONN_CONNID,
            SNI_QUERY_CONN_PARENTCONNID,
            SNI_QUERY_CONN_SECPKG,
            SNI_QUERY_CONN_NETPACKETSIZE,
            SNI_QUERY_CONN_NODENUM,
            SNI_QUERY_CONN_PACKETSRECD,
            SNI_QUERY_CONN_PACKETSSENT,
            SNI_QUERY_CONN_PEERADDR,
            SNI_QUERY_CONN_PEERPORT,
            SNI_QUERY_CONN_LASTREADTIME,
            SNI_QUERY_CONN_LASTWRITETIME,
            SNI_QUERY_CONN_CONSUMER_ID,
            SNI_QUERY_CONN_CONNECTTIME,
            SNI_QUERY_CONN_HTTPENDPOINT,
            SNI_QUERY_CONN_LOCALADDR,
            SNI_QUERY_CONN_LOCALPORT,
            SNI_QUERY_CONN_SSLHANDSHAKESTATE,
            SNI_QUERY_CONN_SOBUFAUTOTUNING,
            SNI_QUERY_CONN_SECPKGNAME,
            SNI_QUERY_CONN_SECPKGMUTUALAUTH,
            SNI_QUERY_CONN_CONSUMERCONNID,
            SNI_QUERY_CONN_SNIUCI,
            SNI_QUERY_CONN_SUPPORTS_EXTENDED_PROTECTION,
            SNI_QUERY_CONN_CHANNEL_PROVIDES_AUTHENTICATION_CONTEXT,
            SNI_QUERY_CONN_PEERID,
            SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC,
            SNI_QUERY_CONN_SSL_SECCTXTHANDLE,
        }

        internal enum TransparentNetworkResolutionMode : byte
        {
            DisabledMode = 0,
            SequentialMode,
            ParallelMode
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct Sni_Consumer_Info
        {
            public int DefaultUserDataLength;
            public IntPtr ConsumerKey;
            public IntPtr fnReadComp;
            public IntPtr fnWriteComp;
            public IntPtr fnTrace;
            public IntPtr fnAcceptComp;
            public uint dwNumProts;
            public IntPtr rgListenInfo;
            public IntPtr NodeAffinity;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct SNI_CLIENT_CONSUMER_INFO
        {
            public Sni_Consumer_Info ConsumerInfo;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszConnectionString;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string HostNameInCertificate;
            public PrefixEnum networkLibrary;
            public byte* szSPN;
            public uint cchSPN;
            public byte* szInstanceName;
            public uint cchInstanceName;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fOverrideLastConnectCache;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fSynchronousConnection;
            public int timeout;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fParallel;
            public TransparentNetworkResolutionMode transparentNetworkResolution;
            public int totalTimeout;
            public bool isAzureSqlServerEndpoint;
            public SqlConnectionIPAddressPreference ipAddressPreference;
            public SNI_DNSCache_Info DNSCacheInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SNI_DNSCache_Info
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedFQDN;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedTcpIPv4;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedTcpIPv6;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedTcpPort;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SNI_Error
        {
            internal ProviderEnum provider;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
            internal string errorMessage;
            internal uint nativeError;
            internal uint sniError;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string fileName;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string function;
            internal uint lineNumber;
        }

        #endregion

        #region DLL Imports
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("secur32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern uint QueryContextAttributes(ref CredHandle contextHandle, [In] ContextAttribute attribute, [In] IntPtr buffer);

        internal static uint SNIAddProvider(SNIHandle pConn, ProviderEnum ProvNum, [In] ref uint pInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIAddProvider(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIAddProvider(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIAddProvider(pConn, ProvNum, ref pInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIAddProviderWrapper(SNIHandle pConn, ProviderEnum ProvNum, [In] ref SNICTAIPProviderInfo pInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIAddProviderWrapper(SNIHandle pConn, ProviderEnum ProvNum, [In] ref AuthProviderInfo pInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIAddProviderWrapper(pConn, ProvNum, ref pInfo);
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
                    return SNINativeManagedWrapperX64.SNICheckConnection(pConn);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNICheckConnection(pConn);
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
                    return SNINativeManagedWrapperX64.SNIClose(pConn);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIClose(pConn);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static void SNIGetLastError(out SNI_Error pErrorStruct)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIGetLastError(out pErrorStruct);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    SNINativeManagedWrapperX64.SNIGetLastError(out pErrorStruct);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    SNINativeManagedWrapperX86.SNIGetLastError(out pErrorStruct);
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
                    SNINativeManagedWrapperX64.SNIPacketRelease(pPacket);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    SNINativeManagedWrapperX86.SNIPacketRelease(pPacket);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static void SNIPacketReset([In] SNIHandle pConn, IOType IOType, SNIPacket pPacket, ConsumerNumber ConsNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    SNINativeManagedWrapperARM64.SNIPacketReset(pConn, IOType, pPacket, ConsNum);
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    SNINativeManagedWrapperX64.SNIPacketReset(pConn, IOType, pPacket, ConsNum);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    SNINativeManagedWrapperX86.SNIPacketReset(pConn, IOType, pPacket, ConsNum);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIQueryInfo(QTypes QType, ref uint pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIQueryInfo(QType, ref pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIQueryInfo(QTypes QType, ref IntPtr pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIQueryInfo(QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIQueryInfo(QType, ref pbQInfo);
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
                    return SNINativeManagedWrapperX64.SNIReadAsync(pConn, ref ppNewPacket);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIReadAsync(pConn, ref ppNewPacket);
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
                    return SNINativeManagedWrapperX64.SNIReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIReadSyncOverAsync(pConn, ref ppNewPacket, timeout);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNIRemoveProvider(SNIHandle pConn, ProviderEnum ProvNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIRemoveProvider(pConn, ProvNum);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIRemoveProvider(pConn, ProvNum);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIRemoveProvider(pConn, ProvNum);
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
                    return SNINativeManagedWrapperX64.SNISecInitPackage(ref pcbMaxToken);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNISecInitPackage(ref pcbMaxToken);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static uint SNISetInfo(SNIHandle pConn, QTypes QType, [In] ref uint pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNISetInfo(pConn, QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNISetInfo(pConn, QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNISetInfo(pConn, QType, ref pbQInfo);
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
                    return SNINativeManagedWrapperX64.SNITerminate();
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNITerminate();
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
                    return SNINativeManagedWrapperX64.SNIWaitForSSLHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIWaitForSSLHandshakeToComplete(pConn, dwMilliseconds, out pProtocolVersion);
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
                    return SNINativeManagedWrapperX64.UnmanagedIsTokenRestricted(token, out isRestricted);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.UnmanagedIsTokenRestricted(token, out isRestricted);
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
                    return SNINativeManagedWrapperX64.GetSniMaxComposedSpnLength();
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.GetSniMaxComposedSpnLength();
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, out Guid pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, [MarshalAs(UnmanagedType.Bool)] out bool pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIGetInfoWrapper(pConn, QType, out pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, ref IntPtr pbQInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIGetInfoWrapper(pConn, QType, ref pbQInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIGetInfoWrapper(pConn, QType, ref pbQInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, out ushort portNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out portNum);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIGetInfoWrapper(pConn, QType, out portNum);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIGetInfoWrapper(pConn, QType, out portNum);
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
                    return SNINativeManagedWrapperX64.SNIGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLen);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIGetPeerAddrStrWrapper(pConn, bufferSize, addrBuffer, out addrLen);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIGetInfoWrapper([In] SNIHandle pConn, SNINativeMethodWrapper.QTypes QType, out ProviderEnum provNum)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIGetInfoWrapper(pConn, QType, out provNum);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIGetInfoWrapper(pConn, QType, out provNum);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIGetInfoWrapper(pConn, QType, out provNum);
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
                    return SNINativeManagedWrapperX64.SNIInitialize(pmo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIInitialize(pmo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIOpenSyncExWrapper(ref SNI_CLIENT_CONSUMER_INFO pClientConsumerInfo, out IntPtr ppConn)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIOpenSyncExWrapper(ref pClientConsumerInfo, out ppConn);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIOpenWrapper(
            [In] ref Sni_Consumer_Info pConsumerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string szConnect,
            [In] SNIHandle pConn,
            out IntPtr ppConn,
            [MarshalAs(UnmanagedType.Bool)] bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            [In] ref SNI_DNSCache_Info pDNSCachedInfo)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIOpenWrapper(ref pConsumerInfo, szConnect, pConn, out ppConn, fSync, ipPreference, ref pDNSCachedInfo);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIOpenWrapper(ref pConsumerInfo, szConnect, pConn, out ppConn, fSync, ipPreference, ref pDNSCachedInfo);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIOpenWrapper(ref pConsumerInfo, szConnect, pConn, out ppConn, fSync, ipPreference, ref pDNSCachedInfo);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static IntPtr SNIPacketAllocateWrapper([In] SafeHandle pConn, IOType IOType)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIPacketAllocateWrapper(pConn, IOType);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIPacketAllocateWrapper(pConn, IOType);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIPacketAllocateWrapper(pConn, IOType);
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
                    return SNINativeManagedWrapperX64.SNIPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIPacketGetDataWrapper(packet, readBuffer, readBufferLength, out dataSize);
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
                    SNINativeManagedWrapperX64.SNIPacketSetData(pPacket, pbBuf, cbBuf);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    SNINativeManagedWrapperX86.SNIPacketSetData(pPacket, pbBuf, cbBuf);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static unsafe uint SNISecGenClientContextWrapper(
            [In] SNIHandle pConn,
            [In, Out] byte[] pIn,
            uint cbIn,
            [In, Out] byte[] pOut,
            [In] ref uint pcbOut,
            [MarshalAsAttribute(UnmanagedType.Bool)] out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszUserName,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszPassword)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNISecGenClientContextWrapper(pConn, pIn, cbIn, pOut, ref pcbOut, out pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNISecGenClientContextWrapper(pConn, pIn, cbIn, pOut, ref pcbOut, out pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNISecGenClientContextWrapper(pConn, pIn, cbIn, pOut, ref pcbOut, out pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static uint SNIWriteAsyncWrapper(SNIHandle pConn, [In] SNIPacket pPacket)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIWriteAsyncWrapper(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIWriteAsyncWrapper(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIWriteAsyncWrapper(pConn, pPacket);
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
                    return SNINativeManagedWrapperX64.SNIWriteSyncOverAsync(pConn, pPacket);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIWriteSyncOverAsync(pConn, pPacket);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        private static IntPtr SNIClientCertificateFallbackWrapper(IntPtr pCallbackContext)
        {
            switch (s_architecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return SNINativeManagedWrapperARM64.SNIClientCertificateFallbackWrapper(pCallbackContext);
                case System.Runtime.InteropServices.Architecture.X64:
                    return SNINativeManagedWrapperX64.SNIClientCertificateFallbackWrapper(pCallbackContext);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIClientCertificateFallbackWrapper(pCallbackContext);
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }
        #endregion

        internal static uint SNISecGetServerCertificate(SNIHandle pConnectionObject, ref X509Certificate2 certificate)
        {
            System.UInt32 ret;
            CredHandle pSecHandle;
            X509Certificate pCertContext = null;

            // provides a guaranteed finally block – without this it isn’t guaranteed – non interruptable by fatal exceptions
            bool mustRelease = false;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                pConnectionObject.DangerousAddRef(ref mustRelease);
                Debug.Assert(mustRelease, "AddRef Failed!");

                IntPtr secHandlePtr = Marshal.AllocHGlobal(Marshal.SizeOf<CredHandle>());

                ret = SNIGetInfoWrapper(pConnectionObject, QTypes.SNI_QUERY_CONN_SSL_SECCTXTHANDLE, ref secHandlePtr);
                //ERROR_SUCCESS
                if (0 == ret)
                {
                    // Cast an unmanaged block to pSecHandle;
                    pSecHandle = Marshal.PtrToStructure<CredHandle>(secHandlePtr);

                    // SEC_E_OK
                    if (0 == (ret = QueryContextAttributes(ref pSecHandle, ContextAttribute.SECPKG_ATTR_REMOTE_CERT_CONTEXT, pCertContext.Handle)))
                    {
                        certificate = new X509Certificate2(pCertContext.Handle);
                    }
                }
                Marshal.FreeHGlobal(secHandlePtr);
            }
            finally
            {
                if (pCertContext != null)
                {
                    pCertContext.Dispose();
                }
                if (mustRelease)
                {
                    pConnectionObject.DangerousRelease();
                }
            }
            return ret;
        }

        internal static uint SniGetConnectionId(SNIHandle pConn, ref Guid connId)
        {
            return SNIGetInfoWrapper(pConn, QTypes.SNI_QUERY_CONN_CONNID, out connId);
        }

        internal static uint SniGetProviderNumber(SNIHandle pConn, ref ProviderEnum provNum)
        {
            return SNIGetInfoWrapper(pConn, QTypes.SNI_QUERY_CONN_PROVIDERNUM, out provNum);
        }

        internal static uint SniGetConnectionPort(SNIHandle pConn, ref ushort portNum)
        {
            return SNIGetInfoWrapper(pConn, QTypes.SNI_QUERY_CONN_PEERPORT, out portNum);
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
                    return SNINativeManagedWrapperX64.SNIServerEnumOpen();
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIServerEnumOpen();
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
                    return SNINativeManagedWrapperX64.SNIServerEnumRead(packet, readbuffer, bufferLength, out more);
                case System.Runtime.InteropServices.Architecture.X86:
                    return SNINativeManagedWrapperX86.SNIServerEnumRead(packet, readbuffer, bufferLength, out more);
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
                    SNINativeManagedWrapperX64.SNIServerEnumClose(packet);
                    break;
                case System.Runtime.InteropServices.Architecture.X86:
                    SNINativeManagedWrapperX86.SNIServerEnumClose(packet);
                    break;
                default:
                    throw ADP.SNIPlatformNotSupported(s_architecture.ToString());
            }
        }

        internal static unsafe uint SNIOpenMarsSession(ConsumerInfo consumerInfo, SNIHandle parent, ref IntPtr pConn, bool fSync, SqlConnectionIPAddressPreference ipPreference, SQLDNSInfo cachedDNSInfo)
        {
            // initialize consumer info for MARS
            Sni_Consumer_Info native_consumerInfo = new Sni_Consumer_Info();
            MarshalConsumerInfo(consumerInfo, ref native_consumerInfo);

            SNI_DNSCache_Info native_cachedDNSInfo = new SNI_DNSCache_Info();
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
                SNI_CLIENT_CONSUMER_INFO clientConsumerInfo = new SNI_CLIENT_CONSUMER_INFO();

                // initialize client ConsumerInfo part first
                MarshalConsumerInfo(consumerInfo, ref clientConsumerInfo.ConsumerInfo);

                clientConsumerInfo.wszConnectionString = constring;
                clientConsumerInfo.HostNameInCertificate = hostNameInCertificate;
                clientConsumerInfo.networkLibrary = PrefixEnum.UNKNOWN_PREFIX;
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
                                            ProviderEnum providerEnum,
                                            AuthProviderInfo authInfo)
        {
            UInt32 ret;
            uint ERROR_SUCCESS = 0;
            SNIAuthProviderInfoWrapper sniAuthInfoWrapper;

            if (authInfo.clientCertificateCallback != null)
            {
                sniAuthInfoWrapper.pDelegateContext = authInfo.clientCertificateCallbackContext;
                sniAuthInfoWrapper.pSqlClientCertificateDelegate = authInfo.clientCertificateCallback;

                authInfo.clientCertificateCallbackContext = sniAuthInfoWrapper;
                authInfo.clientCertificateCallback = SNIClientCertificateFallbackWrapper;
            }

            ret = SNIAddProviderWrapper(pConn, providerEnum, ref authInfo);

            if (ret == ERROR_SUCCESS)
            {
                // added a provider, need to requery for sync over async support
                bool fSupportsSyncOverAsync;
                ret = SNIGetInfoWrapper(pConn, QTypes.SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, out fSupportsSyncOverAsync);
                Debug.Assert(ret == ERROR_SUCCESS, "SNIGetInfo cannot fail with this QType");
            }

            return ret;
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal static uint SNIAddProvider(SNIHandle pConn,
                                            ProviderEnum providerEnum,
                                            CTAIPProviderInfo authInfo)
        {
            UInt32 ret;
            uint ERROR_SUCCESS = 0;


            SNICTAIPProviderInfo ctaipInfo = new SNICTAIPProviderInfo();

            ctaipInfo.prgbAddress = authInfo.originalNetworkAddress[0];
            ctaipInfo.cbAddress = (byte)authInfo.originalNetworkAddress.Length;
            ctaipInfo.fFromDataSecurityProxy = authInfo.fromDataSecurityProxy;

            ret = SNIAddProviderWrapper(pConn, providerEnum, ref ctaipInfo);

            if (ret == ERROR_SUCCESS)
            {
                // added a provider, need to requery for sync over async support
                bool fSupportsSyncOverAsync;
                ret = SNIGetInfoWrapper(pConn, QTypes.SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, out fSupportsSyncOverAsync);
                Debug.Assert(ret == ERROR_SUCCESS, "SNIGetInfo cannot fail with this QType");
            }

            return ret;
        }

        internal static void SNIPacketAllocate(SafeHandle pConn, IOType IOType, ref IntPtr pPacket)
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

        internal static unsafe uint SNISecGenClientContext(SNIHandle pConnectionObject, byte[] inBuff, uint receivedLength, byte[] OutBuff, ref uint sendLength, byte[] serverUserName)
        {
            fixed (byte* pin_serverUserName = &serverUserName[0])
            {
                bool local_fDone;
                return SNISecGenClientContextWrapper(
                    pConnectionObject,
                    inBuff,
                    receivedLength,
                    OutBuff,
                    ref sendLength,
                    out local_fDone,
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

        private static void MarshalConsumerInfo(ConsumerInfo consumerInfo, ref Sni_Consumer_Info native_consumerInfo)
        {
            native_consumerInfo.DefaultUserDataLength = consumerInfo.defaultBufferSize;
            native_consumerInfo.fnReadComp = null != consumerInfo.readDelegate
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.readDelegate)
                : IntPtr.Zero;
            native_consumerInfo.fnWriteComp = null != consumerInfo.writeDelegate
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
