// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
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
    }
}

#endif
