// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

namespace Interop.Windows.Sni
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SniClientConsumerInfo
    {
        public SniConsumerInfo ConsumerInfo;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string wszConnectionString;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string HostNameInCertificate;
        public Prefix networkLibrary;
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
        public SniDnsCacheInfo DNSCacheInfo;
    }
}

#endif
