// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SniDnsCacheInfo
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
}

#endif
